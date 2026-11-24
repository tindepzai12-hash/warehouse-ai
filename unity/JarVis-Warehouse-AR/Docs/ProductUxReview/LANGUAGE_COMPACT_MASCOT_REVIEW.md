# JarVis — English, compact form and mascot tap

Ngày 13/10/2026. Chỉ sửa presentation và focused Editor QA trong dự án Unity hiện có. Không rebuild/import scene Figma, reset dự án, commit/push, thay database hoặc gửi mã nguồn ra ngoài.

## File thay đổi

| File | Nội dung |
| --- | --- |
| `Assets/JarVis/Scripts/UI/WarehouseProductWorkspace.cs` | Dịch display của ngành, nhóm, mẫu, thuộc tính, enum và đơn vị; tìm mẫu bằng English; lỗi theo locale. Một hàng sản phẩm 88px thay hai khối tổng khoảng 154px; nút tìm nằm trong input; số đo và đơn vị cùng hàng. Thu gọn trường tùy chọn, mã và quy cách; giữ scroll khi mở/đóng; giữ bản nháp khi đổi tab/kiểm tra hàng tương tự. |
| `Assets/JarVis/Scripts/UI/PrototypeMascot.cs` | Hit target 84×82 và pointer click thật; vui ngay khi chạm, kéo dài 1.6 giây; chạm lại khởi động lại phản hồi; giảm chuyển động vẫn cười nhưng không nhảy/spark. Giữ artwork đã duyệt và PreviewTime/ambient loop. |
| `Assets/JarVis/Scripts/UI/ImportedHomeDashboard.cs` | English: `1 consecutive active day`, nhiều ngày vẫn `days`. |
| `Assets/Editor/ProjectBootstrap/ProductWorkspaceReview.cs` | QA English, bảo toàn dữ liệu/canonical enum, compact layout, raycast mascot, giảm chuyển động. Runner không đóng Editor tương tác; từ chối chạy khi scene chưa lưu hoặc đang Play. |
| `Tools/imported-translations.tsv` | Nguồn dịch chung; bổ sung schema và thông báo, phân biệt `Gói`/`gói` theo lookup case-sensitive. |
| `Assets/Resources/JarVisImportedTranslations.json` | Sinh từ nguồn dịch bằng script sẵn có; 628 entries. |
| `Docs/APP_TRANSLATIONS_VI_EN.md` | Bảng dịch sinh cùng JSON. |
| `CODEX_CONTEXT.md` và tài liệu review này | Bàn giao trạng thái, kiểm thử và giới hạn. |

## Phân biệt bản dịch và dữ liệu

- Chỉ dịch interface/schema display. Template IDs, enum values, mã SKU/barcode và số đo không đổi.
- Bản nháp mới chọn mẫu khi English được gợi ý tên English. Tên đã nhập/bản nháp đã có giữ nguyên khi đổi ngôn ngữ, không dịch âm thầm. Tên, đơn vị và nội dung trả từ API cũng giữ nguyên.
- Màu và size bắt buộc vẫn hiện ngay. Hương vị/mức đường hoặc thông số tùy chọn trống nằm trong Additional information; giá trị đã nhập vẫn hiện. Validation mở thông tin bổ sung để không khóa người dùng bởi trường bị ẩn.
- Search template đang có từ khóa hiển thị nhóm như heading, không có nút Collapse vô tác dụng. Điều hướng vẫn dùng route `5:450`.

## Kiểm thử đã chạy

- Unity connected Editor `6000.4.7f1`: compile native UI/mascot và compile sau AI review đều thành công; CLI báo 0 errors, 0 warnings. Kết quả tại `Logs/ProductLanguageMascotCompile.json`, `ProductLanguageMascotQaCompile.json`, `ProductLanguageMascotFinalCompile.json`.
- Lần batch compile đầu không chạy được vì project đang mở; `Logs/ProductLanguageCompile.log` là lần thử không thành công, không dùng làm bằng chứng compile. Sau đó chuyển sang Unity CLI compile trực tiếp, không đóng Editor/mất scene chưa lưu.
- `ProjectBootstrap.ProductWorkspaceReview.Run`: **69/69 PASS**, `Logs/ProductWorkspace/Result.txt`, timestamp `2026-10-12T17:45:35.1717596Z` (00:45 ngày 13/10 Việt Nam). Fixture chỉ trong Editor, không đăng nhập/gọi model/mutate database/in thật. Kiểm tra thêm xác nhận danh sách sản phẩm tương tự hiện cả hai biến thể, Back quay đúng form/giữ số đo; chạm một biến thể để xem tem vẫn có đường quay về nháp rõ ràng, không chuyển mất luồng sang danh sách Labels.
- QA xác nhận tất cả schema captions/options có English, English search tìm đúng ID, màu Black lưu canonical Đen, tên người dùng/API không bị dịch, singular day, tap đúng first raycast target và vui ngay trong cùng frame, chạm lại, static smile/reduced motion, kết thúc phản hồi.
- `node Tools/audit-imported-localization.cjs`: 212 unique imported labels, 628 entries, untranslated `[]`.
- `node --test Docs/ProductFormMockup/catalog-schema.test.cjs`: **7/7 PASS**. Schema nguồn 5 ngành/14 nhóm/134 mẫu không đổi.
- Đã xem ảnh native English 412×915 và 390×844, light/dark, form/xác nhận/áo thun và Home happy. Ảnh tại `Logs/ProductWorkspace/*English*.png`.
- Một AI reviewer độc lập tìm lỗi toggle mất scroll, validation ẩn và notice chưa dịch; đã sửa. Đây là đánh giá AI + kiểm thử Editor, không thay thế nghiên cứu usability với nhân viên thật.
- Review cuối phát hiện CTA kiểm tra hàng tương tự chỉ tìm SKU đầu tiên. Đã thay bằng trang kiểm tra rõ ràng hiện toàn bộ tập tương tự (tối đa60, có báo nếu nhiều hơn), giữ nháp/scroll và nút tiếp tục; không thêm filter ngầm vào ô tìm kiếm.
- Đã giữ riêng callback quay về nháp qua bước xem tem của sản phẩm tương tự. Chọn tab khác chủ động hoặc đổi tài khoản/endpoint xóa callback cũ; xem tem thông thường vẫn quay về danh sách tìm kiếm như trước.

## Giới hạn và TODO

- Tạo sản phẩm chỉ là bản nháp trong phiên. Chưa lưu OMS/cấp SKU thật; chưa encoder/máy in/history API. Không biến các hành động này thành success giả.
- Form vẫn scroll được; không cam kết mọi mẫu đều chỉ một màn hình. Người mới cần thử bằng tác vụ thực tế trên Samsung trước khi kết luận dễ dùng.
- JarVis encouragement chạm được khi Home online sử dụng được; không mở khóa nghiệp vụ khi đang offline.
- TODO: thử English/VI, keyboard, text size và thao tác chạm/scroll trên Samsung; sau đó nối các capability OMS/in thật khi có contract và quyền backend.

## Android artifact

APK đích: `E:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse.apk`.
Kết quả build hiện hành đọc tại `Logs/ProductWorkspace/AndroidBuild.txt`; command connected Editor tại `Logs/ProductLanguageMascotBuildCommand.json`. Không chạy AndroidProjectConfigurator.ApplySettings, không đổi scene/settings để build, không cài Samsung tự động.

Build cuối **Succeeded**, timestamp `2026-10-12T17:46:57.5311662Z` (00:46:57 ngày 13/10 Việt Nam). APK thực tế **49,504,383 bytes / 47.21 MiB**; `BuildReport.totalSize` không phải kích thước APK nén. SHA-256: `680FCD60CD0A556D1765FAFCE15C6D6480B94647E8BD25BF6FC0DC234FFE45A6`. APK trước vẫn được giữ tại `Builds/Android/JarVis-Warehouse.previous.apk`.
