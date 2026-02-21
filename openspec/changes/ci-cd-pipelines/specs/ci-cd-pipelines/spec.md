## ADDED Requirements

### Requirement: CI Workflow

The repository SHALL have a GitHub Actions CI workflow that builds and tests the solution on every PR and push to the `main` branch.

#### Scenario: CI runs on pull requests to main
- **WHEN** a pull request is opened, synchronised, or reopened targeting `main`
- **THEN** the CI workflow runs
- **AND** it builds the entire solution
- **AND** it runs all tests (unit and integration)

#### Scenario: CI runs on pushes to main
- **WHEN** code is pushed to the `main` branch
- **THEN** the CI workflow runs
- **AND** it builds and tests the solution

#### Scenario: CI uses .NET 10 SDK
- **WHEN** the CI workflow runs
- **THEN** it uses `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`

#### Scenario: CI sets the CI environment variable
- **WHEN** the CI workflow runs
- **THEN** the `CI` environment variable is set to `true`
- **AND** this activates `ContinuousIntegrationBuild` and SourceLink in `Directory.Build.props`

#### Scenario: CI build fails on warnings
- **WHEN** the CI workflow builds the solution
- **THEN** warnings are treated as errors (inherited from `Directory.Build.props`)
- **AND** any compiler warning causes the build to fail

#### Scenario: CI test failure fails the workflow
- **WHEN** any test fails during the CI workflow
- **THEN** the workflow reports failure
- **AND** the PR check shows as failed

### Requirement: Release Workflow

The repository SHALL have a GitHub Actions release workflow that packs and publishes the NuGet package when a GitHub release is published.

#### Scenario: Release workflow triggers on GitHub release
- **WHEN** a GitHub release is published (type `published`)
- **THEN** the release workflow runs

#### Scenario: Release workflow does not trigger on drafts
- **WHEN** a GitHub release draft is created or edited
- **THEN** the release workflow does NOT run

#### Scenario: Release extracts version from GitHub release tag
- **WHEN** the release workflow runs
- **THEN** it extracts the version from the GitHub release tag by stripping the `v` prefix (e.g., `v0.1.0` → `0.1.0`)
- **AND** the extracted version is passed to `dotnet pack` via `/p:Version=`

#### Scenario: Release builds and tests before publishing
- **WHEN** the release workflow runs
- **THEN** it builds the solution
- **AND** it runs all tests
- **AND** only proceeds to pack and publish if build and tests succeed

#### Scenario: Release packs the NuGet package
- **WHEN** the release workflow packs the project
- **THEN** it runs `dotnet pack` on the `Patchly` project with `-c Release` and the extracted version
- **AND** the output `.nupkg` is placed in a known output directory

#### Scenario: Release uploads package artifacts
- **WHEN** the release workflow has packed the NuGet package
- **THEN** it uploads the `.nupkg` and `.snupkg` files as workflow artifacts via `actions/upload-artifact`

#### Scenario: Release publishes to nuget.org
- **WHEN** the release workflow publishes
- **THEN** it runs `dotnet nuget push` targeting `https://api.nuget.org/v3/index.json`
- **AND** it uses the `NUGET_API_KEY` repository secret for authentication
- **AND** it uses `--skip-duplicate` to prevent failure on re-publish

#### Scenario: Release sets CI environment variable
- **WHEN** the release workflow runs
- **THEN** the `CI` environment variable is set to `true`
- **AND** SourceLink and deterministic build are active

### Requirement: Workflow Runner

Both workflows SHALL run on `ubuntu-latest`.

#### Scenario: CI runs on Ubuntu
- **WHEN** the CI workflow runs
- **THEN** it uses `runs-on: ubuntu-latest`

#### Scenario: Release runs on Ubuntu
- **WHEN** the release workflow runs
- **THEN** it uses `runs-on: ubuntu-latest`
