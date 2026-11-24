# JarVis Maps — quét không gian trước, xác nhận đường đi sau

Ngày: 25/10/2026. Trạng thái: thiết kế thay thế sau kiểm tra mã nguồn; chưa phải bản APK đã triển khai.

## 1. Sửa đúng vấn đề

Yêu cầu của chủ dự án: app phải thu ảnh của phòng thật, nhận biết hình học liên quan tới tường/vật cản, và kiểm tra lối đi A–B trước khi cho người dùng đi theo mũi tên. Không tiếp tục chỉ sửa hình mũi tên trên một tuyến chưa được kiểm tra.

Qua mã nguồn hiện tại:
- `VisualMapData.cs`: capture schema 1 chứa JPEG, intrinsics, pose, mặt sàn và cờ A/B. Không có depth, confidence, lưới vật cản hay hành lang đi bộ.
- `visualmap_core.py`, `reconstruct`: dựng COLMAP sparse, căn tọa độ mét, chiếu vị trí camera của tuyến xuống một độ cao sàn rồi xuất `state = ready`. Có kiểm tra khả năng ghép ảnh, độ lệch tọa độ và chiều dài tuyến; chưa kiểm tra tuyến xuyên tường hay va vật cản.
- `VisualMapSession.cs`: QR A/B là mã định danh, không phải phép đo pose 3D của một vật mốc đã hiệu chuẩn. Mũi tên bám tuyến đã ghi, không biết toàn bộ không gian có thể đi được.
- Vì vậy: định vị khớp ảnh không đồng nghĩa có đường đi hợp lệ. Thêm ảnh mà không thêm dữ liệu hình học và quy tắc kiểm tra không tự khắc phục được vấn đề này.

Không đổi package JarVis Warehouse, không xóa dữ liệu hay APK cũ, không truy cập ổ E. Các bản 0.1.0/0.1.1 không được quảng bá là có tránh vật cản.

## 2. Công nghệ và giới hạn đã đối chiếu

Tách ba lớp dữ liệu:
1. **Định vị**: COLMAP + hloc tìm vị trí camera trong không gian đã chụp.
2. **Hình học**: ảnh chiều sâu + độ tin cậy + pose đồng bộ để dựng bằng chứng sàn, bề mặt cản và vùng chưa quan sát. Một bản dựng thưa không thay thế lớp này.
3. **Đường đi**: tuyến A–B nằm trong một hành lang đã có dữ liệu đủ tốt, không giao vùng cản hoặc vùng chưa xác định.

Ưu tiên Android ARCore Raw Depth kèm confidence. Danh sách Google có Galaxy S20 Ultra 5G hỗ trợ Depth và ToF, nhưng app vẫn phải kiểm tra thiết bị thực tế và đọc thử dữ liệu. Không suy từ tên máy ra cảm biến chắc chắn đang hoạt động.

Trong project hiện có AR Foundation 6.4.3 / ARCore 6.6.2; API local có `AROcclusionManager.TryAcquireRawEnvironmentDepthCpuImage` và `TryAcquireEnvironmentDepthConfidenceCpuImage`. Có API không chứng minh ảnh depth trên Samsung đã đúng chiều, đúng timestamp hoặc đúng đơn vị.

Raw Depth không phủ mọi pixel; giá trị confidence thấp không được diễn giải thành khoảng trống. Kính, gương, bề mặt khó đo, vật mảnh và vùng bị che khuất có thể không được nhận biết đầy đủ. Không hứa phát hiện mọi vật cản hay bảo đảm an toàn tuyệt đối.

Nếu Depth không khả dụng: báo rõ chưa hỗ trợ quy trình quét hình học tự động và không cho công bố map theo quy trình mới. Không tự quay lại dùng ảnh thường rồi ghi “đã kiểm tra vật cản”. Hướng thay thế sau này là sơ đồ có kích thước và vùng cấm được đo/duyệt thủ công; ngoài phạm vi bản test này.

Nguồn chính thức:
- [COLMAP: sparse và dense reconstruction là hai giai đoạn khác nhau](https://colmap.github.io/tutorial.html).
- [Google: Raw Depth, confidence và giới hạn vùng có dữ liệu](https://developers.google.com/ar/develop/unity-arf/depth/raw-depth).
- [Google: thiết bị hỗ trợ, gồm Galaxy S20 Ultra 5G](https://developers.google.com/ar/devices).
- [Google: Depth tốt nhất khoảng 0,5–5 m; cần dịch chuyển thiết bị](https://developers.google.com/ar/develop/depth).

## 3. Luồng sử dụng thay thế

### A. Tạo map phòng — chưa có đường dẫn
1. Kiểm tra quyền camera, tracking và khả năng nhận Depth/confidence. Hiển thị nguyên nhân nếu thiếu; có nút thử lại/thoát.
2. Xác nhận đúng mặt sàn thật. Bản test chỉ một phòng, một tầng, sàn gần phẳng; không tự gộp cầu thang hoặc sàn có cao độ khác.
3. Tự chụp khi người dùng dịch chuyển chậm. Hướng dẫn nhìn cả sàn, chân tường, hai bên lối đi, góc phòng, cửa và vật chắn; không chỉ quay camera tại chỗ.
4. Lưu ảnh + intrinsics + pose + timestamp; depth/confidence phải có calibration và pose tương ứng. Không ghép ảnh chiều sâu cũ vào pose camera mới.
5. Gửi PC dựng **map nháp**: sparse map phục vụ định vị; lớp hình học phục vụ kiểm tra đường. Chưa phát hành làm map chỉ đường.
6. Xem hình chiếu từ trên xuống: vùng đã đo có bằng chứng sàn, vùng bị cản, vùng thiếu dữ liệu. Màu phải đi kèm nhãn/chú giải. Không tự vẽ kín tường nếu còn lỗ dữ liệu.

### B. Xác định tuyến A–B trên map nháp
1. Định vị camera vào đúng map nháp trước. Nếu chuyển phiên AR sau khi dựng map, bắt buộc đối chiếu lại; không dùng nguyên gốc tọa độ của phiên cũ.
2. Quét A, xác nhận vị trí đứng trên sàn. QR chỉ chọn đầu tuyến; phép đối chiếu ảnh mới xác định hệ tọa độ.
3. Người tạo map đi thực tế theo lối muốn sử dụng tới B; app ghi tuyến ứng viên, không coi việc đã đi qua là bằng chứng hình học đầy đủ.
4. Quét B rồi xác nhận điểm đứng đích trên sàn. Không lấy độ cao/tọa độ của hình QR treo trên tường làm điểm đến đi bộ.
5. Kiểm tra **toàn bộ hành lang quanh tuyến**, không chỉ các điểm camera. Nếu thiếu dữ liệu, đánh dấu đúng đoạn cần quét bổ sung; nếu bị cản, yêu cầu chọn/ghi lại tuyến hợp lệ.
6. Nếu phải cập nhật hình học, tạo phiên bản nháp mới và kiểm tra lại tuyến. Không trộn ngầm dữ liệu khác hệ tọa độ.

### C. Duyệt rồi mới phát hành một map
1. Cho xem A, B, tuyến, đoạn thiếu dữ liệu/vướng vật cản và độ rộng hành lang dự kiến.
2. Nút “Dùng map này” chỉ bật sau khi kiểm tra hình học, định vị và tuyến đều đạt, đồng thời người tạo map đã xác nhận.
3. Chỉ thay map đang dùng sau khi bản mới hợp lệ; lỗi dựng, mất mạng, hủy hoặc thoát không được làm mất bản trước.
4. Bản schema 1 cũ được giữ để tra cứu/xuất, không tự nâng thành map đã kiểm tra vật cản.

### D. Chỉ đường
1. Chọn “Chỉ đường” → quét A → đối chiếu ảnh với đúng phiên bản map.
2. Chỉ hiện mũi tên sau khi có các quan sát độc lập nhất quán; nghi ngờ vị trí thì dừng hướng dẫn, không dịch chuyển tuyến đột ngột.
3. Theo tuyến đã duyệt, không nối thẳng A–B, không tự làm mượt cắt qua góc tường. Một mũi tên phía trước và marker B; khoảng cách chỉ là ước lượng.
4. Kiểm tra Depth phía trước khi đi trong chế độ mới. Vật cản mới hoặc thiếu dữ liệu cần thiết → “Dừng lại / nhìn lại lối đi”; không tự bịa tuyến vòng qua vùng chưa quét.
5. Chỉ báo tới gần B khi vị trí đủ tin cậy và đã đi tới vùng đích; không chỉ vì hình QR B xuất hiện trong camera.

## 4. Quy tắc kiểm tra đường đi

- Ô hình học có ít nhất ba trạng thái: **chưa xác định**, **có bằng chứng sàn/lối trống**, **bị cản**. Không có điểm 3D hoặc depth bằng 0 không có nghĩa là đi được.
- Cần kiểm tra không gian cho thân người: bề rộng hành lang, chiều cao, mặt sàn liên tục; không chỉ một đường 2D. Ngưỡng ban đầu phải ghi trong cấu hình và được thử tại phòng thật, không gọi là tiêu chuẩn an toàn đã chứng nhận.
- Ranh giới map cũng là vùng chưa xác định. Khoảng trống ở mép ảnh không được trở thành lối ra khỏi phòng.
- Kiểm tra các đoạn nối và các góc cua bằng thể tích quét của hành lang; kiểm tra lại sau resample/smoothing. Đường gốc hợp lệ không chứng minh đường làm mượt hợp lệ.
- Khoảng hở giữa hai vật cản không đủ rộng cho cấu hình người đi → từ chối, dù tâm tuyến không chạm vật.
- Bằng chứng vật cản không được xóa bởi một ảnh mất depth. Chỉ cập nhật bằng phép đo mới đủ tin cậy và có provenance.
- Map dựng từ trước không đảm bảo bố trí phòng vẫn giữ nguyên. Theo dõi vật cản mới là lớp hỗ trợ, không thay thế quan sát thực tế của người dùng.
- Không nối các đoạn ghi trước/sau mất tracking khi chưa có phép căn chỉnh được kiểm chứng.

## 5. Giao diện tinh gọn — Unity uGUI hiện có

Giữ xanh–trắng JarVis, Be Vietnam Pro, camera tràn màn hình và safe area. Đây là thay đổi luồng chức năng, không rebuild UI Warehouse hoặc đổi toàn bộ thiết kế thương hiệu.

Trang đầu chỉ có hai hành động chính: **Tạo map phòng** và **Chỉ đường**. Kết nối PC là mục phụ, không thêm menu điều khiển kỹ thuật lên camera.

Các bước tạo map dùng chung một khung, không tạo thêm hàng loạt màn menu:
1. Quét phòng: camera + hướng dẫn một câu + ảnh/depth hợp lệ + nút dừng chụp.
2. Dựng nháp: công đoạn PC + lỗi có cách khắc phục; giữ gói dữ liệu để gửi lại.
3. Kiểm tra map: hình chiếu trên xuống + chú giải vùng đã đo/cản/chưa rõ.
4. Ghi A–B: trạng thái quét mã rõ ràng; nhận mã một lần có phản hồi; ghi tuyến chỉ khi đã định vị.
5. Duyệt tuyến: tô đúng đoạn cần quét lại/bị cản; chỉ cho dùng khi đạt.

Chỉ đường: header gọn, một hướng dẫn đang cần làm, khoảng cách ước lượng, nút dừng; không phủ kín camera bằng log, nhãn kỹ thuật hoặc nhiều mũi tên.

### Tiến độ không gây hiểu nhầm
- Lúc chưa xác định phạm vi cần quét: hiện bước hiện tại, số ảnh/khung depth hợp lệ và việc cần làm tiếp. Không hứa phần trăm toàn phòng khi chưa biết kích thước/phạm vi phòng.
- Khi đã có hành lang A–B: có thể hiện `ô đủ dữ liệu / tổng ô cần kiểm tra` thành % phủ dữ liệu của **hành lang đó**. Phải ghi rõ mẫu số; ô bị cản vẫn là ô đã quan sát nên 100% dữ liệu không có nghĩa tuyến hợp lệ.
- Tách hai kết quả: “Đủ dữ liệu: 100%” và “Tuyến: bị chặn / còn thiếu / đạt kiểm tra”. Không dùng đồng hồ thời gian để giả tăng %.
- Upload dùng phần trăm byte; dựng PC dùng tên công đoạn. Không gọi số ảnh ghép được là % quét phòng.

## 6. Prompt thực thi được rà soát

> Thiết kế lại module JarVis Maps hiện có theo mục 1–5 của tài liệu này. Mục tiêu là một phòng, một tầng, một map và một tuyến A–B được kiểm tra; không mở rộng thành hệ thống dẫn đường cả tòa nhà. Giữ app Warehouse/Routes, package, server auth và dữ liệu cũ. Không dùng GPS, quota Immersal, dịch vụ trả phí hoặc mô hình giả lập làm bằng chứng về phòng thật.
>
> Đầu tiên tách environment map, localization và route thành dữ liệu/trạng thái riêng. Capture ảnh phòng phải thực hiện được trước khi có QR A/B. Schema mới chứa calibration, timestamp, pose, depth/confidence và provenance; không mặc định dữ liệu schema cũ đã có hình học. PC dựng map nháp trước, sau đó mới ghi/duyệt tuyến; nếu khác phiên AR thì bắt buộc tái định vị.
>
> Kiểm tra capability Depth ở runtime, loại phép đo không đồng bộ/sai định dạng/độ tin cậy thấp và giữ unknown. Xác minh chiều ảnh, đơn vị depth, intrinsics và camera raw/display bằng fixture rồi bằng vật mốc đo thật trên Samsung; không suy từ build thành công. Không chiếu một mặt sàn giả qua mọi đồ vật.
>
> Tạo lớp hình học có thể kiểm tra hành lang đi bộ. Chặn công bố và chặn hướng dẫn qua tường, vật cản, vùng thiếu quan sát, lối quá hẹp hoặc sàn không được hỗ trợ. Duyệt cả đoạn nối và kết quả smoothing. Không cấp A* hay NavMesh quyền đi qua unknown. Khi vật cản mới xuất hiện, dừng; không tự đổi tuyến chưa được kiểm tra.
>
> Giữ QR như mã ID; không giả vờ đã biết pose 3D của QR chỉ từ chuỗi giải mã. Định vị phải gắn đúng map/version/session, cần quan sát độc lập nhất quán. Phản hồi cũ, mất tracking, nhảy tọa độ, cancel và app pause không được làm map xuất hiện ở vị trí sai. Thay map bằng giao dịch nguyên tử, giữ bản cũ khi thất bại.
>
> Dùng uGUI/font/tokens hiện có. Làm ít màn theo mục 5, một hành động chính theo ngữ cảnh, phản hồi QR nhận/chưa nhận rõ ràng, không fake %. Không thêm debug overlay vào bản người dùng. Chỉ tối ưu hình mũi tên sau khi tuyến và tọa độ đã đạt. Kiểm tra bằng ảnh UI hai tỷ lệ, kiểm thử logic, server, compile/build và thử đi thật. Chưa có dữ liệu phòng hoặc kiểm thử Samsung thì ghi rõ chưa xác nhận, không bàn giao như đã hoàn tất.

### Đánh giá prompt
- Đúng phạm vi: một phòng / một tuyến, không yêu cầu xây cả navigation engine tổng quát.
- Không còn nhầm sparse map với hình học chống va chạm; dữ liệu thiếu bị chặn.
- Không có vòng phụ thuộc “phải có tuyến mới dựng được map phòng”; tách bản nháp và công bố.
- Không tái dùng tọa độ AR qua phiên mới mà bỏ bước định vị.
- Không hứa QR đơn thuần giải quyết pose; không hứa đổi điện thoại sửa được thuật toán.
- Tiến độ có mẫu số, lỗi có hành động khôi phục, xác nhận không đồng nghĩa đã tránh được mọi vật cản.
- Kết luận: đủ rõ để làm theo từng mốc; chưa đủ bằng chứng để ấn định độ chính xác, tốc độ quét hoặc ngưỡng hành lang cho phòng thật.

## 7. Thứ tự triển khai và cổng kiểm thử

| Mốc | Thay đổi | Điều kiện qua mốc |
| --- | --- | --- |
| 1 | Capture RGB + Raw Depth/confidence, chuẩn dữ liệu mới | Samsung có dữ liệu thật; đo thử sàn/tường/vật mốc; ảnh-depth-pose khớp, không đảo trục hoặc dùng dữ liệu cũ |
| 2 | Dựng hình học map nháp độc lập tuyến | Có lớp unknown/cản/sàn, kiểm được ô dữ liệu; lỗi không đụng bản đang dùng |
| 3 | Ghi và kiểm tra hành lang A–B | Vượt test xuyên tường, góc cua, vùng thiếu dữ liệu, cửa hẹp, mép map và đoạn nối dài |
| 4 | UI duyệt map + công bố một bản | Không công bố map thiếu kiểm tra, không khôi phục sai version, có recovery khi mất mạng/dừng/app pause |
| 5 | Dẫn đường + quan sát vật cản phía trước | Tái định vị đúng sau mở lại app, không nhảy tuyến, dừng khi vật cản mới hoặc mất dữ liệu cần thiết |
| 6 | APK thử và đi thật | Bản ký/version rõ; ảnh UI không chồng lấn; Samsung quay màn hình và có mốc đo đối chiếu A/B, góc cua và tường |

Danh sách kiểm tra không được bỏ:
- Đường thẳng thoáng; đường chữ L quanh tường; A/B ở hai phía tường; cửa hẹp; bàn/ghế cắt tuyến; vật thấp và vật nhô cao; góc thiếu ảnh; gương/kính; ánh sáng yếu.
- Đường smoothing cắt góc; segment nhảy qua vật mảnh; vùng ngoài map; vật di chuyển sau lúc dựng; lùi/xoay điện thoại; rời app/mất tracking giữa chừng; sai QR; sai map; mạng ngắt; dựng map mới thất bại.
- Hai góc chụp độc lập không thống nhất → không hướng dẫn. Một ảnh lặp lại không được tính thành nhiều lần xác nhận.
- Ghi rõ kết quả là mô phỏng/logic hay thực tế. Chỉ test synthetic không đạt điều kiện bàn giao dẫn đường phòng thật.

## 8. Trạng thái tại thời điểm lập thiết kế

- Đã kiểm tra owner, schema, pipeline dựng map và quy tắc hiện có; xác định thiếu lớp hình học và kiểm tra hành lang.
- Đã đối chiếu tài liệu Google/COLMAP và API package cài trong project.
- Unity CLI hiện không tìm thấy Editor đang kết nối. Chưa chạy QA UI, chưa sửa scene/runtime, chưa xuất APK cho thiết kế mới.
- Chưa có bộ thu mới gồm RGB + Depth + confidence của phòng. Ảnh thường cũ có thể hỗ trợ định vị, nhưng không được chứng nhận thành dữ liệu vật cản chỉ bằng nâng version.
- Bước kỹ thuật đầu tiên là chứng minh capture chiều sâu đồng bộ trên Samsung, không tiếp tục xuất APK mũi tên rồi gọi là đã giải quyết tường/vật cản.
