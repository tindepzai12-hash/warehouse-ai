# JarVis voice messages — 2026-10-07

This report supersedes the recording/feed behavior in `VOICE_WORKFLOW_IMPLEMENTATION.md`. Previous live-provider results remain historical evidence, not evidence for the new implementation. The owner deferred Samsung testing; no billable STT/chat calls were made for this change.

## 1. Initial root cause

The existing authenticated `/api/oms/mobile/jarvis/voice` already sent one complete WAV through STT and the shared JarVis chat turn. However, Unity destroyed the original clip and rendered only the returned transcript after waiting for the answer. There was no recording cancel bar, real waveform, optimistic audio bubble, original-audio playback or durable request identity. A lost response followed by Retry could spend another provider turn.

## 2. Architecture

- `ImportedChatMedia`: existing permission/photo/TTS integration, voice orchestration, cancellation and upload lifecycle.
- `VoiceRecorder`: bounded microphone capture, sample-position timer, real RMS envelope, Stop and one-time finalize.
- `VoiceMessage` / `VoiceAudio`: UUID, timestamp, delivery status, optional transcript, canonical WAV validation, 64-bar PCM envelope and app-cache audio ownership.
- `VoiceRecordingBar`: replaces the existing flexible composer only while recording/stopped. No scene rebuild or persistent recording popup.
- `VoiceMessageView`: user-aligned audio row, play/pause, progress, duration, status, safe retry and collapsed transcript.
- `VoicePlayback`: one source and at most one decoded playback clip. Stops competing audio/TTS, pauses/resumes and resets on finish.
- `OmsMobileApi.VoiceUpload`: capability check, authenticated one-file multipart POST, message identity, disposable request and terminal retry policy.
- Existing backend `routes.js` / `voice-upload.js` / `chat-media.js`: unchanged STT/provider architecture, same text/voice history; `voice-receipts.js` adds bounded SQLite receipts.

## 3. Modified files in this pass

Unity: `Assets/JarVis/Scripts/UI/ImportedChatMedia.cs`, `ImportedJarvisChat.cs`, `ChatActionIcon.cs`; `Assets/JarVis/Scripts/Backend/OmsMobileApi.cs`; `CODEX_CONTEXT.md`, `UNITY_UI_WORKFLOW.md`, `Docs/VOICE_WORKFLOW_IMPLEMENTATION.md`.

Backend: `E:/warehouse-ai/oms/routes.js`, `tests/oms-voice-workflow.test.cjs`, `docs/VOICE_WORKFLOW_IMPLEMENTATION.md`. Pre-existing dirty files elsewhere were preserved.

## 4. Created files

Unity UI: `VoiceMessage.cs`, `VoiceRecorder.cs`, `VoiceWaveform.cs`, `VoicePlayback.cs`, `VoiceRecordingBar.cs`, `VoiceMessageView.cs` under `Assets/JarVis/Scripts/UI/`, with Unity-generated `.meta` files.

Unity QA: `Assets/Editor/ProjectBootstrap/VoiceMessageReview.cs` and this report. Backend: `oms/voice-receipts.js`, `tests/oms-voice-receipts.test.cjs`, `scripts/voice-message-fixture.cjs`. Fixture scripts/providers never form a production fallback; they create only their own temporary DB and short-lived localhost proxy, never the live warehouse DB.

## 5. Recording workflow

Mic tap → explicit Android permission if required → actual `Microphone.Start` (requested 16 kHz, 30 seconds, no looping). Sample cursor determines duration. The recorder reads a reused 1024-frame buffer at 10 Hz and computes RMS; 48 live bars form a bounded rolling buffer, not random animation.

Cancel destroys the current recording, makes no message/request and restores the independent typed draft. Stop retains recorded audio for Send/Delete. At the 30-second limit capture stops, never auto-uploads. From 25 seconds the UI warns about the limit. Send stops first, encodes all captured frames as PCM16 mono using the actual clip sample rate, rejects <0.25s/silence and finalizes only once.

## 6. Voice UI and playback

Valid Send creates the audio bubble before any network result. It is not replaced by transcript. Static waveform normalizes the 64 RMS windows from the actual full recording for visibility, without fabricating bars. Play/Pause decodes and plays that original WAV, highlights progress and resets on finish. Starting B releases A; TTS and original voice do not overlap. Optional transcript defaults collapsed and is rendered as plain text.

Delivery states: Sending → Processing → Completed or Failed. Conversation scrolling stays enabled. Retry uses the same message and audio file, never appends another row. VI/EN status/copy and light/dark surfaces reuse the current JarVis identity and 44px targets.

## 7. Backend/OpenAI workflow and release gate

`GET /api/oms/mobile/jarvis/voice` is authenticated and returns `{protocol:"voice-message-v1",idempotency:true,maxSeconds:30}` without invoking a provider. Unity requires this capability before sending audio. A deployment lacking the new capability displays a server-update error; the WAV is not uploaded or charged. This is a metadata check, not a second AI request.

Main request remains exactly one `POST /api/oms/mobile/jarvis/voice`, multipart file `audio` / `question.wav` / `audio/wav`, plus `X-Voice-Message-Id` UUID v4 header. No parallel endpoint or client API key. MIME/filename/actual WAV/silence/size/time validation precede STT. Existing session turn lock excludes concurrent voice/text requests. STT → same `chatTurn` → existing provider → one assistant response. Transcripts are intermediates, not a second user send.

SQLite table `oms_voice_receipts` stores session-token hash, message ID, audio hash, time, status and bounded response JSON (including transcript/answer, not raw audio). It is created lazily in the existing DB. Max 10,000 receipts; expired records are pruned on the next receipt claim after 24h. A successful retry returns the cached result without repeating STT/chat/history; a changed WAV with the same ID is rejected. A pending receipt after process crash is ambiguous and cannot call the provider again. The client does not offer Retry beyond 23h. This is at-most-once provider execution within the receipt retention/session scope, not exactly-once delivery or restart-persistent conversation history.

No automatic model/account rotation. Provider failures after execution starts are saved as terminal (`retryPolicy:"never"`); the original audio remains available for playback. Pre-provider network/BUSY/rate/capability failures can retry manually.

Deployment/restart of the new backend has not been performed by this pass. The existing published backend's earlier login/voice validation does not prove support for this capability. Update the existing service with these files before testing the APK voice pipeline; do not substitute a demo DB, upload `.env`/accounts/warehouse data or publish an unreviewed dirty working tree.

## 8. Conversation context

No new conversation store is introduced: text, photos and voice still use the authenticated mobile session's server-owned RAM history (last 12 messages, 1h inactive expiry). Successful original voice turns append one user transcript and one assistant answer internally. Receipt replays append neither. UI audio remains typed as `VoiceMessage`, alongside existing text/photo rows. Logout/new auth clears the local conversation and audio files. Server restart/session expiry still loses the prior RAM context, as before.

## 9. Errors, resources and privacy

Permission callback generations ignore callbacks after leaving/resetting the screen. Unavailable/busy microphone, too-short/silent/invalid audio, auth expiry, unsupported backend, network timeout, STT and JarVis failures have visible non-crashing states. Failed uploads preserve the audio bubble. Abort/navigation/background marks in-flight audio failed and disposes requests; uncertain provider outcomes use receipt replay, not new identities.

Ownership: capture clip/buffer are destroyed after finalize/cancel; WAV is written once in this app's temporary cache; full PCM exists only during encode/decode. A single active playback clip is retained. Upload's byte array/request are released afterward; idle messages retain only cached WAV and small metadata/envelope, not duplicate full clips. Existing 60-row history trim, session reset and destruction dispose the audio files. OS cache eviction/app restart can remove cached audio; this feature does not claim durable audio-history restoration. Receipt responses are new short-retention server data, unlike the old audio-only memory workflow; protect the existing DB accordingly.

## 10. Verification

- Initial `VoiceMessageCompile2.log`: Unity exit 0, runtime scripts compiled. Attempt 1 could not open a project already in use; no Editor was killed or lock removed.
- `VoiceMessageReview1.log`: one QA-only C# expiry argument error, fixed; preserved failed log.
- `VoiceMessageReview2.log`, `VoiceMessageReview3.log`, `VoiceMessageReview4.log`: compile + Play review passed. Current `Logs/VoiceMessageReview/Result.txt` records 39 checks: 3s/20s full finalization, exact WAV, genuine PCM envelope, one-time finalize, optimistic rows, actual AudioSource progress/pause/exclusivity/reset, error retention/retry UI, transcript collapse, VI/EN × light/dark composer/draft/targets and file disposal. Generated PCM fixtures are not human microphone or ASR evidence.
- Backend `logs/voice-message-tests-final.log`: 3/3 focused suites passed, covering auth/capability, shared history, concurrency, byte validation, STT/JarVis failure, cached retry, conflict, session isolation and durable pending receipts. Providers and databases are isolated test fixtures.
- Broad `logs/voice-message-verify.log`: 54/54 passed before the final capability patch. Final broad `logs/voice-message-verify-final.log`: 53/54 passed; unrelated existing web UI test has a strict-mode locator matching `NV-00103` twice (`tests/oms-ui.test.cjs:66`). It was not fixed outside voice scope.
- `Logs/VoiceMessageTransport1.log` / `Logs/VoiceMessageReview/Transport.txt`: compile + 12 Unity HTTP checks PASS against the isolated backend/proxy. The first successful response was deliberately dropped after processing; same-ID Retry recovered it without another STT/answer or voice row. 3s and 20s WAV reached the actual upload/parser/routes; second voice carried prior user/assistant context, draft stayed independent, original audio still played after upload. Backend counters `E:/warehouse-ai/logs/voice-message-fixture-metrics.json`: 3 main audio POSTs (one manual retry), 2 STT fixture executions, 2 answer fixture executions, matching IDs for the retry. This proves app/HTTP/backend integration with generated PCM and injected providers, not real microphone, acoustic recognition or model quality. Fixture server was shut down and its temporary DB removed.
- `Logs/VoiceMessageAndroidBuild.log`: Android build **Succeeded**, Unity exit 0; final APK 50,326,967 bytes. Production runtime source is the same as `VoiceMessageReview4`; the subsequent transport changes affect Editor QA only, not player code. SHA256 `DA1FE79A8C7522D21E567C6394D5395EBA8D02AD77506084FE589B12DCE6AD58`. Android merged manifest includes `RECORD_AUDIO`; no install/device/permission acceptance is implied.

## 11. Remaining acceptance work

Owner postponed Samsung connection. Verify real 3s/20s VI/EN/mixed speech, live amplitude/timing, denied permission/mic busy, whole-utterance STT/AI accuracy and two-voice follow-up context on the phone. Check audible original playback, keyboard/safe areas, network failure/retry, background/logout and performance/GC on Samsung S20 Ultra. Any billable provider test requires owner approval; no fabricated transcript or model answer is used in the real flow. Publish/reload the reviewed backend first and retain the existing live DB/account.

APK intended path: `E:/unity of teep/JarVis-Warehouse-AR/Builds/Android/JarVis-Warehouse.apk`. A build artifact alone is not device/E2E acceptance.

## Compact Unity refinement — 2026-10-07

Owner requested a smaller voice interface and then explicitly prioritized viewing it in Unity before APK/backend/device work. Voice bubbles are now 288×72 logical pixels by default, with play, waveform and duration on one row, a muted status caption and a 44px transcript icon instead of a separate transcript row. The recording composer retains 44px targets, uses neutral trash/stop icons, a blue Send circle, rounded waveform bars and a simple elapsed timer. Light surfaces remain white/pale blue rather than all blue.

Files in this refinement: `Assets/JarVis/Scripts/UI/VoiceMessageView.cs`, `VoiceRecordingBar.cs`, `VoiceWaveform.cs`, `ChatActionIcon.cs`, `ImportedFigmaNavigation.cs`, `Assets/Editor/ProjectBootstrap/VoiceMessageReview.cs` and this report. Existing scene/source imports and unrelated working-tree changes are preserved.

The verification section above describes the earlier layout. `Logs/VoiceMessageCompactReview1.log` exposed a QA-only LayoutElement namespace error, corrected with the explicit UnityEngine.UI type. Review 2 was interrupted and did not produce fresh results. `Logs/VoiceMessageCompactReview3.log` completed with Unity exit 0 and 50 PASS checks in `Logs/VoiceMessageReview/Result.txt`, including compact height, transcript expand/collapse, real fixture playback and 44px controls in VI/EN/light/dark. Four fresh screenshots were inspected. Dark captures still omit the second play glyph and dock scanner glyph; button size/state checks do not prove glyph visibility. This needs a focused render check in the interactive Editor, not an unsupported claim of full visual acceptance.

Review 3 also exposed a missing AudioListener in the existing scene. Navigation initialization now adds one only in Play and only when no active listener exists, without rebuilding/saving the scene; a focused assertion was added. The new Play assertion has not run yet. `Logs/UnityInteractiveVoice.log` records opening the existing `Assets/Scenes/JarVisFigmaApp.unity` in Unity 6000.4.7f1; check this log for the latest compile status.

The existing APK and signature evidence above belong to the earlier voice layout, not this compact refinement. APK rebuild is deferred to honor the owner's Unity-first request. No backend edits/deployment, real microphone capture, Samsung test or billable AI calls were performed during this refinement.
