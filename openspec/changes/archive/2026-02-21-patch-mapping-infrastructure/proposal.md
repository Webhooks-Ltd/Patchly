## Why

Every Patchly user writes the same `if (patch.Provided.X) target.X = patch.X;` boilerplate. While this works, there's no blessed pattern for where to put it. Teams end up with ad-hoc extension methods scattered across the codebase with no common interface, no DI integration, and no discoverability. Patchly should provide a structured, AOT-safe way to define and use patch-to-entity mappings.

## What Changes

### Core package additions (`Patchly`)

**`IPatchApplier`** — a single injectable service that applies any registered patch to its target:

```csharp
public interface IPatchApplier
{
    void Apply<TPatch, TTarget>(TPatch patch, TTarget target)
        where TPatch : IPatchDocument;
}
```

**`PatchMap<TPatch, TTarget>`** — abstract class. Users override `Apply` with their mapping logic — the same code they'd write in an extension method, but in a structured, discoverable class:

```csharp
public abstract class PatchMap<TPatch, TTarget> where TPatch : IPatchDocument
{
    public abstract void Apply(TPatch patch, TTarget target);
}
```

**User-defined map:**

```csharp
public class CustomerPatchMap : PatchMap<CustomerPatch, Customer>
{
    public override void Apply(CustomerPatch patch, Customer target)
    {
        if (patch.Provided.FirstName) target.GivenName = patch.FirstName;
        if (patch.Provided.Age)       target.Age = patch.Age ?? 0;
    }
}
```

### Source generator additions

The source generator discovers all `PatchMap<,>` subclasses in the assembly and generates:

1. **`PatchApplier`** — implementation of `IPatchApplier` that resolves the correct map via DI and delegates to it
2. **`AddPatchlyMaps()`** — `IServiceCollection` extension that registers all discovered maps and the `IPatchApplier`

### Usage

```csharp
// Startup
services.AddPatchlyMaps();

// Any endpoint — one dependency for all patch types
public class CustomerController
{
    private readonly IPatchApplier _patchApplier;

    [HttpPatch("{id}")]
    public IActionResult Patch(int id, CustomerPatch patch)
    {
        var customer = _repo.Get(id);
        _patchApplier.Apply(patch, customer);
        _repo.Save(customer);
        return Ok(customer);
    }
}
```

## Design Decisions

- **No name matching / convention-based mapping.** Maps are hand-written code. Rename a property and the build breaks. This is intentional — convention-based mapping is fragile and breaks silently.
- **`IPatchApplier` in core, not a separate package.** It's a simple interface with no dependencies. Both the core mapping infrastructure and `Patchly.AutoMapper` implement it, so it must be in the shared package.
- **`PatchMap<,>` in core, not a separate package.** It's a single abstract class with no dependencies. No reason for a separate NuGet package.
- **Maps can have DI dependencies.** Since maps are resolved from the container, they can take constructor dependencies (loggers, other services, etc.).
- **`IPatchApplier` is the public API, not individual maps.** Users inject one service for all patch types, not N services for N patch types.

## Capabilities

### New Capabilities

- `patch-mapping`: Structured patch-to-entity mapping via `PatchMap<,>`, `IPatchApplier`, and generated DI registration

## Impact

- `src/Patchly/` — add `IPatchApplier`, `PatchMap<TPatch, TTarget>`
- `src/Patchly.Generators/PatchDocumentGenerator.cs` — discover `PatchMap<,>` subclasses, generate `PatchApplier` and `AddPatchlyMaps()`
- README.md — document mapping pattern
- No breaking changes to existing API
