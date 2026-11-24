# JarVis — audit và sửa UI/UX, 13/10/2026

## A. Phạm vi và bằng chứng

Giữ dự án Unity 6000.4.7f1, scene JarVisFigmaApp, nhận diện trắng–xanh, font/mascot đã duyệt. Không sửa dự án đo phòng, tạo project mới, seed kho, gọi model hay đổi tài khoản thật. Kiểm tra trực tiếp Play trong Editor đang mở; không mở Editor thứ hai. Bằng chứng: `screenshots/before`, `screenshots/after`, `screenshots/settings`; 26 route, VI/EN, sáng/tối, cỡ lớn, viewport 412x915 và 390x844. Route sweep có cả màn nhập cũ; việc gọi route trực tiếp không chứng minh người dùng thực sẽ đi qua màn đó.

Ba vòng hữu hạn: baseline, sửa + after, xác nhận cuối sau phản hồi Settings và review độc lập. Không tuyên bố ứng dụng đã thương mại hóa chỉ vì compile thành công.

Kết quả cuối: native69 checks, product69 checks, Settings17 checks đều PASS; tổng155 assertions offline, không phải155 chức năng production. Bản sao log sạch ở `verification/`; ảnh sweep baseline Tasks/Inventory có QA records còn final đã xóa cache khi signed-out, vì vậy không suy cải thiện populated list từ so ảnh khác trạng thái. Kiểm tra session/callback bằng reflection không gọi API với QA tokens.

## B. Chấm điểm độc lập

AI reviewer Lorentz đọc 78 ảnh before/after và hai ảnh Settings. Rubric: phân cấp15, chữ20, layout15, navigation15, workflow15, interaction10, accessibility10. Điểm là heuristic cho trạng thái trong ảnh, không phải phép đo usability với người thật hoặc chứng nhận accessibility. Không đủ bằng chứng để chấm toàn app 90/100.

| Màn / route | Trước | Sau review đầu | Ghi chú |
|---|---:|---:|---|
| Splash 5:11 |36|36|Chữ JarVis xuống dòng; còn cần sửa layout nguồn có kiểm chứng|
| Login 5:36 |70|71|Sửa giới hạn password; auth thật chưa thử|
| Home 126:8 |65|69|Đậm chữ/icon; weekday rõ; hoạt động ưu tiên chưa cao|
| Offline 85:14 |64|65|Presence không phải authentication|
| Tasks 48:244 |65|65|Before có QA rows, after signed-out; không so populated UX trực tiếp|
| Task location 83:89 |52|56|Route import cũ, không đại diện task detail live|
| Task scan 14:130 |52|56|Thiếu context trong route sweep|
| Task quantity 14:215 |50|54|Nút/số lượng route cũ cần audit riêng|
| Task finish 14:300 |51|55|Không chứng minh write backend|
| Task review 32:149 |48|52|Context thực chưa xác minh|
| Scanner 5:331 |69|71|Camera tắt trong QA|
| Product details 5:375 |48|52|Route import cũ|
| Products & labels 5:450 |64|67|Chỉ draft; chưa ghi/in thật|
| Inventory 5:780 |67|68|Trạng thái trước/sau khác nhau|
| Inventory 182:2 |67|68|Route thứ hai dùng cùng owner|
| Inbound 5:935 |64|67|Chưa write nhập kho|
| Outbound 14:387 |64|67|Chưa write xuất kho|
| Issue 5:1029 |69|71|Submission còn hạn chế|
| JarVis 5:1216 |60|63|Mic/model thật chưa thử|
| Staff message 182:151 |59|62|Chưa chat nhân viên thật|
| Map 215:2 |62|65|2D checkpoint; chưa AR/device acceptance|
| Settings 5:1308 |72|74|Bản chỉnh switch theo ảnh mới: reviewer chấm71, không ép tăng điểm|
| Profile |68|71|Một số thông tin/actions còn hạn chế|
| Activity |70|72|Online activity, không payroll|
| Tools |62|65|Global + dẫn Tools còn khó hiểu|
| Inbound/outbound chooser |70|72|Chọn hành động trước khi vào form|

Sau review độc lập đã sửa Sign in recovery và nút Accept/Start/Resume/Complete ở task detail. Chưa có đánh giá độc lập lần nữa cho hai thay đổi này; không tự nâng điểm.

## C–D. Lỗi gốc và thay đổi thực

- **Chữ nhạt:** Bold flag đã tồn tại nhưng TMP chỉ có Regular. Thêm font Bold chính thức cùng family và mapping700, không thay family; kèm OFL. Dock luôn Bold, cỡ11; vector Home/Tasks/Map nét2.4, giữ mascot/QR và routes.
- **Tên thứ mơ hồ:** `M T W T F S S` → `Mon Tue Wed Thu Fri Sat Sun`. Giữ thứ tự Monday-first của server; không đổi lịch/streak.
- **Settings không nhất quán:** ba switch dùng cùng track58x32/thumb26/touch58x48, x288 trong card; cùng màu On/Off và phản hồi giảm chuyển động. Display & motion14 bằng Text size/Dark mode. Bỏ giá trị Large/Medium/Small cạnh tiêu đề, giữ ba nhãn dưới slider để chọn cỡ chữ.
- **P1 dữ liệu cũ giữa tài khoản:** xóa tasks/stock/query/detail/count/filter khi token/account/endpoint/expiry đổi. Guard generation+session trước nhận callback và task transition. Không xóa dữ liệu thành công cùng phiên chỉ vì refresh mạng thất bại; hiển thị feedback stale và giữ nút reload.
- **P1 password:** Settings cho phép256 nhưng login cắt64. Login giờ256, kể cả input đã tồn tại. Test65 và256 ký tự, không đổi password thật.
- **Working thiếu nhiệm vụ đã nhận:** nhận cả ACCEPTED/IN_PROGRESS/BLOCKED, badge vẫn chỉ rõ status. Không đổi backend state machine. Nút task detail gọi rõ Accept/Start/Resume/Complete.
- **Recovery khó hiểu:** Tasks/Inventory signed-out hiện Sign in thay Refresh; callback tới login, không tự đăng nhập hay fake success.

Files chính: ImportedUiPolish, ImportedUtilityIcon, ImportedFigmaNavigation, ImportedHomeDashboard, ImportedWarehouseLists, ImportedSettings, ThemeSwitchMotion; font TTF/TMP/mapping/license; UiUxAuditReview và SettingsControlsReview; CODEX_CONTEXT. Snapshot GitHub còn đồng bộ các dependency đã hoàn thành lượt trước: WarehouseProductWorkspace, templates/translations, PrototypeWorkspace, PrototypeMascot và ProductWorkspaceReview. Không nhận các thay đổi backend có sẵn là công việc lượt này.

## E. Kiểm thử và giới hạn

| Hạng mục | Trạng thái | Bằng chứng / giới hạn |
|---|---|---|
| Compile mỗi nhóm C# |PASS|Logs/*Compile.json; 0 errors/0 warnings|
| Routes, shortcut callbacks, dock pointer handlers |PASS|Logs/UiUxAudit/before.txt và after.txt|
| Bold face/labels, vector stroke, Mon–Sun |PASS|Native after assertions + ảnh Play|
| Password65/256; đổi account/cache; stale callback; Working statuses |PASS|Offline assertions; fake tokens chỉ trong runner, không yield/call API|
| Settings alignment, geometry, palette, font size, bỏ duplicate size, ba callbacks |PASS|17 checks Logs/UiUxAudit/settings.txt; prefs được khôi phục|
| Product/form/mascot regression |PASS|69 checks Logs/ProductWorkspace/Result.txt, không DB/model/printer|
| TalkBack, contrast toàn bộ app, keyboard/safe area/performance Samsung |NOT TESTED|Không suy từ ảnh Editor|
| Login/tasks mutation/inventory thật và backend downtime E2E |NOT TESTED|Không gọi production để lấy hình QA|
| Camera/mic/STT/AI/physical QR/AR |NOT TESTED|Không device/provider acceptance|
| Tạo SKU/encoder/printer/history, nhập/xuất/issue write, staff chat |BLOCKED|Capability chưa nối; không dựng success giả|
| Trọn workflow picking trên kho thật |NOT TESTED|Cần map/checkpoint/account/order thực|

Ngoại lệ còn lại: text overflow detector ở Quick actions/Today's tasks dùng chế độ Overflow có chủ đích; ảnh xác nhận không đè nhau. Splash JarVis wrap và glyph check không có trong font ở màn legacy cần audit nguồn riêng; không xóa scene cũ hoặc thay số liệu trống bằng dữ liệu giả. Task filters cuộn ngang, selected contrast dark và body nhỏ còn cần cải thiện. Idempotency task/media, logout offline và route gating cần contract/backend review riêng. Không cam kết đã giải quyết tất cả lỗi.

### Nghiên cứu / nguồn

Tên thứ dùng dạng abbreviated theo [Unicode CLDR English](https://www.unicode.org/cldr/charts/48/summary/en.html), không narrow initials. [W3C non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) là tham chiếu ngưỡng3:1 cho control, không chứng nhận WCAG của app Unity. Font [Google Fonts Be Vietnam Pro](https://github.com/google/fonts/tree/main/ofl/bevietnampro) được nhúng, license tại Docs/BeVietnamPro-OFL.txt. Các trang/font đã được kiểm tra HTTP trực tiếp; search-service lỗi/không trả nội dung không được dùng làm bằng chứng.

## F. Project memory

File riêng: `C:\Users\Windows\.codex\visualizations\2026\09\25\01a0d680-32c9-7860-b7b1-61b2c45328c7\tính tình của cậu chủ.md`, 24 quy tắc. Nguồn: conversation cung cấp, AGENTS/CODEX_CONTEXT/workflow/docs và lịch sử Git truy cập được. Không đọc được toàn bộ chat ngoài context, không suy tính cách đời tư; không commit/push file này.

## G–H. Đóng gói và tiếp theo

APK: `E:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse.apk`. Kết quả mới nhất trong Logs/ProductWorkspace/AndroidBuild.txt; metadata cuối được ghi ở RELEASE.md. APK trước giữ nguyên. Không coi callback CLI timeout5s là kết quả build; kiểm tra BuildReport/timestamp/file/hash mới.

GitHub: branch `codex/jarvis-uiux-audit` trong repo warehouse-ai; chỉ stage snapshot Unity dependency/source/docs, không backend dirty, APK/cache/Recovery/LocalFigmaConnection/private memory. SHA/push cuối ở RELEASE.md.

Snapshot không copy Figma binary/recovery scenes/XR Temp/fallback glyph cache hoặc ProjectSettings có thay đổi ngoài phạm vi. Giữ project settings hiện hữu trong snapshot. Nguồn chính vẫn ở thư mục cũ; Git repo nguồn không reset/commit thay đổi khác. Build dùng QueueAndroidBuild để trả CLI trước khi vào BuildPipeline; không polling Pipeline trong lúc build, vì lỗi timeout5s của request có thể làm StrictMode thất bại. Cảnh báo XR simulation duplicate Temp được ghi riêng, không xóa asset có references để ép build.

Việc cần người dùng: acceptance trên Samsung với camera/mic/keyboard và tác vụ picking thật. Việc kỹ thuật tiếp: hoàn thiện auth recovery ở Map, audit các route legacy/placeholder để tránh dùng nhầm, nối write/print capabilities theo contract đã duyệt và test idempotency. Không đưa đơn hàng/returns/finance vào mobile khi chưa được duyệt.
