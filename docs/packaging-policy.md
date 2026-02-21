# Packaging Policy

Rules and best practices for NuGet packaging, dependencies, and build configuration.

## Single Package Strategy

Patchly ships as a single NuGet package containing both the core library and the source generator:

- `lib/net6.0/Patchly.dll` — core library (IPatchDocument, PatchDocumentAttribute)
- `analyzers/dotnet/cs/Patchly.Generators.dll` — source generator (compile-time only)

Users install with `dotnet add package Patchly`. Nothing else needed.

## Dependency Rules

### Core Library (Patchly)

- **Zero NuGet dependencies.** System.Text.Json ships in-box with .NET 6+. Do not add a package reference for it.
- If a new dependency is genuinely needed, it must be discussed and justified — every runtime dependency is a cost to consumers.

### Source Generator (Patchly.Generators)

- **Must target `netstandard2.0`.** This is a Roslyn hosting requirement, non-negotiable.
- **`Microsoft.CodeAnalysis.CSharp` must use `PrivateAssets="all"`** — this prevents the Roslyn dependency from flowing to consuming projects, which would cause assembly load conflicts.
- Pin to the **lowest viable version** of `Microsoft.CodeAnalysis.CSharp` for maximum SDK compatibility. Currently `4.8.0` (supports .NET 8 SDK 8.0.100+).
- **`SuppressDependenciesWhenPacking=true`** — prevents any generator dependencies from appearing in the NuGet package's dependency list.
- **`IncludeBuildOutput=false`** — the generator DLL is packed via an explicit `<None>` item into `analyzers/dotnet/cs`, not via the default `lib/` output.
- **`DevelopmentDependency=true`** — marks the generator as a development-only dependency.
- If the generator needs helper libraries beyond Roslyn, they must be **ILMerged or embedded** into the generator assembly. Separate DLLs in the analyzers folder cause load issues.
- **PolySharp** or similar may be needed to polyfill modern C# features (e.g., `IsExternalInit`, `HashCode`) that compile to netstandard2.0.

### Test Projects

- Test dependencies (xUnit, FluentAssertions, etc.) are not packaged. No special restrictions beyond keeping versions current.

## Generator Packaging Configuration

The `Patchly.csproj` must include the generator DLL in the package:

```xml
<ItemGroup>
  <None Include="..\Patchly.Generators\$(OutputPath)\Patchly.Generators.dll"
        Pack="true"
        PackagePath="analyzers/dotnet/cs"
        Visible="false" />
</ItemGroup>
```

The `Patchly.Generators.csproj` must have:

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
  <IsRoslynComponent>true</IsRoslynComponent>
  <IncludeBuildOutput>false</IncludeBuildOutput>
  <DevelopmentDependency>true</DevelopmentDependency>
  <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0"
                    PrivateAssets="all" />
</ItemGroup>
```

## Build Configuration

### Directory.Build.props (shared across all projects)

- `LangVersion=latest`
- `Nullable=enable`
- `TreatWarningsAsErrors=true`
- `Deterministic=true`
- `ContinuousIntegrationBuild=true` when `$(CI)` is set
- SourceLink via `Microsoft.SourceLink.GitHub`
- `IncludeSymbols=true` with `SymbolPackageFormat=snupkg`
- `PublishRepositoryUrl=true` and `EmbedUntrackedSources=true`

### Central Package Management

All dependency versions are defined in `Directory.Packages.props`. Individual `.csproj` files must not specify versions — only package names.

## Versioning

- SemVer 2.0 strict.
- Version is determined at CI time by the [`paulhatch/semantic-version`](https://github.com/PaulHatch/semantic-version) GitHub Action, based on commit history and conventional-commit-style tags.
- Do **not** hardcode version numbers in `.csproj` or `Directory.Build.props`. The CI pipeline passes the version via `/p:Version=` at build/pack time.
- Start at `0.1.0` (pre-stable API). Move to `1.0.0` when the public API is stable.
- Generator output changes that alter compiled behaviour are at minimum a **minor** version bump.
- Pre-release: `-alpha.N`, `-beta.N`, `-rc.N`. CI builds get automatic pre-release suffixes from `semantic-version`.

## Package Metadata

Every packable project must include (or inherit from Directory.Build.props):

- `PackageLicenseExpression=MIT`
- `PackageReadmeFile=README.md`
- `PackageTags` — `patch`, `partial-update`, `aspnetcore`, `source-generator`, `system-text-json`, `openapi`
- `RepositoryUrl` and `RepositoryType=git`
- `Description`
- `PackageIcon` (if available)
