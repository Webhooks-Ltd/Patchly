## 1. Core Library Types

- [x] 1.1 Add `PatchMap<TPatch, TTarget>` abstract class to `src/Patchly/PatchMap.cs` — generic with `where TPatch : IPatchDocument`, single abstract method `void Apply(TPatch patch, TTarget target)`
- [x] 1.2 Add `IPatchApplier` interface to `src/Patchly/IPatchApplier.cs` — single method `void Apply<TPatch, TTarget>(TPatch patch, TTarget target) where TPatch : IPatchDocument`
- [x] 1.3 Add `Microsoft.Extensions.DependencyInjection.Abstractions` package reference to `Directory.Packages.props` and `Patchly.csproj`
- [x] 1.4 Verify solution compiles and existing tests pass after adding the DI abstractions dependency

## 2. Source Generator — Discovery Pipeline

- [x] 2.1 Add `PatchMapModel` sealed record (map class name, fully-qualified name, namespace, TPatch fully-qualified name, TTarget fully-qualified name) to `Patchly.Generators` — must implement `IEquatable<PatchMapModel>` via record semantics to work with `EquatableArray<T>`, matching the existing `PatchClassModel` pattern
- [x] 2.2 Add `PATCH020` duplicate map diagnostic to `Diagnostics.cs` — format message with two args: Arg0 = comma-separated conflicting class names, Arg1 = `(TPatch, TTarget)` pair string (fits existing `DiagnosticInfo` two-arg limit)
- [x] 2.3 Create `PatchMapGenerator.cs` implementing `IIncrementalGenerator` with `CreateSyntaxProvider` — syntax predicate matches class declarations with a base list containing `PatchMap`, semantic transform verifies `Patchly.PatchMap<,>` via `OriginalDefinition` (tasks 2.3–2.5 form a single implementation unit)
- [x] 2.4 Filter discovered types to concrete, non-abstract, non-generic classes only (skip abstract intermediates and open generics)
- [x] 2.5 Extract `TPatch` and `TTarget` fully-qualified type arguments from the resolved base type
- [x] 2.6 Collect all maps, detect duplicate `(TPatch, TTarget)` pairs and report `PATCH020` diagnostic

## 3. Source Generator — Code Emission

- [x] 3.1 Generate `internal sealed class PatchApplier : IPatchApplier` in the `Patchly` namespace with `IServiceProvider` constructor, type-switch dispatch in `Apply<TPatch, TTarget>`, and `InvalidOperationException` for unregistered pairs (message includes full type names)
- [x] 3.2 Generate `internal static class PatchlyServiceCollectionExtensions` in `Microsoft.Extensions.DependencyInjection` namespace with `public static IServiceCollection AddPatchlyMaps(this IServiceCollection services)` — registers `IPatchApplier` as scoped, each map as transient with service type `PatchMap<TPatch, TTarget>`
- [x] 3.3 Generate XML doc on `AddPatchlyMaps()` listing all discovered map classes for discoverability
- [x] 3.4 Use fully-qualified type names throughout generated code (handles nested classes, cross-namespace maps)
- [x] 3.5 Add `[GeneratedCode]` attributes and `#nullable enable` to generated files
- [x] 3.6 Emit nothing when zero maps are discovered

## 4. Tests

- [x] 4.1 Unit test: `PatchMap<,>` subclass applies patch to target entity correctly
- [x] 4.2 Unit test: `PatchMap<string, T>` fails to compile (constraint violation)
- [x] 4.3 Generator test: single map produces correct `PatchApplier` and `AddPatchlyMaps()` output
- [x] 4.4 Generator test: multiple maps in different namespaces all registered
- [x] 4.5 Generator test: abstract intermediate class skipped, concrete subclass registered
- [x] 4.6 Generator test: open generic subclass skipped
- [x] 4.7 Generator test: nested class map uses fully-qualified name
- [x] 4.8 Generator test: internal map class discovered and registered
- [x] 4.9 Generator test: duplicate `(TPatch, TTarget)` pair emits `PATCH020` error diagnostic
- [x] 4.10 Generator test: zero maps produces no generated output
- [x] 4.11 Integration test: `AddPatchlyMaps()` → resolve `IPatchApplier` → apply patch → verify target mutation
- [x] 4.12 Integration test: map with scoped dependency resolves correctly within a scope
- [x] 4.13 Integration test: unregistered pair throws `InvalidOperationException` with type names in message
- [x] 4.14 Generator test: map class, patch type, and target type in three different namespaces — generated code uses fully-qualified names and compiles
- [x] 4.15 Integration test: `AddPatchlyMaps()` called twice on the same `IServiceCollection` does not throw

## 5. Verification and Documentation

- [x] 5.1 Add `PatchMap<,>` usage and `AddPatchlyMaps()` to the AOT smoketest project, verify `dotnet publish` with AOT produces no trim/AOT warnings
- [x] 5.2 Update `docs/packaging-policy.md` to document the `Microsoft.Extensions.DependencyInjection.Abstractions` dependency and rationale
- [x] 5.3 Update `README.md` with patch mapping pattern: `PatchMap<,>` definition, `AddPatchlyMaps()` registration, `IPatchApplier` usage in controllers
