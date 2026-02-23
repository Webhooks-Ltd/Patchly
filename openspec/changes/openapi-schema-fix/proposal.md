## Why

ASP.NET Core 9+ uses `JsonSchemaExporter` to generate OpenAPI schemas. When a type has `[JsonConverter]`, its `JsonTypeInfo.Kind` is `JsonTypeInfoKind.None` — meaning the type's serialization is fully controlled by a custom converter and cannot be introspected. `JsonSchemaExporter` produces an empty schema (`{ }`) for these types. Since Patchly emits `[JsonConverter(typeof(...))]` on every `[PatchDocument]` class, all patch documents have empty OpenAPI schemas. This is a known .NET runtime limitation ([dotnet/runtime#105769](https://github.com/dotnet/runtime/issues/105769), targeted for .NET 11).

## What Changes

- On NET8+, the `PatchlyJsonTypeInfoResolver` will return `JsonTypeInfo` with `Kind = Object` and populated `Properties` collection instead of wrapping the converter via `CreateValueInfo`. Each `JsonPropertyInfo` will have custom `Get`/`Set` delegates — the `Set` delegate both assigns the property value and adds to `_providedProperties` for tracking.
- The `[JsonConverter]` attribute remains on the class as a fallback for users who don't configure the resolver (e.g., direct `JsonSerializer.Deserialize` without custom options).
- When the resolver is in the `TypeInfoResolverChain` (inserted before `DefaultJsonTypeInfoResolver`), it takes priority over the `[JsonConverter]` attribute. `JsonSchemaExporter` sees `Kind = Object` with properties and produces correct schemas.
- A source-generated `AddPatchly()` extension method on `IServiceCollection` will register the resolver into ASP.NET Core's HTTP JSON options (both minimal API and MVC JSON options).
- Scope: the `Kind = Object` resolver path covers streaming-path types (parameterless constructor + settable properties). Buffered-path types (init-only / `[JsonConstructor]`) fall back to the existing converter via `CreateValueInfo` (empty schemas remain for these types until a follow-up change).
- Integration tests using `WebApplicationFactory` will verify OpenAPI schemas contain correct property definitions.

### Rejected alternatives

- **`TransformSchemaNode` post-processing (NET9+)**: Could fix schemas by post-processing, but requires per-assembly schema transformer configuration, doesn't improve the resolver/AOT story, and only works on NET9+.
- **Remove `[JsonConverter]` entirely on NET8+**: Would break users who call `JsonSerializer.Deserialize` without configuring the resolver. Keeping the attribute as fallback is safer.

## Capabilities

### New Capabilities
- `openapi-integration`: Source-generated extension method for ASP.NET Core service registration and resolver-based deserialization on NET8+

### Modified Capabilities
- `source-generation`: Generated resolver returns `Kind = Object` type info instead of converter-wrapped `Kind = None`
- `native-aot-support`: Resolver becomes the primary deserialization path on NET8+ (not just an AOT helper)
- `openapi-compatibility`: Schemas work automatically when resolver is registered, with documented setup

## Impact

- `PatchlyJsonTypeInfoResolver.g.cs` — major rewrite of generated resolver code to emit `Kind = Object` type info with property metadata
- `PatchDocumentGenerator.cs` — generator emits new resolver code shape and `AddPatchly()` extension method
- `AddPatchly()` is source-generated (references assembly-specific internal `PatchlyJsonTypeInfoResolver.Default`)
- New integration test project with `Microsoft.AspNetCore.OpenApi` and `WebApplicationFactory` dependencies
- Sample projects updated to show `builder.Services.AddPatchly()` registration
- Write path: when resolver is active, STJ uses its own property-based serialization. `[JsonIgnore]` on tracking members prevents infrastructure leakage. `DefaultIgnoreCondition` handled natively by STJ property pipeline.
