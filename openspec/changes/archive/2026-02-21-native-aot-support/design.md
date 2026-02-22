## Context

Patchly's source generator emits a `[JsonConverter(typeof(...))]` attribute on each `[PatchDocument]` partial class and a nested `private sealed` converter class. This works with reflection-based System.Text.Json deserialization. However, in Native AOT apps, reflection is disabled. Users must provide a `JsonSerializerContext` with `[JsonSerializable]` types, and the STJ source generator generates metadata for those types. Because Roslyn source generators cannot see each other's output, the STJ generator doesn't know about Patchly's converter and generates its own metadata — which breaks.

The existing non-AOT behaviour must remain unchanged.

## Goals / Non-Goals

**Goals:**
- `[PatchDocument]` types work in Native AOT apps with `PublishAot=true`
- Zero-ceremony for non-AOT users (existing behaviour unchanged)
- Minimal ceremony for AOT users (one line of configuration)
- Full property tracking (`WasProvided`, `Provided`) works identically in AOT

**Non-Goals:**
- Auto-detecting AOT and registering the resolver automatically (requires user startup code)
- Emitting a `JsonSerializerContext` subclass (would conflict with user's context)
- Supporting .NET 6/7 AOT scenarios (AOT is .NET 8+ in practice)

## Decisions

### Generate an `IJsonTypeInfoResolver` per assembly

Patchly's generator collects all `[PatchDocument]` types in the assembly and emits a single `PatchlyJsonTypeInfoResolver` class implementing `IJsonTypeInfoResolver`. For each known type, it returns a `JsonTypeInfo<T>` wrapping Patchly's converter via `JsonMetadataServices.CreateValueInfo<T>(options, converter)`.

Rationale: This integrates cleanly with ASP.NET Core's `TypeInfoResolverChain` pattern. Users add the resolver before their `JsonSerializerContext`, and Patchly handles `[PatchDocument]` types while their context handles everything else.

Alternative considered: Generating a `JsonSerializerContext` subclass. Rejected because users already have their own context, and two contexts for the same types would conflict.

Alternative considered: Making users manually register converters via `options.Converters.Add(...)`. Rejected because it requires per-type registration and leaks implementation details.

### Gate behind `#if NET8_0_OR_GREATER`

The `IJsonTypeInfoResolver` interface and `JsonMetadataServices.CreateValueInfo<T>` are .NET 8+ APIs. The resolver code is wrapped in `#if NET8_0_OR_GREATER` so it compiles away for .NET 6/7 targets.

Rationale: Native AOT is a .NET 8+ feature in practice. This keeps the generated code compatible with all supported target frameworks.

### Change converter visibility to `internal`

The nested converter class changes from `private sealed` to `internal sealed` so the assembly-wide resolver can instantiate it.

Rationale: The resolver lives in a different generated file but the same assembly, so `internal` provides just enough access. The converter was never intended to be part of the public API.

Alternative considered: `public`. Rejected because it unnecessarily exposes implementation details.

### Resolver lives in the `Patchly` namespace

The generated resolver is placed in the `Patchly` namespace regardless of where the `[PatchDocument]` types are declared. This provides a predictable, discoverable location.

### Use `Collect()` pipeline pattern

The generator adds a second `RegisterSourceOutput` using `.Collect()` on the existing pipeline. This gathers all valid models and emits the single resolver file. The per-class generation remains unchanged.

Rationale: Standard incremental generator pattern for assembly-wide output. The existing per-class pipeline is not modified — only an additional consumer is added.

## Risks / Trade-offs

- **[Risk] User forgets to add resolver** → Deserialization fails at runtime with "metadata not provided" error. Mitigated by clear README documentation and potentially a future PATCH016 diagnostic.
- **[Trade-off] One line of setup for AOT** → Non-AOT stays zero-config. AOT requires `TypeInfoResolverChain.Insert(0, PatchlyJsonTypeInfoResolver.Default)`. This is the standard pattern for all AOT-compatible libraries.
- **[Trade-off] Converter visibility loosened** → Moving from `private` to `internal` slightly reduces encapsulation, but the converter was already accessible via the `[JsonConverter]` attribute. No practical impact.
