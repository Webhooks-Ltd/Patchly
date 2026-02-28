## Why

After `openapi-schema-fix`, buffered-path `[PatchDocument]` types (init-only properties or `[JsonConstructor]`) still produce empty OpenAPI schemas because the resolver falls back to `CreateValueInfo` with `Kind = None`. This follow-up generates a `TransformSchemaNode` callback that fills in the correct schema for these types using compile-time property metadata.

## What Changes

- Source generator emits a `PatchlySchemaTransformer` class (NET9+) that implements schema post-processing for buffered-path `[PatchDocument]` types
- The transformer populates `schema.Type`, `schema.Properties` for types where the resolver returns `Kind = None`
- Exposed via `options.AddSchemaTransformer(PatchlySchemaTransformer.Default)` or integrated into `AddPatchly()`
- Streaming-path types are unaffected (already correct via resolver)

## Capabilities

### New Capabilities
- `schema-transformer`: Source-generated OpenAPI schema transformer for buffered-path types

### Modified Capabilities
- `openapi-compatibility`: Buffered-path types produce correct schemas when transformer is registered

## Impact

- New generated file `PatchlySchemaTransformer.g.cs` (NET9+ only)
- `AddPatchly()` may optionally register the transformer automatically
- Integration tests verifying buffered-path schemas are correct
