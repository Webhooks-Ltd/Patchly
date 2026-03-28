# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] - 2026-03-28

### Fixed

- Source Link debugging now works correctly for CI-built packages (deterministic build paths)

### Added

- Deterministic semantics mode for `[PatchDocument]` via `SemanticsMode = PatchSemanticsMode.DeterministicV1`
- `PatchValueState` (`Omitted`, `Null`, `Value`) and `IPatchDocument.GetState(string)`
- Generated `State` accessor for deterministic patch documents
- Diagnostic `PATCH030` warning for non-nullable collection properties in deterministic mode
- `UnknownPropertyHandling` (`Ignore`, `Reject`) on `[PatchDocument]` for opt-in rejection of unrecognized JSON properties

### Changed

- `PATCH010` warning message now clarifies value-only ambiguity in state semantics
- README expanded with deterministic semantics examples and collection replace behavior
- Generated converters and the .NET 8+ resolver now honor per-type unknown-property handling consistently

## [0.3.3] - 2026-02-27

### Fixed

- README rendering on NuGet (replaced HTML image tag with markdown)
- Icon padding trimmed for cleaner display

## [0.3.2] - 2026-02-27

### Added

- Project logo and NuGet package icon
- XML documentation file in NuGet package for IntelliSense support

## [0.3.1] - 2026-02-23

### Added

- `AddPatchly()` extension method for ASP.NET Core service registration — registers `PatchlyJsonTypeInfoResolver` into minimal API JSON options
- Correct OpenAPI schemas for streaming-path `[PatchDocument]` types when `AddPatchly()` is configured

### Changed

- `PatchlyJsonTypeInfoResolver` now returns Object-kinded `JsonTypeInfo` with populated `Properties` for streaming-path types instead of converter-wrapped `Kind = None` (fixes empty OpenAPI schemas)
- Buffered-path types (init-only / `[JsonConstructor]`) continue to use the converter-based resolver path

## [0.2.0]

### Added

- Support for `init`-only properties on `[PatchDocument]` classes via buffered deserialization
- Support for `[JsonConstructor]`-annotated parameterized constructors
- New diagnostics: PATCH016 (info), PATCH017 (warning), PATCH018, PATCH019, PATCH021, PATCH022

### Changed

- PATCH006 message now mentions `[JsonConstructor]` as an alternative to a parameterless constructor
- `[JsonConstructor]` constructors are now honored instead of ignored

### Removed

- PATCH013 diagnostic (init-only properties are now supported)
- PATCH015 diagnostic (`[JsonConstructor]` is no longer ignored)
