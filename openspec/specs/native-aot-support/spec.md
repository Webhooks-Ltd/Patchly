# Native AOT Support

## Purpose

Define the PatchlyJsonTypeInfoResolver that enables Native AOT and trimming-safe JSON deserialization for all `[PatchDocument]` types without runtime reflection.
## Requirements
### Requirement: PatchlyJsonTypeInfoResolver Generation

The source generator SHALL emit a `PatchlyJsonTypeInfoResolver` class that implements `IJsonTypeInfoResolver`. For streaming-path types (parameterless constructor + settable properties), the resolver SHALL return `JsonTypeInfo` with `Kind = Object` and populated `Properties` collection using custom `Get`/`Set` delegates. For buffered-path types (init-only properties or `[JsonConstructor]`), the resolver SHALL return `JsonTypeInfo` wrapping the converter via `CreateValueInfo`. The `Set` delegates on streaming-path properties SHALL both assign the property value and call the type's `MarkProvided` method to track which properties were present.

#### Scenario: Resolver is generated when PatchDocument types exist

- **WHEN** one or more `[PatchDocument]` classes exist in the assembly
- **THEN** the generator emits a file `PatchlyJsonTypeInfoResolver.g.cs`
- **AND** it contains a `PatchlyJsonTypeInfoResolver` class in the `Patchly` namespace
- **AND** the class implements `System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver`
- **AND** the class has a `public static PatchlyJsonTypeInfoResolver Default` singleton property

#### Scenario: Resolver returns Object-kinded TypeInfo for streaming-path types

- **WHEN** `GetTypeInfo(typeof(CustomerPatch), options)` is called
- **AND** `CustomerPatch` is a streaming-path `[PatchDocument]` class (settable properties, parameterless constructor)
- **THEN** the resolver returns a non-null `JsonTypeInfo<CustomerPatch>`
- **AND** the `JsonTypeInfo.Kind` is `JsonTypeInfoKind.Object`
- **AND** the `JsonTypeInfo.Properties` collection contains one `JsonPropertyInfo` per tracked property
- **AND** each `JsonPropertyInfo` has the correct `PropertyType` and JSON property name

#### Scenario: Resolver returns converter-wrapped TypeInfo for buffered-path types

- **WHEN** `GetTypeInfo(typeof(BufferedPatch), options)` is called
- **AND** `BufferedPatch` is a buffered-path `[PatchDocument]` class (init-only properties or `[JsonConstructor]`)
- **THEN** the resolver returns a non-null `JsonTypeInfo<BufferedPatch>`
- **AND** the `JsonTypeInfo` uses the generated converter for deserialization

#### Scenario: Resolver returns null for unknown types

- **WHEN** `GetTypeInfo(typeof(string), options)` is called
- **THEN** the resolver returns `null`
- **AND** the resolver chain falls through to the next resolver

#### Scenario: Resolver handles multiple PatchDocument types

- **WHEN** the assembly contains `CustomerPatch`, `OrderPatch`, and `ProductPatch` all decorated with `[PatchDocument]`
- **THEN** the resolver returns correct `JsonTypeInfo` for each type
- **AND** streaming-path types get `Kind = Object` type info
- **AND** buffered-path types get converter-wrapped type info

#### Scenario: Resolver is gated behind NET8_0_OR_GREATER

- **WHEN** the consuming project targets .NET 6 or .NET 7
- **THEN** the `PatchlyJsonTypeInfoResolver` class is not compiled (excluded by `#if NET8_0_OR_GREATER`)
- **AND** no compilation errors occur

#### Scenario: Resolver is available on .NET 8+

- **WHEN** the consuming project targets .NET 8 or later
- **THEN** `PatchlyJsonTypeInfoResolver` is available and usable

### Requirement: AOT Deserialization with Resolver

When `PatchlyJsonTypeInfoResolver` is added to the `TypeInfoResolverChain`, deserialization of streaming-path `[PatchDocument]` types SHALL work identically to the converter-based path, including full property tracking. The resolver's `Set` delegates SHALL assign property values and call `MarkProvided` atomically during deserialization.

#### Scenario: Null vs absent distinction works via resolver

- **WHEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** a streaming-path `CustomerPatch` is deserialized from `{"firstName": "Alice", "age": null}`
- **THEN** `WasProvided("FirstName")` returns true
- **AND** `WasProvided("Age")` returns true (explicitly null)
- **AND** `WasProvided("LastName")` returns false (absent)

#### Scenario: Provided accessor works via resolver

- **WHEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** a streaming-path `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.FirstName` is true
- **AND** `patch.Provided.LastName` is false

#### Scenario: Serialization works via resolver

- **WHEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** a `CustomerPatch` is serialized
- **THEN** the output JSON contains property values without tracking infrastructure
- **AND** `[JsonIgnore]`-decorated members are excluded

#### Scenario: Resolver deserialization matches converter deserialization

- **WHEN** the same JSON payload is deserialized via the resolver path (resolver in chain)
- **AND** via the converter path (no resolver, `[JsonConverter]` fallback)
- **THEN** both paths produce identical `WasProvided` results for all properties
- **AND** both paths produce identical property values

#### Scenario: Converter in options.Converters overrides resolver

- **GIVEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** the generated converter is also added to `options.Converters`
- **WHEN** `GetTypeInfo` is called for a streaming-path type
- **THEN** the result MAY have `Kind = None` because `CreateJsonTypeInfo<T>` resolves converters from `options.Converters`
- **AND** this configuration is unsupported — users MUST NOT add Patchly converters to `options.Converters` when the resolver is active

### Requirement: Resolver Not Generated When No PatchDocument Types

The generator SHALL NOT emit the resolver file when there are no valid `[PatchDocument]` types.

#### Scenario: No resolver emitted for empty assembly

- **WHEN** the assembly contains no `[PatchDocument]` classes
- **THEN** no `PatchlyJsonTypeInfoResolver.g.cs` file is generated

