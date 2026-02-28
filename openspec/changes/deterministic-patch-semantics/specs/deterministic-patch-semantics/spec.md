## ADDED Requirements

### Requirement: Deterministic Tri-State Field Semantics

When deterministic semantics mode is enabled for a patch document, each tracked property SHALL expose one of three states: `Omitted`, `Null`, or `Value`.

#### Scenario: Omitted property yields Omitted state
- **GIVEN** a deterministic patch document with property `string? Email`
- **AND** payload `{}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Email` is `Omitted`

#### Scenario: Property explicitly set to null yields Null state
- **GIVEN** a deterministic patch document with property `string? Email`
- **AND** payload `{"email": null}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Email` is `Null`

#### Scenario: Property explicitly set to value yields Value state
- **GIVEN** a deterministic patch document with property `string? Email`
- **AND** payload `{"email": "alice@example.com"}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Email` is `Value`

### Requirement: Deterministic Nested Object Semantics

In deterministic mode, nested object properties SHALL follow explicit no-op, clear, and apply behavior.

#### Scenario: Omitted nested property is no-op
- **GIVEN** a deterministic patch document with nested property `AddressPatch? Address`
- **AND** payload `{"firstName": "Alice"}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Address` is `Omitted`
- **AND** application logic can treat `Address` as unchanged

#### Scenario: Null nested property is explicit clear
- **GIVEN** a deterministic patch document with nested property `AddressPatch? Address`
- **AND** payload `{"address": null}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Address` is `Null`
- **AND** application logic can treat `Address` as explicit clear

#### Scenario: Nested object value supports partial update intent
- **GIVEN** a deterministic patch document with nested property `AddressPatch? Address`
- **AND** payload `{"address": {"city": "Seattle"}}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Address` is `Value`
- **AND** nested patch state indicates `City` is provided while unrelated nested fields are omitted

### Requirement: Deterministic Collection Replace Semantics

In deterministic mode, collection properties SHALL use replace semantics in V1.

#### Scenario: Omitted collection property is no-op
- **GIVEN** a deterministic patch document with property `List<string>? Tags`
- **AND** payload `{}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Tags` is `Omitted`

#### Scenario: Null collection property means clear
- **GIVEN** a deterministic patch document with property `List<string>? Tags`
- **AND** payload `{"tags": null}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Tags` is `Null`

#### Scenario: Empty collection means replace with empty value
- **GIVEN** a deterministic patch document with property `List<string>? Tags`
- **AND** payload `{"tags": []}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Tags` is `Value`
- **AND** the property value is an empty collection (not null)

#### Scenario: Non-empty collection means replace with payload values
- **GIVEN** a deterministic patch document with property `List<string>? Tags`
- **AND** payload `{"tags": ["vip", "priority"]}`
- **WHEN** the payload is deserialized
- **THEN** the state for `Tags` is `Value`
- **AND** the property value is the payload collection
