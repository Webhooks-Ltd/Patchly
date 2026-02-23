## Why

The generated `JsonConverter.Read()` currently constructs an empty instance first, then sets properties in a streaming loop. This forces two restrictions: no `init`-only properties (PATCH013 error) and no parameterized constructors (PATCH006 error when no parameterless constructor exists). By buffering property values into local variables during the read loop and constructing the object afterward, both restrictions can be lifted without changing tracking semantics.

## What Changes

- Add a second codegen path for `JsonConverter.Read()` that buffers deserialized values into local variables and constructs the instance after the read loop via object initializer or constructor arguments
- The buffered path is only emitted when needed — when the class has `init`-only properties or a `[JsonConstructor]`-annotated parameterized constructor. Classes with parameterless constructors and `set` properties continue to use the current streaming approach (no behavioral or performance change for existing users).
- Support `init`-only properties via object initializer syntax in generated code — remove PATCH013 as an error
- Support `[JsonConstructor]`-annotated parameterized constructors — the generator matches constructor parameters to properties and passes buffered values as arguments
- Remove PATCH015 warning (JsonConstructor ignored) since it will now be respected
- Relax PATCH006 — only error when no accessible constructor exists at all (neither parameterless nor a valid `[JsonConstructor]`-annotated one)
- Update diagnostics documentation in `docs/diagnostics.md` and `README.md`

## Capabilities

### New Capabilities

- `init-property-support`: Support for `init`-only properties on `[PatchDocument]` classes via buffered deserialization and object initializer generation
- `json-constructor-support`: Support for `[JsonConstructor]`-annotated parameterized constructors on `[PatchDocument]` classes

### Modified Capabilities

- `source-generation`: Generated converter `Read()` method gains a second codegen path (buffer-then-construct) used only when init properties or parameterized constructors are present
- `serialization`: Deserialization behavior unchanged from consumer perspective, but internal converter structure has two paths
- `patch-document-attribute`: PATCH006, PATCH013, PATCH015 diagnostic rules change

## Impact

- `src/Patchly.Generators/PatchDocumentGenerator.cs` — second codegen path in `GenerateJsonConverter`, changes to `TransformType` validation, new model fields for constructor info
- `src/Patchly.Generators/Diagnostics.cs` — remove PATCH013, PATCH015; update PATCH006 message
- `src/Patchly.Generators/PatchClassModel.cs` / `PatchPropertyModel.cs` — new fields for init-only flag and constructor parameter mapping
- `tests/Patchly.Tests/` — new tests for init properties and JsonConstructor; update diagnostic tests
- `docs/diagnostics.md` and `README.md` — update diagnostic tables
- No changes to the core `Patchly` library (attribute, interface, PatchMap)
- No breaking changes to existing users — classes with parameterless constructors and `set` properties continue to use the identical streaming codegen path
