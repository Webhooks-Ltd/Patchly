---
id: idea-patchly-automapper
title: AutoMapper integration for patch application
status: icebox
priority: p3
impact: medium
effort: high
confidence: low
owner: unassigned
created: 2026-02-28
updated: 2026-02-28
related_specs:
  - patch-mapping
related_changes: []
---

## Problem

Teams using AutoMapper may want first-class integration patterns when applying patch documents to domain entities.

## User / Segment

ASP.NET teams already standardized on AutoMapper.

## Proposed Approach

Define an optional integration package or extension pattern that maps `Provided`/state semantics safely.

## Evidence

Idea exists from prior change stub; no formal demand data captured yet.

## Assumptions

AutoMapper users are a meaningful segment of Patchly adopters.

## Risks / Unknowns

- Could increase maintenance burden.
- Mapping semantics can be domain-specific.
- Potential overlap with existing patch map abstraction.

## Success Metric

Adoption of integration API/package and reduced custom mapping boilerplate.

## Next Decision

Collect demand signals from issues/discussions before promotion.
