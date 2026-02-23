## Context

`IPatchDocument` currently provides `WasProvided(string)` and `ProvidedProperties` for tracking which properties were present in the JSON payload. Consumers who need the actual values (for audit logging, change forwarding, or debugging) must manually iterate and extract them using reflection or per-property code.

The source generator already knows all tracked property names and types at compile time. It can emit a method body that builds a dictionary from the internal `_providedProperties` HashSet and the property values.

## Goals / Non-Goals

**Goals:**
- Add `GetProvidedValues()` to `IPatchDocument` returning `IReadOnlyDictionary<string, object?>`
- Generator emits the implementation — no reflection, AOT-safe
- Only includes properties that were actually provided (present in JSON payload)

**Non-Goals:**
- Deep cloning of values (returns current references/values)
- Caching the dictionary across calls (fresh dictionary each call)
- Typed variants (`GetProvidedValues<T>()`) — the `object?` boxing is acceptable for introspection

## Decisions

### Decision 1: Method on IPatchDocument, not just the generated class

The method lives on `IPatchDocument` so it works in generic contexts (middleware, audit infrastructure). The generator emits the implementation in the partial class.

Alternative: Generated method only (not on interface). Rejected because the primary use cases (audit middleware, generic logging) require working through the interface.

### Decision 2: Return IReadOnlyDictionary<string, object?>

Keys are C# property names (matching `ProvidedProperties`). Values are the current property values, boxed for value types.

Alternative: `IEnumerable<KeyValuePair<string, object?>>` — rejected because dictionary provides O(1) lookup by name, which callers will want for selective logging.

### Decision 3: Fresh dictionary per call

Each call to `GetProvidedValues()` creates a new `Dictionary<string, object?>`. No caching.

Rationale: The method is an introspection API used for logging/audit — not a hot path. Caching adds complexity (invalidation if properties are mutated after deserialization) for no practical benefit.

### Decision 4: Keys match ProvidedProperties exactly

The dictionary keys MUST be the same strings as those in `ProvidedProperties` (C# property names). This ensures consistency across the API surface.

## Risks / Trade-offs

- [Boxing value types] → Acceptable for introspection use cases. Document in XML docs that this is not a high-performance API.
- [Interface addition] → Non-breaking because only the generator implements the interface. No user code implements `IPatchDocument` manually.
- [Mutable values] → Dictionary values are references to the patch's properties. If a caller mutates a reference-type value, it affects the patch. This is standard .NET behavior and not worth defensive copying.
