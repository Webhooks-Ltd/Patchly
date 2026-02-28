# Patchly

Source-generated partial update (HTTP PATCH) DTOs for ASP.NET Core.

## Rules

- When adding or changing features, update `README.md` to reflect the current state of the library
- When adding or changing features, update `CHANGELOG.md` under the `[Unreleased]` section
- Don't add comments to code unless absolutely necessary
- Read `openspec/project.md` for full project context, architecture, and conventions
- Read `openspec/specs/` for detailed requirements and scenarios before implementing
- Follow `docs/packaging-policy.md` for all NuGet packaging, dependency, and versioning decisions
- Use `docs/backlog/` for idea backlog management (`index.md`, `ideas/*.md`, and template)
- Do not create OpenSpec change stubs just to park ideas; create OpenSpec changes only when an idea is ready for specification/implementation
- When adding or changing diagnostics, update `docs/diagnostics.md` and keep the diagnostics section in `README.md` in sync
- Use `openspec status` to check current change progress

## Commit Convention

Use [Conventional Commits](https://www.conventionalcommits.org/):

| Prefix | When |
|---|---|
| `feat:` | New capability / public API |
| `fix:` | Bug fix |
| `docs:` | README, XML docs |
| `chore:` | CI, build, housekeeping |
| `refactor:` | Internal restructuring |
| `test:` | Tests only |
| `feat!:` / `fix!:` | Breaking change |

Lowercase after prefix, imperative mood, under 72 chars. Optional scope: `feat(generator): add inheritance support`.

## Tech Stack

- Core library: `net6.0`
- Source generator: `netstandard2.0`
- System.Text.Json (no Newtonsoft)
- No runtime reflection

## Package Structure

Single NuGet package `Patchly` — core library in `lib/net6.0/`, generator in `analyzers/dotnet/cs/`.
