# JarVis Routes 0.3.4 — kết quả sửa theo ảnh Samsung

## Trạng thái và chứng cứ
Implemented_unverified trên máy thật. Người dùng trả lời đang dùng bản cũ / chưa rõ phiên bản; không xác nhận ảnh lỗi là bản 0.3.3. ADB không thấy thiết bị; kiểm tra MTP cuối trước khi xuất không thấy Samsung. Không tuyên bố đã cài hoặc đã đi thử 0.3.4.

Phạm vi: native uGUI repair, camera/safe-area/header/footer sẵn có, chỉ Routes. Không tạo màn hay asset raster; không thay API, app chính, dữ liệu map, QR hoặc ngưỡng lệch để làm như đúng. Các sửa nằm trong source V034; Before giữ baseline. PLAN.md là prompt và điều kiện chấp nhận.

## Kết quả kiểm thử
| Nhóm | Số PASS |
|---|---:|
| Room logic | 50 |
| Regression | 41 |
| Readiness | 40 |
| Presentation | 159 |
| Native UI / world | 1401 |
| Focused 034 | 205 |

1896 checks trước build, cộng 150 core khi build = **2046** (xem Reports/offline-checks.txt và offline-result.txt). Các báo cáo readiness-033 / regression-033 giữ tên cũ nhưng được chạy lại trên source 0.3.4, không dùng số đếm bản cũ để nhận là test mới.

Đã kiểm tra hai tỉ lệ 412×915 và 412×732, 28 trạng thái UI, cùng 18 capture AR tuyến thẳng 5,5 m / góc 5,8 m / vòng 12 m ở góc chúc máy 20°,35°,55°. Xem toàn bộ contact sheet, kiểm tra rõ số %, thứ tự thông báo, nút và khung không đè nhau. Render AR này là synthetic, không camera Samsung. Toàn bộ 120 tổ hợp aspect/pitch/yaw/depth có kiểm tra giới hạn toàn hình chiếu bao gồm biên chuyển động.

Test mới phát hiện và đã sửa: lỗi tên biến biên dịch; phép co hình chiếu chưa đủ ở góc xiên; khung quét đè card trên màn ngắn. Kiểm thử được chạy lại sau sửa, không bỏ assert để lấy PASS. Sau khi thêm gợi ý camera và header khóa B 100%, focused/UI đã chạy lại.

PlayMode cuối PASS qua nhiều frame thật, đúng scene Routes, HUD hai module / thư viện hai mốc / feature sampler được nối. Lần trước không tiến frame do Editor chạy nền; runner chỉ bật Application.runInBackground trong phiên test và phục hồi scene khi về Edit. Không đổi PlayerSettings.runInBackground của ứng dụng. Editor trả về JarVisFigmaApp, Play=false, scene sạch.

## Giới hạn phải giữ khi bàn giao
- Chưa có log tuyến thật để kết luận vì sao B lệch trong các video trước. Sàn ước lượng theo camera 1,30 m; ARCore có drift, không tránh vật cản.
- % là điều kiện chuẩn bị / khóa pose, không phải % diện tích phòng. Không thể hứa quét xong trong số giây cố định. Không giảm yêu cầu feature/pose để tạo tiến độ giả.
- Đủ 100% và Start chỉ cho phép thử tuyến đã ghi đúng phòng, đúng vị trí A/B. Không tự biến map cũ thành map phòng mới.
- Cần xác nhận v0.3.4 trong HUD và quay màn hình từ lúc chuẩn bị qua A tới B trước khi gọi nghiệm thu vật lý.

## APK
Strict build **Succeeded, errors=0**, 81,73 giây. Package com.teep.jarvisroutes, versionName 0.3.4 / code13, minAPI29. APK 50.388.383 byte, SHA256 `3335534D70988587475DE3D577C98FB4EF89C57BD91BE9438EBFC0596B24AB3C`.

APK v2 signature Verifies, cùng certificate SHA256 `13163f8a75bbbdfdda62fb60ffbc9f03822b2b22fa6410fd2483339225dda49d` với 0.3.3/0.3.2 nên hỗ trợ cài cập nhật. Đây là khóa debug để thử nội bộ, không phải khóa phát hành cửa hàng. File trong V034 được đối chiếu bản build. Reports chứa aapt badging, apksigner, hash và test kết quả thật.

Không thấy Samsung ở lần kiểm tra MTP cuối. APK được cung cấp tại V034, không ghi Readback=PASS / không nhận là đã chép hoặc cài. Cài đè và xác nhận dòng v0.3.4; không gỡ ứng dụng vì cần giữ tuyến cũ.
