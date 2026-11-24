# Prompt thực thi / tự duyệt — JarVis Routes 0.3.3

## Bằng chứng và phạm vi
Video mới dài 21,17 giây đã xem toàn thời lượng qua 42 mẫu cách 0,5 giây và các ảnh gốc. Thấy nhiều mũi tên/nhánh gần nhau, pin B và nhãn phóng rất lớn khi gần camera, khoảng còn lại vẫn khoảng 6 m; sau đó cảnh báo lệch ước lượng hơn 3 m. Chưa có JSON map thật nên không kết luận dữ liệu tuyến/căn A chính xác, không ép thẳng hay tăng ngưỡng để giấu lệch. Các sọc màn hình có trong video nhưng chưa đủ dữ kiện kết luận do app hay phần cứng.

Chỉ sửa app thử tuyến phòng ở D, giữ hai module / một map và chữ ký cập nhật. Không truy cập E, không xóa map, không thêm GPS/Immersal/mapping cloud, không tuyên bố đã tái tạo toàn phòng hay tránh vật cản.

## Quy tắc mũi tên (khoảng còn lại dọc tuyến)
| Còn lại | Tối đa cùng lúc |
|---|---|
| ≤2 m | 0 — tập trung B |
| >2–5 m | 1 |
| >5–10 m | 2 |
| >10 m | 3 |

Chỉ dùng ba đối tượng tái sử dụng, không tạo hàng trăm mũi tên toàn tuyến. Mốc mũi tên cố định theo chiều dài tuyến, cách khoảng 2,2 m; bắt đầu phía trước ≥1,2 m, nhìn trước ≤6 m, không sát B (<0,8 m). Chỉ hiện đoạn hiện hành và một phần ngắn sau góc kế tiếp, không lộ các nhánh quay lại/giao nhau sau đó. Kiểm tra trước camera/frustum; rộng tối đa khoảng 18% màn hình, bề rộng vật lý không quá khoảng 0,31 m. Pin B khoảng 6% chiều cao vùng nhìn, có trần theo khoảng cách và nhãn gọn, không đè header/footer; vòng đích giữ tại vị trí map.

## Chuẩn bị trước di chuyển
1. Trên cùng HUD, yêu cầu quan sát đồ vật/sàn có chi tiết, dịch máy nhẹ và lia chậm. Đọc ARPointCloudManager thật (không prefab trực quan), lọc điểm đang tracking, phía trước camera, confidence nếu có; đếm ≥20 điểm rải ≥3/6 ô nhìn và ổn định ≥0,5 giây. Kết hợp camera tươi, tracking liên tục, ≥12 frame, ≥1,3 giây, dịch ≥8 cm và đổi góc ≥15°.
2. 0–80% là tổng điều kiện chuẩn bị môi trường đã đạt, không phải % diện tích phòng/map tái tạo; 80–99% đo khóa pose A; 100% chỉ sau QR đúng và pose ổn định thật. Mất camera/tracking/feature freshness có thể giảm tiến độ; không timer tự chạy 100%.
3. Đã khóa A khi Chỉ đường thì hiện bản đồ tuyến đã lưu / độ dài và nút hiện hữu **Bắt đầu đi**. Chưa cho hiện mũi tên và chưa tăng tiến độ đường đi trước khi người dùng xác nhận; camera/pose mất thì phải phục hồi/quét A đúng quy tắc cũ.
4. Nếu thiếu đặc trưng, nói rõ đang thiếu và cách lia/đủ sáng; không yêu cầu tải/cài lại map vô căn cứ. Không có dữ liệu feature không được giả thành đã quét. Map được căn theo thẻ A, không phải tự nhận dạng lại cả căn phòng.

## UI ownership / kiểm thử
Repair native uGUI: WalkingRouteRecorderUi / RoomScanReticle, cùng Canvas safe-area và hai card sẵn có; không UI Toolkit, không màn mới, không asset raster mới. Unity 6000.4.7f1, bridge trực tiếp không có; dùng batch Editor thực để biên dịch/render/PlayMode. Snapshot trước: V032 source + Logs/RoomRoutes native captures. Agent tự duyệt: phạm vi và quy tắc phù hợp yêu cầu; phần trăm được định nghĩa rõ, không nói dối scan map.

Test: budget biên 2/5/10 m, pool ≤3 với tuyến dài, loop/crossing/corner, arrow scaling và near-plane, pin/label cap, đủ/thiếu/stale feature và ô nhìn, progress không100 khi chưa khóaA, pause/rescan, chưa bấm không chạy, UI 412×915/412×732 toàn trạng thái, PlayMode, APK/code/signature/hash và chép Samsung đọc ngược. Cần người dùng đi thật để xác nhận AR; chưa được gọi bàn giao nghiệm thu vật lý.

Nguồn: https://developers.google.com/ar/develop/fundamentals ; API point cloud/positions/confidence/trackablesChanged đối chiếu package AR Foundation hiện cài. Feature points giúp tracking, không chứng minh đã quét x% toàn phòng.

## Tiến độ
- [x] Xem video và tự duyệt prompt/rules.
- [x] Triển khai.
- [x] Test / render / duyệt hình: 1636 checks và PlayMode nhiều frame đạt; xem QA.md. Đi thật chưa nghiệm thu.
- [x] Build / chữ ký / SHA256: 0.3.3 code12, strict build errors=0; hướng dẫn đã viết.
- [ ] Chép Samsung và đọc ngược đối chiếu hash.
- [ ] Người dùng thử camera / feature readiness / căn A và đi thật tới B.
