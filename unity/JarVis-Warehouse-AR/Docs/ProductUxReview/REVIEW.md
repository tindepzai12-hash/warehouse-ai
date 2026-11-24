# JarVis — báo cáo tái cấu trúc sản phẩm và tem

Đây là baseline mockup trước approval. Người dùng đã duyệt review AI + native UI integration sau báo cáo này; trạng thái mới nhất và giới hạn nằm trong NOVICE_REVIEW.md (12–13/10/2026). Các kết luận “chưa sửa Unity / chưa APK / awaiting approval” bên dưới là lịch sử của lượt mockup, không phải trạng thái hiện tại.

Ngày kiểm tra: 12/10/2026. Trạng thái: mockup độc lập, chờ chủ dự án duyệt.

## 1. Phạm vi và bằng chứng

- Prototype mới: `Docs/ProductUxReview`, preview **http://127.0.0.1:4198/**. Prototype cũ **http://127.0.0.1:4197/catalog.html** giữ nguyên.
- Đã đọc AGENTS, CODEX_CONTEXT, UNITY_UI_WORKFLOW, PRODUCT, DESIGN và prompt; đối chiếu catalog.html/js/schema, serve.cjs và checker cũ. Dữ liệu mẫu, điều hướng và validation trong catalog là bằng chứng từ code; ảnh Before-Desktop.png là trạng thái giao diện cũ.
- Unity đang có rất nhiều file modified/untracked từ các lượt trước. Không reset, xóa, commit, push hoặc sửa chúng. Không đổi Assets, Packages, ProjectSettings, backend hoặc DB.
- Catalog cũ là vanilla HTML/CSS/JS, không phải React. Backend và Unity có hợp đồng riêng, không suy rằng mockup đã nối thật.
- Schema cũ được server mới đọc nguyên trạng, client tạo bản sao cấu hình trong RAM. **5 ngành, 14 nhóm con, 134 mẫu, gồm 14 lựa chọn thủ công**; giữ ID và thuộc tính. Mọi ví dụ đều là dữ liệu mẫu, không gán thương hiệu tùy tiện.
- Skills dùng: Impeccable (Operate, new-work, craft-floor, context/detector) và frontend-design. QA dùng Playwright có sẵn, Edge headless. Không tạo ảnh AI hoặc gọi dịch vụ trả phí.

## 2. Nghiên cứu: nguồn đã đọc và giới hạn

| Nguồn | Kết quả truy cập | Điều hỗ trợ | Phạm vi áp dụng |
|---|---|---|---|
| [Odoo 19.0: Expiration dates](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/product_tracking/expiration_dates.html) | HTTP 200; đọc nội dung article ngày 12/10/2026 | Odoo gán thông tin hạn cho sản phẩm theo dõi bằng lot/serial; hạn cụ thể được đặt sau khi gán lot/serial | Quy tắc Odoo, hỗ trợ đề xuất tách chính sách SKU và hạn của lô; không phải quy định pháp luật cho mọi kho |
| [Odoo 19.0: Packaging](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/configure/packaging.html) | HTTP 200; đọc định nghĩa packaging | Packaging chứa nhiều đơn vị của một sản phẩm; cấu hình theo sản phẩm | Tham chiếu mô hình quy cách; không chứng minh mỗi kho bắt buộc tạo SKU mới cho mọi thùng |
| [W3C WAI: Labeling Controls](https://www.w3.org/WAI/tutorials/forms/labels/) | HTTP 200 | Label gắn với control; title không thay thế label | Hướng dẫn web accessibility; không chứng minh Unity native tương đương |
| [GS1: declared net content](https://www.gs1.org/1/gtinrules/en/rule/declared-net-content) | HTTP 403 | Chưa xác minh lại nội dung trong lượt này | Không coi URL truy cập thất bại là bằng chứng đã đọc |
| [GS1 GTIN management standard](https://standards.gs1.org/gtin-management-standard/) | Lỗi SSL | Chưa xác minh version / chính sách cụ thể | Cần kiểm tra nguồn GS1 trước triển khai barcode sản xuất |
| [Shopify: SKU](https://help.shopify.com/en/manual/products/details/sku) | HTTP 403 | Chưa xác minh lại trong lượt này | Không áp quy định Shopify thành bắt buộc chung |

Search connector không trả nội dung; sandbox ban đầu không phân giải DNS. Đã đọc các nguồn khả dụng qua yêu cầu GET tài liệu chính thức, không gửi mã nguồn hoặc ảnh. Không vô hiệu hóa SSL hay tìm cách vượt chặn. Phần nghiên cứu còn thiếu GS1/Shopify; không tuyên bố đã hoàn thành nghiên cứu tiêu chuẩn đầy đủ.

**Đề xuất thiết kế, không phải nghĩa vụ pháp luật:** ba bước, số tem giấy tách tồn kho, quyền in dựa trên capability, nhóm bổ sung, chuyển quy cách thành SKU riêng khi kho quản lý/lấy hàng độc lập. Check digit chỉ kiểm tra định dạng, không xác nhận quyền dùng GTIN. Mã MOCK không được gọi là GTIN hợp lệ.

## 3. Functional audit và Before / After

| Chức năng | Bản cũ: điều đã xác minh | Bản mới | Quyết định / lý do | Ảnh hưởng người dùng |
|---|---|---|---|---|
| Điều hướng | 4 mục: tạo, phục hồi, web danh mục, web in | 3 mục: Sản phẩm, Tem sản phẩm, Quản lý | Gộp theo công việc, không theo mobile/web | Không phải chọn nơi in theo thiết bị |
| Ngành và mẫu | Hai trạng thái chọn liên tiếp | Cùng màn hình, tìm trong ngành, nhóm con thu gọn | Gộp | Thấy ngữ cảnh và mẫu đang chọn |
| Tiến trình | 4 trạng thái nội dung; thanh chỉ 3 nhãn không khớp | Chọn mẫu → Điền thông tin → Xác nhận | Sửa | Không đoán bước còn lại |
| Tạo SKU | Tên, thuộc tính, quantity dù không nhận hàng | Tên, thuộc tính biến thể, đơn vị/quy cách, mã, chính sách hạn | Giữ riêng tạo sản phẩm; bỏ quantity khỏi SKU | Không nhầm lưu sản phẩm là nhập hàng |
| NSX/HSD/lô | Checkbox kèm nháp nhận hàng trong tạo sản phẩm | Không có ngày/lô trong form SKU; giải thích bước nhận hàng sau lưu | Luồng độc lập, không xóa nghiệp vụ nhận hàng | Một SKU có thể có nhiều lô với hạn khác nhau |
| Màu/size/model/định lượng | Form động theo schema | Giữ trên phần nhận diện, không đưa field required vào nâng cao | Giữ nguyên nghiệp vụ | Giày EU không lẫn size áo; nồi không dùng size quần áo |
| Brand/flavour/sugar/material/shade | Gợi ý theo schema | Giữ, không tự gán brand; điền ví dụ chỉ khi bấm | Giữ nhận diện; không mặc định mọi thuộc tính là bắt buộc pháp lý | Cần chủ kho quyết định thuộc tính phân biệt SKU |
| Kích thước/phụ trợ | Tùy theo mẫu và custom | Optional chiều dài/rộng/cao, power, configuration, ghi chú vào bổ sung; required vẫn trên cùng | Đưa vào nâng cao có điều kiện | Ít trường phụ, không mất thông số bắt buộc của mẫu |
| SKU/barcode | Nằm rời trong form, mã preview chưa là encoder thật | Một nhóm Mã sản phẩm; 2 nguồn SKU, 2 nguồn barcode; quét giả lập | Gộp trình bày, giữ riêng ý nghĩa | Thấy mã trùng, không silent overwrite |
| In / phục hồi | Recovery được dùng lại ở hai khu vực | Tìm → xem → in hoặc in lại trong Tem sản phẩm | Gộp entry point | Tem rách không tạo lại SKU |
| Mất bố cục / mất mã | Không có nhánh thể hiện đủ sự khác biệt | Có mã gốc: khôi phục bố cục; thiếu mã: chặn, cần xác minh | Giữ độc lập trạng thái dữ liệu | Không phát minh barcode rồi gọi là khôi phục |
| Số lượng in | Copies riêng ở recovery | Số tem giấy riêng trong dialog, 1–999; không cộng tồn | Giữ nguyên độc lập | 3 tem không có nghĩa 3 sản phẩm nhập |
| Lịch sử | RAM, table trong Web In tem | Tab Lịch sử dưới Tem sản phẩm; lọc/tìm/chi tiết success và fail | Gộp vị trí, giữ chức năng độc lập | Truy được SKU, mã gốc, số tem, lý do, khổ, vai trò demo, giờ |
| Vai trò | Dropdown trong form phục hồi | Công cụ demo ở header; form in không hỏi vai trò | Đưa khỏi form nghiệp vụ | Quyền thật phải từ đăng nhập/backend |
| Cấu hình mẫu/brand | Chỉnh option/brand trong RAM | Quản lý riêng: option, brand gợi ý, bảng required, đề xuất, dùng lại nháp | Giữ công cụ quản lý độc lập | Nhân viên không phải chỉnh schema để tạo hàng |
| Custom / đề xuất | Thuộc tính riêng và mẫu phiên | Nâng cao: chữ/số/số đo, chặn lô/HSD/NSX/tồn; đề xuất và dùng lại nháp | Giữ trong nâng cao | Luồng phổ thông không cần mở |

### Cần giữ là chức năng độc lập
Tạo/sửa nhận diện SKU; nhận hàng và lô; xem tem; lệnh in/in lại; lịch sử chi tiết; cấu hình mẫu; phân quyền thật. Chúng có thể chung một module nhưng không được nhập thành một nút làm mọi thứ.

### Những điểm chưa xác minh / không đưa vào demo
- In vật lý, encoder Code128/EAN, scanner camera thật, driver printer, file PNG/PDF barcode scan được.
- Trùng mã toàn kho, chủ hàng/tenant, multi-device SKU allocation và barcode mapping authoritative.
- Tìm hàng theo lô/vị trí có ở fixture recovery cũ. Bản này tìm tem theo tên/SKU/barcode; tra lô/vị trí cần nối từ nhận hàng/tồn kho, chưa mô phỏng tại đây. Không suy rằng production mất chức năng.
- Mẫu cũ có required do quyết định thiết kế, chưa có căn cứ bắt buộc cho mọi ngành. Giữ để duyệt; không đổi toàn bộ model thật.

## 4. Sitemap

```text
Sản phẩm
  Danh sách → Chi tiết → Xem tem
  Tạo sản phẩm
    Chọn mẫu: ngành + tìm mẫu + nhóm con + thủ công
    Điền thông tin: nhận diện + quy cách + mã + chính sách HSD
      Bổ sung: thông số phụ, ghi chú, custom, đề xuất mẫu
    Xác nhận: nguồn SKU/mã, nhận diện, chính sách hạn, bố cục tem
    Thành công: in tem / danh sách / tạo tiếp / giải thích nhận hàng
Tem sản phẩm
  Tìm → Xem tem
    In tem → số tem, khổ → mô phỏng kết quả
    Tem rách/mất → In lại mã cũ → lý do → mô phỏng kết quả
    Thiếu bố cục → khôi phục bố cục, không đổi mã
    Thiếu mã gốc → chặn, yêu cầu xác minh
  Lịch sử → tìm/lọc → chi tiết
Quản lý
  Mẫu → thuộc tính → thêm lựa chọn phiên
  Brand gợi ý → thêm gợi ý phiên
  Đề xuất → nháp chưa xuất bản → dùng lại bản nháp
Công cụ demo (ngoài nghiệp vụ)
  Vai trò, capability in giả lập, offline, lỗi lưu/in
```

## 5. Luồng A–E và ngoại lệ

| Luồng | Đường đi chính | Ngoại lệ / quay lại |
|---|---|---|
| A Tạo hàng | Sản phẩm → Tạo → ngành/mẫu → nhập → Xác nhận → Lưu demo → thành công | Mẫu không thấy → thủ công; field thiếu, mã trùng, số đo sai → field error/focus; quay lại giữ draft; đang lưu chặn gửi kép; offline chặn; lỗi lưu giữ draft và cho thử lại |
| B In hàng đã có | Tem → tìm → xem → In → số tem/khổ → loading → thành công | Search rỗng có xóa tìm kiếm; copies sai chặn; không có quyền/offline chặn; lỗi in giữ mã/số tem và ghi lượt thất bại; hủy/Escape quay lại danh sách |
| C Tem hỏng/mất | Tem → sản phẩm → Tem hỏng/mất → lý do → In lại | Luôn mã cũ. Thiếu bố cục cần xác nhận khôi phục; mất mã không có nút phát minh/in. Retry lỗi có log riêng, không thay SKU |
| D Lịch sử | Tem → Lịch sử → tìm/lọc → chi tiết | Chưa có lượt phù hợp có nút quay lại tìm/in; dialog detail đóng bằng nút/Escape; log không nhận là đã in vật lý |
| E Không có mẫu | Search không có → thủ công → custom nếu cần → Xác nhận/Lưu như A | Chặn thuộc tính lô/HSD/NSX; custom thiếu hoặc số sai lỗi; có đề xuất mẫu; nháp có thể dùng lại trong phiên. Không tự xuất bản mẫu |

Loading là trạng thái mô phỏng ở lưu/in (400ms); tìm danh sách RAM trả ngay, không giả vờ gọi API. Role và offline chỉ là công cụ test, không phải bảo mật production.

## 6. Chín yêu cầu được đối chiếu

1. Tem sản phẩm thống nhất, phân biệt in giấy và phục hồi dữ liệu: implemented.
2. Ngành và mẫu trên cùng màn hình: implemented; toàn bộ schema gốc.
3. Đúng ba bước + back giữ nháp + success actions: implemented.
4. Help dài chuyển thành help mở rộng/contextual; errors cạnh field: implemented.
5. Chính/phụ theo template.required; nhận diện biến thể vẫn hiện: implemented.
6. Mã sản phẩm có SKU/barcode và nguồn tách riêng; duplicate/format/scan/error: implemented local demo.
7. Tìm và nhóm thu gọn, no results/manual; không fake “thường dùng”: implemented.
8. Nâng cao custom, đề xuất, dùng lại draft: implemented RAM, chưa publish/review nghiệp vụ thật.
9. Lịch sử dưới Tem; vai trò ngoài form: implemented demo only.

## 7. Kiểm thử và ba vòng review

### Vòng 1 — chức năng
`review.cjs`: **75/75 PASS** ở lượt cuối; browser errors 0, external application requests 0, non-GET mutations 0. Flow A–E, enum, brand, draft, package count, unit conversion, scan duplicate, checksum, permission, lỗi/offline, copies, lịch sử, thiếu mã, khôi phục bố cục. Kết quả trong `QA_RESULTS.json`; chạy lại bằng `node review.cjs` khi server cũ 4197 và server mới 4198 đang hoạt động.

Schema gốc: `node --test ../ProductFormMockup/catalog-schema.test.cjs` **7/7 PASS**. `node --check app.js` và `node --check serve.cjs` PASS. Không coi 56 check cũ là kết quả mới.

Lỗi đã phát hiện/sửa: structuredClone chứa hàm schema làm trang không render; server sandbox không thể truy cập từ browser thật; công cụ demo che CTA sau tương tác; state quản lý đè draft; select input xảy ra trước change làm đổi đơn vị sai; phép đổi sau rerender; nhầm đo checkbox ẩn là target đang thấy; test package dùng lại SKU đã lưu gây duplicate hợp lệ (sửa fixture test, không bỏ validator).

### Vòng 2 — walkthrough vai trò (tự review, không phải nghiên cứu với người dùng thật)
- Nhân viên mới: tìm tem của hàng đã có, xem trước; thiếu quyền hướng dẫn liên hệ quản lý. Không phải chọn account role trong form.
- Quản lý tạo hàng: ngành/mẫu cùng màn hình, dùng ví dụ rồi sửa; nhận ra chưa nhập tồn. Back không mất thông tin; tạo xong in là thao tác riêng.
- Nhân viên thay tem: bắt đầu từ tìm SKU, không từ tạo hàng. Mã gốc readonly, reason riêng. Không có dữ liệu gốc thì dừng đúng chỗ.
- Quản lý schema: chỉnh lựa chọn phiên, dùng lại mẫu nháp; quay về sản phẩm không ghi đè draft. Cần kiểm chứng sự dễ hiểu trực tiếp với bạn, không tuyên bố giảm thời gian bao nhiêu %.

### Vòng 3 — visual/responsive/accessibility
- Batched screenshots desktop/mobile; xem trực tiếp form desktop, overview desktop, form390 và print360. Một batch sửa: field measure label thống nhất, text nhỏ lên 12px, toast tự hết sau 3.5s/clear khi chuyển màn, công cụ demo không che nút sau click ngoài. Sau đó confirm ảnh cuối.
- Widths **1440, 768, 390, 360**: no overflow trang form/overview và dialog; nút/input/select đang thấy ≥44px; mặc định ≥48px.
- Labels gắn control; field errors có aria-invalid/describedby; main skip link, focus ring, native modal focus, Escape, reduced-motion rules. Keyboard checks nằm trong QA JSON.
- Detector chạy một lần: warning small text đã sửa; border-accent là tiến trình/tab có ngữ nghĩa, không card accent; dark-glow là false positive vì nền trắng và shadow có offset. Không gọi những warning này là test failure production.
- Chưa có TalkBack/VoiceOver, bàn phím Samsung/iOS vật lý, IME/keyboard che form, độ đọc dưới ánh sáng kho, hoặc test người dùng thật. HTML responsive không chứng minh Unity/native.
- Confirm cuối phát hiện vị trí cuộn cũ làm ảnh full-page bắt sticky nav giữa trang. Đã reset cuộn khi đổi màn/bước và trước snapshot; conditional form rerender vẫn không ép cuộn. Kiểm thử chạy lại và ảnh được tái tạo.

## 8. Scorecard nội bộ

| Tiêu chí | Điểm / trọng số | Căn cứ / phần còn thiếu |
|---|---|---|
| Clarity | 17/20 | 3 bước thật, đơn vị/số tem tách; form mobile vẫn cần scroll, cần bạn duyệt copy |
| Business correctness | 18/20 | SKU/lot/receipt/print tách; GS1 chưa đọc lại, model chủ hàng chưa duyệt |
| Form usability | 12/15 | Dynamic, example, error, draft; chưa thử người mới trên điện thoại thật |
| Information architecture | 13/15 | 3 mục, lịch sử đúng module; vị trí cleaning và quản lý trên app cần chốt |
| Visual consistency | 8/10 | White–blue, BeVietnamPro, 48px; chưa native parity |
| Responsive + accessibility | 8/10 | 4 widths, keyboard, labels; chưa screen-reader/device |
| Interaction completeness | 8/10 | A–E local states; không scanner/printer/backend thật |
| **Tổng** | **84/100** | Tự đánh giá thiết kế; không phải điểm usability đo từ người dùng |

## 9. Triển khai sau phê duyệt — kế hoạch, chưa thực hiện

1. Bạn duyệt sitemap, ngành cleaning, thuộc tính nhận diện bắt buộc theo chủ hàng, vai trò/capability in và khổ tem. Chốt acceptance cho A–E trước code production.
2. Map UI sang owners native hiện có: PrototypeUi/PrototypeDialog/PrototypeWorkspace, ImportedFigmaNavigation, ImportedCameraScanner, ImportedWarehouseLists/OmsMobileApi. Không thay auth hay tạo backend song song.
3. UI-only: nav/entry labels, cùng màn hình ngành+mẫu, stepper, disclosure/help, dialog. Reuse tokens, font, bottom toolbar; giữ app nhân viên hiện tại ngoài phạm vi đã duyệt.
4. Logic cần xác nhận: draft theo template, transaction validation, category/template version, unit normalization, product packaging definition, barcode mapping. API thật phải allocate SKU atomic, enforce unique theo tenant/owner, trả source/code; quyền enforced server-side. Không chốt tên endpoint mới từ mockup.
5. In: dùng encoder thật có quiet-zone/checksum; đủ dữ liệu gốc/label layout; printer adapter, export format và repeat policy. Mockup không phải mã vạch dùng sản xuất. Lịch sử cần immutable actor, request ID/idempotency, trạng thái printer acknowledged/failed, retry không ghi nhận printed khi chưa biết.
6. Lô/receiving vẫn module riêng: quantity, lot, NSX/HSD, access location, PAO nếu có. SKU policy không tự tính hạn từ category. Không đặt barcode scope cho order, pallet, employee hoặc lot trong đợt này.
7. Migration: audit schema/IDs + tham chiếu hiện hữu; snapshot DB sau khi được phép; additive mapping và version, không overwrite mã cũ. Dry-run trên bản sao đã ẩn dữ liệu nhạy cảm; preserve original category ID nếu đổi nhóm hiển thị. Backfill mã thiếu chỉ từ nguồn xác minh, manual resolution queue.
8. Rollout staging có feature flag; test hợp đồng/auth/unique trên backend, compile Unity sau mỗi nhóm native đã duyệt; acceptance Samsung/printer. Android APK chỉ build ở giai đoạn được duyệt; iOS phải có signing/Mac pipeline phù hợp.
9. Rollback: tắt flag trở về entry cũ, giữ controller/API và schema cũ; không rollback DB destructively hay tái dùng mã đã cấp. Lịch sử/mapping mới nếu đã ghi phải được giữ và tương thích đọc. Không xóa mockup cũ trước acceptance.

## 10. Quyết định cần bạn duyệt

- Chuyển chất tẩy rửa vào Đồ gia dụng theo yêu cầu trước, hay giữ Other theo schema gần nhất? Demo giữ nguyên để chờ quyết định.
- Brand, hương vị, mức đường, màu/model nào phải phân biệt SKU trong kho của bạn?
- Thùng và đơn chiếc có được picking độc lập, cần SKU riêng hay mapping packaging của cùng sản phẩm?
- Nhân viên được in ngay, hay quản lý cấp quyền? Demo mặc định quản lý in, nhân viên chỉ xem.
- Tem có những nội dung nào và printer gì? Không đưa HSD từng lô lên tem SKU một cách mặc định.

## 11. File và provenance

Chỉ tạo file dưới `Docs/ProductUxReview`: index.html, app.css, app.js, serve.cjs, review.cjs, SURFACE_BRIEF.md, REVIEW.md, DESIGN.md, README.md, QA_RESULTS.json; screenshots/ và log server là outputs. Schema/fonts đọc từ nguồn có sẵn, không thay nguồn.
Ảnh screenshots là capture trình duyệt thật từ mockup địa phương ngày 12/10/2026, không render AI. Before-Desktop từ bản cũ; các ảnh khác từ bản mới, dữ liệu mock.

Không chạy compile Unity/Android vì yêu cầu mới nhất cấm thay Unity trước approval. **Không tạo APK mới; không cập nhật APK hiện có.**

Skill context cũng báo PRODUCT.md dùng metadata platform/schema cũ; không tự sửa tài liệu production trong lượt này. Có thể chuẩn hóa riêng bằng workflow init nếu chủ dự án yêu cầu.

AWAITING USER APPROVAL — MOCKUP READY FOR REVIEW
