# JarVis Development Knowledge Audit
Ngày: 2026-10-07 (Asia/Saigon). Phạm vi: workspace Unity hiện có, backend local, prototype review, context và những phần lịch sử có sẵn trong chat/Git/tài liệu. Không có quyền truy cập toàn bộ lịch sử ngoài những nguồn này; không chạy live provider/deploy/DB mutation để audit.

## Authority and baseline
- Unity root AGENTS → CODEX_CONTEXT → UNITY_UI_WORKFLOW; scene hiện hành JarVisFigmaApp, native uGUI/TMP, Unity 6000.4.7f1. Prototype B approved supersedes Figma-row guidance cũ đối với UI Unity.
- Git log Unity hiện thấy checkpoint 6077fe6, 49d107e và các commit warehouse workspace; nhiều runtime/editor/assets vẫn untracked, project settings/source đã dirty trước task. Không gộp mọi dirty file thành thay đổi của lượt đóng gói.
- Backend Git release 2f83f4f ghi nhận unified mobile voice/login; working tree mới có capability/receipt chưa chứng minh đã phát hành. Docs RENDER/DEPLOYMENT/UPDATING ghi lịch sử, không thay cho kiểm tra service hiện tại.
- Prototype review ở workspace riêng phân biệt mockup/demo/reachable/source evidence; số lượng chức năng được bảo toàn là traceability, không chứng minh tất cả đã implement.
- Lượt này chỉ đóng gói skill/docs/helper, không sửa tính năng app mới hoặc tiếp tục mic/backend/Render.

## Evidence inventory
| Nguồn đã đọc | Điều rút ra | Giới hạn |
|---|---|---|
| AGENTS.md, CODEX_CONTEXT.md, UNITY_UI_WORKFLOW.md | Read-before-edit, owner map, scoped tests, preserve dirty work | Context có lịch sử cần đối chiếu với source mới |
| Docs/PrototypeParity/LAYOUT_CONTRACT.md, VERIFICATION.md, DEFERRED_WORK.md | Measured native translation, approved design, owner sequencing, device caveats | Old plus-only composer superseded bởi voice prompt |
| Tools/Verify-JarvisUi.ps1 | Refuse conflicting Editor, check fresh result/time/exit | Editor path và scanner method là JarVis-specific |
| Assets/JarVis/Scripts/Backend/OmsMobileApi.cs | Shared origin/session, DTO/async/disposal/capability gate | Auth success không chứng minh model/audio/hardware |
| UI ImportedChatMedia / VoiceRecorder / VoicePlayback / VoiceMessageView | Capture state, full WAV, optimistic audio, lifecycle/exclusive playback | devices[0] chưa đúng lựa chọn Ng Tín; user báo lỗi nghe/gửi |
| Assets/Editor/ProjectBootstrap/VoiceMessageReview.cs | Generated PCM/UI and isolated HTTP layers | Fixture/time progression ≠ real mic/audible acceptance |
| VoiceInputDiagnostics.cs và pending Library request | Read-only inspection thiết bị/mute/listener/session | Mới thêm trước khi user đổi task; kết quả chưa đọc/verify |
| Docs/VOICE_MESSAGE_SYSTEM.md và Logs/VoiceMessageReview/Result.txt | Bounded protocol/privacy/idempotency; 50 compact checks ở lượt trước | Listener assertion mới chưa chạy; dark glyph issue còn |
| Backend package.json, render.yaml | Actual Node commands/dependency detection, deployment config | Example free/ephemeral storage không production durable |
| Backend docs/JARVIS_CHANGE_MAP.md, OMS_LIVE_START.md, UNITY_API_CONTRACT.md | Runtime source-of-truth, live DB identity/roles, trace contracts | Migration docs/contract đời cũ không tự thành active imported flow |
| Backend docs/RENDER.md, DEPLOYMENT.md, UPDATING.md, VOICE_WORKFLOW_IMPLEMENTATION.md | PORT/bind/env/secrets, deployment IDs, health vs route, no unreviewed push | Prior deploy proof is dated; latest voice protocol not verified live |
| Backend oms/routes.js, voice-receipts.js and tests referenced in voice report | Server turn lock, same history, receipt semantics, test isolation | RAM context not durable; pending receipt crash fail-closed |
| E:/JarVis-Redesign-Review/REVIEW_REPORT.md | Prototype first, feature preservation, labels and limitations | Historical 348 assertions precede later map edits; not fresh map evidence |
| Chat user instructions, interruptions and reported failures | Unity-first, Samsung later, compact UI, no upload/rotation | No observed human mic fix; video inspection blocked, not completed |

## Classification — 10 groups
| Nhóm | JarVis-specific / không copy | Reusable / module |
|---|---|---|
| 1 Project-specific knowledge | Warehouse FEFO/lot-location, roles, DB identity, scene IDs, mascot, viewport, endpoints/accounts | Keep in project memory, not skills |
| 2 Reusable knowledge | Compile vs runtime vs hardware; health vs route/provider; ephemeral vs durable | Evidence ladder, release verification |
| 3 Reusable workflow | Inspect partial owner → smallest slice → integrate → scoped regression | feature-delivery/root-cause-debug |
| 4 Reusable skill | Named modules with trigger/precheck/workflow/errors/output | 18 app-* skill folders, minimum router |
| 5 Reusable automation | Fresh-result validation, deterministic routing, no-overwrite copying | Planning/install/package validators; no recurring automation |
| 6 Coding conventions | C#/PowerShell style và GUID/meta local | Follow incumbent style; avoid unrelated rename/comments or new framework |
| 7 Architecture patterns | OMS warehouse domain/schema | Shared session/origin, owner separation, authoritative server, explicit context/retention |
| 8 Deployment patterns | Render service ID/private repo/model/free-plan values | Reviewed revision→env/build/start→deploy ID→health→actual route, storage gate |
| 9 Debugging patterns | Ng Tín hardware name / current error | Input→capture→WAV→HTTP→provider→UI→output; confirmed facts vs hypothesis |
| 10 UI/UX patterns | JarVis palette/mascot/native coordinates | Respect approval/identity, real components/states/navigation, grouped visual QA |

## Workflows actually supported by evidence
- Feature starts with current owner/partial implementation and test boundary, not a new project.
- New UI/reference: measure → components/tokens/assets → isolated prototype when asked → show/approval → runtime translation → interaction/render. Latest explicit approval controls authority.
- API: actual server contract → shared client DTO/session → loading/error/cancel/retry → isolated HTTP + old adjacent paths.
- AI: server key/proxy → configured model/prompt → shared conversation → defensive response/error parsing → fixture tests → optional cost-approved live evaluation.
- Unity: source runtime edit → compile/Play focused runner; no all-scene rebuild/import; open Editor reused, no kill/second instance.
- Render: detect actual runtime/root/commands/env/storage → reviewed release → deployment/logs → HTTPS health/auth/capability. Published prior version ≠ latest source.
- Debug: expected/actual/layer → reproduction → trace → minimal root fix → regression. User-reported mic failure remains unresolved despite UI tests.
- Architecture: owner/data retention/compatibility/impact → reversible slice → migration plan/verification, not gratuitous rewrite.
- Handoff: exact changed file set, evidence age/layer, failures, hardware/provider gaps and artifact provenance.

## Lessons learned — debt is not best practice
| Observed risk/debt | Better reusable procedure |
|---|---|
| Microphone.devices[0] | Enumerate/select actual/default input, persist preference, handle disconnect; do not hard-code Ng Tín |
| Missing AudioListener and unchecked audible output | Explicit listener/source/mute/output diagnostics and human audible acceptance |
| Rounded compact cards pass geometry but some dark glyphs disappear | Render inspection independent of size/raycast assertion |
| STT transcript formerly replaced voice / user utterance cropping risks | Preserve original whole audio, optional transcript, one utterance identity |
| Provider retry / lost response ambiguity | Stable scoped ID + server receipt/hash/retention; cached result, terminal uncertain provider outcome |
| RAM context/ephemeral SQLite | Explicit longevity and crash/restart semantics; durability/restore gate before production |
| Dated reports/header/doc conflicts | Canonical current authority + revision/date/layer; preserve historical results as historical |
| Wide dirty repo and auto-deploy | Review explicit files/revision; do not git add . or push unreviewed work |
| Hard-coded editor paths / many patch scripts | Discover runtime/tool availability, smallest owner changes, scoped scripts with tested invariants |
| Synthetic/mock integration | Labels, isolated data, separate human speech/quality/device tests |
| UI-only/map placeholder/prototype | Never represent intended capability as production business functionality |
| Blocked video/tool access | Stop blocked action, request targeted screenshot/input; no alternate policy bypass |

## Missing capabilities added to the system
Security/env/privacy, durable-data/migration/recovery, deployment rollback boundaries, actual device selection/output acceptance, evidence freshness/artifact provenance, lifecycle/reconnect/idempotency, prototype approval gate, portable no-overwrite packaging and improvement review. These are workflow requirements; they do not silently implement missing JarVis features.

## Scope result
Audit/classification and reusable architecture are delivered. JarVis is NOT declared production-ready. Mic/audio/send, latest backend protocol publication, glyph visibility and Samsung acceptance remain separate app TODOs.
