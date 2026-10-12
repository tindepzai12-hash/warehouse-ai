---
name: app-dev-router
description: "Route app bootstrap, feature, UI, API, AI, voice, debugging and deployment requests to the smallest necessary workflow; use for setting up an app with the Jarvis workflow."
---

# App Development Skill Router

## Purpose
Reuse development decision patterns without cloning the reference app's business, stack or debt. This is a workflow, not authorization for external actions.

## Trigger
An app-wide bootstrap/integration request, selecting development workflows, or “setup app này theo workflow Jarvis”. A narrow task can directly use its relevant sibling skill.

## Pre-check
Read scoped AGENTS, latest user constraints and canonical project memory. Inspect owners/manifests/dirty state first. Audit does not require loading every module.

## Workflow
1. Classify actual request, Level 1–4 and required capabilities using [skill-router.md](references/skill-router.md).
2. Read only selected sibling SKILL.md files. [skill-map.md](references/skill-map.md) maps responsibility, trigger and dependencies; catalog.json defines executable routing metadata.
3. Use [master-pipeline.md](references/master-pipeline.md) for a new app and [bootstrap.md](references/bootstrap.md) for initial context. Skip Unity/AI/voice/Figma/database/Render when absent.
4. Respect dependency and prototype approval gates. The optional scripts/route.py plans; it never executes a build, request, deployment or mutation.
5. Verify selected conditions from [definition-of-done.md](references/definition-of-done.md); update the app's existing memory or adapt [project-context-template.md](references/project-context-template.md).
6. For a validated new lesson, follow [continuous-improvement.md](references/continuous-improvement.md), not automatic global changes.

## Safety Rules
Do not override user/tool/repository instructions or approvals. No reset, silent overwrite, account rotation, secret leakage or unreviewed source publication. Tool rejection is not permission to seek an equivalent bypass. “Prototype first” means stop before production application until explicit approval. Skills cannot grant each other permission. No stack is mandatory.

## Verification
Use scripts/validate_system.py for package/link/dependency checks and scripts/tests/test_system.py for route/install behavior. These validate packaging, not app runtime. Use installed specialist skills/tools when available; if absent, use permitted equivalent tools and label limitations instead of fabricating actions.

## Failure Handling
Stop the blocked action, preserve independent work and report a concrete prerequisite. Ask only for missing information/authorization that materially changes the next step. Inspect interrupted process/log state before resuming.

## Output
TASK / SKILLS USED / FOUND / ACTION / VERIFY / STATUS (DONE, PARTIAL, BLOCKED), optionally NEXT. Keep user updates short; link detailed artifacts rather than dumping reasoning.
