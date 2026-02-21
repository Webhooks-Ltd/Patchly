# Patchly — Partial Updates for ASP.NET Core

## Overview

**One-liner**: A source-generated partial update library for ASP.NET Core that distinguishes null from absent, with zero ceremony and clean OpenAPI schemas.

**Tagline**: PATCH done right for .NET.

**License**: MIT

**Repository**: Open-source NuGet package.

## Goals

1. Solve the null-vs-absent problem for HTTP PATCH in ASP.NET Core without wrapper types, Optional<T>, or JSON Patch documents.
2. Generate a System.Text.Json JsonConverter at compile time that tracks which JSON properties were present during deserialization.
3. Produce OpenAPI schemas that look like plain DTOs — no tracking properties leak into OpenAPI, and generated clients (NSwag, Kiota) work cleanly.
4. Be AOT-friendly and trimming-safe through source generation. No runtime reflection.
5. Require the absolute minimum ceremony from the developer: add `[PatchDocument]` to a partial class with nullable properties. Done.

## Tech Stack

- .NET 6+ for the core library (`net6.0` TFM)
- `netstandard2.0` for the source generator assembly (required by Roslyn analyzer/generator hosting)
- C# 12+
- System.Text.Json (no Newtonsoft.Json dependency)
- Microsoft.CodeAnalysis.CSharp (Roslyn) for the incremental source generator
- xUnit + FluentAssertions for unit tests
- ASP.NET Core for integration tests
- Verify (optional) for snapshot testing of generated source

## Package Structure

```
src/
  Patchly/                     # Core library: IPatchDocument, [PatchDocument] attribute
  Patchly.Generators/          # Source generator: JsonConverter, WasProvided, Provided accessor
tests/
  Patchly.Tests/               # Unit tests (converter, tracking, attribute, integration)
```

### Package Roles

- **Patchly** (`net6.0`): Ships `IPatchDocument` interface and `[PatchDocument]` marker attribute. Also bundles the source generator in the `analyzers/dotnet/cs` NuGet folder. Single package install.
- **Patchly.Generators** (`netstandard2.0`): The Roslyn incremental source generator. Packed into the Patchly NuGet package's analyzers folder. Not referenced directly by user code at runtime.

## Architecture

### How It Works

1. Developer creates a partial class with nullable properties and applies `[PatchDocument]`:

```csharp
[PatchDocument]
public partial class CustomerPatch
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? Age { get; set; }
}
```

2. The incremental source generator (Patchly.Generators) detects the attribute and emits a companion partial class file containing:
   - A `HashSet<string>` field (`_providedProperties`) tracking which JSON property names appeared during deserialization.
   - `IPatchDocument` interface implementation.
   - `bool WasProvided(string propertyName)` method.
   - A nested `ProvidedSet` readonly struct with per-property `bool` accessors, exposed via a `Provided` property.
   - A nested `JsonConverter<CustomerPatch>` class using `Utf8JsonReader` to deserialize and record provided properties.
   - A `[JsonConverter(typeof(...))]` attribute on the partial class pointing to the generated converter.

3. At runtime, ASP.NET Core model binding deserializes the request body using System.Text.Json, which invokes the generated converter. The converter reads each JSON property, records its name in the tracking set, and assigns the value. Properties absent from the JSON payload are never recorded.

4. In the controller/handler, the developer checks `patch.Provided.FirstName` or `patch.WasProvided("FirstName")` to determine which fields to apply.

### Source Generator Pipeline

The generator uses `IIncrementalGenerator` with `ForAttributeWithMetadataName` for optimal performance:
- Filters to classes decorated with `[PatchDocument]`
- Extracts property metadata (name, type, JSON property name) into a value-equatable model
- Emits the partial class extension via `RegisterSourceOutput`

### Key Constraint: No Runtime Reflection

All type inspection happens at compile time inside the generator. The generated code uses only direct property access — no `PropertyInfo`, no `Expression<Func<>>`, no `dynamic`. This ensures full compatibility with .NET Native AOT and IL trimming.

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Source generator, not runtime reflection | AOT/trimming safety. Zero startup cost. Compile-time validation. |
| Nullable properties on the patch DTO | `string?` and `int?` allow the developer to represent "client sent null" (value is null, property was provided) vs "client didn't send this" (property not provided). Non-nullable value types cannot distinguish these states. |
| `HashSet<string>` for tracking | O(1) lookup. String-based to match JSON property names after case normalization. |
| Custom `JsonConverter<T>` per patch class | Only way to intercept individual property reads in System.Text.Json without reflection. STJ's built-in deserialization does not expose "which properties were present". |
| `[JsonIgnore]` on tracking members | Prevents `_providedProperties`, `WasProvided`, `ProvidedProperties`, and `Provided` from appearing in serialization output or OpenAPI schemas. |
| `IPatchDocument` as a marker/contract interface | Enables generic constraints and middleware that operate on any patch document. |
| Partial class requirement | Source generators can only add members to partial classes. The attribute triggers a diagnostic if applied to a non-partial class. |
| `netstandard2.0` for the generator assembly | Required by the Roslyn analyzer hosting model. All generators and analyzers must target netstandard2.0. |

## Conventions

- All public API types have XML documentation comments.
- No compiler warnings (treat warnings as errors in CI).
- Generated code includes `[GeneratedCode]` and `#nullable enable` pragmas.
- Generated file names follow the pattern `{ClassName}.g.cs`.
- JSON property name matching uses `camelCase` by default (matching `JsonSerializerDefaults.Web`) but respects `[JsonPropertyName]` overrides.
- Property name comparisons in `WasProvided()` accept the C# property name (PascalCase). The generated code maps between C# names and JSON names internally.
- Integration tests validate the full round-trip: HTTP request with partial JSON -> controller -> `WasProvided` / `Provided` check -> verify entity state.

## Key Dependencies (allowed)

- System.Text.Json (ships with .NET 8+)
- Microsoft.CodeAnalysis.CSharp (generator project only, not a runtime dependency)
- Microsoft.Extensions.DependencyInjection.Abstractions (if service registration helpers are added)

## Key Dependencies (explicitly excluded)

- Newtonsoft.Json
- Microsoft.AspNetCore.JsonPatch
- Any reflection-based serialization library
