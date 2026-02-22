# Build Infrastructure

## Purpose

Define the solution structure, build configuration, NuGet packaging strategy, and dependency management for the Patchly project.

## Requirements

### Requirement: Solution Structure

The solution SHALL contain the following projects with the specified target frameworks.

#### Scenario: Core library targets net6.0
- **WHEN** the `Patchly` project is inspected
- **THEN** it targets `net6.0`
- **AND** it contains `IPatchDocument` interface and `PatchDocumentAttribute` class

#### Scenario: Generator targets netstandard2.0
- **WHEN** the `Patchly.Generators` project is inspected
- **THEN** it targets `netstandard2.0`
- **AND** it has `IsRoslynComponent` set to true
- **AND** it references `Microsoft.CodeAnalysis.CSharp` with `PrivateAssets="all"`

#### Scenario: Test project targets net6.0
- **WHEN** the `Patchly.Tests` project is inspected
- **THEN** it targets `net6.0`
- **AND** it references xUnit and FluentAssertions

### Requirement: Single NuGet Package

The `Patchly` NuGet package SHALL bundle both the core library and the source generator in a single package.

#### Scenario: Generator is packed into analyzers folder
- **WHEN** `dotnet pack` is run on the `Patchly` project
- **THEN** the output `.nupkg` contains the core library in `lib/net6.0/`
- **AND** the generator assembly in `analyzers/dotnet/cs/`

#### Scenario: Generator is not a runtime dependency
- **WHEN** a consuming project references the `Patchly` package
- **THEN** `Patchly.Generators.dll` is NOT copied to the output directory
- **AND** it is NOT referenced at runtime

#### Scenario: Single install command
- **WHEN** a developer runs `dotnet add package Patchly`
- **THEN** the `[PatchDocument]` attribute, `IPatchDocument` interface, and source generator are all available without additional package references

### Requirement: Directory.Build.props Configuration

The solution SHALL have a `Directory.Build.props` at the root with shared build settings.

#### Scenario: Shared compiler settings
- **WHEN** any project in the solution is compiled
- **THEN** `LangVersion` is `latest`
- **AND** `Nullable` is `enable`
- **AND** `TreatWarningsAsErrors` is `true`
- **AND** `Deterministic` is `true`

#### Scenario: SourceLink is configured
- **WHEN** any project in the solution is compiled in CI
- **THEN** `PublishRepositoryUrl` is `true`
- **AND** `EmbedUntrackedSources` is `true`
- **AND** `IncludeSymbols` is `true` with `snupkg` format

### Requirement: Central Package Management

The solution SHALL use a `Directory.Packages.props` file to centralise dependency versions.

#### Scenario: All package versions are centralised
- **WHEN** a project references a NuGet package
- **THEN** the version is specified in `Directory.Packages.props` not in the individual `.csproj`

### Requirement: Package Metadata

The `Patchly` package SHALL include complete NuGet metadata for public publishing.

#### Scenario: Required metadata is present
- **WHEN** the package is packed
- **THEN** the `.nupkg` contains:
  - `PackageLicenseExpression` set to `MIT`
  - `PackageReadmeFile` pointing to `README.md`
  - `PackageTags` including `patch`, `partial-update`, `aspnetcore`, `source-generator`, `system-text-json`, `openapi`
  - `RepositoryUrl` and `RepositoryType`
  - `Description`

### Requirement: Versioning

All packages in the solution SHALL share the same version number, determined at release time from the GitHub release tag.

#### Scenario: Version is extracted from release tag
- **WHEN** the release workflow runs
- **THEN** the version is extracted from the GitHub release tag by stripping the `v` prefix (e.g., `v0.1.0` -> `0.1.0`)
- **AND** the version is passed to `dotnet pack` via `/p:Version=`
- **AND** no version number is hardcoded in `.csproj` or `Directory.Build.props`

#### Scenario: Initial version
- **WHEN** v1 is first published
- **THEN** the version is `0.1.0` (signalling pre-stable API)

#### Scenario: Version tag format
- **WHEN** a GitHub release is created for publishing
- **THEN** the tag SHALL use the `v` prefix followed by a SemVer 2.0 version (e.g., `v0.1.0`, `v1.0.0-beta.1`)
