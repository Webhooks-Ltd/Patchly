# Patchly

Source-generated partial update (HTTP PATCH) DTOs for ASP.NET Core.

## Rules

- When adding or changing features, update `README.md` to reflect the current state of the library
- Don't add comments to code unless absolutely necessary
- Read `openspec/project.md` for full project context, architecture, and conventions
- Read `openspec/specs/` for detailed requirements and scenarios before implementing
- Follow `docs/packaging-policy.md` for all NuGet packaging, dependency, and versioning decisions
- Use `openspec status` to check current change progress

## Tech Stack

- Core library: `net6.0`
- Source generator: `netstandard2.0`
- System.Text.Json (no Newtonsoft)
- No runtime reflection

## Package Structure

Single NuGet package `Patchly` — core library in `lib/net6.0/`, generator in `analyzers/dotnet/cs/`.
