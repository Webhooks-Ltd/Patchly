## 1. Model Changes

- [x] 1.1 Add `ConstructorParameterModel` record to `src/Patchly.Generators/` with fields: `ParameterName`, `TypeName`, `MatchedPropertyName`, `HasDefaultValue`, `DefaultValueExpression`
- [x] 1.2 Add `UseBufferedDeserialization` (bool) and `ConstructorParameters` (EquatableArray<ConstructorParameterModel>?) to `PatchClassModel`
- [x] 1.3 Add new diagnostic descriptors to `Diagnostics.cs`: PATCH016 (info: buffered path), PATCH017 (warning: unmatched ctor param), PATCH018 (error: multiple JsonConstructors), PATCH019 (error: init-only property not covered by ctor param), PATCH021 (error: ctor param type mismatch). Remove PATCH013 and PATCH015.
- [x] 1.4 Update PATCH006 message to mention `[JsonConstructor]` as an alternative to parameterless constructors

## 2. Generator Validation Logic

- [x] 2.1 Update `TransformType` in `PatchDocumentGenerator.cs`: restructure PATCH006 check and `[JsonConstructor]` detection so that `[JsonConstructor]` is detected BEFORE the PATCH006 early return (currently PATCH006 returns early at line 87, before JsonConstructor detection at line 93)
- [x] 2.2 Relax PATCH006 to allow classes with a valid `[JsonConstructor]` constructor but no parameterless constructor
- [x] 2.3 Remove PATCH013 error for init-only properties — instead set `UseBufferedDeserialization = true` when any init-only property is found
- [x] 2.4 Remove PATCH015 warning for `[JsonConstructor]` — instead set `UseBufferedDeserialization = true` when `[JsonConstructor]` is present
- [x] 2.5 Detect `[JsonConstructor]` constructor parameters: build `ConstructorParameterModel` list with case-insensitive name matching to tracked properties, capturing default values
- [x] 2.6 Add validation: emit PATCH018 error when multiple `[JsonConstructor]` constructors are found
- [x] 2.7 Add validation: emit PATCH017 warning when a `[JsonConstructor]` parameter name doesn't match any tracked property
- [x] 2.8 Add validation: emit PATCH021 error when a matched constructor parameter type differs from the property type
- [x] 2.9 Add validation: emit PATCH019 error when `[JsonConstructor]` is present and an `init`-only property is not covered by any constructor parameter
- [x] 2.10 Add validation: emit error when `[JsonConstructor]` constructor lacks `[SetsRequiredMembers]` but class has `required` members
- [x] 2.11 Emit PATCH016 info diagnostic when buffered path is selected, with reason (init properties or JsonConstructor)

## 3. Buffered Codegen Path

- [x] 3.1 Add `GenerateBufferedReadMethod` to `PatchDocumentGenerator.cs` that emits: local variable per property, per-property `bool` tracking locals (not a second HashSet), read loop, then construction. **Note: shares core logic with the streaming path (property matching, JSON reading) — extract shared helpers or document that changes to shared patterns must be applied in both paths.**
- [x] 3.2 Implement object initializer construction strategy (for init-only properties with parameterless constructor): emit `new T(false) { Prop = value }` when `HasRequiredMembers`, otherwise `new T { Prop = value }`. Include all tracked properties (both `init` and `set`) in the initializer.
- [x] 3.3 Implement constructor invocation strategy (for `[JsonConstructor]` constructors): pass matched buffered values as arguments, using declared default values for unmatched params (or `default` if no declared default)
- [x] 3.4 Handle mixed case: properties covered by constructor params passed as arguments + remaining `set` properties assigned after construction
- [x] 3.5 After construction, emit `result._providedProperties.Add("PropertyName")` calls gated by per-property `bool` locals (the `_providedProperties` field is `readonly` so cannot be reassigned, but `.Add()` mutates contents)
- [x] 3.6 Wire up path selection in `GenerateJsonConverter`: call `GenerateBufferedReadMethod` when `model.UseBufferedDeserialization` is true, existing streaming path otherwise

## 4. Tests

- [x] 4.1 Add tests for init-only property deserialization: basic, mixed with set, null value, required keyword, Provided accessor
- [x] 4.2 Add tests for `[JsonConstructor]` deserialization: basic, with init properties, properties not covered by constructor (set only), empty JSON, null vs absent
- [x] 4.3 Add tests for constructor parameter matching: matched, unmatched parameter warning (PATCH017), multiple JsonConstructor error (PATCH018), type mismatch error (PATCH021)
- [x] 4.4 Add tests for init-only property not covered by `[JsonConstructor]` parameter (PATCH019 error)
- [x] 4.5 Add test for `[JsonConstructor]` with `required` members missing `[SetsRequiredMembers]`
- [x] 4.6 Add test for constructor parameter with declared default value (value is used, not `default`)
- [x] 4.7 Update existing diagnostic tests: remove PATCH013 and PATCH015 expectations, update PATCH006 expectations
- [x] 4.8 Add test verifying streaming path is still used for classes with only `set` properties and parameterless constructor
- [x] 4.9 Add test for PATCH016 info diagnostic when buffered path is selected
- [x] 4.10 Add test for `[JsonIgnore]` + `init`-only property (excluded from tracking)
- [x] 4.11 Add test for `private init` accessor (works from nested converter)
- [x] 4.12 Add test for `required init` + object initializer path with `[SetsRequiredMembers]` private constructor

## 5. Documentation

- [x] 5.1 Update `docs/diagnostics.md`: remove PATCH013 and PATCH015 entries, add PATCH016–PATCH021 entries, update PATCH006 rationale
- [x] 5.2 Update `README.md` diagnostics table to match
- [x] 5.3 Update `README.md` to document init-only property and `[JsonConstructor]` support with examples
- [x] 5.4 Add note to README explaining why records are still blocked (PATCH003) despite init support
