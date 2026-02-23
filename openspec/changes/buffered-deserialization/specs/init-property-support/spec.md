## ADDED Requirements

### Requirement: Init-Only Property Support

The source generator SHALL support `init`-only properties on `[PatchDocument]` classes by using the buffered deserialization path, which constructs the instance via object initializer after reading all JSON properties.

#### Scenario: Class with init-only properties deserializes correctly

- **WHEN** a `[PatchDocument]` partial class has `string? FirstName { get; init; }` and `string? LastName { get; init; }`
- **AND** JSON payload `{"firstName": "Alice"}` is deserialized
- **THEN** `FirstName` is `"Alice"`
- **AND** `LastName` is null (default)
- **AND** `WasProvided("FirstName")` returns true
- **AND** `WasProvided("LastName")` returns false

#### Scenario: Class with mix of init and set properties

- **WHEN** a `[PatchDocument]` partial class has `string? Name { get; init; }` and `int? Age { get; set; }`
- **AND** JSON payload `{"name": "Alice", "age": 30}` is deserialized
- **THEN** `Name` is `"Alice"` and `Age` is `30`
- **AND** `WasProvided("Name")` and `WasProvided("Age")` both return true

#### Scenario: Init-only property with null value

- **WHEN** a `[PatchDocument]` partial class has `string? Email { get; init; }`
- **AND** JSON payload `{"email": null}` is deserialized
- **THEN** `Email` is null
- **AND** `WasProvided("Email")` returns true

#### Scenario: Init-only property with required keyword

- **WHEN** a `[PatchDocument]` partial class has `public required string? Name { get; init; }`
- **AND** JSON payload `{"name": "Alice"}` is deserialized
- **THEN** the generated code compiles without error
- **AND** `Name` is `"Alice"`
- **AND** `WasProvided("Name")` returns true

#### Scenario: Init-only property with JsonIgnore is excluded from tracking

- **WHEN** a `[PatchDocument]` partial class has `[JsonIgnore] string? Internal { get; init; }` and `string? Name { get; set; }`
- **AND** JSON payload `{"name": "Alice"}` is deserialized
- **THEN** `Internal` is not tracked and does not appear on the `Provided` accessor
- **AND** `WasProvided("Name")` returns true

#### Scenario: Private init accessor works from generated nested converter

- **WHEN** a `[PatchDocument]` partial class has `string? Name { get; private init; }`
- **AND** JSON payload `{"name": "Alice"}` is deserialized
- **THEN** `Name` is `"Alice"` (the nested converter can access private init via object initializer)
- **AND** `WasProvided("Name")` returns true

#### Scenario: Provided accessor works with init-only properties

- **WHEN** a `[PatchDocument]` partial class has `string? FirstName { get; init; }` and `string? LastName { get; init; }`
- **AND** JSON payload `{"firstName": "Alice"}` is deserialized
- **THEN** `Provided.FirstName` returns true
- **AND** `Provided.LastName` returns false

### Requirement: Buffered Path Selection

The source generator SHALL use the buffered deserialization path only when the class requires it, and the streaming path otherwise.

#### Scenario: Class with only set properties uses streaming path

- **WHEN** a `[PatchDocument]` partial class has only `{ get; set; }` properties and a parameterless constructor
- **THEN** the generated converter uses the streaming deserialization path (construct first, then set properties)

#### Scenario: Class with any init property uses buffered path

- **WHEN** a `[PatchDocument]` partial class has at least one `{ get; init; }` property
- **THEN** the generated converter uses the buffered deserialization path (read all properties first, then construct via object initializer)

#### Scenario: Streaming path is unchanged

- **WHEN** a `[PatchDocument]` partial class uses the streaming path
- **THEN** the generated converter code is identical to the current implementation
- **AND** there is no performance impact for existing users

### Requirement: Buffered Path Diagnostic

The source generator SHALL emit an informational diagnostic when the buffered deserialization path is selected, so that users are aware the generated code differs from the default streaming path.

#### Scenario: Info diagnostic when init property triggers buffered path

- **WHEN** a `[PatchDocument]` partial class has at least one `{ get; init; }` property
- **THEN** the compiler emits a diagnostic with severity Info
- **AND** the message indicates that buffered deserialization is being used due to init-only properties

#### Scenario: Info diagnostic when JsonConstructor triggers buffered path

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]`-annotated constructor
- **THEN** the compiler emits a diagnostic with severity Info
- **AND** the message indicates that buffered deserialization is being used due to the parameterized constructor

#### Scenario: No diagnostic for streaming path

- **WHEN** a `[PatchDocument]` partial class uses the streaming path
- **THEN** no buffered path diagnostic is emitted
