# Deployment record

Repository (private): https://github.com/tindepzai12-hash/warehouse-ai

Website: https://warehouse-ai-fmjc.onrender.com

Render Dashboard: https://dashboard.render.com/web/srv-dapi78navr4c738ooqu0

- Workspace: Bảo Tín's workspace
- Service ID: srv-dapi78navr4c738ooqu0
- Region: Singapore
- Instance plan: Free
- Branch: main; auto-deploy on commit enabled
- Build: `pip install -r requirements.txt`
- Start: `python run.py`
- Runtime: Python 3.12.14
- Config: server-side Render environment, AI_MODE=openai; no secrets committed
- Prototype auth: same local demo token, verified by its SHA-256 on Render; cloud identity render-demo, staff role
- SQLite is ephemeral. This deployment does not satisfy durable production storage requirements. Session/quota/retry cache can reset on redeploy/restart/spin-down. Do not use for real inventory transactions.

Update code locally, run tests, commit and push to main. Render automatically builds a new version. See UPDATING.md for the workflow and storage constraints. Keys are changed in Render Environment, never in source files.
