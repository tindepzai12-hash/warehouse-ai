# TASK UI-HOME-TYPO-002 — sửa chữ và bố cục thẻ Home

Status: ASSIGNED to `thiết kế ứng dụng (coding)` (planning, 2026-10-24 Asia/Saigon; chưa triển khai)
Owner chat: `thiết kế ứng dụng (coding)`
Design review: `thiết kế hình ảnh (ui/ux)` chỉ góp ý bố cục/chữ, không sửa Unity hoặc tạo asset
Model/effort gợi ý: GPT-6.1 Sol · Medium cho thực thi; High nếu tìm thấy lỗi TMP/font asset có ảnh hưởng toàn app
Priority: P1

## Nguồn yêu cầu và kết quả nhìn thấy

Người dùng gửi hai ảnh chụp Home: cụm `Quick actions` và thẻ nhiệm vụ có câu `Admin manages tasks on the web.`. Người dùng nhận thấy chữ có vấn đề và bố cục chữ không đẹp. Mục tiêu là thẻ Home có typography rõ, đều, không ngắt dòng vụng, căn hàng nhất quán và dễ đọc trên điện thoại.

## Hiện trạng đã kiểm tra ngày 2026-10-24

- Project Unity hiện ở `D:\unity of teep\JarVis-Warehouse-AR`; E: không hiện, nhưng thay đổi này không cần backend/DB E:.
- Owner runtime là `Assets/JarVis/Scripts/UI/ImportedHomeDashboard.cs`, `Build()`/`Shortcut()`/`Paint()`.
- Bốn quick actions dùng grid 2 cột: ô 177×63, gap 12. Label đặt x=49, w=104, y=13, h=40, font 13 với auto size xuống 11. Chữ dài bị ép thành 2 dòng, trong khi `Tasks` và `Report issue` một dòng; ảnh cho thấy nhịp chữ và khoảng trắng giữa các ô thiếu thống nhất.
- Thẻ task 366×(68+extra) có text x=56, y=12, w=275, h=46+extra nhưng `PrototypeUi.Text()` mặc định TopLeft. Với câu admin một dòng, chữ bám phía trên và để khoảng trống lớn bên dưới; icon và chevron nằm gần giữa.
- Cả hai phần lấy TMP font từ imported scene (`nav.screens[0].root.GetComponentInChildren<TMP_Text>(true).font`). **Chưa đủ bằng chứng để kết luận font asset hỏng**. Cần kiểm tra font face/weight/fallback, metrics và text scale trong Unity trước khi đổi font toàn cục.
- `CODEX_CONTEXT.md`, `UNITY_UI_WORKFLOW.md`, `AGENTS.md` và nguồn thiết kế B là authority. Không rebuild scene cho sửa runtime C#.

## Kế hoạch thực thi

1. **Chẩn đoán chữ:** chụp Home trong Unity ở EN/VI, Sáng/Tối, cỡ chữ Nhỏ/Vừa/Lớn; xem font asset thật, weight Bold, glyph fallback, auto size, line spacing, clipping và alignment. Ghi rõ lỗi nào quan sát được, lỗi nào là giả thuyết.
2. **Thiết kế layout:** chốt quy tắc chung cho 4 ô: icon, nhãn, chevron cùng trục thị giác; nhãn 1–2 dòng có độ cao/baseline hợp lý; ngắt dòng tự nhiên và font size không giảm quá mức. Cho câu admin và mọi trạng thái task một vị trí theo chiều dọc dễ đọc, icon và chevron cân hàng. Ưu tiên sửa layout/khung chữ cục bộ, chỉ đổi câu chữ khi giúp đọc rõ mà vẫn giữ nghĩa VI/EN.
3. **Thực thi cục bộ:** sửa owner Home nhỏ nhất; giữ màu thẻ, icon, hành động và route. Không sửa font toàn app nếu lỗi chỉ ở bố cục Home. Nếu có bug font asset chung, ghi bằng chứng và tách phạm vi trước khi sửa.
4. **Kiểm tra:** compile và review UI tập trung, xem ảnh xuất ra ở kích thước logic 412×915 và thiết bị 1080×2400 nếu môi trường cho phép. Xem các trạng thái admin, đang tải, lỗi, rỗng và có nhiệm vụ; kiểm tra nút vẫn nhận chạm. Không cần backend live hoặc gọi model.

## Tiêu chí chấp nhận

1. `Inbound / Outbound`/bản VI và `Check inventory`/bản VI không bị ngắt dòng khó đọc, cắt chữ hoặc tự thu nhỏ thành cỡ chữ lệch rõ so với các ô còn lại. `Tasks` và `Report issue` cân trục với hai ô dài; nhãn không chạm icon/chevron.
2. Câu `Admin manages tasks on the web.` và các câu trạng thái nhiệm vụ được căn dọc hợp lý trong thẻ, không bám mép trên tạo khoảng trống dưới quá lớn; không cắt chữ ở mọi text scale được hỗ trợ.
3. Chữ giữ dấu tiếng Việt đúng, không có ô vuông/missing glyph, không dùng fallback sai hoặc Bold giả gây sai độ dày. Nếu không xác minh được font asset là nguyên nhân, báo rõ đây là lỗi layout/typography đã sửa chứ không khẳng định thay font.
4. Sáng/Tối, VI/EN và cỡ chữ Nhỏ/Vừa/Lớn đều đọc được tại 412×915; không tràn/chồng ở 1080×2400 nếu có kiểm tra thiết bị. Tất cả bốn quick actions và thẻ task giữ route/callback cũ.
5. Có ảnh trước/sau và kết quả compile/review sau thay đổi; báo file đổi, phần đã thử và giới hạn thiết bị. Không lấy ảnh mock hay PASS cũ thay cho kiểm tra runtime mới.

## Điều phối

Phiếu `UI-STOCK-SCAN-001` đã giao cùng owner và chưa hoàn thành. Chat coding làm tuần tự, không chỉnh cùng project khi đang chạy Unity Routes/AR hoặc khi Editor có scene chưa lưu. UI/UX review chỉ đưa đề xuất bằng chữ, không ghi file vào project. Planning kiểm tra kết quả và cập nhật trạng thái sau khi thấy mã/ảnh/log thật.

## Báo cáo yêu cầu

`TASK UI-HOME-TYPO-002 — DONE/PARTIAL/BLOCKED`; nêu nguyên nhân thực, quyết định bố cục/chữ, file đổi, kiểm tra PASS/FAIL, ảnh trước/sau, trạng thái Unity Editor, giới hạn Samsung và rollback.
