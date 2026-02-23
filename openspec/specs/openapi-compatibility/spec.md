# OpenAPI Compatibility

## Purpose

Ensure Patchly-generated patch documents SHALL produce clean, transparent OpenAPI schemas. Generated tracking infrastructure MUST NOT leak into API documentation or client-generated code.
## Requirements
### Requirement: Schema Transparency

A `[PatchDocument]` class SHALL appear in the OpenAPI schema as a plain object with nullable properties. No Patchly infrastructure (tracking fields, converter types, internal methods, Provided accessor) SHALL be visible.

#### Scenario: Patch class appears as a flat object schema

- **GIVEN** a streaming-path `[PatchDocument]` class with properties `string? FirstName`, `string? LastName`, `int? Age`
- **AND** an ASP.NET Core API endpoint that accepts this class as a `[FromBody]` parameter
- **AND** `AddPatchly()` is configured
- **WHEN** the OpenAPI schema is generated (via the built-in .NET OpenAPI support)
- **THEN** the schema for the patch class shows three properties: `firstName`, `lastName`, `age`
- **AND** no additional properties from Patchly infrastructure appear

#### Scenario: Tracking field is hidden from OpenAPI schema

- **GIVEN** a `[PatchDocument]` class with a generated `_providedProperties` field decorated with `[JsonIgnore]`
- **AND** `AddPatchly()` is configured
- **WHEN** the OpenAPI schema is generated
- **THEN** `_providedProperties` does NOT appear in the schema
- **AND** no `HashSet` or `IReadOnlySet` type appears in the schema

#### Scenario: WasProvided method is not in schema

- **GIVEN** a `[PatchDocument]` class with generated `WasProvided`, `ProvidedProperties`, and `Provided` members
- **AND** `AddPatchly()` is configured
- **WHEN** the OpenAPI schema is generated
- **THEN** none of `wasProvided`, `providedProperties`, or `provided` appear as properties in the schema
- **AND** the `[JsonIgnore]` attribute on these members prevents their inclusion

#### Scenario: Nested converter type is not exposed

- **GIVEN** a `[PatchDocument]` class with a nested `CustomerPatchJsonConverter` class
- **AND** `AddPatchly()` is configured
- **WHEN** the OpenAPI schema is generated
- **THEN** `CustomerPatchJsonConverter` does NOT appear as a separate schema
- **AND** no reference to the converter type appears in any schema definition

### Requirement: Provided Accessor Hidden From Schema

The generated `Provided` property and `ProvidedSet` struct SHALL NOT appear in OpenAPI schemas.

#### Scenario: Provided property is hidden via JsonIgnore
- WHEN an OpenAPI schema is generated for a `[PatchDocument]` class
- THEN the `Provided` property does NOT appear in the schema
- AND the `ProvidedSet` type does NOT appear as a separate schema definition

#### Scenario: ProvidedSet does not leak as a referenced type
- WHEN NSwag or Kiota generates a client from the OpenAPI schema
- THEN no `ProvidedSet` type appears in the generated client code

### Requirement: Property Nullability in Schema

All properties on a patch document SHALL be nullable in the OpenAPI schema, since any property might be sent as null to clear the field.

#### Scenario: String property is nullable in schema

- GIVEN a `[PatchDocument]` class with `string? FirstName`
- WHEN the OpenAPI schema is generated
- THEN the `firstName` property has `"nullable": true` (OpenAPI 3.0) or `"type": ["string", "null"]` (OpenAPI 3.1)

#### Scenario: Integer property is nullable in schema

- GIVEN a `[PatchDocument]` class with `int? Age`
- WHEN the OpenAPI schema is generated
- THEN the `age` property has `"nullable": true` or equivalent for the OpenAPI version

#### Scenario: Nested object property is nullable in schema

- GIVEN a `[PatchDocument]` class with `Address? ShippingAddress`
- WHEN the OpenAPI schema is generated
- THEN the `shippingAddress` property is nullable
- AND it references the `Address` schema with a nullable wrapper

### Requirement: No Required Properties

In a PATCH payload, every property is optional — the client sends only the fields it wants to change. The OpenAPI schema SHALL reflect this by having no required properties.

#### Scenario: No properties are marked as required

- GIVEN a `[PatchDocument]` class with properties `string? FirstName`, `string? LastName`, `int? Age`
- WHEN the OpenAPI schema is generated
- THEN the schema has no `required` array
- OR the `required` array is empty

#### Scenario: C# required modifier does not make the property required in schema

- GIVEN a `[PatchDocument]` class with `public required string? FirstName { get; set; }`
- WHEN the OpenAPI schema is generated
- THEN `firstName` is NOT listed in the `required` array

### Requirement: NSwag Client Compatibility

Generated NSwag clients SHALL be able to send partial payloads to endpoints accepting `[PatchDocument]` types.

#### Scenario: NSwag generates a clean client model

- GIVEN an OpenAPI schema generated from a `[PatchDocument]` class with `string? FirstName`, `int? Age`
- WHEN NSwag generates a C# client from this schema
- THEN the generated client model has `string? FirstName` and `int? Age` properties
- AND no Patchly infrastructure types appear in the generated client

#### Scenario: NSwag client sends only non-null properties with WhenWritingNull

- GIVEN an NSwag-generated client model for a patch endpoint
- AND the client serializer is configured with `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`
- AND the developer sets only `FirstName = "Alice"` on the client model (leaving `Age` as null)
- WHEN the client sends the PATCH request
- THEN the JSON payload is `{"firstName": "Alice"}` (age is omitted because it is null)
- AND the server correctly tracks only `FirstName` as provided

#### Scenario: NSwag client sends explicit null via JsonObject

- GIVEN an NSwag-generated client where the developer needs to explicitly set a field to null
- WHEN the developer constructs a `JsonObject` manually: `new JsonObject { ["firstName"] = null }`
- AND sends it as the PATCH body
- THEN the JSON payload is `{"firstName": null}`
- AND the server tracks `FirstName` as provided with a null value

### Requirement: Kiota Client Compatibility

Generated Kiota clients SHALL correctly handle partial payloads through their built-in backing store.

#### Scenario: Kiota generates a clean client model

- GIVEN an OpenAPI schema generated from a `[PatchDocument]` class
- WHEN Kiota generates a client from this schema
- THEN the generated model has properties matching the patch class
- AND no Patchly infrastructure types appear

#### Scenario: Kiota backing store tracks changes automatically

- GIVEN a Kiota-generated client model for a patch endpoint
- AND the developer sets `FirstName = "Alice"` and `LastName = null` on the model
- WHEN the client serializes the model
- THEN the JSON payload includes `{"firstName": "Alice", "lastName": null}`
- AND properties not touched by the developer are omitted from the payload
- AND the server correctly distinguishes the explicit null (`lastName`) from absent properties

#### Scenario: Kiota client works without additional configuration

- GIVEN a Kiota-generated client for an API with `[PatchDocument]` endpoints
- WHEN the developer uses the client with default settings
- THEN partial updates work correctly out of the box
- AND no special serializer configuration is needed on the client side

### Requirement: Built-in .NET OpenAPI Support (Microsoft.AspNetCore.OpenApi)

For .NET 9+ projects using the built-in OpenAPI document generation, Patchly schemas SHALL be correct when `AddPatchly()` is called during service registration. The resolver MUST return `JsonTypeInfo` with `Kind = Object` for streaming-path types so that `JsonSchemaExporter` can introspect the type's properties.

#### Scenario: Built-in OpenAPI shows properties when AddPatchly is configured

- **GIVEN** a .NET 9+ project using `builder.Services.AddOpenApi()` and `builder.Services.AddPatchly()`
- **AND** a streaming-path `[PatchDocument]` class with `string? FirstName`, `int? Age`
- **WHEN** the OpenAPI document is generated at `/openapi/v1.json`
- **THEN** the schema for the patch class has properties `firstName` and `age`
- **AND** both properties have correct types (`string`, `integer`)
- **AND** both properties are nullable

#### Scenario: Built-in OpenAPI respects JsonIgnore on tracking members

- **GIVEN** a .NET 9+ project using `builder.Services.AddOpenApi()` and `builder.Services.AddPatchly()`
- **AND** a `[PatchDocument]` class with `[JsonIgnore]` on tracking members
- **WHEN** the OpenAPI document is generated
- **THEN** tracking members (`_providedProperties`, `WasProvided`, `ProvidedProperties`, `Provided`) do NOT appear in the schema

#### Scenario: Built-in OpenAPI shows nullable properties

- **GIVEN** a .NET 9+ project using built-in OpenAPI and `AddPatchly()`
- **AND** a `[PatchDocument]` class with `string? FirstName`, `int? Age`
- **WHEN** the OpenAPI document is generated
- **THEN** both properties appear as nullable in the schema

#### Scenario: Buffered-path types have empty schemas without workaround

- **GIVEN** a .NET 9+ project using `builder.Services.AddOpenApi()` and `builder.Services.AddPatchly()`
- **AND** a buffered-path `[PatchDocument]` class (with init-only properties)
- **WHEN** the OpenAPI document is generated
- **THEN** the schema for the patch class is empty (`{ }`) because the resolver falls back to converter-wrapped `Kind = None`

