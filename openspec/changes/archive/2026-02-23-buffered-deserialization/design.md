## Context

The generated `JsonConverter.Read()` currently uses a streaming approach: construct an empty instance via parameterless constructor, then set properties one at a time while tracking them. This means `init`-only properties and parameterized constructors are rejected at compile time (PATCH013, PATCH006).

The `PatchPropertyModel` already tracks `IsInitOnly`. The `PatchClassModel` needs a new field to capture constructor parameter info when `[JsonConstructor]` is present.

Key files:
- `src/Patchly.Generators/PatchDocumentGenerator.cs` — generator logic and codegen
- `src/Patchly.Generators/PatchClassModel.cs` — pipeline model for patch classes
- `src/Patchly.Generators/PatchPropertyModel.cs` — pipeline model for properties (already has `IsInitOnly`)
- `src/Patchly.Generators/Diagnostics.cs` — diagnostic descriptors

## Goals / Non-Goals

**Goals:**
- Support `init`-only properties on `[PatchDocument]` classes
- Support `[JsonConstructor]`-annotated parameterized constructors
- Zero impact on existing users — classes with parameterless constructors and `set` properties use the identical streaming codegen path
- Maintain AOT compatibility and no runtime reflection

**Non-Goals:**
- Supporting records (remain blocked by PATCH003)
- Supporting generic `[PatchDocument]` classes
- Supporting multiple `[JsonConstructor]` constructors on the same class
- Optimizing the buffered path to match streaming performance — the allocation difference is negligible for HTTP PATCH payloads

## Decisions

### Two codegen paths in `GenerateJsonConverter`

The generator selects at compile time which `Read()` body to emit:

- **Streaming path** (existing, unchanged): When the class has a parameterless constructor and all tracked properties have `set` accessors. Construct first, then set+track in the read loop.
- **Buffered path** (new): When any tracked property is `init`-only OR a `[JsonConstructor]` constructor is present. Buffer values into local variables during the read loop, construct afterward.

The selection is a simple boolean on `PatchClassModel` (e.g., `UseBufferedDeserialization`). The `Write()` method is unchanged — it only reads properties, never sets them.

**Rationale:** Two paths avoids imposing allocations (local variables + separate HashSet) on the common case. The generator already knows everything it needs at compile time to pick the right path.

**Important: The two codegen paths share core logic** (property matching, JSON reading, tracking). When modifying shared converter behavior (e.g., `MatchesPropertyName`, `ShouldWriteProperty`, `ResolvePropertyName`, `Write()`), changes must be applied to both paths. Consider extracting shared helper generation into a common method to reduce duplication.

### Buffered path construction strategy

The buffered path uses one of two construction strategies based on what the class provides:

1. **Object initializer** (when class has a parameterless constructor but has `init` properties):
   ```csharp
   var result = new CustomerPatch { FirstName = _firstName, LastName = _lastName };
   ```

2. **Constructor invocation** (when `[JsonConstructor]` is present):
   ```csharp
   var result = new CustomerPatch(_firstName, _lastName);
   ```

Constructor parameters are matched to properties by name (case-insensitive). Any tracked property not covered by a constructor parameter is set via property setter after construction — but only if it has a `set` accessor. An `init`-only property that is not covered by a constructor parameter cannot be set after construction, so the generator SHALL emit a diagnostic error for this case.

### Constructor parameter-to-property matching

When a `[JsonConstructor]` constructor is found, the generator matches each constructor parameter to a tracked property by comparing parameter name to property name (case-insensitive, matching STJ's convention). Unmatched parameters receive their declared default value if one exists, otherwise `default`. A new diagnostic warns if a constructor parameter doesn't match any tracked property. A diagnostic error is emitted if the matched property's type differs from the parameter type. A diagnostic error is emitted if an `init`-only property is not covered by any constructor parameter (since it cannot be set after construction).

### Model changes

- `PatchClassModel` gains: `bool UseBufferedDeserialization`, `EquatableArray<ConstructorParameterModel>? ConstructorParameters`
- New record `ConstructorParameterModel(string ParameterName, string TypeName, string? MatchedPropertyName, bool HasDefaultValue, string? DefaultValueExpression)`
- `PatchPropertyModel.IsInitOnly` already exists — it was previously used only to emit PATCH013. Now it drives codegen path selection.

### Diagnostic changes

| Diagnostic | Current | New |
|---|---|---|
| PATCH006 | Error if no parameterless constructor | Error only if no parameterless constructor AND no valid `[JsonConstructor]` constructor |
| PATCH013 | Error for init-only properties | Removed — init-only properties are now supported via buffered path |
| PATCH015 | Warning that `[JsonConstructor]` is ignored | Removed — `[JsonConstructor]` is now respected |
| PATCH016 | — | Info: buffered deserialization path is being used |
| PATCH017 | — | Warning: `[JsonConstructor]` parameter name doesn't match any property |
| PATCH018 | — | Error: multiple `[JsonConstructor]` constructors found |
| PATCH019 | — | Error: `init`-only property not covered by `[JsonConstructor]` parameter |
| PATCH021 | — | Error: constructor parameter type does not match matched property type |

### `required` keyword interaction

The `[SetsRequiredMembers]` attribute on the private `bool` constructor (used by the streaming path for `required` properties) also needs to work for the buffered path. For the object initializer strategy, the generated code uses the same private `[SetsRequiredMembers]` constructor combined with an object initializer: `new T(false) { Prop = value }`. For the `[JsonConstructor]` strategy, the user's constructor must have `[SetsRequiredMembers]` if the class has `required` members — the generator SHALL emit a diagnostic error if this is missing.

## Risks / Trade-offs

**[Risk] Two codegen paths double the surface area for converter bugs** → Mitigated by extracting shared helper generation (property matching, write logic) and testing both paths with the same behavioral test suite.

**[Risk] Constructor parameter matching could be ambiguous** → Mitigated by using STJ's own convention (case-insensitive name match) and emitting a diagnostic for unmatched parameters.

**[Risk] Init-only + required + [JsonConstructor] interactions are complex** → Mitigated by testing the combinations explicitly. The generator already tracks `IsRequired` and `IsInitOnly` per property.

**[Trade-off] Buffered path allocates one local variable per property** → Acceptable for the niche use case. The streaming path remains the default for the common case. Use per-property `bool` locals (e.g., `bool _firstNameProvided`) for tracking rather than a second `HashSet` — avoids the extra allocation and is simpler to generate. After construction, emit `result._providedProperties.Add(...)` calls gated by each bool (the field is `readonly` so it cannot be reassigned, but `.Add()` mutates its contents).

**[Note] Records remain blocked (PATCH003) despite init support** → Records have value-based equality semantics, `with` expressions, and compiler-generated members that interact unpredictably with the generated tracking infrastructure. Supporting `init` on classes is a simpler, well-bounded change. Record support would be a separate investigation.
