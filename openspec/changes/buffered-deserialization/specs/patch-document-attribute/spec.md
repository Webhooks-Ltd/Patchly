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

**Reason:** Init-only properties are now supported via the buffered deserialization path. PATCH013 is no longer emitted.

**Migration:** Remove any workarounds that replaced `init` with `set` to avoid PATCH013. Init-only properties now work directly.

### Requirement: JsonConstructor Warning

**Reason:** `[JsonConstructor]` is now respected by the generated converter. PATCH015 is no longer emitted.

**Migration:** No action needed. Classes with `[JsonConstructor]` will now use the buffered deserialization path automatically.
