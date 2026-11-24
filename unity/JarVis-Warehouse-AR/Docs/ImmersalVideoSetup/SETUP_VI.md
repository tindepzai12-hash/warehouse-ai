# Immersal — Setup theo video AR Indoor Navigation

Ngày: 19/11/2026. Repo đối chiếu: https://github.com/immersal/imdk-unity

## Thứ tự thực hiện theo các ảnh người dùng gửi

1. **Package Manager / Immersal SDK.** Project JarVis đã có gói thật `com.immersal.core` 2.5.0 dạng embedded. Video dùng 2.0.3 trên Unity 2022.3; máy hiện tại dùng Unity 6000.4.7f1. Giữ gói đã có để tránh nhập SDK hai lần. Với project mới hoàn toàn, URL cài theo repo là `https://github.com/immersal/imdk-unity.git`; cần chốt phiên bản tương thích thay vì tùy tiện cập nhật giữa lượt test.
2. **Scene riêng.** Tạo `Assets/Scenes/ARNavigation_Immersal_Video.unity` từ SimpleSample chính thức đã import. Không dựng giả các component Immersal bằng GameObject trống, không ghi đè scene app chính.
3. **AR Session + XR Origin.** Giữ cấu hình camera và input chính thức của sample. Đây là cấu hình để chạy AR trên nền tảng được hỗ trợ; Game view đen trong Editor không chứng minh camera Android lỗi.
4. **ImmersalSDK.** Dùng prefab thật từ package với Session, Platform, Localizer, SceneUpdater và TrackingAnalyzer được nối sẵn.
5. **XRSpace.** Có PoseFilter và PoseSmoother thật. Data Processors nối theo thứ tự PoseFilter → PoseSmoother. Bật Process Poses để chuỗi xử lý được dùng; đây là lựa chọn cấu hình cần kiểm tra khi chạy, không phải cam kết hết trôi.
6. **XR Map.** Đặt dưới XRSpace, trạng thái ban đầu chưa gán map: mapId=-1, Map File trống. Không đổi đuôi GLB thành .bytes hoặc điền một map ID không tồn tại.
7. **Canvas / EventSystem.** Giữ UI trạng thái định vị chính thức của sample. Chưa cấu hình map thì không được hiển thị thành công giả.
8. **Login như ảnh cuối.** Mở menu Immersal SDK và mục đăng nhập của gói đang dùng. Người dùng tự nhập tài khoản trong Unity; không gửi mật khẩu/token qua chat, không chụp màn hình khi credential đang hiện. Không tự tạo tài khoản/key hay đăng nhập bằng tài khoản khác.
9. **Gán bản đồ ở bước tiếp theo.** Cần map Immersal hợp lệ cho chính địa điểm: file .bytes và dữ liệu đi kèm hoặc map ID có quyền truy cập, theo hướng dẫn của SDK. Mesh Scaniverse/Polycam hỗ trợ đối chiếu hình học và đặt nội dung nhưng không thay thế dữ liệu định vị Immersal.
10. **Sau khi có map mới tiếp tục phần navigation của video.** Đối chiếu scan/map2D, đặt các Navigation Target A/B/C đúng số đo và hệ tọa độ, tạo/bake vùng NavMesh, gắn Navigation UI/Path, rồi kiểm tra trên Samsung. Cảnh setup trống hiện tại chưa phải demo chỉ đường đã nghiệm thu.

## Mở scene và kiểm tra

Project: `D:\unity of teep\JarVis-Warehouse-AR`.

- Menu `Tools → JarVis → Immersal → 01 Create or Open Video Setup` tạo scene nếu chưa có; nếu đã có thì chỉ mở, không ghi đè thay đổi bằng tay.
- Menu `Tools → JarVis → Immersal → 02 Validate Video Setup` kiểm tra tham chiếu scene đang mở.
- Menu `Tools → JarVis → Immersal → 03 Add to Map AR and Open Login` đăng ký scene trong danh sách build, mở scene và cửa sổ đăng nhập Immersal. Không đọc hay tự nhập tài khoản.
- Trong app: tab **Bản đồ → Immersal AR → Mở Immersal AR** mở scene riêng, tạm dừng UI/camera của màn chính và có nút **Về bản đồ kho**. Chưa có map thì hiển thị trạng thái thiếu map; không báo định vị thành công.
- Để thêm map sau này: mở scene trên, chọn **XRSpace → XR Map**, gán Map File hoặc Map ID theo SDK rồi lưu scene. Cửa sổ **Immersal Login** là công cụ của Unity Editor, không phải màn đăng nhập trong APK.
- Script Editor: `Assets/Editor/ProjectBootstrap/ImmersalVideoSetup.cs`.
- Báo cáo khi chạy thành công: `Logs/ImmersalVideoSetup/validation.json`.

Kiểm tra này chỉ xác nhận cấu trúc scene, component thật, tham chiếu SDK, chuỗi pose và script không bị thiếu. Không chứng minh map đã định vị, NavMesh đã được tạo cho địa điểm hoặc camera/tracking Samsung đã hoạt động.

## Hai cơ chế phải phân biệt

Video gốc dùng Immersal để nhận cảnh và tìm pose theo map của Immersal. Yêu cầu QR A trước đây dùng một mốc cố định để căn map. Trong scene mới ưu tiên dựng đúng Immersal theo video; chưa tự trộn hai hệ thống cùng di chuyển XRSpace. Nếu thêm QR A dự phòng về sau, phải có cơ chế chọn nguồn pose và phép đổi tọa độ rõ ràng.

Workflow Delivery/Pickup hai hộp vẫn giữ theo kế hoạch đã thống nhất. Việc dựng scene theo video không tự triển khai hoặc thay đổi các điều kiện xác minh barcode/QR của nhiệm vụ.
