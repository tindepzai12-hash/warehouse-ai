# JarVis — bản demo Unity để duyệt giao diện

## Phân chia thiết kế

| Phần | Tham khảo | Áp dụng vào JarVis |
|---|---|---|
| Dẫn đường | Google Maps | Hướng đi và khoảng cách lớn, một tuyến đang chọn, chuyển B/C rõ ràng, ghim đỏ ở A/B/C |
| Thẻ nổi | Apple Maps | Thẻ gọn, kéo mắt xuống thao tác nhưng chừa camera ở giữa; vùng chạm ít nhất 48 đơn vị |
| JarVis nghe–nói | Grok Bot 1.0 | Cửa sổ trắng nhỏ, mắt riêng của JarVis, sóng âm, chat/mic/báo sự cố/đóng đỏ |
| Hội thoại và ảnh | ChatGPT | Bong bóng dễ đọc, soạn tin, voice, gửi lại và mở lớn bản đồ 2D |
| Nhiệm vụ | Todoist | Tên đậm, số lượng thật theo trạng thái, tiến độ và mục đã xong rõ ràng |

Các nguồn chỉ hướng dẫn hình thức. Không sao chép thương hiệu hay thay điều kiện nghiệp vụ.

## Luồng bắt buộc

Nhận nhiệm vụ #00001 CẤT HÀNG → barcode A1 → popup thành công + OK → barcode A2 → popup thành công + OK → QR vị trí A → AR → chọn B/C (tối ưu mặc định) → xác nhận giao → QR đúng điểm → popup QR + OK → barcode đúng sách → khóa mục đã xong → điểm còn lại → popup hoàn thành → Về A.

Cho phép quét A2 trước A1 như app hiện tại; mỗi quyển chỉ nhận một lần. Không bỏ QR A. Không dùng barcode sách để xác định tư thế camera. Xác nhận B/C vẫn dùng lớp RoompresentDeliveryVerification hiện tại với tracking và khoảng cách <=1.2 m.

## Phạm vi bản demo

- Unity 6000.4.7f1; UGUI + TextMeshPro như app hiện tại. Scene/prefab riêng, không dựng lại Figma app.
- Màn vào app, đăng nhập, trang chủ, nhiệm vụ/chi tiết, barcode/QR, AR, bản đồ, chat/voice, sự cố, kho/sản phẩm/tem, nhập/xuất, hồ sơ/hoạt động, công cụ, cài đặt và các popup.
- Dữ liệu mẫu chỉ trong bộ nhớ của demo. Mã A1/A2, QR và map giữ đúng dữ liệu đã duyệt.
- Thành phần hiển thị và sự kiện có thể chèn vào app; bộ nối lệnh giữ nguyên các owner hiện có. Bản demo không gửi giao dịch, không đăng nhập giả vào máy chủ, không gọi model trả phí.
- Các màn scan nhận kết quả từ owner camera qua hàm SubmitScan. Bảng điều khiển Editor riêng cấp dữ liệu thử; không đặt nút giả “quét thành công” trong giao diện người dùng.
- Lớp phủ không sở hữu ARSession, XROrigin, camera, anchor hay dữ liệu map. Đóng/mở popup chỉ thay CanvasGroup và các panel.

## Cấu trúc và chuyển động

Canvas → safe area → page (header/content/footer) + overlay (scrim/card) + toast. Các vùng lặp dùng container; bố cục thủ công có chủ đích cho màn chỉ dẫn cố định và minh họa bản đồ.

Font Be Vietnam Pro hiện có. Nền trắng/xanh nhạt, chữ đậm màu mực; AR dùng thẻ tối tương phản, điều khiển phụ nhỏ. Mỗi khung scan là một component: QR vuông, barcode ngang; scan line chỉ biểu thị đang tìm, không giả phần trăm nhận diện.

Nhấn 120 ms; popup 280 ms; chuyển nội dung 200 ms. Dùng unscaled time; tôn trọng giảm chuyển động. Không timeScale=0, không reload scene, không tắt tracking để hiển thị cửa sổ.

## Kiểm tra và giới hạn

Chụp mới trong Unity ở 1080×2400 và 1080×1920; kiểm tra chữ tràn, nút, thứ tự mã đúng/sai, hoàn tất B vẫn còn C, popup không làm đổi scene/camera/anchor. Kiểm tra ảnh động quét ở nhiều thời điểm. Giữ byte các scene sản xuất và JSON định vị.

Ảnh phòng trong demo là ảnh tham chiếu đã có, có nhãn DEMO; không phải camera Samsung mới. Kết quả Editor không chứng minh độ chính xác định vị hay chất lượng micro/camera thiết bị. Chưa xuất/cài APK trong lượt duyệt này.
