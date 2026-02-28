## ADDED Requirements

### Requirement: PatchDocument Semantics Mode Configuration

The `[PatchDocument]` attribute SHALL support configuring patch semantics mode per document type.

#### Scenario: Attribute defaults to legacy mode
- **WHEN** a patch document is declared as `[PatchDocument]` without explicit semantics mode
- **THEN** generation uses legacy semantics behavior by default

#### Scenario: Attribute enables deterministic mode
- **WHEN** a patch document is declared with deterministic semantics mode
- **THEN** deterministic state APIs and deterministic-mode diagnostics are enabled for that type

### Requirement: IPatchDocument State Lookup Contract

`IPatchDocument` SHALL expose an API to query tri-state value semantics by C# property name.

#### Scenario: Interface exposes state lookup method
- **GIVEN** the `IPatchDocument` public contract
- **WHEN** the interface is referenced from application code
- **THEN** it includes a `GetState(string propertyName)` member that returns a tri-state value

#### Scenario: State lookup is consistent with WasProvided
- **GIVEN** a deterministic patch document instance
- **WHEN** `GetState(propertyName)` returns `Omitted`
- **THEN** `WasProvided(propertyName)` returns false
- **AND WHEN** `GetState(propertyName)` returns `Null` or `Value`
- **THEN** `WasProvided(propertyName)` returns true

### Requirement: Deterministic Mode Collection Guardrail

The generator SHALL warn when deterministic mode is enabled on patch documents that define non-nullable collection properties.

#### Scenario: Non-nullable collection property emits warning in deterministic mode
- **GIVEN** a patch document in deterministic mode with property `List<string> Tags`
- **WHEN** the project is compiled
- **THEN** the generator emits a warning diagnostic indicating nullable collection types are recommended for clear-vs-replace semantics

#### Scenario: Same shape does not emit deterministic guardrail in legacy mode
- **GIVEN** a patch document in legacy mode with property `List<string> Tags`
- **WHEN** the project is compiled
- **THEN** deterministic guardrail warning is not emitted
