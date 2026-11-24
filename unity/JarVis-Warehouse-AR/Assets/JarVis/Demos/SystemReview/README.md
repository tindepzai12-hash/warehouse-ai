# JarVis — Demo giao diện Unity

Mở **index.html** để xem 45 trạng thái được chụp mới ở hai chiều cao màn hình. Các ảnh chuyển động được ghi từ Play Mode của Unity. Đây là bản để duyệt thiết kế, chưa thay APK đang cài trên Samsung.

## Mở trong project hiện có

Scene: `Assets/JarVis/Demos/SystemReview/JarVisSystemDemo.unity`.

Prefab: `Assets/JarVis/Demos/SystemReview/JarVisSystemDemo.prefab`.

Menu: **Tools → JarVis → UI Demo → Open complete review**. Bật Play; dùng bảng **JarVis Demo** bên cạnh Game View để cấp mã thử. Nút thử nằm trong Editor, không nằm trên giao diện app.

Đã đặt font tiếng Việt vào prefab. Canvas co theo chiều rộng 412 đơn vị, có safe area; bản chụp kiểm tra ở 1080×2400 và 1080×1920.

## Thử luồng bắt buộc

1. Nhiệm vụ → Bắt đầu nhiệm vụ.
2. Bảng Editor: Barcode A1 → bấm OK trên màn app → Barcode A2 → OK.
3. Bảng Editor: QR A + tư thế thử → Mở chỉ dẫn AR.
4. Chọn B hoặc C; bấm xác nhận giao tại điểm đó.
5. Cấp đúng QR điểm giao → OK → cấp đúng barcode sách.
6. Làm tương tự điểm còn lại → popup hoàn thành → Về A.

A1: **4714551570610 / CPNB-1004 / tới B**.
A2: **4714551570603 / CPNB-1003 / tới C**.
QR A dùng **JWM:MAIN:E** cũ; B/C **JWM:MAIN:B/C**.

Luồng dùng lại `RoompresentBarcodeGate`, `RoompresentDeliveryRecord` và `RoompresentDeliveryVerification`; không tạo điều kiện xác nhận mới. Thử cả B trước và C trước. Barcode không đặt tọa độ. QR A chỉ mở AR khi nhận được tư thế tin cậy, không chỉ đọc chuỗi mã.

## Chèn vào JarVis

Các màn, khung quét, avatar, sóng và chuyển động là UGUI/TextMeshPro chạy được trong Unity, không phải ảnh toàn màn làm nút. Các phần mới nằm riêng tại `SystemReview` để duyệt và tái sử dụng.

Các cổng sự kiện `CommandRequested`, `RouteRequested`, `CameraRefreshRequested`, `RescanARequested`, `ReturnARequested`, `MessageRequested`, `IncidentRequested` giữ việc thực thi ở các owner đang có. Màn phụ chuyển lệnh về trang gốc; bản demo không tự ghi kho, gửi báo cáo, đăng nhập hay gọi AI.

| Lệnh/sự kiện | Owner cần giữ khi gắn giao diện |
|---|---|
| inventory | ImportedFigmaNavigation.Show(5:780) |
| product-label | Show(5:450) / WarehouseProductWorkspace |
| inbound / outbound | Show(5:935) / Show(14:387) |
| incident | Show(5:1029); trong AR dùng popup của RoompresentArAssistant |
| profile / activity / settings | prototype:profile / prototype:activity / 5:1308 |
| online / signout | PrototypeDialog.Presence / SignOut |
| dẫn đường / xác minh giao | SmallRoomAR; giữ camera, anchor, proximity và proof của owner |
| ảnh/voice AI | RoompresentArAssistant / ImportedJarvisChat; dùng phiên OMS có sẵn |

Không cắm dữ liệu thử vào lớp lưu bền. Không bật hai hệ thống xác nhận đồng thời. Khi gắn production, phần trình bày nhận trạng thái từ owner thật; bảng cấp mã Editor chỉ phục vụ demo.

## Giữ tracking

Các popup không sở hữu ARSession, XROrigin, map hay camera; chỉ tạo lớp UGUI. Bài kiểm tra đã đối chiếu các owner thật trong scene Roompresent cùng một reference/anchor thử không rỗng. Camera, map và reference giữ nguyên qua chat, ảnh lớn, báo cáo, hoàn thành và về A.

Đây không phải xác nhận độ chính xác tracking Samsung. Tọa độ đầu vào, mã và khoảng cách trong rehearsal được cấp rõ ràng từ Editor. Ảnh phòng được ghi nhãn tham chiếu. Sóng âm demo chưa phải micro thật.

Nút làm mới camera có cổng gọi owner camera; trong rehearsal không có camera thật để mở lại. Khi tích hợp cần nối việc tắt/bật feed và lấy nét ở camera owner, tuyệt đối không gọi ARSession.Reset.

## Dữ liệu và kiểm tra

- `RESULT.txt`: 45 trạng thái × 2 kích thước; chữ tràn, thứ tự mã, tracking/khoảng cách, B→C và C→B, lưu nguyên các scene/mã nghiệp vụ/JSON.
- `PLAY-RESULT.txt`: vệt quét chuyển động thật, không tự cộng tiến độ; popup và nút OK; chuyển B→C trong Play; không phát sinh lỗi runtime.
- `PLAN.md`: phân chia Google Maps / Apple Maps / Grok / ChatGPT / Todoist và cấu trúc giao diện.
- Có một chặn riêng cho scene demo ở `LocalOmsBackend`: xem thử UI không tự khởi động backend thật. Đường đi hoạt động của scene app chính không đổi.

Các trang kho, tem, nhập/xuất và voice giữ cổng tới chức năng hiện có, không tuyên bố đã hoàn thiện những API/khả năng vốn chưa kết nối trong bản JarVis hiện tại.
