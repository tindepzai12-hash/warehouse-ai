# JarVis Routes 0.3.0 — thử tuyến A → B trong phòng

## Bản mới làm gì?

Chỉ có **Ghi tuyến** và **Chỉ đường**. Một tuyến hoàn chỉnh hiện hành; không dùng lại đường mẫu/đường cũ từ bản 0.2. Không cần GPS, server, Immersal hoặc tải ảnh phòng lên cloud. “Map” ở đây là hành trình camera và các mốc, không phải mô hình 3D toàn bộ phòng.

## Chuẩn bị thẻ — quan trọng

1. Mở `IN-THE-A-B.html` trên máy tính, in A4 với **100% / Actual size**, không Fit to page. Hai trang là A và B.
2. Dùng thước kiểm tra **toàn bộ hình vuông có họa tiết là 18 × 18 cm**, không phải riêng ô QR ở giữa. Vạch 10 cm dùng kiểm tra tỉ lệ in.
3. Dán thẻ A và B **thẳng đứng**, phẳng, cố định trên tường/cạnh kệ; chữ phía ngoài giấy hướng lên. Tránh bóng lóa, không gấp hoặc đổi chỗ thẻ sau khi ghi.
4. Không dùng lại QR A/B trơn từ bản trước. QR giữa thẻ nhận tên; vùng họa tiết ngoài giúp AR đo vị trí và hướng. Đọc được QR nhưng chưa nhận toàn bộ ảnh thì app chưa bắt đầu.

Có thể hiển thị trên màn hình cố định thay giấy, nhưng phải đo vùng ảnh đúng 18 cm, tắt tự xoay và tránh phản chiếu. Không cầm thẻ bằng tay trong lúc ghi/dẫn. Hai điện thoại hiển thị ảnh không đo đúng kích thước sẽ làm sai tỉ lệ đường.

## Cài bản thử

Cài `JarVis-Routes-0.3.0.apk` trên Samsung Android, chọn cập nhật JarVis Routes nếu có. Không cần gỡ bản cũ hoặc cài driver máy tính. Cho phép Camera, cần Google Play Services for AR hoạt động. Đây là APK Routes riêng, không phải bản cập nhật app kho/Render.

## Ghi một tuyến mới

1. Chọn một lối đi thông thoáng, cùng tầng, nên dài 3–6 m và có một góc rẽ nhẹ để dễ nhận xét. Không thử ở cầu thang/ngoài đường.
2. Mở app, chờ AR ổn định; lia máy chậm nhìn nền/tường có chi tiết, tránh chỉ nhìn tường trắng.
3. Chọn **Ghi tuyến**. Nếu đã có tuyến, bấm xác nhận **Ghi lại tuyến**. Tuyến cũ chưa bị xóa lúc này.
4. Đứng gần A, nhìn thấy **toàn bộ thẻ** rõ trong camera, giữ máy ổn định khoảng 1–2 giây đến khi app báo **Đi tới B**. Đừng quay đi chỉ vì camera đọc được QR.
5. Cầm dọc điện thoại, camera cao khoảng **1,3 m** so với sàn; đi chậm theo đường muốn ghi. Luôn nhìn môi trường thật, không che camera hoặc khóa máy. Đây là độ cao camera, không phải chiều cao người.
6. Đến B, đưa toàn bộ thẻ B vào camera và giữ yên. App tự kết thúc khi khóa được B, tuyến dài ít nhất 1 m/2 giây và lưu thành công.
7. Chỉ coi ghi xong khi màn hình báo tuyến A → B đã sẵn sàng. Hủy/mất tracking/đóng app trước B không tạo map mới và không thay tuyến hoàn chỉnh trước.

## Đi lại tuyến

1. Quay về gần A, chọn **Chỉ đường**.
2. Quét **toàn bộ thẻ A đúng bản đã ghi**. Khi khóa được vị trí, mũi tên cyan xuất hiện tự động; không cần bấm căn hướng tay, không chờ quét mặt sàn.
3. Mũi tên được giãn khoảng 1,6 m, tránh sát camera, có nhịp tiến–lùi nhẹ tối đa 8 cm. Chuyển động chỉ ở hình mũi tên, không làm đổi tuyến đã ghi. Với tuyến rất ngắn khoảng cách được thích nghi.
4. Đi theo thứ tự mũi tên. Đích là **ghim B xanh ngọc** và vòng mảnh tại vị trí B đã ghi. Khoảng cách còn lại là ước lượng theo tuyến, không phải GPS.
5. Gần điểm camera cuối tuyến, app báo **Đã tới gần B**. Điều này không có nghĩa đã quét/xác minh lại QR B trong lượt dẫn.
6. Nếu mất tracking, app ra nền hoặc vị trí nhảy: dừng đi, chọn Dừng chỉ đường, quay lại A và quét lại. Không đi theo đường đang lệch hoặc xuyên vật cản.

## Cần quay màn hình lượt thử nào?

- Một lượt đầy đủ: Ghi tuyến → A → đi qua góc → B → báo lưu.
- Thoát/mở lại app để xác nhận tuyến vẫn còn. Chỉ đường → A → đi tới B.
- Đứng ở A lần dẫn thứ hai lệch nhẹ so với lần ghi, nhưng vẫn gần A và thấy rõ thẻ, để đánh giá tự căn giữa hai phiên.
- Một lượt bấm Hủy khi đang ghi lại, rồi Chỉ đường để xác nhận tuyến trước vẫn còn.
- Khi gặp lỗi, giữ lại thông báo và video từ trước lúc quét A, đừng chỉ chụp phần mũi tên sau đó.

## Giới hạn chưa thể bỏ qua

Độ cao sàn lấy từ camera đầu tuyến trừ 1,3 m, **chưa đo sàn thật**. AR có thể trôi, sai nếu thẻ in sai cỡ/di chuyển, thiếu sáng, bề mặt thiếu chi tiết hoặc cầm máy quá khác độ cao. Bản này không tránh vật cản, không lập đường mới khi bị chặn, không dùng an toàn quan trọng. Nếu cần chạy xa/nhiều phòng cần thêm mốc trung gian và kiểm thử riêng.

Kiểm thử Editor/ảnh mô phỏng không xác nhận camera hoặc độ chính xác trên Samsung thật. Chỉ nghiệm thu AR sau lượt đi trong phòng với thẻ thực tế.
