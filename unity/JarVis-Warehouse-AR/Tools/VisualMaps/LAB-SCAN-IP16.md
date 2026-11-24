# Thu dữ liệu lab bằng iPhone 16 và JarVis Maps

Ngày 26/10/2026. Người dùng xác nhận iPhone16/16Plus. Không có LiDAR trên các model này; có thể quét bằng camera. Chưa cần mượn máy Pro. Chất lượng cần thử một vùng nhỏ trước.

## App iPhone được chọn: Scaniverse — Classic / Mesh

- Cài đúng Scaniverse của Niantic Spatial: https://apps.apple.com/app/id1541433223
- Chọn trải nghiệm Classic nếu app đưa lựa chọn Classic/New. Classic giữ xử lý cục bộ và xuất scan; New/Site/VPS là luồng khác, có quota cloud. Không cần bật subscription hoặc đăng scan lab lên bản đồ công khai.
- Quét mới -> Mesh (nếu có chọn loại). Với cả phòng dùng Area/Large nếu phiên bản hiện lựa chọn kích thước. Tên nút có thể khác theo bản; ưu tiên loại MESH, không Splat cho bước dựng hình học.
- Thử 20–30 giây ở một góc phòng -> xử lý -> Share/Export Model -> OBJ hoặc GLB -> Save to Files. Nếu phiên bản chặn mesh export miễn phí hoặc không có Mesh, dừng ở lượt nhỏ và gửi ảnh màn Export; không quét cả lab rồi mới phát hiện chỉ xuất được video/SPZ.
- OBJ: giữ nguyên .obj + .mtl + texture trong ZIP. GLB tiện một file. PLY phải xác định là point cloud hay Gaussian splat; không coi mọi PLY là mesh. Video xoay mô hình không thay được dữ liệu 3D.
- Giữ Save Raw Data nếu tùy chọn có; chưa hứa app cho xuất camera poses/raw depth. Mesh từ Scaniverse không thể dùng trực tiếp thay JPEG+intrinsics cho hloc.

Nguồn nhà phát triển: https://www.nianticspatial.com/capture/scaniverse-release-notes (hỗ trợ non-LiDAR và giữ tài khoản cá nhân/export); https://www.nianticspatial.com/docs/scaniverse/quickstart/ (Classic vs New); https://apps.apple.com/app/id1541433223 (giá/các kiểu xử lý/xuất). Thông tin kiểm ngày trên; cần xác nhận Export trong chính phiên bản đang cài.

## Trước khi quét cả hai điện thoại

1. Chọn vùng thử: cửa lab -> một đoạn lối đi -> bàn/đích, khoảng 5–10m. Không quét cả khuôn viên trong lượt đầu. Về sau chia khu cửa, hành lang, phòng, các vùng chồng lấn.
2. Đặt ba mốc A, B, C khác nhau trên bề mặt cố định; C lệch khỏi đường A–B để tạo tam giác. Có thể là bảng giấy với chữ/cross đậm rõ. QR A/B vẫn dùng cho tuyến. Không chỉ dán lên đồ vật dễ dời.
3. Chụp ảnh tổng thấy vị trí mốc, đo AB và AC/BC bằng thước; ghi đơn vị mét. Chụp kích thước giấy/cạnh QR, chiều cao tâm mốc so sàn và một chiều rộng cửa. Nếu đo được 4 mốc càng tốt. Đây là kiểm tỷ lệ/căn hệ, không phải GPS.
4. Giữ nguyên vị trí cửa/ghế/mốc và điều kiện ánh sáng trong hai lượt iPhone/Samsung. Tên chung LAB-ZONE01; mỗi máy có scan ID riêng.

## Cách đi và hướng camera

- Bấm Record trong app quét, rồi đi chậm; app tự thu ảnh, không cần chụp từng ảnh bằng nút Camera iPhone.
- Giữ camera1x, tránh đổi lens/zoom; khoảng 1–3m từ tường/đồ vật là mục tiêu thử, không phải giới hạn thiết bị. Di chuyển ngang/chếch để thấy thị sai; không chỉ xoay tại chỗ.
- Lượt đầu bắt đầu nhìn A + cửa/góc phòng, đi qua B và C; nhìn đồng thời phần sàn, chân bàn, tường. Quét cả lối đi và các bên, không chỉ nhìn thẳng một bức tường trắng.
- Ở mỗi góc/vật phẩm, đi thêm hai góc nhìn cách nhau ~20–40cm, giữ phần cảnh cũ chồng lấn nhiều. Giảm tốc ở góc, cửa, chân vật cản.
- Đi vòng khép kín về vùng A nếu bố trí cho phép; nhìn lại mốc cũ. Nếu app báo mất tracking, lưu lượt đang có rồi tạo lượt mới; không cố nối một map đã nhảy.
- Xem kết quả từ trên xuống và nghiêng: cửa bịt kín, tường đôi/lệch, chân bàn biến mất, sàn lỗ nhiều -> giữ bản đó để chẩn đoán và quét bổ sung riêng. Không cắt/xoay/scale bản gốc.

Kỹ thuật chung từ nhà phát triển: https://www.nianticspatial.com/docs/scaniverse/techniques/ — di chuyển đều, chồng góc, lấy nhiều vị trí, kiểm phần mesh thiếu; thời gian dài không đồng nghĩa phủ đủ.

## Samsung / JarVis Maps0.2.1: lượt thu cho ngày mai

1. Cài cập nhật JarVis-Maps-0.2.1.apk, giữ app cũ/data. Mở -> Quét phòng mới. Không cần PC/QR để bắt đầu thu phòng.
2. Đi lại cùng vùng/mốc như iPhone. Hướng tới A/B/C và đồ vật cố định từ nhiều góc. Xem số ảnh đã lưu và hướng dẫn ánh sáng/chuyển động; không cần đợi % toàn phòng.
3. Tối thiểu20 ảnh là điều kiện dựng, không phải bảo đảm đủ. Với một vùng lab nhỏ, mục tiêu thử80–150 ảnh rõ để có nhiều góc; giới hạn app/server hiện600 ảnh, 220MB ZIP. Chia vùng trước khi quá giới hạn.
4. Nếu muốn kèm tuyến ứng viên: căn sàn thật (không căn mặt bàn), thu>=20ảnh rồi Ghi A→B; quét A, đi lối an toàn tới B, quét B kết thúc. Không quét được B thì giữ lượt ảnh để phân tích, không giả tuyến đã hoàn tất.
5. Lưu lượt quét -> đặt tên LAB-ZONE01-SAMSUNG -> Xuất ZIP vào Download/JarVisMaps. Gói chứa ảnh + pose + intrinsics + manifest, depth/confidence nếu sensor cung cấp. Giữ ZIP, không chỉ gửi video màn hình.
6. Ghi thêm video màn hình30–60giây để chẩn đoán thông báo/độ trễ nếu cần; video bổ sung không thay ZIP.

## Bộ file chuyển cho PC

LAB-ZONE01/
- iphone/scan-original.obj+.mtl+textures hoặc scan-original.glb
- samsung/<capture>.zip (nguyên gói)
- reference/anh-moc-A-B-C.jpg, kich-thuoc.txt
- reference/man-hinh-loi.mp4 nếu có
- notes.txt: tên máy, app/version, thời gian, vùng đã quét, vị trí A/B/C, đồ vật/cửa bị dời

Không yêu cầu cloud. Chép qua Files/USB hoặc cách chia sẻ file đang dùng. Giữ nguyên iPhone và Samsung originals; nhận đủ file mới bắt đầu ghép.

## Pipeline cho lượt phát triển ngày27/10/2026

1. Kiểm mesh/ZIP/chất lượng/cảnh trùng, dựng sparse Samsung như hiện có.
2. Chọn tối thiểu3 cặp điểm3D không thẳng hàng từ mốc/cạnh cố định, thêm điểm kiểm độc lập. Căn hệ mét/gravity bằng rigid/Sim3 đúng đơn vị; ghi transform/provenance và residual từng mốc.
3. ICP chỉ tinh chỉnh khi đã căn thô và có vùng overlap; không ép hai phòng lặp/hành lang giống nhau tự khớp. Điểm kiểm độc lập vượt sai số mục tiêu thì chưa công bố.
4. Dùng mesh iPhone bổ sung tường/chân vật cản; depth Samsung giữ ray/confidence. Tạo floorgrid/obstacles/unknown theo tầng cao và kiểm headroom; vật động/kính vẫn cần xem thực tế.
5. Duyệt tuyến A–B trên PC, publish mapID/revision riêng rồi trả Android. Cùng T_map_to_AR điều khiển vị trí minimap và chevron AR. Không ghép bằng GPS hoặc chỉ vì hai mốc có cùng tên.
6. Kiểm ba lượt đi thật A/giữa/B và số đo; chỉ gọi bản kết hợp đạt sau khi có evidence. Bản hôm nay thu dữ liệu và thêm UI/logic; không tự hứa ngày mai đạt accuracy trước khi nhận scan.

## Lưu ý về app thay thế

3d Scanner App của Laan Labs cần LiDAR theo help https://3dscannerapp.com/help/; không chọn cho iPhone16 thường. Không nhầm các app gần tên hoặc trang3dscannerlidar.com với sản phẩm Laan. Immersal Mapper phục vụ visual localization, không tự cung cấp đầy đủ mesh vật cản/floorplan cần ở đây.
