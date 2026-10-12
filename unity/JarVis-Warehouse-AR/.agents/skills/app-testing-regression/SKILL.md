---
name: app-testing-regression
description: "Choose scoped fresh tests and regression evidence appropriate to the changed boundary."
---

# Testing and regression

## Purpose
Choose scoped fresh tests and regression evidence appropriate to the changed boundary.

## Trigger
Code/contract/UI change requiring validation, new tests or test failures.

## Pre-check
Map impacted owners/contracts/routes and existing test commands/frameworks. Record runtime version, revision/dirty state, test isolation and prior evidence dates.

## Workflow
Start with changed owner tests, expand to adjacent contracts/navigation, then relevant build/runtime. Exercise error/cancel/empty/auth/concurrency states. Use temporary DBs/providers, never production mutation. Correlate run ID/start time/output freshness/exit code and artifact hashes. A changed UI needs render inspection alongside geometry checks. Keep unrelated failures separate.

## Safety Rules
No stale PASS reuse, tests that only grep success wording, silent expected-error filtering, mock data claimed live or adding a test framework without scope.

## Verification
Produce layer-specific PASS/FAIL/SKIPPED/BLOCKED evidence tied to current inputs. For new scripts, test real outcomes such as ordering, collision refusal and preservation, not prose wording alone.

## Failure Handling
Investigate relevant failures; leave unrelated defects outside scope and document them. Unknown process IDs after interruption require log/process inspection, not blind repeated polls.

## Output
Commands, fresh results/artifacts, regression scope, isolated/live distinction and untested conditions.
