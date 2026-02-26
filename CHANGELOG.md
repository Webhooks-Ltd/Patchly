# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Project logo and NuGet package icon

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
