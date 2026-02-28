## Context

Patchly already exposes presence tracking (`WasProvided`, `ProvidedProperties`, `Provided`), but downstream PATCH handlers still duplicate logic to interpret three distinct states: omitted, explicit `null`, and explicit value. This is especially error-prone for nested objects and collections where teams often disagree on whether payloads should merge, clear, or replace.

The design goal is to make those semantics explicit and deterministic while preserving backward compatibility and current performance characteristics.

Constraints:
- Runtime library targets `net6.0`; generator targets `netstandard2.0`.
- `System.Text.Json` only.
- No runtime reflection.

## Goals / Non-Goals

**Goals:**
- Provide a first-class tri-state contract for each tracked property.
- Make deterministic semantics available per patch DTO via explicit opt-in.
- Define nested and collection behavior clearly enough to encode as tests and docs.
- Keep behavior parity across streaming and buffered deserialization paths.

**Non-Goals:**
- Automatic entity merge/mapping engine.
- RFC 6902 JSON Patch operation support.
- Collection merge strategies (append, key-based merge, remove-by-key) in V1.
- Changing the default behavior for existing `[PatchDocument]` classes.

## Decisions

### 1) Add explicit tri-state surface

Decision:
- Introduce `PatchValueState` with `Omitted`, `Null`, `Value`.
- Extend `IPatchDocument` with `GetState(string propertyName)`.

Rationale:
- `WasProvided` is useful but binary; many API handlers need explicit `null` vs non-null distinction.
- Interface-level API enables generic middleware and mapping layers to consume semantics consistently.

Alternative considered:
- Keep only `WasProvided` and document patterns. Rejected because it preserves ambiguity and repeated custom logic.

### 2) Opt-in deterministic mode on attribute

Decision:
- Add `PatchSemanticsMode` and a `SemanticsMode` property on `[PatchDocument]`.
- Default remains legacy behavior.

Rationale:
- Avoids breaking existing consumers while enabling stricter semantics for new or migrated DTOs.

Alternative considered:
- Flip default globally. Rejected due to compatibility risk in minor release cadence.

### 3) Generate strongly-typed state accessor

Decision:
- In deterministic mode, generator emits `State` accessor and nested `StateSet` with per-property `PatchValueState` values.

Rationale:
- Improves ergonomics and discoverability compared with string-only lookups.
- Keeps implementation reflection-free and AOT-safe.

Alternative considered:
- Only `GetState(string)` API. Rejected because typed access reduces mistakes and improves IDE guidance.

### 4) Deterministic semantics contract for nested and collections

Decision:
- Nested object: omitted -> no-op, null -> clear, value -> apply nested partial update semantics.
- Collection (V1): omitted -> no-op, null -> clear, value (including empty collection) -> replace.

Rationale:
- This is the smallest deterministic contract that removes common ambiguity.

Alternative considered:
- Merge semantics by default for collections. Rejected because merge rules are domain-specific and often surprising.

### 5) Guardrail diagnostics in deterministic mode

Decision:
- Add warning for non-nullable collection properties in deterministic mode.
- Keep existing property validation diagnostics and update wording where needed.

Rationale:
- Non-nullable collections obscure clear-vs-replace intent and lead to accidental behavior.

## Risks / Trade-offs

- [API surface growth] -> Keep additions minimal (`PatchValueState`, `PatchSemanticsMode`, `GetState`, generated `State`).
- [User confusion about automatic mapping] -> Document that Patchly provides semantics and tracking, not domain merge policy.
- [Compatibility drift between streaming and buffered paths] -> Mirror scenario coverage across both paths in tests.
- [Initial migration friction] -> Ship opt-in mode with migration examples and warnings instead of hard errors.

## Migration Plan

1. Ship additive APIs and deterministic mode opt-in in one release.
2. Keep legacy mode as default.
3. Add README migration section with before/after handler examples.
4. Gather usage feedback/issues; evaluate recommending deterministic mode by default in a future major release.

Rollback:
- Consumers can revert to legacy semantics by removing deterministic mode opt-in on `[PatchDocument]` classes.

## Open Questions

- Should unknown property names passed to `GetState` return `Omitted` or be configurable for strict behavior later?
- Should deterministic-mode guardrails ever escalate to errors, or remain warnings permanently?
- Is a future collection strategy extension point needed, or should strategy remain application-level by design?
