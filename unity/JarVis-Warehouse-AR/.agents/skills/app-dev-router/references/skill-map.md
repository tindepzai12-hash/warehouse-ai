# Skill architecture

Một router + 17 module; không phải 18 bước bắt buộc. Dependency dưới đây là điều kiện cần có khi thực thi, không có nghĩa luôn tải module đó.

| Skill | Trách nhiệm | Trigger | Prerequisite / module liên quan |
|---|---|---|---|
| app-dev-router | Chọn tối thiểu và đặt gate | Bootstrap/tích hợp/chọn workflow | User scope + project memory |
| app-project-audit | Stack, owner, baseline, business vs reusable | Chưa biết trạng thái | Read access, AGENTS |
| app-architecture | Boundaries, impact, migration | New boundary/Level 4 | Audit + constraints |
| app-git-safety | Giữ dirty work và release scope | Sửa/Git/deploy | Baseline; authorization riêng cho commit/push |
| app-environment-security | Env, secrets, roles, origins, retention | Integration/env/auth | Audited target environment |
| app-backend-data | Backend/auth/DB/realtime/uploads | Server/data changes | Env/auth/data identity; migration plan nếu cần |
| app-api-integration | DTO/request/lifecycle/retry | Client↔server | Existing route hoặc contract + shared session |
| app-openai-chat | Proxy, model/prompt/context/parsing/eval | Remote OpenAI/chat | Backend, server secret, cost gate |
| app-voice-audio | Capture/device/WAV/playback/STT/TTS | Audio/voice | Audio stack; API/AI chỉ nếu remote |
| app-unity-app | Runtime Unity, compile, target | Unity change | Current Editor/project/UI owner |
| app-figma-ui | Frame/component/assets/approval | Figma/reference/prototype | Exact visual authority; approval before apply |
| app-ui-navigation | States, layout, routes, a11y | UI/navigation | Tokens/owners; approved concept nếu prototype |
| app-root-cause-debug | Reproduce→trace→cause→fix | Bug | Actual expected/observed evidence |
| app-testing-regression | Layer tests + old related paths | Changed boundary | Existing test isolation and owner map |
| app-render-deploy | Service/env/build/start/log/health/routes | Authorized Render action | Reviewed revision + provider access + storage/cost gate |
| app-release-verification | Applicable DoD + artifacts/release | Handoff/release | Fresh layer-specific evidence |
| app-project-memory | Current app context + lessons | Bootstrap/handoff/change | Canonical memory authority |
| app-feature-delivery | Acceptance→slice→integrate | New/partial feature | Existing partial module and dependencies |

## Dependency graph

```text
request + AGENTS + memory
          ↓
audit → optional root-cause reproduction
          ↓
Git protection → optional architecture → feature slice
          ↓
environment/security → backend/data → API → optional AI/context
          ↓                               ↓
reference/prototype → APPROVAL → UI/navigation → Unity if selected
                                                    ↓
                                            voice if selected
                                                    ↓
                                     scoped tests + regression
                                                    ↓
                                    authorized deploy if requested
                                                    ↓
                                      verification → project memory
```

Không có dependency bắt buộc Render→Unity hoặc Git remote→local backend. Local-only voice bỏ API/OpenAI; UI nhỏ bỏ architecture/deploy. catalog.json chứa requires và conditionalEdges; router chỉ áp dụng edge khi hai module đều được chọn. Khi prerequisite kỹ thuật chưa tồn tại, hoàn thành phần độc lập rồi dừng bước phụ thuộc.
