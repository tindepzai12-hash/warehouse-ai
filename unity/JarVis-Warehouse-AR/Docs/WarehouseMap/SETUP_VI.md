# Thiết lập bản đồ kho JarVis

## Hiện trạng và lựa chọn kỹ thuật

- App Unity đọc bản đồ đã công bố từ OMS và vẽ sơ đồ 2D, đồ thị lối đi, tuyến đến **node tiếp cận ô kệ**. Nhiệm vụ và tồn kho có nút mở bản đồ tại đúng mã vị trí.
- Web quản lý phụ có công cụ tại `http://127.0.0.1:8000/warehouse/map.html` khi backend mới đã được chạy. Chỉ tài khoản quản trị công bố được bản đồ. Bản nháp trong trình duyệt chưa tác động app cho đến khi bấm **Công bố bản đồ**.
- Mã mốc có dạng `JWM:MAIN:<ID>`; quét QR chỉ nhận diện điểm bắt đầu đã đăng ký. Nó **không đo hướng camera, không cung cấp pose 6DoF** và không phải xác nhận đã lấy/cất hàng.
- Không dùng Geospatial/VPS như nguồn định vị mặc định cho kho: Google mô tả VPS dựa trên Street View; trong nhà chỉ có thể đạt chính xác cao khi có phủ VPS phù hợp, điều này chưa được kiểm chứng tại kho. Đây là quyết định thiết kế, không phải khẳng định VPS không thể hoạt động trong mọi nhà kho.
- AR Foundation/ARCore đã có trong project, nhưng **mũi tên AR theo tọa độ kho chưa được bật**. Cần ảnh mốc giàu đặc trưng, thư viện tracked-image, đo kích thước in thật, hiệu chuẩn transform mốc → kho và QA trên Samsung. Không thể suy pose từ một chuỗi QR.

Nguồn chính thức đã kiểm tra ngày 2026-10-11: [ARCore Geospatial](https://developers.google.com/ar/develop/geospatial), [VPS availability](https://developers.google.com/ar/develop/unity-arf/geospatial/check-vps-availability), [ARCore Augmented Images](https://developers.google.com/ar/develop/augmented-images), [Unity AR Foundation image tracking 6.6](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.6/manual/features/image-tracking.html), [thiết bị ARCore](https://developers.google.com/ar/devices/). Danh sách Google có Galaxy S20 Ultra 5G; biến thể máy thật vẫn phải xác nhận bằng thử nghiệm.

## Chủ kho cần chuẩn bị gì?

| Việc vật lý | Vì sao | Dụng cụ | File hoặc dữ liệu đầu ra | Nơi nhập và cách kiểm tra |
|---|---|---|---|---|
| Vẽ hoặc xuất sơ đồ nhìn từ trên xuống của **từng tầng** | Làm nền để đặt lối đi/kệ | Bản vẽ CAD, diagrams.net, Inkscape hoặc ảnh sơ đồ sẵn có; **không bắt buộc** mua ứng dụng scan 3D | PNG/JPG tối đa 3 MB, tối đa 4096 px mỗi chiều | Trên web: **Bản đồ kho → Sơ đồ tầng → Ảnh sơ đồ**; đối chiếu hình với kho thật |
| Đo ngang/sâu từng tầng và một đoạn chuẩn rõ ràng | Chuyển pixel thành mét, tránh tuyến sai tỉ lệ | Thước cuộn hoặc máy đo laser | Hai kích thước mét; hai điểm trên ảnh và khoảng cách đo | Chọn 2 điểm → nhập mét → Tính tỷ lệ; so thêm ít nhất hai đoạn khác ngoài thực địa |
| Ghi mã ô kệ đúng như OMS | Liên kết nhiệm vụ/tồn kho với bản đồ | Danh sách vị trí từ web quản lý | CSV theo `locations-template.csv` hoặc nhập bằng cách bấm lên ảnh | Tạo mã vị trí trên OMS trước; chọn mã ở **Đặt ô kệ**, gán node tiếp cận; mã sai sẽ bị từ chối khi công bố |
| Khảo sát **lối đi thực tế**, lối một chiều, điểm bị chặn | Đồ thị phải đi đúng lối, không xuyên kệ | Đi kiểm tra kho, sơ đồ, checklist an toàn | Nodes và edges trên web | Đặt node trong lối đi, nối hai node, đánh dấu chặn/một chiều; xem thử tuyến và đi bộ đối chiếu |
| Chọn điểm dán QR cố định, ghi ID, đo vị trí/kích thước | Điểm khởi đầu phải tra được trên bản đồ | Tem QR in ở kích thước xác định, thước, bút đánh dấu | ID `JWM:MAIN:<ID>`, tọa độ, node gần nhất, góc, bề ngang mét | **Đặt mốc QR** trên web; scan bằng màn Quét mã trong app và xác nhận đúng điểm. Bản hiện tại chưa tạo tờ QR in tự động |
| Khảo sát lại sau mỗi thay đổi layout | Bản đồ cũ không còn an toàn | Checklist đi bộ | Phiên bản map mới | Chỉnh web sẽ tự bỏ cờ **đã khảo sát**; chỉ bật lại sau khi đo và đi thử. App yêu cầu quét lại mốc khi version đổi |

Tọa độ dùng mét: gốc (0,0) là góc trên-trái ảnh, X sang phải, Z xuống dưới. Mỗi ô kệ có tọa độ hình ảnh và `accessNodeId` nằm trong lối đi. **Không** nhập vị trí sản phẩm/lot vào file map: sản phẩm, lô và số dư nằm trong OMS, map chỉ ánh xạ mã vị trí.

## Thao tác theo thứ tự

1. Sao lưu database kho theo quy trình vận hành của bạn. Backend có schema bản đồ **thêm bảng mới**, nhưng chưa áp dụng thủ công vào database thật trong lượt này. Khởi động phiên bản backend đã cập nhật trong khung bảo trì được phép; kiểm tra đăng nhập và `GET /api/oms/warehouse-map` sau khi khởi động.
2. Đăng nhập web quản lý bằng tài khoản quản trị rồi mở **Bản đồ kho**. Thêm tầng, nạp ảnh hoặc nhập kích thước mét. Nếu có ảnh, chọn hai điểm cách nhau một đoạn đã đo và tính tỷ lệ.
3. Đặt node lối đi → nối cạnh → đặt ô kệ (chọn mã OMS và node tiếp cận) → đặt mốc QR (chọn node neo, bề ngang in và góc). Với vị trí nhiều, nhập CSV mẫu; JSON dùng để sao lưu/chuyển bản nháp.
4. Chọn một mốc và ô kệ ở **Xem trước tuyến**. Nếu không có tuyến, kiểm tra cạnh chặn/một chiều hoặc node tiếp cận. Đi bộ kiểm tra ngoài kho; preview trên web không tự chứng minh lối đi an toàn.
5. Chỉ tích **đã đo kiểm** sau khi đối chiếu thực địa. Bấm **Công bố bản đồ**; app nhân viên tải map phiên bản mới khi mở lại. Nếu chưa có dữ liệu thật, để trống hoặc công bố chưa khảo sát; app sẽ cảnh báo, không coi là hướng dẫn vận hành.
6. Trong Unity/app: mở nhiệm vụ hoặc chi tiết tồn kho → **Chỉ đường**; quét QR mốc gần nhất qua màn **Quét mã**. App hiển thị tuyến 2D tới node tiếp cận. Đến kệ vẫn cần đối chiếu mã vị trí/sản phẩm theo nghiệp vụ OMS; nút đổi trạng thái nhiệm vụ hiện **không** phải giao dịch trừ/cộng tồn.

## Giới hạn chưa hoàn tất

- Web chưa có lớp hình học riêng cho rack/obstacle, vùng cấm polygon, cửa và thang; cạnh được khai báo thủ công. Cần bổ sung lớp này và kiểm tra giao cắt trước khi coi tuyến là an toàn.
- Tuyến hiện cùng tầng; chưa có thang/tầng liên thông, tuyến tối ưu theo mật độ xe nâng, sơ tán hoặc reroute theo chướng ngại vật thời gian thực.
- Mã QR chỉ nhận ID; chưa có nguồn pose/heading AR. Chưa có in marker tự động hoặc đồng bộ trạng thái hỏng/hiệu chuẩn.
- Nút nhiệm vụ hiện cập nhật tiến độ, chưa xác nhận scan source/destination/product/lot hay idempotent stock transaction. Không dùng bản này làm bằng chứng picking/putaway hoàn chỉnh.
- Samsung chưa được cắm thử; quyền camera, độ chính xác, tải bản đồ, offline và hiệu năng chưa được chấp nhận trên thiết bị.
- APK lượt này build bằng Unity 6000.4.7f1 với cờ `--burst-disable-compilation` vì Windows Application Control chặn `Unity.Burst.Cecil` trong build Burst AOT; compile, Android validator và build không lỗi sau khi dùng cờ. Đây là workaround cho môi trường build, không phải bằng chứng tính năng AR/thiết bị đã đạt QA; build lại không có cờ có thể tái hiện lỗi chính sách Windows.

