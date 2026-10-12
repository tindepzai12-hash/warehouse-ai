# Native prototype verification — 2026-10-07

Latest run: **PASS**, 2026-10-06 18:49:27 UTC (2026-10-07 01:49:27 Vietnam). Evidence: `Logs/PrototypeParity/Result.txt` and `Logs/PrototypeParityPass7.log`. No project compiler errors, runtime exceptions or shader errors were found in that run.

## Executed checks

The installed Unity 6000.4.7f1 Editor opened `Assets/Scenes/JarVisFigmaApp.unity`, entered Play mode and exercised 16 native routes using `ProjectBootstrap.PrototypeParityReview.Run`. Screens were captured at 412 × 915 and 390 × 844 logical viewports; an additional 1080 × 2400 capture exercises device aspect ratio. Light/dark themes, larger text and English variants were included. Active text overflow and interactive control containment were checked.

- Home header, spacing, four coloured quick actions, shared dock and enlarged transparent JarVis were compared with measured prototype geometry. Going online now paints the state immediately after receiving the presence snapshot.
- Home hides the timer; avatar → Profile → Activity reads the existing presence total. Editor snapshot checks cover pause while offline, continuation on returning online and the server's Taiwan midnight boundary.
- JarVis blinks and repeats its celebration every five seconds. Flame preview captures show separate animation frames. Reduced-motion settings are respected.
- Scanner sweep stays inside the blue frame at eight sampled times. Manual lookup and the shared dock remain visible.
- Task and inventory templates render Editor-only fixture rows. Their detail modals open and close. These fixtures never enter the database.
- The JarVis composer retains input/send and exposes only the plus control for additional actions. The plus menu opens; media handlers were not invoked.
- Presence confirmation cancellation preserves online state. Profile navigation, transaction selector navigation and removal of overlapping legacy login icons were checked.
- Final Home, dark large-text alternate viewport and blurred offline screenshots were visually reviewed after pass 7.

## Scope and remaining differences

The source is the approved B prototype in `E:/JarVis-LiquidGlass-Prototype`; `measured-layout.json` and source screenshots document its layout. Native text remains editable TMP text and uses the same Be Vietnam Pro Regular font file. Browser and TMP bold synthesis/antialiasing, SVG refraction versus native glass shading and native operating-system chrome can differ. This is not a claim of literal 100% pixel identity.

The existing scene's runtime owners build the new presentation in **Play mode**; the imported source frames visible in Edit mode are not the final runtime UI. Real authentication, OMS fields, blank operation inputs and assistant behaviour are retained instead of the browser's mocked data. Native detail/confirmation dialogs fit inside the phone viewport. Existing language/password helper screens retain their real native forms and have not been fully converted to the browser dialog layout.

This pass verifies presentation and local interactions. It does not verify Android hardware camera/AR, device IME/accessibility, APK performance, network authentication, stock mutations, printing, issue submission or live AI/media calls. Some operation endpoints were not wired before this pass; completing and auditing those functional modules is deferred, not represented as finished here.

SF Pro Display download, installation and typography replacement are also deferred according to the owner's latest sequencing; see `DEFERRED_WORK.md`.

## Follow-up: login pointer regression

The owner's input report exposed a gap in the original parity checks: they verified overlapping login icons, not pointer focus or actual character editing. `PrototypeLoginPresentation` hid the old child viewport Image while that Image was still the input field's pointer target. The native input background now explicitly owns raycasts and `targetGraphic`; both fields remain enabled, editable and interactive. `LoginInteractionReview` checks pointer hit/focus, keyboard characters, password masking and Backspace without authenticating or saving credentials. Its result is recorded separately in `Logs/PrototypeParity/LoginInteraction.txt`; the earlier parity PASS alone is not evidence that typing worked.

Focused follow-up **PASS**, repeated after disabling inherited selectable colour tint on the new background. Both fields received pointer hits, gained focus, accepted keyboard characters and supported Backspace; the password stayed masked. `Logs/LoginInteractionReview3.log` has no compiler errors or runtime exceptions. The current run timestamp is in `Logs/PrototypeParity/LoginInteraction.txt`. Screenshot: `Logs/PrototypeParity/Login-input-fixed.png`. Batch text geometry was explicitly updated between simulated key events to mirror rendered frames. Live server authentication and physical Android keyboard remain untested.

## Reproduce

### Chat plus menu icons — 2026-10-07

Replaced the text-only photo/record/listen popup with three font-independent vector icon buttons in a compact horizontal menu. Each hit area is 56 × 56 logical pixels. The menu follows the composer position, hides suggested questions while open, restores them on close, and closes when leaving chat. Photo/microphone/speaker availability follows the existing media buttons; recording/playback use Stop symbols while active. Menu creation reads the current theme, and existing picker, recording and speech handlers are reused. Send arrow colour is explicitly owned so dark-theme recolouring cannot turn it black; the English empty-chat hint wraps within its viewport.

`ProjectBootstrap.ChatIconMenuReview.Run` passed at 2026-10-07 06:35:21 UTC (13:35:21 Vietnam), testing English/Vietnamese × light/dark, vector symbols, icon-only content, tap target dimensions, availability rules, composer clearance, suggestions, toggle/route close, wrapped hint and white send arrow. Final screenshots were visually inspected. Evidence: `Logs/PrototypeParity/ChatIconMenu.txt`, `Chat-icons-{light,dark}-{Vi,En}.png`, and `Logs/ChatIconMenuReview3.log`. No compiler errors or runtime exceptions in the final log. No API calls or hardware media actions were triggered; actual device media behaviour was not retested. An earlier run exposed a teardown null reference in the new theme lookup; the final run includes the null guard and passed through shutdown.

### Navigation / English startup regression — verified after fresh compilation

Follow-up complete: fresh `UnifiedLoginReview.log` and `UnifiedBackendReview.log` runs passed after full compilation. The extended runner checks light/dark chat → login, settings-detail → login, login Back, dock visibility and icon colours. The final opt-in run also verified through Unity's actual `UnityWebRequest`: live admin login, shared authenticated chat session, real OpenAI answer with `mode: openai` and `synthetic: false`, followed by QA logout. Current timestamp and checks are in `Logs/PrototypeParity/LoginInteraction.txt`; no runtime exceptions or compiler errors occurred in these logs.

The unauthenticated preview client/fallback was removed from `ImportedJarvisChat`. The ignored app endpoint file now contains only the live URL, no prototype token. `LocalOmsBackend` provides Editor-only live-server startup when entering Play if that loopback port is not running, using the existing live database and explicitly disabling demo seeding. It neither changes passwords nor seeds sample inventory; physical Android still requires its own configured HTTPS endpoint. Automatic interactive startup is compiled but was not exercised by the batch runner, which deliberately suppresses it.

The owner's English dark-login screenshot and Editor log showed `InvalidOperationException: Sequence contains no matching element` in `PrototypeLoginPresentation.Configure`. Matching the Vietnamese button label failed after localization and interrupted initialization before `Show(initialScreen)` could hide the shared dock. The login presentation now binds directly to the existing navigation login controller, avoiding translated-label discovery entirely. Navigation also sets its current route before deactivating outgoing pages, centralizes dock visibility with explicit login/splash exclusions, prevents Back leaving login, and refreshes settings-close visibility. Runtime SVG dock icons receive explicit selected/unselected dark colours. Demo clears the previous mobile session; the dark demo link uses a brighter blue.

The focused input runner was extended to cover chat keyboard → login, settings detail → login, login Back, and dark dock icon colours. The open Editor initially retained the older script assembly despite asset refreshes; it was closed before the fresh compiled runs reported above. Earlier input-only PASS results did not verify these navigation edits.

### Follow-up: login server configuration

The login connection report was traced to an inactive endpoint at `127.0.0.1:8011`. Restarting that preview restored transport but did not resolve the owner's `admin` login: a read-only database check showed no admin account in `oms-preview.sqlite3`, while the existing sole admin was in `warehouse-live.sqlite3`. Unity's ignored `Library/JarvisBackend.local.json` now points to `http://127.0.0.1:8000`. The live server was started on 8000 with the existing live database and demo seeding disabled. The local backend `.env` default port was aligned to 8000 so a later normal restart uses the same endpoint; credentials and other settings were preserved.

Checks completed on 2026-10-07: live health HTTP 200; real mobile admin login using locally stored credentials HTTP 200; authenticated `/me` HTTP 200; required session fields valid; QA session logout HTTP 200. No password/token was printed or stored by the check. No warehouse mutations, account creation or password changes occurred. Existing backend integration tests `oms-mobile`, `oms-live`, and `oms-ai-chat` passed 3/3 with their own temporary databases and mocked AI. Their first sandbox attempt could not connect to its local test port (EACCES); the permitted rerun passed. Physical Android and the owner's manual end-to-end Unity sign-in remain separate from this server smoke check.

### Follow-up: OpenAI connection

On 2026-10-07, the owner requested connecting JarVis to OpenAI. Inspection confirmed that authenticated Unity chat already calls `/api/oms/mobile/jarvis/chat`, and the server provider uses the official `https://api.openai.com/v1/responses` endpoint with `gpt-4o-mini` and its server-side key. No provider credentials were added to Unity. The existing disposable mobile-provider smoke check passed with a real 5S answer. A second short request through the actual live backend on 8000 returned HTTP 200, `mode: openai`, `synthetic: false`, and a Vietnamese answer; the running server's `liveApiVerified` became true. Its temporary QA admin session was logged out. Safe evidence without credentials is in `E:/warehouse-ai/logs/mobile-openai-live-backend.json`. No stock mutation occurred. The current authenticated route is verified; a full Unity device chat interaction remains untested.

Run the installed Unity Editor with `-batchmode -projectPath "E:/unity of teep/JarVis-Warehouse-AR" -executeMethod ProjectBootstrap.PrototypeParityReview.Run -logFile "E:/unity of teep/JarVis-Warehouse-AR/Logs/PrototypeParityReview.log"`. Do not pass `-quit`: the runner enters Play mode asynchronously and exits itself after completing checks. Fixtures exist only in this Editor runner; no API/model calls are made. Batch camera capture is intentionally suppressed without changing interactive Editor or Android camera behaviour.

### Voice master prompt — compact composer, 2026-10-07

The owner's new voice prompt supersedes plus-only actions. `VoiceWorkflowReview.Run` reuses the existing scene and imported HorizontalLayoutGroup. Run 5 passed real TTS → WAV → STT → shared chat, but run 6 subsequently failed Vietnamese context recall because Whisper transcribed the synthetic question incorrectly. Preserved failure: `Logs/PrototypeParity/VoiceWorkflow-live-run6.txt`; run 6 screenshots remain `Voice-live-{en,vi}.png`. Do not claim consistently accurate Vietnamese recognition from run 5 alone. The recognition model now defaults to gpt-4o-mini-transcribe with bilingual vocabulary hints, without new billable validation calls. Latest run 7 PASS at 2026-10-07T07:23:58.1491412Z (`Logs/VoiceWorkflowReview7.log`, current `Logs/PrototypeParity/VoiceWorkflow.txt`) verifies login, compact icons, four theme/language combinations, WAV/silence/error handling and cancellation without model calls. UI online state is an explicit Editor fixture without changing employee hours. No physical mic/Android permission claim. Screenshots `Voice-composer-{light,dark}-{Vi,En}.png` plus `-alternate.png` were captured; light Vi and dark En alternate visually inspected. Full contract and cloud publication status: `Docs/VOICE_WORKFLOW_IMPLEMENTATION.md`.
