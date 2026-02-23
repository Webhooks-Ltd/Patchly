## ADDED Requirements

### Requirement: GetProvidedValues Method

The `IPatchDocument` interface SHALL expose a `GetProvidedValues()` method that returns `IReadOnlyDictionary<string, object?>` containing only the properties that were present in the JSON payload, keyed by C# property name with their current values.

#### Scenario: Returns only provided properties

- **WHEN** a `CustomerPatch` is deserialized from `{"firstName":"Alice","age":30}`
- **THEN** `patch.GetProvidedValues()` returns a dictionary with exactly 2 entries
- **AND** the dictionary contains key `"FirstName"` with value `"Alice"`
- **AND** the dictionary contains key `"Age"` with value `30` (boxed int)

#### Scenario: Returns empty dictionary when no properties provided

- **WHEN** a `CustomerPatch` is deserialized from `{}`
- **THEN** `patch.GetProvidedValues()` returns an empty dictionary

#### Scenario: Includes properties explicitly set to null

- **WHEN** a `CustomerPatch` is deserialized from `{"firstName":null}`
- **THEN** `patch.GetProvidedValues()` returns a dictionary with key `"FirstName"` and value `null`

#### Scenario: Keys match ProvidedProperties

- **WHEN** a `CustomerPatch` is deserialized from `{"firstName":"Alice"}`
- **THEN** `patch.GetProvidedValues().Keys` contains exactly the same strings as `patch.ProvidedProperties`

#### Scenario: Fresh instance returns empty dictionary

- **WHEN** a `CustomerPatch` is created via `new CustomerPatch()`
- **THEN** `patch.GetProvidedValues()` returns an empty dictionary

#### Scenario: Each call returns a fresh dictionary

- **WHEN** `GetProvidedValues()` is called twice on the same patch instance
- **THEN** the two dictionaries are separate instances (not the same reference)

#### Scenario: JsonIgnore properties excluded

- **WHEN** a `[PatchDocument]` has `[JsonIgnore] public string? Internal { get; set; }`
- **AND** JSON payload includes `{"internal":"value"}`
- **THEN** `patch.GetProvidedValues()` does NOT contain key `"Internal"`

#### Scenario: Buffered path is transparent

- **WHEN** a `[PatchDocument]` uses init-only properties (buffered deserialization path)
- **AND** JSON payload is `{"firstName":"Alice"}`
- **THEN** `patch.GetProvidedValues()` returns a dictionary with key `"FirstName"` and value `"Alice"`

#### Scenario: Keys use C# property names not JSON names

- **GIVEN** a `[PatchDocument]` with `[JsonPropertyName("first_name")] public string? FirstName`
- **AND** JSON payload `{"first_name":"Alice"}`
- **THEN** `patch.GetProvidedValues().Keys` contains `"FirstName"` (C# name, not `"first_name"`)
