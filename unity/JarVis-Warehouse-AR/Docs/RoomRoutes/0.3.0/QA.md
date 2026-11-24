# Kiểm tra bản thử phòng 0.3.0 — 24/10/2026

## Đã chạy, không phải kết quả giả lập điện thoại thật

- Đọc video tham chiếu 14,05s, contact sheet 8 ô. Thiết kế lại chủ động theo phản hồi preview: giãn mũi tên 1,6m, 8cm tiến–lùi/chu kỳ 1,8s; thay cụm xanh chồng bằng ghim B xanh ngọc và vòng đích rỗng.
- Unity 6000.4.7f1: biên dịch C# và build Android cuối thành công, **0 lỗi build**. Vòng shader đầu của hiệu ứng mềm lỗi tên biến HLSL; đã sửa, không phát hành bản lỗi màu hồng. Kiểm tra shader được thêm trước render/build.
- **150 assertions** bộ ghi tuyến hiện có: `Logs/WalkingRoutes/offline-result.txt` mới ngày 24/10. Không tái sử dụng log cũ làm bằng chứng.
- **49 kiểm tra room**: hoàn chỉnh A/B; restore vị trí/yaw ở 0°,90°,-90°,179°; tự dẫn không căn tay; điểm dày không kẹt tiến độ; đích theo vị trí marker B; lưu/mở lại/thay map; không thay bằng bản dở; file hỏng phục hồi; NaN và tuyến cũ không có anchor bị từ chối; tuyến L không chỉ xuyên góc; B trước A/A kết thúc/đứng yên <1m/sai QR/draft bị chặn; mất tracking; hai thẻ; decode QR trong toàn ảnh; pose thiếu/cũ/Limited không dùng. `Logs/RoomRoutes/logic.txt`.
- **24 render uGUI native**: 12 trạng thái × 412×915 và 412×732, kiểm tra font, không cắt chữ, chạm >=44, hai nút hoạt động, khoảng camera giữa hai thẻ; click xác nhận/giữ/hủy ghi vẫn giữ map tốt; thông báo quyền camera không bị home che. 451 dòng kiểm tra hình học/chữ/tương tác gồm lặp theo trạng thái, **không coi là 451 chức năng khác nhau**. `Logs/RoomRoutes/ui.txt`.
- Render AR với camera Unity tổng hợp: hai mũi tên trên tuyến 4m, cách 1,6m; không chờ plane; có ghim B và chữ B; thay độ cao điện thoại không kéo đích; tiến–lùi tối đa 8cm, reduced motion tắt chuyển động; animation không thay tọa độ đích. Đã xem ảnh sau sửa shader và chữ B. `Logs/RoomRoutes/ar-synthetic.png`.
- 48 frame native Unity cho preview 2s/24fps; mã hóa H.264 `AR-PREVIEW-MO-PHONG.mp4`. **Đây là preview mô phỏng, không phải quay AR tại phòng của người dùng.**
- **Unity Play Mode scene thật** khởi động đạt: không error/exception/assert được ghi nhận, đúng một ARTrackedImageManager, thư viện đúng hai thẻ, HUD hai chế độ có mặt. Editor không có ARCore vật lý nên Ready=false là kết quả mong đợi. `Logs/RoomRoutes/startup.txt`.
- `arcoreimg eval-img`: Room-A=100, Room-B=100. Đây là điểm đặc trưng ảnh, **không phải chứng nhận độ chính xác định vị**.
- APK: `com.teep.jarvisroutes`, 0.3.0, versionCode9, arm64, Android10 trở lên; 50.346.991 byte (~48MiB). Camera có mặt, không xin GPS/microphone. Chữ ký v2 hợp lệ, khóa debug dành cho bản thử, không phải bản phát hành store.
- SHA256 APK: `DB6F2B7958973C5144EF7B9C39602BC148E53331E9846708A6935C96AC433D50`.

## Chưa được xác nhận — không đánh dấu PASS

- Samsung hiện không có kết nối ADB/MTP dùng được tại lần gom file. Chưa cài/chạy APK này trên máy người dùng; không tự cài driver để vòng qua.
- Camera thực tế, cấp/từ chối quyền trên Android, khóa đủ toàn ảnh A/B, sai kích thước in, chói sáng, drift và độ chính xác quay lại A ở một phiên khác.
- Ghi → B → lưu → tắt/mở app → A → đi theo mũi tên tới B trong phòng thực tế. Thử pause/khóa máy/hủy dưới ARCore thật và tình huống hết dung lượng.
- Safe area/tai thỏ trên Samsung, tăng cỡ chữ hệ thống và hiệu năng/nhiệt/pin trên máy thật. Các render Editor chỉ kiểm tra hai tỉ lệ và font mặc định, không giả nhận đã thử font hệ thống lớn.

## Điều kiện thử và nghiệm thu

Dùng đúng hai thẻ mới **toàn ảnh 18×18cm**, dán thẳng đứng cố định. Camera cầm khoảng 1,3m so với sàn; đường cùng tầng, sáng, có chi tiết, không vật cản. Không cần GPS/server; map là hành trình và mốc, không quét mesh phòng. AR không tránh vật cản, sàn chưa đo thật, không dùng cho an toàn quan trọng. Đọc `HUONG-DAN-TEST.md`, quay màn hình từ trước khi quét A, đối chiếu bằng thước/vị trí thật trước khi gọi là nghiệm thu AR.

## Phạm vi file và dữ liệu

Không thay main Warehouse APK, backend, Render hay nhiệm vụ kho. Không xóa tuyến/media cũ. Room map nằm trong vùng riêng ứng dụng `room-map/current.json`; bản previous chỉ phục hồi lỗi, UI không chọn nhiều map. Lượt ghi mới chỉ thay current sau kết thúc B và lưu thành công. Giao diện cũ minimap/export/demo không còn trong luồng hai chế độ này; owner dùng chung được giữ tương thích để không xóa bừa các nguồn cũ.

Prompt trong PLAN-VA-PROMPT.md đã tự rà trước triển khai và bổ sung phản hồi giãn cách/chuyển động/đích. Kết quả hiện tại là **bản thử phòng đã qua cổng nguồn/Editor/build**, chưa phải nghiệm thu trên điện thoại thật.
