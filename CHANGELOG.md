# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
