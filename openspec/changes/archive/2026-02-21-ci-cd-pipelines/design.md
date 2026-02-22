## Context

Patchly is a single-package NuGet library hosted on GitHub at `Webhooks-Ltd/Patchly`. The repository has a `Directory.Build.props` that sets `ContinuousIntegrationBuild=true` when the `CI` environment variable is present, and SourceLink is configured via `Microsoft.SourceLink.GitHub`. The packaging policy specifies `paulhatch/semantic-version` for version computation and `/p:Version=` injection at pack time.

Currently there are no GitHub Actions workflows. The solution contains two test projects: `Patchly.Tests` (unit tests) and `Patchly.IntegrationTests` (ASP.NET Core integration tests).

## Goals / Non-Goals

**Goals:**
- Automated build and test on every PR and push to `main`
- Automated NuGet package publish when a GitHub release is created
- Version computed from commit history via `paulhatch/semantic-version`
- SourceLink and deterministic builds active in CI

**Non-Goals:**
- Pre-release package publishing from branches (can be added later)
- Automated GitHub release creation (releases are created manually)
- Code coverage reporting or quality gates
- Multi-OS matrix builds (single Ubuntu runner is sufficient)

## Decisions

### Two separate workflows

**CI workflow** (`ci.yml`) runs on PRs and pushes to `main`. **Release workflow** (`release.yml`) runs when a GitHub release is published.

Rationale: Separation of concerns. CI is fast feedback on every change. Release is a deliberate action with NuGet publishing. Combining them would require complex conditional logic.

Alternative considered: Single workflow with conditional jobs. Rejected because it makes the workflow harder to read and maintain.

### ubuntu-latest runner

Both workflows run on `ubuntu-latest`.

Rationale: Fastest startup time, cheapest runner, and the solution is fully cross-platform (.NET 6+). No Windows-specific build steps are needed.

### actions/setup-dotnet with .NET 10.0

Use `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`.

Rationale: Test projects target `net10.0` and the solution uses `.slnx` format (requires .NET 9+ SDK). The .NET 10 SDK can build all targets in the solution (`net6.0`, `netstandard2.0`, `net10.0`). Using a single SDK version keeps the workflow simple.

Alternative considered: .NET 8 SDK. Rejected because it cannot build `net10.0` targets or parse `.slnx` solution files.

### Version extracted from GitHub release tag

The release workflow extracts the version from the GitHub release tag (e.g., `v0.1.0` → `0.1.0`) and passes it to `dotnet pack` via `/p:Version=`.

Rationale: Deterministic — the NuGet package version always matches the GitHub release tag. Simpler than `paulhatch/semantic-version` which computes versions from commit history and could diverge from the tag.

Alternative considered: `paulhatch/semantic-version` action. Rejected because it introduces a mismatch risk between the GitHub release tag and the computed version, requires `fetch-depth: 0` for full history, and adds unnecessary complexity when the tag already contains the version.

### NuGet publish via dotnet nuget push

Use `dotnet nuget push` with `--api-key` from the `NUGET_API_KEY` repository secret.

Rationale: Simplest approach. No additional actions needed. The `--skip-duplicate` flag prevents failures if a version is accidentally re-published.

### CI sets the CI environment variable

The CI workflow explicitly sets `CI: true` in the environment to activate `ContinuousIntegrationBuild` and SourceLink in `Directory.Build.props`.

Rationale: GitHub Actions sets `CI=true` by default, but being explicit makes the intent clear.

## Risks / Trade-offs

- **[Risk] NUGET_API_KEY secret not configured** → Publishing will fail with a clear error. Documented in the workflow file and tasks.
- **[Risk] Release tag missing `v` prefix** → Version extraction step strips the `v` prefix. If the tag doesn't start with `v`, the raw tag is used as the version. Document the expected format (`vX.Y.Z`).
- **[Trade-off] No matrix build** → We don't test on Windows/macOS, but the library has no platform-specific code. Acceptable for a source generator + STJ library.
- **[Trade-off] No pre-release publishing** → Branch builds don't publish packages. This keeps the workflow simple but means manual testing of pre-release packages. Can be added later if needed.
