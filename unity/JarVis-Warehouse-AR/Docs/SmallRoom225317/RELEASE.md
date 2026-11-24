# JarVis 0.3.0 — phòng nhỏ 225317

APK đầy đủ: `D:/unity of teep/JarVis-Warehouse-AR/Builds/Android/JarVis-Warehouse-0.3.0.apk`

- Package: `com.teep.jarviswarehousear`, version 0.3.0, code 14, ARM64.
- APK: 90,161,301 bytes; SHA256 `D805B011E027DF34B9BD10317AFF678E8026A2F519FCFA8A9570734D18DA5D42`.
- Unity build cuối: Succeeded, 0 lỗi, 17.60 giây. Báo cáo tổng dung lượng build gồm file trung gian; kích thước APK là số bytes ở trên.
- Entry vẫn là JarVisFigmaApp. Giữ Splash/Login/Warehouse/AtlasAR/Immersal cũ và thêm `Assets/Scenes/SmallRoom_AR_225317.unity` (tổng 7 scene).

## Test

1. Cài APK, mở JarVis → Nhiệm vụ → **MAP PHÒNG NHỎ · NHIỆM VỤ THỬ**. Có lối vào tương tự ở Bản đồ kho.
2. Đứng tại A phía trong cửa. Quét barcode A1 `4714551570610` (CPNB-1004), rồi A2 `4714551570603` (CPNB-1003). Không cần bấm Quét tiếp.
3. Sau khi đủ hai sách, AR tự mở. Vẫn đứng tại A và hướng camera về chậu B, giữ máy yên một lát để nhận sàn. Không có hộp thoại căn A/B/C.
4. Đi theo đường tối ưu trên NavMesh: A1 giao tại B (chậu đỏ/xanh); A2 tại C (túi đen có hình trắng). Trật tự tính theo đường đi thực tế, không ép luôn B trước.
5. Tại đích, đặt sách và đưa QR B/C vào camera; app tự đọc và xác nhận. Nhãn mới ở `SmallRoom-B.png`, `SmallRoom-C.png`. Chấp nhận nhãn cũ chính xác `JWM:MAIN:B/C` và `JWM:ROOMPRESENT:153613:B/C` như nhãn giao hàng; không dùng map Immersal cũ để định vị phòng này.

## Setup và bằng chứng

- Nhập đúng OBJ/MTL/JPG từ ZIP Scaniverse 2026-11-19 225317. Bản quét gốc được giữ làm tham chiếu Editor; đường đi được bake từ các tam giác sàn quan sát được, không vẽ sàn phủ xuyên vật cản.
- Điểm B/C chỉ đối tượng. Đường kết thúc ở sàn trống ngay cạnh đối tượng để có chỗ đứng; app chỉ ghi B/C.
- `paths.txt`: 6 đường có hướng đều hoàn chỉnh; thử lại đủ 6 đường sau xoay 37° và dịch map. Quãng đường A→B khoảng 4.422 m, A→C khoảng 4.252 m, B↔C khoảng 1.046 m trong bản quét.
- `book-photos.txt`: cả hai ảnh sách do người dùng cung cấp giải được EAN-13 đúng.
- `mission-core.txt`: chặn sách sai, A1 lặp không tính thành A2, thiếu sách, sai QR, sai khoảng cách, mất tracking; thử đủ hai thứ tự giao và phép biến đổi A/hướng B.
- Scene có một ARSession, XROrigin, camera AR và EventSystem; không có ImmersalSDK/XRMap cũ. Đường và pin chỉ hiện khi tracking + anchor đáng tin cậy.
- Giữ phiên AR và anchor khi tracking tạm mất, tiếp tục thử hồi phục, tự hiện đường khi ổn định; tiến độ nhiệm vụ lưu riêng trên máy. Nếu hệ điều hành thực sự mất anchor, không dựng vị trí giả; cần mở lại AR tại A hướng B.

## Giới hạn đã xác định

- Căn ban đầu dựa trên quy ước người dùng đã xác nhận: đứng A, hướng B. Barcode xác minh sách; bản thân barcode không đo được vị trí/hướng điện thoại.
- Chưa kiểm chứng độ khớp vật lý, camera quét liên tiếp hoặc phục hồi tracking của bản này bằng đi thử trong phòng.
- Samsung vẫn báo ADB `unauthorized`; APK mới chưa cài tự động lên máy. Không xóa app/dữ liệu cũ.
- Giữ các màn và tích hợp AI hiện có; chưa kiểm thử lại đăng nhập, OpenAI, ảnh và voice thật trong bản này.
- Lượt build trước gặp lỗi dừng asset worker; lượt cuối đã chạy lại và qua StrictMode với 0 lỗi. Không thay đổi bảo vệ Windows hay truy cập ổ E.

Menu Unity: **Tools → JarVis → Small Room → 01 Set Up and Verify / 02 Review Current Setup / 03 Build Full JarVis APK**. Setup từ chối ghi đè scene nhỏ đã tồn tại; build giữ bản sao scene bẩn trong `Library/SmallRoomBackups` trước khi chuyển scene.
