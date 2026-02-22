## ADDED Requirements

### Requirement: PatchMap base class

The library SHALL provide an abstract generic class `PatchMap<TPatch, TTarget>` in the `Patchly` namespace where `TPatch` is constrained to `IPatchDocument` and `TTarget` is unconstrained. The class SHALL declare a single abstract method `void Apply(TPatch patch, TTarget target)` that subclasses override with their mapping logic.

#### Scenario: Apply a patch map to a target entity

- **WHEN** a developer creates `CustomerPatchMap : PatchMap<CustomerPatch, Customer>`, overrides `Apply` with `if (patch.Provided.FirstName) target.GivenName = patch.FirstName;`, and calls `Apply` with a patch where `FirstName` was provided
- **THEN** `target.GivenName` is updated to the patch value

#### Scenario: Map with DI dependencies

- **WHEN** a developer creates a `PatchMap<,>` subclass with constructor parameters (e.g., `ILogger`), registers it via `AddPatchlyMaps()`, and resolves `IPatchApplier`
- **THEN** the map's constructor dependencies are resolved by the DI container and the `Apply` method executes with those dependencies available

#### Scenario: TPatch constraint enforced

- **WHEN** a developer attempts to create `PatchMap<string, Customer>` where `string` does not implement `IPatchDocument`
- **THEN** compilation fails with a generic constraint violation error

### Requirement: IPatchApplier interface

The library SHALL provide an interface `IPatchApplier` in the `Patchly` namespace with a single method `void Apply<TPatch, TTarget>(TPatch patch, TTarget target) where TPatch : IPatchDocument`. Consumers inject `IPatchApplier` as the single service for applying any registered patch type to its target.

#### Scenario: Apply a patch via IPatchApplier

- **WHEN** a developer injects `IPatchApplier` and calls `Apply(customerPatch, customer)` where a `PatchMap<CustomerPatch, Customer>` is registered
- **THEN** the corresponding map's `Apply` method is invoked with the patch and target, and the target is mutated accordingly

#### Scenario: Apply with unregistered map

- **WHEN** a developer calls `IPatchApplier.Apply<TPatch, TTarget>` for a `(TPatch, TTarget)` pair that has no registered `PatchMap`
- **THEN** an `InvalidOperationException` is thrown whose message contains the full type names of both `TPatch` and `TTarget`

### Requirement: Source-generated PatchApplier implementation

The source generator SHALL discover all concrete, non-abstract, non-generic classes that inherit from `PatchMap<TPatch, TTarget>` in the compilation and generate an `internal sealed class PatchApplier` in the `Patchly` namespace that implements `IPatchApplier`. The generated implementation SHALL use a compile-time type-switch to dispatch to the correct map — no runtime reflection.

#### Scenario: Generator discovers a single map

- **WHEN** the compilation contains one class `OrderPatchMap : PatchMap<OrderPatch, Order>`
- **THEN** the generator emits an `internal sealed class PatchApplier` in the `Patchly` namespace that dispatches `Apply<OrderPatch, Order>` to a DI-resolved `OrderPatchMap`

#### Scenario: Generator discovers multiple maps

- **WHEN** the compilation contains `CustomerPatchMap : PatchMap<CustomerPatch, Customer>` and `OrderPatchMap : PatchMap<OrderPatch, Order>`
- **THEN** the generated `PatchApplier` dispatches each pair to its respective map

#### Scenario: Generator skips abstract intermediate classes

- **WHEN** the compilation contains `abstract class AuditedPatchMap<TPatch, TTarget> : PatchMap<TPatch, TTarget>` and `class CustomerPatchMap : AuditedPatchMap<CustomerPatch, Customer>`
- **THEN** the generator registers only `CustomerPatchMap`, not `AuditedPatchMap`

#### Scenario: Generator skips generic subclasses

- **WHEN** the compilation contains `class GenericMap<T> : PatchMap<SomePatch, T>`
- **THEN** the generator does not register `GenericMap<T>` (it is open generic and cannot be instantiated)

#### Scenario: No maps in compilation

- **WHEN** the compilation contains no `PatchMap<,>` subclasses
- **THEN** the generator does not emit `PatchApplier` or `AddPatchlyMaps()`, and any call to `AddPatchlyMaps()` in user code results in a compile error (method does not exist)

#### Scenario: Generator discovers map in a nested class

- **WHEN** the compilation contains `class Outer { class CustomerPatchMap : PatchMap<CustomerPatch, Customer> { } }`
- **THEN** the generator registers it using the fully-qualified type name and the map is resolvable via `IPatchApplier`

#### Scenario: Map, patch, and target in different namespaces

- **WHEN** `MyApp.Patches.CustomerPatch`, `MyApp.Domain.Customer`, and `MyApp.Maps.CustomerPatchMap` are in separate namespaces
- **THEN** the generated `PatchApplier` correctly references all types using fully-qualified names and compiles without errors

#### Scenario: Internal map class is discovered

- **WHEN** the compilation contains `internal class CustomerPatchMap : PatchMap<CustomerPatch, Customer>`
- **THEN** the generator discovers and registers it identically to a public map

### Requirement: Duplicate map diagnostic

The source generator SHALL emit a compiler diagnostic error (`PATCH020`, category "Patchly") when two or more concrete classes map the same `(TPatch, TTarget)` pair. The diagnostic message SHALL identify the conflicting `(TPatch, TTarget)` pair and the names of both classes. Only one map per pair is allowed.

#### Scenario: Two maps for the same pair

- **WHEN** the compilation contains both `CustomerPatchMapA : PatchMap<CustomerPatch, Customer>` and `CustomerPatchMapB : PatchMap<CustomerPatch, Customer>`
- **THEN** the generator emits diagnostic `PATCH020` as an error, and the message contains both `CustomerPatchMapA` and `CustomerPatchMapB` and the pair `(CustomerPatch, Customer)`

### Requirement: Generated DI registration

The source generator SHALL emit an `internal static class PatchlyServiceCollectionExtensions` in the `Microsoft.Extensions.DependencyInjection` namespace containing a `public static IServiceCollection AddPatchlyMaps(this IServiceCollection services)` extension method. This method SHALL register `IPatchApplier` as **scoped** (with implementation type `Patchly.PatchApplier`) and each discovered `PatchMap<,>` subclass as **transient** with service type `PatchMap<TPatch, TTarget>` and implementation type equal to the concrete subclass.

#### Scenario: Register and resolve

- **WHEN** a developer calls `services.AddPatchlyMaps()` and resolves `IPatchApplier` from the service provider
- **THEN** the resolved `IPatchApplier` is the generated `PatchApplier` and it can dispatch to all registered maps

#### Scenario: Maps with scoped dependencies

- **WHEN** a map takes a scoped dependency (e.g., `DbContext`) and `IPatchApplier.Apply` is called within a request scope
- **THEN** the map receives the scoped instance from the current request scope, not the root container

#### Scenario: AddPatchlyMaps called multiple times

- **WHEN** `AddPatchlyMaps()` is called multiple times on the same `IServiceCollection`
- **THEN** no exception is thrown; registrations are added each time and the last registration wins for `IPatchApplier`

### Requirement: DI abstractions dependency

The `Patchly` core package SHALL add a package reference to `Microsoft.Extensions.DependencyInjection.Abstractions` to support the generated `AddPatchlyMaps()` extension method. The packaging policy (`docs/packaging-policy.md`) SHALL be updated to reflect this allowed dependency and its rationale.

#### Scenario: Package installs without additional dependencies in ASP.NET Core

- **WHEN** a developer adds the `Patchly` NuGet package to an ASP.NET Core project
- **THEN** `Microsoft.Extensions.DependencyInjection.Abstractions` is already satisfied by the shared framework and no additional DLLs are deployed

### Requirement: AOT and trimming safety

All generated code SHALL be AOT-compatible and trimming-safe. The generated `PatchApplier` SHALL use only direct type comparisons and casts — no `Type.GetType()`, no `Activator.CreateInstance()`, no reflection. Generated files SHALL include `[GeneratedCode]` attributes.

#### Scenario: Publish with Native AOT

- **WHEN** a project using `PatchMap<,>` and `IPatchApplier` is published with `<PublishAot>true</PublishAot>`
- **THEN** the build succeeds with no trim or AOT warnings related to Patchly
