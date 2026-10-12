---
name: app-backend-data
description: "Implement backend, authentication, persistence and domain validation in existing owners."
---

# Backend and data

## Purpose
Implement backend, authentication, persistence and domain validation in existing owners.

## Trigger
Route/service/storage work, auth, database, uploads or realtime server behavior.

## Pre-check
Inspect active server entry, route middleware, package lock, schema/migrations, database identity and authorization source. Confirm whether data is fixture or live.

## Workflow
Update the authoritative domain/service then route and contract consumers. Validate inputs and role/ownership before mutations. Use transactions for multi-write operations, parameterized SQL and bounded uploads. For realtime, document ordering/reconnect/deduplication and pause work when hidden. Version migrations and design restore checks before live data change.

## Safety Rules
Never substitute a demo database or seed stock/users to make tests pass. Do not run mutation tests against live data. Durable storage must be demonstrated, not inferred from a successful deploy.

## Verification
Use isolated database tests for success, validation, unauthorized/forbidden, concurrent/idempotent behavior and rollback. Verify selected API consumer contracts.

## Failure Handling
Separate dependency/startup, schema, auth and domain errors. Preserve data; migration rollback does not automatically restore mutated data.

## Output
Changed owners, contract/schema impact, fixture isolation, test evidence and live rollout dependencies.
