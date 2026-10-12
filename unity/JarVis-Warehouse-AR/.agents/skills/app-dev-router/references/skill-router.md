# Skill router

## Classification
- Level 1: text/icon/layout nhỏ, existing owner; scoped checks, không full architecture/deploy.
- Level 2: screen/form/feature, tìm phần đã có rồi integration/regression.
- Level 3: AI/auth/DB/voice/deploy, boundary/security/integration checks.
- Level 4: restructure/migrate/data flow, impact/compatibility/backup before implementation.

Levels là mức rủi ro, không quyền thực thi. Kết quả route là kế hoạch; không tự commit/push/deploy/capture/upload.

## Minimum selection
1. Chỉ audit trạng thái: project-audit.
2. Có sửa: git-safety + đúng owner; verification theo scope, testing khi có behavior/build/render thay đổi.
3. Backend/data/auth/realtime/upload: environment-security + backend-data; API chỉ khi client/contract liên quan.
4. Remote OpenAI: backend/data + API + environment-security + openai-chat. Local model không tự kích hoạt OpenAI.
5. Voice local: voice-audio + frontend adapter nếu có, không AI/Render. Voice→AI: thêm server/API/OpenAI.
6. Figma/reference hoặc prototype-first: figma-ui + UI + adapter được chọn. Prototype approval là gate độc lập trước production application.
7. Bug: root-cause-debug + owner được evidence chỉ ra. Chưa biết owner thì trace trước, không chạy cả frontend/backend/providers.
8. Render chỉ khi provider là Render và có yêu cầu deploy; không chọn vì repo có render.yaml.
9. Migration/new system: architecture; auth/data boundaries kéo security kể cả không có backend flag.
10. Project memory chỉ khi bootstrap/handoff/architecture, hoặc cần ghi decision/evidence mới.

## Runnable planning helper
`python scripts/route.py --kind ai --frontend unity`
`python scripts/route.py --kind deploy --backend --provider render`
`python scripts/route.py --kind figma --frontend unity --prototype-first`
`python scripts/route.py --kind bug --frontend unity --voice`
`python scripts/route.py --kind ui --frontend web --level 1`

Chạy từ skill root hoặc dùng absolute script path. Các flags phải lấy từ audit/user intent; helper không hiểu free text/NLP, không thay quyết định của Codex. Voice mặc định local-only; thêm --ai khi user yêu cầu remote speech→OpenAI. --backend, --database, --auth, --realtime, --upload mô tả capability thật; --provider custom không ép Render. Không truyền secrets/user speech cho helper.

## Gates and stop conditions
- Prototype-first: chỉ audit/prototype/show, chờ approval; không sửa production dù route chứa UI implementation.
- Secret/provider access: thiếu thì dừng live integration, không fake success.
- Publication: explicit authorization + reviewed files/commit; không bulk push dirty work.
- Live AI/audio: bounded approved data/cost; no unapproved call or retry.
- Paid resource: ask before purchase/upgrade.
- Policy denial: stop blocked action, finish unaffected work, request missing input; không equivalent workaround.
- Editor open/unsaved: reuse supported in-Editor runner, không kill/start duplicate.

## Examples with exclusions
| Request | Selected flow | Skip / gate |
|---|---|---|
| Kết nối OpenAI vào Unity | audit → Git → architecture → env/security → backend → API → OpenAI → Unity → tests → verification | No voice/Figma/Render unless requested; live spend approval |
| Deploy backend Render | audit → Git → architecture → env/security → backend → tests → Render → verification | No Unity/OpenAI inference; publish exact authorized revision |
| Figma vào Unity, prototype trước | audit → Git → Figma → show + APPROVAL → UI → Unity → tests → verification | Do not apply prototype before approval |
| Mic không có tiếng trong Unity | audit → reproduce/input/output → Git → Unity → voice → tests → verification | No STT/provider call until actual capture established |
| Đổi một icon web | audit → Git → UI → focused render/test → verification | No DB/architecture/Render/full regression |

Human router may narrow a route further once reproduction proves an owner is unaffected. Any skipped mandatory requirement must retain an explanation; a script route is not an instruction to mutate every selected subsystem.
