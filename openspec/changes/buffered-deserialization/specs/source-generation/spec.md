## MODIFIED Requirements

### Requirement: Generated JsonConverter

The source generator emits a nested `JsonConverter<T>` class that uses `Utf8JsonReader` to deserialize JSON, tracking which properties were present in the payload. Per-property deserialization MUST delegate to `JsonSerializer.Deserialize<TProperty>(ref reader, options)` to respect all `JsonSerializerOptions` configuration.

The converter SHALL use one of two codegen paths selected at compile time:

- **Streaming path**: When the class has a parameterless constructor and all tracked properties have `set` accessors. Constructs an empty instance first, then sets properties and tracks them in the read loop.
- **Buffered path**: When any tracked property is `init`-only OR a `[JsonConstructor]` constructor is present. Buffers deserialized values into local variables during the read loop, then constructs the instance afterward via object initializer or constructor invocation.

Both paths produce identical observable behavior: the same properties are tracked, `WasProvided()` returns the same results, and `Provided` accessor works identically.

#### Scenario: Converter reads known properties and tracks them

- GIVEN a generated converter for `CustomerPatch` with properties `FirstName`, `LastName`, `Age`
- AND a JSON payload `{"firstName": "Alice", "age": 30}`
- WHEN the converter deserializes the payload
- THEN the resulting `CustomerPatch` has `FirstName == "Alice"` and `Age == 30`
- AND `WasProvided("FirstName")` returns true
- AND `WasProvided("Age")` returns true
- AND `WasProvided("LastName")` returns false
- AND `LastName` is null (default)

#### Scenario: Converter delegates per-property deserialization to JsonSerializer

- GIVEN a generated converter for a class with `int? Count`
- AND `JsonSerializerOptions` with `NumberHandling = JsonNumberHandling.AllowReadingFromString`
- AND a JSON payload `{"count": "42"}`
- WHEN the converter deserializes the payload
- THEN `Count` is `42` (number read from string)
- AND this works because the converter calls `JsonSerializer.Deserialize<int?>(ref reader, options)` not `reader.GetInt32()`

#### Scenario: Converter handles the Write method

- GIVEN a generated converter for `CustomerPatch`
- AND a `CustomerPatch` instance with `FirstName = "Alice"` and `Age = 30` (both provided)
- WHEN the converter serializes the instance
- THEN the output JSON contains `"firstName"` and `"age"` keys with their values
- AND the output does NOT contain `_providedProperties`, `Provided`, or any tracking fields

#### Scenario: Converter Write method respects DefaultIgnoreCondition

- GIVEN a generated converter for `CustomerPatch`
- AND `JsonSerializerOptions` with `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
- AND a `CustomerPatch` with `FirstName = "Alice"` (provided) and `LastName = null` (provided)
- WHEN the converter serializes the instance
- THEN `lastName` is omitted from the JSON output (respecting WhenWritingNull)

#### Scenario: Converter respects JsonPropertyName attribute

- GIVEN a `[PatchDocument]` partial class with property `[JsonPropertyName("first_name")] public string? FirstName { get; set; }`
- WHEN the source generator runs
- THEN the generated converter matches JSON property `"first_name"` (not `"firstName"`)
- AND `WasProvided("FirstName")` uses the C# property name, not the JSON name

#### Scenario: Converter respects JsonSerializerOptions naming policy

- GIVEN a generated converter for `CustomerPatch` with property `FirstName`
- AND no `[JsonPropertyName]` override on the property
- WHEN deserializing with `JsonSerializerOptions` that has `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- THEN the converter matches JSON property `"firstName"`
- AND when deserializing with `PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`
- THEN the converter matches JSON property `"first_name"`

#### Scenario: Converter handles empty JSON object

- GIVEN a generated converter for `CustomerPatch`
- AND a JSON payload `{}`
- WHEN the converter deserializes the payload
- THEN the resulting `CustomerPatch` has all properties at their defaults
- AND `WasProvided` returns false for all properties
- AND `ProvidedProperties` is empty

#### Scenario: Converter handles null JSON token as input

- GIVEN a generated converter for `CustomerPatch`
- AND a JSON payload that is the token `null`
- WHEN the converter deserializes the payload
- THEN the result is null (not a `CustomerPatch` instance)

#### Scenario: Buffered path constructs via object initializer for init properties

- GIVEN a `[PatchDocument]` class with `string? Name { get; init; }`
- WHEN the source generator runs
- THEN the generated converter's `Read()` method buffers `Name` into a local variable
- AND constructs the instance using object initializer syntax: `new T { Name = _name }`
- AND populates `_providedProperties` via `.Add()` calls for each provided property after construction

#### Scenario: Buffered path constructs via constructor for JsonConstructor

- GIVEN a `[PatchDocument]` class with `[JsonConstructor]` constructor `(string? name)`
- WHEN the source generator runs
- THEN the generated converter's `Read()` method buffers `name` into a local variable
- AND constructs the instance using constructor invocation: `new T(_name)`
- AND populates `_providedProperties` via `.Add()` calls for each provided property after construction
