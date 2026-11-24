# JarVis Maps 0.1.1 — chụp phòng, dựng COLMAP và chỉ đường bằng hloc

> Tài liệu lịch sử. Với bản 0.2.0 nhiều map, xem **HUONG-DAN-0.2.md**. Bản mới tự chụp không cần QR/căn sàn trước và chưa nghiệm thu chỉ đường tránh vật cản; không làm theo các tên nút/luồng 0.1.1 bên dưới để đánh giá 0.2.0.

## Sửa lệch ngay sau A — bản 0.1.1
- Cài `JarVis-Maps-0.1.1.apk` đè lên JarVis Maps; **không gỡ app / xóa dữ liệu**. Gói và chữ ký giữ nguyên để giữ cấu hình và ZIP đã tạo.
- Đã sửa phép căn: khi giữ bản đồ thẳng theo trọng lực, tính lại phần dịch chuyển để vị trí camera vẫn khớp; không giữ phần dịch chuyển của góc nghiêng cũ.
- Hai lượt khớp phải dùng hai frame mới và hai góc nhìn khác nhau (đổi góc ít nhất 8° hoặc dịch máy ít nhất 15 cm). Giữ nguyên một góc chỉ ở `1/2`, không dùng lặp cùng ảnh để tự xác nhận.
- Nếu lệch trọng lực quá 3°, chưa bật đường. Đây là ngưỡng kỹ thuật chặn kết quả kém, **không phải cam kết sai số thực địa dưới 3° hay một số cm cụ thể**.
- Không cần xóa / thu lại bản đồ chỉ để thử bản sửa này. Nếu vẫn lệch, giữ ZIP và quay từ lúc quét A tới khi đường bật; cần kiểm tra cả đầu vào camera và bản đồ thật.

## Quan trọng trước khi cài
- Đây là app mới tên **JarVis Maps**, package `com.teep.jarvismaps`. Không gỡ JarVis Warehouse hay JarVis Routes; dữ liệu hai app cũ không bị thay đổi.
- **Chụp bằng chính APK này. Không cần quay video riêng.** App lưu ảnh JPEG cùng thông số camera, vị trí, hướng camera và mặt sàn. Video thường / ảnh chụp bên ngoài chưa có trình nhập trong bản này.
- COLMAP và hloc chạy trên PC, không nằm trực tiếp trong APK. Điện thoại và PC phải cùng Wi-Fi tin cậy; PC phải bật và server phải đang chạy khi gửi ảnh / đối chiếu vị trí. Chưa phải bản chỉ đường hoàn toàn offline trên điện thoại.
- Bản thử đầu tiên dùng **cùng điện thoại Samsung để thu ảnh và chỉ đường**, giữ dọc. Không đổi máy, đổi camera hoặc zoom giữa lượt. Căn khác biệt giữa ảnh camera thô và camera AR được học khi dựng bản đồ, không đoán một góc xoay 90° cố định.
- Chưa có ảnh phòng của bạn, nên chưa có bản đồ thật để nghiệm thu độ lệch. Kiểm thử ảnh tổng hợp và build thành công không chứng minh AR đi thật đã đúng.

## 1. Mở phần xử lý trên PC
1. Trong thư mục gói này, chạy **START-PC.cmd**. Môi trường Python trên máy hiện tại đã được cài riêng trong `.venv`; không cần cài CUDA, driver hay Torch.
2. Mở **http://127.0.0.1:8765** trong trình duyệt trên PC. Trang này hiển thị QR kết nối riêng, không phải QR A/B.
3. Cho Samsung và PC vào cùng Wi-Fi riêng. Nếu Windows hỏi quyền mạng cho Python, chỉ cho phép mạng **Private** mà bạn tin cậy; không cần mở cổng ra Internet hoặc tắt tường lửa.
4. Mở app → **Kết nối PC** → quét QR trên trang PC. QR có token riêng: không gửi công khai hoặc đưa lên GitHub. Không đưa `data/connection.json` vào source control.
5. Địa chỉ Wi-Fi có thể đổi khi đổi mạng / khởi động lại router. Nếu app báo không liên lạc được PC, mở lại trang và quét kết nối mới, không phải cài lại APK.

Server dùng HTTP nội bộ, **ảnh và token không được TLS mã hóa trên LAN**. Chỉ dùng mạng tin cậy, không chuyển tiếp cổng 8765. Không có ảnh gửi tới Render, Immersal hoặc dịch vụ cloud. Trang QR chỉ đọc được trực tiếp trên PC qua loopback.

## 2. Chuẩn bị hai mã A và B
- Mở `QR-PRINT.html` trên PC để in, hoặc dùng hai file `QR-A.png`, `QR-B.png` trên thiết bị khác.
- A chứa `JWM:MAIN:A`; B chứa `JWM:MAIN:B`. Có thể dùng lại QR cũ có đúng hai nội dung này.
- Đặt hai mã cố định, đủ lớn để đọc, không phản sáng; không đổi vị trí sau khi ghi.
- Khác bản Routes cũ: mã ở đây chỉ xác nhận **A/B**, không dùng ảnh thẻ 18 cm để suy ra vị trí 3D. Vị trí được đối chiếu với bản đồ ảnh COLMAP.
- **B là chỗ người dùng đứng khi quét kết thúc**, chiếu xuống mặt sàn đã căn; không phải tọa độ chính xác của miếng QR trên tường.

## 3. Thu ảnh phòng — KHÔNG chỉ xoay máy tại một chỗ
1. Mở **Ghi bản đồ mới**, cấp quyền Camera.
2. Hướng camera xuống **sàn thật**, lia chậm tới khi ARCore nhận mặt phẳng, rồi bấm **Căn mặt sàn**. Đừng căn vào mặt bàn. Không sử dụng chiều cao 1,30 m giả định như bản cũ.
3. Đi chậm một vòng quanh phần phòng sẽ dùng. App tự chụp tối đa khoảng 1 ảnh/giây khi bạn đã dịch chuyển hoặc đổi góc đủ nhiều. Số “ảnh đã lưu” là số file thật; không phải phần trăm diện tích phòng.
4. Nên thu **80–150 ảnh**. Tối thiểu 20 ảnh để chuyển bước; giới hạn kỹ thuật hiện tại 600 ảnh / gói 220 MB, không phải quota dịch vụ 100 ảnh. Giới hạn này để tránh hết RAM / dung lượng, không hứa chụp vô hạn.
5. Các ảnh kế tiếp cần nhìn thấy nhiều đồ vật chung: đi từng bước khoảng 20–40 cm, quay máy chậm. Chụp cùng góc phòng từ nhiều chỗ trái/phải; có chuyển động ngang để dựng chiều sâu và căn tỷ lệ.
6. Ưu tiên bàn, kệ, góc tường có đồ vật / họa tiết. Tránh toàn tường trắng, sàn bóng, gương, cửa sổ cháy sáng, người/vật đang di chuyển. Giữ ánh sáng gần giống lúc sẽ chỉ đường.
7. Không rời app / khóa màn hình khi thu ảnh và ghi tuyến. Nếu AR mất theo dõi kéo dài hoặc tọa độ nhảy, app ngắt lượt đó; không tự nối hai hệ tọa độ không biết.

## 4. Ghi đúng lối từ A tới B
1. **Vẫn giữ app đang mở**, quay lại vị trí bắt đầu A sau lượt thu ảnh quanh phòng.
2. Bấm **Ghi tuyến A → B**, quét A, chờ dòng **Đã ghi A**.
3. Đi chậm theo đúng lối muốn người sau đi; đừng đi vòng thu ảnh thêm ở bước này. Các ảnh đánh dấu “trên tuyến” sẽ tạo đường đi.
4. Tới chỗ muốn làm đích, đứng đó quét B; chờ **Đã nhận B**. Tuyến phải dài ít nhất 1 m và có ít nhất 4 ảnh trên tuyến.
5. Chọn **Gửi ảnh sang PC và dựng**. Phần trăm lúc này là tiến độ gửi byte thật. Sau đó PC báo từng công đoạn: trích đặc trưng → ghép ảnh → dựng COLMAP → căn tỷ lệ / kiểm tra tuyến → sẵn sàng; không bịa một % đã quét phòng.
6. Chờ PC xử lý, có thể mất vài phút tùy số ảnh và cấu trúc phòng. Bản đồ cũ chỉ được thay khi bản mới qua kiểm tra chất lượng. Nếu không ghép được ảnh A/B, quá ít ảnh hợp lệ hoặc sai số căn lớn, app báo nguyên nhân và chưa công bố bản đồ.

### Gửi thất bại hoặc cần gửi file cho mình kiểm tra
- Chọn **Xuất ZIP vào Download**. Gói nằm ở **Tệp của bạn → Bộ nhớ trong → Download → JarVisMaps**.
- Gửi **nguyên file ZIP**, không chỉ gửi video màn hình hoặc vài ảnh rời, để giữ manifest thông số camera.
- Ảnh và ZIP nội bộ vẫn còn sau lỗi mạng; từ trang đầu có **Gửi lại gói ảnh gần nhất**. Không xóa dữ liệu app để thử lại.

## 5. Chỉ đường bằng bản đồ vừa dựng
1. Giữ PC/server chạy, mở **Chỉ đường A → B** rồi quét QR A.
2. Sau khi nhận A, **hướng camera ra phần phòng đã thu ảnh**, không cứ giữ sát vào QR. Đứng yên một chút để PC đối chiếu.
3. App yêu cầu **hai lượt định vị ảnh nhất quán từ hai góc nhìn khác nhau**; hiện `1/2`, `2/2` và số điểm khớp. Ở `1/2`, nhìn nhẹ sang một góc khác đã thu ảnh, rồi giữ máy ổn định. Đây không phải % quét diện tích phòng. Kết quả ít điểm / sai hình học sẽ không bật mũi tên.
4. Khi khóa được vị trí và anchor AR, đi theo **một mũi tên gần phía trước**; điểm B màu xanh dương có chữ B. Nếu mũi tên nằm ngoài camera, thẻ dưới cho biết nên nhìn sang bên nào / hạ máy.
5. Khi đi, app đối chiếu lại lúc bạn giữ máy ổn định. Nếu lâu chưa định vị lại, mất tracking hoặc kết quả mới bất đồng, app **ẩn đường và yêu cầu dừng để căn lại**, không âm thầm nhảy đường sang nơi khác.
6. Tới gần B, đối chiếu vị trí thật. App chưa phát hiện vật cản, chưa tránh người hay hàng hóa và không dùng cho quyết định an toàn.

## Khi lỗi, gửi gì để sửa đúng?
- ZIP ảnh gốc; ảnh màn hình lỗi; video từ lúc quét A tới khi lệch; cho biết lệch ngay sau quét hay sau khi đi.
- Đánh dấu hai điểm trên sàn cách nhau 1–2 m để so vị trí ảo và thực. Đừng chỉ nhận xét mũi tên đẹp / xấu: cần đo sai lệch tại A, giữa tuyến và B qua ít nhất ba lượt.
- Chưa cần đổi iPhone hoặc mua LiDAR. Bản này cần kiểm tra đúng dữ liệu ảnh, tỷ lệ, camera và tái định vị trước.

## Kỹ thuật và cài lại PC
- Python đã kiểm thử: 3.12.14 Windows; PyCOLMAP 4.2.1. Bộ thư viện khóa trong `requirements-lock.txt`.
- hloc commit `c13273bd0ecc2917a35910fd843712a1c6243193`. Dùng `hloc.localize_sfm.QueryLocalizer` thật, COLMAP SIFT và FLANN 2D–3D; không chạy SuperPoint/LightGlue, không tải model học sâu ở bản đầu.
- Bản đồ là sparse landmarks để định vị, không cần dựng mesh 3D đẹp / mô hình vật lý cả phòng. Tỷ lệ mét và sàn lấy từ phiên AR khi thu ảnh; chúng vẫn cần kiểm chứng thực địa.
- Tối đa bốn luồng được cấu hình cho các bước COLMAP chính; không benchmark hay thay driver. Theo dõi nhiệt độ PC khi xử lý bộ ảnh lớn.
- Sang máy khác: cài Python 3.12 và Git nếu chưa có; chạy `SETUP-PC.ps1 -Python <đường-dẫn-python.exe>`, rồi `START-PC.cmd`. Script tạo môi trường riêng, không sửa Python hệ thống. Bản mã nguồn trong project ở `Tools/VisualMaps`; môi trường chạy trên máy hiện tại nằm cùng gói APK này.
- Dừng server bằng Ctrl+C nếu chạy cửa sổ, hoặc chạy `STOP-PC.ps1` trên PC. Không có tác vụ tự khởi động Windows.
- Nguồn: https://github.com/colmap/colmap (BSD, chú ý giấy phép phụ thuộc); https://github.com/cvg/Hierarchical-Localization (Apache-2.0). Giữ giấy phép nguồn khi phân phối; source hloc được tải nguyên bản trong `vendor/hloc`.
