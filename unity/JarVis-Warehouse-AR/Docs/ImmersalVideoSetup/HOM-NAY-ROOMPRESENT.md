# JarVis — bàn giao công việc ngày 19/11/2026

## Bản xuất

- Xuất **nguyên ứng dụng JarVis**, không phải app AR tách riêng.
- Phiên bản: 0.2.8, Android version code 12.
- Mã ứng dụng giữ nguyên: `com.teep.jarviswarehousear`, để cập nhật bản đang có trên Samsung.
- Điểm vào: `JarVisFigmaApp`. Bao gồm các scene Splash/Login/Warehouse cũ, AtlasAR và Roompresent.
- APK: `D:/unity of teep/JarVis-Warehouse-AR/Builds/Android/JarVis-Warehouse-0.2.8.apk`, 88.623.689 byte (88,6 MB).
- **Đã build thành công, 0 lỗi; cài bằng ADB thành công trên Samsung SM-G988N và mở được app.** Màn hình Nhiệm vụ hiển thị đúng bài thử giao hai sách. Đang chờ chủ máy quét hai sách thật để thử AR.
- SHA-256: `5898B5AA9C524F85F55E75718792518F592DF0672A41D13864E1423F67CF986C`.
- Ảnh chụp thật: `JarVis-028-online.png` (màn hình Nhiệm vụ) và `JarVis-028-scanner.png` trong thư mục bàn giao.

## Nhiệm vụ thử: giao hai quyển sách

| Sách | Mã mẫu nhìn thấy | Barcode EAN-13 đã giải mã từ ảnh | Điểm giao |
|---|---|---|---|
| A1 | CPNB-1004 | `4714551570610` | B — kệ sách bên trái |
| A2 | CPNB-1003 | `4714551570603` | C — kệ sách bên phải |

Đây là **hai quyển sách**, không sử dụng thông tin hộp sữa trong bài thử cũ.

1. Tại A, vào **Nhiệm vụ**, nhận nhiệm vụ Roompresent.
2. Quét barcode sách A1 và A2 bằng camera. Bấm **Quét tiếp** giữa hai lượt.
3. Quét trùng A1 không thay cho A2; quét sản phẩm khác không mở dẫn đường.
4. Sau khi đã quét cả hai sách, app mở AR. Mang cả hai quyển theo.
5. Mặc định **Tối ưu**: so sánh chiều dài đường đi được trên NavMesh cho hai thứ tự B→C và C→B. Có thể chọn B trước hoặc C trước. Đây là phép tính đường đi, không gửi yêu cầu mô hình AI.
6. Tới đích đang được hướng dẫn, đặt đúng sách xuống rồi quét QR tại điểm đó. App kiểm tra đích, trạng thái định vị và khoảng cách ngang không quá 1,2 m.
7. Giao xong một sách mới hướng dẫn tới điểm còn lại. Chỉ hoàn tất khi đã xác nhận cả B và C.

Tiến độ thử được lưu cục bộ trên điện thoại. Không tạo nhiệm vụ, điều chỉnh tồn kho hay ghi giao dịch vào OMS thật. QR xác nhận vị trí, **không tự chứng minh rằng sách đã được đặt xuống**.

## QR điểm giao

- B: `JWM:ROOMPRESENT:153613:B`
- C: `JWM:ROOMPRESENT:153613:C`
- Nhãn PNG: thư mục `Docs/RoompresentQR` trong project.
- Bài thử cũng chấp nhận hai nhãn cũ `JWM:MAIN:B` và `JWM:MAIN:C`.
- Không dùng barcode trên sách để thay QR đích.

## Căn bản đồ và khôi phục tracking

- Immersal map: **153613 / Roompresent**. GLB Scaniverse là một lần quét độc lập, chưa thể giả định trùng tọa độ.
- A là tâm QR trên bàn thuyết trình, trước đây gọi E; B/C là hai kệ sách từ trái sang phải theo ảnh xác nhận.
- Lần đầu trong AR: làm theo ba bước ghi A/B/C. Đưa dấu + vào chính điểm tương ứng trên vật thật, giữ yên; nút ghi chỉ mở khi đã định vị và camera nhận được mặt phẳng.
- Căn bằng xoay quanh phương thẳng đứng và tịnh tiến, **không tự co giãn bản đồ**. Sai số khớp ba điểm phải nằm trong ngưỡng kiểm tra; khớp ba điểm không chứng minh độ chính xác toàn phòng.
- Kiểm tra ghim bằng mắt khi đi ngang, xoay máy. Chỉ bấm xác nhận khi các ghim đúng vật thật. Nếu không đúng, căn lại; không khẳng định vị trí chỉ từ kết quả Editor.
- Khi mất tracking, Immersal tiếp tục tìm lại vị trí. Bộ giám sát gọi lại phiên định vị nếu phiên bị dừng bởi lỗi SDK; không hủy nhiệm vụ hoặc đích đang đi.
- Trong lúc chưa có vị trí đáng tin cậy, chỉ dẫn AR được ẩn; trạng thái phục hồi vẫn hiển thị. Khi tracking trở lại, tiếp tục tuyến hiện tại. Không thể bảo đảm camera luôn tracking trong mọi ánh sáng/góc nhìn.
- Lưu phép căn theo map trên thiết bị, **không lưu lại tư thế camera của phiên AR cũ**. Khi mở lại, cần định vị thực và đối chiếu ghim.
- Để nguyên hai kệ trong lúc demo. Nếu đẩy kệ sang vị trí khác, ghim đã cấu hình không tự biết kệ đã di chuyển.

## Kiểm tra đã qua trước build

- EAN-13 của cả hai ảnh sách thật: giải mã đúng toàn ảnh ở nửa độ phân giải. Ảnh A2 ban đầu thất bại; bộ quét được bổ sung lọc nhiễu dọc/ngang theo vạch, vẫn dùng kiểm tra EAN của ZXing.
- Nhiệm vụ: chưa nhận / thiếu sách / trùng mã / sai sản phẩm / sai QR / mất tracking / khoảng cách không hợp lệ / quét trùng điểm đều không hoàn tất sai.
- Hoàn tất được cả hai thứ tự B→C và C→B trong kiểm tra logic.
- QR B/C sinh ra được giải mã ngược đúng nội dung.
- Phép căn thử với xoay 37° và tịnh tiến khôi phục đúng; bộ điểm không nhất quán bị từ chối.
- NavMesh khi xoay và dịch cùng nội dung: cả 6 chiều A↔B, A↔C, B↔C có đường hoàn chỉnh.
- Build validator chấp nhận đủ 6 scene và giữ JarVis là scene đầu.

## Chưa được coi là đạt

- Chưa có số đo sai lệch vị trí AR trong phòng, thử quay người/che camera/đi ra rồi quay lại trên Samsung.
- Chưa kiểm tra camera quét hai sách và QR thật trong APK mới.
- Chưa kiểm tra lại đăng nhập, gửi ảnh, micro, OpenAI bằng phiên thật của bản APK mới. Các phần này giữ tích hợp hiện có; bản build thành công không chứng minh dịch vụ AI đang đáp ứng.
- Không truy cập ổ E, không tạo backend/database thay thế, không ghi khóa OpenAI vào Unity.

Chi tiết kiểm tra nằm ở `Logs/RoompresentFullApp`. Lỗi thử A2 cũ trong `failure.txt` là lịch sử; đối chiếu thời gian với `prepared.txt` và `book-photo-decoder.txt` mới hơn.
