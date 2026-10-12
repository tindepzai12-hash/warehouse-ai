---
name: app-project-audit
description: "Audit an existing or new app repository before implementation or integration."
---

# Project discovery

## Purpose
Audit an existing or new app repository before implementation or integration.

## Trigger
Existing code, unknown stack, inherited changes, new app bootstrap.

## Pre-check
Read scoped AGENTS and project memory; inspect cwd, repository root, branch, dirty/untracked files, manifests, entrypoints and applicable child instructions. Read only relevant source owners and dated evidence; never dump secrets.

## Workflow
Map user action → UI/controller → client → route → storage/provider. Distinguish implemented, reachable, tested, deployed and device-verified. Identify a partial existing module before proposing another. Classify scope Level 1–4 and list mandatory missing prerequisites. For an empty repository, record unknown product choices rather than cloning the reference app.

## Safety Rules
Do not reset, overwrite, seed a database, rebuild a scene, or treat stale reports as current runtime truth. Read-only audit does not authorize mutations.

## Verification
Produce an owner map backed by file references and a baseline with current revision/dirty-state notes. Resolve competing sources by newest explicit user decision; mark unresolved conflicts.

## Failure Handling
If access or evidence is missing, narrow the audit and label unknowns. Stop dependent implementation until a mandatory prerequisite is established.

## Output
Stack, owners, reusable vs business-specific facts, impacted surfaces, missing prerequisites and the smallest next route.
