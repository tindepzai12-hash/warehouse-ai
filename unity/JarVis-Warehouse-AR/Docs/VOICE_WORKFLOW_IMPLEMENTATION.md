# JarVis voice workflow implementation — 2026-10-07

Latest voice-message redesign: see `VOICE_MESSAGE_SYSTEM.md`. Recording now uses an inline Cancel/Stop/waveform/timer/Send bar and optimistic playable audio bubbles, not transcript-only feed. This file preserves the earlier implementation/provider/deployment evidence; it is not final validation of the new flow.

## Audit and scoped plan

Unity 6000.4.7f1, native uGUI + TMP, entry `Assets/Scenes/JarVisFigmaApp.unity`, portrait 412 × 915 logical canvas. The imported `input-bar` already owns a HorizontalLayoutGroup; the existing CanvasScaler/viewport continues to scale the screen. There is no active MCP editor selection in batch QA. Runtime owner evidence is `ImportedFigmaNavigation.BindScreen` → `ImportedJarvisChat.Configure` → `ImportedChatMedia.Configure`; pre-change screenshot evidence is `Logs/PrototypeParity/Chat-icons-light-Vi.png`. Existing approved mascot, fonts and theme tokens remain in use. This is a bounded composer repair, not a scene import/rebuild.

Before implementation, the audit and file plan were communicated in chat:

| Owner | Existing behavior | Planned/resulting change |
| --- | --- | --- |
| ImportedChatMedia.cs | Mic → mono WAV → JSON transcription → editable draft; one-shot Listen | Explicit voice states, multipart turn, one feed, persistent Speaker ON/OFF, cancellation |
| ImportedJarvisChat.cs | Typed/photo chat and independent reply formatting | Shared reply formatting; compact flexible composer, preserve typed draft during voice |
| OmsMobileApi.cs | Authenticated chat/transcribe/speech APIs | Voice multipart API and explicit disposable voice/TTS request ownership |
| ChatActionIcon.cs | Photo/mic/speaker outline vectors | Muted-speaker state; same stroke family and hit areas |
| oms/routes.js | Session-owned text history and separate transcription | Reserve a shared voice/text turn; call the same history, warehouse facts and AI answer |
| oms/chat-media.js | PCM validation and OpenAI audio endpoints | Reuse provider, validate actual bytes, silence rejection, abort propagation |
| New oms/voice-upload.js | No multipart upload handler | Auth-first Busboy parser, size/time/part/filename/MIME limits, memory only |

Backend local: `E:/warehouse-ai`, Node/Express + SQLite. Editor override uses `http://127.0.0.1:8000` and `data/warehouse-live.sqlite3`. Existing login and OpenAI chat share OmsMobileSession; there is no Editor preview AI fallback. Photo chat uses OpenAI Responses. Audio now defaults to `gpt-4o-mini-transcribe`, with `tts-1`/`nova` speech unchanged. OpenAI key remains exclusively server-side.

Pre-release Render inspection, owner-confirmed workspace: **Bảo Tín's workspace**, service **warehouse-ai**, `srv-dapi78navr4c738ooqu0`, `https://warehouse-ai-fmjc.onrender.com`. Old deployment `893edb4…` (2026-09-23) had health 200 but **POST `/api/oms/mobile/login` returned 404**. The new publication and verification below resolve that missing-route gap.

## Implemented architecture

Explicit mic tap → request Android permission if needed → record at requested 16 kHz, at most 30 s → second mic tap/limit stops capture → trim to captured frames → average channels → PCM16 mono WAV → reject too-short/silent recording → authenticated multipart upload → validate WAV → OpenAI STT → same JarVis `chatTurn` used by typed/image chat → return transcript + answer → add exactly one user transcript and one assistant response. A pre-existing typed draft is preserved.

The backend reserves the session turn before voice upload/STT; a concurrent text/voice turn returns BUSY. Successful turns alone append to the same 12-message server-owned history. Voice never writes inventory, tasks or warehouse operations. No automatic retry can duplicate provider charges. Local rate/quota behavior remains the existing process-memory implementation; this change does not claim durable quotas/history.

Unity states: Idle, Recording, Processing, Uploading, WaitingForAI, Completed, Error. Recorded state shows a pulsing stop icon and elapsed seconds. Processing/upload/wait blocks other send/record/photo actions. The speaker preference can be switched off while a reply is pending. Permission callbacks have a generation guard. Navigating away, clearing session, pausing or destroying cancels microphone/network/TTS and releases resources. One AudioSource is reused; newest reply replaces old speech. Text survives a TTS failure. Speaker preference is saved locally, initially OFF.

Composer tree: existing input-bar → photo 44 × 48, microphone 44 × 48, speaker 44 × 48, flexible TMP input, send 44 × 48. HorizontalLayoutGroup owns spacing; input flexes, row stretches between 16-unit screen margins. Icons are 24-unit vectors with transparent hit backgrounds. The older plus-menu remains an inactive compatibility path. No 3-button label popup is shown in the current composer.

## API contract

`POST /api/oms/mobile/jarvis/voice` (equivalent authenticated route to the prompt's `/voice`):

- `Authorization: Bearer <existing app session>`; HTTPS outside localhost.
- `multipart/form-data`; exactly one file field **audio**, filename `question.wav`, MIME `audio/wav` (`audio/x-wav` and `audio/wave` also accepted). No extra fields.
- File max 2,048,044 bytes; envelope max file limit + 16 KiB; upload timeout 20 s. PCM16 mono, 8–32 kHz, 0.25–32 s, exact RIFF/fmt/data lengths and alignments; Unity records max 30 s at 16 kHz. Byte validation overrides MIME claims. No temp/persistent WAV files.
- Whole voice backend deadline 95 s; Unity voice request 110 s, bounded for slow requests. STT provider has 65 s cap; AI answer uses existing server timeout. No automatic retry.

Success example (shape only, not test evidence):

```json
{"success":true,"transcript":"...","reply":"...","message":"...","language":"vi","sources":[],"mode":"openai","synthetic":false,"operationalMutation":false}
```

Errors: `{ "success": false, "code": "INVALID_AUDIO", "detail": "..." }`. Codes include NO_AUDIO, INVALID_AUDIO, AUDIO_TOO_LARGE, TRANSCRIPTION_FAILED, EMPTY_TRANSCRIPT, AI_REQUEST_FAILED, OPENAI_CONFIGURATION_ERROR, SERVER_ERROR, TIMEOUT, NETWORK_ERROR plus AUTH_REQUIRED/BUSY/RATE_LIMIT for session controls. Unity handles empty/oversized uploads before constructing multipart data. No provider response body, key, password, token, transcript or raw WAV is logged.

## Files changed/created

Unity modified: `Assets/JarVis/Scripts/UI/ImportedChatMedia.cs`, `ImportedJarvisChat.cs`, `ChatActionIcon.cs`, `Assets/JarVis/Scripts/Backend/OmsMobileApi.cs`, `Assets/Resources/JarVisImportedTranslations.json`, existing `Assets/Editor/ProjectBootstrap/ChatIconMenuReview.cs` for SpeakerOff compatibility; context/verification documents.

Unity created: `Assets/Editor/ProjectBootstrap/VoiceWorkflowReview.cs` and this report (Unity imports the script metadata).

Backend modified: `server.js`, `oms/routes.js`, `oms/chat-media.js`, `package.json`, `package-lock.json` (Busboy 1.6.0). Created: `oms/voice-upload.js`, `tests/oms-voice-workflow.test.cjs`; backend contract/deployment-readiness report.

## Environment and Inspector setup

Existing server env names: OPENAI_API_KEY, OPENAI_MODEL, OPENAI_BASE_URL (optional; default official OpenAI), DATABASE_PATH, NODE_ENV, MAX_REQUESTS_PER_MINUTE, MAX_REQUESTS_PER_DAY, REQUEST_TIMEOUT_SECONDS, MAX_OUTPUT_TOKENS; OMS_SEED_DEMO/OMS_DEMO_PASSWORD only for intentional development fixtures. Added OPENAI_TRANSCRIPTION_MODEL (default gpt-4o-mini-transcribe) and opt-in OMS_BOOTSTRAP_ADMIN_HASH for separate cloud account provisioning. No new OpenAI secret or client key. Node >=18 is already declared. Install dependencies using the updated lockfile.

No manual Inspector assignments are required for this runtime composer: the imported scene owner configures TMP controls, buttons and the one AudioSource. Android Microphone access uses Unity's Permission API. Keep the project's Android input/permission settings and build pipeline. Builds now bundle the published cloud endpoint; existing explicit Editor/device overrides take precedence. Enter Play in the existing scene to see the runtime UI; Edit frames remain source assets.

## Verification status

- **IMPLEMENTED**: unified voice route/provider/history, Unity WAV/upload/feed, compact controls, speaker preference and lifecycle cleanup.
- **RUNTIME VERIFIED, backend**: `npm run verify`, **53/53 passed**, evidence `E:/warehouse-ai/logs/voice-bilingual-verify.log`. Tests use temporary databases and explicitly injected providers, not acoustic accuracy evidence. They cover auth, silence/fake WAV/MIME/path/extra file/extra field/oversize, shared text/voice history, failure recovery, concurrency, abort, cloud admin provisioning, bilingual prompt/model configuration and exact transcript preservation.
- **LIVE PROVIDER FINDING**: Run 5 passed at `2026-10-07T06:58:44.9479428Z` (`Logs/VoiceWorkflowReview5.log`). Run 6 subsequently failed the Vietnamese context-recall assertion: Whisper transcribed the synthetic question incorrectly, and JarVis asked for clarification. Transport, language, two-message feed, independent draft, layout/scroll and speaker cancellation still passed. Preserve this failed evidence in `Logs/PrototypeParity/VoiceWorkflow-live-run6.txt` and `Logs/VoiceWorkflowReview6.log`; do not claim consistently accurate Vietnamese recognition. Both runs used TTS fixtures, not human microphone capture.
- **UNITY UI/TRANSPORT CHECK**: Run 7 PASS `2026-10-07T07:23:58.1491412Z`, `Logs/VoiceWorkflowReview7.log` and current `Logs/PrototypeParity/VoiceWorkflow.txt`. Validator, compile, login, four theme/language combinations, alternate aspects, WAV conversion, silence/error recovery and lifecycle passed. Cancellation uses an intentionally invalid RIFF header to prevent any billable model call even if cancellation races. This run does not verify the new model's acoustic accuracy. Online UI gating uses an Editor fixture without changing employee hours.
- **NOT RUNTIME VERIFIED**: physical Android microphone, Android permission refusal/device-busy behavior, real phone keyboard/photo picker, audible speaker quality, new-model acoustic accuracy and successful cloud AI inference. Published cloud login/voice validation passed as recorded below.

## Security and release boundary

Auth runs before multipart parsing. Memory/file/part limits, byte checks and silence detection precede STT. Audio/images are ephemeral; history retains text only. Secrets, local account file and databases remain ignored; no warehouse sample data or preview token is substituted into Unity. Existing login credentials were used only in memory for local QA.

The user authorized publication on the existing Render Free service with a separate cloud admin, conditional on no paid infrastructure. A random cloud password is stored only in ignored `E:/warehouse-ai/data/oms-cloud-admin-credentials.txt`; only its scrypt hash is merged into Render env. Existing local accounts/database are not uploaded. SQLite remains ephemeral; the hash can restore the initial cloud admin after reset, not employees/stock/history. OpenAI usage remains billable; cloud smoke checks avoid model calls. Publication/login status is recorded below when verified.

## Bilingual recognition update

Installed official OpenAI `transcribe` skill, available next turn. The server defaults to `gpt-4o-mini-transcribe` and supplies a short English/Vietnamese terminology prompt (JarVis, Justin, SKU, QR, 5S, FIFO, FEFO, QC, tồn kho, mã vạch, vị trí kệ). It does not force UI language, translate, rewrite the transcript, or auto-retry/fall back. New-model live recognition is **NOT VERIFIED** under the cost condition; real human VI/EN/mixed speech needs a device evaluation.

Unity builds bundle the public HTTPS endpoint in `Resources/JarvisBackend.json`. Explicit Editor/device overrides take precedence; invalid overrides fail visibly. Login/text/photo/voice all read the same endpoint/session. The existing localhost Editor override remains in effect. No credentials or OpenAI key are bundled.

## Cloud publication result

Render release `2f83f4fb713fe9c34c4bf4a8c88575eaead1ae57`, deploy `dep-db2v836q1p3s73esu3t0` is **live**. Cloud smoke PASS at `2026-10-07T07:33:40.438Z`: health, unauthorized rejection, separate admin login, `/mobile/me` on the same session, invalid WAV/silence rejection and logout. Evidence: `E:/warehouse-ai/logs/cloud-mobile-smoke.txt`; local helper `scripts/verify-cloud-mobile.cjs`. No billable model calls. Route provider configuration exists, but actual cloud key/model access/inference quality is not claimed. The original Free plan was retained; no new infrastructure was purchased.

The local backend was reloaded with the same existing live database/account and new STT configuration. Unity run 7 verifies local login/UI; cloud API smoke verifies published auth/voice validation. Do not conflate these with human Android microphone verification or a live successful new-model transcription.

Primary API references checked: [OpenAI speech-to-text](https://developers.openai.com/api/docs/guides/speech-to-text), [Busboy upload limits](https://github.com/mscdex/busboy).
