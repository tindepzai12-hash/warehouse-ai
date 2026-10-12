# Review nhân viên mới và native integration — 12/10/2026

Người dùng đã duyệt việc review AI, sửa workflow rồi đưa vào JarVis Unity. Approval này thay gate “mockup only” trước đó cho phần UI liên quan, không phải giấy phép tạo dữ liệu production/đóng vai quản lý/in giả.

## AI review thật sự đã làm gì?

Một agent riêng (Mendel) chỉ đọc app.js + REVIEW.md, không gọi mạng hoặc sửa file. Agent đóng vai nhân viên mới với ba công việc: tạo sữa180ml, tìm hàng để in, thay tem rách. Không phải phiên thử với con người, không có chứng cứ thời gian hoàn thành hoặc bảo đảm người mới nào cũng dễ dùng.

| Vấn đề AI tìm thấy | Sửa và bằng chứng |
|---|---|
| Có thể tạo trùng sữa đang có bằng mã tự cấp | Gợi ý sản phẩm tương tự trước form; nút dùng hàng cũ để xem/in tem. Mã trùng vẫn bị chặn; similarity là cảnh báo, không khẳng định hai tên giống nhau chắc chắn cùng SKU |
| Nhập “sữa180ml” không ra “Sữa tươi 180 ml” | Search bỏ dấu/khoảng trắng và match token chữ/số; test query gộp tìm được sữa dù có từ “tươi” |
| Điền ví dụ ghi đè dữ liệu vừa nhập | Hỏi xác nhận trước overwrite; hủy giữ giá trị390ml. Native không có auto-fill dữ liệu ví dụ |
| In tem và thay tem rách khó phân biệt | Đổi nhãn “In thêm tem” / “Thay tem rách / mất”, giải thích giữ mã gốc. Native printing chưa có capability nên disabled, không fake log |
| Chọn60×40 nhưng caption cố định50×30 | Caption cập nhật theo khổ đã chọn, nói rõ preview không mô phỏng tỉ lệ in vật lý |

Vòng1: independent AI code walkthrough. Vòng2: sửa + browser flow test. Vòng3: native functional runner + nhìn ảnh412/390; gộp lựa chọn thuộc tính thành một control, danh sách nhóm mẫu thu gọn, rút ngắn hàng xác nhận. Review native độc lập lần cuối (Hubble) tìm thêm lifecycle loading bị kẹt, mất vị trí sau choice và thiếu exact-code priority/barcode trong kết quả. Đã sửa, thêm test cancellation/loading, return scroll, exact rank; dữ liệu/draft clear khi endpoint/account/auth state đổi, callback stale bị bỏ. Không tiếp tục vòng tinh chỉnh thẩm mỹ vô hạn. Vẫn cần test người mới thật trên Samsung, nhất là keyboard/scroll và đọc dưới ánh sáng kho.

## Unity đã đưa vào những phần nào?

- Route cũ5:450, mở từ Công cụ → **Sản phẩm & tem**, sử dụng native uGUI, header/dock/theme/font/caret của JarVis. Không WebView, không tạo project/scene mới.
- Ba mục Sản phẩm / Tem sản phẩm / Quản lý, ngành và mẫu chung màn, toàn bộ134 mẫu gốc, nhóm thu gọn + search, manual fallback.
- Bản nháp ba bước; field động required, supplemental, brand, packaging/unit, SKU/manual source, barcode/package source, chính sách HSD theo lô. Back/template revisit giữ nháp trong phiên; đóng app sẽ mất (ghi rõ).
- Search/dedup product từ tồn kho API hiện có; không đưa fixture hoặc mẫu demo vào production. Mã gốc giữ nguyên. Duplicate/manual SKU/barcode và positive measurement, integer packaging, check digit được kiểm tra local; chưa thay server uniqueness.
- Không đổi inbound/outbound/assistant/map/auth/DB hoặc scene; không gộp nhận hàng/lot dates vào SKU.

## Phần chưa thể xem là đã nối thật

Mobile `OmsMobileApi` hiện chưa có contract tạo/sửa product, cấp SKU atomic, quyền quản lý catalog, lệnh printer/encoder và print history. Vì vậy:
- “Giữ bản nháp trong phiên” không nói đã lưu OMS; không tự sinh SKU/GTIN production.
- “In thêm/Thay tem” native disabled và giải thích chưa kết nối; không record lịch sử giả.
- Mục Quản lý không có role picker hoặc nút thay cấu hình thật; quyền phải được backend xác nhận.
- Custom attribute/propose/reuse management đầy đủ vẫn ở mockup, chưa port thành mobile capability. Form chính native đã vào, **không tuyên bố 100% feature parity với demo**.
- Tra cứu live API mới được wiring, chưa kiểm thử bằng tài khoản/thiết bị thật trong lượt này. QA dùng fixture chỉ trong Editor, không gọi backend hoặc model.

## Kiểm thử và log

- Browser final **80/80 PASS**, `QA_RESULTS.json`; bốn widths1440/768/390/360. Added cases: compact search, similar-item suggestion, overwrite confirm/cancel, paper caption. Schema **7/7 PASS**.
- Compile nhóm đầu phát hiện CS0103 do thiếu namespace Localization; đã sửa. Runner nhóm sau phát hiện dữ liệu AcceptInventory chưa render lại; đã sửa đúng owner (không bỏ check).
- Unity final runner `ProductWorkspaceReview`: UI/scene native,134templates, dedup across2lots, draft validation/back, native unit conversion, SKU duplicate, fractionalpack, checksum, targets≥44px, shared dock. Kết quả cuối trong `Logs/ProductWorkspace/Result.txt`; compile/runtime log `Logs/ProductWorkspaceFinalReview.log`.
- **40/40 native checks PASS** sau khi sửa lifecycle/scroll/exact-code priority/hardware Back. Không còn lỗi compile mới; warning cũ nêu riêng bên dưới.
- Captures `Products-412.png`, `Templates-412.png`, `Form-412.png`, `Confirm-412.png`, `Confirm-390.png`, `Form-dark-390.png` trong `Logs/ProductWorkspace`; QA fixtureQA-MILK không nhập DB/runtime resources.
- Pre-existing warnings: deprecated FindFirstObjectByType và analyzer assembly bị Windows Application Control chặn. Không mở rộng sửa dự án khác để xóa warning.
- Chưa chứng minh camera/scanner/printer, TalkBack/IME, công thái học Samsung, persistence offline draft hoặc live write API. Không đổi credentials, không upload source, không deploy.

## Files thay đổi

Runtime: `Assets/JarVis/Scripts/UI/WarehouseProductWorkspace.cs` (+meta), `Assets/JarVis/Scripts/UI/PrototypeWorkspace.cs`, `Assets/Resources/JarVisProductTemplates.json` (+meta).
Navigation: `ImportedFigmaNavigation.cs`, chỉ intercept Back cho route5:450 để quay lại lựa chọn/bước trước, không làm mất flow khi dùng phím Back. Danh sách có CTA tiếp tục bản nháp đã giữ trong phiên.
Verification: `Assets/Editor/ProjectBootstrap/ProductWorkspaceReview.cs` (+meta), generated log/captures.
Prototype: `Docs/ProductUxReview/app.js`, `Docs/ProductUxReview/review.cjs`, QA_RESULTS.json + screenshots.
Context/docs: CODEX_CONTEXT.md, UNITY_UI_WORKFLOW.md, UNITY_PLAN.md, NOVICE_REVIEW.md.

## TODO ưu tiên

1. Test ba công việc với một nhân viên mới thật; giữ hoặc sửa tên/field từ quan sát, không từ điểm AI.
2. Chốt contract mobile product/draft persistence theo tenant/account, server uniqueness/capability, mutation confirmation riêng.
3. Native custom attributes/template proposal sau khi có schema/version/permission; không nhận demo role là authority.
4. Encoder/printer real + quiet zone, payload/khổ tem, request ID/idempotency và immutable history. Chỉ báo đã in sau acknowledgement.
5. Compile Android/build acceptance khi feature contract phù hợp; APK không tự coi hoàn thiện in/tạo SKU chỉ vì build thành công.

## Đánh giá sau sửa

Đường đi đã rõ hơn cho người mới: tìm hàng cũ trước → xem mã gốc; hàng mới → chọn mẫu → thông tin → xác nhận bản nháp. Ví dụ không ghi đè âm thầm, lựa chọn không kéo về đầu form, Back không thoát nhầm module, có CTA tiếp tục nháp. Không đưa điểm “dễ dùng100%” hay bảo đảm lần đầu ai cũng hiểu. Các job cần tạo SKU thật/in thật vẫn bị chặn đúng vì contract chưa có; hoàn thành UI không đồng nghĩa hoàn thành workflow vận hành.

## Android build cuối — 13/10/2026 00:06 (Việt Nam)

**Succeeded**, process return0, `Logs/ProductWorkspaceAndroidFinalBuild.log` + `Logs/ProductWorkspace/AndroidBuild.txt`. APK mới nhất: `E:\unity of teep\JarVis-Warehouse-AR\Builds\Android\JarVis-Warehouse.apk`, **49,484,635 bytes** (~47.2MiB). BuildReport.totalSize là metric riêng của Unity, không phải kích thước APK nén; dùng FileInfo để báo size artifact. APK cũ11/10 được giữ tại `JarVis-Warehouse.previous.apk`, không ghi đè backup khi rebuild cuối. Không cài Samsung trong lượt này. Không chạy AndroidProjectConfigurator.ApplySettings hoặc tái tạo scene khi build.
