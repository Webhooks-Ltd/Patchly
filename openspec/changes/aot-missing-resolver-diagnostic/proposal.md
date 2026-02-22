## Why

When an AOT app uses `[PatchDocument]` types but forgets to add `PatchlyJsonTypeInfoResolver` to the resolver chain, deserialization fails at runtime with a confusing "metadata not provided" error. A compile-time diagnostic would catch this before deployment.

## What Changes

- Add a PATCH016 analyzer diagnostic that fires when the project has `PublishAot=true` (or uses `CreateSlimBuilder`) and `[PatchDocument]` types exist but `PatchlyJsonTypeInfoResolver` is not referenced in startup code
- Severity: Warning
- This is a best-effort heuristic — it may not catch all cases (e.g., resolver added via extension method indirection)

## Capabilities

### New Capabilities

- `aot-missing-resolver-diagnostic`: Compile-time warning when AOT project is missing resolver registration

## Impact

- `src/Patchly.Generators/` — new analyzer (separate from the generator, or combined)
- Tests for diagnostic triggering and suppression
- README.md — document PATCH016 diagnostic
- No breaking changes
