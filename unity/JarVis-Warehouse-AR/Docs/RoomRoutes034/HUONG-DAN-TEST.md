# JarVis Routes 0.3.4 — bản thử sửa mũi tên / tiến độ

## Cài đúng bản
Cài đè JarVis-Routes-0.3.4.apk, không gỡ app / xóa dữ liệu. Trên dòng trạng thái đầu màn hình phải thấy **v0.3.4**. Nếu chưa thấy, chưa chạy đúng bản sửa này. MTP/ADB không xác nhận cài đặt thay người dùng.

Giữ hai thẻ Room-A / Room-B cũ, toàn bộ ảnh mốc 18 × 18 cm, dán phẳng thẳng đứng. Không chỉ phóng QR ở giữa. Nếu đổi phòng, dời A/B hoặc đổi lối, cần ghi tuyến mới; chuẩn bị môi trường không tự chuyển map cũ sang phòng khác.

## % và thời gian chờ
- **1/2 — Quan sát môi trường, 0–80%:** camera có frame mới, AR ổn định, dịch nhẹ tối thiểu 8 cm, lia chậm ít nhất 15°, đủ điểm chi tiết ở nhiều vùng nhìn. Nhìn cạnh đồ vật / sách / kệ, không chỉ tường trắng. Không cần đi vòng quanh phòng.
- **2/2 — Khóa vị trí A, 80–99%:** đưa toàn bộ thẻ A vào camera, giữ thẻ ổn định. Đọc được QR chưa đồng nghĩa khóa được vị trí 3D.
- **100%:** A đã khóa. Khi Chỉ đường, bấm Bắt đầu đi; không đi chỉ vì đã đọc được QR.
- Có số % ở header, thanh tiến độ trong card và trạng thái khung quét. Khi đi, AR đã chuẩn bị 100% không phải đã đi xong 100% quãng đường.
- Khi ghi và chờ B: 0% nghĩa chưa khóa B, không phải mất toàn bộ quãng đường đã ghi. Đưa thẻ B vào camera để khóa / kết thúc.
- Không có thời gian hoàn tất cố định. Sau 8 giây chưa đủ điều kiện, app nhấn mạnh việc còn thiếu. Không tự chạy % bằng thời gian, không bịa số giây còn lại, không yêu cầu tải lại map nếu chỉ thiếu sáng/chi tiết. QR được đọc song song lúc chuẩn bị nhưng không được vượt bước an toàn.

## Mũi tên
| Khoảng còn lại dọc tuyến | Tối đa |
|---|---|
| ≤2 m | Không mũi tên, tập trung B |
| >2–8 m | 1 |
| >8 m | 2 |

Mũi tên nằm trên tuyến đã ghi, cách khoảng 2,2 m; không chỉ hướng của nhánh sau góc khi chưa tới góc. Không đè lên nhau hoặc các card. Bề rộng hình chiếu ≤14% màn hình và giới hạn kích thước vật lý. Camera không thấy sàn thì app hướng dẫn chúc máy xuống, không đưa mũi tên giả lên tường. Có thể thấy ít hơn mức tối đa do góc nhìn / gần góc rẽ. Giữ mũi tên tiến–lùi nhẹ; giảm chuyển động thì đứng yên.

## Lượt nghiệm thu cần quay màn hình
1. Kiểm tra v0.3.4; ghi tuyến thẳng mới 3–5 m trong phòng: quan sát → A → đi → B.
2. Quay lại A: Chỉ đường → quay màn hình từ bước môi trường → A → 100% → Bắt đầu đi → B. Tuyến thẳng không có nhánh rẽ giả. Lúc còn 5,5 m không được có ba mũi tên.
3. Thử một góc thật, sai thẻ, che camera và đưa app ra nền/mở lại. Nếu AR mất/nhảy vị trí, dừng và quét lại A; không đi xuyên vật cản.
4. Nếu % đứng: quay cả số % / thông báo đang thiếu. Nếu B lệch: quay từ lúc A và cung cấp dữ liệu tuyến; không suy đoán mọi lệch là do mũi tên.

Đây là bản thử ARCore, sàn ước lượng theo độ cao camera 1,30 m. Chưa tự nhận diện toàn phòng, không phát hiện vật cản; vẫn có thể trôi tọa độ. Test Unity không chứng minh độ chính xác đi thật trên Samsung.
