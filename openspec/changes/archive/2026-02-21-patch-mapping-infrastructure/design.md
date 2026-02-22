## Context

Patchly currently generates `IPatchDocument` implementations with per-property `WasProvided` tracking and JSON converters. Users apply patches manually:

```csharp
if (patch.Provided.FirstName) target.GivenName = patch.FirstName;
if (patch.Provided.Age)       target.Age = patch.Age ?? 0;
```

This works but every team ends up writing their own mapping pattern. There's no standard structure, no DI integration, and no way to centralize the "apply patch to entity" concern.

The core package (`Patchly`) has zero NuGet dependencies today. The source generator (`Patchly.Generators`) targets `netstandard2.0` and uses an `IIncrementalGenerator` pipeline that already collects all `[PatchDocument]` classes assembly-wide (for the AOT resolver).

## Goals / Non-Goals

**Goals:**

- Provide `PatchMap<TPatch, TTarget>` as a structured base class for hand-written patch-to-entity mappings
- Provide `IPatchApplier` as the single injectable service consumers use to apply any patch
- Source-generate `PatchApplier` implementation and `AddPatchlyMaps()` DI registration
- Keep zero runtime NuGet dependencies on the core package (DI abstractions are in-box from .NET 6)
- Remain fully AOT-safe — no reflection at runtime

**Non-Goals:**

- Convention-based or auto-mapping (property name matching, `AutoMapper` integration) — that's the separate `patchly-automapper` change
- Supporting maps defined in a different assembly than the patch documents
- Async mapping support (maps are simple property assignments)
- Validation inside maps (validation is a separate concern)

## Decisions

### 1. `IPatchApplier` and `PatchMap<,>` live in the core `Patchly` package

**Decision:** Add both types to `src/Patchly/`.

**Rationale:** They're simple types with no dependencies. `IPatchApplier` is one method. `PatchMap<,>` is one abstract method. Putting them in a separate package would force users to install two packages for the basic mapping scenario. The `Patchly.AutoMapper` package will also implement `IPatchApplier`, so the interface must live in the shared core.

**Alternatives considered:**
- Separate `Patchly.Mapping` package — rejected because it adds package management overhead for zero benefit. These types have no external dependencies.

### 2. `Microsoft.Extensions.DependencyInjection.Abstractions` as a dependency

**Decision:** Add a package reference to `Microsoft.Extensions.DependencyInjection.Abstractions` in `Patchly.csproj` for `IServiceCollection`.

**Rationale:** The generated `AddPatchlyMaps()` extension method needs `IServiceCollection`. This package is already in-box with every ASP.NET Core app (it ships with the shared framework). The NuGet reference only matters for consumers on bare `net6.0` console apps, where it's a tiny, well-known dependency.

**Alternatives considered:**
- Generate the registration code without the dependency (require users to register manually) — rejected. The whole point is zero-ceremony DI setup. Manual registration defeats the purpose.
- Put the DI extension in a separate package — rejected. Same reasoning as Decision 1; it fragments the package for no benefit.

### 3. Generator discovers `PatchMap<,>` subclasses via Roslyn

**Decision:** Extend `PatchDocumentGenerator` (or add a new generator class) to scan for classes that inherit from `PatchMap<TPatch, TTarget>` and extract the type arguments.

**Rationale:** The generator already collects all `[PatchDocument]` types. Collecting `PatchMap<,>` subclasses is the same pattern — `ForAttributeWithMetadataName` won't work here since there's no attribute, so we'll use a syntax-based pipeline filtering for class declarations with a base type matching `PatchMap<,>`.

**Implementation approach:**
- Add a second `IIncrementalGenerator` class (e.g., `PatchMapGenerator`) in `Patchly.Generators` to keep concerns separate from the existing `PatchDocumentGenerator`
- Use `SyntaxProvider.CreateSyntaxProvider` with a broad syntax predicate (match any class with a base list containing `PatchMap`), then use the semantic model to verify the base type is `Patchly.PatchMap<,>` via `OriginalDefinition` comparison
- Filter to **concrete, non-generic** classes only — skip abstract intermediate classes (e.g., `abstract class AuditedPatchMap<TPatch, TTarget> : PatchMap<TPatch, TTarget>`)
- Extract `TPatch` and `TTarget` type arguments from the base class
- Emit a **compiler diagnostic** when duplicate maps for the same `(TPatch, TTarget)` pair are detected
- Collect all discovered maps and generate `PatchApplier` and `AddPatchlyMaps()`
- Use value-equatable model types (records / `EquatableArray<T>`) following the same pattern as `PatchDocumentGenerator` to ensure correct incremental caching

**Alternatives considered:**
- Require a `[PatchMap]` attribute — rejected. The base class already uniquely identifies maps. An attribute is redundant ceremony.
- Merge into `PatchDocumentGenerator` — rejected. Different concern, different pipeline. Keeping them separate is cleaner and doesn't couple the two features.

### 4. Generated `PatchApplier` resolves maps from DI

**Decision:** The generated `PatchApplier` takes `IServiceProvider` and resolves `PatchMap<TPatch, TTarget>` instances on demand.

**Rationale:** Maps can have constructor dependencies (loggers, services). Resolving from the container lets maps participate in DI naturally. The generator knows all map types at compile time, so it can generate a type-switch that avoids reflection.

**Generated pattern:**
```csharp
// Generated in the Patchly namespace, internal to the consuming assembly
namespace Patchly
{
    internal sealed class PatchApplier : IPatchApplier
    {
        private readonly IServiceProvider _sp;

        public PatchApplier(IServiceProvider sp) => _sp = sp;

        public void Apply<TPatch, TTarget>(TPatch patch, TTarget target)
            where TPatch : IPatchDocument
        {
            if (typeof(TPatch) == typeof(CustomerPatch) && typeof(TTarget) == typeof(Customer))
            {
                var map = (PatchMap<CustomerPatch, Customer>)_sp.GetRequiredService(typeof(PatchMap<CustomerPatch, Customer>));
                map.Apply((CustomerPatch)(object)patch, (Customer)(object)target);
                return;
            }

            throw new InvalidOperationException($"No PatchMap registered for {typeof(TPatch).FullName} -> {typeof(TTarget).FullName}");
        }
    }
}

// Extension method in the conventional namespace for IServiceCollection extensions
namespace Microsoft.Extensions.DependencyInjection
{
    internal static class PatchlyServiceCollectionExtensions
    {
        public static IServiceCollection AddPatchlyMaps(this IServiceCollection services)
        {
            services.AddScoped<Patchly.IPatchApplier, Patchly.PatchApplier>();
            services.AddTransient<Patchly.PatchMap<CustomerPatch, Customer>, CustomerPatchMap>();
            return services;
        }
    }
}
```

`PatchApplier` is thread-safe — it holds no mutable state. Each `Apply` call resolves a fresh map instance from the container.

The type-switch is O(n) in the number of registered maps. This is negligible for typical use (even dozens of maps). For assemblies with 50+ maps, dictionary-based dispatch could be added later without changing the public API.

**Alternatives considered:**
- Dictionary lookup by `(Type, Type)` tuple — works but the type-switch is faster (JIT can optimize it) and avoids boxing/dictionary overhead for the common case.
- Constructor-inject all maps — rejected. For assemblies with many maps, this would eagerly resolve all of them. `IServiceProvider` resolves only the one needed per call.

### 5. `AddPatchlyMaps()` registers maps as transient, `IPatchApplier` as scoped

**Decision:** Register each `PatchMap<,>` subclass as transient. Register `IPatchApplier` as **scoped**.

**Rationale:** Transient is the safest default for maps — they have no state. `IPatchApplier` must be scoped (not singleton) because it captures `IServiceProvider`. A singleton would capture the **root** `IServiceProvider`, meaning any map with scoped dependencies (e.g., `DbContext`) would resolve from the root scope — the classic captive dependency bug. With `ValidateScopes` enabled (default in Development), this throws; in Production, it silently misbehaves. Scoped registration ensures `PatchApplier` receives the request-scoped `IServiceProvider`, so all map resolutions happen in the correct scope. The cost is one extra allocation per request — negligible.

**Alternatives considered:**
- Singleton `IPatchApplier` — rejected due to captive dependency risk when maps take scoped dependencies.
- Inject `IServiceScopeFactory` and create a scope per `Apply` call — rejected, heavier and unusual pattern.

### 6. Maps constrain `TPatch : IPatchDocument`

**Decision:** `PatchMap<TPatch, TTarget>` constrains `TPatch : IPatchDocument`. No constraint on `TTarget`.

**Rationale:** The constraint ensures maps are only written for types that have `WasProvided` tracking. `TTarget` is unconstrained because entities/models don't implement any Patchly interface.

## Risks / Trade-offs

**One map per `(TPatch, TTarget)` pair** — The generated type-switch assumes a 1:1 mapping. If someone needs two different maps for the same pair, they can't. → Mitigation: This is intentional and enforced — the generator emits a compiler diagnostic if duplicates are detected. If you need conditional logic, put it in the single map.

**`IServiceProvider.GetRequiredService` in the hot path** — Each `Apply` call resolves a service. → Mitigation: DI containers (Microsoft's default) cache transient factory delegates. The actual overhead is a dictionary lookup + object allocation, which is negligible compared to the database call that typically follows.

**Breaking the zero-dependency rule** — Adding `Microsoft.Extensions.DependencyInjection.Abstractions` is a new dependency for the core package. → Mitigation: This package is universally present in ASP.NET Core apps (the primary audience). For non-DI scenarios, users can still use `PatchMap<,>` directly without calling `AddPatchlyMaps()`. The dependency is already listed as allowed in `project.md`. The packaging policy (`docs/packaging-policy.md`) should be updated to reflect this decision.

**Generator ordering** — `PatchMapGenerator` doesn't depend on `PatchDocumentGenerator` output. Maps reference patch types that exist in user code (they'd get the same types whether or not the generator ran). No ordering concern.

**Forgotten `AddPatchlyMaps()` call** — If users define `PatchMap<,>` subclasses but forget to call `AddPatchlyMaps()`, the maps are dead code with no indication. → Mitigation: The generated `AddPatchlyMaps()` method includes XML doc listing all discovered maps for discoverability. A missing call surfaces as a standard DI resolution failure when `IPatchApplier` is injected.
