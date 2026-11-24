# Kiểm chứng JarVis Maps 0.2.0

Ngày 25/10/2026, giờ Việt Nam. Phạm vi: bản thử thu dữ liệu nhiều map và PC phân tích, không phải nghiệm thu chỉ đường tránh vật cản.

## Đã chạy
- 52 kiểm thử Python đạt: manifest schema1/2, không cần QR/sàn khi chỉ thu phòng, chưa kết thúc không được công bố, hai map/rename/revision/khởi động lại, ID/path/ZIP lỗi, depth calibration/timestamp, hành lang cắt tường/vùng unknown/bề rộng, model vắng và inference thực.
- NanoDet OpenCV Zoo được pin commit/hash, inference CPU thật nhận `person` trên ảnh fixture chính thức OpenCV; ảnh xám trống không có nhãn. Điều này không chứng minh nhận được mọi đồ vật trong phòng người dùng. Đã sửa tiền xử lý RGB/black letterbox và thứ tự đầu ra OpenCV5 theo shape.
- Hình học test bằng depth có calibration tổng hợp tạo được ô bằng chứng sàn, cản, unknown và PNG. Vẫn `navigationAllowed=false`, không tự coi sàn là khoảng trống toàn thân.
- COLMAP/hloc dựng 28 ảnh phòng **tổng hợp**, đối chiếu ảnh giữ ngoài tập; sau đó HTTP loopback thực khớp 351 điểm. Map ID sai bị từ chối; map được chọn tường minh vẫn dùng được khi map khác đang dựng. Không dùng sai số tổng hợp để quảng cáo độ chính xác phòng thật.
- 44 kiểm tra Unity logic đạt: pose/yaw/independent views, hướng JPEG, đo sáng, gợi ý ánh sáng/motion ARCore, trái/phải dựa góc ảnh đã lưu, lưu checkpoint/mở lại/xuất, không đưa ảnh orphan vào ZIP, map thiếu tuyến không được dẫn đường.
- 398 kiểm tra layout đạt; 34 ảnh render Editor ở 412×915 và 412×732. Đã xem trực tiếp màn thiếu sáng, gợi ý trái, thư viện có tên dài và Review có tên lượt. Không tràn chữ/chồng header–card–khung quét trong các fixture này. Nền tối là fixture, không phải camera Android.
- JavaScript Studio: cú pháp và DOM fixture đạt cho thư viện trống, phản hồi map cũ không ghi đè map đang chọn và hủy đổi tên. Chưa coi đây là kiểm tra hình ảnh trình duyệt thực.
- Unity biên dịch không lỗi/warning. APK 0.2.0/code3, package `com.teep.jarvismaps`, min Android29, ARM64. Chữ ký debug phải khớp bản cũ trước khi đưa cho người dùng; thông số file cuối trong RELEASE-0.2.md.

## Cách tái hiện
Trong môi trường PC đã có: `.venv\Scripts\python.exe -m pytest test_visualmaps.py test_workbench.py -q`, sau khi đã có synthetic-result.json thì chạy `http_review.py`. `synthetic_review.py` tạo dữ liệu riêng dưới tests-output; không dùng ảnh kho thật.

Unity: `ProjectBootstrap.VisualMapReview.Run()` và `VisualMapUiReview.Run()`, rồi build bằng `VisualMapBuild.Build()`. Không bật Play scene Warehouse hay backend ở E. Giữ APK cũ để rollback; không uninstall khi cần giữ dữ liệu.

## Chưa xác nhận / còn thiếu
- Camera màu, depth/confidence đồng bộ, hướng depth/intrinsics, `File.Replace` và MediaStore export trên Samsung thực.
- Hai phòng vật lý, dựng và xem sơ đồ thật, mạng chập chờn, mở lại app sau khi hệ điều hành đóng.
- Toàn bộ pipeline kiểm tra khoảng trống toàn thân/vật cản động và quy trình duyệt/xuất bản tuyến. Chưa có điều kiện bật navigation-ready từ kết quả hiện tại.
- Độ lệch tại A/giữa tuyến/B qua ít nhất ba lượt đi thật. Không có điện thoại ảo hoặc render Editor nào thay được phép đo này.

Samsung chưa xuất hiện qua ADB và WPD trong lượt kiểm tra cuối; chưa tuyên bố đã chép/cài APK hay test camera thành công. Không cài driver hoặc thay cấu hình máy để che tình trạng này.
