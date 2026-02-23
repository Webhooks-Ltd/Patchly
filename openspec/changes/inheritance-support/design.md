## Context

The generator currently uses `symbol.GetMembers()` to discover properties, which only returns members declared directly on the class — not inherited ones. A class hierarchy like `DerivedPatch : BasePatch` where both have `[PatchDocument]` will silently miss base class properties in the derived converter.

Base class property sharing is a common .NET pattern (e.g., `AuditPatch` with `ModifiedBy`, `CorrelationId`). This must work before v1.0.

## Goals / Non-Goals

**Goals:**
- Derived `[PatchDocument]` classes track properties from base `[PatchDocument]` classes
- `Provided` accessor includes base class properties
- `WasProvided`, `ProvidedProperties`, `GetProvidedValues` include base class properties
- Only the derived class generates a converter — the base class converter handles its own properties independently

**Non-Goals:**
- Supporting `[PatchDocument]` on abstract base classes (PATCH004 remains)
- Supporting inheritance from non-`[PatchDocument]` base classes (base properties are ignored)
- Polymorphic deserialization (`[JsonDerivedType]` support)

## Decisions

### Decision 1: Walk the BaseType chain for property discovery

Change the generator to walk `symbol.BaseType` recursively, collecting properties from each ancestor that has `[PatchDocument]`. The property list for a derived class includes all tracked properties from all `[PatchDocument]` ancestors plus its own declared properties.

Alternative: Only look at the immediate base type. Rejected because multi-level hierarchies are legitimate (e.g., `BasePatch → AuditPatch → CustomerPatch`).

### Decision 2: Each [PatchDocument] class generates independently

Both base and derived classes generate their own converters, tracking fields, and `Provided` accessors. The derived class's generated code covers all inherited + declared properties. The base class's generated code covers only its own.

This means deserializing a `DerivedPatch` tracks all properties (base + derived), while deserializing a `BasePatch` tracks only base properties. This is the correct behavior.

Alternative: Generate only for leaf classes and have them reference base class infrastructure. Rejected because the base class must also be independently usable.

### Decision 3: ProvidedSet includes inherited properties

The generated `ProvidedSet` struct on a derived class has bool properties for every tracked property — both inherited and declared. The struct is always generated fresh per class (not inherited from the base).

### Decision 4: Property name conflicts

If a derived class shadows a base property with `new`, only the derived property is tracked. This matches System.Text.Json behavior where the derived property wins.

### Decision 5: Only base classes with [PatchDocument] contribute

If a base class does NOT have `[PatchDocument]`, its properties are not included in the derived class's tracking. The attribute marks the boundary of what's tracked.

Alternative: Track all inherited properties regardless of attribute. Rejected because it would be surprising — the user should explicitly opt in via `[PatchDocument]`.

## Risks / Trade-offs

- [ProvidedSet size growth] → Deep hierarchies produce larger structs. Acceptable — structs are stack-allocated and the size is bounded by property count.
- [Name collisions from `new` keyword] → Handled by taking the derived property. Document this behavior.
- [PATCH004 blocks abstract base classes] → Intentional. Abstract bases can't be deserialized standalone. Users who want shared properties should use a concrete base class. Could revisit post-v1.0.
