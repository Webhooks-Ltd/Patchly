## 1. Interface Change

- [ ] 1.1 Add `GetProvidedValues()` method to `IPatchDocument` interface with XML doc comment
- [ ] 1.2 Add `using System.Collections.Generic` if needed for `IReadOnlyDictionary`

## 2. Generator Implementation

- [ ] 2.1 Emit `GetProvidedValues()` method body in `GenerateSource` that iterates tracked properties and builds a dictionary
- [ ] 2.2 Ensure the method only includes properties present in `_providedProperties` HashSet
- [ ] 2.3 Verify generated method works with both streaming and buffered codegen paths

## 3. Tests

- [ ] 3.1 Test: returns only provided properties with correct values
- [ ] 3.2 Test: returns empty dictionary for empty JSON `{}`
- [ ] 3.3 Test: includes properties explicitly set to null
- [ ] 3.4 Test: keys match `ProvidedProperties` exactly
- [ ] 3.5 Test: fresh instance returns empty dictionary
- [ ] 3.6 Test: each call returns a fresh dictionary instance
- [ ] 3.7 Test: `[JsonIgnore]` properties excluded
- [ ] 3.8 Test: works with init-only properties (buffered path)
- [ ] 3.9 Test: keys use C# property names, not JSON wire names

## 4. Documentation

- [ ] 4.1 Update `README.md` to document `GetProvidedValues()`
- [ ] 4.2 Update `CHANGELOG.md` under `[Unreleased]`
