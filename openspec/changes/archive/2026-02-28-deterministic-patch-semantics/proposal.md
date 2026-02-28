## Why

Patchly tracks whether a property was present in JSON, but teams still implement PATCH behavior inconsistently for omitted values, explicit nulls, nested objects, and collections. That ambiguity causes accidental overwrites and no-op bugs in production APIs, which lowers trust in the library.

## What Changes

- Introduce deterministic PATCH semantics as an explicit opt-in mode for `[PatchDocument]` types.
- Add a first-class tri-state API for provided fields: `Omitted`, `Null`, and `Value`.
- Define deterministic behavior for nested objects and collections (replace semantics for collections in V1).
- Add generator diagnostics that warn when DTO shapes are ambiguous for deterministic semantics.
- Add tests and docs that lock in behavior across streaming and buffered deserialization paths.

## Capabilities

### New Capabilities

- `deterministic-patch-semantics`: Defines and exposes tri-state field semantics with deterministic nested and collection behavior.

### Modified Capabilities

- `patch-document-attribute`: Adds deterministic semantics mode configuration and `IPatchDocument` state lookup contract.
- `source-generation`: Emits deterministic state accessors and lookup implementation.
- `serialization`: Specifies deterministic state derivation and collection replace semantics from JSON payloads.

## Impact

- `src/Patchly/` public API additions for deterministic semantics mode and tri-state value state.
- `src/Patchly.Generators/` generated members and diagnostics for deterministic mode.
- `tests/Patchly.Tests/` and integration tests for scalar, nested, and collection scenarios.
- `README.md` usage guidance and migration examples.
- `CHANGELOG.md` updates under `[Unreleased]`.

## Assumptions

- Backward compatibility is required for existing consumers, so deterministic semantics ship as opt-in.
- Collection behavior in this change is replace-only; merge/append strategies are out of scope.
- Implementation remains source-generated, AOT-safe, and free of runtime reflection.
