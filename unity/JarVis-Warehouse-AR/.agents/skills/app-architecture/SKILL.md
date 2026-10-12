---
name: app-architecture
description: "Plan app boundaries and migration impact for new systems or Level 4 changes."
---

# Architecture and impact

## Purpose
Plan app boundaries and migration impact for new systems or Level 4 changes.

## Trigger
New subsystem, conflicting owners, migration, data-flow or persistence change.

## Pre-check
Use the audit owner map; determine product truth, threat/data boundaries, existing contracts, data longevity and deployment constraints.

## Workflow
Draw client → API → domain/service → storage/provider boundaries; keep authorization on the server. Separate UI preferences from business state. Compare the smallest compatible option with a justified alternative. Define failure/timeout/cancellation ownership and optional capabilities. For migration, specify old/new readers, compatibility window, backup/restore, rollout gate and rollback limitations.

## Safety Rules
Do not restructure an entire repository for a small feature. Avoid duplicate authentication, chat history or API clients. Do not assume RAM or ephemeral disk is durable.

## Verification
Review dependencies and affected existing paths; add contract/migration tests where supported. A diagram is a proposal, not proof that runtime migrated.

## Failure Handling
If ownership is ambiguous, identify the unresolved decision and inspect its actual consumer. Stage a reversible slice rather than replacing everything.

## Output
Impact map, selected boundaries, rejected alternatives, migration/rollback plan and evidence needed.
