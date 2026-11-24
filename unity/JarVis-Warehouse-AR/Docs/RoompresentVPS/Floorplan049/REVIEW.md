# JarVis 0.4.9 — sơ đồ phòng và giao diện gọn

## Phạm vi theo yêu cầu

- Màn Bản đồ dùng ảnh vuông 1024 × 1024, có bàn A, hai kệ sách B/C và tuyến A–B, A–C, B–C.
- Đối chiếu scan gốc `Scaniverse 2026-11-19 153704.glb` với video mới `2aOboREaREIVeTNngJ8bWwmq8Hzoh1UPcb9wf9cm.mp4` (5,77 giây). Bỏ ghế, laptop, quần áo và đồ nhỏ.
- Hình đồ nội thất được giản lược từ bề mặt scan và video; khung sàn là minh họa bố trí, không phải bản đo tường hoàn chỉnh.
- Không đổi vị trí A/B/C. Các góc tuyến xuất từ NavMesh của scene đã lưu, không tự nối đường xuyên nội thất.
- Bỏ AR Guidance và vùng lỗi Map unavailable khỏi màn Bản đồ; màn này đọc sơ đồ đóng gói, không chờ tải sơ đồ OMS.
- Thu gọn danh sách tồn kho: thẻ 166 → 142 đơn vị UI, khoảng giữa thẻ 12 → 10; bỏ khoảng trống trước danh sách.
- Popup chi tiết đo chiều cao theo nội dung; ảnh đúng mã, X đỏ bằng icon có sẵn, vùng bấm 44 × 44.
- Working vàng nhạt; Assigned xanh xám nhạt; Completed xanh lá nhạt. Có bảng màu sáng/tối riêng.
- Prompt thông tin sản phẩm yêu cầu trả lời trực tiếp, không thêm câu kết xác nhận/chú thích chung. Không xóa câu trả lời lịch sử và không huấn luyện mô hình.

## Kế hoạch UI / bằng chứng

- Kỹ năng: unity-mcp-ui-layout, chế độ sửa UGUI; FFmpeg để đọc video.
- Chủ sở hữu: ImportedFigmaNavigation, Canvas/ImportedFigmaViewport 412 × 915 portrait. Các màn hiện có và font/logo được dùng lại.
- Intake: ảnh Samsung 0.4.8 trong `../scan-progress-map-20261123/samsung-048-start.png`, hai ảnh tồn kho/AI người dùng cung cấp; nguồn C# của từng màn. Không có đối tượng đang chọn trong Editor; dùng batch scene inspection/capture thay MCP.
- Cấu trúc: WarehouseMapScreen → Prototype shell → scroll content → RoompresentMapPanel → ảnh vuông + overlay vị trí + chú thích. Không thêm canvas/camera AR hoặc đổi scene khi mở map.
- Tồn kho giữ scroll owner hiện có, ảnh sản phẩm gốc và cùng hành vi tìm kiếm/chi tiết. Popup giữ modal owner hiện có, scroll nội dung nếu dài.
- Lưu ảnh screenshot thực từ Unity, hai tỷ lệ 1080 × 2400 và 1080 × 1920 cho map; tồn kho/chi tiết và tab task bằng tiếng Việt/Anh, sáng/tối.
- `source-before/` chứa bản sao trước sửa của các file liên quan. Không rebuild/import toàn bộ Figma.

## Tọa độ và tracking

- `unity-scan.bin`: mesh scan được Unity xuất trong đúng hệ tọa độ mapRoot; không dựa trên giả định chuyển hệ tọa độ GLB.
- `navigation-geometry.json`: điểm A/B/C, các góc tuyến NavMesh đầy đủ và điểm sàn.
- `scan-height-layers.png` / `rotated-scan-diagnostic.png`: lớp sàn, mặt bàn/kệ, tường nhìn từ trên xuống phục vụ đối chiếu.
- `semantic-tracing.json`: vật thể giữ/bỏ và nguồn scan/video.
- `RoompresentMapProjection.json`: phép quay trực chuẩn và một hệ số tỷ lệ chung, không kéo méo trục. Điểm A/B/C và hash hiệu chuẩn bằng bản trước.
- Mặc định vẫn ghi A là vị trí mặc định. Vị trí trực tiếp chỉ đọc khi AR đang tin cậy; mất tracking chỉ giữ vị trí cuối đã biết và ghi rõ.
- Scene AR, SmallRoomAR, xác thực QR/barcode và dữ liệu hiệu chuẩn không chỉnh sửa trong đợt này. Cài APK sẽ khởi động lại phiên AR; bảo toàn file hiệu chuẩn không phải chứng nhận tracking vật lý liên tục qua lần cập nhật.

## Kiểm tra

- `RoomFloorplan049Review`: kiểm tra ảnh vuông, chiếu điểm, không hiện hai vùng bị bỏ, text vừa khung, screenshot các màn, prompt sản phẩm không quá giới hạn 1000 ký tự và chứa dữ liệu barcode đúng, hash scene/hiệu chuẩn/AR verification giữ nguyên.
- Phép chiếu kiểm tra độc lập bằng NumPy: trục vuông góc, độ dài 1; A/B/C và hash hiệu chuẩn giống trước sửa.
- Build: xem `D:/unity of teep/JarVis-Warehouse-AR/Logs/RoomFloorplan049-build.log`.
- Cài Samsung và đối chiếu file trước/sau chỉ được xem là đạt khi có `handoff.json` tương ứng.
- Quét QR thật và đi bộ A/B/C cần kiểm tra thực địa; ảnh Unity không chứng minh độ chính xác đó. Prompt đã sửa không tự chứng minh câu trả lời mới của nhà cung cấp AI.
