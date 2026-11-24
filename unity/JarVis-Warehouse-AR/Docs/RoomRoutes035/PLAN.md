# JarVis Routes 0.3.5 — sửa hai lỗi từ lượt thử thực tế

## Bằng chứng và phạm vi
- Video người dùng 12,53 giây: chuyển sang khóa A khoảng giây 4, quay về quan sát môi trường khoảng giây 7,5; kết thúc vẫn ở bước 1/2. Đã xem contact sheet toàn bộ, lấy mẫu mỗi 0,5 giây.
- Ảnh v0.3.4: B còn 3,7 m, báo sắp rẽ phải, không có mũi tên. Chuỗi thông báo khớp nhánh `first > until` trong LiveView.
- Sửa tại runtime Routes, không đổi app chính, không xóa tuyến, không truy cập ổ E. uGUI hiện hữu, repair mode; giữ màu và tài nguyên hiện tại, không sinh ảnh mới.
- Baseline Unity 6000.4.7f1: cảnh JarVisFigmaApp sạch, không Play/compile/error. Giữ nguyên header, footer, scanner và quyền sở hữu layout; chỉ thêm glyph nhỏ vào hàng tiêu đề footer hiện tại.

## Prompt triển khai đã tự rà soát
1. Chỉ chốt điều kiện môi trường sau khi đủ quan sát thật. Khi camera còn mới, tracking liên tục và pose không nhảy, việc điểm đặc trưng ít đi lúc hướng vào QR không được hủy kết quả đã đạt. Mất tracking/camera, gián đoạn, pose nhảy, Pause hoặc bắt đầu phiên mới vẫn hủy chốt. Không tự tăng phần trăm theo thời gian.
2. Giữ khóa QR bằng cả nội dung và pose ảnh ổn định. Không bỏ yêu cầu marker, không tự nhận A hay B, không dùng GPS giả hoặc khôi phục tọa độ qua phiên AR.
3. Giữ lưới mũi tên theo tuyến ở đoạn bình thường. Khi lưới vượt giới hạn góc rẽ, bổ sung cue ở cuối đoạn hiện tại; trong 1,2 m tới góc dùng hướng đoạn kế tiếp tại chính góc đã ghi. Không hiển thị nhánh xa, không nối đường chéo tới B. Giữ pool 2, ngân sách 0/1/2 theo quãng đường, giới hạn kích thước, vùng HUD và kiểm tra chồng lấn.
4. Khi góc/sàn quá gần hoặc ngoài camera, giữ chỉ dẫn hướng nhỏ trong thẻ hiện tại thay vì màn hình dẫn đường trống. Glyph này là chỉ dẫn màn hình, không giả làm vật thể AR; tắt cùng trạng thái mất tracking/lệch tuyến. Không thêm nút hoặc popup.
5. Viết regression cho toàn bộ pha chuẩn bị → chờ QR >4 giây → khóa A; cả mất tracking thật và phục hồi. Di chuyển camera tới gần góc trái/phải, đoạn ngắn, vòng và giao cắt; kiểm tra cue không biến thành nhánh tương lai. Chụp UI 412×915 và 412×732, không tràn chữ/nút; giữ các kiểm thử an toàn cũ.
6. Chỉ build 0.3.5/code14 sau khi compile/reviews/PlayMode đạt; kiểm tra manifest, chữ ký, SHA256, phục hồi thiết lập app chính. Nếu Samsung có kết nối, chép APK và đọc ngược kiểm tra hash. Không gỡ app hoặc xóa map.

## Tự phản biện / giới hạn
- Không đánh đồng hết hạn point-cloud với mất tracking: chốt kết quả quan sát, không chốt quyền hiển thị AR.
- Không sửa lỗi thiếu mũi tên bằng cách thả mọi mũi tên sau góc hoặc đặt chúng trước camera bất kể tuyến.
- Video không có log ARCore nên nguyên nhân tụt pha được xác định từ thời điểm và code, không khẳng định mọi gián đoạn đều do điểm đặc trưng.
- Mặt sàn vẫn là độ cao ước lượng 1,30 m, không có xác minh vật cản. Kiểm thử Editor không thay thế đi thử camera/ARCore trên Samsung.

## Tiến độ
- [x] Xem bằng chứng và xác định nhánh gây lỗi.
- [x] Sửa runtime và regression.
- [x] Kiểm thử, xem ảnh render và PlayMode.
- [x] Build và xác minh APK 0.3.5/code14; chép Samsung/Download và đọc ngược hash khớp. Cài đặt và đi thử thực tế chưa nghiệm thu, ghi riêng trong QA.
