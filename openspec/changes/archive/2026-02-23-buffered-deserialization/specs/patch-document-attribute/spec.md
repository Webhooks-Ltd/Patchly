## MODIFIED Requirements

### Requirement: Parameterless Constructor Validation

The source generator SHALL verify that a `[PatchDocument]` class has either an accessible parameterless constructor OR a valid `[JsonConstructor]`-annotated constructor. If neither exists, the generator emits PATCH006.

#### Scenario: Class without a parameterless constructor and no JsonConstructor

- **WHEN** a `[PatchDocument]` partial class has only a parameterized constructor without `[JsonConstructor]`
- **THEN** the compiler emits diagnostic `PATCH006` with severity Error
- **AND** the message indicates that `[PatchDocument]` classes must have an accessible parameterless constructor or a `[JsonConstructor]` constructor
- **AND** no source is generated for this class

#### Scenario: Class with both parameterless and parameterized constructors

- **WHEN** a `[PatchDocument]` partial class has a parameterless constructor and additional parameterized constructors
- **THEN** no diagnostic is emitted
- **AND** source generation proceeds using the parameterless constructor (streaming path)

#### Scenario: Class with only a JsonConstructor constructor

- **WHEN** a `[PatchDocument]` partial class has only a `[JsonConstructor]`-annotated parameterized constructor and no parameterless constructor
- **THEN** no PATCH006 diagnostic is emitted
- **AND** source generation proceeds using the buffered path with constructor invocation

## REMOVED Requirements

### Requirement: Init-Only Property Validation

The generator SHALL no longer emit PATCH013 for init-only properties. Init-only properties MUST be supported via the buffered deserialization path.

#### Scenario: Init-only property no longer errors

- **WHEN** a `[PatchDocument]` class has an `init`-only property
- **THEN** PATCH013 is not emitted
- **AND** the generator uses the buffered deserialization path

**Migration:** Remove any workarounds that replaced `init` with `set` to avoid PATCH013. Init-only properties now work directly.

### Requirement: JsonConstructor Warning

The generator SHALL no longer emit PATCH015 when a `[JsonConstructor]` attribute is present. `[JsonConstructor]` MUST be respected by the generated converter.

#### Scenario: JsonConstructor no longer warns

- **WHEN** a `[PatchDocument]` class has a `[JsonConstructor]`-annotated constructor
- **THEN** PATCH015 is not emitted
- **AND** the generator uses the buffered deserialization path with constructor invocation

**Migration:** No action needed. Classes with `[JsonConstructor]` will now use the buffered deserialization path automatically.
