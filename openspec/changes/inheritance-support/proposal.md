## Why

Users may want to share common patch properties across DTOs via inheritance (e.g., `AuditPatch` base with `ModifiedBy`). Currently `[PatchDocument]` on a derived class doesn't pick up base class properties, and applying it to both base and derived is untested/unsupported.

## What Changes

- Support `[PatchDocument]` on a class that inherits from another `[PatchDocument]` class
- The derived converter should track properties from both the base and derived class
- The `Provided` accessor should include base class properties
- Handle the case where only the derived class has `[PatchDocument]` but the base has trackable properties

## Capabilities

### New Capabilities

- `inheritance-support`: `[PatchDocument]` works correctly on class hierarchies

## Impact

- `src/Patchly.Generators/PatchDocumentGenerator.cs` — walk base type chain for property discovery
- Tests for single and multi-level inheritance
- README.md — document inheritance behaviour
- Potential breaking change if current behaviour silently ignores base properties
