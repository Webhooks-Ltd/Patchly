## 1. Property Discovery

- [ ] 1.1 Add helper method to check if a type symbol has `[PatchDocument]` attribute
- [ ] 1.2 Extract property collection logic from `TransformType` into a helper that walks `symbol.BaseType` chain, collecting properties from each ancestor with `[PatchDocument]`
- [ ] 1.3 Collect base properties in base→derived order, deduplicating by property name so derived class properties shadow base class properties
- [ ] 1.4 Stop walking at `object` or any base class without `[PatchDocument]`

## 2. Generated Code for Inheritance

- [ ] 2.1 Emit `new` modifier on `_providedProperties`, `WasProvided`, `ProvidedProperties`, `Provided`, and `ProvidedSet` when the class has a `[PatchDocument]` base class, to avoid CS0108 warnings
- [ ] 2.2 Skip re-declaring `: IPatchDocument` on derived classes whose base already implements it
- [ ] 2.3 Make the `required`-members sentinel constructor `protected` instead of `private` when the class is not sealed, so derived classes can call it
- [ ] 2.4 Use `nameof(ClassName.PropertyName)` with the correct class context for inherited properties in the generated converter

## 3. Buffered Path Integration

- [ ] 3.1 Ensure inherited settable properties from base classes are handled correctly in the buffered codegen path alongside derived init-only properties
- [ ] 3.2 Validate that `[JsonConstructor]` parameter matching only considers the derived class's constructor (not base class constructors)

## 4. Tests

- [ ] 4.1 Test: single-level inheritance — derived class tracks both base and own properties
- [ ] 4.2 Test: multi-level inheritance (3 levels) — all properties from all levels tracked
- [ ] 4.3 Test: only base properties provided in JSON — derived properties not marked as provided
- [ ] 4.4 Test: base class deserialized independently still works
- [ ] 4.5 Test: non-`[PatchDocument]` base class properties are ignored
- [ ] 4.6 Test: property shadowing with `new` — only derived property tracked, single entry in `ProvidedProperties`
- [ ] 4.7 Test: inheritance with buffered deserialization — base settable properties + derived init-only properties

## 5. Documentation

- [ ] 5.1 Update `README.md` to document inheritance support
- [ ] 5.2 Update `CHANGELOG.md` under `[Unreleased]`
