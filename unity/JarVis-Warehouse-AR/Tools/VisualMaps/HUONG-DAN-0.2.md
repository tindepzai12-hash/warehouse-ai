# JarVis Maps 0.2 — quét, lưu và phân tích nhiều phòng

Ngày 25/10/2026. Đây là bản thử chức năng thu dữ liệu/phân tích, **chưa nghiệm thu chỉ đường tránh tường/vật cản**. Không gỡ app Maps cũ để cài bản này; cài cập nhật cùng gói `com.teep.jarvismaps` nhằm giữ dữ liệu. JarVis Routes và app Warehouse là hai gói khác, không bị thay thế.

## 1. Bắt đầu quét trên Samsung
1. Mở **JarVis Maps → Quét phòng mới**, cấp quyền Camera nếu được hỏi.
2. Hướng camera quanh phần phòng muốn lưu và đi chậm. App tự chụp khi camera/AR sẵn sàng, không bắt quét QR, căn sàn hay kết nối PC trước. Không cần quay video riêng.
3. Đọc dòng hướng dẫn ngay trên màn camera. Thiếu sáng: bật thêm đèn hoặc nhìn vùng sáng hơn. Máy quá nhanh: chậm lại. Gợi ý trái/phải là góc nhìn gần vị trí hiện tại chưa có ảnh, không phải app đã biết toàn bộ cấu trúc phòng.
4. Số ảnh là số đã lưu thật, không phải % diện tích phòng. Cảnh báo không làm mất ảnh trước đó. Chỉ khung gần đen, trắng cháy hoặc trống mới chưa được tính. App không bắt làm theo mọi gợi ý mới cho lưu.
5. Bấm **Lưu lượt quét phòng** khi xong, đặt tên rõ như “Phòng ngủ - cửa ra vào”. Có thể lưu/xuất với ít ảnh; để PC dựng cần tối thiểu 20 ảnh cùng lượt liên tục. 80–150 ảnh chồng cảnh từ nhiều vị trí là điểm bắt đầu thử, không phải quota bắt buộc hay bảo đảm dựng được map.

Không chỉ xoay điện thoại tại chỗ: ảnh cần thấy cùng đồ vật từ vài vị trí khác nhau để suy ra chiều sâu. Giữ dọc, camera chính, không đổi zoom. Chỉ di chuyển nếu lối đi thực tế trống và bạn nhìn được nó; ứng dụng chưa bảo vệ khỏi va chạm.

## 2. Căn sàn và ghi tuyến — tùy chọn bổ sung
- Thu ảnh phòng không đòi có tuyến. Nếu muốn dữ liệu có độ cao sàn, hướng vào sàn thật rồi bấm **Căn sàn (không bắt buộc)**; không căn vào mặt bàn.
- Sau khi đã căn sàn và có ít nhất 20 ảnh phòng, bấm **Ghi thêm tuyến A → B**. Quét A, đi đúng lối muốn lưu, tại chỗ đích đứng quét B để kết thúc. Nội dung A: `JWM:MAIN:A`; B: `JWM:MAIN:B`.
- Đây chỉ là tuyến ứng viên. QR xác nhận mốc, không tự cung cấp pose chính xác; B là vị trí bạn đứng khi kết thúc, không phải tọa độ miếng QR trên tường.
- Nếu AR mất vị trí kéo dài hoặc tọa độ nhảy, app giữ ảnh và cho xuất, nhưng không ghép bừa hai hệ tọa độ. Lượt bị ngắt không được công bố là tuyến chính xác. Đừng xóa dữ liệu app để thử lại.

## 3. Lưu nhiều lượt và xuất ZIP
- **Thư viện nhiều map** có các lượt nằm trên điện thoại và các map đã dựng trên PC khi kết nối. Mỗi lượt/map có ID riêng, không tự ghi đè phòng trước.
- Chọn lượt trên điện thoại để đổi tên và **Xuất ZIP vào Download** hoặc **Gửi PC để phân tích**.
- ZIP ở **Tệp của bạn → Bộ nhớ trong → Download → JarVisMaps**. Chép nguyên ZIP sang PC để giữ intrinsics, pose và depth nếu có. Không chỉ gửi một vài JPEG hoặc video màn hình.
- Ảnh tự có checkpoint sau mỗi ảnh; lượt đang chụp khi app bị đóng được đánh dấu cần phục hồi, không giả là đã kết thúc. Mạng lỗi không xóa ảnh/ZIP.
- Giới hạn kỹ thuật một lượt: 600 ảnh và 220 MB ZIP. Chia nhiều lượt/map khi quá lớn; hiện chưa có chức năng gộp tự động các lượt AR khác nhau.

## 4. PC phân tích và vẽ sơ đồ
Trên máy hiện tại, thư mục đã có môi trường Python là thư mục chứa tài liệu này trong vùng làm việc Codex. Chạy **START-PC.cmd**, mở trang cục bộ được in ra. Không cần tải lại CUDA hoặc driver.

Trên bản sao nguồn mới ở `Tools/VisualMaps`, cài môi trường riêng bằng `SETUP-PC.ps1 -Python <đường_dẫn_python_3.12.exe>` rồi `SETUP-OBJECTS.ps1`; sau đó `START-PC.cmd`. Chỉ thực hiện cài đặt khi bạn muốn dùng bản sao đó; không có cài hệ thống tự động.

- Không dùng Wi-Fi: chép ZIP qua USB, trên **JarVis Maps Studio** chọn ZIP → **Nhập và phân tích**.
- Cùng Wi-Fi riêng: trong app **Kết nối PC**, quét QR trên trang PC, rồi mở lượt chụp và gửi. % gửi là byte thật; xử lý tiếp báo từng bước, không giả % phủ phòng.
- PC ghép ảnh COLMAP, căn về tọa độ mét, ghi kết quả riêng vào thư viện. Nếu có depth/confidence đồng bộ và sàn đã căn, có bản nháp bề mặt: sáng = bằng chứng sàn, đỏ = bề mặt cản, tối = chưa xác định.
- Sơ đồ không được biến vùng tối thành đường trống. Không có depth thì vẫn có thể ghép ảnh/định vị và nhận diện đồ vật 2D; không tự tạo phòng/tường giả.
- Chọn từng map để xem báo cáo/đổi tên. **Quét bản cập nhật riêng** trên app tạo ID mới, liên kết map gốc và tăng revision; không tự hòa trộn hình học cũ với lượt chụp mới.
- Công cụ dòng lệnh `IMPORT-MAP.py <đường_dẫn_ZIP>` dùng khi server đã dừng; tránh hai tiến trình cùng ghi thư viện.

Ảnh xử lý tại PC trong mạng riêng, không tải lên dịch vụ Internet. Kết nối LAN HTTP chưa mã hóa, chỉ dùng Wi-Fi tin cậy; không mở port 8765 ra Internet hoặc chia sẻ QR kết nối. Không đóng PC khi đang nhận/dựng.

## 5. Nhận diện đồ vật
**Nhận diện đồ vật → Chụp và nhận diện trên PC** gửi một ảnh tới PC đã kết nối. Dùng model NanoDet thật cho các nhóm phổ thông, không phải mọi loại đồ và không phải SKU hàng kho. PC cũng nhận diện trên tối đa 30 ảnh phân bố trong map đã ghép.

Nhãn/confidence là gợi ý 2D, có thể thiếu/sai. Nhiều ảnh thấy cùng một ghế không có nghĩa có nhiều ghế. Không dùng nhãn này để khẳng định đường đã trống, nhận được kính/tường hay mọi vật cản.

## 6. Chưa được coi là hoàn thành
- Chưa đo camera RGB/depth, hướng tia, căn sàn và độ lệch trong phòng thật trên Samsung với bản 0.2.0.
- Sơ đồ hiện là bằng chứng bề mặt, chưa kiểm tra khoảng trống toàn thân, vật cản động hay phát hiện tường đầy đủ. Map tạo ra giữ `navigationAllowed=false`; nút chỉ đường chưa được mở bằng cách giả trạng thái.
- Bước tiếp theo là thu ZIP phòng thật, kiểm tra calibration/sơ đồ và đường đi, rồi hoàn thiện quy trình duyệt/xuất bản tuyến. Không tự sửa JSON để bật mũi tên trước bước này.

Khi test quét, gửi ZIP cùng ảnh/video màn hình nếu có lỗi. Không cần mua iPhone hoặc thay phòng chỉ để bắt đầu; phần nào camera không cung cấp được app phải báo rõ, không suy diễn.
