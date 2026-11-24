# JarVis Maps 0.2.1 — bản thu dữ liệu lab và kiểm thử

Ngày 26/10/2026. Mục tiêu hiện tại: thu dữ liệu Samsung và iPhone để xử lý/căn hệ trên PC. Chưa có dữ liệu lab thật để nghiệm thu định vị, tránh vật cản hoặc bản đồ kết hợp.

## APK và chuyển Samsung

- APK dự án: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Maps-0.2.1.apk`.
- Bản gửi trực tiếp: `JarVis-Maps-0.2.1.apk` cạnh tài liệu này.
- Package `com.teep.jarvismaps`, versionName `0.2.1`, versionCode `4`, ARM64, Android API29 trở lên.
- Dung lượng APK thật: **50.471.983 byte** (khoảng 48,1 MiB); tổng byte trong Unity BuildReport không phải kích thước APK.
- SHA256 `B94EA12BC8C6F5C3F615EA6901E4F51003C55A6017F2C25C4B960F7F14E7D92B`.
- apksigner xác minh chữ ký v2 thành công; chứng thư SHA256 `13163f8a75bbbdfdda62fb60ffbc9f03822b2b22fa6410fd2483339225dda49d`, khớp bản Maps trước.
- **Đã chép tới Galaxy S20 Ultra 5G → Bộ nhớ trong → Download** qua MTP. Đã đọc ngược file từ điện thoại về PC; số byte và SHA256 khớp hoàn toàn.
- Chưa tự cài APK hoặc chạy camera/đi thử trên điện thoại. Mở My Files → Download → JarVis-Maps-0.2.1.apk → Cập nhật. Không gỡ app để giữ các lượt quét cũ.

## Những gì đã thay đổi

1. Minimap 2D dùng cùng hệ tọa độ với AR; vị trí và hướng camera lấy từ pose định vị thật, không ép biểu tượng vào tuyến. Khi chưa định vị hoặc thiếu hình học, giao diện ghi rõ.
2. Map đã dựng có thể mở chế độ định vị 2D, kể cả khi tuyến chưa được duyệt để chỉ đường. Không dùng việc này để vượt điều kiện nghiệm thu tuyến.
3. Mũi tên trắng viền xanh: một chevron khi đi tiếp, ba khi rẽ; nhịp chuyển động nhẹ, tự thu kích thước theo khung nhìn. Điểm nhìn trước tính bằng mét theo đường ghi, giảm nhảy qua đoạn đường quay đầu ở gần nhau.
4. Nhận diện vật phẩm vẽ khung trên đúng ảnh đã gửi phân tích, giữ ảnh cố định; chưa phải theo dõi vật thể 3D trong camera trực tiếp.
5. Giữ quét phòng tự chụp, nhắc ánh sáng/chuyển động, lưu nhiều lượt, xuất ZIP ảnh/pose/intrinsics và depth nếu có. Không hiển thị phần trăm bao phủ toàn bộ phòng khi chưa có cơ sở đo.
6. Launcher PC xử lý được môi trường Python bundled trên máy này; không cài thêm phần mềm hoặc thay cấu hình Python toàn máy.

## Kiểm tra đã thực hiện

- Unity logic: **54 kiểm tra đạt**, gồm lookahead độc lập mật độ waypoint, góc rẽ, đường quay đầu, pose/map transform và dữ liệu hình học.
- Unity UI: **434 kiểm tra bố cục đạt, 36 khung hình render** ở 412×915 và 412×732; gồm minimap, nhận diện ảnh và mũi tên. Đây là fixture Editor, không phải ảnh đi thử trong phòng.
- PC: **52 kiểm tra đạt qua hai lượt chạy** (50 kiểm tra thông thường; 2 kiểm tra HTTP chạy lại với quyền mở socket sau khi sandbox chặn). Không bỏ qua lỗi HTTP.
- APK build thành công; chữ ký, version và chuyển file được kiểm tra như trên.
- Chưa nghiệm thu thực địa: tracking Samsung, độ lệch A/B, camera/depth thật, mức sai số minimap, mesh iPhone hoặc bản đồ ghép hai máy.

## Việc cần làm trên hai điện thoại

1. Samsung: cài cập nhật → Quét phòng mới → đi chậm quanh vùng nhỏ, nhìn cả lối đi và các bên → lưu lượt quét → xuất ZIP nguyên gói. Hướng tới 80–150 ảnh rõ cho một vùng nhỏ; tối thiểu20 ảnh không bảo đảm dựng được. Giới hạn hiện600 ảnh/220 MB ZIP.
2. iPhone16/16Plus: dùng Scaniverse của Niantic Spatial, thử Classic/Mesh ở một góc20–30 giây và kiểm tra xuất OBJ/GLB miễn phí trước. Máy này không có LiDAR; chưa cần mượn máy Pro để thu lượt đầu.
3. Giữ ba mốc cố định A/B/C không thẳng hàng ở cùng vị trí khi quét cả hai máy; đo khoảng cách và kích thước mốc. Giữ mesh iPhone cùng texture, ZIP Samsung, ảnh mốc và số đo gốc.
4. Khi đã có đủ dữ liệu: kiểm dựng, căn hai hệ bằng mốc và điểm kiểm độc lập, dựng sàn/tường/vật cản/unknown, duyệt tuyến và thử đi thật. Không đồng nhất hai map chỉ bằng tên QR.

Đọc `LAB-SCAN-IP16.md` để làm từng bước; `UPGRADE-0.2.1-PLAN.md` chứa kế hoạch và prompt triển khai tuần tự cho GPT-6.1 High. Các bước ghép mesh/nhận diện3D/duyệt vật cản là công việc tiếp theo, chưa được tuyên bố hoàn tất trong APK này.

## Chạy PC

Môi trường đã có trên máy hiện nằm cùng tài liệu này trong thư mục làm việc Codex (`RouteRecorder\VisualMaps`). Chạy `START-PC.cmd` tại đây; PC và Samsung cùng Wi-Fi tin cậy. Không đóng cửa sổ server khi đang gửi dữ liệu. Bản sao nguồn ở dự án `Tools\VisualMaps` không chép lặp `.venv`, vendor hoặc dữ liệu phòng để tiết kiệm dung lượng; nếu chạy ở vị trí đó cần thiết lập theo hướng dẫn PC hiện có.

Lần cập nhật này không xóa lượt quét hay các bản đồ cũ; không truy cập ổ E.
