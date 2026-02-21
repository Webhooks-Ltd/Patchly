## Context

Patchly is a greenfield open-source NuGet package. There is no existing codebase. The design must establish the solution structure, source generator architecture, NuGet packaging strategy, and public API from scratch.

The library targets ASP.NET Core developers who need HTTP PATCH endpoints that distinguish null from absent properties, without wrapper types that pollute OpenAPI schemas. The integration point is System.Text.Json's `JsonConverter<T>` — no ASP.NET Core model binding or middleware is involved.

Constraints:
- Source generators must target `netstandard2.0` (Roslyn hosting requirement)
- No runtime reflection (AOT/trimming safety)
- Generated code must not leak into OpenAPI schemas
- Single NuGet package for simplest possible adoption

## Goals / Non-Goals

**Goals:**
- Ship a working v1 with `[PatchDocument]` attribute, `IPatchDocument` interface, source-generated `JsonConverter`, `WasProvided(string)`, and `Provided` accessor
- Clean OpenAPI schema output with zero configuration
- Comprehensive test coverage: unit, generator output, and ASP.NET Core integration
- Single `dotnet add package Patchly` installation experience
- Deterministic, SourceLink-enabled builds ready for NuGet publishing

**Non-Goals:**
- `ApplyTo` method (deferred to v2 — needs target type resolution design)
- Nested partial update tracking across object boundaries (each `[PatchDocument]` tracks its own level)
- Newtonsoft.Json support
- .NET versions older than 8.0

## Decisions

### 1. Single NuGet package with bundled generator

**Decision:** Ship one package `Patchly` containing both the core library (`lib/net6.0/`) and the source generator (`analyzers/dotnet/cs/`).

**Alternatives considered:**
- Two packages (`Patchly` + `Patchly.Generators`): More flexible but adds installation friction. Users must remember both packages and keep versions in sync.
- Generator-only package emitting attribute via `RegisterPostInitializationOutput`: Eliminates the core library entirely, but prevents shipping `IPatchDocument` as a runtime-resolvable interface for generic constraints.

**Rationale:** The Microsoft STJ source generator ships this way. One package, one install command. The generator DLL goes in the `analyzers` folder so it's compile-time only, never a runtime dependency.

### 2. JsonConverter integration (not model binding)

**Decision:** Hook into System.Text.Json via a `[JsonConverter]` attribute on the generated partial class. No ASP.NET Core middleware, formatters, or model binders.

**Alternatives considered:**
- Custom `InputFormatter`: ASP.NET Core only. Doesn't work in SignalR, message consumers, manual deserialization, or unit tests without the full pipeline.
- Custom `IModelBinder`: Same ASP.NET Core coupling. Different registration for controllers vs minimal APIs.
- `JsonDocument` pre-parse in middleware: Double-parses the body. Fragile. Couples tracking to HTTP context.

**Rationale:** The `[JsonConverter]` attribute is self-describing on the type. STJ picks it up automatically everywhere — ASP.NET Core, SignalR, queues, manual `JsonSerializer.Deserialize`, unit tests. Zero registration, zero configuration.

### 3. Per-property deserialization via `JsonSerializer.Deserialize<T>(ref reader, options)`

**Decision:** The generated converter delegates each property's deserialization to `JsonSerializer.Deserialize<TProperty>(ref reader, options)` rather than calling `reader.GetInt32()`, `reader.GetString()`, etc. directly.

**Alternatives considered:**
- Direct `Utf8JsonReader` methods (`GetInt32`, `GetString`, etc.): Faster for primitives but ignores `NumberHandling`, custom converters on property types, and other `JsonSerializerOptions` configuration.

**Rationale:** Delegating to the serializer ensures full compatibility with the application's configured options: `NumberHandling.AllowReadingFromString` (Web defaults), custom enum converters, nested `[PatchDocument]` types, and any other registered converters. The performance difference is negligible for PATCH payloads (typically small).

### 4. `Provided` accessor as a nested readonly struct

**Decision:** Generate a nested `readonly struct` per patch class with `bool` properties for each tracked property. Accessed via a `Provided` property on the patch class.

**Alternatives considered:**
- `WasProvided(nameof(...))` only: Works but verbose and string-based.
- Lambda overload `WasProvided(p => p.FirstName)`: Requires `Expression<Func<>>` which is not AOT-friendly, or complex source-generator interception.
- Per-property bool properties directly on the class (`FirstNameWasProvided`): Pollutes the class surface and could leak into OpenAPI schemas.

**Rationale:** `patch.Provided.FirstName` is ergonomic, IntelliSense-discoverable, and the struct is hidden from OpenAPI via `[JsonIgnore]`. The string-based `WasProvided` remains on `IPatchDocument` for generic/dynamic scenarios. Both APIs coexist.

### 5. Property name matching strategy

**Decision:** The generated converter computes JSON-to-C# property name mappings by consulting `options.PropertyNamingPolicy` at deserialization time, with `[JsonPropertyName]` overrides taking precedence. Matching respects `options.PropertyNameCaseInsensitive`.

**Alternatives considered:**
- Bake in camelCase at generation time: Simpler but breaks if the application uses a different naming policy (snake_case, etc.).
- Pre-compute a `Dictionary<string, string>` on first use: Adds a static cache concern and thread-safety complexity.

**Rationale:** Consulting `options` at runtime is the most correct approach. PATCH payloads are small (typically <20 properties), so the per-property `ConvertName` call is not a bottleneck.

### 6. Naming the Provided struct type

**Decision:** Name the nested struct `ProvidedSet` (not `ProvidedProperties`) to avoid collision with the `IPatchDocument.ProvidedProperties` member.

**Rationale:** The accessor property is `Provided`, the type is `ProvidedSet`. `ProvidedProperties` on the interface returns `IReadOnlySet<string>` — a different concept (the raw set of names vs the ergonomic bool accessor). Distinct names prevent confusion.

### 7. Unsupported features emit diagnostics

**Decision:** The following are unsupported with specific diagnostics:
- Non-partial class (PATCH001, Error)
- Struct (PATCH002, Error)
- Record (PATCH003, Error)
- Abstract class (PATCH004, Error)
- Generic class (PATCH005, Error)
- No parameterless constructor (PATCH006, Error)
- `init`-only properties (PATCH013, Error — cannot be set in converter's read loop)
- Non-nullable value type property (PATCH010, Warning)
- No public properties (PATCH011, Warning)
- Read-only property (PATCH012, Warning)
- `[JsonExtensionData]` on a property (PATCH014, Error — not supported)
- `[JsonConstructor]` on a constructor (PATCH015, Warning — ignored by generated converter)

## Risks / Trade-offs

**[Risk] Naming policy runtime resolution adds per-request overhead** → The overhead is trivial for PATCH payloads (small property count). If profiling shows it matters, a per-options cached lookup can be added as a non-breaking optimisation.

**[Risk] `required` keyword on properties causes compile errors in generated `new()` call** → The generated converter will include `[System.Diagnostics.CodeAnalysis.SetsRequiredMembers]` on its construction path. If this proves fragile, we fall back to a diagnostic (PATCH016, Error).

**[Risk] Generator crashes on malformed user code** → The generator pipeline wraps type inspection in defensive checks and skips unresolvable types rather than throwing. A diagnostic (PATCH099, Warning) is emitted for skipped types.

**[Risk] Single-package approach makes it harder to use the attribute without the generator** → Acceptable for v1. If a real need emerges (e.g., shared contracts library), we can split the packages later without breaking the public API.

