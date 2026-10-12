---
name: app-render-deploy
description: "Deploy a reviewed app to an existing or authorized Render service with health and route verification."
---

# Render deployment

## Purpose
Deploy a reviewed app to an existing or authorized Render service with health and route verification.

## Trigger
An explicitly requested Render release/configuration or Render deployment failure.

## Pre-check
Inspect current service/repository/branch/root/runtime/build/start/health configuration and provider tooling. Resolve actual environment names, bind/PORT requirements, persistence, auto-deploy triggers and costs. Read current official Render docs at execution.

## Workflow
Detect static vs backend workload before choosing build/start commands. Verify dependencies/lockfile locally and intended release diff; configure server secrets using secure provider tools. For SQLite, establish durable storage/single-writer constraints or label ephemeral demo limitations; do not upgrade resources without approval. Deploy only authorized source/service. Correlate deploy ID/commit → logs → health → auth/real route contract. Require explicit approval for a billable provider smoke test.

## Safety Rules
No unreviewed bulk push, source/data/credential upload, new paid plan/service without authorization, or demo DB substituted for existing live storage.

## Verification
Successful deploy status plus HTTPS health/auth/API capability evidence on that exact release. Health alone does not prove AI, durable data or a newly added route.

## Failure Handling
Follow logs: dependency/build → start/bind/PORT → env → schema/storage → auth/route → provider. Use code rollback only with database compatibility; do not redeploy unchanged code in loops.

## Output
Service/revision/deploy evidence, safe config names, persistence/cost caveats, route checks and rollback/release blockers.
