# JarVis Routes 0.3.1 — kết quả kiểm tra 24/10/2026

## Bằng chứng lỗi đã xem
- Video người dùng dài 17,30 giây: contact toàn clip, đủ 17 frame lấy mẫu mỗi giây và 12 frame chi tiết giai đoạn chuyển trạng thái 10,5–13,5 giây.
- Có lúc UI báo AR sẵn sàng nhưng dẫn đường đã dừng; nhận A xong hiện đường rồi mất. Video không phân biệt được nhánh tracking mất, pose nhảy hay khung hình gián đoạn. Không tự kết luận nguyên nhân phần cứng hoặc GPS.
- Chưa lấy được JSON tuyến thật trong bộ nhớ riêng của ứng dụng trên Samsung; không tuyên bố đã đo lại đường đó.

## Thay đổi có mục đích
- Khung góc QR, vạch quét theo trạng thái ảnh camera còn mới, nhãn chờ/đang quét/sai thẻ/đã đọc/đã khóa. Chỉ controller nhận QR đúng + pose mới, ổn định mới được bật thành công; không dùng animation giả nhận diện.
- Tracking chớp ngắn: ngừng tiến độ, ẩn đường; yêu cầu vị trí đủ gần và tracking ổn định liên tục trước khi tiếp tục. Timeout, pose nhảy và pause yêu cầu quét lại A ngay trong module, không giữ trạng thái chết.
- Reset tiến độ khi khóa lại A. Thông báo lỗi theo nguyên nhân; header không báo dẫn đang hoạt động khi đã yêu cầu quét lại.
- Polyline dẫn là bản sao giản lược hình học sai lệch tối đa 12 cm so với dữ liệu vào, giữ góc thật; không sửa JSON raw và không ép mọi tuyến thành thẳng. Không thể dùng bộ lọc này để khôi phục độ chính xác của một bản ghi AR đã drift lớn.
- Chiếu tiến độ lên đoạn hiện hành, đi lần lượt qua các đoạn; không nhảy sang đoạn tương lai gần mình ở tuyến vòng. Phân biệt góc rẽ của đường và lời nhắc quay camera.
- Giữ giãn cách mũi tên, motion tiến/lùi 8 cm và pin B của 0.3.0. Không đổi backend, app Warehouse chính, driver hoặc dữ liệu người dùng.

## Kiểm thử đã chạy
- Unity 6000.4.7f1: 150 assertion lõi ghi/lưu/xuất tuyến PASS.
- Room logic: 50 assertion marker transform, QR, storage, route và guard PASS.
- Hồi quy bổ sung: 41 assertion PASS: thẳng nhiễu, camera đổi yaw, trái/phải thật, tuyến vòng, mất tracking ngắn/chập chờn/timeout, nhảy pose, quay lại A reset tiến độ, pause, mã sai/cũ, camera ngừng, không giả success, raw không đổi.
- Native uGUI: 20 trạng thái × 2 kích thước 412×915 và 412×732; 821 kiểm tra layout/font/nút/AR mesh/motion PASS. Đã xem toàn bộ 40 ảnh qua 10 contact sheet, thêm ảnh độc lập phần quét/khóa/đang dẫn và render AR mô phỏng. Sửa khung quét còn treo sau khi đã dẫn/đến nơi, ưu tiên lời nhắc dừng khi lệch tuyến, ổn định capture font trong bài render native. Khung/status không đè nút, không chặn chạm; không dùng mockup web thay Unity. Header camera trong các fixture là trạng thái Editor, không phải ảnh camera thật.
- Native Unity PlayMode startup PASS: HUD hai chức năng, một image manager, thư viện hai thẻ; không có lỗi runtime trong lượt kiểm tra. Log licensing token không có sẵn là thông báo của Editor, không phải kết quả camera Samsung.
- Android build PASS, 0 lỗi, package `com.teep.jarvisroutes`, version 0.3.1, code 10, arm64, min SDK 29. Chữ ký APK v2 hợp lệ, cùng chứng chỉ bản 0.3.0 để cập nhật giữ dữ liệu.

APK: 50.361.767 byte.

SHA256: `180EF546A8D58751CF2BD4BE6F46C9DFD5D18A9A234E0DED3F19A5D3803240F4`.

## Chuyển file
Đã chép APK, hướng dẫn và hai thẻ vào Samsung `Internal storage/Download/JarVis-Room-0.3.1`. APK đọc ngược từ điện thoại có cùng SHA256 với bản build. Đây là xác nhận truyền file, chưa phải xác nhận đã cài hoặc đã đi thử. ADB chưa thấy thiết bị; không cài driver hay gỡ ứng dụng để ép kết nối.

## Chưa nghiệm thu thực địa
Quét camera và đi A → B trên Samsung với bản 0.3.1 chưa được xác nhận. Cần lượt quay màn hình mới; PASS mô phỏng không thay thế kiểm thử ARCore ngoài đời. Độ cao sàn vẫn ước lượng từ camera 1,30 m; QR không tạo GPS hay tự khôi phục phiên tracking đã mất.
