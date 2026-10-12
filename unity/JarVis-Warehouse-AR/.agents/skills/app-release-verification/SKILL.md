---
name: app-release-verification
description: "Determine task completion and release readiness from current evidence, not code or build alone."
---

# Completion and release

## Purpose
Determine task completion and release readiness from current evidence, not code or build alone.

## Trigger
Final task handoff, artifact build/install or authorized production release.

## Pre-check
Read task scope/approval gates and selected Definition of Done. Collect latest tests, console errors, feature reachability, contract/device evidence, artifact revision and remaining risks.

## Workflow
For small edits, require scoped compile/render/behavior evidence; do not force deployment. For a release, check reviewed files, config/auth, data retention/backup, fresh build, signature/permissions/platform, release/service revision and rollback. Mark non-applicable conditions explicitly. End with DONE/PARTIAL/BLOCKED and concrete residual work.

## Safety Rules
No declaring production-ready from compiled code, fixture success, screenshots or health alone. A previous APK does not contain later source edits. Never claim publishing/installation that was not performed.

## Verification
Compare requirement-by-requirement with actual dated evidence and current artifact hash. Required checks may be skipped only if truly not applicable, not because unavailable.

## Failure Handling
Fail closed on mandatory missing proof. Preserve useful artifacts and label them accurately; do not spend/run external mutation just to turn a report green.

## Output
TASK / SKILLS USED / FOUND / ACTION / VERIFY / STATUS; add NEXT and artifact path/revision when relevant.
