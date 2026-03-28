## Context

Patchly has two deserialization paths that both need to handle unknown properties:

1. **Converter path**: The generated `JsonConverter<T>.Read` method iterates JSON properties by name, matching each against known C# properties. Unmatched properties currently hit a `reader.Read(); reader.Skip();` branch in both the streaming and buffered codegen paths (`PatchDocumentGenerator.cs` lines ~877 and ~932). Used on all TFMs and as the sole path on .NET 6/7.

2. **Resolver path** (.NET 8+): `PatchlyJsonTypeInfoResolver` emits `JsonTypeInfo<T>` with per-property `Set` delegates for streaming-path types. When deserialized through the resolver, STJ handles property iteration — the generated converter's `Read` method is never called. The existing spec (`native-aot-support/spec.md:61`) and tests (`ResolverDeserializationTests.cs:56`) require resolver/converter parity.

The `PatchDocumentAttribute` already carries a `SemanticsMode` enum property that the generator reads at compile time. The same pattern applies here.

.NET 8 added `JsonUnmappedMemberHandling.Disallow` to System.Text.Json, which natively rejects unmapped properties on `JsonTypeInfo`. The resolver path can use this directly. The converter path cannot — it bypasses STJ's built-in unmapped member handling since the generated converter reads properties manually. The converter must therefore implement its own equivalent.

## Goals / Non-Goals

**Goals:**
- Allow individual patch document types to reject payloads containing unrecognized JSON properties.
- Report all unknown properties in a single error for the type being deserialized.
- Maintain resolver/converter parity — both paths accept or reject the same payloads.
- Keep the default behavior (`Ignore`) unchanged — fully backward compatible.
- Configuration is compile-time via the attribute, not runtime.
- Preserve the existing nested deserialization contract (`JsonSerializer.Deserialize<T>(ref reader, options)` for nested properties).

**Non-Goals:**
- Global/app-wide or environment-specific unknown property policy. Per-type attributes are the only configuration mechanism. Runtime/environment-based toggling may be considered in a future version if demand materializes.
- Runtime configuration via `JsonSerializerOptions` or DI.
- Warning mode (log but accept) — only `Ignore` and `Reject` for v1.
- Handling unknown properties inside non-`[PatchDocument]` nested objects (only Patchly-generated converters participate).
- Cross-type error aggregation or dotted path accumulation across nesting levels. Each type reports its own unknowns independently. Aggregation may be revisited in a future version if demand materializes.

## Decisions

### Decision 1: Attribute-level configuration, not runtime options

Add `UnknownPropertyHandling` as a property on `PatchDocumentAttribute`, mirroring the existing `SemanticsMode` pattern. The name follows STJ conventions (`JsonUnmappedMemberHandling`, `JsonNumberHandling`).

```csharp
public enum UnknownPropertyHandling { Ignore = 0, Reject = 1 }

[PatchDocument(UnknownPropertyHandling = UnknownPropertyHandling.Reject)]
public partial class StrictPatch { ... }
```

The generator reads the enum value from the attribute and emits different logic at compile time for both the converter and resolver paths.

**Why not runtime `PatchlyOptions`?** STJ converters are instantiated per-type by the serializer infrastructure. Threading a runtime options object into the converter requires either static/ambient state (untestable) or `TypeInfoResolver` plumbing (breaks the clean converter model). Attribute-level config keeps the zero-runtime-configuration property that makes Patchly clean. It also makes the behavior local to the patch type, which is the right granularity — some types may need strictness while others don't.

### Decision 2: Dual-path implementation — converter and resolver

Both deserialization paths must enforce unknown property rejection to maintain parity:

**Converter path** (all TFMs, buffered types on .NET 8+): The generated `Read` method collects unknown property names during its property loop. After the full object is read, if any unknowns were found, throw a `JsonException` listing all of them. Use lazy allocation: `List<string>? unknowns = null;` before the loop, `(unknowns ??= new List<string>()).Add(...)` in the unknown branch. Valid payloads pay zero allocation cost.

**Resolver path** (.NET 8+, streaming-path types): The generated `PatchlyJsonTypeInfoResolver.GetTypeInfo` sets `typeInfo.UnmappedMemberHandling` on the `JsonTypeInfo` for all `[PatchDocument]` types: `Disallow` for `Reject` mode, `Skip` for `Ignore` mode. Setting `Skip` explicitly for Ignore-mode types ensures the Patchly attribute takes precedence over any global `JsonSerializerOptions.UnmappedMemberHandling` the app may configure — without this, a globally-configured `Disallow` would break parity by rejecting unknown properties on the resolver path while the converter path ignores them. This is gated behind `#if NET8_0_OR_GREATER`.

Note: buffered-path types on .NET 8+ already fall back to the converter via `JsonMetadataServices.CreateValueInfo`, so the converter-path implementation covers them.

### Decision 3: Use JSON property names in error messages

Error messages report the JSON property name the client actually sent (e.g., `"zipCodee"`), not the C# property name. This matches what the caller sees in their payload.

**Converter path error message format:**
```
Unknown JSON properties on CustomerPatch: 'unknownProp'
Unknown JSON properties on CustomerPatch: 'foo', 'bar'
```
Single-quoted property names in a comma-separated list, prefixed with the type name. The `{TypeName}` is always the type whose converter throws. This format is for human consumption — callers should not parse it programmatically. Structured error reporting (e.g., for `ValidationProblemDetails` integration) may be considered in a future version.

**Resolver path error messages** use STJ's native `JsonException` format, which includes the JSON path (e.g., `$.address.zipCodee`). This is controlled by STJ, not by Patchly.

**Alternative considered:** C# paths everywhere. Rejected because error consumers are typically API clients who see JSON, not C# code. The introspection API (WasProvided, ProvidedProperties) rightly uses C# names because those consumers are writing C#.

### Decision 4: Add to PatchClassModel

The `UnknownPropertyHandling` setting applies to the class, not individual properties. Add an `UnknownPropertyHandling` field to `PatchClassModel`. The generator's `TransformType` reads the attribute value and populates the model. Both converter emitter and resolver emitter branch on this value.

### Decision 5: Each type's own setting is authoritative

Each `[PatchDocument]` type controls its own unknown property handling independently. A parent type with `Reject` does not override a nested type's `Ignore` setting, and vice versa. To enforce full-depth strictness, apply `Reject` to every type in the hierarchy.

Nested `[PatchDocument]` properties continue to be deserialized via `JsonSerializer.Deserialize<T>(ref reader, options)`, preserving the existing serializer delegation contract. This means:

- Each nested type's own converter (or resolver `JsonTypeInfo`) handles its own unknown property enforcement.
- Cross-assembly nested patch documents work exactly as before — no assembly co-location requirement.
- If a nested Reject-mode type encounters unknowns, it throws its own `JsonException` during the parent's deserialization loop. This interrupts the parent's loop, so the parent's own unknowns (if any) are not reported in that error. The developer fixes the nested error first, then the parent error surfaces on the next attempt.
- No cross-type error aggregation. Each type reports only its own unknowns.

## Risks / Trade-offs

**Risk: Converter and resolver error messages differ in format.**
→ Mitigation: The observable behavior (accept/reject) is identical on both paths. Only the error message format differs: converter uses `Unknown JSON properties on {TypeName}: ...`, resolver uses STJ's native `JsonException` with JSON path. Consumers should not parse error messages programmatically. Documented as an implementation detail.

**Risk: No cross-nesting error aggregation — multiple unknowns at different nesting levels require multiple fix-and-retry cycles.**
→ Mitigation: This matches STJ's own `JsonUnmappedMemberHandling.Disallow` behavior, which also throws on the first type that encounters an unmapped member. Consumers are used to this pattern. Cross-nesting aggregation may be revisited in a future version if demand materializes.

**Risk: Collecting all unknowns allocates a list on every Reject-mode deserialization, even for valid payloads.**
→ Mitigation: Use lazy allocation — only create the list when the first unknown is encountered. Valid payloads pay zero cost. A `List<string>` with a handful of entries on an invalid payload is negligible.

**Risk: `JsonPropertyName` overrides and naming policies affect what counts as "known".**
→ Mitigation: The matching logic already handles this via `MatchesPropertyName` in the converter path and via STJ's property metadata in the resolver path. Unknown detection reuses the same matching.

**Risk: `[JsonIgnore]` properties sent in the payload appear as unknown under Reject mode.**
→ This is correct behavior — the client is sending something that will be silently dropped, and Reject mode means strict contract enforcement. Documented as intentional. Applies to both converter and resolver paths (ignored properties are excluded from tracked/metadata properties in both).

**Risk: A JSON property name containing `.` would be ambiguous in a dotted path format.**
→ Not applicable in v1 — error messages report property names relative to the current type only, with no dotted path accumulation. Each type's error contains only its own direct property names.
