## Why

`IPatchDocument` exposes `ProvidedProperties` (property names) and `WasProvided(string)` (per-property check), but there is no way to get the actual values of provided properties as key-value pairs. This forces consumers to write manual reflection or per-property extraction code for audit logging, change tracking, and forwarding partial updates to downstream services.

## What Changes

- `IPatchDocument` SHALL gain a `GetProvidedValues()` method returning `IReadOnlyDictionary<string, object?>` containing only the properties that were present in the JSON payload, keyed by C# property name
- The source generator SHALL emit the implementation, boxing value types as needed
- The method MUST be AOT-safe (no reflection)

## Capabilities

### New Capabilities

- `provided-values-introspection`: A method on `IPatchDocument` that returns provided property names and their current values as a dictionary

### Modified Capabilities

- `provided-accessor`: `IPatchDocument` gains a new method alongside `WasProvided` and `ProvidedProperties`
- `source-generation`: Generator emits `GetProvidedValues()` implementation in the generated partial class

## Impact

- `src/Patchly/IPatchDocument.cs` — add `GetProvidedValues()` to interface
- `src/Patchly.Generators/PatchDocumentGenerator.cs` — emit method body that checks `_providedProperties` and builds dictionary
- `tests/Patchly.Tests/` — new tests for the method
- `README.md` — document the new method
- No breaking changes — additive interface change only (consumers implement via generated code, not manually)
