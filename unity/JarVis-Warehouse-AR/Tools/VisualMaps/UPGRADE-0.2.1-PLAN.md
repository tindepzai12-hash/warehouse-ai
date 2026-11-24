# JarVis Maps — nâng quét vật phẩm, sơ đồ và chỉ đường

Ngày 26/10/2026, Asia/Saigon. Yêu cầu kế hoạch/prompt được bổ sung bằng yêu cầu xuất APK và chép Samsung. Bản 0.2.1 thực hiện các lát cắt mô tả bên dưới; không coi các mốc tương lai đã hoàn thành.

## Kết quả khảo sát mã và ảnh

- Unity 6000.4.7f1; AR Foundation 6.4.3; ARCore 6.6.2; runtime riêng Assets/JarVis/Scripts/VisualMaps, scene JarVisVisualMaps. Main Warehouse có owner khác.
- Hiện có: nhiều capture/map/revision, RGB + pose/intrinsics + depth/confidence tùy thiết bị, sparse COLMAP/hloc, detector NanoDet 2D, PC surface grid unknown/floor/obstacle. Chưa có dense mesh, semantic inventory map hay tuyến được chứng minh toàn thân.
- Lỗi hiện có: arrow lookahead bị giới hạn bởi điểm mẫu kế tiếp (~0.12 m), trong khi rule ẩn arrow ở <0.75 m; khiến tuyến có nhiều mẫu mất arrow. Progress segment chỉ tiến từng mẫu khiến người đi nhanh bị bỏ lại.
- Ảnh tham khảo: nhóm chevron sáng/viền xanh rõ trên camera, đích và khoảng cách dễ đọc. Dùng ngôn ngữ thị giác này, không trích logo/ảnh Google làm tài nguyên. Mũi tên phải lấy hướng từ tuyến và tương quan camera.
- UI repair: giữ uGUI/safe area/CanvasScaler412x915. Thêm minimap vào vùng giữa header và action card, có chiều cao theo chỗ trống; không che camera trung tâm/khung QR. Chủ động kiểm tra 412x915 và 412x732. Hình tham khảo là ý tưởng, không crop thành widget nên candidate/item/crop ledgers rỗng. Agent review chấp nhận cấu trúc này cho lần triển khai.

## Kiến trúc đích

1. Scan phone: tự chụp RGB/pose/calibration; nhắc tối, rung, thiếu góc bằng bằng chứng cảm biến. Không dùng % theo thời gian hoặc giả độ phủ toàn phòng. Thu floor/depth nếu có, vẫn cứu và xuất được ảnh nếu thiếu.
2. PC: kiểm archive -> dựng/đối chiếu sparse -> hiệu chỉnh hệ tọa độ -> geometry -> sơ đồ 2D -> object observations -> duyệt -> xuất đúng ID/revision.
3. Một map frame duy nhất: p_map = inverse(T_world_from_map) * p_camera_world. Minimap lấy x,z và hướng chiếu ngang của camera. Route/đích/geometry cùng origin/metres. Không snap chấm người dùng lên tuyến để che lệch.
4. AR: QR chỉ chọn điểm/ngữ cảnh; pose cần khớp ảnh. ARCore theo dõi giữa các lần relocalize. Khi mất tracking, chấm live/arrow biến mất, map vẫn xem được và trạng thái ghi rõ đang đối chiếu.
5. Hai chế độ khả dụng: xem/định vị trên map đã dựng; chỉ đường khi map có tuyến được duyệt. Không đòi tuyến A–B để xem vị trí trong phòng.

## Các mốc và gate

### M1 — tọa độ + sơ đồ 2D (0.2.1)
- Android đọc /maps/{id} và /geometry bằng token hiện tại; hủy/loại callback khi đổi map hoặc rời màn.
- Kiểm width/height <=512, origin/resolution/cells, cell enum và tọa độ hữu hạn. Geometry lỗi không biến thành map đường trống.
- Vẽ grid đã phân tích, tuyến, A/B, hướng và vị trí thật trong map frame. Unknown khác floor, obstacle khác route.
- Cho định vị không QR trong chế độ xem map; chỉ đường A–B vẫn có QR A + hai góc khớp độc lập.
- Nghiệm thu: xoay/dịch T, camera di chuyển x,z và yaw; mapID đổi; mất tracking; map không route/không depth.

### M2 — mũi tên dễ đọc (0.2.1)
- Follow theo tổng quãng đường tuyến, project chỉ quanh progress hợp lý; lookahead 1.6 m, không theo số lượng waypoint.
- Khi gần cua, marker dừng ở cua, hướng nhắc lấy tangent trước/sau; không nối tắt qua cua.
- World cue trắng viền xanh, một cụm 1–3 chevron tùy hướng, chiều cao vừa mắt; animation tiến/lùi <=3 cm có mục đích. Không phủ chuỗi kín phòng.
- Đích có pin B và khoảng cách còn lại; hướng camera lệch có chữ gợi ý. Bỏ cue khi đã tới/mất định vị/lệch lối.
- Test tuyến dày/thưa, thẳng/trái/phải, quay ngược, cua gắt/hairpin, đứng yên, đi nhanh qua nhiều mẫu.

### M3 — scan đồ vật rõ nguồn (0.2.1)
- Giữ ảnh chụp đang nhận diện để người dùng so kết quả đúng ảnh; hiện frame ảnh cùng bounding boxes khi có.
- Không gắn box cũ lên camera live. Nhãn detector là loại phổ thông/chưa xác nhận, không tự tạo SKU/tồn kho.
- Mất mạng/model không có phải báo lý do, ảnh capture gốc giữ nguyên. Bước tiếp theo là người dùng xác nhận vật phẩm + gắn vị trí 3D khi có depth hợp lệ.

### M4 — geometry tốt và map vật phẩm (sau khi có dữ liệu phòng)
- Đo/kiểm depth raw rotation, intrinsics, camera pose/time trên Samsung; không suy từ display rotation.
- Lưu observed free-space rays + occupied endpoints theo voxel chiều cao, giữ unknown; fuse nhiều góc/confidence. Tách floor/obstacles/headroom; grid2D từ cột thể tích, không từ camera trajectory.
- Nếu depth không có: PC dense MVS/TSDF khi thật sự có depth/poses tương thích. Không coi sparse points/semantic boxes là tường đủ kín.
- Object observations 2D + depth erosion/median/consistency -> object3D có covariance/source frames; chỉ hợp nhất khi cùng loại, vị trí và revision. Không có depth thì giữ nhãn ảnh.
- Studio sửa polygon/door/obstacle, gán tên vật phẩm/đích, duyệt hành lang body radius + headroom; A*/graph dẫn đường chỉ qua vùng đã duyệt; có provenance/reviewer/revision/rollback.
- Nghiệm thu vật lý 3 lượt A/giữa/B + vật thấp/cao/kính/cửa đóng; so vị trí với số đo phòng. Bản 0.2.1 chưa hoàn thành mốc này.

### M5 — phát hành
- Focused compile/logic/UI2 tỷ lệ. Bump0.2.1/code4, giữ com.teep.jarvismaps và chứng thư cũ; kiểm apksigner/hash.
- Chép Download Samsung qua ADB nếu có, MTP nếu chỉ có truyền tệp; xác nhận dung lượng/hash đọc lại. Chép file không đồng nghĩa đã cài/đi thử.
- Thiết bị thử: quét phòng có đồ, save/export, PC dựng, mở cùng map và đối chiếu vị trí. Có log/capture thật mới kết luận alignment.

## Prompt cho GPT-6.1 High / model ít token hơn

Bạn tiếp tục JarVis Maps tại D:/unity of teep/JarVis-Warehouse-AR. Đọc AGENTS.md, CODEX_CONTEXT.md và UNITY_UI_WORKFLOW.md; không truy cập E. Staging và plan này ở RouteRecorder/VisualMaps trong workspace Codex. Không đổi Warehouse/OMS. Yêu cầu người dùng đã cho phép sửa, build và chép Samsung; không hỏi lại các lựa chọn đã chốt. Không tự nhận model đã đổi; người dùng chọn model/effort ở UI.

THỰC HIỆN TUẦN TỰ:
1. Đọc RELEASE-0.2.1/QA nếu có và code owner đúng mốc. Báo current state từ file thực tế; không dùng plan làm chứng cứ đã chạy.
2. Mỗi lần chỉ một lát cắt: contract -> implement -> focused tests -> compile -> fresh UI screenshot nếu chạm UI -> update checklist. Dùng rg tìm phạm vi; không tải hết repo/history.
3. M1/M2/M3 chỉ đóng khi API, tọa độ, trạng thái async và UI fixture đã kiểm. Đầu vào giả chỉ ghi synthetic. Không bỏ CanNavigate để làm nút chạy.
4. Với M4, trước tiên phân tích ZIP phòng thật và calibration. Lập báo cáo missing evidence, rồi triển khai một tầng geometry. Không dùng floor preview thay chứng minh full-body clearance. Nếu không có depth, đừng hứa tránh mọi vật.
5. Tiêu chí logic: đường thẳng không tự rẽ; waypoint mật độ khác không đổi lookahead; turn không nối tắt; off-route người thật không bị kéo lên centerline; hai bản map/revision không trộn; QR không sinh pose giả; object box chỉ trên ảnh đã infer.
6. Khi build, giữ package/signature/data, restore PlayerSettings/scene; không mở project thứ hai khi Unity đang dùng. Chỉ chép APK version mới, hash/check đọc lại; chưa có máy thì báo chính xác.
7. Sau mỗi mốc ghi ngắn: files, command/result, evidence path, chưa kiểm thực địa, mốc tiếp. Không rerun toàn bộ review không liên quan. Tiếp tục đến mốc đã được giao, không dừng ở lời hứa.

ĐẦU RA KHI HẾT CONTEXT: Current task; Completed with evidence; Current files; Exact next command; Remaining risks. Dùng plan index thay lặp lại toàn bộ chat. GPT-6.1 High nên giữ contract/QA liên mốc; model ít token hơn nhận đúng một mốc + test acceptance, không tự mở rộng kiến trúc.

## Nguồn nghiên cứu chính thức

- https://colmap.github.io/tutorial.html — sparse SfM và dense MVS là hai bước khác nhau.
- https://github.com/cvg/Hierarchical-Localization — visual localization camera against reconstructed model.
- https://developers.google.com/ar/develop/anchors — anchors cập nhật theo tracking; giữ nội dung gần anchor (khuyến nghị khoảng8m).
- https://developers.google.com/ar/develop/java/depth/raw-depth — depth thưa + confidence, yêu cầu chuyển động và kiểm timestamp.
- https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.4/manual/features/device-tracking.html — XROrigin/session/world space.
- https://www.open3d.org/docs/latest/tutorial/t_reconstruction_system/integration.html — TSDF tích hợp depth với pose đã biết, không tự suy geometry từ ảnh bất kỳ.

Tự rà prompt: rõ scope/revision/đơn vị/nguồn pose, đường đi và vật phẩm tách đúng trách nhiệm, có dữ liệu lỗi/unknown, có gate thiết bị thật và mốc tương lai. Mức chất lượng được đánh giá qua code/evidence; tên model không thay được nghiệm thu.
