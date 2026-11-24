# JarVis Routes 0.3.5 — kết quả kiểm tra

## Lỗi đã sửa trong mã
- Chốt kết quả quan sát môi trường trong phiên tracking liên tục. Point-cloud ít đi hoặc hết hạn khi hướng vào QR không còn tự đẩy từ bước 2/2 về 1/2. Camera cũ, mất tracking, pose nhảy, Pause và phiên mới vẫn hủy điều kiện sẵn sàng.
- Khi vị trí lưới mũi tên vượt góc rẽ, lấy cue hợp lệ trên đoạn hiện tại thay vì bỏ trống. Trong 1,2 m tới góc, cue AR tại góc chỉ theo đoạn kế tiếp đã ghi; không mở các nhánh xa.
- Thêm glyph hướng nhỏ vào hàng tiêu đề footer uGUI có sẵn, không thêm màn hình/nút. Khi sàn/góc ngoài camera vẫn có hướng trên thẻ; khi mất tracking/lệch tuyến vẫn tắt hướng. Xoay máy theo góc không làm cue đảo về đoạn cũ.
- Vẫn giữ ngân sách AR: còn ≤2 m dùng đích B và hướng trên thẻ, >2–8 m tối đa 1 mũi tên AR, >8 m tối đa 2. Không chồng lấn hay che nút, không đổi điểm đến theo hoạt ảnh.

## Đã thực hiện
- Xem toàn bộ video 12,53 giây qua contact sheet mỗi 0,5 giây và khung cuối; xem ảnh lỗi v0.3.4. Không có log ARCore từ điện thoại nên không khẳng định mọi lần mất vị trí đều cùng nguyên nhân.
- Unity compile không lỗi; các bộ logic 50, regression 41, readiness 40, presentation 159, UI 1401, focused 034 205 đều PASS.
- Regression 035 mở rộng: 849 dòng kiểm tra PASS, gồm chờ QR 10,4 giây với 0 điểm đặc trưng mới sau khi đã đạt điều kiện, mất tracking thật, không cho môi trường thiếu chi tiết vượt cổng, khóa A vẫn cần marker pose.
- Góc trái/phải: vị trí 0 / 0,4 / 1 / 1,6 / 2,1 / 2,5 m; hai tỷ lệ 412×915 và 412×732; camera 35° và 55°. Đã đi mô phỏng qua góc sang đoạn kế tiếp. Có 48 ảnh kết hợp AR/HUD; ảnh ghi rõ mô phỏng Editor, tracking được cấp vào thành phần vẽ để kiểm tra, không phải hình đi thật.
- 56 ảnh trạng thái UI hai tỷ lệ; xem các contact sheet, không tràn chữ/nút ở các fixture. Ảnh chụp cận 3,7 m ở góc 55° xác nhận cue AR rẽ phải; góc camera 35° không nhìn thấy cue trên sàn thì vẫn có mũi tên footer.
- PlayMode chạy qua nhiều frame: PASS, trả lại cảnh JarVisFigmaApp sạch. Đây không phải kiểm thử camera/ARCore vật lý.
- Android strict build Succeeded, 0 lỗi, 77,23 giây. APK 50.389.747 byte, package `com.teep.jarvisroutes`, version 0.3.5/code14, minSdk29/target36.
- APK signature v2 Verifies; certificate SHA256 `13163f8a75bbbdfdda62fb60ffbc9f03822b2b22fa6410fd2483339225dda49d`, khớp các bản trước, cho phép cài cập nhật giữ dữ liệu.
- APK SHA256: `95EC40FF1145A4DEB3F46D5EA8E76CFB573FEF5BBE19A93B18CAC047F9C512AD`.

## Chép Samsung / chưa nghiệm thu đi thật
- Samsung hiện lại kết nối sau build. Đã chép `Internal storage/Download/JarVis-Routes-0.3.5.apk` lúc 18:24 giờ Việt Nam 24/10/2026; đọc ngược file qua MTP và đối chiếu SHA256 khớp hoàn toàn. Log `Reports/phone-transfer.txt`. Chưa cài ứng dụng thay người dùng, chưa xác nhận đi thật.
- Cần quay một lượt chuẩn bị → nhìn QR A trong vài giây → Bắt đầu đi → đi qua góc → tới B, kiểm tra trên máy thật. Không gỡ app/xóa map để cập nhật.
- Sàn vẫn ước lượng 1,30 m, QR ảnh tham chiếu 18 cm, không phát hiện vật cản; đường xấu đã ghi không tự biến thành map phòng chính xác. Không tuyên bố hết lỗi AR thực tế.

## Vị trí bằng chứng
- Log gốc: `D:\unity of teep\JarVis-Warehouse-AR\Logs\RoomRoutes`, `RoomRoutes034`, `RoomRoutes035`, `WalkingRoutes`.
- Kế hoạch, báo cáo và log sao chép: `Docs\RoomRoutes035` trong project; APK: `Builds\Android\JarVis-Routes-0.3.5.apk`.
