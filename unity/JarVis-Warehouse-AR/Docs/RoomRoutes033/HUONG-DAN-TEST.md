# JarVis Routes 0.3.3 — thử tuyến trong phòng

## Cài và giữ dữ liệu
- Cài cập nhật `JarVis-Routes-0.3.3.apk`, không gỡ app/xóa dữ liệu trước. Hai chức năng và một tuyến hiện hành được giữ nguyên.
- Dùng đúng hai thẻ Room-A / Room-B cũ, in phần ảnh mốc đúng 18 × 18 cm, dán phẳng thẳng đứng, không di chuyển sau khi ghi. Không chỉ phóng QR trần lên kích thước bất kỳ: app cần toàn bộ ảnh mốc để đo pose.
- Nếu đổi phòng, di chuyển A/B hoặc đổi lối đi: ghi lại tuyến trong module Ghi tuyến. Quan sát môi trường không tự sửa một tuyến được ghi ở nơi khác.

## Ghi tuyến
1. Chọn **Ghi tuyến**. Lia chậm qua sàn/đồ vật có chi tiết, đủ sáng; dịch máy nhẹ khoảng 8 cm và đổi góc ít nhất 15°. Đứng ở nơi an toàn, không bước khi nhìn chằm chằm điện thoại.
2. Đạt bước môi trường (80% tổng chuẩn bị), hướng máy tới toàn bộ thẻ A. Khung quét phân biệt đang chờ camera, tìm thẻ, sai mã, đang khóa pose, đã khóa.
3. Chỉ đi khi app xác nhận A và chuyển **Đi tới B**. Đi chậm theo đúng lối muốn lưu. Quét toàn bộ thẻ B để kết thúc/lưu; đừng quét A thay B.
4. Chỉ một tuyến hoàn chỉnh hiện hành; ghi mới có hỏi xác nhận và không thay tuyến cũ nếu bị hủy/lưu thất bại.

## Chỉ đường — cần chuẩn bị mỗi lượt
1. Quay lại đúng A, chọn **Chỉ đường**. Quan sát xung quanh theo hướng dẫn trên camera, rồi quét A.
2. **0–80%:** các điều kiện camera tươi, tracking, chuyển động quan sát và đặc trưng hình ảnh thật. **80–99%:** đang khóa vị trí A. **100%:** đã khóa A thật, chưa tự bắt đầu đi.
3. Đối chiếu độ dài tuyến, vị trí A/B và phòng. Bấm **Bắt đầu đi** khi sẵn sàng. Trước nút này chưa hiện đường AR và chưa tăng tiến độ đi.
4. Giữ máy dọc, hơi hướng xuống để thấy đường. Mũi tên cyan tiến–lùi nhẹ, B xanh dương có giới hạn kích thước khi tới gần. Không cần GPS hoặc Internet cho tuyến cục bộ này.

## Quy tắc số mũi tên
| Khoảng còn lại dọc tuyến | Tối đa hiện đồng thời |
|---|---|
| ≤2 m | 0 — tập trung biểu tượng B |
| >2–5 m | 1 |
| >5–10 m | 2 |
| >10 m | 3 |

Mốc cách khoảng 2,2 m; phía trước tối thiểu khoảng 1,2 m, nhìn trước tối đa 6 m, tránh sát B. Chỉ hiện trong camera và trước góc sắp tới, không phô các nhánh vòng lại phía sau. Do góc nhìn/đoạn ngắn có thể ít hơn số tối đa. Mũi tên đi theo tuyến đã ghi, không tự tạo rẽ phải.

## Khi phần trăm dừng hoặc giảm
- Đây là **% điều kiện chuẩn bị AR**, KHÔNG phải % diện tích phòng hay % bản đồ 3D đã quét. Không có timer giả tự chạy đủ 100%.
- Thiếu điểm/ô nhìn: hướng máy qua cạnh đồ vật, sách/kệ/sàn có họa tiết, đủ sáng; tránh tường trắng, kính và lia nhanh. App hiển thị số điểm/vùng còn thiếu.
- Chờ camera/quyền camera: làm theo thông báo; cho phép Camera nếu chưa cấp. Sai mã: dùng thẻ A/B đúng bước. Không cần tải/cài lại map chỉ vì tiến độ chưa đủ.
- Mất tracking/pose nhảy: dừng, app ẩn mũi tên và hướng dẫn phục hồi hoặc về A quét lại. Lệch tuyến có B màu cam **ước lượng**, không phải lệnh đi thẳng xuyên vật cản.
- B vẫn xa sai hoặc đường lệch hẳn: dừng lượt test. Cần video từ bước chuẩn bị và dữ liệu tuyến để đối chiếu, không tự tăng ngưỡng cho qua.

## Checklist thử thực tế cần quay màn hình
- Ghi mới một đường thẳng 3–5 m, không di chuyển A/B, sau đó đi lại: môi trường → quét A → 100% → Bắt đầu đi → tới B; không có rẽ giả.
- Thử tuyến có một góc thật: chỉ đổi hướng theo đúng góc đã ghi, không hiện dày các nhánh tương lai.
- Sai QR, che camera, đưa app ra nền/rồi quay lại, hủy ghi, mở lại app: không báo khóa giả/không mất tuyến cũ.
- Quay từ khi chọn module, thấy phần trăm và các thông báo, tới B. Không đi khi camera/đường không khớp lối thật.

Đây vẫn là bản thử ARCore có thể trôi, sàn dùng độ cao camera ước lượng ~1,30 m. Không tự nhận diện toàn phòng, không dò vật cản, không đảm bảo độ chính xác bằng quét chuẩn khảo sát. Test Editor không thay được nghiệm thu đi thật trên Samsung.
