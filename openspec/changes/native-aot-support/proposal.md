## Why

Patchly doesn't work with Native AOT apps (`PublishAot=true`). The generated `[JsonConverter]` attribute on partial classes is invisible to the System.Text.Json source generator because Roslyn source generators don't see each other's output. AOT apps that require `JsonSerializerContext` either get parameterized constructor errors or "metadata not provided" errors. AOT is increasingly the default for new .NET projects (e.g., `CreateSlimBuilder`), so this is a blocker for adoption.

## What Changes

- Generate a `PatchlyJsonTypeInfoResolver` class (implementing `IJsonTypeInfoResolver`) that AOT users add to their `TypeInfoResolverChain`
- The resolver creates `JsonTypeInfo<T>` for each `[PatchDocument]` type using Patchly's generated converters
- Change generated converter visibility from `private` to `internal` so the resolver can instantiate it
- Gate the resolver behind `#if NET8_0_OR_GREATER` since `IJsonTypeInfoResolver` is a .NET 8+ API
- Existing non-AOT behaviour is completely unchanged — `[JsonConverter]` attribute stays

## Capabilities

### New Capabilities

- `native-aot-support`: Generated `IJsonTypeInfoResolver` for AOT-compatible JSON serialization of `[PatchDocument]` types

### Modified Capabilities

- `source-generation`: Converter visibility changes from `private` to `internal`; new `Collect()` pipeline step to generate assembly-wide resolver

## Impact

- `src/Patchly.Generators/PatchDocumentGenerator.cs` — new pipeline step, converter visibility change, resolver generation
- `src/Patchly.Generators/PatchClassModel.cs` — add fully-qualified type name field
- Generated code — new `PatchlyJsonTypeInfoResolver.g.cs` file emitted per assembly
- README.md — new Native AOT section
- No breaking changes to existing public API
