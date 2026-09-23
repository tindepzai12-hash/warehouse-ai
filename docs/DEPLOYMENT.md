# Deployment record

Repository (private): https://github.com/tindepzai12-hash/warehouse-ai

Website: https://warehouse-ai-fmjc.onrender.com

Render Dashboard: https://dashboard.render.com/web/srv-dapi78navr4c738ooqu0

- Workspace: Bảo Tín's workspace
- Service ID: srv-dapi78navr4c738ooqu0
- Region: Singapore
- Instance plan: Free
- Branch: main; auto-deploy on commit enabled
- Build: `npm install`
- Start: `npm start`
- Runtime: Node 18+
- Config: server-side Render environment, AI_MODE=openai; no secrets committed
- Prototype auth: same local demo token, verified by its SHA-256 on Render; cloud identity render-demo, staff role
- SQLite is ephemeral. This deployment does not satisfy durable production storage requirements. Session/quota/retry cache can reset on redeploy/restart/spin-down. Do not use for real inventory transactions.

Update code locally, run tests, commit and push to main. Render automatically builds a new version. See UPDATING.md for the workflow and storage constraints. Keys are changed in Render Environment, never in source files.

## Verification — 2026-09-23

- Deploy `dep-dapi8bp7lnhs73df8b1g`, commit `2ff1f5172455b08c5dd510d04d87c8dafc381b3c`: live; trigger `new_commit`, confirming auto-deploy works.
- HTTPS UI and health loaded; mode openai.
- Existing demo token authenticated as render-demo/staff; invalid token returned 401.
- Two user-approved paid OpenAI requests on Render succeeded: a natural Vietnamese five-required/three-present question and a no-replacement follow-up.
- Both cited SOP-SHORT-PICK/reporting; same session, contextTurns progressed 0 to 1.
- Source-grounded procedural answers observed; this is a smoke test, not a numerical accuracy evaluation or inventory transaction verification.
- Error-log query returned no error entries at verification time.
- Durable session storage across restart remains unimplemented on this Free deployment. No paid Render resources were created.
