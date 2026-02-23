## ADDED Requirements

### Requirement: JsonConstructor Support

The source generator SHALL support `[JsonConstructor]`-annotated parameterized constructors on `[PatchDocument]` classes by using the buffered deserialization path, matching constructor parameters to properties by name (case-insensitive).

#### Scenario: Class with JsonConstructor deserializes correctly

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]` constructor with parameters `(string? firstName, string? lastName)`
- **AND** corresponding properties `string? FirstName { get; set; }` and `string? LastName { get; set; }`
- **AND** JSON payload `{"firstName": "Alice"}` is deserialized
- **THEN** the constructor is called with `firstName: "Alice"` and `lastName: null`
- **AND** `WasProvided("FirstName")` returns true
- **AND** `WasProvided("LastName")` returns false

#### Scenario: JsonConstructor with init-only properties

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]` constructor with parameters `(string? name, int? age)`
- **AND** corresponding properties `string? Name { get; init; }` and `int? Age { get; init; }`
- **AND** JSON payload `{"name": "Alice", "age": 30}` is deserialized
- **THEN** the constructor is called with `name: "Alice"` and `age: 30`
- **AND** both `WasProvided("Name")` and `WasProvided("Age")` return true

#### Scenario: Properties not covered by constructor parameters are set after construction

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]` constructor with parameter `(string? name)`
- **AND** an additional property `int? Age { get; set; }` not in the constructor
- **AND** JSON payload `{"name": "Alice", "age": 30}` is deserialized
- **THEN** the constructor is called with `name: "Alice"`
- **AND** `Age` is set to `30` via the property setter after construction
- **AND** both properties are tracked as provided

#### Scenario: JsonConstructor triggers buffered path

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]`-annotated constructor
- **THEN** the generated converter uses the buffered deserialization path regardless of property accessor types

#### Scenario: Empty JSON object with JsonConstructor

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]` constructor with parameters `(string? name, int? age)`
- **AND** JSON payload `{}` is deserialized
- **THEN** the constructor is called with `name: null` and `age: null`
- **AND** `WasProvided` returns false for all properties

### Requirement: Constructor Parameter Matching

The source generator SHALL match constructor parameters to tracked properties by name (case-insensitive), following System.Text.Json's convention.

#### Scenario: Parameter matches property by name

- **WHEN** a constructor parameter is `string? firstName`
- **AND** a tracked property is `string? FirstName { get; set; }`
- **THEN** the parameter is matched to the property
- **AND** the buffered value for `FirstName` is passed as the constructor argument

#### Scenario: Unmatched constructor parameter receives its default value

- **WHEN** a constructor parameter `string? role = "user"` does not match any tracked property
- **AND** the parameter has a declared default value
- **THEN** the default value `"user"` is passed for that parameter
- **AND** the compiler emits diagnostic `PATCH017` with severity Warning that the parameter is unmatched

#### Scenario: Unmatched constructor parameter without default receives language default

- **WHEN** a constructor parameter `string? middleName` does not match any tracked property
- **AND** the parameter has no declared default value
- **THEN** `default` is passed for that parameter
- **AND** the compiler emits diagnostic `PATCH017` with severity Warning that the parameter is unmatched

#### Scenario: Constructor parameter type mismatch with matched property

- **WHEN** a constructor parameter `int? name` matches a tracked property `string? Name` by name
- **AND** the parameter type `int?` differs from the property type `string?`
- **THEN** the compiler emits diagnostic `PATCH021` with severity Error
- **AND** no source is generated for this class

#### Scenario: Only one JsonConstructor is allowed

- **WHEN** a `[PatchDocument]` partial class has multiple constructors with `[JsonConstructor]`
- **THEN** the compiler emits diagnostic `PATCH018` with severity Error
- **AND** no source is generated for this class

#### Scenario: Init-only property not covered by constructor parameter

- **WHEN** a `[PatchDocument]` partial class has a `[JsonConstructor]` constructor with parameter `(string? name)`
- **AND** an additional property `int? Age { get; init; }` not covered by any constructor parameter
- **THEN** the compiler emits diagnostic `PATCH019` with severity Error indicating the init-only property cannot be set after construction
- **AND** no source is generated for this class

### Requirement: JsonConstructor with Required Members

When a `[PatchDocument]` class has `required` members and uses a `[JsonConstructor]` constructor, the constructor MUST have `[SetsRequiredMembers]`.

#### Scenario: JsonConstructor with required members and SetsRequiredMembers

- **WHEN** a `[PatchDocument]` partial class has `public required string? Name { get; init; }`
- **AND** a `[JsonConstructor]` constructor with `[SetsRequiredMembers]` that accepts `(string? name)`
- **THEN** the generated code compiles without error
- **AND** source generation proceeds normally

#### Scenario: JsonConstructor with required members missing SetsRequiredMembers

- **WHEN** a `[PatchDocument]` partial class has `public required string? Name { get; set; }`
- **AND** a `[JsonConstructor]` constructor that does NOT have `[SetsRequiredMembers]`
- **THEN** the compiler emits a diagnostic error indicating `[SetsRequiredMembers]` is required on the `[JsonConstructor]` constructor when the class has `required` members

### Requirement: JsonConstructor Without Parameterless Constructor

The source generator SHALL allow classes that have a `[JsonConstructor]`-annotated constructor but no parameterless constructor.

#### Scenario: Class with only a parameterized JsonConstructor

- **WHEN** a `[PatchDocument]` partial class has only a `[JsonConstructor]` constructor `(string? name)` and no parameterless constructor
- **THEN** no PATCH006 diagnostic is emitted
- **AND** source generation proceeds using the buffered path with constructor invocation
