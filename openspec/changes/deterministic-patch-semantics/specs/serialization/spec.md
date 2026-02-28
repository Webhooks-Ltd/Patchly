## ADDED Requirements

### Requirement: Deterministic State Derivation from JSON

In deterministic mode, deserialization SHALL derive tri-state property status directly from JSON token presence and value.

#### Scenario: Null and absent are distinguished in deterministic mode
- **GIVEN** a deterministic patch document with `string? Email`
- **AND** payload variants `{}` and `{"email": null}`
- **WHEN** both payloads are deserialized
- **THEN** the first yields `Omitted` state for `Email`
- **AND** the second yields `Null` state for `Email`

#### Scenario: Explicit value yields Value state in deterministic mode
- **GIVEN** a deterministic patch document with `string? Email`
- **AND** payload `{"email": "alice@example.com"}`
- **WHEN** deserialized
- **THEN** state for `Email` is `Value`

### Requirement: Deterministic Nested and Collection Serialization Semantics

In deterministic mode, nested objects and collections SHALL preserve deterministic patch intent during deserialization.

#### Scenario: Nested object payload maps to Value state
- **GIVEN** a deterministic patch document with nested property `AddressPatch? Address`
- **AND** payload `{"address": {"city": "Seattle"}}`
- **WHEN** deserialized
- **THEN** state for `Address` is `Value`
- **AND** nested fields are tracked independently by the nested patch document

#### Scenario: Collection empty array remains explicit Value state
- **GIVEN** a deterministic patch document with `List<string>? Tags`
- **AND** payload `{"tags": []}`
- **WHEN** deserialized
- **THEN** state for `Tags` is `Value`
- **AND** deserialized value is an empty collection

#### Scenario: Collection null remains explicit Null state
- **GIVEN** a deterministic patch document with `List<string>? Tags`
- **AND** payload `{"tags": null}`
- **WHEN** deserialized
- **THEN** state for `Tags` is `Null`

### Requirement: Deterministic Mode Does Not Alter JSON Output Shape

Deterministic mode SHALL not introduce new tracking members into serialized JSON payloads.

#### Scenario: State infrastructure remains excluded from output JSON
- **GIVEN** a deterministic patch document instance with tracking metadata
- **WHEN** serialized with `System.Text.Json`
- **THEN** output JSON includes only model properties
- **AND** output JSON excludes state/tracking infrastructure members
