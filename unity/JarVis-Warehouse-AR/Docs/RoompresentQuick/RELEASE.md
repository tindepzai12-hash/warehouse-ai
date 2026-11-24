# JarVis 0.3.1 — Roompresent thuyết trình

- APK đầy đủ: `D:/unity of teep/JarVis-Warehouse-AR/Builds/Android/JarVis-Warehouse-0.3.1.apk`.
- Package `com.teep.jarviswarehousear`, version 0.3.1/code15, ARM64. 90,165,325 bytes.
- SHA256 `6977B88577139997C9B29DF5A2673FC7FF1D9ABBC3B68A7947AA7ABFD6978C54`.
- Build cuối Succeeded, 0 lỗi, 54.83 giây; chữ ký APK hợp lệ. Đã đọc trực tiếp IL2CPP metadata trong APK để xác nhận có cả hai scene mới và tên file tiến độ Roompresent mới.

## Cách test

1. JarVis → Bản đồ → **ROOMPRESENT · THUYẾT TRÌNH** → **Test Roompresent · A1 → B, A2 → C**. Nhiệm vụ Roompresent cũng nằm đầu mục Nhiệm vụ.
2. Tại A ở mép bàn thuyết trình (E cũ), quét A1 `4714551570610` và A2 `4714551570603`. Camera tự quét tiếp; đủ hai sách thì AR tự mở.
3. Giữ điện thoại tại vị trí A và hướng về xe B; đợi nhận mặt sàn rồi đi theo mũi tên. Không có hộp thoại căn ba điểm hay quét QR A trong luồng thử này.
4. A1 giao xe B bên trái; A2 giao xe C bên phải theo ảnh người dùng đã xác nhận. Đặt xe đúng vị trí trong bản quét. Tại đích, đặt sách và đưa QR của đích vào camera để app tự xác nhận. Dùng Roompresent-B.png / Roompresent-C.png hoặc nhãn JWM:MAIN:B/C cũ.

## Đã kiểm tra và giới hạn

- Scene `Assets/Scenes/Roompresent_AR_QuickTest.unity` dùng đúng GLB Roompresent và sàn quan sát đã có. A/B/C giữ các đối tượng đã xác nhận; đường kết thúc trên sàn trống cạnh đối tượng.
- 6 đường có hướng hoàn chỉnh, kiểm tra lại sau xoay/dịch map; lấy mẫu đường mỗi 5 cm với dung sai sàn 2,5 cm. A→B 6.058 m, A→C 6.397 m, B↔C 1.706 m: từ A ưu tiên B rồi C.
- Kiểm tra mã sách sai/lặp/thiếu, sai QR và khác phòng, khoảng cách/tracking không hợp lệ, hai thứ tự giao. Kiểm tra hồi quy map nhỏ và giải mã lại đúng cả hai ảnh barcode thật được cung cấp.
- Tiến độ test mới lưu riêng ở `jarvis-roompresent-quick-delivery-v1.json`, không xóa tiến độ Roompresent cũ, không ghi tồn kho/nhiệm vụ OMS thật.
- Dùng chung scanner và AR guide đã có, thêm cờ chọn nhiệm vụ/phòng; giữ phiên AR/anchor và tiến độ khi tracking tạm mất, tự tiếp tục khi hồi phục. Mất hẳn anchor cần mở lại tại A hướng B; không dựng tọa độ giả.
- Đây là test khởi đầu theo quy ước A/hướng B như bài phòng nhỏ. Barcode chỉ xác minh sách; không tự đo vị trí/hướng. Không tuyên bố đã căn GLB với hệ tọa độ Immersal hay đã kiểm chứng sai số vật lý. Luồng Immersal cũ giữ riêng ở mục nâng cao.
- Full app giữ 8 scene: JarVisFigmaApp/Splash/Login/Warehouse/AtlasAR/Immersal cũ/phòng nhỏ/Roompresent test. Chưa kiểm thử lại AI, ảnh, voice, đăng nhập thật hay đi thử phòng trong bản này.
- Samsung ADB vẫn unauthorized, chưa cài APK tự động. Unity đang mở sẵn scene Roompresent test.

Menu Unity: Tools → JarVis → Roompresent Test → Review Presentation Test / Build Full JarVis APK. Setup từ chối ghi đè scene đã có; bản sao các scene bẩn được lưu trong Library/RoompresentQuickBackups trước khi chuyển scene.
