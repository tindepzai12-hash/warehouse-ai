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

## Kiểm tra thiết bị ngày 20/11/2026

- Samsung SM-G988N đã kết nối ADB và được cấp quyền. Bản 0.3.4/code18 đã cài, mở được camera AR, tiếp tục được nhiệm vụ hai sách.
- Nút **Nét: Toàn phòng / Nét: Tự động** đã hiện trên màn AR; log nhận lệnh chuyển cả hai chiều. Đã thấy request camera chính chuyển AF OFF khi chọn chế độ toàn phòng. Chưa chứng minh mọi góc nhìn đều hết mờ.
- Map 153613 đã nạp thật trên Android. Trong lượt thử 0.3.4, 12 kết quả khớp yếu bị loại; chưa có vị trí đủ điều kiện. Không đồng nghĩa lỗi camera hoặc map chưa tải.
- Bản 0.3.3 trước đó từng nhận nhóm kết quả quay gần 180 độ với confidence 12–13; nhóm khác có confidence 21–23. Ngưỡng lọc 20 và RMSE tối đa 3 là điều chỉnh theo lượt thử này, chưa phải bảo đảm độ chính xác.
- Ảnh người dùng gửi từ iPhone Mapper hiển thị **ID 153613 – Roompresent, localized 11/38** và đám điểm trên khu kệ. Chứng minh map có kết quả nhận vị trí trong phiên đó; ảnh tĩnh chưa xác nhận độ bám khi di chuyển, chưa biết đang dùng xử lý trên máy hay cloud.
- Chưa lưu phép căn map Immersal với GLB trên Samsung. Ghim B/C và đường được ẩn cho đến khi có vị trí tin cậy và phép căn đã đối chiếu. Không áp dụng phép căn suy đoán để làm xuất hiện ghim.

## Phần đang hoàn tất

- Bản 0.3.5/code19 đã xuất: giữ ảnh camera đầy đủ cho bộ nhận map, giữ nút lấy nét, sau 45 giây chưa nhận được phòng sẽ giải thích và có nút thử lại. Thử lại giữ phiên ARCore và tiến độ, không gán camera về A.
- File tiến độ thử trên Samsung xác nhận `scannedA1=true`, `scannedA2=true`, chưa giao B/C; đây là lý do mở thẳng AR ở lượt tiếp tục. Đã thêm **Thử từ đầu · quét lại A1 + A2** trong thẻ nhiệm vụ để người dùng chủ động làm lại bài thử. Nút này tạo lại bản ghi thử riêng; không ghi giao dịch kho thật.
- Cần kiểm tra thực tế độ rõ, thời gian nhận map, sự nhất quán khi xoay/di chuyển, sau đó mới gán và xác nhận B/C.
- AI nhận diện hai xe từ ảnh chưa được tích hợp thành chức năng định vị 3D trong app. Nhìn thấy xe trong ảnh không tự xác định được hệ tọa độ map.

## APK đã xuất

- File: `D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse-0.3.5.apk`
- Toàn bộ JarVis, phiên bản 0.3.5, versionCode 19; ARM64, Android 10 trở lên.
- Unity StrictMode build thành công, 0 lỗi, 76,02 giây (lần xuất cuối có nút quét lại).
- AAPT xác nhận đúng gói `com.teep.jarviswarehousear`, quyền camera/micro/internet và activity khởi chạy.
- Chữ ký APK v2 xác minh thành công.
- Dung lượng: 90.293.831 byte.
- SHA256: `fe5f5396ea4aff9cbf7d322b3d1483fdf2fda886a1cc0cb3d23c4f482bb580d7`.
- Đây là bản đã sửa mã định vị; chưa phải kết quả kiểm chứng độ chính xác trên Samsung. Các chức năng AI/voice/ảnh không được thử lại trong lượt sửa định vị này.
