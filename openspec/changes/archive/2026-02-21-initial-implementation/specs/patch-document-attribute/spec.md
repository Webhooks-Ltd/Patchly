## ADDED Requirements

### Requirement: Parameterless Constructor Validation

The source generator SHALL verify that a `[PatchDocument]` class has an accessible parameterless constructor, since the generated `JsonConverter.Read` method must call `new T()`.

#### Scenario: Class without a parameterless constructor
- **WHEN** a `[PatchDocument]` partial class has only a parameterized constructor (e.g., `public CustomerPatch(string id)`)
- **THEN** the compiler emits diagnostic `PATCH006` with severity Error
- **AND** the message indicates that `[PatchDocument]` classes must have an accessible parameterless constructor
- **AND** no source is generated for this class

#### Scenario: Class with both parameterless and parameterized constructors
- **WHEN** a `[PatchDocument]` partial class has a parameterless constructor and additional parameterized constructors
- **THEN** no diagnostic is emitted
- **AND** source generation proceeds normally using the parameterless constructor

### Requirement: Init-Only Property Validation

The source generator SHALL reject `init`-only properties because the generated converter sets properties in a loop after construction, which is incompatible with `init` accessors.

#### Scenario: Init-only property emits an error
- **WHEN** a `[PatchDocument]` partial class has a property `string? Name { get; init; }`
- **THEN** the compiler emits diagnostic `PATCH013` with severity Error
- **AND** the message indicates that `init`-only properties are not supported on `[PatchDocument]` classes because the generated converter cannot set them after construction

#### Scenario: Mix of settable and init-only properties
- **WHEN** a `[PatchDocument]` partial class has `string? FirstName { get; set; }` and `string? LastName { get; init; }`
- **THEN** the compiler emits diagnostic `PATCH013` for `LastName`
- **AND** no source is generated for the entire class (init-only property is a hard error)

### Requirement: JsonExtensionData Validation

The source generator SHALL reject properties decorated with `[JsonExtensionData]` because the generated converter handles unknown properties by skipping them.

#### Scenario: JsonExtensionData property emits an error
- **WHEN** a `[PatchDocument]` partial class has a property `[JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; set; }`
- **THEN** the compiler emits diagnostic `PATCH014` with severity Error
- **AND** the message indicates that `[JsonExtensionData]` is not supported on `[PatchDocument]` classes

### Requirement: JsonConstructor Warning

The source generator SHALL warn when a `[JsonConstructor]` attribute is present because the generated converter ignores it.

#### Scenario: JsonConstructor attribute emits a warning
- **WHEN** a `[PatchDocument]` partial class has a constructor decorated with `[JsonConstructor]`
- **THEN** the compiler emits diagnostic `PATCH015` with severity Warning
- **AND** the message indicates that `[JsonConstructor]` is ignored by the Patchly-generated converter

### Requirement: Required Keyword Handling

The source generator SHALL handle the C# `required` keyword on properties by applying `[SetsRequiredMembers]` to the generated converter's construction path.

#### Scenario: Required properties compile without error
- **WHEN** a `[PatchDocument]` partial class has `public required string? FirstName { get; set; }`
- **THEN** the generated code compiles without error
- **AND** the converter can construct the object and set properties normally
- **AND** `WasProvided("FirstName")` works correctly

## MODIFIED Requirements

### Requirement: Property Type Validation

The source generator SHALL validate that properties on a `[PatchDocument]` class are suitable for partial update tracking.

#### Scenario: Class with only nullable properties compiles cleanly

- GIVEN a `[PatchDocument]` partial class with properties `string? Name`, `int? Count`, `DateTime? Date`, `decimal? Price`
- WHEN the project is compiled
- THEN no diagnostics are emitted for these properties
- AND all properties are included in the generated tracking and Provided accessor

#### Scenario: Class with a non-nullable value type property emits a warning

- GIVEN a `[PatchDocument]` partial class with property `int Count` (non-nullable)
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH010` with severity Warning
- AND the message indicates that non-nullable value type properties cannot distinguish between "not provided" and "default value"
- AND the property is still included in tracking (WasProvided and Provided work) and generation proceeds

#### Scenario: Class with a non-nullable reference type property compiles without warning

- GIVEN a `[PatchDocument]` partial class with property `string Name` (non-nullable reference type in a nullable-enabled context)
- WHEN the project is compiled
- THEN no diagnostic is emitted for this property (reference types can be sent as null in JSON regardless of C# nullability annotation)
- AND the property is included in tracking and generation

#### Scenario: Class with no public properties emits a warning

- GIVEN a `[PatchDocument]` partial class with zero public properties
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH011` with severity Warning
- AND the message indicates the patch document has no properties to track

#### Scenario: Class with read-only properties

- GIVEN a `[PatchDocument]` partial class with a property `string? Name { get; }` (no setter)
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH012` with severity Warning
- AND the message indicates that read-only properties will be tracked but cannot be set during deserialization
- AND the property is excluded from the generated JsonConverter deserialization logic and from the Provided accessor
