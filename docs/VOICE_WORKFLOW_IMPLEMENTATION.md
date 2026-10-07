# Unified app voice workflow — 2026-10-07

Current route: `POST /api/oms/mobile/jarvis/voice`, authenticated multipart field `audio` / `question.wav` / `audio/wav`. Returns `success`, `transcript`, `reply` and existing JarVis metadata. Uses the same server-owned history, language selection, warehouse facts, provider and mobile session as `/jarvis/chat`.

Run `npm install` for Busboy; run `npm run verify`. The current complete verification is **53/53 passed** in `logs/voice-bilingual-verify.log`. Voice workflow tests use injected providers and temporary databases; they are not hardware or acoustic-accuracy evidence.

Upload is auth-first, in memory, one file/no fields; validates extension/MIME plus actual RIFF PCM16 mono bytes, silence, size and duration. Request cancellation propagates to STT/Responses; a session cannot run concurrent text and voice turns. Errors have stable sanitized codes. No automatic retry or warehouse mutation.

Full contract, architecture, changed file list, env/Inspector setup, test limitations and security review: `E:/unity of teep/JarVis-Warehouse-AR/Docs/VOICE_WORKFLOW_IMPLEMENTATION.md`.

## Render release readiness

Owner-confirmed workspace **Bảo Tín's workspace**, service **warehouse-ai**, `srv-dapi78navr4c738ooqu0`, https://warehouse-ai-fmjc.onrender.com. Verified health 200 and mobile/login 404 on 2026-10-07. Latest live source `893edb4…` does not contain local OMS mobile routes. A redeploy of that commit will still lack voice/login.

Authorized release dependency set: current `server.js`, `package.json`/lock; `oms/{store.js,schema.sql,routes.js,presence.js,jarvis-local.js,chat-media.js,voice-upload.js}`; referenced existing knowledge files; OMS web bundle and relevant backend tests. The working tree contains earlier unpublished OMS/RAG/UI work, so avoid `git add .` or an unreviewed bulk push. `scripts/prepare-cloud-admin.cjs` creates a separate random cloud password in ignored `data/oms-cloud-admin-credentials.txt`; only its scrypt hash goes to `OMS_BOOTSTRAP_ADMIN_HASH`. Opt-in startup provisions that admin without demo stock, never replacing an existing administrator. `OMS_SEED_DEMO=false`; local accounts/database are not uploaded.

Do not upload `.env`, `data/`, account files or `.local-token`. Free-service SQLite is ephemeral: warehouse data, employees and history may be lost on restart/redeploy. The opt-in hash restores only the initial admin, not warehouse data. Keep the existing Free service, with no paid resource or plan upgrade. OpenAI calls during actual voice/chat use remain billable. Cloud smoke checks do not call billable models. Publication/login status is recorded after the deployment is live. Unity builds bundle the public HTTPS endpoint; explicit local/device overrides take precedence and all mobile functions share one endpoint/session.

## Bilingual recognition and limits

Default `OPENAI_TRANSCRIPTION_MODEL=gpt-4o-mini-transcribe`; configurable on the server. English/Vietnamese/mixed speech uses automatic detection without forcing the UI language. A short terminology prompt includes JarVis, Justin, SKU, QR, 5S, FIFO, FEFO, QC and Vietnamese warehouse vocabulary. Returned words/accents are preserved, without translation, rewriting or guessing missing words. Access/configuration failures are visible; no silent mock fallback or automatic billable retry.

The official OpenAI `transcribe` skill was installed locally to assist implementation/evaluation; it is not a Unity runtime dependency. References: [model](https://developers.openai.com/api/docs/models/gpt-4o-mini-transcribe), [STT prompting](https://developers.openai.com/api/docs/guides/speech-to-text).

Unity live run 5 passed. Run 6 subsequently failed the Vietnamese context-recall assertion because Whisper transcribed the synthetic question incorrectly; all other transport/feed/layout assertions passed. The failing record is preserved at `E:/unity of teep/JarVis-Warehouse-AR/Logs/PrototypeParity/VoiceWorkflow-live-run6.txt`. The new recognition model has not been tested with billable calls, honoring the user's cost condition. Real human microphone accuracy, Android permission and audible playback still need device testing. A UI-only pass is not an ASR-quality pass.
