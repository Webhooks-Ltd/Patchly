## 1. Generator Pipeline Changes

- [x] 1.1 Add `FullyQualifiedName` field to `PatchClassModel` using `symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)` (includes containing types for nested classes)
- [x] 1.2 Populate `FullyQualifiedName` in `TransformType` method
- [x] 1.3 Change generated converter visibility from `private sealed` to `internal sealed` in `GenerateJsonConverter`
- [x] 1.4 Add `Collect()` pipeline step in `Initialize()` to gather all valid models, wrapped in `EquatableArray<PatchClassModel>` for proper incremental caching
- [x] 1.5 Add `GenerateResolver()` method that emits `PatchlyJsonTypeInfoResolver.g.cs` (skip if no valid models)
- [x] 1.6 Wire the `Collect()` output to `GenerateResolver()` via `RegisterSourceOutput`
- [x] 1.7 Ensure resolver handles nested `[PatchDocument]` types (fully-qualified type names including containing types)

## 2. Testing

- [x] 2.1 Run existing unit tests to verify converter visibility change doesn't break anything
- [x] 2.2 Run existing integration tests to verify non-AOT behaviour is unchanged
- [x] 2.3 Add unit test verifying `PatchlyJsonTypeInfoResolver` is generated when `[PatchDocument]` types exist
- [x] 2.4 Add unit test verifying resolver is NOT generated when no `[PatchDocument]` types exist
- [x] 2.5 Add unit test verifying the resolver returns correct `JsonTypeInfo` for known types and null for unknown types
- [x] 2.6 Add unit test verifying nested `[PatchDocument]` types are handled correctly in the resolver
- [x] 2.7 Add integration test verifying AOT-like deserialization with manually configured `JsonSerializerOptions` (no reflection, resolver chain only)
- [x] 2.8 Add integration test verifying resolver works with different `JsonSerializerOptions` (e.g., different naming policies)

## 3. Documentation

- [x] 3.1 Add Native AOT section to README.md documenting `PatchlyJsonTypeInfoResolver` setup — emphasise resolver must come before the user's `JsonSerializerContext` in the chain
- [x] 3.2 Note that property-level types (e.g., `List<string>`) must be in the user's `JsonSerializerContext`
- [x] 3.3 Update the "How It Compares" table to note AOT support
