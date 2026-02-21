## ADDED Requirements

### Requirement: Provided Accessor Struct

The source generator SHALL emit a nested `readonly struct` named `ProvidedSet` inside each `[PatchDocument]` class, with a `bool` property for each tracked property on the patch class. The struct SHALL be accessible via a `Provided` property on the patch class.

#### Scenario: Provided accessor returns true for a provided property
- **WHEN** a `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.FirstName` returns true

#### Scenario: Provided accessor returns false for an absent property
- **WHEN** a `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.LastName` returns false

#### Scenario: Provided accessor returns true for a property explicitly set to null
- **WHEN** a `CustomerPatch` is deserialized from `{"firstName": null}`
- **THEN** `patch.Provided.FirstName` returns true

#### Scenario: Provided accessor returns false for all properties on a fresh instance
- **WHEN** a `CustomerPatch` is created via `new CustomerPatch()`
- **THEN** `patch.Provided.FirstName` returns false
- **AND** `patch.Provided.LastName` returns false
- **AND** `patch.Provided.Age` returns false

#### Scenario: Provided accessor returns false for all properties from empty JSON
- **WHEN** a `CustomerPatch` is deserialized from `{}`
- **THEN** `patch.Provided.FirstName` returns false
- **AND** `patch.Provided.Age` returns false

### Requirement: ProvidedSet Struct Generation

The generated `ProvidedSet` struct SHALL be a nested `readonly struct` inside the patch class, wrapping the internal `HashSet<string>` tracking field. It SHALL have one `bool` property per tracked property on the patch class.

#### Scenario: ProvidedSet is a nested readonly struct
- **WHEN** the source generator runs for `CustomerPatch`
- **THEN** the generated code contains `public readonly struct ProvidedSet` nested inside `CustomerPatch`

#### Scenario: ProvidedSet properties use nameof for lookups
- **WHEN** the source generator runs for a class with property `FirstName`
- **THEN** the generated `ProvidedSet.FirstName` property body uses `nameof(CustomerPatch.FirstName)` in the `Contains` call

#### Scenario: ProvidedSet wraps the tracking HashSet by reference
- **WHEN** the source generator runs
- **THEN** the `ProvidedSet` struct contains a single `private readonly HashSet<string>` field
- **AND** the struct is constructed via an `internal` constructor accepting the `HashSet<string>`

#### Scenario: Provided property is decorated with JsonIgnore
- **WHEN** the source generator runs
- **THEN** the `Provided` property on the patch class is decorated with `[System.Text.Json.Serialization.JsonIgnore]`

#### Scenario: ProvidedSet has a property for each tracked property
- **WHEN** the source generator runs for a class with properties `FirstName`, `LastName`, `Age`
- **THEN** the generated `ProvidedSet` has exactly three `bool` properties: `FirstName`, `LastName`, `Age`

#### Scenario: ProvidedSet excludes JsonIgnored properties
- **WHEN** the source generator runs for a class with `[JsonIgnore] public string? InternalNote`
- **THEN** the generated `ProvidedSet` does NOT have an `InternalNote` property

#### Scenario: ProvidedSet excludes read-only properties
- **WHEN** the source generator runs for a class with `public string? Name { get; }` (no setter)
- **THEN** the generated `ProvidedSet` does NOT have a `Name` property

### Requirement: Provided Accessor Coexists With IPatchDocument

The `Provided` accessor and the `IPatchDocument` interface members SHALL both be available on the same class, providing ergonomic and generic access respectively.

#### Scenario: Both Provided and WasProvided agree
- **WHEN** a `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.FirstName` returns true
- **AND** `patch.WasProvided("FirstName")` returns true
- **AND** `patch.ProvidedProperties.Contains("FirstName")` returns true

#### Scenario: Provided accessor and WasProvided both return false for absent properties
- **WHEN** a `CustomerPatch` is deserialized from `{"age": 30}`
- **THEN** `patch.Provided.FirstName` returns false
- **AND** `patch.WasProvided("FirstName")` returns false

### Requirement: Nested PatchDocument Properties

When a `[PatchDocument]` class has a property whose type is also a `[PatchDocument]`, the outer `Provided` accessor SHALL track whether the nested object was sent, and the nested object SHALL have its own `Provided` accessor for its properties.

#### Scenario: Nested PatchDocument tracking is independent
- **WHEN** `CustomerPatch` has property `AddressPatch? Address` where `AddressPatch` is also a `[PatchDocument]`
- **AND** the JSON payload is `{"address": {"line1": "123 Main St"}}`
- **THEN** `patch.Provided.Address` returns true
- **AND** `patch.Address.Provided.Line1` returns true
- **AND** `patch.Address.Provided.City` returns false

#### Scenario: Nested PatchDocument not provided
- **WHEN** the JSON payload is `{"firstName": "Alice"}`
- **THEN** `patch.Provided.Address` returns false
- **AND** `patch.Address` is null
