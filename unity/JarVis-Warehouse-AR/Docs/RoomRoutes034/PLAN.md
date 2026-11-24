# JarVis Routes 0.3.4 — sửa theo ảnh thực tế

## Phân tích / prompt được tự duyệt trước khi sửa
Ảnh cho thấy 3 mũi tên lớn ở khoảng còn 5,5 m và dòng AR sẵn sàng. Không thấy phiên bản; APK 0.3.3 chưa từng được xác nhận chép/cài vào Samsung. Không suy đoán ảnh là 0.3.3 hay lỗi hết chỉ vì test Editor đạt. ADB chưa thấy thiết bị; đề nghị chủ máy đối chiếu phiên bản song song.

Chỉ sửa Routes, không sửa OMS/app chính, không truy cập E, không xóa map hay đổi QR. UI native uGUI repair: giữ camera, safe area, hai card/header-footer; không thêm màn. Chủ sở hữu WalkingRouteRecorderUi, WalkingRouteLiveView, RoomRoutePresentation và Readiness. Unity 6000.4.7f1 đang mở, Play=false, scene Figma sạch; dùng CLI Editor kết nối, không Editor thứ hai. Nguồn thị giác là ảnh lỗi, không phải mockup để sao chép. Không tạo asset raster.

1. Hiện rõ v0.3.4 trên HUD để không test nhầm APK. Mọi trạng thái chuẩn bị có số % ở header, thanh tiến độ thật trong card cũ và số % trong reticle. A/B khóa xong phải hiện 100%; đi đường hiển thị đã chuẩn bị 100%, không lẫn % phòng quét với % đi.
2. Không tăng % theo timer; chỉ đếm điều kiện tracking/camera/chuyển động/đặc trưng và khóa pose. Hiện việc còn thiếu, sau 8 giây nhắc cách khắc phục cụ thể; không hứa thời gian hoàn tất hay tự 100. Không tự giảm yêu cầu an toàn để che quét lâu. Cho QR được giải mã song song chuẩn bị nhưng không cho khởi hành/ghi trước Ready và pose hợp lệ.
3. Còn ≤2 m chỉ B; >2–8 m tối đa một mũi tên; >8 m tối đa hai. Chỉ hướng của đoạn hiện tại tới trước góc thật đầu tiên, không lộ mũi tên hướng khác sau góc. Không ép tuyến thật thành thẳng, không đổi dữ liệu đã ghi. Xác minh trên đường thẳng, góc, vòng/giao nhau.
4. Mũi tên tối đa 14% chiều rộng màn hình tính bằng toàn bộ hình chiếu bounding box sau xoay, không chỉ công thức độ sâu tâm; tối đa 0,26 m bề rộng thật. Cắt ngoài vùng camera giữa header/footer và loại hình chiếu chồng nhau. Chuyển động tiến–lùi 3,5 cm cũ có kiểm tra biên; giảm chuyển động giữ đứng yên. B không đổi anchor theo camera.
5. Test logic biên, QR sớm không bypass, thiếu feature/QR/sai QR/camera cũ, mọi trạng thái header %, 2 tỉ lệ 412×915/732; render thêm AR góc và góc máy dọc. Xem ảnh thật sau render. Build strict 0.3.4 code13, xác minh chữ ký/hash. Cài đè giữ dữ liệu. Chỉ gọi nghiệm thu AR khi chủ máy quay lượt ghi/đi thật.

Tự duyệt: yêu cầu có tiêu chí đo được, giữ riêng tiến độ điều kiện và pose; không biến thời gian thành chất lượng tracking. Không kết luận nguyên nhân tọa độ sai hay sọc màn hình qua một ảnh. Giới hạn chưa kiểm chứng máy thật phải giữ trong bàn giao.

## Tiến độ
- [x] Đọc ảnh, code, kiểm tra Editor / thiết bị; duyệt prompt.
- [x] Sửa tiến độ và mũi tên.
- [x] Kiểm thử / render và duyệt hình; PlayMode nhiều frame đạt. Nghiệm thu đi thật tách riêng.
- [x] Xuất APK / chữ ký / checksum: strict Succeeded, errors=0, code13, chữ ký v2 cùng bản cũ.
- [x] Cung cấp APK trong V034; chưa chép Samsung vì không thấy thiết bị.
- [ ] Chủ máy xác nhận cài đúng v0.3.4 và nghiệm thu đi thật.
