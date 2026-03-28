# Unknown Property Handling

## Purpose

Define Patchly's opt-in strict handling for unrecognized JSON properties during deserialization across both converter and resolver paths.

## Requirements

### Requirement: UnknownPropertyHandling enum

The core library SHALL expose an `UnknownPropertyHandling` enum with values `Ignore` (0) and `Reject` (1).

#### Scenario: Enum exists in Patchly namespace

- **WHEN** a developer references `Patchly.UnknownPropertyHandling`
- **THEN** the enum is available with members `Ignore` and `Reject`

### Requirement: PatchDocumentAttribute exposes UnknownPropertyHandling

The `PatchDocumentAttribute` SHALL have a public property `UnknownPropertyHandling` of type `UnknownPropertyHandling`, defaulting to `Ignore`.

#### Scenario: Default attribute value

- **WHEN** a class is decorated with `[PatchDocument]` without specifying `UnknownPropertyHandling`
- **THEN** the generated converter SHALL ignore unknown properties
- **AND** the resolver-emitted `JsonTypeInfo` SHALL have `UnmappedMemberHandling` set to `Skip`, ensuring Ignore behavior even if the app globally configures `Disallow`

#### Scenario: Explicit Reject value

- **WHEN** a class is decorated with `[PatchDocument(UnknownPropertyHandling = UnknownPropertyHandling.Reject)]`
- **THEN** the generated converter and resolver-emitted `JsonTypeInfo` SHALL reject payloads containing unrecognized properties

### Requirement: Reject mode fails on unknown top-level properties (converter path)

When `UnknownPropertyHandling` is `Reject`, the generated converter SHALL throw a `JsonException` if the JSON payload contains any property names that do not match a tracked property on the patch document. The exception message SHALL follow the format: `Unknown JSON properties on {TypeName}: '{name1}', '{name2}'`.

#### Scenario: Single unknown top-level property

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class `CustomerPatch` with properties `FirstName` and `LastName`
- **WHEN** deserialized via the converter path from `{"firstName":"Alice","unknownProp":"value"}`
- **THEN** a `JsonException` is thrown
- **AND** the exception message is `Unknown JSON properties on CustomerPatch: 'unknownProp'`

#### Scenario: Multiple unknown top-level properties

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class `CustomerPatch` with property `FirstName`
- **WHEN** deserialized via the converter path from `{"firstName":"Alice","foo":"x","bar":"y"}`
- **THEN** a `JsonException` is thrown
- **AND** the exception message contains both `'foo'` and `'bar'`

#### Scenario: All properties known with Reject mode

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with properties `FirstName` and `Age`
- **WHEN** deserialized from `{"firstName":"Alice","age":30}`
- **THEN** deserialization succeeds
- **AND** `WasProvided("FirstName")` returns true
- **AND** `WasProvided("Age")` returns true

#### Scenario: Empty object with Reject mode

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with properties `FirstName` and `Age`
- **WHEN** deserialized from `{}`
- **THEN** deserialization succeeds
- **AND** no properties are marked as provided

#### Scenario: Null token with Reject mode

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class
- **WHEN** deserialized from `null`
- **THEN** the result is null
- **AND** no exception is thrown

#### Scenario: Duplicate known property names with Reject mode

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with property `FirstName`
- **WHEN** deserialized from `{"firstName":"Alice","firstName":"Bob"}`
- **THEN** deserialization succeeds (last value wins, matching current behavior)
- **AND** `FirstName` is `"Bob"`

### Requirement: Reject mode fails on unknown properties (resolver path, .NET 8+)

When `UnknownPropertyHandling` is `Reject` and deserialization occurs through `PatchlyJsonTypeInfoResolver`, the resolver-emitted `JsonTypeInfo` SHALL have `UnmappedMemberHandling` set to `JsonUnmappedMemberHandling.Disallow`. STJ natively enforces rejection with its own `JsonException` format.

#### Scenario: Resolver path rejects unknown properties

- **GIVEN** a streaming-path `[PatchDocument(UnknownPropertyHandling = Reject)]` class with properties `FirstName` and `Age`
- **AND** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **WHEN** deserialized from `{"firstName":"Alice","unknownProp":"value"}`
- **THEN** a `JsonException` is thrown

#### Scenario: Resolver path accepts valid payloads

- **GIVEN** a streaming-path `[PatchDocument(UnknownPropertyHandling = Reject)]` class with properties `FirstName` and `Age`
- **AND** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **WHEN** deserialized from `{"firstName":"Alice","age":30}`
- **THEN** deserialization succeeds
- **AND** property tracking works correctly

#### Scenario: Resolver/converter parity for Reject mode

- **GIVEN** a streaming-path `[PatchDocument(UnknownPropertyHandling = Reject)]` class
- **WHEN** the same payload with an unknown property is deserialized via both converter and resolver paths
- **THEN** both paths throw a `JsonException`

#### Scenario: Resolver/converter parity for Ignore mode

- **GIVEN** a streaming-path `[PatchDocument]` class (default Ignore)
- **WHEN** the same payload with an unknown property is deserialized via both converter and resolver paths
- **THEN** both paths succeed with identical property tracking

### Requirement: Each type's own setting is authoritative

Each `[PatchDocument]` type's `UnknownPropertyHandling` setting controls only its own level. Nested `[PatchDocument]` properties continue to use `JsonSerializer.Deserialize<T>(ref reader, options)`, and each nested type's own converter/resolver handles its own enforcement independently. There is no cross-type error aggregation.

#### Scenario: Parent Reject with child Ignore — child ignores its unknowns

- **GIVEN** a parent `[PatchDocument(UnknownPropertyHandling = Reject)]` with nested `Address` that is `[PatchDocument]` (default Ignore)
- **WHEN** deserialized from `{"address":{"city":"Leeds","unknownNested":"x"}}`
- **THEN** deserialization succeeds
- **AND** `unknownNested` is silently ignored by the child's Ignore mode
- **AND** the parent does not report any unknowns (all parent-level properties are known)

#### Scenario: Parent Ignore with child Reject — child throws independently

- **GIVEN** a parent `[PatchDocument]` (default Ignore) with nested `Address` that is `[PatchDocument(UnknownPropertyHandling = Reject)]`
- **WHEN** deserialized from `{"unknownTop":"x","address":{"unknownNested":"y"}}`
- **THEN** a `JsonException` is thrown by the child converter
- **AND** the exception message contains `'unknownNested'`
- **AND** the exception message does NOT contain `'unknownTop'`

#### Scenario: Both parent and child Reject with unknowns only at child level

- **GIVEN** a parent `[PatchDocument(UnknownPropertyHandling = Reject)]` with nested `Address` also `[PatchDocument(UnknownPropertyHandling = Reject)]`
- **WHEN** deserialized from `{"address":{"unknownNested":"x"}}`
- **THEN** a `JsonException` is thrown by the child converter
- **AND** the exception message contains `'unknownNested'`

#### Scenario: Both parent and child Reject with unknowns at both levels

- **GIVEN** a parent `[PatchDocument(UnknownPropertyHandling = Reject)]` with nested `Address` also `[PatchDocument(UnknownPropertyHandling = Reject)]`
- **WHEN** deserialized from `{"unknownTop":"x","address":{"unknownNested":"y"}}`
- **THEN** the child's `JsonException` for `'unknownNested'` surfaces first (thrown during parent's property loop)
- **AND** the parent's own `'unknownTop'` error surfaces on a subsequent attempt after the child error is fixed

#### Scenario: Null nested patch document under Reject mode

- **GIVEN** a parent `[PatchDocument(UnknownPropertyHandling = Reject)]` with nested `Address` property
- **WHEN** deserialized from `{"address":null,"unknownProp":"x"}`
- **THEN** a `JsonException` is thrown for `'unknownProp'`
- **AND** the null `Address` is handled without error

### Requirement: Reject mode uses JSON property names in errors

Error messages SHALL report the JSON property name the client sent, respecting `[JsonPropertyName]` overrides and the configured naming policy, not the C# property name.

#### Scenario: JsonPropertyName override with unknown sibling

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with `[JsonPropertyName("first_name")] string? FirstName`
- **WHEN** deserialized from `{"first_name":"Alice","bad_prop":"x"}`
- **THEN** a `JsonException` is thrown
- **AND** the exception message contains `'bad_prop'`
- **AND** `'first_name'` is NOT listed as unknown

#### Scenario: Case-insensitive matching does not report known property as unknown

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with property `FirstName`
- **AND** `PropertyNameCaseInsensitive` is true
- **WHEN** deserialized from `{"FIRSTNAME":"Alice"}`
- **THEN** deserialization succeeds
- **AND** `WasProvided("FirstName")` returns true

### Requirement: JsonIgnore properties reported as unknown under Reject mode

When a property has `[JsonIgnore]`, it is excluded from the tracked property set. Under `Reject` mode, if the JSON payload contains a property name that corresponds to a `[JsonIgnore]`-decorated property, it SHALL be treated as unknown. This is correct behavior — the client is sending something that will be silently dropped, and Reject mode means strict contract enforcement.

#### Scenario: JsonIgnore property in payload with Reject mode

- **GIVEN** a `[PatchDocument(UnknownPropertyHandling = Reject)]` class with `[JsonIgnore] string? InternalField` and tracked property `FirstName`
- **WHEN** deserialized from `{"firstName":"Alice","internalField":"secret"}`
- **THEN** a `JsonException` is thrown
- **AND** the exception message contains `'internalField'`

### Requirement: Ignore mode preserves current behavior

When `UnknownPropertyHandling` is `Ignore` (default), the generated converter SHALL silently skip unrecognized properties, exactly as it does today. The resolver-emitted `JsonTypeInfo` SHALL set `UnmappedMemberHandling` to `Skip` to ensure the Patchly attribute takes precedence over any global `JsonSerializerOptions.UnmappedMemberHandling`.

#### Scenario: Unknown properties silently ignored (default)

- **GIVEN** a `[PatchDocument]` class with property `FirstName` (no `UnknownPropertyHandling` specified)
- **WHEN** deserialized from `{"firstName":"Alice","unknownProp":"value"}`
- **THEN** deserialization succeeds
- **AND** `FirstName` is `"Alice"`
- **AND** `WasProvided("FirstName")` returns true
