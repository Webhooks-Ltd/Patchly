## MODIFIED Requirements

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
