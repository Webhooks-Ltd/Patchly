## 1. Public API and Attribute Surface

- [x] 1.1 Add `PatchValueState` enum (`Omitted`, `Null`, `Value`) in core library
- [x] 1.2 Add `PatchSemanticsMode` enum with `Legacy` and `DeterministicV1`
- [x] 1.3 Extend `PatchDocumentAttribute` with `SemanticsMode` configuration (default `Legacy`)
- [x] 1.4 Extend `IPatchDocument` with `GetState(string propertyName)` and XML docs

## 2. Source Generator Implementation

- [x] 2.1 Extend generator model to capture semantics mode for each patch document
- [x] 2.2 Emit deterministic `State` accessor and nested state set structure for tracked properties
- [x] 2.3 Emit `GetState(string propertyName)` implementation in generated partial class
- [x] 2.4 Keep implementation reflection-free and compatible with trimming/AOT
- [x] 2.5 Ensure deterministic state logic is consistent for streaming and buffered paths

## 3. Diagnostics and Guardrails

- [x] 3.1 Add deterministic-mode warning for non-nullable collection properties
- [x] 3.2 Update relevant diagnostic messaging to clarify deterministic mode constraints
- [x] 3.3 Add tests verifying guardrail warnings are emitted only in deterministic mode

## 4. Behavioral Test Coverage

- [x] 4.1 Add scalar tri-state tests for omitted, explicit null, and explicit value
- [x] 4.2 Add `GetState` tests for case-insensitive and unknown property names
- [x] 4.3 Add nested patch tests for omitted/no-op, null/clear, and value/partial update intent
- [x] 4.4 Add collection tests for omitted/no-op, null/clear, empty replace, and non-empty replace
- [x] 4.5 Add duplicate JSON property tests to verify final state follows last-value semantics
- [x] 4.6 Add parity tests proving equivalent behavior in streaming and buffered converter paths

## 5. Documentation and Release Notes

- [x] 5.1 Update `README.md` with deterministic mode usage and tri-state examples
- [x] 5.2 Document deterministic collection replace semantics and migration guidance
- [x] 5.3 Update `CHANGELOG.md` under `[Unreleased]`
