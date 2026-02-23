## MODIFIED Requirements

### Requirement: Provided Accessor Coexists With IPatchDocument

The `Provided` accessor, the `IPatchDocument` interface members (`WasProvided`, `ProvidedProperties`, `GetProvidedValues`), SHALL all be available on the same class, providing ergonomic and generic access respectively.

#### Scenario: Both Provided and WasProvided agree

- **WHEN** a `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.FirstName` returns true
- **AND** `patch.WasProvided("FirstName")` returns true
- **AND** `patch.ProvidedProperties.Contains("FirstName")` returns true

#### Scenario: Provided accessor and WasProvided both return false for absent properties

- **WHEN** a `CustomerPatch` is deserialized from `{"age": 30}`
- **THEN** `patch.Provided.FirstName` returns false
- **AND** `patch.WasProvided("FirstName")` returns false

#### Scenario: GetProvidedValues agrees with Provided accessor

- **WHEN** a `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.FirstName` returns true
- **AND** `patch.GetProvidedValues().ContainsKey("FirstName")` returns true
- **AND** `patch.GetProvidedValues()["FirstName"]` equals `"Alice"`
