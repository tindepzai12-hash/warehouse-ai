# Codex Handoff Prompt - warehouse-ai

> Historical handoff notes: this file contains earlier mock/Python-era records. For the current source of truth, start with [RUNBOOK_VI.md](RUNBOOK_VI.md), then inspect `server.js`, `render.yaml` and tests. Do not reintroduce `AI_MODE` or mock mode from sections below.

## Role

You are Codex working inside the existing `E:\warehouse-ai` repository. Continue from the current repository state. Do not recreate the project from scratch. Read the actual files before changing behavior, and preserve user changes.

## Important security rule

Never print, commit, copy, or include the contents of `.env`, `.local-token`, `data/`, API keys, bearer tokens, or database rows containing user/session data. Refer to secret names only. The repository intentionally ignores `.env`, `.local-token*`, `data/`, `node_modules/`, and generated logs.

## Current objective

Maintain and finish the migration of the Warehouse AI backend from Python/FastAPI to Node.js/Express for Render deployment. The current runtime must be Node.js, not Python. Preserve the existing frontend in `static/` unless a user explicitly requests frontend changes.

## Repository state

- Repository root: `E:\warehouse-ai`
- Branch: `main`
- Recent base commit: `67677bb Migrate warehouse service to Node with SQLite initialization [skip render]`
- Recent worktree changes include `server.js`, `render.yaml`, and `docs/UPDATING.md`; inspect `git status` before editing.
- The old Python backend files and Python dependency files were removed during migration.
- Do not revert unrelated user changes.

## Runtime and commands

- Node.js: 18 or newer.
- Install: `npm install`
- Start: `npm start`
- Start script: `node server.js`
- Provision token: `npm run create-user -- create-user --user worker-demo --token-file .local-token`
- Syntax check: `node --check server.js`
- Frontend tests, when Node is available: `node --test tests/*.test.cjs`
- Local URL: `http://localhost:8000`
- Render supplies `PORT`; the server binds `0.0.0.0`.

## Package configuration

`package.json` currently defines:

- `express`
- `dotenv`
- `sqlite3`
- `cors`
- `body-parser`
- `npm start` -> `node server.js`
- `npm run create-user` -> `node provision.js`
- Node engine `>=18`

## Main files

- `server.js`: Express server, environment settings, SQLite store, authentication, quota, sessions, RAG, mock provider, OpenAI Responses provider, answer validation, static serving.
- `provision.js`: creates/rotates a prototype user token and stores only its SHA-256 hash.
- `package.json`: Node dependencies and scripts.
- `render.yaml`: Render Blueprint for the existing Node web service named `warehouse-ai`.
- `.env.example`: local environment template. Never copy real secrets into documentation.
- `knowledge/reviewed.json`: approved runtime corpus used by RAG.
- `knowledge/unverified.json`: provenance/test data that must not be retrieved or sent to the model.
- `static/index.html`: browser UI.
- `static/app.js`: browser API client and chat UI.
- `static/style.css`, `static/jarvis.js`, `static/voice.js`, `static/mic-check.js`, `static/assets/*`: frontend styling, avatar, speech and microphone diagnostics. Do not modify these for a backend task.
- `docs/WAREHOUSE_AI_CODEX_SPEC.md`: broader product specification. Milestone A is the current implemented scope; do not claim Milestone B/D functionality exists.
- `docs/ROADMAP.md`: scope boundaries for later warehouse transactions and Unity/AR.
- `docs/RENDER.md`: Node deployment instructions.

## Environment contract

Expected names from `.env.example`:

```dotenv
AI_MODE=mock
OPENAI_API_KEY=
OPENAI_MODEL=
DATABASE_PATH=data/warehouse.sqlite3
MAX_REQUESTS_PER_MINUTE=20
MAX_REQUESTS_PER_DAY=100
MAX_OUTPUT_TOKENS=1200
REQUEST_TIMEOUT_SECONDS=30
CHINESE_LOCALE=
PORT=8000
PROTOTYPE_TOKEN_SHA256=
```

`server.js` uses `process.env.OPENAI_MODEL || 'gpt-4o-mini'` as the model fallback. For OpenAI mode, `OPENAI_API_KEY` and a model are required. Use `AI_MODE=mock` for a no-cost deployment smoke test. Use `AI_MODE=openai` only after setting a valid server-side key and confirming model access.

Never put an API key in `static/`, source control, chat output, screenshots, or client-side code.

## Render contract

`render.yaml` must remain equivalent to:

```yaml
services:
  - type: web
    name: warehouse-ai
    runtime: node
    region: singapore
    plan: free
    branch: main
    buildCommand: npm install
    startCommand: npm start
    healthCheckPath: /api/health
    autoDeployTrigger: commit
```

The current Blueprint environment is intentionally `AI_MODE=mock` so a missing OpenAI key cannot crash boot. It sets `OPENAI_MODEL=gpt-4o-mini`, quota limits, output token limit, timeout, and SQLite path. Render's existing service may have old environment values; Blueprint/source changes do not automatically erase manually configured service variables. If logs say `Startup failed: Set OPENAI_API_KEY and OPENAI_MODEL in server environment`, inspect the deployed service Environment tab: either set `AI_MODE=mock` for smoke testing, or add a valid `OPENAI_API_KEY` and set `AI_MODE=openai`.

The deployment target is the existing Node service:

- Name: `warehouse-ai`
- Service ID: `srv-dapi78navr4c738ooqu0`
- Public URL: `https://warehouse-ai-fmjc.onrender.com`

Do not deploy to the discarded `warehouse-ai-1` service. Do not claim a deployment succeeded based only on build success. Check runtime logs, `/api/health`, and the public page.

Render setup:

- Environment: Node
- Build: `npm install`
- Start: `npm start`
- Health: `/api/health`
- One instance for SQLite
- For persistence, mount a Persistent Disk at `/var/data` and set `DATABASE_PATH=/var/data/warehouse.sqlite3`; without it, SQLite is ephemeral.

## Static frontend contract

`server.js` must contain:

```js
const path = require('path');

app.get('/', (req, res) => {
  res.sendFile(path.join(__dirname, 'static', 'index.html'));
});

app.use('/static', express.static(path.join(__dirname, 'static')));
```

The frontend calls relative `/api/health` and `/api/feedback` URLs. It expects `sourceDetails`, `message`, `action`, `sessionId`, `mode`, `latencyMs`, and `contextTurns` in successful feedback responses. Verify these URLs with HTTP requests after changes:

- `/`
- `/static/style.css`
- `/static/app.js`
- `/static/assets/jarvis-welcome.jpg`
- `/api/health`

## API contract

### `GET /api/health`

Returns service/database status, mode, milestone, synthetic flag, and live provider verification status. It does not prove that an OpenAI request succeeds.

### `GET /api/me`

Requires `Authorization: Bearer <prototype-token>`. Returns user id, role, and assigned tasks. Authentication hashes the presented token with SHA-256 and compares it with SQLite.

### `GET /api/knowledge/:documentId/:sectionId`

Requires Bearer authentication. Returns a matching chunk from `knowledge/reviewed.json`, otherwise `404 Knowledge source not found`.

### `POST /api/feedback`

Requires Bearer authentication. Request validation preserves the previous Python contract:

- UUID `requestId` is required.
- Optional UUID `sessionId`.
- `taskId` matches `TEST-[A-Z]+-NNN` and assigned tasks are enforced.
- `language`: `en`, `vi`, or `zh`.
- `locale` must match language when provided.
- Supported event types include user question, scan success/failure, shelf mismatch, route deviation, completion, and summary request.
- Question is trimmed and 1-2000 characters.
- Observation only allows `reportedVisibleQuantity`, `scanCode`, and `scanFailureCount` with bounded values.
- Unknown request fields return `422`.

Expected behavior includes:

- `401`: missing/invalid Bearer token.
- `403`: task is not assigned.
- `404`: foreign/missing session or knowledge source.
- `409`: request id reused with different payload, session busy/task changed, or inflight duplicate.
- `429`: per-minute or daily prototype budget reached.
- `502`: provider access/request/structured output/citation/action failure.
- `503`: provider quota/rate limit.
- `504`: provider timeout.
- `413`: body exceeds 16 KiB.

SQLite tables include `users`, `sessions`, `replies`, `usage`, and `inflight`. Successful responses are cached by `(user_id, request_id)` and fingerprinted for safe retry. Session history is limited to the latest 20 messages. Warehouse facts are synthetic and read-only in Milestone A.

## RAG and answer policy

- Load only `knowledge/reviewed.json`.
- Reject corpus chunks that are not `source_status=spec_confirmed_project_policy` and `runtime_approved=true`.
- Use lexical retrieval and previous topic follow-up behavior.
- In OpenAI mode, send the full reviewed corpus in the selected language after ranking, without sending unverified data.
- Keep source citation pairs tied to retrieved document/section IDs.
- Never expose blind-count expected quantities.
- Never claim inventory mutation, disposal, allocation, route clearance, or completed transaction.
- Treat worker reports as reports, not verified WMS facts.
- Do not fabricate shelves, quantities, routes, causes, identities, or safety limits.

## OpenAI Responses contract

`providerAnswer()` calls `POST https://api.openai.com/v1/responses` with:

- `model: settings.model`
- `store: false`
- `max_output_tokens: settings.maxTokens`
- server-side `Authorization: Bearer ...`
- `text.format.type: 'json_schema'`
- `text.format.name: 'warehouse_feedback'`
- `text.format.strict: true`

The schema must keep `additionalProperties: false` on object schemas and list every property in `required`, including nested `action` fields and source citation fields. Keep enums and nullable fields valid for strict structured outputs. Do not replace the Responses API payload with Chat Completions without updating tests and the contract.

## Special user rule currently implemented

`server.js` currently has a deterministic branch before mock/OpenAI retrieval:

- If the question contains `Quỳnh Anh` or normalized `Quynh Anh`, return the configured Vietnamese sentence about Phạm Lê Quỳnh Anh.
- It returns a valid feedback object with no sources, `action.type=none`, no clarification, and does not call OpenAI.
- Preserve this behavior only if it remains an explicitly authorized user requirement. Do not leak secrets or introduce unrelated personal data.

## Known incident and resolution

1. Frontend initially rendered unstyled because static middleware lacked the `/static` prefix. Fixed with `app.use('/static', express.static(path.join(__dirname, 'static')))` and an explicit root `sendFile` route.
2. Render originally displayed Python 3 because the old service was Python. Added Node Blueprint configuration. A new Node deploy then failed at boot because the service environment still had `AI_MODE=openai` without a valid key. Set Blueprint default to `AI_MODE=mock`; service-level environment variables may still need manual correction.
3. Local browser initially hit an old Node process. Restarting with the main `data/warehouse.sqlite3` and `AI_MODE=mock` fixed stale behavior. Verify the actual listener and restart after code changes.
4. PowerShell can mangle Vietnamese characters in ad-hoc requests. Use UTF-8 request bodies or Node fetch for tests with Vietnamese text.
5. The current local server may be running as a detached Node process on port 8000. Check before starting another process.

## Verification checklist for Codex

1. Read `git status`; do not discard existing edits.
2. Run `node --check server.js`.
3. Confirm `package.json`, `render.yaml`, `server.js`, `.env.example`, and docs agree on Node commands.
4. Start a temporary local server with a temporary database and `AI_MODE=mock`.
5. Verify `/`, `/static/style.css`, `/static/app.js`, `/static/assets/jarvis-welcome.jpg`, and `/api/health` return `200`.
6. Provision a temporary user and verify `/api/me` and one `/api/feedback` request.
7. Verify `Quỳnh Anh là ai?` returns the deterministic authorized response if that requirement is still active.
8. Verify invalid auth, unassigned task, request-id conflict, and body-size handling.
9. Do not call live OpenAI or spend money unless the user explicitly asks and provides a valid server-side configuration.
10. Before final response, state exactly which files changed, which commands passed, and whether Render was actually deployed. Never claim Render success from local checks.

## Recommended next actions

- Commit and push current `server.js`, `render.yaml`, `docs/UPDATING.md`, and this handoff file.
- In Render, update `warehouse-ai` (`srv-dapi78navr4c738ooqu0`) and do not create another duplicate service.
- Keep `AI_MODE=mock` until `/api/health` and the public UI are live.
- Add `OPENAI_API_KEY` only in Render Environment when switching to `AI_MODE=openai`.
- Recheck deploy logs and public `/api/health` after each deployment.
- Treat Milestone A as a prototype; do not claim real WMS transactions, persistent production storage, Unity, AR, or production security are complete.
