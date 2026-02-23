## Context

The generator currently emits `[JsonConverter(typeof(TJsonConverter))]` on every `[PatchDocument]` class. The `PatchlyJsonTypeInfoResolver` (NET8+) wraps this converter via `JsonMetadataServices.CreateValueInfo<T>(options, converter)`, which produces `JsonTypeInfo.Kind = None`. On NET6/7, the attribute alone drives deserialization.

ASP.NET Core 9+ calls `JsonSchemaExporter.GetJsonSchemaAsNode()` which cannot introspect `Kind = None` types. Result: empty schemas.

The resolver is generated as `internal sealed` in the `Patchly` namespace. The converter is generated as `internal sealed` nested inside each `[PatchDocument]` class. The `_providedProperties` field is `private` on the generated partial class — accessible to the nested converter but not to the resolver.

## Goals / Non-Goals

**Goals:**
- OpenAPI schemas for `[PatchDocument]` types show all tracked properties with correct types and nullability when the resolver is registered
- Resolver returns `Kind = Object` type info with populated `Properties` on NET8+ for streaming-path types
- Source-generated `AddPatchly()` extension method for ASP.NET Core minimal API service registration
- `[JsonConverter]` remains as fallback when resolver is not configured
- Integration tests proving OpenAPI schemas are correct

**Non-Goals:**
- Changing anything for NET6/7 (converter-only path stays as-is)
- Removing `[JsonConverter]` attribute from generated code
- Supporting users who put `PatchlyJsonTypeInfoResolver` after `DefaultJsonTypeInfoResolver` in the chain
- Polymorphic `[JsonDerivedType]` support
- MVC `JsonOptions` auto-configuration (documented manual setup instead)

## Decisions

### Decision 1: Resolver returns Object-kinded JsonTypeInfo for streaming-path types

On NET8+, change from:
```csharp
JsonMetadataServices.CreateValueInfo<T>(options, new TConverter())
```
To building a `JsonTypeInfo<T>` via `JsonTypeInfo.CreateJsonTypeInfo<T>(options)` with:
- `CreateObject` factory
- `JsonPropertyInfo` for each tracked property with custom `Get`/`Set` delegates
- The `Set` delegate assigns the value AND calls `MarkProvided(name)`

`CreateJsonTypeInfo<T>` does NOT resolve `[JsonConverter]` from the type — it uses built-in converters only. This means even though `[JsonConverter]` is on the class, the resolver gets `Kind = Object` automatically.

This produces a `JsonTypeInfo` with populated `Properties` that `JsonSchemaExporter` can introspect.

Alternative: Use `TransformSchemaNode` to post-process schemas. Rejected because it only fixes the symptom (schema), not the underlying architecture.

### Decision 2: Buffered-path types fall back to converter

`CreateJsonTypeInfo<T>` (the public API) does not support parameterized constructor binding. The internal `JsonMetadataServices.CreateObjectInfo<T>` + `JsonObjectInfoValues<T>` API does, but it's documented as "for use by STJ source generator only" and coupling to it is fragile across .NET versions.

For types using the buffered path (init-only properties or `[JsonConstructor]`), the resolver continues to use `CreateValueInfo` with the converter. These types will still have `Kind = None` and empty OpenAPI schemas.

This is acceptable because:
- The vast majority of `[PatchDocument]` classes use settable properties (streaming path)
- Buffered-path support can be added in a follow-up via `CreateObjectInfo<T>` if validated per .NET version
- Users needing OpenAPI schemas for buffered-path types can use `TransformSchemaNode` as a workaround

### Decision 3: Expose _providedProperties to resolver via internal method

Add a generated `internal void MarkProvided(string name)` method to each `[PatchDocument]` class. The resolver's `Set` delegates call this method instead of accessing `_providedProperties` directly. `internal` visibility works because both the partial class and the resolver are generated into the same assembly.

Alternative: Make `_providedProperties` internal. Rejected — exposes implementation detail beyond what's needed.

### Decision 4: Streaming-path types use Set delegates

For types with parameterless constructor + settable properties:
- `CreateObject` returns `new T()` (or `new T(false)` for `required` members)
- Each `JsonPropertyInfo.Set` delegate: assigns property value, calls `MarkProvided`
- Each `JsonPropertyInfo.Get` delegate: reads property value
- STJ handles the read loop, property matching, and naming policy natively

This replaces the converter's streaming read loop entirely when the resolver is active.

### Decision 5: Source-generated AddPatchly() extension (minimal API only)

Generate a `PatchlyServiceCollectionExtensions` class with:
```csharp
public static IServiceCollection AddPatchly(this IServiceCollection services)
```
That calls:
```csharp
services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, PatchlyJsonTypeInfoResolver.Default));
```

This is source-generated because it references the assembly-specific `PatchlyJsonTypeInfoResolver.Default`. Gated behind `#if NET8_0_OR_GREATER`. Only emitted when the compilation references `Microsoft.AspNetCore.Http` (detected by checking referenced assemblies for `ConfigureHttpJsonOptions` availability). Non-web projects (console apps, libraries) get the resolver but not the extension method.

MVC users configure manually:
```csharp
services.Configure<JsonOptions>(o =>
    o.JsonSerializerOptions.TypeInfoResolverChain.Insert(0, PatchlyJsonTypeInfoResolver.Default));
```

This avoids any dependency on `Microsoft.AspNetCore.Mvc.Core` and is fully AOT-safe.

### Decision 6: Write path uses STJ property pipeline

When the resolver is active, STJ serialization uses its own property-based write path (not the converter's `Write` method). This means:
- `[JsonIgnore]` on tracking members prevents infrastructure leakage (already in place)
- `DefaultIgnoreCondition` handled natively by STJ (including per-property `[JsonIgnore(Condition = ...)]`)
- `[JsonPropertyName]` and naming policies work automatically
- The converter's custom `Write` logic (`ShouldWriteProperty`, `ResolvePropertyName`) is only used when the resolver is NOT active

Known behavioral differences: STJ's pipeline respects per-property `[JsonIgnore(Condition = ...)]` which the current converter ignores. This is an improvement, not a regression.

### Decision 7: Resolver ordering must be documented

`PatchlyJsonTypeInfoResolver` MUST come before `DefaultJsonTypeInfoResolver` in the chain. If it comes after, `DefaultJsonTypeInfoResolver` sees `[JsonConverter]` first and returns `Kind = None`. The `AddPatchly()` extension handles this automatically (`Insert(0, ...)`). Document for manual configuration.

Also document: adding Patchly converters directly to `options.Converters` is unsupported when the resolver is active — `CreateJsonTypeInfo<T>` resolves from `options.Converters`, and having the converter there would produce `Kind = None`.

## Risks / Trade-offs

- [Write path behavioral change] → When resolver is active, serialization uses STJ's property pipeline instead of custom converter. Risk of subtle differences in `DefaultIgnoreCondition` handling or property ordering. → Mitigate with comprehensive round-trip tests comparing converter vs resolver output.
- [Buffered-path types have empty schemas] → Acceptable for now. Most `[PatchDocument]` classes use settable properties. → Document limitation and workaround (`TransformSchemaNode`). Follow-up change can add `CreateObjectInfo` support.
- [Two-resolver ordering] → If user has their own `JsonSerializerContext` AND `PatchlyJsonTypeInfoResolver`, ordering matters. → Document that `AddPatchly()` inserts at position 0 and should be called after `AddContext<T>()`.
- [AddPatchly() requires ASP.NET Core references] → `ConfigureHttpJsonOptions` requires `Microsoft.AspNetCore.Http` assembly. → The extension is only generated when targeting a web project. If not available, users configure `JsonSerializerOptions` directly.
