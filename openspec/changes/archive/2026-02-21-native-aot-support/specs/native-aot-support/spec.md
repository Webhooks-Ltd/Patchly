## ADDED Requirements

### Requirement: PatchlyJsonTypeInfoResolver Generation

The source generator SHALL emit a `PatchlyJsonTypeInfoResolver` class that implements `IJsonTypeInfoResolver` and provides `JsonTypeInfo<T>` for all `[PatchDocument]` types in the assembly.

#### Scenario: Resolver is generated when PatchDocument types exist
- **WHEN** one or more `[PatchDocument]` classes exist in the assembly
- **THEN** the generator emits a file `PatchlyJsonTypeInfoResolver.g.cs`
- **AND** it contains a `PatchlyJsonTypeInfoResolver` class in the `Patchly` namespace
- **AND** the class implements `System.Text.Json.Serialization.Metadata.IJsonTypeInfoResolver`
- **AND** the class has a `public static PatchlyJsonTypeInfoResolver Default` singleton property

#### Scenario: Resolver returns JsonTypeInfo for known PatchDocument types
- **WHEN** `GetTypeInfo(typeof(CustomerPatch), options)` is called
- **AND** `CustomerPatch` is a `[PatchDocument]` class in the assembly
- **THEN** the resolver returns a non-null `JsonTypeInfo<CustomerPatch>`
- **AND** the returned `JsonTypeInfo` uses Patchly's generated converter for deserialization

#### Scenario: Resolver returns null for unknown types
- **WHEN** `GetTypeInfo(typeof(string), options)` is called
- **THEN** the resolver returns `null`
- **AND** the resolver chain falls through to the next resolver

#### Scenario: Resolver handles multiple PatchDocument types
- **WHEN** the assembly contains `CustomerPatch`, `OrderPatch`, and `ProductPatch` all decorated with `[PatchDocument]`
- **THEN** the resolver returns correct `JsonTypeInfo` for each type
- **AND** each `JsonTypeInfo` uses its respective Patchly-generated converter

#### Scenario: Resolver is gated behind NET8_0_OR_GREATER
- **WHEN** the consuming project targets .NET 6 or .NET 7
- **THEN** the `PatchlyJsonTypeInfoResolver` class is not compiled (excluded by `#if NET8_0_OR_GREATER`)
- **AND** no compilation errors occur

#### Scenario: Resolver is available on .NET 8+
- **WHEN** the consuming project targets .NET 8 or later
- **THEN** `PatchlyJsonTypeInfoResolver` is available and usable

### Requirement: AOT Deserialization with Resolver

When `PatchlyJsonTypeInfoResolver` is added to the `TypeInfoResolverChain`, deserialization of `[PatchDocument]` types SHALL work identically to the reflection-based path, including full property tracking.

#### Scenario: Null vs absent distinction works via resolver
- **WHEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** a `CustomerPatch` is deserialized from `{"firstName": "Alice", "age": null}`
- **THEN** `WasProvided("FirstName")` returns true
- **AND** `WasProvided("Age")` returns true (explicitly null)
- **AND** `WasProvided("LastName")` returns false (absent)

#### Scenario: Provided accessor works via resolver
- **WHEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** a `CustomerPatch` is deserialized from `{"firstName": "Alice"}`
- **THEN** `patch.Provided.FirstName` is true
- **AND** `patch.Provided.LastName` is false

#### Scenario: Serialization works via resolver
- **WHEN** `PatchlyJsonTypeInfoResolver.Default` is in the resolver chain
- **AND** a `CustomerPatch` is serialized
- **THEN** the output JSON contains property values without tracking infrastructure

### Requirement: Resolver Not Generated When No PatchDocument Types

The generator SHALL NOT emit the resolver file when there are no valid `[PatchDocument]` types.

#### Scenario: No resolver emitted for empty assembly
- **WHEN** the assembly contains no `[PatchDocument]` classes
- **THEN** no `PatchlyJsonTypeInfoResolver.g.cs` file is generated
