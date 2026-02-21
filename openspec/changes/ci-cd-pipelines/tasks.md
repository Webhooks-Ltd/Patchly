## 1. CI Workflow

- [x] 1.1 Create `.github/workflows/ci.yml` with trigger on PRs and pushes to `main`
- [x] 1.2 Configure `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`
- [x] 1.3 Add `dotnet restore` step
- [x] 1.4 Add `dotnet build` step with `--no-restore` flag
- [x] 1.5 Add `dotnet test` step with `--no-build` flag
- [x] 1.6 Set `CI: true` environment variable at the workflow level

## 2. Release Workflow

- [x] 2.1 Create `.github/workflows/release.yml` with trigger on `release: types: [published]`
- [x] 2.2 Configure `actions/setup-dotnet@v4` with `dotnet-version: '10.0.x'`
- [x] 2.3 Add version extraction step that strips the `v` prefix from the GitHub release tag
- [x] 2.4 Add `dotnet restore` step
- [x] 2.5 Add `dotnet build` step (full solution, so generator DLL exists for pack)
- [x] 2.6 Add `dotnet test` step
- [x] 2.7 Add `dotnet pack` step for the `Patchly` project with `-c Release` and `/p:Version=` from extracted tag version
- [x] 2.8 Add `actions/upload-artifact` step to upload `.nupkg` and `.snupkg` files
- [x] 2.9 Add `dotnet nuget push` step targeting `https://api.nuget.org/v3/index.json` with `NUGET_API_KEY` secret and `--skip-duplicate`
- [x] 2.10 Set `CI: true` environment variable at the workflow level
