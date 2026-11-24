# QA JarVis Routes 0.3.3

## Phạm vi và bằng chứng
- Video người dùng: 21,17 s, 42 mẫu toàn thời lượng cách 0,5 s; xem sáu contact sheet và các khung gốc. Pin B quá lớn gần camera, nhiều glyph/nhánh đồng thời; vị trí thực tế chưa đủ dữ liệu để kết luận đã căn chính xác.
- Cài đặt: native uGUI cùng HUD/safe-area/hai module/một map; không thêm màn, GPS, dịch vụ mapping hay tự sửa tuyến thành đường thẳng.
- Prompt và quy tắc khoảng cách được tự duyệt trong PLAN.md trước triển khai. Source sửa ở project D; không truy cập E.

## Đạt trong Unity
| Bộ kiểm tra | Số kiểm tra đạt |
|---|---:|
| Room logic | 50 |
| Regression | 41 |
| Readiness | 40 |
| Presentation | 158 |
| Native UI / AR world | 1197 |
| Core route | 150 |

Tổng 1636 kiểm tra tự động; 56 capture UI (28 trạng thái × 412×915 / 412×732) và world captures. Kiểm tra biên 2/5/10 m, tuyến 1000 m chỉ ba glyph pool, fixed route grid, giới hạn đoạn trước góc/loop, projected size, mất tracking, điểm đặc trưng thiếu/cũ/chỉ một ô, không tự 100%, không tăng quãng đường trước nút Bắt đầu đi, giữ map khi hủy, nhãn không cắt và vùng chạm ≥44 px. Kết quả chi tiết trong Reports.

Đã duyệt toàn bộ contact sheet UI ban đầu, rồi capture mới của các trạng thái thay đổi (chuẩn bị, xuất phát, sai QR, camera cũ), và ảnh world đường ngắn/dài/caution/near B. Hình synthetic không phải ảnh camera Samsung.

PlayMode cuối: PASS qua nhiều khung hình thật, đúng scene Routes, hai mốc A/B, ARPointCloudManager và RoomFeatureSampler/HUD được nối; không đóng Editor. Bài test ban đầu chạy nhầm scene Figma do playModeStartScene đang ép; đã sửa runner chọn scene đúng tạm thời và khôi phục cấu hình. Không suy ra app AR lỗi từ lỗi cấu hình test đó.

## Lưu ý build
Hai lượt xuất đầu thất bại khi request Pipeline/CLI hết hạn 5 giây; các thông báo timeout bị strict build tính là lỗi. Không sử dụng APK của lượt Failed. Lượt cuối dùng job bất đồng bộ chính thức của CLI, gỡ đúng callback trì hoãn của lượt trước, vẫn giữ StrictMode. Kết quả **Succeeded, errors=0**, build 34,21 s; job completed. Không chỉnh/xóa asset simulation để che cảnh báo.

APK `com.teep.jarvisroutes`, 0.3.3 / code 12, arm64-v8a, min API29 / target36; 50.386.191 byte. Chữ ký v2 hợp lệ, cùng certificate SHA256 `13163f8a75bbbdfdda62fb60ffbc9f03822b2b22fa6410fd2483339225dda49d` như 0.3.2 nên có thể cài cập nhật, không gỡ app. Đây là khóa debug để thử nội bộ, không phải bản phát hành cửa hàng.

SHA256 APK cuối: `A6ADC487F3DBA23201EABF4BEFD9BDA7D96814703180E8BDE7B22209965C5B42`. Bản file trong V033 đã đối chiếu khớp bản build. Chi tiết badging/signature/hash trong Reports. Chuyển sang điện thoại chỉ được tính đạt khi có `Reports/phone-transfer.txt` ghi Readback=PASS.

Editor đã trả về scene và playModeStartScene `JarVisFigmaApp`, dừng Play; applicationId chính `com.teep.jarviswarehousear` / version 0.1.2 được build runner khôi phục. Không đổi app chính thành Routes.

AR Foundation có cảnh báo asset XR simulation tạm trùng đường dẫn từ trước. Đối chiếu nội dung giống nhau nhưng GUID có tham chiếu EditorBuildSettings; không xóa/gộp bừa, không sửa package hay bỏ strict để che lỗi.

## Chưa nghiệm thu vật lý
- ARPointCloud callback/độ phủ trên Samsung, khóa ảnh mốc in 18 cm và đi thật A→B với map hiện tại chưa kiểm chứng bằng bản 0.3.3.
- Phần trăm là điều kiện chuẩn bị, không nhận dạng lại toàn phòng hoặc mức hoàn thành quét bản đồ 3D. Không tự phát hiện phòng khác. QR A vẫn là mốc căn hệ tọa độ, visual-inertial tracking vẫn có drift.
- Chưa có JSON tuyến/pose log thật để giải thích đầy đủ sai số B ở video. Không tăng ngưỡng lệch hay gán tiến độ giả để làm như đã hết lỗi.
- Sàn ước lượng theo camera ~1,30 m, không yêu cầu chờ plane nhưng không chứng minh vị trí sàn thật/không tránh vật cản.

Hướng dẫn và checklist thực địa: HUONG-DAN-TEST.md. Cần video từ bước chuẩn bị → quét A → Bắt đầu đi → tới B trước khi gọi là nghiệm thu hoàn chỉnh.
