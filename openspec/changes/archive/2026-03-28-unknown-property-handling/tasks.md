## 1. Core Library Types

- [x] 1.1 Add `UnknownPropertyHandling` enum (`Ignore = 0`, `Reject = 1`) to `src/Patchly/`
- [x] 1.2 Add `UnknownPropertyHandling` property to `PatchDocumentAttribute` with default `Ignore`

## 2. Generator Model

- [x] 2.1 Add `UnknownPropertyHandling` field to `PatchClassModel`
- [x] 2.2 Read `UnknownPropertyHandling` from the attribute in `TransformType` and populate the model

## 3. Converter Emission — Reject Logic

- [x] 3.1 Emit unknown property collection in the streaming `Read` path (replace `reader.Skip()` branch with conditional collect-or-skip based on `UnknownPropertyHandling`)
- [x] 3.2 Emit unknown property collection in the buffered `Read` path (same pattern)
- [x] 3.3 Emit post-loop `JsonException` throw when unknown list is non-empty, using format: `Unknown JSON properties on {TypeName}: '{name1}', '{name2}'`
- [x] 3.4 Use lazy allocation: `List<string>? unknowns = null;` with `(unknowns ??= new List<string>()).Add(...)` — zero cost for valid payloads

## 4. Resolver Emission — Reject Logic (.NET 8+)

- [x] 4.1 In `GenerateResolver`, for streaming-path types with `Reject`, emit `typeInfo.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow;`
- [x] 4.2 For Ignore-mode types, emit `typeInfo.UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip;` to ensure Patchly attribute wins over any global `JsonSerializerOptions.UnmappedMemberHandling`
- [x] 4.3 Verify buffered-path types still fall through to converter via `CreateValueInfo` (no resolver change needed for buffered types)

## 5. Tests — Ignore Mode (Regression)

- [x] 5.1 Verify existing `UnknownProperties_SilentlyIgnored` test still passes
- [x] 5.2 Add test: default attribute (no `UnknownPropertyHandling`) ignores unknown properties

## 6. Tests — Reject Mode (Converter Path)

- [x] 6.1 Add test: single unknown top-level property throws `JsonException` with property name in specified message format
- [x] 6.2 Add test: multiple unknown top-level properties lists all in error message
- [x] 6.3 Add test: all known properties with Reject mode succeeds normally
- [x] 6.4 Add test: empty object `{}` with Reject mode succeeds
- [x] 6.5 Add test: null token with Reject mode returns null without error
- [x] 6.6 Add test: duplicate known property names with Reject mode succeeds (last value wins)
- [x] 6.7 Add test: `[JsonPropertyName]` override — known property recognized, unknown sibling rejected with JSON name
- [x] 6.8 Add test: case-insensitive matching — `FIRSTNAME` matches `FirstName`, not reported as unknown
- [x] 6.9 Add test: `[JsonIgnore]` property sent in payload is treated as unknown under Reject mode
- [x] 6.10 Add test: buffered path (init-only properties) with Reject mode rejects unknowns

## 7. Tests — Reject Mode (Resolver Path, .NET 8+)

- [x] 7.1 Add test: resolver path rejects unknown properties for streaming-path Reject-mode type
- [x] 7.2 Add test: resolver path accepts valid payload for Reject-mode type
- [x] 7.3 Add test: resolver/converter parity — both paths reject the same unknown payload
- [x] 7.4 Add test: resolver/converter parity — both paths accept the same valid payload
- [x] 7.5 Add test: resolver path sets `UnmappedMemberHandling = Skip` for Ignore-mode types (ignores unknowns even with global Disallow)

## 8. Tests — Nested Behavior

- [x] 8.1 Add test: parent Reject with child Ignore — child ignores its own unknowns, parent succeeds
- [x] 8.2 Add test: parent Ignore with child Reject — only child throws for its own unknowns
- [x] 8.3 Add test: both Reject, unknowns only at child level — child throws independently
- [x] 8.4 Add test: both Reject, unknowns at both levels — child error surfaces first
- [x] 8.5 Add test: null nested patch document under Reject mode handled without error

## 9. Documentation

- [x] 9.1 Update README.md with unknown property handling section
- [x] 9.2 Update CHANGELOG.md under `[Unreleased]`
- [x] 9.3 Update `docs/diagnostics.md` if any new analyzer diagnostics are added
