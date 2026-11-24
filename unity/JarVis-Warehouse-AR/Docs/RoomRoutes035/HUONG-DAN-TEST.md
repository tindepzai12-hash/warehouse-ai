# Thử bản 0.3.5 trên Samsung

1. Mở `JarVis-Routes-0.3.5.apk` và chọn **Cập nhật**. Không gỡ bản cũ, không xóa dữ liệu. Header trong app phải có **v0.3.5**.
2. Giữ nguyên thẻ A/B và tuyến đã lưu. Nếu đã đổi vị trí thẻ hoặc đổi phòng thì ghi lại tuyến: Ghi tuyến → chuẩn bị AR → quét toàn bộ thẻ A → đi đúng lối → quét toàn bộ thẻ B. Thẻ dùng kích thước tham chiếu 18×18 cm, dán thẳng đứng, đủ sáng.
3. Chọn Chỉ đường. Lia máy nhẹ nhìn chi tiết quanh vị trí A. Khi chuyển sang **2/2 · Khóa vị trí A**, hướng vào thẻ và giữ ổn định. Chờ QR vài giây không được tự quay về quan sát chỉ vì ít điểm đặc trưng; mất tracking thật thì vẫn phải chuẩn bị lại để tránh chỉ sai.
4. Khi hiện **Sẵn sàng đi · 100%**, bấm **Bắt đầu đi**. Theo tuyến đã ghi; màu xanh dương đánh dấu B. Quan sát lối thật, không dựa vào app để tránh vật cản.
5. Trước góc rẽ: kiểm tra hướng trái/phải trên thẻ dưới và khoảng cách tới góc. Khi góc trên sàn ở ngoài camera, vẫn dùng mũi tên trên thẻ; hướng camera xuống góc nếu cần thấy cue AR. Không có lý do bắt người dùng tìm “mũi tên ở nhánh sau góc” nữa.
6. Qua góc: mũi tên phải trở lại hướng đi của đoạn tiếp theo, không nhảy sang nhánh tương lai. Còn ≤2 m ưu tiên pin B và hướng trên thẻ để không có mũi tên AR sát chân.
7. Nếu còn lỗi, quay màn hình từ trước khi chọn Chỉ đường, bao gồm header phần trăm và v0.3.5; mô tả lối thẳng hay có góc. Không cần xóa map hoặc cài lại nhiều lần.

Bản này đã kiểm tra logic/render/khởi động/build trong Editor. Chưa thay thế nghiệm thu đi thật bằng camera Samsung.
