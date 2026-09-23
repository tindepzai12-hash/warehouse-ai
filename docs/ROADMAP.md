# Phạm vi giữ nguyên — specification v1.1

Nguồn đầy đủ: `WAREHOUSE_AI_CODEX_SPEC.md` trong thư mục này. Bản gốc `E:\WAREHOUSE_AI_CODEX_SPEC.md` không sửa. Phạm vi yêu cầu của người dùng hiện tại chỉ là Milestone A; các chỉ thị tiến hành giai đoạn sau trong tài liệu không tự kích hoạt triển khai.

## Đã làm ở A

Node.js/Express, browser UI, health/authenticated feedback, OpenAI Responses integration với transport test, RAG cục bộ có citations, SQLite phiên theo user/task, allowlist/schema/target validation, giới hạn body/quota/timeout, API language/locale, voice browser, tài liệu và tests. Không có live warehouse. Tự động trigger/cooldown hành vi, tóm tắt phiên dài, hình ảnh và Unity action router thuộc tích hợp tiếp theo; hiện cooldown schema bị chặn trong khoảng 0–60.

## B — dịch vụ kho có giao dịch, không chuyển quyết định cho AI

- Persistent users/roles, products/variants, barcode mappings (không giả định barcode duy nhất từng đơn vị), owners, lots/dates, locations/capacity, balances/status, totes, tasks/lines, reservations, movements, exceptions, counts, route blocks/clearances, metadata và audit.
- Put-away: nhận nhiệm vụ → quét pallet/tote → đề xuất vị trí theo quy tắc capacity/turnover/expiry/storage → quét vị trí → quét sản phẩm/số thực/xác nhận → commit một lần. Sai vị trí phải chặn.
- Picking: individual/batch/wave; route hợp lệ; xác minh location/item/variant/lot/owner; ảnh thật khi có; condition; quantity/tote; packing handoff, không tự đánh dấu dispatch.
- Cycle count: periodic/random/discrepancy, ICQA, blind count che expected quantity cả UI lẫn model; đếm hai lần, quy tắc WMS quyết định và audit. Hai lần không nhất quán hoặc có chuyển động xen giữa thì reconciliation, không ghi đè âm thầm.
- EXC-001: partial/remainder, reserve allocation, source hold, urgent count; hết reserve → pending/escalation.
- EXC-002: chỉ số lượng xác định chuyển Available → Quality Hold, replacement; không phải scrap/disposal; ambiguous identity chặn.
- EXC-003: suspend/skip, hold, ICQA, chỉ cấp location thay thế đã xác minh.
- EXC-004: blocked path exclusion, chỉ quay lại khi clearance có thẩm quyền.
- EXC-005: pending identification/relabel, thông báo Inbound, liên kết shortage tránh trừ đôi.
- Separate authenticated endpoints dự kiến: tasks, scan-validation, picks, put-aways, exceptions, counts/recounts, clearances. Chưa đăng ký endpoint giả trả success trong A.
- Mỗi mutation cần validated IDs, ownership/role, worker confirmation, idempotency, transaction/version/reservation checks; thử concurrency và offline/retry. Inventory và route fixtures phải ghi synthetic.

## C — deploy thực và chứng minh persistence

Theo RENDER.md: deploy qua Git/container, secrets server-side, disk/database bền vững, kiểm tra health/auth/request thật/RAG/session/restart. Báo rõ paid API test. Chưa thực hiện.

## D — Unity/Android và tương tác

Kiểm tra Unity 6.4 `6000.4.7f1` được báo cáo trước thay đổi. Giữ login, nhiệm vụ, tiến độ, scanning/manual entry, text/voice, ngôn ngữ Anh/Việt/Trung với locale cấu hình, Avatar appearance/voice và skip intro, explicit image submission, 2D fallback rồi AR liên tục với alignment/positioning/walkable paths/relocalization. Không có continuous video. Không coi map scan là positioning hoàn chỉnh.

Giữ các vai trò component AIEvent, BehaviorTracker, AITriggerEngine, AIFeedbackClient, AIFeedbackPresenter, AIActionRouter, ProjectContextProvider, AIDebugPanel. Typed events có timestamp; thresholds/cooldowns/source/image controls cấu hình được; ba lần lỗi/30 giây chỉ là ví dụ. Client phải kiểm tra lại action/target/permission/state ngay trước trình bày để tránh proposal stale. Báo mạng mất, không claim inventory success offline; AR mất tracking phải báo unknown/stale và dùng 2D.

Giữ U-flow như layout cấu hình, không tự tạo kích thước/tọa độ. News screen là phụ. Không tự đưa marketplace, volumetric packing, gesture control hoặc barcode unique từng món vào scope. Test Android thật trước tuyên bố camera/mic/AR hoạt động.

## Ánh xạ 20 acceptance tests trong specification

| # | Phạm vi / trạng thái |
|---|---|
| 1 | A: FIFO/FEFO có nguồn — automated |
| 2 | A: thiếu dữ liệu/occupied không tạo shelf/route — automated |
| 3 | A: continuity, ownership, restart — automated; long-session semantic summary còn giới hạn |
| 4 | B: remainder đúng một lần; A chỉ kiểm tra guidance 5/3 và retry feedback |
| 5 | A hướng dẫn pending, B kiểm tra allocation thực |
| 6–7 | A hướng dẫn Quality Hold/không đoán; B kiểm tra mutation quantity/identity |
| 8 | A manual/relabel guidance; B double-deduction protection |
| 9–11 | B deterministic mismatch/holds/route exclusion/clearance |
| 12 | A adapter không cung cấp expected quantity, test provider payload; B blind-count UI/auth thực |
| 13 | B recount/audit |
| 14 | A forged role/unknown action/target bị từ chối; không có mutation endpoint |
| 15 | A không có giao dịch thành công; provider failures báo lỗi; B operation results |
| 16 | A retry cache; B business idempotency/network |
| 17 | A provider error tests giả lập; live credentials/model chưa kiểm tra |
| 18 | A unverified numbers excluded — automated |
| 19 | D AR tracking loss |
| 20 | A session/users/quota persist local SQLite — automated; B inventory, C deployed disk restart |

Không dùng tests A để tuyên bố workflows B hoặc thiết bị D đã hoạt động.
