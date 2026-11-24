# JarVis Maps 0.2.0 — bản thử quét và phân tích

Build cuối ngày 25/10/2026, khoảng 23:38 giờ Việt Nam. Không phải bản nghiệm thu chỉ đường tránh vật cản.

- APK: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Maps-0.2.0.apk`.
- Package `com.teep.jarvismaps`, versionName `0.2.0`, versionCode `3`, ARM64, Android API29 trở lên.
- Dung lượng file APK: **50.464.979 byte**. Tổng byte trong BuildReport không phải dung lượng APK để chép sang điện thoại.
- SHA256: `7FBB57292F2802975622274D78B22BA077A8D03A0219B8068F15FC9D8DA20009`.
- Chứng thư debug SHA256 cần khớp bản Maps cũ: `13163f8a75bbbdfdda62fb60ffbc9f03822b2b22fa6410fd2483339225dda49d`. Cài cập nhật, không gỡ app để giữ dữ liệu.

## Thay đổi chính
Quét phòng tự chụp, không đợi căn sàn/QR/PC; gợi ý chữ theo ảnh/cử động/ARCore và góc ảnh đã lưu; checkpoint, đặt tên, lưu nhiều lượt, mở lại và xuất ZIP; thư viện PC chọn map/revision riêng; nhập ZIP; COLMAP/hloc sparse localization; depth/confidence tùy thiết bị; sơ đồ bằng chứng bề mặt; model NanoDet nhận diện phổ thông 2D thật.

Đọc `HUONG-DAN-0.2.md` để sử dụng, `MASTER-PLAN-0.2.md` để xem prompt và các mốc chưa xong, `QA-0.2.md` để phân biệt thử tổng hợp/Editor với thiết bị thực.

## Chưa bàn giao như bản hoàn chỉnh
Map tạo ra chưa được mở chỉ đường vì chưa có pipeline duyệt khoảng trống toàn thân/vật cản và nghiệm thu phòng thật. Chưa có ghép nhiều lượt AR tự động, mesh phòng hoàn chỉnh hay tránh mọi vật cản động. Không mở gate bằng sửa JSON.

Chưa chép/cài lên Samsung: thiết bị chưa xuất hiện trong kiểm tra ADB/WPD. Không tuyên bố đã đo camera/depth hoặc đi thử. APK 0.1.1 và nguồn/capture cũ được giữ, không xóa dữ liệu trên E hay ổ nào khác.

Nguồn PC trong `Tools/VisualMaps` không chép lặp `.venv`, vendor, dữ liệu phòng hoặc ảnh kiểm thử. Môi trường chạy ngay trên máy hiện tại vẫn nằm cạnh bản sao tài liệu này trong thư mục làm việc Codex; bản sao nguồn mới dùng SETUP-PC.ps1 và SETUP-OBJECTS.ps1.
