# Master App Development Pipeline

## Phases and exit gates
| Phase | Action | Exit evidence |
|---|---|---|
| 0 Scope | Requirement, constraints, prototype-first, budget, target | Acceptance + explicit permissions |
| 1 Discover | Read AGENTS/memory/status/stack/entry/history | Owner map + baseline + unknowns |
| 2 Design | Only necessary architecture, contracts, states, dependency graph | Compatible smallest slice + impact |
| 3 Prepare | Git safety, env/secrets, dependencies, isolated fixtures | No overwrite; prerequisites satisfied |
| 4 Prototype (conditional) | Reference/components/assets, isolated interactive concept | Shown design + approval before production |
| 5 Implement | Authoritative domain/server/API then selected frontend | Reachable integrated slice, no duplicate owner |
| 6 Integrate (conditional) | AI/context, voice, auth, DB, realtime, uploads, services | Boundary/cancel/retry/errors tested |
| 7 Validate | Focused tests → adjacent regression → relevant runtime/build/render | Fresh dated layer evidence |
| 8 Release (conditional) | Authorized revision/config/deploy/artifact | Actual release ID/hash + route checks |
| 9 Verify/handoff | Applicable DoD, limitations, memory | DONE/PARTIAL/BLOCKED + next step |

Đây là graph có gates, không waterfall bắt buộc. Feature trong app cũ bắt đầu từ baseline hiện có và chỉ đi nhánh cần đổi. Requirement thay đổi thì cập nhật scope/dependencies trước, không âm thầm mở rộng.

## Conditional capabilities
| Needed? | YES | NO |
|---|---|---|
| Backend | backend-data + API consumers | Local app/static UI, skip server |
| Database | schema/validation/transactions/backup/durability | No invented persistence |
| Authentication | Server roles/ownership/session expiry | Explicitly public surface, don't invent login |
| Realtime | Order/dedup/reconnect/backpressure/hidden-state lifecycle | No always-on polling |
| Voice | Actual device/capture/audio/playback; remote branch only if required | Skip permissions/audio/provider spend |
| File upload | Content/size limits, lifecycle/retention/access | Skip media capture/storage |
| AI | Current provider/model, server proxy for remote, eval/cost gate | Skip key/model/prompt infrastructure |
| Unity | Existing Unity owner/scene/compile/Play/target | Use audited web/native stack |
| Figma | Exact design frame/assets/approval | Preserve current design/user reference |
| Render | Provider-specific deploy only when requested | Use selected provider, or no deployment |
| Mobile | Safe area/IME/permissions/device evidence | Target actual classes, no Android requirement |
| Web | Routes/focus/responsive/browser QA | No web prototype unless useful/requested |

## Feature and bug routes
Feature: understand → inspect partial owner → contract/states → prerequisites → minimal implementation → real route integration → scoped regression → DoD.
Bug: expected/actual → reproduce → classify layer → trace input/output → supported cause → minimal fix → original case + adjacent regression → verification. A blocked reproduction remains a hypothesis, not DONE.

## Production-ready gate
Requires applicable DoD plus operational auth/data persistence/privacy/backup/recovery/cost/observability and deployment evidence. A local UI prototype, model mock, APK compile or health-only smoke cannot satisfy this gate.
