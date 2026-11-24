# JarVis Lab Test — trạng thái bàn giao thử ngày 27/10/2026

## Phạm vi bản này

Bản ngoại tuyến riêng `com.teep.jarvislab`, tên **JarVis Lab Test**, phiên bản **0.1.0 / code 1**. Ghi năm mốc A–E và các lối đã đi; có hai bài thử nhập kho và lấy/chuyển hàng. Chưa nối website theo yêu cầu mới nhất của người dùng.

- Nhập kho: đúng mã hàng `LAB-PACKAGE-001` → tới B → đúng QR B mới hoàn tất.
- Lấy/chuyển: từ vị trí đã định vị tới B → đúng mã hàng → tới C → đúng QR C mới hoàn tất. Không bắt quét A để mở nhiệm vụ khi đã có vị trí hợp lệ.
- Mã hàng, mã vị trí và pose camera có chức năng riêng. Đóng/mở app hoặc mất định vị cần nhìn lại một mốc đã ghi.
- Đường được chọn trên đồ thị các đoạn đã đi. Không nối trực tiếp xuyên qua vùng chưa ghi. Minimap và AR dùng cùng hệ tọa độ.

## File phát hành

Các đường dẫn bên dưới tính từ thư mục `MissionWorkflow` của vùng làm việc:

| File | Kích thước |
|---|---:|
| `output/apk/JarVis-Lab-Test-0.1.0.apk` | 50.967.007 byte |
| `output/pdf/JarVis-Lab-5-moc-A-E-va-ma-hang.pdf` | 345.259 byte |
| `output/HUONG-DAN-JarVis-Lab-Test.txt` | 9.311 byte |

SHA256 APK: `69EE03EC35ACCA62B4685C8016E8A62353A4189070D1B0A0489D83D033776331`.

Cả ba file đã được chép vào **Bộ nhớ trong/Download** của **Galaxy S20 Ultra 5G**, sau đó đọc lại từ điện thoại và xác minh SHA256 đều khớp. Bằng chứng: `evidence/phone-copy-verified.json`. Đây là xác nhận chuyển file, chưa xác nhận cài đặt hay chạy AR trên điện thoại.

## Kiểm tra đã đạt trên máy tính

- Unity build thành công, 0 lỗi; APK có chữ ký v2 hợp lệ, ARM64, Android tối thiểu 10/API 29.
- 31 kiểm tra logic: điều kiện nhiệm vụ, mã sai/bước sai, vị trí không hợp lệ, đường nhiều nhánh, đường ngược và vùng chưa ghi.
- 164 kiểm tra bố cục/serialization; 10 ảnh giao diện ở hai chiều cao màn hình. Đã sửa minimap không vẽ do thiếu CanvasRenderer.
- Cả năm thẻ có QR đúng; ARCore image evaluation 100/100 là kiểm tra ảnh đầu vào, không chứng minh camera điện thoại đã nhận tốt.
- Ba file nguồn module đã đối chiếu khớp giữa bản tích hợp trong Unity và vùng làm việc.
- Các scene JarVis chính, Visual Maps và hai file ProjectSettings được đối chiếu SHA256: giữ nguyên so với trước lượt triển khai.

Bằng chứng: `evidence/mission-core-results.txt`, `evidence/unity-build-8.log`, `evidence/apk-verification.json`, `evidence/project-preservation-verified.json`, `evidence/source-sync-verified.json`; kết quả UI/build trong `D:\unity of teep\JarVis-Warehouse-AR\Logs\LabTest`.

## Còn phải thử thực địa

Chưa xác nhận cài APK, nhận thẻ, hướng mũi tên, độ lệch, quyền camera và độ dễ dùng khi đi thật. Các ảnh UI dùng dữ liệu kiểm thử có đánh dấu, không phải bản đồ lab đã đo.

1. Cài APK đúng tên **JarVis Lab Test** từ Download.
2. In PDF ở **100% / Actual size**; đo toàn bộ thẻ gồm viền hoa văn rộng **20 cm**. Không dùng QR A/B cũ thay ảnh thẻ mới.
3. Đặt cố định thẻ A–E. Ghi từ A qua các mốc theo lối trống; quay về A rồi lưu. Ghi thêm các lối khác nếu cần.
4. Chạy hai bài thử. Quét sai hàng/sai đích phải giữ nguyên bước. Nếu mất định vị, nhìn lại một mốc đã ghi; không xóa dữ liệu để thử lại.
5. Xuất ZIP ở **Download/JarVisMaps**, gửi ZIP cùng video màn hình và ảnh vị trí A–E để đối chiếu với GLB iPhone và hai ZIP Samsung.

Tiến độ 0–100% là độ ổn định của ảnh mốc được tracking, không phải phần trăm căn phòng đã tái tạo. Minimap hiện là mốc và các lối đã ghi; chưa phải sơ đồ tường/vật cản hợp nhất từ GLB. Bản này chưa nhận diện bàn/ghế hay tránh vật cản động.

## Trình tự công việc tiếp theo

1. Đọc ZIP thực địa; báo số mốc, cạnh, ảnh, gián đoạn, sai số khi quay về A và khả năng nối các đích.
2. Căn GLB + ảnh/pose Samsung + mốc mới bằng ít nhất ba điểm tương ứng không thẳng hàng; dùng D/E làm điểm kiểm tra độc lập.
3. Đối chiếu vùng đi được, vật cản và các lối thông người dùng đã xác nhận. Giữ vùng thiếu dữ liệu ở trạng thái chưa biết.
4. Chỉ sửa lỗi AR/đường đi có bằng chứng; kiểm tra cùng pose cho chấm xanh, mũi tên và đích.
5. Chỉ nối web khi người dùng yêu cầu và bản thực địa đạt. Module backend đã soạn trong vùng làm việc chưa được triển khai lên Render.

Kế hoạch chi tiết và prompt làm tiếp: `KE-HOACH-VA-HUONG-DAN-THU.md`. Không cần build lại chỉ vì cập nhật tài liệu này.
