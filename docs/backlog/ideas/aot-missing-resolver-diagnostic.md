---
id: idea-aot-missing-resolver-diagnostic
title: Diagnostic for missing AOT resolver configuration
status: icebox
priority: p2
impact: high
effort: medium
confidence: medium
owner: unassigned
created: 2026-02-28
updated: 2026-02-28
related_specs:
  - native-aot-support
  - openapi-integration
related_changes: []
---

## Problem

In AOT or resolver-based setups, missing `PatchlyJsonTypeInfoResolver` configuration can lead to runtime failures or unexpected behavior that is hard to diagnose.

## User / Segment

Teams publishing Native AOT apps or manually composing `JsonSerializerOptions` resolvers.

## Proposed Approach

Add compile-time or runtime diagnostic guidance when Patchly types are used without required resolver wiring.

## Evidence

Prior idea stub exists; current evidence is mainly inferred from known AOT configuration pitfalls.

## Assumptions

Misconfiguration frequency is high enough to justify additional diagnostics.

## Risks / Unknowns

- Hard to detect all misconfigurations safely.
- False positives could annoy users.

## Success Metric

Reduced support/debug incidents related to AOT resolver misconfiguration.

## Next Decision

Gather examples from user reports before promoting to `triage`.
