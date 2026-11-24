# Roompresent: căn tại QR A cố định — 20/11/2026

## Vì sao Samsung đang chờ

- Log JarVis 0.3.5 cho thấy camera tracking và map 153613 đã nạp.
- Phiên PID 21715 xác minh A1 lúc 13:53:15, A2 lúc 13:53:21 theo giờ thiết bị; cả hai đã mở AR. Barcode không phải nguyên nhân chờ nhận map.
- Camera/intrinsics báo 1920×1080. Các kết quả native gần nhất confidence 12–14, RMSE 3,665–7,196 bị chính sách chất lượng của JarVis loại. Chưa có phép đo độc lập để kết luận các pose này đúng hay sai, hoặc ngưỡng đã tối ưu.
- Không có roompresent-vps-registration-v2.json trên Samsung. Ngay cả khi VPS nhận được phòng, B/C còn cần căn thật.
- Ảnh iPhone Mapper localized 11/38 xác nhận có trả vị trí trên iPhone, không xác nhận phép căn GLB hay độ chính xác Samsung.

## Cách mới đang bổ sung trong 0.3.6

Người dùng xác nhận tờ QR A vẫn dán cố định trên bàn. App đọc đúng mã A, đo ba tâm hoa văn QR trên cùng mặt phẳng ngang ARCore và tạo native AR anchor. Không lấy vị trí camera ban đầu làm A; không suy ra tọa độ từ chuỗi barcode sách; không cần nhập kích thước giấy vì kích thước metric lấy từ mặt phẳng AR đã đo.

Các mã A cho phép lần thiết lập đầu: JWM:MAIN:E (E cũ đổi thành A), JWM:MAIN:A, JWM:ROOMPRESENT:153613:A. Sau khi lưu, phải khớp chính mã đã căn. Không chấp nhận barcode sản phẩm làm mốc.

Chỉ chấp nhận khi ba tâm finder thuộc cùng một mặt phẳng ngang đang tracking, khoảng cách và hình học hợp lệ, máy giữ yên, ít nhất bốn quan sát nhất quán. Giải mã QR đơn thuần không mở đường đi.

## Thiết lập một lần tại phòng

1. Bản đồ → Roompresent → Thiết lập map; hoặc Thiết lập B/C trong AR.
2. Hướng camera vào toàn QR A và một vùng mặt bàn xung quanh. Giữ yên để nhận mặt phẳng. App đo A tự động.
3. Đưa dấu + tới đúng điểm trên mặt trên xe trái B, ghi khi nút bật.
4. Ghi tương tự xe phải C.
5. Đi ngang, quay máy và đối chiếu ba ghim với vật thật. Chỉ bấm Ghim đã khớp khi đúng. Nếu không đúng, làm lại; không lưu để ép hiển thị.

Phép căn được lưu riêng ở roompresent-fixed-a-v1.json, kèm fingerprint map/GLB, ba điểm và xác nhận người vận hành. Không chuyển dữ liệu v1/v2 cũ sang mà thiếu kiểm tra.

## Lượt giao sách sau đó

Quét A1 4714551570610 và A2 4714551570603 bằng camera → mở AR → hướng máy vào QR A cố định → đi theo đường tối ưu tới B/C → đúng QR điểm giao và đủ gần mới hoàn thành.

Mốc A cùng hai xe phải giữ nguyên sau khi căn. QR chỉ cần hiện lúc khởi tạo hoặc khi cần tìm lại mốc; native AR anchor và camera tracking duy trì vị trí khi đi/đổi hướng. Khi mất tracking, app giữ nhiệm vụ và ẩn chỉ dẫn không đáng tin, tự nhận lại; nếu anchor không phục hồi thì cần nhìn lại QR A. Không hứa không bao giờ mất tracking hoặc độ chính xác tuyệt đối.

## Kiểm tra đã chạy

- Toán vị trí/hướng QR ở bốn hướng, loại điểm phản chiếu, mặt không ngang và barcode sản phẩm.
- Giải mã ba mã A ở bốn góc xoay (12 trường hợp), kiểm tra thứ tự điểm finder thật của ZXing.
- Phép căn lưu lại và từ chối dữ liệu chưa xác nhận.
- Các kiểm tra gate VPS và căn ba điểm cũ vẫn qua.
- Chưa xác nhận đo mặt bàn/anchor và B/C đúng ngoài phòng ở thời điểm viết. Không dùng kết quả build để thay cho thử trên Samsung.

Không gọi OpenAI/Astra hay gửi ảnh phòng lên dịch vụ để suy đoán vị trí. Cách căn này hoạt động tại thiết bị; trợ lý AI hỗ trợ rà lỗi và lập trình.

## APK đã xuất

- Toàn bộ JarVis 0.3.6 / versionCode 20, entry JarVisFigmaApp.
- Unity StrictMode: Succeeded, 0 lỗi, 70,887764 giây; log unity-036-build-r2.log. Lần đóng gói đầu thất bại, chạy kiểm tra đóng gói trực tiếp thành công rồi Unity build lại thành công. Không giao bản lỗi đầu.
- File: D:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse-0.3.6.apk
- 90.310.471 byte; SHA256 F974019B40379DCF20715433301B4155FACB23CE3BB936E9A493B382DB17F1BF.
- Đang cài/kiểm tra Samsung; chưa xác nhận độ chính xác ngoài phòng.
