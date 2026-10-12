---
name: app-root-cause-debug
description: "Reproduce and trace app failures across UI, API, storage and providers before a minimal fix."
---

# Root-cause debugging

## Purpose
Reproduce and trace app failures across UI, API, storage and providers before a minimal fix.

## Trigger
A reported failure, regression, broken integration or discrepancy between tests and actual use.

## Pre-check
Capture expected vs actual behavior, exact action/environment/revision, reproducibility and redacted error evidence. Inspect interrupted tools and ongoing processes before retrying.

## Workflow
Classify UI/input/lifecycle → client payload/HTTP → server auth/contract → storage/provider → response/output. Reproduce at the smallest relevant layer; compare known-good current evidence, not old PASS files. Form one supported hypothesis and test it. Patch the actual owner minimally, then add a regression case if a test framework exists.

## Safety Rules
Do not change many unrelated files as experiments, bypass policy, upload source, reset work or repeatedly execute billable requests. Do not claim causality from absence of logs.

## Verification
Prove original failure no longer occurs and related old behavior still works. Separate reproducible fixture evidence from user hardware/live-provider evidence.

## Failure Handling
If reproduction is blocked, record observed facts, hypotheses and the precise missing input. A script added for diagnostics is not a fixed feature.

## Output
Reproduction, confirmed cause vs hypothesis, smallest patch, regression results and blockers.
