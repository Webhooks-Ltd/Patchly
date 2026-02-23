## MODIFIED Requirements

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
