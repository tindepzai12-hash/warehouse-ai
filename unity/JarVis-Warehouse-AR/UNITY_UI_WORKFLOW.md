# Sửa giao diện JarVis trong Unity

## Sản phẩm và tem — 2026-10-12
Owner mới trên route `5:450`: `WarehouseProductWorkspace`, đăng ký bởi `PrototypeWorkspace`. Prototype web đã được duyệt để đưa UI vào Unity sau novice review. Dữ liệu ngành/mẫu: `Assets/Resources/JarVisProductTemplates.json`, 134 mẫu từ schema cũ. Giữ scene và navigation gốc; không chạy Figma builder. Kiểm tra tập trung: `ProjectBootstrap.ProductWorkspaceReview.Run` → `Logs/ProductWorkspace`. Runtime không có fixture hoặc quyền demo; tạo hàng chỉ session draft, API tạo SKU/in/history/quản lý chưa nối. Xem `Docs/ProductUxReview/NOVICE_REVIEW.md` trước khi nâng cấp tiếp.

## Quy trình hiện tại — đọc phần này trước

**Scene app chính:** `Assets/Scenes/JarVisFigmaApp.unity`. Các hướng dẫn WarehouseScene/InventoryPreview bên dưới là luồng cũ hoặc preview riêng, không phải entry hiện tại. Không đọc lại toàn bộ lịch sử chat để xác định scene.

**Thiết kế hiện tại:** prototype B tại `E:\JarVis-LiquidGlass-Prototype`, theo master prompt ngày 2026-10-07. `PrototypeUi`, `PrototypeWorkspace`, `PrototypeDialog` và các presentation component thay phần hiển thị trong Play, giữ controller/API cũ. Đọc `Docs/PrototypeParity/LAYOUT_CONTRACT.md` và `VERIFICATION.md`; dùng `ProjectBootstrap.PrototypeParityReview.Run` cho ảnh/check hiện tại. Các checker Figma đời cũ có thể còn assert kích thước/header/toolbar đã được chủ dự án thay đổi; không sửa UI ngược lại chỉ để thỏa checker cũ. Đổi SF Pro Display và nâng cấp nghiệp vụ được yêu cầu làm sau đợt sửa UI này.

1. Đọc `AGENTS.md`, `CODEX_CONTEXT.md` và chọn hàng tương ứng trong bảng dưới. Chỉ mở code của chức năng cần sửa; dùng `rg` để tìm tham chiếu khi cần. Kiểm tra `git status --short` để giữ các thay đổi có sẵn.
2. Xác định một lỗi hoặc flow và tiêu chí hoàn thành: hiển thị đúng, nhận thao tác, gọi đúng API, xử lý trống/lỗi. Sửa trong owner hiện có; không tạo controller hoặc backend song song.
3. Nếu chỉ đổi logic C#: để Unity compile, **không import Figma hoặc Build scene lại**. Chỉ dùng `CdmFigmaAppBuilder.Build()` khi thay cấu trúc/layout sinh bởi builder hoặc cập nhật nguồn Figma. Builder tạo lại scene, vì vậy giữ bản sao các chỉnh sửa scene bằng tay trước khi dùng.
4. Chạy kiểm tra nhỏ nhất phù hợp trong bảng; chỉ chạy toàn bộ pointer/validator khi đổi điều hướng chung, contract chung hoặc phát hiện ảnh hưởng nhiều module. Không chạy lại bài đã qua nếu code không đổi và không có bằng chứng lỗi mới.
5. Đọc kết quả PASS/FAIL và chỉ lấy dòng Console lỗi liên quan; xem ảnh của màn bị sửa. Compile/callback/giải mã ảnh mã không chứng minh camera Android, đăng nhập hay AI thực đã chạy.
6. Dọn sau khi kiểm tra: bỏ import/code không dùng đã xác nhận; bỏ nút giả không có khả năng thực hiện; không xóa asset chỉ vì tên cũ. Khi xóa script/asset, tìm cả GUID trong scene/prefab và tham chiếu code, xử lý `.meta` cùng asset. Giữ bản Figma nguồn, thư viện giải mã và scene legacy còn được tham chiếu.
7. Báo ngắn: đã sửa gì, kiểm tra gì, phần chưa xác nhận. Cập nhật bảng owner/trạng thái ở đây khi architecture thay đổi; không tạo một handoff hoặc báo cáo dài cho từng sửa nhỏ.

| Cần sửa | Owner / nguồn | Kiểm tra tối thiểu |
|---|---|---|
| Layout import, cuộn, thanh QR | `Assets/Editor/ProjectBootstrap/CdmFigmaAppBuilder.cs`; geometry và `.figma` trong `Assets/JarVis/DesignImports` | Chỉ rebuild khi layout đổi; kiểm tra top/bottom của màn bị ảnh hưởng |
| Đường đi giữa màn, Login/Logout, tab dưới | `Assets/JarVis/Scripts/UI/ImportedFigmaNavigation.cs` dùng một footer Home import chung (`BottomNavigation`); không bật footer riêng từng màn | `ImportedUiReview.Run` so tọa độ pixel toàn bộ tab qua 22 màn; Login thật cần OMS backend |
| Co đều 412×915 | `Assets/JarVis/Scripts/UI/ImportedFigmaViewport.cs` | 412×915, 1080×2400, 1440×3200; kiểm tra riêng safe area trên máy thật |
| Camera QR/barcode | `Assets/JarVis/Scripts/UI/ImportedCameraScanner.cs`; `Assets/JarVis/Plugins/ZXing` | `ScannerUiVerification.Run`; Android: cấp/từ chối quyền, quét mã, rời/mở lại camera |
| Chất lượng viền, hình tròn, pill trên 22 màn | `ImportedUiQuality.cs`, `Resources/JarVisImportedVisuals.json`; import 3×/rect 128 px | `ImportedUiReview.Run`: chụp 22 màn, raycast + synthetic key events của 3 input đã có. Không chứng minh mọi input tĩnh đã thành chức năng |
| Contract/auth/tồn kho thật | `Assets/JarVis/Scripts/Backend/OmsMobileApi.cs` và `E:/warehouse-ai/oms/routes.js`, `store.js` | API test tương ứng; app đọc dữ liệu thật; không dùng sample để báo thành công |
| Online/Offline, tổng thời gian trong ngày | `ImportedPresence.cs`, `OmsMobileApi.cs`; backend `oms/presence.js`, `oms_online_sessions` | `tests/oms-presence.test.cjs`; `Library/JarVisReviewPresence.request` với giá trị `focused` kiểm tra Home/login/bật/tắt/logout không gọi model |
| Home màu sắc đã duyệt, streak và phím tắt | `ImportedHomeDashboard.cs` giữ footer chung; `ImportedPresence` sở hữu thẻ thời gian; `oms/presence.js` trả streak từ phiên Online lưu bền | `node --test --test-isolation=none tests/oms-streak.test.cjs tests/oms-presence.test.cjs`; `ProjectBootstrap.ImportedHomeReview.Run` kiểm tra VI/EN, sáng/tối, chữ vừa/lớn và cuộn; `RunLive` kiểm tra API presence thật, không gọi model |

Home mới dùng cùng font Be Vietnam Pro (`boldSpacing=0`) và căn chữ giữa trong các thẻ, không kế thừa tracking chữ đậm 7 từ asset cũ. Ngoại tuyến `ImportedOfflineBlur` chụp chính `ImportedHomeDashboard` trong chế độ backdrop preview, không gọi API tải nhiệm vụ khi chụp và không giữ ảnh nền legacy. Kiểm tra tập trung trong Editor đang mở bằng `Library/JarVisReviewHome.request` (nội dung `live` để kiểm tra presence thật). Kết quả `Logs/HomeReview/Result.txt`; ảnh `Logs/ComposerLocalizationReview/Home-*`. Thanh điều hướng dưới giữ nguyên.
| Danh sách nhiệm vụ, bộ lọc, chi tiết, tra cứu tồn kho | `ImportedWarehouseLists.cs`, primitive dùng chung `JarVisUi.cs`, màu `JarVisDesignTokens.cs` | `ImportedPolishReview` kiểm tra VI/EN, footer, chữ tràn; dữ liệu và cập nhật trạng thái dùng API OMS thật. Không seed kho để chụp ảnh; Android cần kiểm tra bàn phím/cuộn riêng |
| Bản đồ kho 2D và mốc QR | `WarehouseMapScreen.cs`, `WarehouseMapRoute.cs`, `OmsMobileApi.cs`, `ImportedCameraScanner.cs`; backend OMS `/warehouse-map`; web `/warehouse/map.html` | `WarehouseMapRouteReview.Run` và `WarehouseMapUiReview.Run` dùng fixture Editor; API test `tests/oms-warehouse-map.test.cjs`. Tuyến thực, camera Samsung, AR và nhập/công bố map thực địa chưa được chứng minh bằng fixture. |
| AI chat trong scene import | `ImportedJarvisChat.cs`, `OmsMobileApi.cs`; `oms/routes.js` mobile chat, `server.js` provider | `ImportedUiReview.Run` kiểm tra input/cuộn; `npm run verify`; `node scripts/verify-mobile-ai-live.cjs` chỉ khi cần một request OpenAI thật |
| Ảnh và voice JarVis | `ImportedChatMedia.cs` tích hợp ảnh/quyền/TTS; `VoiceRecorder`, `VoiceRecordingBar`, `VoiceMessageView`, `VoiceWaveform`, `VoicePlayback`, `VoiceMessage`; `OmsMobileApi.cs`; backend `oms/routes.js`, `chat-media.js`, `voice-upload.js`, `voice-receipts.js` | `ProjectBootstrap.VoiceMessageReview.Run` kiểm tra composer/audio fixture không gọi AI; test `oms-voice-workflow` + `oms-voice-receipts`; đọc `Docs/VOICE_MESSAGE_SYSTEM.md`. Mic/permission/STT/context thật phải kiểm tra Samsung riêng, không suy từ compile |

### Kiểm tra nhanh có sẵn

- Trong Unity, lưu scene rồi dừng Play: **Tools → JarVis → Verify Scanner and Settings**. Không dựng lại scene. Kiểm tra giải mã QR/Code128/EAN13 và raycast nút quét ở đầu/cuối Cài đặt. Kết quả: `Logs/ScannerUiVerification.txt`, `Settings-Top.png`, `Settings-Bottom.png`.
- Batch khi project chưa mở bằng Editor: dùng Unity 6000.4.7f1 với `-batchmode -quit -projectPath "E:\unity of teep\JarVis-Warehouse-AR" -executeMethod ProjectBootstrap.ScannerUiVerification.Run -logFile "E:\unity of teep\JarVis-Warehouse-AR\Logs\ScannerUiVerification.log"`.
- Cách ưu tiên khi chạy terminal: `& './Tools/Verify-JarvisUi.ps1'` từ project root. Script kiểm tra Editor đang mở, chạy test không rebuild và ghi `Logs/ScannerUiVerification.latest.json`. Chỉ công nhận PASS khi exit code bằng 0 và kết quả được sinh mới trong chính lượt chạy đó. Dùng `-Rebuild` chỉ khi layout/source thay đổi.
- Khi thực sự cần cập nhật scene sinh tự động: `ProjectBootstrap.ScannerUiVerification.RebuildAndRun`. Không dùng mặc định cho mỗi thay đổi scanner.
- Không mở hai Unity Editor cùng project. Không đóng/kill phiên có scene chưa lưu để chạy batch. Không in credentials, clipboard, `.env` hoặc log licensing để debug UI.
- Khi nguồn Figma thay đổi: chạy `node Tools/export-imported-ui-visuals.cjs` một lần để cập nhật bán kính/màu nền từ chính nguồn. C# chất lượng dùng chung dựng nền 9-slice có khử răng cưa, giữ bố cục, ảnh và icon nguồn. Không cần tải thêm package cho sửa viền.
- Kiểm tra đầy đủ UI mới: **Tools → JarVis → Review All Imported UI** (Play Mode). Ảnh 824×1830 và kết quả nằm ở `Logs/UiReview/`; review chuyển qua các màn rồi trở về Login. Ô Login dùng placeholder và text riêng, một Image nhận chạm trong viewport; không clone FigmaNode vào editable text để tránh trùng ID.

### Trạng thái cần nhớ

Đã có: 22 frame import, điều hướng, Login gọi OMS, camera decoder và nhập mã, tra tồn kho theo phiên backend, Settings giữ nút QR ở trước nội dung. Kiểm tra Editor của giải mã và Settings đã qua trước lượt rà soát; cập nhật kết quả mới trong file test.

Chưa xác nhận: camera Samsung, đăng nhập và lookup với account thật, APK/safe area. AI chat đã nối OpenAI qua phiên OMS; UI có nút gửi, cuộn, loading và giữ draft khi lỗi. Lịch sử AI giữ trong RAM theo phiên backend (12 tin gần nhất, hết hạn sau 1 giờ không hoạt động), không phải lịch sử lưu bền. Đã kiểm tra một câu 5S tiếng Việt qua OpenAI bằng DB tạm, không thay kho thật. Chưa hoàn thiện trong scene import: chat nhân viên, ghi nhập/xuất, in tem và các mục Settings ngoài tài khoản/ngôn ngữ/cỡ chữ/giao diện. Không gọi toàn app là hoàn tất. Không xóa service/asset legacy trước khi thay xong các flow này.

## Tham khảo luồng cũ: màn lô hàng riêng

Figma hàng dưới là bản thiết kế tham chiếu; không cần Publish để dùng trong Unity. Unity dùng uGUI, không dùng ảnh chụp toàn màn hình làm nút bấm.

## Màn Lô hàng đã đưa vào Unity

- Mở `Assets/Scenes/JarVisInventoryPreview.unity` để xem bố cục trắng/xanh ở Game View. Kiểm tra trước ở **1440 × 3200** (Samsung S20 Ultra, dọc), rồi ở **1080 × 2400** và 412 × 915. Figma là tham chiếu thị giác, không phải kích thước cố định của app.
- Sửa trực tiếp `Assets/Resources/JarVisInventoryPanel.prefab` trong Prefab Mode. Chữ, màu, kích thước, vị trí và các thẻ tĩnh nằm trong prefab; prefab này cũng được `WarehouseWorkspaceController` dùng ở màn kho thật.
- Số lô, số lượng và hạn dùng được `WarehouseInventoryScreen.cs` cập nhật từ `OmsMobileApi.GetInventory()` khi nhân viên mở màn. Preview hiển thị kho trống; số mẫu trong Figma không được đưa vào dữ liệu chạy.
- Nếu thay một Button/Input/Text bằng đối tượng mới, gán lại ô tham chiếu trong component `WarehouseInventoryScreen` ở Inspector. Menu ngữ cảnh **Repair Inventory View References** có thể tìm các tên cũ để sửa một lần; sau đó lưu prefab. Việc đổi tên một đối tượng đã được gán tham chiếu không làm mất liên kết.
- Menu **Tools → JarVis → Build Inventory Prefab and Preview** chỉ tạo asset nếu chưa có; không ghi đè prefab/scene đã chỉnh tay.

## Luồng dữ liệu và kiểm tra

`oms/store.js` (SQLite) → `oms/routes.js` (`/api/oms/mobile/inventory`) → `OmsMobileApi.cs` → `WarehouseInventoryScreen.cs` → prefab. Admin nhập dữ liệu trên web; app không lưu OpenAI key hay database. Khi sửa DTO, cập nhật API và Unity cùng một lượt.

Sau khi sửa UI, mở `WarehouseScene` ở 1440 × 3200 để kiểm tra cả thanh điều hướng hai hàng, vùng camera/thanh hệ thống, nút chạm, bàn phím đăng nhập, cuộn danh sách lô và nội dung chat dài. Các màn chính dùng `SafeAreaPanel`; thanh điều hướng do `WarehouseWorkspaceController` sở hữu để các nút không đổi chỗ khi chuyển màn. Màn lô hàng giữ thanh riêng chỉ trong scene preview. Chạy `ProjectBootstrap.JarvisProjectValidator.Validate` trong Unity; kiểm tra tiếp trên Samsung S20 Ultra bằng tài khoản nhân viên và API thực. Preview scene không thuộc Build Settings. Trên Android thật cần backend HTTPS mà thiết bị truy cập được; `localhost` của PC không dùng trực tiếp trên điện thoại.

Admin có thể đăng nhập app bằng tên admin để dùng AI; phiên admin mobile được /me, /logout, /jarvis/chat, /jarvis/transcribe, /jarvis/speech. Quản lý dữ liệu trên web. Editor-only có thể dùng JarvisApiClient qua token preview staff trong Library (không phải OpenAI key); bản build cần phiên OMS thật. Chat có ba chấm suy nghĩ và pulse avatar; không nối hoạt họa vào vị trí thanh tab. Kiểm tra đăng nhập admin → raycast Send → câu trả lời OpenAI thực: đã qua 2026-10-06. Khi cần chạy lại live integration, tạo Library/JarVisVerifyLiveChat.request trước Review All Imported UI; bài này đọc credentials admin cục bộ, không ghi vào Assets/log và có 1 request OpenAI thật.

### JarVis media và ngôn ngữ

- Thanh chat dùng icon vector `ChatActionIcon`: ảnh / micro / loa ON-OFF / gửi, cùng một hàng compact với ô nhập co giãn. Thanh tab và gợi ý ẩn khi nhập, hiện lại khi đóng bàn phím; ảnh đính kèm giữ preview và nút bỏ ảnh. Android dùng vùng bàn phím thực qua `ChatPhotoPicker.keyboardHeight` khi Unity không trả diện tích IME. Cần thử Samsung thật để xác nhận bàn phím và safe area.
- VI/EN tại Login nối `ImportedAppLocalization` với `JarvisLocalization`, lưu lựa chọn trên máy. Không sửa node ID, tên layer, route, SKU, tên hàng thật, nội dung người dùng hoặc câu trả lời AI. Bảng nguồn duy nhất: `Tools/imported-translations.tsv`; chạy `node Tools/build-imported-translations.cjs` để cập nhật JSON trong Resources và `Docs/APP_TRANSLATIONS_VI_EN.md`. Kiểm tra thiếu dịch bằng `node Tools/audit-imported-localization.cjs`.
- **Tools → JarVis → Review Composer and Languages** kiểm tra scene hiện có, 22 trang, đổi ngôn ngữ từ Login, giữ draft/password, icon mesh, hit area, mũi tên và ảnh. Không đọc credentials, không gửi OpenAI. Kết quả và ảnh ở `Logs/ComposerLocalizationReview`. Batch khi Editor đóng: Unity `-batchmode -projectPath ... -executeMethod ProjectBootstrap.ImportedComposerLocalizationReview.Run -logFile ...`; không thêm `-quit` vì bài chạy qua Play Mode rồi tự thoát.

- Hỏi tiếng Anh → trả lời tiếng Anh; hỏi tiếng Việt → trả lời tiếng Việt. Tự chọn theo câu hỏi mới nhất, giữ ngôn ngữ trước cho mã/câu mơ hồ; yêu cầu dịch rõ ràng được ưu tiên. Ngôn ngữ UI không quyết định ngôn ngữ câu trả lời.
- Chọn ảnh → xem preview/Bỏ ảnh → nhập câu hỏi (không bắt buộc) → Gửi. Một JPEG/PNG, được thu nhỏ cạnh dài tối đa 1024px, JPEG ≤2 MiB. Android dùng ACTION_OPEN_DOCUMENT, không xin quyền truy cập toàn bộ thư viện. Ảnh chỉ ở RAM, không lưu trong database hay lịch sử model; hỏi tiếp về ảnh cần đính kèm lại.
- Master prompt voice 2026-10-07 thay thế luồng draft cũ: micro → quyền thiết bị → nói tối đa 30 giây → chạm micro lần nữa → WAV mono PCM16 → multipart `/api/oms/mobile/jarvis/voice` → OpenAI STT + cùng lịch sử chat JarVis → một tin người dùng và một phản hồi AI. Giữ riêng chữ nháp đang nhập. Chỉ dùng mic sau thao tác, hủy khi rời màn/app vào nền.
- Loa là công tắc ON/OFF lưu cục bộ, mặc định OFF. ON tự đọc phản hồi mới qua backend tts-1/nova (tối đa 4000 ký tự); OFF dừng tải/phát. Một AudioSource, không phát chồng. Câu trả lời chữ vẫn giữ khi TTS lỗi. Thanh nhập dùng các icon ảnh/micro/loa nhỏ cùng một HorizontalLayoutGroup với ô nhập co giãn và nút gửi; menu plus cũ chỉ còn đường tương thích ẩn.
- Admin mobile được thêm /jarvis/transcribe và /jarvis/speech, vẫn không có quyền quản lý kho trong app. API key luôn ở server; các endpoint media dùng giới hạn lượt AI chung theo tài khoản. Lỗi giữ chữ/ảnh để thử lại.
- Kiểm tra Java bằng javac/Android SDK chỉ xác nhận biên dịch; micro, permission, EXIF và trình chọn ảnh phải kiểm tra trên Samsung thật. Không báo APK hoạt động từ kết quả Editor.


## Login caret và thông báo nhỏ
- `ImportedInputCaretRepair` giữ TMP caret cùng parent/RectTransform với editable text sau khi viewport được chuyển. Không sửa bằng offset cứng.
- `ImportedSnackbar` hiển thị một thanh thông báo trong viewport logic, tự ẩn, nút đóng 48px; không chặn cả màn. `ImportedFigmaNavigation.Notice` là điểm gọi chung; lỗi đăng nhập được chuyển sang thông báo VI/EN dễ hiểu, không đưa lỗi mạng kỹ thuật lên form.
- Kiểm tra nhỏ: Tools → JarVis → Review Login Caret and Notifications; hoặc tạo `Library/JarVisReviewLoginFeedback.request` trong Editor hiện có. Kết quả/ảnh ở `Logs/ComposerLocalizationReview/LoginFeedback.txt`, `Login-Caret-Fixed.png`, `Login-Error-Snackbar.png`. Không gọi OpenAI, không dựng lại scene. Android keyboard cần kiểm tra trên máy thật.

## UI polish từ bản Figma đã import
- `ImportedUiPolish` giữ bố cục Figma, căn khung nhãn/icon/thẻ; `ImportedUtilityIcon` dựng các icon giao diện bằng mesh, giữ nguyên ảnh thương hiệu. Footer dùng cùng trục giữa cho icon và chữ.
- `ImportedPressFeedback` đổi opacity khi chạm, không dịch RectTransform. Settings đổi ngôn ngữ có callback thật; chế độ tối có công tắc và lưu lựa chọn trên thiết bị.
- Kiểm tra UI không dùng model/API: Tools → JarVis → Review UI Polish hoặc `Library/JarVisReviewPolish.request`. Chụp 22 màn ở VI/EN vào `Logs/ComposerLocalizationReview/Polish-*`; xem `Polish.txt` và các nhãn bị cắt trước khi báo hoàn tất. Không rebuild/import toàn scene để sửa runtime UI.

## Cài đặt tài khoản và giao diện
- `ImportedSettings` bổ sung các màn đổi mật khẩu, chọn VI/EN, cỡ chữ Nhỏ/Vừa/Lớn và Sáng/Tối/Theo thiết bị; lưu lựa chọn giao diện bằng PlayerPrefs. Android đọc uiMode cho chế độ theo thiết bị, Editor dùng sáng.
- Đổi mật khẩu gọi `OmsMobileApi.ChangePassword` → `/api/oms/mobile/account/password` → `OmsStore.changeOwnPassword`; xác thực mật khẩu hiện tại, mật khẩu mới 12–256 ký tự, cập nhật hash trong transaction và hủy tất cả phiên web/mobile. Không đổi tài khoản admin trong kiểm tra UI.
- Màu nền import lấy từ `Tools/build-imported-theme.cjs` → `Resources/JarVisThemeSurfaces.json`; không tint ảnh thương hiệu/chân dung. Font scale do `ImportedAppLocalization` sở hữu; chat tính lại chiều cao bubble khi đổi cỡ chữ.
- Kiểm tra tập trung: `Library/JarVisReviewSettings.request`; kết quả/ảnh trong `Logs/ComposerLocalizationReview/Settings*`. Backend dùng database tạm qua `tests/oms-password.test.cjs`; không gọi model. Android bàn phím và theme hệ thống cần kiểm tra máy thật.

## Viền, cỡ chữ và hiệu ứng kính trong uGUI
- `UniformUiSurface` + `UniformSurfaceUv` + shader `Resources/JarVisShaders/UniformSurface` dựng viền theo kích thước rect, không phụ thuộc UV ảnh import/9-slice. Giữ một owner màu; `ImportedSettings.ApplyTheme` bỏ qua các surface này. Không đổi màu ảnh thương hiệu.
- Cỡ chữ dùng Slider số nguyên 0/1/2, lưu Nhỏ/Vừa/Lớn; `TextSizeSliderMotion` đồng bộ vị trí và phần track đã chọn. Công tắc tối riêng có hit area 60×48 và chuyển động `ThemeSwitchMotion`. Kính là hiệu ứng uGUI với nền blur chụp khi mở màn, không phải vật liệu Liquid Glass native Apple.
- `ImportedOfflineBlur` chụp/blur nội dung bên dưới thông báo khóa; chữ và nút bật trực tuyến giữ rõ. `ScannerSweep` là chỉ dẫn quét đỏ chuyển động, không phải trạng thái đã nhận mã.
- `ImportedSettingsReview` kiểm tra VI/EN riêng, kéo qua 3 nấc, form/password/theme và chụp Login/Offline/Sign out trong scene hiện có. Không gọi OpenAI hoặc thay mật khẩu; hiệu năng shader và bàn phím cần xác nhận trên Samsung.


## Thanh dưới, bộ lọc nhiệm vụ và danh tính hiển thị
- `ImportedUiPolish.Footer` chia Home/Tasks/Scan/Map/JarVis theo 5 vị trí đều nhau, mỗi tab giữ icon và nhãn cùng trục. `ImportedUtilityIcon` có biểu tượng thông báo; nút hiện thông báo kho trống khi chưa có dữ liệu.
- `ImportedTaskFilterMotion` + `TaskChipSurface` sở hữu màu chọn và nhún 240 ms khi bấm; chỉ một mục xanh, giữ cuộn ngang hiện có. Không ghi thêm nhiệm vụ mẫu.
- `UserPortraitGraphic` dùng ảnh được duyệt trong `Resources/JarVisUserJustin.png`, clip tròn bằng mesh với UV giữ tỉ lệ ảnh. Chỉ dùng cho avatar người dùng Home/Settings; không thay mascot JarVis hoặc avatar nhân viên khác. Tên hiển thị Justin là cấu hình giao diện theo yêu cầu, không đổi hồ sơ/auth backend.
- Kiểm tra tập trung có thêm `Home-Justin-Footer.png`, `Tasks-All-Fixed.png`, `Tasks-Working-Fixed.png`; kiểm tra chỉ một tab xanh. Khi đổi theme phải giữ màu chữ gốc cả màn chưa hoạt động, không thu màu chữ đã đổi ở theme tối làm baseline sáng.
