# Bản thử phòng 0.3.0 — kế hoạch và prompt đã tự rà

## Nguồn và phạm vi

Video người dùng cung cấp dài 14,05 s: mũi tên cyan dạng đầu tên đặc, nằm ngang theo sàn, nhấn sáng theo chiều tuyến; đích là dấu xanh chỉ xuống. Contact sheet: Reference/contact.png. Chỉ tham khảo hình thức, không đưa Placenote/cloud hoặc nội dung video vào APK.

Nâng cấp owner WalkingRouteRecorder hiện có và scene Routes hiện có; uGUI, portrait, xanh–trắng, Be Vietnam Pro. Không thay app nghiệp vụ, backend, Render, kho hay tuyến cũ. Xuất một APK Routes mới. Không tạo danh sách map, minimap, nút xuất ZIP, demo 71 điểm, tài khoản hay màn thiết lập không cần cho bài thử này.

## Kế hoạch thực hiện

1. Đọc video và kiểm tra nguồn, giữ bản trước khi sửa.
2. Dùng thẻ A/B chứa QR và ảnh định vị riêng; tạo reference image library 18 cm. Kiểm tra điểm đặc trưng bằng công cụ arcoreimg có sẵn. QR chỉ nhận ID; AR image tracking đo tư thế. Không coi đọc ID là định vị.
3. Ghi tư thế camera đầu so với mốc A, đường camera tương đối và mốc B. Khi quét lại A trong phiên khác, phục hồi origin/yaw từ mốc thật; không yêu cầu người dùng đứng đúng hướng máy cũ hay bấm căn tay. Không dựng đường khi chưa khóa được mốc.
4. Lưu một tuyến hoàn chỉnh A→B hiện hành, tách draft. Chỉ thay tuyến cũ sau khi B hợp lệ và ghi file thành công. Hủy/mất tracking/đóng app không làm mất tuyến hoàn chỉnh trước. Không tự dùng tuyến 0.2 cũ làm map phòng mới.
5. Tinh gọn UI: trang chọn hai chức năng, camera với một thanh trạng thái và một thẻ dưới. Ghi: quét A → đi → tự nhận B → lưu. Dẫn: quét A → tự căn → hiện mũi tên và đích B → tới gần đích. Có Hủy/Dừng, Thử lại khi lỗi; xác nhận nhỏ khi ghi đè. Không thêm màn trung gian.
6. Mũi tên đặc cyan sát sàn, nhấn sáng theo hướng, dấu đích xanh chỉ xuống kèm B; giữ XZ cố định, không rung/nảy tuyến. Chỉ hiện các mũi tên phía trước người dùng; mất tracking/lệch tuyến phải ẩn và báo dừng. Không chờ phát hiện plane; độ cao sàn vẫn ước lượng từ camera, ghi rõ giới hạn.
7. Kiểm tra logic, sai QR, transform giữa hai phiên, hoàn tất/draft/hủy/thay tuyến/lỗi file, dẫn tuyến L, marker mất theo dõi, hai tỉ lệ màn hình, tăng chữ, quyền camera; build và xác minh APK. Kiểm tra điện thoại thật là cổng riêng, không thay bằng mô phỏng.

## Prompt triển khai

Bạn đang sửa JarVis Routes trong D:/unity of teep/JarVis-Warehouse-AR. Đọc AGENTS.md, CODEX_CONTEXT.md và owner liên quan trước khi sửa. Thực hiện đúng bài thử A→B trong một phòng: hai chức năng Ghi tuyến và Chỉ đường, một tuyến hoàn chỉnh hiện hành. Tái sử dụng camera AR/decoder và owner hiện có; không mở thêm camera, không cần GPS/server/Immersal/Placenote. Không xóa dữ liệu người dùng hay sửa kho/backend.

Ghi: chọn Ghi tuyến, yêu cầu đúng QR A, đồng thời có tư thế A hợp lệ từ AR image tracking của thẻ chuẩn; khóa tham chiếu rồi lưu chuyển động camera. Quét đúng B tự kết thúc, chỉ thành công nếu tracking tốt, đường đủ dài và lưu bền vững thành công. Giữ tuyến cũ nếu lượt mới thất bại. Không hiện bản dở trong module Chỉ đường. Lưu các dữ liệu cần để tái tạo phép biến đổi từ mốc A trong phiên AR mới, không serialize tọa độ world như thể dùng được xuyên phiên.

Dẫn: chỉ mở tuyến hoàn chỉnh đã lưu; quét đúng A và chờ tư thế đo ổn định, tự căn origin và yaw từ dữ liệu đã ghi. Không thêm bước căn máy thủ công và không âm thầm thay thế bằng ước đoán pose. Dựng tuyến world-space, các đầu mũi tên đặc cyan và dấu đích B xanh giống ngôn ngữ hình trong video. Tiến độ tính theo đường đã đi, không hướng thẳng xuyên góc. Mất tracking/nhảy vị trí/ngừng app phải dừng an toàn và yêu cầu quét A lại. Gần B là ước lượng, không báo đã xác minh QR B khi chưa quét.

UI chỉ gồm lựa chọn hai chức năng và camera toàn màn hình cùng thẻ nhỏ: việc cần làm, thông số thiết yếu và nút dừng/hủy. Không minimap, danh sách map, demo cũ, xuất ZIP hay nút điều chỉnh kỹ thuật thường trực. Nếu đã có tuyến, ghi lại cần xác nhận thay thế; tuyến trước chỉ bị thay sau khi ghi thành công. Hỗ trợ safe area, nút tối thiểu 44–48dp, chữ tiếng Việt, tỷ lệ 412×915 và 412×732. Phần camera không có dữ liệu thật trong Editor phải được đánh dấu fixture trong báo cáo, không gọi là trải nghiệm AR thật.

Thẻ A/B phải có ảnh nhận dạng khác biệt, QR đúng ID và kích thước in 18×18 cm cho toàn bộ vùng ảnh. Không chỉ đưa QR thuần vào thư viện AR vì Google cảnh báo QR/barcode ít phù hợp tracking ảnh. Người thử phải dùng thẻ mới, in đúng kích thước hoặc đo đúng vùng hiển thị; không hứa tự căn với QR cũ bất kỳ. Thông báo chính xác khi đọc được QR nhưng chưa định vị được ảnh.

Chạy các kiểm tra có chứng cứ: transform A ở hai vị trí/yaw khác nhau tái tạo cùng đường, B không kết thúc trước A, A không kết thúc tuyến, QR khác bị từ chối, tuyến <1m bị từ chối, không dẫn draft, lưu lại mở app vẫn có đúng một tuyến, thất bại thay tuyến giữ bản trước, pose cũ/Limited không dùng, tiến độ qua góc, hướng đầu tên/đích, mất tracking, hủy/back/pause, chữ/nút không chồng tại hai tỷ lệ. Không đánh dấu PASS cho việc chưa chạy. Không mở Unity thứ hai hoặc kill phiên chưa lưu. Chỉ xuất APK sau các kiểm tra nguồn/Editor đạt, kiểm chứng chữ ký/hash; báo riêng phần Samsung chưa đo. Không bịa đảm bảo không lỗi.

## Tự đánh giá prompt trước khi code

- Đủ hai luồng và một map; không mở rộng sang hệ quản lý kho: đạt.
- Khắc phục nguyên nhân căn sai giữa phiên, không chỉ thay hình mũi tên: đạt bằng image-pose + QR, còn phải thử trên máy.
- Ghi đè có giao dịch và giữ bản tốt khi lỗi: đạt thiết kế.
- Không tự coi map là mô hình 3D phòng: đạt; đây là tuyến camera + hai mốc cố định.
- Phản ánh video và đích AR rõ; không sao chép UI che camera: đạt.
- Rủi ro được nêu: in sai cỡ/thẻ bị di chuyển/AR trôi/độ cao ước lượng; không có tránh vật cản. Không được hứa chính xác trước thử thật.

Kết luận: đủ rõ để bắt đầu triển khai trong phạm vi. Đây là tự duyệt của agent theo yêu cầu, không phải người dùng đã nghiệm thu sản phẩm. Ghi kết quả sau khi chạy trong QA.md.

## Điều chỉnh sau phản hồi ảnh preview

Giãn mũi tên khoảng 1,6m thay vì 0,85m; tránh sát camera, nhưng tuyến rất ngắn vẫn có mũi tên. Hiệu ứng tiến–lùi chỉ dịch glyph tối đa 8cm dọc tiếp tuyến, chu kỳ khoảng 1,8s, không làm đổi dữ liệu tuyến hay điểm đích. Giảm chuyển động thì tắt hiệu ứng này. Bỏ cụm ba mũi tên xanh chồng và đĩa đặc: dùng ghim B xanh ngọc, vòng đích mảnh rỗng, nhãn trắng gọn. Render lại và kiểm tra khoảng cách, biên độ chuyển động, tọa độ đích cố định trước khi xuất lại.
