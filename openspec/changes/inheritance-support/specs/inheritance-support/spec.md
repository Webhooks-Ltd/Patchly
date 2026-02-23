## ADDED Requirements

### Requirement: Inherited Property Tracking

The source generator SHALL discover tracked properties from all `[PatchDocument]`-annotated base classes in the inheritance chain and include them in the derived class's generated converter, `ProvidedSet`, `WasProvided`, `ProvidedProperties`, and `GetProvidedValues`.

#### Scenario: Single-level inheritance

- **GIVEN** `BasePatch` with `[PatchDocument]` and property `ModifiedBy`
- **AND** `DerivedPatch : BasePatch` with `[PatchDocument]` and property `Name`
- **WHEN** `DerivedPatch` is deserialized from `{"modifiedBy":"admin","name":"Alice"}`
- **THEN** `patch.ModifiedBy` is `"admin"` and `patch.Name` is `"Alice"`
- **AND** `patch.WasProvided("ModifiedBy")` returns true
- **AND** `patch.WasProvided("Name")` returns true
- **AND** `patch.Provided.ModifiedBy` returns true
- **AND** `patch.Provided.Name` returns true

#### Scenario: Multi-level inheritance

- **GIVEN** `GrandBasePatch` → `BasePatch` → `DerivedPatch`, all with `[PatchDocument]`
- **WHEN** `DerivedPatch` is deserialized with properties from all three levels
- **THEN** all properties from all three levels are tracked

#### Scenario: Only base properties provided

- **GIVEN** `DerivedPatch : BasePatch` with `[PatchDocument]`
- **WHEN** JSON payload is `{"modifiedBy":"admin"}`
- **THEN** `patch.WasProvided("ModifiedBy")` returns true
- **AND** `patch.WasProvided("Name")` returns false

#### Scenario: Base class deserialized independently

- **GIVEN** `BasePatch` with `[PatchDocument]` and property `ModifiedBy`
- **WHEN** `BasePatch` itself is deserialized from `{"modifiedBy":"admin"}`
- **THEN** `patch.WasProvided("ModifiedBy")` returns true
- **AND** the base class works independently of any derived classes

### Requirement: Non-PatchDocument Base Class Ignored

Properties from base classes that do NOT have `[PatchDocument]` SHALL NOT be included in the derived class's tracking.

#### Scenario: Base without attribute is ignored

- **GIVEN** `PlainBase` without `[PatchDocument]` and property `Id`
- **AND** `DerivedPatch : PlainBase` with `[PatchDocument]` and property `Name`
- **WHEN** `DerivedPatch` is deserialized from `{"id":1,"name":"Alice"}`
- **THEN** `patch.WasProvided("Name")` returns true
- **AND** `patch.WasProvided("Id")` returns false

### Requirement: Property Shadowing

If a derived class declares a property with the same name as a base class property (using `new`), only the derived class's property SHALL be tracked.

#### Scenario: Derived property shadows base property

- **GIVEN** `BasePatch` with `string? Name { get; set; }`
- **AND** `DerivedPatch : BasePatch` with `new string? Name { get; set; }`
- **WHEN** `DerivedPatch` is deserialized from `{"name":"Alice"}`
- **THEN** the derived `Name` property is set to `"Alice"`
- **AND** `patch.WasProvided("Name")` returns true
- **AND** `patch.Provided.Name` returns true
- **AND** only one `Name` entry exists in `ProvidedProperties`

### Requirement: Inheritance With Buffered Deserialization

Inheritance SHALL work correctly with the buffered deserialization path (init-only properties and `[JsonConstructor]` on derived classes).

#### Scenario: Derived class with init properties and base class properties

- **GIVEN** `BasePatch` with `[PatchDocument]` and `string? ModifiedBy { get; set; }`
- **AND** `DerivedPatch : BasePatch` with `[PatchDocument]` and `string? Name { get; init; }`
- **WHEN** `DerivedPatch` is deserialized from `{"modifiedBy":"admin","name":"Alice"}`
- **THEN** both properties are tracked correctly
- **AND** the buffered path handles both inherited set properties and declared init properties
