# Roompresent — Scene dẫn đường theo video

Ngày 19/11/2026. Project: `D:\unity of teep\JarVis-Warehouse-AR`.

## Scene đã dựng

`Assets/Scenes/ARNavigation_Immersal_Video.unity` đã được đăng ký trong danh sách build và liên kết từ mục Immersal AR trên màn Bản đồ của JarVis.

- SDK Immersal 2.5.0 thật, XRSpace, PoseFilter → PoseSmoother và AR camera của sample chính thức.
- Map định vị **153613 — Roompresent**, dùng nguyên file `.bytes` và metadata người dùng tải. Mây điểm được đọc từ file sparse PLY thật.
- GLB **Scaniverse 2026-11-19 153704.glb**, nhập bằng **com.unity.cloud.gltfast 6.21.0** như phần hướng dẫn tương ứng trong video. Mô hình dùng để đặt nội dung và kiểm tra trong Editor; được đánh dấu EditorOnly, không đè hình quét lên camera thật khi build.
- Navigation UI, Navigation Target và vật liệu đường/mũi tên từ sample SDK. Prefab UI cũ trong project mặc định bật adapter Atlas; riêng scene Roompresent đã chuyển về chế độ NavMesh thật.
- GenerateNavMesh chỉ lấy lớp Navigation. Sàn đi được được lọc từ các tam giác gần mặt sàn, gần nằm ngang trong scan; không dùng hình chữ nhật bao phủ cả bàn, xe và ghế. Sau bake, renderer sàn được ẩn.
- Chế độ đường hoàn chỉnh giữ nguyên các góc NavMesh, lấy vị trí sàn gần camera trước khi tìm đường và bỏ đường cũ nếu không tìm được tuyến đầy đủ. Các thay đổi này là tùy chọn riêng của scene mới; adapter Atlas cũ vẫn giữ hành vi cũ.

## A/B/C theo ảnh chủ dự án xác nhận

Tọa độ dưới đây là điểm chọn trên bề mặt GLB sau phép đổi hệ trục của glTFast sang Unity, đơn vị mét; **không phải kết quả đo sai số ngoài hiện trường**.

| Điểm | Vị trí | X | Y | Z |
|---|---|---:|---:|---:|
| A | Tờ mã trên bàn thuyết trình, dùng lại thẻ E | 1.632181 | 0.826427 | -3.241234 |
| B | Xe sách bên trái theo ảnh người dùng gửi | -2.050744 | 0.854697 | 1.839226 |
| C | Xe sách bên phải theo ảnh người dùng gửi | -0.700945 | 0.670096 | 2.886023 |

Ghim hiển thị tại đồ vật. Tìm đường kết thúc trên phần sàn đi được gần đồ vật để người dùng có thể đứng quét mã; không có ghim phụ biểu thị một đích khác. Alias E → A hiện là cấu hình của scene; việc này không tự thay đổi bộ xác minh barcode/QR của workflow nhiệm vụ chưa triển khai.

## Kiểm tra đã chạy và giới hạn

`Logs/ImmersalVideoSetup/roompresent-paths.txt` ghi kết quả thực tế từ NavMesh trong Unity: A→B, A→C, B→A, B→C, C→A, C→B đều tìm được đường đầy đủ. Đây là kiểm tra tìm đường trên mô hình, không phải phép thử camera/tracking Samsung.

Hai dữ liệu Scaniverse và Immersal được tạo độc lập, không có biến đổi chung được xác minh. Thử khớp hình học tự động cho phương án tốt nhất có trung vị khoảng cách tới bề mặt gần nhất khoảng 0,149 m, khoảng 68,1% điểm trong ngưỡng 0,30 m. Các số này **không phải độ chính xác định vị**, không xác nhận đúng các điểm vật lý tương ứng và không đủ để nghiệm thu; phương án tự động không được tự áp vào map.

`RoompresentVenue.alignmentVerified` đang **false**. Chỉ dẫn AR bị khóa tới khi căn map được kiểm tra. Khi được xác nhận căn map, UI/ghim/đường cũng chỉ hiện khi ARSession đang tracking và Immersal đã có localization thành công. Mất tracking sẽ ẩn chỉ dẫn. Chưa xác nhận hiệu quả trên Samsung.

Scene có cấu trúc và NavMesh để tiếp tục, **chưa phải APK AR đã kiểm chứng trong phòng thật**. Cần đối chiếu các mốc vật lý cố định, kiểm tra riêng mốc không dùng để căn, thử đi/quay người/mất và khôi phục tracking trên thiết bị trước khi bật trạng thái đã xác minh. Khóa bánh hai xe trong lúc căn và trình diễn; nếu đổi vị trí xe phải cập nhật vị trí đích tương ứng.

## Thao tác tiếp trong Unity

Menu `Tools → JarVis → Immersal`:

1. **05 Set Up Roompresent Navigation**: dựng/cập nhật phần map và navigation; giữ vị trí target đã tồn tại để không xóa các chỉnh sửa bằng tay. Lần chạy này đặt trạng thái căn map về chưa xác minh.
2. **06 Review Roompresent Paths**: kiểm tra các tuyến trên NavMesh, xuất báo cáo.
3. **07 Show Roompresent Scene Preview**: xem mô hình với đường A→B→C. Đây là mô phỏng Editor, không phải video tracking điện thoại. Đường preview được đánh dấu EditorOnly.
4. **08 Register Roompresent Scans**: chọn ba cặp mốc tương ứng trong mây điểm Immersal và bề mặt Scaniverse. Dùng các góc tường/bàn cố định cách xa nhau; tránh ba điểm thẳng hàng và tránh dùng xe di chuyển làm mốc.

Công cụ căn map cho chọn từng điểm trực tiếp trong Scene, kiểm tra sự phù hợp tỷ lệ/khoảng cách và báo sai số khớp. Nó không tự bật `Alignment Verified`. Cần kiểm tra thêm các mốc độc lập và tracking thật.

Token đã nhập được giữ trong cấu hình cục bộ bị Git bỏ qua: `Library/ImmersalVideoSetup/UserToken.local`; không đưa vào tài liệu, ảnh chụp hay scene lưu trên nguồn. Bản sao scene trước thay đổi cũng nằm dưới Library. Chưa build APK mới trong lượt thiết lập này; bước build cần cấu hình token theo cơ chế cục bộ phù hợp.

## Đối chiếu video

Đã kiểm tra khung hình và bản lời hướng dẫn từ khoảng 3:30 tới phần chạy thử: nhập GLB → căn mesh/map → tạo sàn → layer Navigation → NavMeshSurface/Bake → Navigation UI → Navigation Target → chạy camera AR. Phần đăng nhập và tải map được bỏ qua theo yêu cầu mới nhất. Video dùng GLB cùng nguồn Immersal; GLB Scaniverse của project này cần thêm bước đăng ký hai hệ tọa độ.

Trong quá trình cài glTFast, Unity yêu cầu mở lại Editor vì Burst đổi phiên bản. Scene và bản sao cục bộ đã được lưu trước khi mở lại. Log còn ghi Windows code integrity chặn một số DLL Burst JIT. Không thay đổi thiết lập bảo vệ Windows. Việc scene bake thành công không chứng minh mọi tác vụ Burst hay tracking thiết bị đã hoạt động.
