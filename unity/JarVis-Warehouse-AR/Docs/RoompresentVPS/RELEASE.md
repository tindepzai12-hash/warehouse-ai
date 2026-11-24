# Roompresent: sửa định vị và kiểm tra thực tế

## Nguyên nhân đã xác định

- Video trước đang mở bài test phòng nhỏ (chậu/túi) trong phòng thuyết trình.
- Luồng Roompresent cũ cũng dùng quy ước đứng A, hướng B để đặt bản đồ. Quét sách ở chỗ khác không cung cấp vị trí hoặc hướng của điện thoại.
- Scaniverse GLB và map Immersal 153613 được quét riêng, có hai hệ tọa độ. Chưa có phép căn đã được xác nhận giữa chúng.

## Luồng mới

1. Nhận nhiệm vụ; quét A1 `4714551570610` và A2 `4714551570603` ở bất kỳ đâu trong Roompresent.
2. Mở scene `Roompresent_VPS`. Immersal dùng file map 153613 nhúng trong APK và camera/intrinsics thật của Samsung để tìm vị trí. Barcode không đặt vị trí điện thoại.
3. Chờ ba kết quả map nhất quán. Chỉ hiện ghim và đường khi vị trí đáng tin và phép căn hai bản scan đã được lưu.
4. Đưa A1 đến B (xe sách trái), A2 đến C (xe sách phải). Điểm đứng để tiếp cận nằm trên sàn; ghim đích nằm tại xe sách.
5. Khi tracking mất hoặc vị trí nhảy, ẩn đường, giữ nhiệm vụ, tiếp tục tìm lại map. App không tự đưa điện thoại về A.
6. Quét QR đích tương ứng khi đã đến gần để xác nhận giao hàng.

## Thiết lập một lần trên Samsung

Trong **Map → Roompresent → Thiết lập map**:

- Nhìn quanh phần phòng có chi tiết cố định cho đến khi báo đã định vị.
- Ghi đúng điểm tương ứng trên bàn A, xe trái B, xe phải C bằng dấu +. Cần mặt phẳng camera nhận ổn định.
- Di chuyển ngang và xoay máy để kiểm tra ba ghim có khớp vật thật không; chỉ bấm **Ghim đã khớp** khi đã đối chiếu.
- Phép căn lưu riêng theo dấu nhận dạng chính xác của map/GLB; không lưu vị trí camera phiên cũ. Không tự nhập phép căn QR cũ hoặc bản căn suy đoán.
- Sau đó các lượt giao hàng không cần quét mốc A hoặc đứng tại A. Cần căn lại nếu đổi map, scan hoặc dời xe sách.

Đây là phép căn do người lắp đặt xác nhận, không phải chứng nhận độ chính xác tuyệt đối.

## Kiểm tra bắt buộc trong phòng

| Kiểm tra | Kết quả cần thấy | Trạng thái |
|---|---|---|
| Quét sách xa A | Mở đúng Roompresent; không giả định vị trí A | Chờ Samsung |
| Định vị từ hai chỗ khác nhau | Cùng hai ghim tại hai xe sách | Chờ Samsung |
| Đi ngang và quay 180° rồi quay lại | Ghim không đi theo camera | Chờ Samsung |
| Che camera rồi mở lại | Giữ nhiệm vụ, ẩn đường khi mất vị trí, tự tìm lại | Chờ Samsung |
| Về JarVis rồi mở lại AR | Dùng phép căn đã lưu, định vị camera mới | Chờ Samsung |
| Giao sai QR hoặc còn ở xa | Không đánh dấu đã giao | Chờ Samsung |

## Đã xác minh ở máy tính

- Scene có đúng một AR Session, camera, XR Origin và XRSpace; nội dung thuộc XRSpace, không thuộc camera.
- Map 153613 được nhúng; chỉ bật DeviceLocalization. Cảnh mới không dùng token/cloud để tìm vị trí.
- Kiểm tra quy tắc nhận vị trí, loại kết quả nhảy/sai map/không hợp lệ, hết hạn và tìm lại đã qua.
- Kiểm tra phép căn cứng, từ chối map/GLB khác, chưa được xác nhận và ba điểm không khớp đã qua.
- Đã kiểm tra file thư viện Android ARM64 là ELF thật trong SDK.
- Chưa chạy được phép nạp map bằng thư viện Windows: plugin đang tắt cho Editor; thử độc lập bị Application Control của Windows chặn. Không thay đổi bảo vệ Windows. Điều này chưa xác định kết quả định vị trên Android.

## Kết nối thiết bị

Lần kiểm tra gần nhất ADB không liệt kê Samsung, Windows cũng chưa thấy thiết bị Samsung/Android hiện diện. Đã yêu cầu rút/cắm lại USB. Chưa cài APK mới hoặc xác nhận độ chính xác ngoài phòng.

## APK đã xuất

- File: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse-0.3.2.apk`
- Toàn bộ JarVis, phiên bản 0.3.2, versionCode 16; ARM64, Android 10 trở lên.
- Unity StrictMode build thành công, 0 lỗi, 103,85 giây.
- AAPT xác nhận đúng gói `com.teep.jarviswarehousear`, quyền camera/micro/internet và activity khởi chạy.
- Chữ ký APK v2 xác minh thành công.
- Dung lượng: 90.208.005 byte.
- SHA256: `36104168233E599218E731A4AB4F362FD94B53E06D20088FA44F354E99AF1DC1`.
- Đây là bản đã sửa mã định vị; chưa phải kết quả kiểm chứng độ chính xác trên Samsung. Các chức năng AI/voice/ảnh không được thử lại trong lượt sửa định vị này.

## Latest follow-up — 2026-11-20 / 0.3.6

The older 0.3.2 device/ADB status above is historical. Samsung R3CN20R5Q2F is authorized, still at Roompresent, and full JarVis 0.3.6/code20 is installed and launched. Unity StrictMode build succeeded with zero errors, 70.887764 seconds; APK v2 signature verified. APK: Builds/Android/JarVis-Warehouse-0.3.6.apk, 90,310,471 bytes, SHA256 F974019B40379DCF20715433301B4155FACB23CE3BB936E9A493B382DB17F1BF.

Actual 0.3.5 logs verified both book barcodes and automatic AR transition; native map loaded/camera tracking but weak matches did not acquire the VPS gate. See DEVICE-LOCALIZATION-035.md. It is not established whether all rejected poses were incorrect or the thresholds are optimal.

Owner confirmed fixed QR A remains on the presentation table. 0.3.6 adds RoompresentQrLocalization: decode identity, measure three finder centres on one AR horizontal plane, require stable metric geometry, create a native AR anchor. Readable QR text alone does not localize. Separate operator-checked B/C registration is saved relative to that fixed QR, not to a previous camera pose; exact map/scan fingerprints and QR identity required. Book barcodes never set spatial pose. Existing VPS registration and small-room paths retained. No OpenAI/Astra calls or E-drive access.

See FIXED-A-TEST.md for installer steps, normal flow and limitations. Math/finder-order tests pass for four headings and twelve real decoder rotations; real QR/plane/anchor capture, shelf fit, walking/turning/recovery still require current Samsung validation. AI voice/image/API features were not retested in this localization follow-up.

Latest 2026-11-20: full JarVis 0.3.9/code23 supersedes 0.3.8. See PASSIVE-039.md for fresh-barcode gate, passive QR A and zero-overlay AR, APK hash and device limits.

Latest full JarVis release: 0.4.0/code24. See DELIVERY-040.md and DELIVERY-040-UI.md for the approved compact controls, explicit QR A step, OK popups and ordered destination/product delivery proof. Installed final APK hash is recorded there; physical testing requires owner login.


Latest release: full JarVis 0.4.1/code25 — see DELIVERY-041.md for exact APK, QR scan UI, metric destination reference transfer and test limitations.
