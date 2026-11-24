# Sửa phản hồi Samsung — 0.3.1

Nguồn: video người dùng 17,30s, 720×1280. Đã xem contact toàn clip, tất cả 17 frame 1fps và frame chuyển trạng thái chi tiết. Đầu clip luồng dẫn đã dừng nhưng header vẫn AR sẵn sàng; khoảng 9,5–10,5s nhận A và dẫn; khoảng 11,5–12,5s hiện đích/mũi tên, rồi lại Cần quét A. Video không cho biết chính xác nhánh guard nào gây dừng; không kết luận lỗi thiết bị hay GPS. Chưa đọc được dữ liệu map riêng của Samsung qua ADB/MTP.

Phạm vi: sửa owner Routes, uGUI trên scene hiện có, giữ hai chức năng và map hiện hành; không backend/main Warehouse/driver. Dùng khung quét vector native, không ảnh trang trí mới. Camera thật là nguồn duy nhất cho trạng thái nhận QR; animation không giả báo đã nhận.

1. Thêm feedback Searching / chờ camera / sai mã / đã đọc QR-đang khóa pose / đã khóa / mất tracking. Khung góc, scanline chỉ chạy khi ảnh camera còn cập nhật, chuyển xanh và xác nhận khi cả QR lẫn pose hợp lệ. Không coi đọc ID là khóa vị trí.
2. Bỏ lỗi trạng thái chết: mất tracking ngắn thì đứng yên, ẩn AR và không tích lũy tiến độ; chỉ tiếp tục khi pose liên tục đủ gần và tracking ổn định trở lại. Timeout, nhảy pose, gián đoạn hoặc pause yêu cầu quét A lại ngay trong cùng màn, không tự nối mù. Header và nút thống nhất trạng thái.
3. Giữ đường raw đã ghi. Tạo polyline dẫn riêng bằng giản lược hình học 12cm, giữ góc thật. Mũi tên theo tiếp tuyến polyline này, không theo yaw điện thoại và không sao chép góc video tham chiếu. Progress chiếu lên đoạn hiện hành, lần lượt qua góc, không chọn điểm gần nhất toàn tuyến. Khoảng cách lệch là tới đoạn, không tới waypoint xa.
4. Phân biệt hướng tuyến (đi thẳng / góc rẽ thật sắp tới) và hướng camera (xoay máy để nhìn thấy tuyến). Tuyến thẳng quay máy 90/180° không được biến thành nhãn rẽ phải/quay đầu đường.
5. Kiểm thử hồi quy tuyến thẳng nhiễu, L, U/cắt nhau, camera đổi yaw, đứng lệch điểm scan, tracking chớp/tái ổn định/timeout/jump/pause, QR sai/cũ, camera dừng, state success, bản đồ gốc không bị sửa. Native UI 2 tỷ lệ, xem đủ ảnh sau build, PlayMode, APK signature/hash. Không dùng PASS Editor để nhận Samsung thật đã hết lỗi.

Tự rà: không ép đường có góc thành đường thẳng, không tắt bảo vệ nhảy vị trí, không giả nhận QR bằng scanline; không yêu cầu tạo lại map chỉ để che lỗi. Cần lượt Samsung mới để xác nhận thực địa.

Tiến độ 24/10/2026: hoàn thành sửa code và kiểm thử phần mềm; 150 assertion lõi, 50 room logic, 41 hồi quy và 821 UI/mesh checks đều PASS. Đã xem đủ 40 ảnh native ở hai tỷ lệ, PlayMode startup PASS, APK 0.3.1/code 10 xác minh chữ ký và hash. Cần lượt camera/đi bộ Samsung để chốt nghiệm thu thực địa, không đánh đồng mô phỏng với thiết bị thật.
