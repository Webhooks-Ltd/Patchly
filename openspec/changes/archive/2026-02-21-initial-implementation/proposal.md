## Why

There is no .NET library that solves HTTP PATCH partial updates with all of: null-vs-absent distinction, clean OpenAPI schemas (no wrapper types), System.Text.Json native, and source-generated (AOT-safe). Patchly fills this gap. This change implements the complete v1: core library, source generator, build infrastructure, and test suite.

## What Changes

- Implement the `Patchly` core library: `IPatchDocument` interface, `[PatchDocument]` attribute, shipped as a single NuGet package with the generator bundled in `analyzers/dotnet/cs`
- Implement the `Patchly.Generators` incremental source generator producing:
  - A `System.Text.Json` `JsonConverter<T>` per patch class that tracks provided properties during deserialization
  - `WasProvided(string)` method and `IReadOnlySet<string> ProvidedProperties` (from `IPatchDocument`)
  - A nested `Provided` accessor struct with per-property `bool` members for ergonomic usage (`patch.Provided.FirstName`)
- Add solution infrastructure: `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, solution file
- Add comprehensive test suite: unit tests for serialization/tracking, source generator output tests, ASP.NET Core integration tests
- Fix spec gaps identified in architecture review:
  - Add PATCH006 diagnostic: missing parameterless constructor
  - Add PATCH013 diagnostic: `init`-only properties not supported
  - Clarify converter must delegate per-property deserialization to `JsonSerializer.Deserialize<T>(ref reader, options)` to respect `NumberHandling` and other options
  - Address `[JsonExtensionData]`, `[JsonConstructor]`, and `required` keyword edge cases
  - Clarify serialization `Write` method must respect `DefaultIgnoreCondition`
- No `ApplyTo` in v1 — ship `WasProvided` and `Provided` accessor only

## Capabilities

### New Capabilities
- `provided-accessor`: The source-generated nested `Provided` struct with per-property bool members for ergonomic property tracking (`patch.Provided.FirstName` instead of `patch.WasProvided(nameof(patch.FirstName))`)
- `build-infrastructure`: Solution structure, Directory.Build.props, central package management, NuGet packaging configuration (single package with generator in analyzers folder), SourceLink, deterministic builds

### Modified Capabilities
- `patch-document-attribute`: Add PATCH006 (no parameterless constructor), PATCH013 (init-only properties), clarify `required` keyword handling, clarify `[JsonExtensionData]` and `[JsonConstructor]` are unsupported
- `source-generation`: Add `Provided` accessor struct generation, remove `ApplyTo` from v1 scope, fix `ForAttributeWithMetadataName` to use string overload, add generator robustness requirements (skip unresolvable types)
- `serialization`: Clarify per-property deserialization delegates to `JsonSerializer.Deserialize<T>(ref reader, options)`, add `DefaultIgnoreCondition` handling in Write method, add `[JsonNumberHandling]` per-property support
- `openapi-compatibility`: Ensure `Provided` accessor struct is hidden from schema, remove Swashbuckle references

## Impact

- **New files**: Solution file, 3 projects (Patchly, Patchly.Generators, Patchly.Tests), build props, package configuration
- **Dependencies**: Microsoft.CodeAnalysis.CSharp 4.8.0 (generator only), xUnit + FluentAssertions (tests only), Microsoft.SourceLink.GitHub (build only)
- **Target frameworks**: net6.0 (core library), netstandard2.0 (generator)
- **Public API surface**: `Patchly.PatchDocumentAttribute`, `Patchly.IPatchDocument`, generated `WasProvided`, generated `Provided` accessor
