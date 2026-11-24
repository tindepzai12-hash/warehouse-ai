# Kiểm tra prompt và gói planning trước bàn giao

Đánh giá ngày 2026-10-24. Đây là self-review cấu trúc prompt, không phải chứng nhận code/thiết bị đã chạy.

## Checklist bắt buộc

| Câu hỏi | Kết quả | Bằng chứng / xử lý |
|---|---|---|
| Có nhận đúng bốn **chat** từ ảnh, giữ chat planning là điều phối? | PASS | README, 01_RESEARCH, 05_DISPATCH. |
| Có phân biệt Unity D:, backend E: và tình trạng E: mất? | PASS | README, 01_RESEARCH, 03_MASTER_PLAN và các prompt. |
| Có chọn model/effort từ thấp tới cao và phân theo từng chat? | PASS | 02_MODELS; nguồn OpenAI Docs và ChatGPT Learn. |
| Có phân biệt model phát triển với runtime Jarvis? | PASS | 02_MODELS và README. |
| Có source of truth và ranh giới mock/live/Editor/Samsung? | PASS | 01_RESEARCH, 03_MASTER_PLAN, 04_SHARED_RULES. |
| Có cơ chế giao việc với ID, owner, dependency, tiêu chí, báo cáo? | PASS | 05_DISPATCH, 06_PROMPTS/E. |
| Có tránh việc hai chat sửa cùng file/scene/DB? | PASS | 03_MASTER_PLAN, 05_DISPATCH. |
| Có tránh tự gửi việc sang chat khác trước khi user pass? | PASS | README, 05_DISPATCH, 06_PROMPTS. |
| Có nhận ra chat hình ảnh đang làm Gao Big Lion? | PASS | 01_RESEARCH và prompt hình ảnh. |
| Có tiêu chí rõ để không nhận sai đã sửa AR/login/voice? | PASS | 03_MASTER_PLAN và prompt coding/bug. |
| Có bảo vệ khóa API, live DB, dữ liệu người dùng? | PASS | 04_SHARED_RULES và các prompt. |
| Có prompt đủ dùng nhưng không chép toàn bộ context dài vào mọi lượt? | PASS | Bốn prompt vai trò + phiếu ngắn; tài liệu chỉ đọc khi liên quan. |

## Rủi ro còn lại và điều kiện áp dụng

1. **Không thể bảo đảm các chat đang mở tự tuân theo file này.** Codex nạp `AGENTS.md` khi khởi tạo phiên; người dùng cần dán prompt/phiếu trực tiếp. Muốn tự động ở các chat mới, lưu bộ tài liệu trong project primary và thêm một dòng tham chiếu vào `AGENTS.md` hiện có sau khi xác minh quyền ghi và không xung đột.
2. **E: chưa hiện**, nên chưa thể đặt tài liệu vào backend E: hoặc đọc cấu hình model runtime live. Bản dùng chung đã nằm trong dự án Unity D: và có bản lưu phụ trong vùng visualization.
3. **Đã dùng quyền ghi được duyệt vào D:** 8 file Markdown nằm dưới `D:\unity of teep\JarVis-Warehouse-AR\Docs\JarVisPlanning`; `AGENTS.md` chỉ thêm một dòng tham chiếu. Các chat đang mở có thể không nạp lại chỉ dẫn này, nên vẫn cần prompt bàn giao trực tiếp.
4. Danh sách model/effort thay theo client/account/rollout; dùng bộ chọn đang hiện và kiểm lại tài liệu chính thức trước thay đổi lớn.
5. Trạng thái chat coding tiếp diễn trong lúc lập kế hoạch; ví dụ TASK cuối chỉ là DRAFT. Trước giao việc phải đọc báo cáo mới nhất, tránh giao trùng.

## Quyết định quality gate

**Prompt đạt để dùng làm bản bàn giao planning**, vì đã có vai trò, phạm vi, nguồn sự thật, giới hạn, tiêu chí nghiệm thu, tình huống bị chặn và mẫu báo cáo. Đã đặt vào project Unity và liên kết từ AGENTS.md; chưa tự thi hành ở ba chat kia vì user nói sẽ pass sau, và không có bằng chứng chức năng Jarvis đã hoàn tất. Không gửi việc hay đổi model của các chat trong lượt này.
