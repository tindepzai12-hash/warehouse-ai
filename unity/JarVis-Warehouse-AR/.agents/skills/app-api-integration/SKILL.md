---
name: app-api-integration
description: "Connect client and backend with typed contracts, lifecycle-safe asynchronous requests and errors."
---

# API integration

## Purpose
Connect client and backend with typed contracts, lifecycle-safe asynchronous requests and errors.

## Trigger
New endpoint/client call, contract change, Unity/web ↔ service integration.

## Pre-check
Locate existing client, session, origin configuration, server route and DTOs. Check protocol compatibility, permissions, payload limits and retry semantics.

## Workflow
Specify request/response fields, status/error codes, auth, timeout and cancellation before writing the consumer. Reuse the shared session/origin. Render loading/empty/error/retry states without consuming drafts on failure. Dispose requests/buffers and ignore callbacks from old screens/sessions. Retry mutations only with an idempotency contract; preserve the same identity after uncertain delivery.

## Safety Rules
Do not add parallel unauthenticated clients or mock success in production. A phone's localhost is not the developer PC. Avoid automatic retry of billable or ambiguous mutations.

## Verification
Test the actual client against an isolated HTTP server, including malformed responses, expiry, timeout/cancel, lost successful response and contract version mismatch.

## Failure Handling
Trace one correlation/message ID across layers with redacted logs. Distinguish connection, auth, capability, parser and provider failures; retain user work.

## Output
Contract, owner reuse, lifecycle/retry policy, tested boundary and unverified live/device behavior.
