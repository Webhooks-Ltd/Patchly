## 1. Generated Class Changes

- [ ] 1.1 Add `internal void MarkProvided(string name) => _providedProperties.Add(name);` to the generated partial class

## 2. Resolver Rewrite (NET8+)

- [ ] 2.1 For streaming-path types, change resolver from `CreateValueInfo` to `JsonTypeInfo.CreateJsonTypeInfo<T>(options)` with `CreateObject` factory and per-property `JsonPropertyInfo` with `Get`/`Set` delegates
- [ ] 2.2 The `Set` delegate on each property SHALL assign the value AND call `MarkProvided(propertyName)`
- [ ] 2.3 The `Get` delegate on each property SHALL read the property value for serialization
- [ ] 2.4 For buffered-path types, keep `CreateValueInfo` with the converter (no change)
- [ ] 2.5 Branch on `UseBufferedDeserialization` (already on `PatchClassModel`) in `GenerateResolver` to emit `CreateJsonTypeInfo` vs `CreateValueInfo`
- [ ] 2.6 Handle `[JsonPropertyName]` overrides on properties — pass explicit JSON name to `JsonPropertyInfo`
- [ ] 2.7 Handle `[JsonIgnore]` properties — exclude from resolver's `Properties` collection
- [ ] 2.8 Handle `[JsonNumberHandling]` on properties — set `NumberHandling` on `JsonPropertyInfo`
- [ ] 2.9 Handle `required` members — `CreateObject` uses `new T(false)` sentinel constructor when needed

## 3. AddPatchly Extension Method

- [ ] 3.1 Generate `PatchlyServiceCollectionExtensions` class with `AddPatchly()` that inserts resolver at position 0 in `ConfigureHttpJsonOptions`
- [ ] 3.2 Gate behind `#if NET8_0_OR_GREATER`
- [ ] 3.3 Only emit when compilation references `Microsoft.AspNetCore.Http` (detect via referenced assemblies)
- [ ] 3.4 Generate in `PatchlyServiceCollectionExtensions.g.cs` hint name

## 4. Integration Test Project

- [ ] 4.1 Create `tests/Patchly.IntegrationTests/` project with `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.Mvc.Testing`, and `WebApplicationFactory`
- [ ] 4.2 Create test `[PatchDocument]` types: streaming-path and buffered-path
- [ ] 4.3 Create minimal API test app with `AddPatchly()` and `AddOpenApi()`

## 5. OpenAPI Schema Tests

- [ ] 5.1 Test: streaming-path type produces schema with all tracked properties and correct types
- [ ] 5.2 Test: properties are nullable in the schema
- [ ] 5.3 Test: tracking infrastructure (`_providedProperties`, `WasProvided`, `ProvidedProperties`, `Provided`) not in schema
- [ ] 5.4 Test: nested converter type not in schema
- [ ] 5.5 Test: buffered-path type produces empty schema (known limitation)
- [ ] 5.6 Test: without `AddPatchly()`, streaming-path type also has empty schema (baseline)

## 6. Resolver Deserialization Tests

- [ ] 6.1 Test: resolver path — null vs absent distinction works
- [ ] 6.2 Test: resolver path — Provided accessor works
- [ ] 6.3 Test: resolver path — deserialization matches converter path (identical WasProvided and property values)
- [ ] 6.4 Test: resolver path — serialization excludes tracking infrastructure
- [ ] 6.5 Test: resolver path — `[JsonPropertyName]` override respected
- [ ] 6.6 Test: resolver path — naming policy (camelCase, snake_case) respected
- [ ] 6.7 Test: resolver path — `DefaultIgnoreCondition.WhenWritingNull` respected in serialization
- [ ] 6.8 Test: resolver path — `[JsonIgnore]` properties excluded
- [ ] 6.9 Test: converter fallback — deserialization works correctly without resolver in chain (regression test)

## 7. End-to-End Integration Tests

- [ ] 7.1 Test: PATCH endpoint round-trip via `WebApplicationFactory` with `AddPatchly()` — send partial JSON, verify property tracking and response
- [ ] 7.2 Test: PATCH endpoint works without `AddPatchly()` via converter fallback (deserialization correct, OpenAPI broken)

## 8. Documentation

- [ ] 8.1 Update `README.md` with `AddPatchly()` setup instructions, OpenAPI section, and resolver ordering caveat (do not add converter to `options.Converters` when resolver is active)
- [ ] 8.2 Update `CHANGELOG.md` under `[Unreleased]`
- [ ] 8.3 Update sample `MinimalApi` project to use `AddPatchly()` and `AddOpenApi()`
