---
name: app-feature-delivery
description: "Deliver one app feature through existing owners, contracts, integration and scoped acceptance."
---

# Feature delivery

## Purpose
Deliver one app feature through existing owners, contracts, integration and scoped acceptance.

## Trigger
A new or partially implemented feature, not a trivial copy edit or pure audit.

## Pre-check
Find existing partial behavior; define user-visible acceptance, impact boundary, dependencies, input/output and error states. Use architecture only when a new boundary/migration requires it.

## Workflow
Understand → audit owner → design contract/states → check dependencies → implement smallest slice → integrate into real navigation/session → test affected and adjacent paths → verify applicable DoD. If prototype-first, pause production work at approval. Add modules only as required by actual capabilities; skip irrelevant providers/stacks.

## Safety Rules
No parallel replacement subsystem, invented credentials, unapproved architecture expansion or implementing app functionality during a workflow-only packaging task.

## Verification
Feature must be reachable, perform the intended action and handle errors; compile plus a rendered fake control is insufficient. Record real vs fixture boundary.

## Failure Handling
If a dependency is absent, finish independent preparation and stop dependent work with a precise missing prerequisite; preserve drafts/data.

## Output
Acceptance checklist, selected modules/order, integrated owner changes, test evidence and remaining dependency.
