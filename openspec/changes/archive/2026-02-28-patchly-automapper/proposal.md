## Why

Many ASP.NET Core teams already use AutoMapper for object-to-object mapping. When they adopt Patchly, there's an integration gap: AutoMapper doesn't know about `Provided`/`WasProvided`, so it maps all properties regardless of whether they were in the JSON payload. Users either abandon AutoMapper for patch endpoints or manually wire conditions on every member mapping.

## What Changes

A new `Patchly.AutoMapper` NuGet package that bridges Patchly's provided-property tracking into AutoMapper's mapping pipeline.

**`AutoMapperPatchApplier : IPatchApplier`** — implements the same `IPatchApplier` interface from the core package, but delegates to AutoMapper:

```csharp
// Under the hood:
public void Apply<TPatch, TTarget>(TPatch patch, TTarget target)
    where TPatch : IPatchDocument
{
    _mapper.Map(patch, target, opts =>
    {
        opts.Items["__patchly_provided"] = patch.ProvidedProperties;
    });
}
```

**`AddPatchlySupport()`** — extension on `IMapperConfigurationExpression` that registers a global convention. For every map where the source is `IPatchDocument`, it adds a per-member precondition that checks whether the source property was provided:

```csharp
cfg.ForAllMaps((typeMap, expr) =>
{
    if (!typeof(IPatchDocument).IsAssignableFrom(typeMap.SourceType))
        return;

    foreach (var propertyMap in typeMap.PropertyMaps)
    {
        var sourceMemberName = propertyMap.SourceMember?.Name;
        if (sourceMemberName == null) continue;

        expr.ForMember(propertyMap.DestinationMember.Name, opts =>
            opts.PreCondition((src, ctx) =>
            {
                if (!ctx.Items.TryGetValue("__patchly_provided", out var p))
                    return true;
                return ((IReadOnlySet<string>)p).Contains(sourceMemberName);
            }));
    }
});
```

Uses the **source** member name from `TypeMap.PropertyMaps` (not destination member name) so custom `MapFrom` configurations work correctly.

**`AddPatchlyAutoMapper()`** — `IServiceCollection` extension that registers `AutoMapperPatchApplier` as `IPatchApplier`.

### Usage

```csharp
// Startup
services.AddAutoMapper(cfg =>
{
    cfg.AddPatchlySupport();
    cfg.AddProfile<MyProfiles>();
});
services.AddPatchlyAutoMapper();

// Profile — completely standard, no Patchly awareness needed
public class CustomerProfile : Profile
{
    public CustomerProfile()
    {
        CreateMap<CustomerPatch, Customer>();
    }
}

// Endpoint — identical to core mapping usage
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

Swap between explicit maps and AutoMapper by changing one line at startup. Endpoint code is unchanged.

## Design Decisions

- **Implements `IPatchApplier` from core.** Same interface, swappable at startup. Endpoint code doesn't know or care which implementation is behind it.
- **Uses `TypeMap.PropertyMaps` for source member resolution.** The `ForAllMembers` condition callback only gives you the destination member name, which breaks when `MapFrom` remaps. Iterating `PropertyMaps` gives the correct source member name.
- **Passes provided set via `context.Items`.** Standard AutoMapper pattern for sideband data. Regular `_mapper.Map()` calls still work normally (no context item set → condition passes → all properties mapped).
- **Nested patch documents are handle-explicitly in v1.** Automatic propagation of provided sets through nested AutoMapper mappings is complex and fragile. Users handle nested patches with separate `Apply` calls.
- **Non-AOT only.** AutoMapper uses reflection. This package is for teams that aren't targeting AOT. AOT users should use the core `PatchMap<,>` approach instead.

## Capabilities

### New Capabilities

- `patchly-automapper`: AutoMapper integration that only maps provided properties from `[PatchDocument]` types

## Impact

- New `Patchly.AutoMapper` NuGet package
- Dependencies: `Patchly`, `AutoMapper`
- README.md — document AutoMapper integration
- No changes to existing packages
- No breaking changes
