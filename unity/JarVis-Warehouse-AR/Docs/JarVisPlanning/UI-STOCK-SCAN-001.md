# TASK UI-STOCK-SCAN-001 — dùng nút QR tròn trên màn Tra tồn kho

Status: ASSIGNED to `thiết kế ứng dụng (coding)` (planning, 2026-10-24 Asia/Saigon; implementation pending)
Owner chat: `thiết kế ứng dụng (coding)`
Model/effort gợi ý: GPT-6.1 Sol · Medium; tăng High nếu điều hướng/Back có lỗi liên quan
Priority: P1

## Nguồn và hiện trạng

Yêu cầu trực tiếp: người dùng muốn bỏ nút xanh `Scan to look up stock` trong hai ảnh chụp; khi đã vào màn tra tồn kho, dùng ngay nút quét QR tròn ở thanh điều hướng dưới, tránh hai lối quét rườm rà.

Đã đọc mã 2026-10-24:
- `Assets/JarVis/Scripts/UI/ImportedWarehouseLists.cs`: `Render()` thêm `PrototypeUi.Action(... "Quét mã để tra cứu" / "Scan to look up stock", ... nav.Show("5:331"))` ở cuối danh sách tồn kho và tăng chiều cao content.
- `Assets/JarVis/Scripts/UI/ImportedFigmaNavigation.cs`: thanh dưới chung có `center-scan-btn` đã bind `Show("5:331")`; màn tồn kho là `5:780`, màn quét là `5:331`.
- `Assets/Editor/ProjectBootstrap/ScannerUiVerification.cs`: có kiểm tra nút QR ở Settings, chưa chứng minh chính nút trên màn tồn kho.
- Unity hiện ở `D:\unity of teep\JarVis-Warehouse-AR`; E: không hiện nhưng nhiệm vụ này chỉ cần mã/fixture Unity trên D:.

## Outcome

Trên màn **Tra tồn kho**, chỉ còn một hành động quét dễ thấy là nút QR tròn ở thanh dưới. Bấm nút mở đúng màn camera quét hiện có; người dùng vẫn tìm bằng chữ và tải lại danh sách được.

## Phạm vi

1. Bỏ nút xanh `Quét mã để tra cứu` / `Scan to look up stock` được tạo trong stock list, ở mọi trạng thái dữ liệu, VI/EN, sáng/tối. Không để khoảng trống thừa ở cuối scroll do chiều cao cũ.
2. Xác minh thanh điều hướng chung hiện và nút QR tròn nhận chạm khi đang ở `5:780`; bấm mở `5:331`. Nếu có tình huống màn tồn kho che nút thì sửa đúng owner điều hướng nhỏ nhất.
3. Xác minh Back từ scanner sau khi mở từ màn tồn kho trở lại `5:780` theo history hiện tại. Giữ hành vi mở scanner từ các màn khác.
4. Kiểm tra riêng empty/loading/error/data list, search và Refresh không bị ảnh hưởng. Không cần gọi backend sống hoặc sửa DB.

## Không thuộc phạm vi / bất biến

- Không đổi UI/logic nhận mã của `ImportedCameraScanner`, route scanner `5:331`, camera permission, OMS API, dữ liệu kho hoặc hàng thật.
- Không rebuild Figma/scene cho sửa runtime C# này; không tạo nút QR mới hoặc màn scanner thứ hai.
- Không thay đổi hành vi quét từ Home/Tasks/Settings/Map; không bỏ Refresh, tìm kiếm hoặc thông báo login.
- Không chạy hai Unity Editor cùng project; kiểm tra trạng thái Editor/scene trước khi test.

## Tiêu chí chấp nhận

1. Không còn nút xanh/nhãn `Scan to look up stock` hoặc `Quét mã để tra cứu` trong `5:780`, kể cả khi danh sách dài hoặc trống.
2. Nút QR tròn của shared bottom navigation hiển thị, không bị che, vùng bấm có raycast, mở `5:331` từ `5:780`; Back quay lại `5:780`.
3. Search, Refresh và các tab dưới giữ hành vi cũ; dữ liệu mock chỉ dùng cho kiểm thử Editor, báo rõ không phải API live.
4. Chạy compile và một kiểm tra UI/điều hướng tập trung có assertion thực về stock → scanner → Back, cộng ảnh màn stock VI/EN ở kích thước Samsung logic 412×915. Xem ảnh trước khi báo đạt. Không báo camera thật đạt nếu chưa thử Samsung.
5. Báo file đổi, kiểm thử PASS/FAIL, phần chưa xác nhận và có/không tạo APK.

## Bàn giao

Giữ cùng TASK ID trong báo cáo theo `Docs/JarVisPlanning/05_DISPATCH.md`. Nếu chat đang làm Routes/AR, hoàn tất nhóm thay đổi đang chạy hoặc bảo đảm không có Editor/scene xung đột trước khi sửa phiếu này. Không lấy việc Routes làm bằng chứng nghiệm thu phiếu UI này.
