## Why

JSON payloads with misspelled or unsupported property names are silently ignored during deserialization. This means typos like `"fistName"` or `"adress"` succeed without warning, leading to patches that appear valid but apply no changes. Teams that care about API contract correctness need a way to reject unrecognized properties at the deserialization boundary.

## What Changes

- Add an `UnknownPropertyHandling` enum (`Ignore`, `Reject`) to the core library.
- Add an `UnknownPropertyHandling` property to `[PatchDocument]` (default: `Ignore`).
- When `Reject` is configured, both the generated converter and the resolver-emitted `JsonTypeInfo` reject payloads containing unrecognized JSON properties.
- Each type's own attribute setting is authoritative — no cross-type propagation or aggregation.
- Nested `[PatchDocument]` deserialization continues to use `JsonSerializer.Deserialize<T>(ref reader, options)` — no contract changes.
- The existing silent-ignore behavior remains the default — no breaking changes.

## Capabilities

### New Capabilities
- `unknown-property-handling`: Opt-in rejection of unrecognized JSON properties during deserialization, enforced on both converter and resolver paths.

### Modified Capabilities
- `serialization`: The generated converter gains a new code path that checks property names against the known set and optionally throws on unknown properties. The resolver-emitted `JsonTypeInfo` gains `UnmappedMemberHandling = Disallow` on .NET 8+ for Reject-mode types.

## Impact

- **Core library** (`Patchly`): New `UnknownPropertyHandling` enum, updated `PatchDocumentAttribute` with new property.
- **Source generator** (`Patchly.Generators`): Converter emission branches on `UnknownPropertyHandling` to emit collect-or-skip logic in the unmatched property branch. Resolver emission sets `UnmappedMemberHandling` on the `JsonTypeInfo` for Reject-mode streaming-path types.
- **Tests**: New test class for unknown-property scenarios covering both converter and resolver paths. Existing `UnknownProperties_SilentlyIgnored` test remains valid (covers `Ignore` mode).
- **Public API**: Additive only. No breaking changes. Nested deserialization contract preserved.
