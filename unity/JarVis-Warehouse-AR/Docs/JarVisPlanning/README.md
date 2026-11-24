# JarVis — gói điều phối bốn chat

Ngày lập: 2026-10-24 (Asia/Saigon). Phiên bản: 1.0. Trạng thái: **kế hoạch và prompt đã soạn; chưa giao việc sang ba chat khác**.

## Đọc theo thứ tự

1. `01_RESEARCH.md` — nguồn đã xem, điều đã biết và điều chưa được chứng minh.
2. `02_MODELS.md` — chọn model và mức suy luận từ thấp tới cao, cho từng chat.
3. `03_MASTER_PLAN.md` — phạm vi, thứ tự, mốc nghiệm thu và rủi ro.
4. `04_SHARED_RULES.md` — quy tắc chung của mọi nhiệm vụ Jarvis.
5. `05_DISPATCH.md` — giao việc, nhận việc, báo cáo, chặn xung đột.
6. `06_PROMPTS.md` — prompt mở đầu cho bốn chat và mẫu phiếu công việc.
7. `07_PROMPT_QA.md` — kiểm tra prompt trước khi bàn giao.

## Phạm vi và trạng thái máy

Ảnh người dùng cho thấy bốn **chat trong cùng dự án Codex**, không phải bốn website: `planning`, `thiết kế ứng dụng (coding)`, `thiết kế hình ảnh`, `sửa lỗi`. Chat này là nơi lập kế hoạch; người dùng sẽ chuyển phiếu sang chat khác sau. Không tự gửi tin nhắn sang chat khác, không tự đổi model hoặc cài đặt của chúng.

Dự án Unity hoạt động ở `D:\unity of teep\JarVis-Warehouse-AR` theo `CODEX_CONTEXT.md` ngày 2026-10-21. Backend OMS và cơ sở dữ liệu sống còn ở `E:\warehouse-ai`; ổ E: **không hiện** trong lần kiểm tra 2026-10-24. Bản dùng chung của gói được đặt tại `D:\unity of teep\JarVis-Warehouse-AR\Docs\JarVisPlanning`; bản trong vùng làm việc của chat planning là bản lưu phụ. Không coi mock là nguồn thay thế backend sống.

`AGENTS.md` của dự án Unity đã tồn tại và có ưu tiên riêng. Đã thêm **một dòng tham chiếu** vào cuối file, giữ nguyên nội dung cũ; bản trước thay đổi được lưu ở vùng làm việc của chat planning. Gói này bổ sung quy trình điều phối; không thay thế `CODEX_CONTEXT.md`, `UNITY_UI_WORKFLOW.md`, tài liệu owner/module hoặc chỉ thị mới của người dùng. Codex đọc `AGENTS.md` khi khởi tạo phiên, vì vậy thay đổi không bảo đảm đổi hành vi của một chat đang chạy. Mỗi phiếu bàn giao vẫn phải chứa prompt áp dụng.

## Cách dùng ngay

- Trong chat `planning`: đọc kế hoạch, tạo phiếu ID duy nhất theo `05_DISPATCH.md`, kiểm tra đủ đầu vào và tiêu chí chấp nhận, sau đó cung cấp phiếu cho người dùng chuyển đi.
- Trong chat nhận việc: dán prompt vai trò trong `06_PROMPTS.md` một lần, rồi dán đúng một phiếu đã duyệt. Chat phải kiểm tra trạng thái file thực tế, không xem tài liệu này là chứng cứ code đã chạy.
- Trước khi đóng phiếu: báo file thay đổi, lệnh kiểm tra và kết quả, bằng chứng máy thật/API thật nếu có, phần chưa xác nhận, đường dẫn bàn giao và phiếu kế tiếp.

## Nguồn model chính thức

- [OpenAI Docs — model selection](https://developers.openai.com/api/docs/guides/model-selection)
- [ChatGPT Learn — models trong Codex/Work](https://learn.chatgpt.com/docs/models)
- [ChatGPT Learn — AGENTS.md](https://learn.chatgpt.com/docs/agent-configuration/agents-md)

Model trong Codex để **phát triển** ứng dụng và model chạy **bên trong JarVis** là hai quyết định khác nhau. Không tự đổi provider/API backend chỉ vì thay model của chat.
