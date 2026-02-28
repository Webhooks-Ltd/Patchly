# Patch Document Attribute

## Purpose

Define the `[PatchDocument]` marker attribute and the `IPatchDocument` interface that form the public API contract of the Patchly core library.
## Requirements
### Requirement: PatchDocument Attribute Declaration

The `[PatchDocument]` attribute SHALL be a marker attribute that signals the source generator to emit tracking, conversion, and application logic for the decorated class. It SHALL target classes only and is not inheritable.

#### Scenario: Attribute applied to a partial class with nullable properties

- GIVEN a partial class `CustomerPatch` decorated with `[PatchDocument]`
- AND the class has properties `string? FirstName`, `string? LastName`, `int? Age`
- WHEN the project is compiled
- THEN the source generator emits a companion partial class file `CustomerPatch.g.cs`
- AND the generated code compiles without errors or warnings

#### Scenario: Attribute applied to a non-partial class

- GIVEN a class `BadPatch` decorated with `[PatchDocument]` that is NOT declared as `partial`
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH001` with severity Error
- AND the message indicates the class must be declared as `partial`
- AND no source is generated for this class

#### Scenario: Attribute applied to a struct

- GIVEN a struct `StructPatch` decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH002` with severity Error
- AND the message indicates that `[PatchDocument]` can only be applied to classes
- AND no source is generated for this type

#### Scenario: Attribute applied to a record class

- GIVEN a partial record class `RecordPatch` decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH003` with severity Error
- AND the message indicates that `[PatchDocument]` is not supported on record types
- AND no source is generated for this type

#### Scenario: Attribute applied to an abstract class

- GIVEN an abstract partial class `AbstractPatch` decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH004` with severity Error
- AND the message indicates that `[PatchDocument]` cannot be applied to abstract classes

#### Scenario: Attribute applied to a generic class

- GIVEN a partial class `GenericPatch<T>` decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH005` with severity Error
- AND the message indicates that `[PatchDocument]` does not support generic type parameters

#### Scenario: Attribute applied to a nested class

- GIVEN a partial class `OuterClass` containing a nested partial class `InnerPatch` decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the source generator emits a companion partial class for `InnerPatch`
- AND the generated code is correctly scoped within `OuterClass`
- AND the generated code compiles without errors

#### Scenario: Attribute applied to a class in a namespace

- GIVEN a partial class `OrderPatch` in namespace `MyApp.Contracts.Patches` decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the generated partial class is in the same namespace `MyApp.Contracts.Patches`

#### Scenario: Attribute applied to a class in the global namespace

- GIVEN a partial class `GlobalPatch` with no namespace declaration, decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the generated partial class has no namespace wrapper

#### Scenario: Multiple PatchDocument classes in the same project

- GIVEN partial classes `CustomerPatch`, `OrderPatch`, and `ProductPatch` all decorated with `[PatchDocument]`
- WHEN the project is compiled
- THEN the source generator emits separate generated files for each class
- AND each generated class is independent (no shared mutable state)

### Requirement: IPatchDocument Interface

The `IPatchDocument` interface SHALL provide a common contract for all generated patch document classes. It MUST enable writing generic code that operates on any patch document.

#### Scenario: Generated class implements IPatchDocument

- GIVEN a partial class `CustomerPatch` decorated with `[PatchDocument]`
- WHEN the source generator runs
- THEN the generated partial class declaration includes `: IPatchDocument`
- AND the class can be assigned to a variable of type `IPatchDocument`

#### Scenario: IPatchDocument exposes WasProvided method

- GIVEN the `IPatchDocument` interface definition
- THEN it declares `bool WasProvided(string propertyName)`
- AND the method accepts the C# property name (PascalCase)

#### Scenario: IPatchDocument exposes ProvidedProperties

- GIVEN the `IPatchDocument` interface definition
- THEN it declares `IReadOnlySet<string> ProvidedProperties { get; }`
- AND the returned set contains the C# property names of all properties present in the deserialized JSON

#### Scenario: Generic constraint using IPatchDocument

- GIVEN a method `void Process<T>(T patch) where T : IPatchDocument`
- AND a generated `CustomerPatch` class
- WHEN `Process(customerPatch)` is called
- THEN the code compiles successfully
- AND `patch.WasProvided("FirstName")` is callable within the method

### Requirement: Attribute Assembly and Namespace

The attribute and interface SHALL be correctly packaged for consumption in the `Patchly` namespace.

#### Scenario: Attribute is in the Patchly namespace

- GIVEN the `PatchDocumentAttribute` class
- THEN it is in the `Patchly` namespace
- AND it can be used as `[Patchly.PatchDocument]` or with a `using Patchly;` import as `[PatchDocument]`

#### Scenario: Attribute targets only classes

- GIVEN the `PatchDocumentAttribute` class definition
- THEN it is decorated with `[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]`

#### Scenario: IPatchDocument is in the Patchly namespace

- GIVEN the `IPatchDocument` interface
- THEN it is in the `Patchly` namespace

### Requirement: Parameterless Constructor Validation

The source generator SHALL verify that a `[PatchDocument]` class has either an accessible parameterless constructor OR a valid `[JsonConstructor]`-annotated constructor. If neither exists, the generator emits PATCH006.

#### Scenario: Class without a parameterless constructor and no JsonConstructor

- **WHEN** a `[PatchDocument]` partial class has only a parameterized constructor without `[JsonConstructor]`
- **THEN** the compiler emits diagnostic `PATCH006` with severity Error
- **AND** the message indicates that `[PatchDocument]` classes must have an accessible parameterless constructor or a `[JsonConstructor]` constructor
- **AND** no source is generated for this class

#### Scenario: Class with both parameterless and parameterized constructors

- **WHEN** a `[PatchDocument]` partial class has a parameterless constructor and additional parameterized constructors
- **THEN** no diagnostic is emitted
- **AND** source generation proceeds using the parameterless constructor (streaming path)

#### Scenario: Class with only a JsonConstructor constructor

- **WHEN** a `[PatchDocument]` partial class has only a `[JsonConstructor]`-annotated parameterized constructor and no parameterless constructor
- **THEN** no PATCH006 diagnostic is emitted
- **AND** source generation proceeds using the buffered path with constructor invocation

### Requirement: JsonExtensionData Validation

The source generator SHALL reject properties decorated with `[JsonExtensionData]` because the generated converter handles unknown properties by skipping them.

#### Scenario: JsonExtensionData property emits an error
- **WHEN** a `[PatchDocument]` partial class has a property `[JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; set; }`
- **THEN** the compiler emits diagnostic `PATCH014` with severity Error
- **AND** the message indicates that `[JsonExtensionData]` is not supported on `[PatchDocument]` classes

### Requirement: Required Keyword Handling

The source generator SHALL handle the C# `required` keyword on properties by applying `[SetsRequiredMembers]` to the generated converter's construction path.

#### Scenario: Required properties compile without error
- **WHEN** a `[PatchDocument]` partial class has `public required string? FirstName { get; set; }`
- **THEN** the generated code compiles without error
- **AND** the converter can construct the object and set properties normally
- **AND** `WasProvided("FirstName")` works correctly

### Requirement: Property Type Validation

The source generator SHALL validate that properties on a `[PatchDocument]` class are suitable for partial update tracking.

#### Scenario: Class with only nullable properties compiles cleanly

- GIVEN a `[PatchDocument]` partial class with properties `string? Name`, `int? Count`, `DateTime? Date`, `decimal? Price`
- WHEN the project is compiled
- THEN no diagnostics are emitted for these properties
- AND all properties are included in the generated tracking and Provided accessor

#### Scenario: Class with a non-nullable value type property emits a warning

- GIVEN a `[PatchDocument]` partial class with property `int Count` (non-nullable)
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH010` with severity Warning
- AND the message indicates that non-nullable value type properties cannot distinguish between "not provided" and "default value"
- AND the property is still included in tracking (WasProvided and Provided work) and generation proceeds

#### Scenario: Class with a non-nullable reference type property compiles without warning

- GIVEN a `[PatchDocument]` partial class with property `string Name` (non-nullable reference type in a nullable-enabled context)
- WHEN the project is compiled
- THEN no diagnostic is emitted for this property (reference types can be sent as null in JSON regardless of C# nullability annotation)
- AND the property is included in tracking and generation

#### Scenario: Class with no public properties emits a warning

- GIVEN a `[PatchDocument]` partial class with zero public properties
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH011` with severity Warning
- AND the message indicates the patch document has no properties to track

#### Scenario: Class with read-only properties

- GIVEN a `[PatchDocument]` partial class with a property `string? Name { get; }` (no setter)
- WHEN the project is compiled
- THEN the compiler emits diagnostic `PATCH012` with severity Warning
- AND the message indicates that read-only properties will be tracked but cannot be set during deserialization
- AND the property is excluded from the generated JsonConverter deserialization logic and from the Provided accessor

### Requirement: PatchDocument Semantics Mode Configuration

The `[PatchDocument]` attribute SHALL support configuring patch semantics mode per document type.

#### Scenario: Attribute defaults to legacy mode
- **WHEN** a patch document is declared as `[PatchDocument]` without explicit semantics mode
- **THEN** generation uses legacy semantics behavior by default

#### Scenario: Attribute enables deterministic mode
- **WHEN** a patch document is declared with deterministic semantics mode
- **THEN** deterministic state APIs and deterministic-mode diagnostics are enabled for that type

### Requirement: IPatchDocument State Lookup Contract

`IPatchDocument` SHALL expose an API to query tri-state value semantics by C# property name.

#### Scenario: Interface exposes state lookup method
- **GIVEN** the `IPatchDocument` public contract
- **WHEN** the interface is referenced from application code
- **THEN** it includes a `GetState(string propertyName)` member that returns a tri-state value

#### Scenario: State lookup is consistent with WasProvided
- **GIVEN** a deterministic patch document instance
- **WHEN** `GetState(propertyName)` returns `Omitted`
- **THEN** `WasProvided(propertyName)` returns false
- **AND WHEN** `GetState(propertyName)` returns `Null` or `Value`
- **THEN** `WasProvided(propertyName)` returns true

### Requirement: Deterministic Mode Collection Guardrail

The generator SHALL warn when deterministic mode is enabled on patch documents that define non-nullable collection properties.

#### Scenario: Non-nullable collection property emits warning in deterministic mode
- **GIVEN** a patch document in deterministic mode with property `List<string> Tags`
- **WHEN** the project is compiled
- **THEN** the generator emits a warning diagnostic indicating nullable collection types are recommended for clear-vs-replace semantics

#### Scenario: Same shape does not emit deterministic guardrail in legacy mode
- **GIVEN** a patch document in legacy mode with property `List<string> Tags`
- **WHEN** the project is compiled
- **THEN** deterministic guardrail warning is not emitted

