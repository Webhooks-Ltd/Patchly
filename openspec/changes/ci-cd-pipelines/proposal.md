## Why

Patchly has no CI/CD pipelines. Code gets pushed without automated build verification or test execution, and there's no automated path to publish the NuGet package. GitHub Actions workflows are needed to validate PRs, run tests on push, and publish releases to nuget.org.

## What Changes

- Add a **CI workflow** (build + test) that runs on PRs and pushes to `main`
- Add a **Release workflow** that packs and publishes the NuGet package to nuget.org when a GitHub release is created
- Release workflow extracts the version directly from the GitHub release tag (e.g., `v0.1.0` → `0.1.0`)
- CI workflow sets the `CI` environment variable to activate `ContinuousIntegrationBuild` and SourceLink

## Capabilities

### New Capabilities

- `ci-cd-pipelines`: GitHub Actions workflows for build/test CI and NuGet release publishing

### Modified Capabilities

- `build-infrastructure`: Add requirement for CI environment variable handling and version injection via GitHub Actions

## Impact

- New `.github/workflows/` directory with workflow YAML files
- No changes to existing source code or project files
- Requires a `NUGET_API_KEY` secret to be configured in the GitHub repository for publishing
