## ADDED Requirements

### Requirement: Per-Property JsonNumberHandling

The generated converter SHALL respect `[JsonNumberHandling]` attributes on individual properties, not just the global `JsonSerializerOptions` setting.

#### Scenario: Property-level JsonNumberHandling is respected
- **WHEN** a `[PatchDocument]` class has `[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public int? Count { get; set; }`
- **AND** the global `JsonSerializerOptions` does NOT have `AllowReadingFromString`
- **AND** JSON payload is `{"count": "42"}`
- **THEN** `Count` is `42`
- **AND** `WasProvided("Count")` returns true

## MODIFIED Requirements

### Requirement: Serialization Output

When a patch document is serialized back to JSON, it SHALL produce clean output that respects `JsonSerializerOptions` configuration.

#### Scenario: Serialization includes all properties with values

- GIVEN a `CustomerPatch` with `FirstName = "Alice"`, `LastName = null`, `Age = 30`
- AND all three were provided during deserialization
- WHEN serialized with System.Text.Json and default options
- THEN the JSON output contains `"firstName": "Alice"`, `"lastName": null`, `"age": 30`
- AND the output does NOT contain `_providedProperties`, `Provided`, or any tracking infrastructure

#### Scenario: Serialization of a manually constructed instance

- GIVEN a `CustomerPatch` created via `new CustomerPatch { FirstName = "Alice" }` (not deserialized)
- WHEN serialized with System.Text.Json
- THEN the JSON output contains all properties with their current values (including defaults/nulls for unset ones)
- AND no tracking fields appear in the output

#### Scenario: Serialization respects DefaultIgnoreCondition WhenWritingNull

- GIVEN a `CustomerPatch` with `FirstName = "Alice"` and `LastName = null` (both provided)
- AND `JsonSerializerOptions` with `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
- WHEN serialized
- THEN the JSON output contains `"firstName": "Alice"`
- AND `"lastName"` is omitted (null value suppressed by WhenWritingNull)

#### Scenario: Serialization respects DefaultIgnoreCondition WhenWritingDefault

- GIVEN a `CustomerPatch` with `int? Age = 0` (provided) and `string? Name = null` (provided)
- AND `JsonSerializerOptions` with `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault`
- WHEN serialized
- THEN both `age` and `name` are omitted (0 and null are defaults)

### Requirement: JsonSerializerOptions Compatibility

The generated converter SHALL work correctly with various JsonSerializerOptions configurations.

#### Scenario: Works with JsonSerializerDefaults.Web

- GIVEN `JsonSerializerOptions(JsonSerializerDefaults.Web)` (camelCase, case-insensitive, number from string)
- AND a `[PatchDocument]` class with `int? Count`
- AND JSON payload `{"count": "42"}` (number as string)
- WHEN deserialized
- THEN `Count` is `42` (number read from string allowed by Web defaults)
- AND `WasProvided("Count")` returns true

#### Scenario: Works with default JsonSerializerOptions

- GIVEN `new JsonSerializerOptions()` (PascalCase, case-sensitive)
- AND a `[PatchDocument]` class with `string? FirstName`
- AND JSON payload `{"FirstName": "Alice"}`
- WHEN deserialized
- THEN `FirstName` is `"Alice"` and `WasProvided("FirstName")` returns true

#### Scenario: Respects JsonIgnore attribute on properties

- GIVEN a `[PatchDocument]` class with `[JsonIgnore] public string? InternalNote { get; set; }`
- WHEN the source generator runs
- THEN the generated converter does NOT read or write `InternalNote` from/to JSON
- AND `InternalNote` is excluded from `WasProvided` tracking
- AND `InternalNote` is excluded from the `Provided` accessor

#### Scenario: Respects JsonInclude on non-public properties

- GIVEN a `[PatchDocument]` class with `[JsonInclude] internal string? SecretCode { get; set; }`
- WHEN the source generator runs
- THEN the generated converter includes `SecretCode` in deserialization, tracking, and the `Provided` accessor
