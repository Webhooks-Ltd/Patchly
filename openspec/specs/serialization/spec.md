# Serialization

## Purpose

Define the JSON deserialization and serialization behaviour of Patchly-generated converters, including property tracking, null vs absent distinction, case sensitivity, type coercion, and edge cases.
## Requirements
### Requirement: Null vs Absent Distinction

The generated converter SHALL distinguish between a JSON property explicitly set to `null` and a JSON property that is absent from the payload entirely. This applies to both the streaming and buffered deserialization paths.

#### Scenario: Property present with a non-null value

- GIVEN a `[PatchDocument]` class with `string? Email`
- AND JSON payload `{"email": "alice@example.com"}`
- WHEN deserialized
- THEN `Email` is `"alice@example.com"`
- AND `WasProvided("Email")` returns true

#### Scenario: Property present with null value

- GIVEN a `[PatchDocument]` class with `string? Email`
- AND JSON payload `{"email": null}`
- WHEN deserialized
- THEN `Email` is null
- AND `WasProvided("Email")` returns true

#### Scenario: Property absent from payload

- GIVEN a `[PatchDocument]` class with `string? Email`
- AND JSON payload `{"name": "Alice"}`
- WHEN deserialized
- THEN `Email` is null (default)
- AND `WasProvided("Email")` returns false

#### Scenario: Distinguishing null from absent in a real-world PATCH flow

- GIVEN a `Customer` entity with `Email = "old@example.com"` and `Phone = "555-1234"`
- AND a `CustomerPatch` deserialized from `{"email": null}`
- WHEN the developer checks `WasProvided` and applies changes manually
- THEN `customer.Email` is set to null (explicitly cleared)
- AND `customer.Phone` is `"555-1234"` (untouched because not provided)

#### Scenario: Null vs absent works with init-only properties

- GIVEN a `[PatchDocument]` class with `string? Email { get; init; }`
- AND JSON payload `{"email": null}`
- WHEN deserialized via the buffered path
- THEN `Email` is null
- AND `WasProvided("Email")` returns true

#### Scenario: Null vs absent works with JsonConstructor

- GIVEN a `[PatchDocument]` class with `[JsonConstructor]` constructor `(string? email)`
- AND JSON payload `{}`
- WHEN deserialized via the buffered path
- THEN `Email` is null (default passed to constructor)
- AND `WasProvided("Email")` returns false

### Requirement: Property Name Matching

The generated converter SHALL match JSON property names to C# properties using the configured naming policy.

#### Scenario: camelCase matching (default ASP.NET Core behaviour)

- GIVEN a `[PatchDocument]` class with property `FirstName`
- AND `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- AND JSON payload `{"firstName": "Alice"}`
- WHEN deserialized
- THEN `FirstName` is `"Alice"`
- AND `WasProvided("FirstName")` returns true

#### Scenario: Case-insensitive property matching

- GIVEN a `[PatchDocument]` class with property `FirstName`
- AND `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true`
- AND JSON payload `{"FIRSTNAME": "Alice"}`
- WHEN deserialized
- THEN `FirstName` is `"Alice"`
- AND `WasProvided("FirstName")` returns true

#### Scenario: JsonPropertyName attribute overrides naming policy

- GIVEN a `[PatchDocument]` class with `[JsonPropertyName("first_name")] public string? FirstName { get; set; }`
- AND JSON payload `{"first_name": "Alice"}`
- WHEN deserialized
- THEN `FirstName` is `"Alice"`
- AND `WasProvided("FirstName")` returns true

#### Scenario: JsonPropertyName attribute takes precedence over camelCase

- GIVEN a `[PatchDocument]` class with `[JsonPropertyName("given_name")] public string? FirstName { get; set; }`
- AND `JsonSerializerOptions` with `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`
- AND JSON payload `{"given_name": "Alice"}`
- WHEN deserialized
- THEN `FirstName` is `"Alice"`
- AND a JSON payload `{"firstName": "Alice"}` would NOT match this property

### Requirement: Supported Property Types

The generated converter SHALL correctly deserialize and track a variety of property types.

#### Scenario: String properties

- GIVEN a `[PatchDocument]` class with `string? Name`
- AND JSON payload `{"name": "Alice"}`
- WHEN deserialized
- THEN `Name` is `"Alice"` and `WasProvided("Name")` returns true

#### Scenario: Nullable int properties

- GIVEN a `[PatchDocument]` class with `int? Age`
- AND JSON payload `{"age": 30}`
- WHEN deserialized
- THEN `Age` is `30` and `WasProvided("Age")` returns true

#### Scenario: Nullable int property with null value

- GIVEN a `[PatchDocument]` class with `int? Age`
- AND JSON payload `{"age": null}`
- WHEN deserialized
- THEN `Age` is null and `WasProvided("Age")` returns true

#### Scenario: Boolean properties

- GIVEN a `[PatchDocument]` class with `bool? IsActive`
- AND JSON payload `{"isActive": false}`
- WHEN deserialized
- THEN `IsActive` is false and `WasProvided("IsActive")` returns true

#### Scenario: DateTime properties

- GIVEN a `[PatchDocument]` class with `DateTime? BirthDate`
- AND JSON payload `{"birthDate": "1990-05-15T00:00:00Z"}`
- WHEN deserialized
- THEN `BirthDate` is parsed correctly and `WasProvided("BirthDate")` returns true

#### Scenario: DateTimeOffset properties

- GIVEN a `[PatchDocument]` class with `DateTimeOffset? ModifiedAt`
- AND JSON payload `{"modifiedAt": "2024-01-15T10:30:00+02:00"}`
- WHEN deserialized
- THEN `ModifiedAt` is parsed correctly preserving the offset

#### Scenario: Guid properties

- GIVEN a `[PatchDocument]` class with `Guid? CategoryId`
- AND JSON payload `{"categoryId": "550e8400-e29b-41d4-a716-446655440000"}`
- WHEN deserialized
- THEN `CategoryId` is the expected GUID and `WasProvided("CategoryId")` returns true

#### Scenario: Enum properties

- GIVEN a `[PatchDocument]` class with `OrderStatus? Status` where `OrderStatus` is an enum
- AND JSON payload `{"status": "Shipped"}` (string enum)
- AND `JsonSerializerOptions` configured with `JsonStringEnumConverter`
- WHEN deserialized
- THEN `Status` is `OrderStatus.Shipped` and `WasProvided("Status")` returns true

#### Scenario: Enum properties with integer values

- GIVEN a `[PatchDocument]` class with `OrderStatus? Status` where `OrderStatus.Shipped = 2`
- AND JSON payload `{"status": 2}` (integer enum)
- AND no `JsonStringEnumConverter` configured
- WHEN deserialized
- THEN `Status` is `OrderStatus.Shipped` and `WasProvided("Status")` returns true

#### Scenario: Decimal and double properties

- GIVEN a `[PatchDocument]` class with `decimal? Price` and `double? Rating`
- AND JSON payload `{"price": 19.99, "rating": 4.5}`
- WHEN deserialized
- THEN `Price` is `19.99m` and `Rating` is `4.5` and both are tracked as provided

#### Scenario: Nested object properties

- GIVEN a `[PatchDocument]` class with `Address? ShippingAddress` where `Address` is a class with `Street`, `City`, `Zip`
- AND JSON payload `{"shippingAddress": {"street": "123 Main St", "city": "Springfield"}}`
- WHEN deserialized
- THEN `ShippingAddress` is an `Address` instance with the expected values
- AND `WasProvided("ShippingAddress")` returns true
- AND `ShippingAddress` is deserialized using the standard System.Text.Json converter for `Address` (not Patchly tracking)

#### Scenario: Nested object set to null

- GIVEN a `[PatchDocument]` class with `Address? ShippingAddress`
- AND JSON payload `{"shippingAddress": null}`
- WHEN deserialized
- THEN `ShippingAddress` is null
- AND `WasProvided("ShippingAddress")` returns true

#### Scenario: Collection properties (List)

- GIVEN a `[PatchDocument]` class with `List<string>? Tags`
- AND JSON payload `{"tags": ["urgent", "vip"]}`
- WHEN deserialized
- THEN `Tags` is a list containing `["urgent", "vip"]`
- AND `WasProvided("Tags")` returns true

#### Scenario: Collection property set to null

- GIVEN a `[PatchDocument]` class with `List<string>? Tags`
- AND JSON payload `{"tags": null}`
- WHEN deserialized
- THEN `Tags` is null
- AND `WasProvided("Tags")` returns true

#### Scenario: Collection property set to empty array

- GIVEN a `[PatchDocument]` class with `List<string>? Tags`
- AND JSON payload `{"tags": []}`
- WHEN deserialized
- THEN `Tags` is an empty list (not null)
- AND `WasProvided("Tags")` returns true

#### Scenario: Array properties

- GIVEN a `[PatchDocument]` class with `int[]? Scores`
- AND JSON payload `{"scores": [100, 95, 88]}`
- WHEN deserialized
- THEN `Scores` is an array `[100, 95, 88]`
- AND `WasProvided("Scores")` returns true

#### Scenario: Dictionary properties

- GIVEN a `[PatchDocument]` class with `Dictionary<string, string>? Metadata`
- AND JSON payload `{"metadata": {"key1": "value1", "key2": "value2"}}`
- WHEN deserialized
- THEN `Metadata` is a dictionary with the expected entries
- AND `WasProvided("Metadata")` returns true

### Requirement: Unknown Property Handling

The generated converter SHALL handle unrecognized JSON properties according to the `UnknownPropertyHandling` configured on the `[PatchDocument]` attribute. When set to `Ignore` (default), unknown properties are silently skipped. When set to `Reject`, the converter SHALL collect all unknown property names during iteration and throw a `JsonException` after the full object is read, listing every unrecognized property.

This applies to both the streaming and buffered converter codegen paths. The resolver path (.NET 8+) uses STJ's native `UnmappedMemberHandling` instead.

#### Scenario: Streaming converter path skips unknown properties in Ignore mode

- **WHEN** a patch document with `UnknownPropertyHandling = Ignore` is deserialized via the streaming converter path
- **AND** the JSON contains unrecognized properties
- **THEN** the converter calls `reader.Read()` and `reader.Skip()` for each unknown property
- **AND** deserialization succeeds with only recognized properties tracked

#### Scenario: Buffered converter path skips unknown properties in Ignore mode

- **WHEN** a patch document with `UnknownPropertyHandling = Ignore` is deserialized via the buffered converter path
- **AND** the JSON contains unrecognized properties
- **THEN** the converter calls `reader.Read()` and `reader.Skip()` for each unknown property
- **AND** deserialization succeeds with only recognized properties tracked

#### Scenario: Streaming converter path rejects unknown properties in Reject mode

- **WHEN** a patch document with `UnknownPropertyHandling = Reject` is deserialized via the streaming converter path
- **AND** the JSON contains unrecognized properties
- **THEN** the converter collects unknown property names during iteration
- **AND** throws a `JsonException` after the full object is read

#### Scenario: Buffered converter path rejects unknown properties in Reject mode

- **WHEN** a patch document with `UnknownPropertyHandling = Reject` is deserialized via the buffered converter path
- **AND** the JSON contains unrecognized properties
- **THEN** the converter collects unknown property names during iteration
- **AND** throws a `JsonException` after the full object is read

#### Scenario: Buffered path with init-only properties rejects unknowns

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with init-only properties (triggers buffered deserialization)
- **WHEN** deserialized from JSON containing unrecognized properties
- **THEN** a `JsonException` is thrown listing all unknown property names

#### Scenario: Resolver path sets UnmappedMemberHandling on .NET 8+

- **GIVEN** a streaming-path `[PatchDocument(UnknownPropertyHandling = Reject)]` class
- **WHEN** `PatchlyJsonTypeInfoResolver.GetTypeInfo` emits the `JsonTypeInfo` for this type
- **THEN** the `JsonTypeInfo.UnmappedMemberHandling` property is set to `JsonUnmappedMemberHandling.Disallow`

#### Scenario: Resolver path sets UnmappedMemberHandling to Skip for Ignore mode

- **GIVEN** a streaming-path `[PatchDocument]` class (default Ignore)
- **WHEN** `PatchlyJsonTypeInfoResolver.GetTypeInfo` emits the `JsonTypeInfo` for this type
- **THEN** the `JsonTypeInfo.UnmappedMemberHandling` property is set to `JsonUnmappedMemberHandling.Skip`
- **AND** unknown properties are ignored even if the app globally configures `JsonUnmappedMemberHandling.Disallow`

#### Scenario: Nested deserialization preserves serializer delegation

- **GIVEN** a parent `[PatchDocument]` with a nested `[PatchDocument]` property
- **WHEN** the parent converter deserializes the nested property
- **THEN** it delegates to `JsonSerializer.Deserialize<T>(ref reader, options)`
- **AND** does NOT directly invoke the nested converter or any internal method

### Requirement: Duplicate Properties in JSON

The converter SHALL handle JSON payloads that contain the same property name multiple times by using the last value.

#### Scenario: Duplicate property uses last value

- GIVEN a `[PatchDocument]` class with `string? Name`
- AND JSON payload `{"name": "Alice", "name": "Bob"}`
- WHEN deserialized
- THEN `Name` is `"Bob"` (last value wins, consistent with System.Text.Json default behaviour)
- AND `WasProvided("Name")` returns true

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

### Requirement: Per-Property JsonNumberHandling

The generated converter SHALL respect `[JsonNumberHandling]` attributes on individual properties, not just the global `JsonSerializerOptions` setting.

#### Scenario: Property-level JsonNumberHandling is respected
- **WHEN** a `[PatchDocument]` class has `[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] public int? Count { get; set; }`
- **AND** the global `JsonSerializerOptions` does NOT have `AllowReadingFromString`
- **AND** JSON payload is `{"count": "42"}`
- **THEN** `Count` is `42`
- **AND** `WasProvided("Count")` returns true

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

### Requirement: Error Handling During Deserialization

The converter SHALL handle malformed or type-mismatched JSON by throwing `JsonException` with descriptive messages.

#### Scenario: Type mismatch throws JsonException

- GIVEN a `[PatchDocument]` class with `int? Age`
- AND JSON payload `{"age": "not-a-number"}`
- AND standard `JsonSerializerOptions` (not Web defaults)
- WHEN deserialized
- THEN a `JsonException` is thrown
- AND the exception message indicates the type mismatch

#### Scenario: Malformed JSON throws JsonException

- GIVEN a `[PatchDocument]` class
- AND JSON payload `{"name": "Alice"` (missing closing brace)
- WHEN deserialized
- THEN a `JsonException` is thrown

#### Scenario: Valid JSON array instead of object throws JsonException

- GIVEN a `[PatchDocument]` class
- AND JSON payload `[1, 2, 3]`
- WHEN deserialized
- THEN a `JsonException` is thrown
- AND the message indicates an object was expected

### Requirement: Deterministic State Derivation from JSON

In deterministic mode, deserialization SHALL derive tri-state property status directly from JSON token presence and value.

#### Scenario: Null and absent are distinguished in deterministic mode
- **GIVEN** a deterministic patch document with `string? Email`
- **AND** payload variants `{}` and `{"email": null}`
- **WHEN** both payloads are deserialized
- **THEN** the first yields `Omitted` state for `Email`
- **AND** the second yields `Null` state for `Email`

#### Scenario: Explicit value yields Value state in deterministic mode
- **GIVEN** a deterministic patch document with `string? Email`
- **AND** payload `{"email": "alice@example.com"}`
- **WHEN** deserialized
- **THEN** state for `Email` is `Value`

### Requirement: Deterministic Nested and Collection Serialization Semantics

In deterministic mode, nested objects and collections SHALL preserve deterministic patch intent during deserialization.

#### Scenario: Nested object payload maps to Value state
- **GIVEN** a deterministic patch document with nested property `AddressPatch? Address`
- **AND** payload `{"address": {"city": "Seattle"}}`
- **WHEN** deserialized
- **THEN** state for `Address` is `Value`
- **AND** nested fields are tracked independently by the nested patch document

#### Scenario: Collection empty array remains explicit Value state
- **GIVEN** a deterministic patch document with `List<string>? Tags`
- **AND** payload `{"tags": []}`
- **WHEN** deserialized
- **THEN** state for `Tags` is `Value`
- **AND** deserialized value is an empty collection

#### Scenario: Collection null remains explicit Null state
- **GIVEN** a deterministic patch document with `List<string>? Tags`
- **AND** payload `{"tags": null}`
- **WHEN** deserialized
- **THEN** state for `Tags` is `Null`

### Requirement: Deterministic Mode Does Not Alter JSON Output Shape

Deterministic mode SHALL not introduce new tracking members into serialized JSON payloads.

#### Scenario: State infrastructure remains excluded from output JSON
- **GIVEN** a deterministic patch document instance with tracking metadata
- **WHEN** serialized with `System.Text.Json`
- **THEN** output JSON includes only model properties
- **AND** output JSON excludes state/tracking infrastructure members
