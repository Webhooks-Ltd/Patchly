## MODIFIED Requirements

### Requirement: Versioning

All packages in the solution SHALL share the same version number, determined at release time from the GitHub release tag.

#### Scenario: Version is extracted from release tag
- **WHEN** the release workflow runs
- **THEN** the version is extracted from the GitHub release tag by stripping the `v` prefix (e.g., `v0.1.0` → `0.1.0`)
- **AND** the version is passed to `dotnet pack` via `/p:Version=`
- **AND** no version number is hardcoded in `.csproj` or `Directory.Build.props`

#### Scenario: Initial version
- **WHEN** v1 is first published
- **THEN** the version is `0.1.0` (signalling pre-stable API)

#### Scenario: Version tag format
- **WHEN** a GitHub release is created for publishing
- **THEN** the tag SHALL use the `v` prefix followed by a SemVer 2.0 version (e.g., `v0.1.0`, `v1.0.0-beta.1`)
