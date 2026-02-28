## ADDED Requirements

### Requirement: Generated Tri-State Accessor

In deterministic mode, the source generator SHALL emit strongly typed state accessors for tracked properties.

#### Scenario: State accessor is generated for deterministic patch document
- **GIVEN** a deterministic patch document with properties `FirstName`, `Age`, and `Tags`
- **WHEN** the source generator runs
- **THEN** generated code includes a public `State` accessor for tri-state values
- **AND** generated code includes per-property state members for those tracked properties

#### Scenario: State accessor is not required for legacy mode
- **GIVEN** a patch document using legacy semantics
- **WHEN** the source generator runs
- **THEN** generation remains compatible with existing legacy members

### Requirement: Generated State Lookup Implementation

The source generator SHALL emit `GetState` implementation that is reflection-free and consistent with tracking behavior.

#### Scenario: GetState returns Omitted for unknown property
- **GIVEN** a generated patch document instance
- **WHEN** `GetState("DoesNotExist")` is called
- **THEN** the method returns `Omitted`
- **AND** no exception is thrown

#### Scenario: GetState behavior is case-insensitive for C# property names
- **GIVEN** payload that provides `FirstName`
- **WHEN** `GetState("firstname")`, `GetState("FIRSTNAME")`, and `GetState("FirstName")` are called
- **THEN** all calls return the same state result

### Requirement: Streaming and Buffered Path Parity for State

Tri-state derivation SHALL be equivalent across both generator deserialization paths.

#### Scenario: Streaming path and buffered path produce identical states
- **GIVEN** equivalent patch documents that compile into streaming and buffered paths
- **AND** identical payload variants for omitted, null, and explicit value
- **WHEN** each payload is deserialized by each path
- **THEN** each property yields the same tri-state value in both paths

#### Scenario: Duplicate property names keep last value semantics
- **GIVEN** payload with duplicate property names where the last value is `null`
- **WHEN** deserialized in deterministic mode
- **THEN** the final property value reflects the last JSON token
- **AND** the final tri-state is `Null`
