---
id: idea-buffered-path-schema-transformer
title: Schema transformer support for buffered-path patch types
status: icebox
priority: p2
impact: medium
effort: medium
confidence: low
owner: unassigned
created: 2026-02-28
updated: 2026-02-28
related_specs:
  - openapi-integration
  - openapi-compatibility
related_changes: []
---

## Problem

Buffered-path patch types can have weaker schema export behavior in some OpenAPI toolchains.

## User / Segment

Teams using OpenAPI generation with init-only or `[JsonConstructor]` patch models.

## Proposed Approach

Investigate a schema transformer/resolver strategy that improves schema quality for buffered-path models.

## Evidence

Prior idea stub exists; no validated implementation path documented.

## Assumptions

A transformer can improve schema fidelity without breaking runtime converter behavior.

## Risks / Unknowns

- Toolchain/version-specific behavior may limit portability.
- Could increase complexity in service registration.

## Success Metric

Buffered-path patch types produce complete and usable schemas in supported frameworks.

## Next Decision

Run a focused technical spike before promotion.
