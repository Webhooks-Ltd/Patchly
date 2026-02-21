## ADDED Requirements

### Requirement: Provided Accessor Hidden From Schema

The generated `Provided` property and `ProvidedSet` struct SHALL NOT appear in OpenAPI schemas.

#### Scenario: Provided property is hidden via JsonIgnore
- **WHEN** an OpenAPI schema is generated for a `[PatchDocument]` class
- **THEN** the `Provided` property does NOT appear in the schema
- **AND** the `ProvidedSet` type does NOT appear as a separate schema definition

#### Scenario: ProvidedSet does not leak as a referenced type
- **WHEN** NSwag or Kiota generates a client from the OpenAPI schema
- **THEN** no `ProvidedSet` type appears in the generated client code

## MODIFIED Requirements

### Requirement: Schema Transparency

A `[PatchDocument]` class SHALL appear in the OpenAPI schema as a plain object with nullable properties. No Patchly infrastructure (tracking fields, converter types, internal methods, Provided accessor) SHALL be visible.

#### Scenario: Patch class appears as a flat object schema

- GIVEN a `[PatchDocument]` class with properties `string? FirstName`, `string? LastName`, `int? Age`
- AND an ASP.NET Core API endpoint that accepts this class as a `[FromBody]` parameter
- WHEN the OpenAPI schema is generated (via the built-in .NET OpenAPI support)
- THEN the schema for the patch class shows three properties: `firstName`, `lastName`, `age`
- AND no additional properties from Patchly infrastructure appear

#### Scenario: Tracking field is hidden from OpenAPI schema

- GIVEN a `[PatchDocument]` class with a generated `_providedProperties` field decorated with `[JsonIgnore]`
- WHEN the OpenAPI schema is generated
- THEN `_providedProperties` does NOT appear in the schema
- AND no `HashSet` or `IReadOnlySet` type appears in the schema

#### Scenario: WasProvided method is not in schema

- GIVEN a `[PatchDocument]` class with generated `WasProvided`, `ProvidedProperties`, and `Provided` members
- WHEN the OpenAPI schema is generated
- THEN none of `wasProvided`, `providedProperties`, or `provided` appear as properties in the schema
- AND the `[JsonIgnore]` attribute on these members prevents their inclusion

#### Scenario: Nested converter type is not exposed

- GIVEN a `[PatchDocument]` class with a nested `CustomerPatchJsonConverter` class
- WHEN the OpenAPI schema is generated
- THEN `CustomerPatchJsonConverter` does NOT appear as a separate schema
- AND no reference to the converter type appears in any schema definition

