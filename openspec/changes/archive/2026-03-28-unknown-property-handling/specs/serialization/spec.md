## MODIFIED Requirements

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
