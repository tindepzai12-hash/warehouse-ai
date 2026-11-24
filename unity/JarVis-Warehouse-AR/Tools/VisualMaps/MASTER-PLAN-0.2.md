# JarVis Maps 0.2 — kế hoạch và prompt triển khai

Ngày 25/10/2026. Yêu cầu mới thay phạm vi một map bằng thư viện nhiều map. Giữ REDESIGN-MAP-FIRST.md làm yêu cầu hình học, nhưng không giữ giới hạn một map của tài liệu đó.

## Bổ sung UX theo phản hồi của người dùng
- Bấm Quét phòng là tự chụp ngay khi AR/camera cung cấp dữ liệu; không đợi QR, kết nối PC hoặc căn sàn. Căn sàn chỉ cần cho sơ đồ đi bộ/tuyến, không chặn thu ảnh phòng.
- Gợi ý từng hành động theo dữ liệu thật: độ sáng ảnh thấp → bật thêm đèn; chói → hướng lệch nguồn sáng; máy chuyển động nhanh → chậm lại. Đếm ảnh đã ghi, không tự tăng phần trăm theo thời gian.
- Trái/phải dựa trên các hướng ảnh đã lưu quanh vị trí đang đứng, chỉ là gợi ý bổ sung góc ảnh; không nhận là biết phần phòng còn thiếu hoặc ép quét 360°. Không nhắc đổi góc khi người dùng đang di chuyển.
- Cảnh báo không xóa tiến độ hoặc bắt khởi động lại. Ảnh dùng được vẫn lưu; chỉ bỏ khung gần đen/trắng/trống. Mất tính liên tục pose phải giữ và cho xuất dữ liệu, nhưng không giả vờ đã tạo tuyến chính xác.
- Màn camera giữ một hướng dẫn chính, số ảnh đã lưu và nút Lưu lượt quét; không mở thêm màn hình cho từng điều kiện ánh sáng. Không tuyên bố đo được lux hay độ phủ toàn phòng từ luma/pose.

## Sản phẩm cần làm
- Android: tạo nhiều lượt quét phòng, đặt tên, tự lưu, xem thư viện, xuất ZIP hoặc gửi PC; không cần mạng để chụp/lưu/xuất.
- PC: nhận ZIP cả qua mạng lẫn nhập từ file; kiểm tra, dựng bản đồ định vị, phân tích hình học khi có depth, nhận diện đồ vật; giữ từng map độc lập.
- Đồng bộ: app chọn đúng map ID và revision, không có map toàn cục ngầm thay thế map khác. Lỗi xử lý bản mới không làm hỏng bản cũ.
- Nhận diện: bước đầu nhóm đồ vật phổ thông bằng mô hình OpenCV Zoo NanoDet có license và checksum. Không tự nhận SKU, tên người, tường hay kính nếu mô hình không hỗ trợ. Nhãn là gợi ý cần duyệt, không phải bảo đảm đường trống.
- Chỉ đường: giữ cơ chế định vị COLMAP/hloc, nhưng không gọi map có ảnh là map đã kiểm tra vật cản. Mọi trạng thái chưa đủ dữ liệu phải hiển thị đúng và khóa phần cần dữ liệu đó.
- Không dịch vụ trả phí, không upload ảnh lên Internet, không cài Torch/CUDA hàng GB, không xóa dữ liệu cũ, không sử dụng E.

## Kế hoạch theo mốc
1. Audit owner và API, viết hợp đồng/prompt/tiêu chí nghiệm thu. Hoàn tất trước khi sửa runtime.
2. Thư viện nhiều map và revision, nhập ZIP cục bộ/HTTP, cách ly lỗi, endpoint chọn đúng map. Kiểm thử lưu và khởi động lại.
3. Capture phòng độc lập A/B, lưu checkpoint, danh sách lượt chụp, xuất/gửi lại; bổ sung Raw Depth/confidence có metadata nếu runtime cung cấp.
4. PC phân tích: map định vị, lớp hình học có unknown, kiểm tra hành lang, sơ đồ nhìn từ trên; nhận diện đồ vật bằng model thật.
5. UI Android gọn: thư viện, chụp, xem trạng thái map, kết nối; thao tác chính rõ, scroll và safe area. QR A/B là ID, không phải phép đo pose.
6. Kiểm thử unit/API/archive/geometry/model thật, Unity compile và UI hai tỷ lệ, APK ký/version. Không xem build là kiểm thử camera.
7. Samsung nghiệm thu: depth/pose thực, hai phòng khác nhau, export/import offline, chọn đúng map, vật cản/tường, mở lại app và mất mạng. Thiếu thiết bị/dữ liệu thì báo rõ, không bàn giao như bản đã dùng ổn.

## Prompt thực thi

### Trạng thái sau triển khai nguồn 0.2.0
- Mốc 1–2: đã có hợp đồng và thư viện nhiều map/ID/revision, nhập ZIP/rename; unit/API kiểm tra hai map và khởi động lại đạt.
- Mốc 3: đã có thu ảnh độc lập QR/căn sàn, RGB khi lấy được khung đồng bộ, Raw Depth/confidence tùy thiết bị, checkpoint/xuất thư viện. Logic Editor đạt; phần cảm biến/lưu Android phải thử Samsung.
- Mốc 4: đã có sparse localization, sơ đồ bằng chứng bề mặt từ depth, kiểm tra hành lang ứng viên và inference 2D thật. **Chưa có dựng mesh/dense hoàn chỉnh, kiểm tra khoảng trống toàn thân, tự nối nhiều lượt, hoặc quy trình duyệt/xuất bản tuyến để bật chỉ đường.** Không gọi mốc này hoàn tất chỉ vì có ảnh preview.
- Mốc 5–6: giao diện và gợi ý quét đã qua render hai tỷ lệ, compile/API/model thật và APK build. Tham khảo QA-0.2.md, không suy rộng kiểm thử fixture thành nghiệm thu camera.
- Mốc 7: chưa đạt nghiệm thu vật lý; chưa có Samsung qua ADB/WPD hoặc ZIP phòng mới tại lần kiểm tra. Không tuyên bố APK chỉ đường đã dùng ổn trên mọi map.

### Hợp đồng triển khai

Bạn đang sửa module VisualMaps của dự án Unity Android JarVis hiện có, không tạo một app demo giả dữ liệu. Đọc AGENTS.md/CODEX_CONTEXT.md, giữ thay đổi không liên quan và không truy cập E. Dùng staging hiện có để sửa và kiểm thử trước khi đưa vào D. Giữ package com.teep.jarvismaps và chữ ký debug hiện có để không mất dữ liệu khi nâng bản; bump version/code. Không bật Play làm backend khác chạy, không đổi thiết lập máy hoặc firewall.

Thực hiện một pipeline dữ liệu có thể kiểm tra được, không chỉ trình diễn mũi tên:

### Dữ liệu và lưu trữ
- Mỗi lượt có capture ID, tên, thời gian, mục đích environment/route, schema, device, calibration, floor, ảnh và pose/timestamp tương ứng. Depth/confidence là dữ liệu bổ sung có định dạng/đơn vị/kích thước/timestamp rõ; thiếu thì ghi thiếu, không tạo số giả.
- Có thể kết thúc lượt thu phòng mà chưa quét QR. Phiên bị ngắt vẫn có thể xuất dữ liệu để cứu ảnh, nhưng không được coi tuyến A–B hoàn chỉnh hoặc gộp pose qua các phiên.
- Checkpoint nguyên tử sau ảnh đã lưu thành công. Lưu lại bản gốc khi đóng gói/upload lỗi. Danh sách đọc được sau khi đóng/mở app. Không tự xóa ảnh hoặc bundle.
- Thư viện chứa nhiều map. Map có ID bất biến, revision, tên người dùng, trạng thái xử lý, nguồn capture, trạng thái hình học và tuyến; tất cả API định vị chỉ rõ map ID. Không suy map từ bản upload gần nhất.
- Map đang processing/failed không che hoặc làm mất map khác. Không ghi đè bản đã duyệt khi import lại; cập nhật tạo revision liên kết, không tự merge nhiều phòng.

### Bảo mật và trao đổi
- Giữ token LAN, giới hạn payload, không redirect/public URL, không log ảnh/token. Không mở firewall hay internet tunnel.
- ZIP chỉ chứa manifest, đúng ảnh và đúng depth/confidence đã khai báo. Chặn path traversal cả slash/backslash, duplicate, symlink/encrypted entries, zip bomb, timestamp/shape/pose/intrinsics không hợp lệ. File lỗi không được tạo map ready.
- Cùng pipeline validate cho nhập ZIP trên PC và upload Android. PC có lệnh nhập offline rõ, nhận đường dẫn tuyệt đối như dữ liệu, không nối thành shell lệnh.
- Endpoint thư viện trả JSON mảng wrapper tương thích JsonUtility; selection dùng ID đã validate, không nhận path người dùng. Object query có requestId, mapId khi cần, timeout và tối đa ảnh; không lưu query camera mặc định.

### Quét, dựng và hình học
- Giữ chụp trong app để có intrinsics/pose. Không khuyên quay video ngoài rồi tự đoán calibration.
- Khi có Raw Depth/confidence: chỉ nhận cặp đồng bộ và đủ format; bảo toàn chiều raw ảnh, stride, đơn vị. Không ép ảnh depth theo display rotation nếu chưa kiểm tra transform.
- Sparse COLMAP/hloc phục vụ định vị. Geometry phục vụ nhận biết bề mặt và kiểm tra hành lang; không thay bằng nhãn object detector.
- Vùng không có bằng chứng giữ unknown. Không vẽ floor xuyên tường, không dùng camera trajectory làm free-space mask. Kiểm tra thân người/các góc cua và toàn đoạn, không chỉ waypoint.
- Nếu chưa có geometry đủ tốt hoặc chưa duyệt: map dùng xem/thu thêm, không ghi navigation-ready. Model nhận ra ghế không có nghĩa đã biết vị trí 3D ghế; nếu thiếu depth thì kết quả ở ảnh 2D.
- Tạo ảnh sơ đồ/JSON phân tích có chú giải, provenance và báo cáo lỗi. Nếu không dựng được vẫn giữ capture và báo lý do cụ thể; không phát sinh bản đồ phòng giả.

### Nhận diện và UI
- Tải mô hình nhỏ từ nguồn chính thức, pin digest/license, có thông báo khi model chưa có. Kiểm thử inference thật, test ảnh trống không được trả detections giả.
- Nhận diện phổ thông là chức năng hỗ trợ, không nhận mọi object/SKU và không bảo đảm phát hiện vật cản. Không dùng confidence detector như độ chính xác định vị.
- Native uGUI/font/brand hiện có; không rebuild màn Warehouse. Trang đầu hành động chính là thư viện và quét phòng; PC/nhận diện đặt theo ngữ cảnh. Camera có nút dừng, trạng thái rõ, không log dài phủ camera.
- Library cuộn được, tên dài wrap/ellipsize, button >=44 logical px; chọn map có phản hồi, trạng thái draft/định vị được/thiếu geometry cần phân biệt bằng chữ.
- Progress là số ảnh/depth hợp lệ, bước xử lý và byte upload. Chỉ hiện % phủ sau khi có phạm vi và mẫu số, không tự tăng theo thời gian.

### Kiểm thử và bàn giao
- Test hai map khác nhau, path traversal/ZIP lỗi, tên có Unicode, lưu khởi động lại, sai ID/revision, import lỗi, request cũ, map chọn khác đang dựng, model vắng/ảnh trống/inference thật.
- Test geometry có tường cắt đường, hành lang hẹp, góc cắt, unknown, vật thấp/cao và mất depth. Không có dữ liệu thì từ chối, không xem như pass.
- Sau chỉnh code: compile, UI hai tỷ lệ, báo console mới, build và kiểm APK/signature. Không xoá legacy không rõ tham chiếu.
- Nghiệm thu thật riêng: không tự nói đã cài/đi thử Samsung khi ADB chưa nhận; không dùng synthetic làm số đo độ chính xác phòng. Bàn giao rõ phần đã chạy, phần chưa xác nhận và dữ liệu cần người dùng thu.

## Tự rà soát prompt trước triển khai
- Đã giải quyết phụ thuộc vòng: capture phòng không cần route, có thể xuất trước khi server dựng.
- Nhiều map dùng ID tường minh, không map ngầm toàn cục.
- Không đồng nhất object recognition, localization và obstacle geometry.
- Không cam kết nhận biết mọi vật hoặc đường an toàn tuyệt đối; yêu cầu gate và thử thực tế.
- Có offline capture/export và PC import cùng validator, không lệ thuộc upload trực tiếp.
- Có migration giữ dữ liệu, trạng thái rõ và tiêu chí lỗi cụ thể; không đặt nút chức năng giả.
- Đánh giá: đủ chặt để triển khai theo mốc. Độ chính xác và thời gian scan phải đo trên dữ liệu phòng, chưa được phép tuyên bố đạt.
