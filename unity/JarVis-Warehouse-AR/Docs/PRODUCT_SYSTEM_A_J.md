# JarVis — Báo cáo A–J: phân loại, thuộc tính động và phục hồi tem

Ngày: 2026-10-08. Trạng thái: **đề xuất / prototype chờ chủ dự án phê duyệt**.

Phạm vi lượt này: audit mã nguồn liên quan, đối chiếu nguồn chính thức, thiết kế kiến trúc và prototype tách khỏi app. Không sửa C#, scene, backend, database, quyền tài khoản hay deployment. Không gọi AI, không seed kho thật. Báo cáo không thay thế nghiệm thu toàn bộ ứng dụng.

## A. Architecture Audit — bằng chứng thực tế

| Thành phần | Đã có trong mã nguồn | Thiếu / giới hạn | Bằng chứng |
|---|---|---|---|
| Unity | uGUI/TMP, viewport 412×915, font Be Vietnam Pro; điều hướng, scanner và session OMS | Form động và nghiệp vụ đăng ký sản phẩm mới chưa có ở luồng native hiện tại | `E:/unity of teep/JarVis-Warehouse-AR/CODEX_CONTEXT.md:3`; `Assets/JarVis/Scripts/Backend/OmsMobileApi.cs:326` |
| Sản phẩm | UUID `id`, SKU duy nhất không phân biệt hoa thường, barcode duy nhất, tên, unit, lot_tracking_required | Chưa có category/template/brand/thuộc tính, product family, expiry policy, mã tự cấp hoặc fingerprint | `E:/warehouse-ai/oms/schema.sql:52`; `E:/warehouse-ai/oms/store.js:380` |
| SKU / barcode | Một bản ghi oms_products tương ứng một mặt hàng quản lý tồn. Barcode là giá trị của sản phẩm đó | Chưa có barcode-record riêng, alias, symbology hoặc provenance. Không có bằng chứng barcode hiện đại diện từng đơn vị vật lý | `E:/warehouse-ai/oms/schema.sql:52`; `E:/warehouse-ai/oms/store.js:362` |
| Lô | Nhiều lô theo product_id; unique(product_id,lot_code); có manufacture_date và expiry_date | POST /lots hiện nhận expiryDate nhưng không nhận manufactureDate; store cũng không ghi NSX trong createLot. Regex ngày chưa đủ kiểm tra ngày lịch | `E:/warehouse-ai/oms/schema.sql:68`; `E:/warehouse-ai/oms/routes.js:337`; `E:/warehouse-ai/oms/store.js:407` |
| Tồn / vị trí | Balance theo sản phẩm–lô–vị trí; số lượng nguyên; constraints không âm; movements riêng | Không biến định lượng ml/kg thành đơn vị tồn. Chưa hỗ trợ tự quy đổi thùng/chai hoặc hàng cân lẻ | `E:/warehouse-ai/oms/schema.sql:79`; `E:/warehouse-ai/oms/store.js:447` |
| Nhập kho | Tạo receiving xác minh lô thuộc sản phẩm; confirm cập nhật balance + movement trong transaction, requestId chống ghi hai lần | Unity hiện vẫn thông báo chưa ghi giao dịch ở nút nhập/xuất. Không coi form hiển thị là flow nhận hàng hoạt động | `E:/warehouse-ai/oms/store.js:433`; `E:/warehouse-ai/oms/store.js:447`; `Assets/JarVis/Scripts/UI/ImportedFigmaNavigation.cs:221` |
| Xuất kho | Schema orders/items và tác vụ, gợi ý lấy hàng theo expiry/FIFO | Chưa xác nhận luồng xuất vật lý đầu-cuối từ Unity trong lượt này. Không nâng cấp xuất trong phạm vi đăng ký/in tem | `E:/warehouse-ai/oms/schema.sql:111`; `E:/warehouse-ai/oms/store.js:419`; `Assets/JarVis/Scripts/UI/ImportedFigmaNavigation.cs:221` |
| Quét camera | ZXing decoder + nhập tay; gửi search inventory; lọc barcode/SKU/lotCode bằng equality ở client | API search bắt đầu từ balances + LIKE + LIMIT 200; sản phẩm chưa có balance không xuất hiện; không thể thay lookup SKU chính xác | `Assets/JarVis/Scripts/UI/ImportedCameraScanner.cs:187`; `E:/warehouse-ai/oms/store.js:362` |
| Chống trùng | Constraints SKU/barcode; lỗi duplicate ở API; tests hiện có | Chưa đối chiếu tên/brand/thuộc tính chuẩn hóa; chưa bảo vệ autoissue/idempotency tạo SKU; fuzzy match không được tự merge | `E:/warehouse-ai/oms/schema.sql:52`; `E:/warehouse-ai/tests/oms.test.cjs:77` |
| Tem / PNG / in lại | Native có màn Tạo tem hàng và nút In tem | Owner hiện chỉ gọi thông báo; không thấy flow encoder→PNG→print, label history hay restore endpoint trong owners đã audit. Không thể báo “in lại không đổi tồn” là chức năng đã kiểm thử thực tế | `Assets/JarVis/Scripts/UI/PrototypeOperationPresentation.cs:17`; `Assets/JarVis/Scripts/UI/ImportedFigmaNavigation.cs:223` |
| Web admin | Tìm/tạo sản phẩm, lô và receiving bằng OMS; barcode hiện dạng text | Chưa có editor category/template/attribute/brand và màn quản lý lịch sử in | `E:/warehouse-ai/oms-web/app.js:104`; `E:/warehouse-ai/oms-web/app.js:35` |
| Auth | Web session + CSRF; mobile bearer; cả hai dùng cùng OmsStore | Mutation web bị chặn nếu is_admin != 1, sau đó còn kiểm MANAGER. Quản lý thường không đồng nghĩa có quyền sửa. Mobile chưa có đăng ký sản phẩm/in tem | `E:/warehouse-ai/oms/routes.js:275`; `E:/warehouse-ai/oms/routes.js:107` |
| Render | render.yaml khai báo Node/free, SQLite path data/warehouse.sqlite3; không thấy disk khai báo trong manifest đó | Đây chỉ là cấu hình repo, chưa kiểm chứng service chạy thật/persistence/backup/commit đang deploy. Không deploy trong lượt này | `E:/warehouse-ai/render.yaml:1`; `E:/warehouse-ai/server.js:77` |

Các đường dẫn `Assets/...` thuộc `E:/unity of teep/JarVis-Warehouse-AR/`. Git Unity đã có 206 mục thay đổi trước lượt này; backend cũng dirty. Không reset, commit hoặc ghi đè các thay đổi đó. Database live được xác nhận tồn tại ở `E:/warehouse-ai/data/warehouse-live.sqlite3`; chỉ xem metadata file, không khởi tạo OmsStore trên database live. Kết luận schema dựa trên repo, không khẳng định đã audit schema/dữ liệu live từng hàng.

**Tái sử dụng:** OmsStore, transaction/writeQueue, UUID/FK, bảng lot/balance/movement/audit, receiving requestId, session/CSRF, decoder ZXing, navigation và tokens JarVis. Không tạo app hay database sản phẩm thứ hai. Migration runner có version/rollback vẫn phải thiết kế thêm; init hiện chỉ có schema IF NOT EXISTS và ALTER bổ sung is_admin.

## B. Product Requirement Analysis — xung đột và quyết định

1. **Chất tẩy rửa:** yêu cầu trước nằm trong Gia dụng; master prompt mới đặt trong Khác. Prototype mới thể hiện **Khác → Chất tẩy rửa**, nhưng đây là thay đổi tổ chức cần chủ dự án duyệt, chưa chuyển dữ liệu cũ.
2. **Quần áo → Thời trang**, Gia dụng giữ dụng cụ và thiết bị. Đủ năm nhóm mới; giày có EU 35–46, không dùng S/M/L và không tự chuyển US/UK.
3. **Số lượng:** giữ ô theo yêu cầu, đặt tên Số lượng dự kiến nhận. Nó thuộc receipt draft, không phải Product/SKU và không là số tem. Chỉ confirm receiving mới cập nhật tồn. Đăng ký-only có thể không tạo phiếu nhận. Đây là phương án cần duyệt để tránh nhân viên tưởng lưu SKU là nhập hàng.
4. **NSX/HSD:** đặt ở vùng Lô của lần nhận này, chỉ khi người dùng kèm receipt draft và loại hàng phù hợp. Mỗi lô có ngày riêng. Không lưu ngày chung vào SKU. Thiếu nhãn → cần xác minh, không tạo ngày giả. Thời trang/gia dụng thông thường không có date fields, nhưng không tắt lot_tracking_required cũ. Ngoại lệ theo hợp đồng chủ hàng phải cấu hình riêng sau duyệt.
5. **Dung tích và kích thước:** cùng số nhưng khác dimension. Diameter cm, capacity l, net content g/ml. Gợi ý không phải thông số của sản phẩm thực. Bỏ câu “Mỗi hộp chứa bao nhiêu”; dùng Dung tích trên nhãn và Đơn vị đếm độc lập.
6. **Size / biến thể:** đổi màu/size/dung tích/model khi quản lý riêng → SKU khác. Đổi lô/HSD/vị trí không tạo SKU. Lốc/thùng là SKU riêng chỉ khi thực sự quản lý mặt hàng đó; không đếm đôi cùng lượng hàng hoặc tự quy đổi.
7. **Mỹ phẩm:** PAO khác HSD; master mới cho để PAO giai đoạn sau. Prototype không tự tính HSD từ PAO. Không mặc định mọi son là ml; g được hỗ trợ.
8. **Khác:** có trong mọi nhóm mẫu; cho typed attributes và mẫu riêng tái sử dụng. Giá trị nhập tay không tự được xuất bản vào taxonomy chung; quản trị duyệt/version. Serial thuộc đơn vị thiết bị, không thuộc SKU chung. Serial/warrranty nằm trong kế hoạch sau, không giả dữ liệu serial trong prototype.
9. **Chống trùng:** exact SKU/barcode phải chặn; match thuộc tính chỉ gợi ý ứng viên. Không merge dựa vào tên chung “Nước suối” hoặc chưa biết brand. Tenant/chủ hàng chưa rõ, giữ global uniqueness đang có cho đến khi duyệt.
10. **Tem hỏng:** xuất lại cùng định danh, không tạo SKU/barcode và không ghi movement. Tìm theo lot/location hỗ trợ xác định hàng, không biến barcode sản phẩm thành barcode lô.
11. **Quyền:** prototype mô phỏng admin để thử xác nhận. Quyền in/đăng ký cho MANAGER/EMPLOYEE cần phê duyệt capabilities riêng; không mở quyền dựa vào role name hoặc giao diện.
12. **Tạo barcode:** backend cấp mã nội bộ ổn định sau transaction; Code128 không là GTIN. GTIN nhà sản xuất cần kiểm tra định dạng/check digit và nguồn, không tự tạo GTIN rồi gọi hợp lệ. Code128/QR decoder hiện có không chứng minh encoder/export đã nối.

Nghiệp vụ tách biến thể/danh mục tham khảo [Shopify taxonomy](https://help.shopify.com/en/manual/products/details/product-category). Lô và số lượng nhận riêng tham khảo [ShipBob Receiving](https://developer.shipbob.com/guides/receiving) — API 2026-07, trang ghi cập nhật 2026-07-17; lot_date của ShipBob có thể mang NSX hoặc HSD theo hợp đồng của họ, JarVis giữ hai trường riêng. Định lượng/PAO tham khảo [GS1 GDM 1.16, May 2026](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/). Không nguồn nào chứng nhận toàn bộ UI JarVis hoặc đặt tất cả trường là bắt buộc.

## C. Category & Attribute Matrix

Danh sách đầy đủ được ghi dưới dạng dữ liệu cấu hình trong `ProductFormMockup/catalog-schema.js`: **5 ngành, 14 nhóm phụ, 120 mẫu có tên + 14 lựa chọn Khác**. Danh sách thương hiệu và giá trị gợi ý lấy từ yêu cầu chủ dự án, không phải chứng nhận hãng bán mọi loại hàng hoặc thông số được nhà sản xuất xác minh. Các ID slug trong prototype chỉ để review, production phải dùng ID bất biến tách khỏi tên hiển thị.

| Ngành → nhóm | Mẫu | Thuộc tính / đơn vị | Validation đề xuất | Lô / HSD |
|---|---|---|---|---|
| Đồ uống | 9 loại: nước suối, nước ngọt có gas, nước trái cây, sữa tươi/sữa hạt, trà/cà phê, tăng lực/thể thao + Khác | volume ml/l; flavour; sugar 4 mức; brand; count-unit hộp/chai/thùng | volume >0; tùy chỉnh phải có giá trị; chuẩn hóa l→ml để đối chiếu, không quy đổi tồn | NSX/HSD theo lô receipt; thiếu ngày cần xác minh |
| Thời trang → Áo | 9 mẫu + Khác | 12 màu; XS–XXXL/Free Size; brand | màu/cỡ cần khi nhận diện phiên bản; size khác nhập chính xác | Không date fields thông thường |
| Thời trang → Quần | 8 mẫu + Khác | màu; chữ hoặc 26–36 theo danh sách; brand | Không dùng size giày; không tự suy số đo từ size | Như trên |
| Thời trang → Váy | 8 mẫu + Khác | màu; XS–XXL/Free Size; brand | Kiểm tra thuộc tính phiên bản | Như trên |
| Thời trang → Giày | 8 mẫu + Khác | màu; EU 35–46; brand | hệ size rõ; Khác phải ghi hệ nếu dùng size khác | Như trên |
| Thời trang → Mũ | 6 mẫu + Khác | màu; Free Size/S–XL hoặc chu vi cm; brand | không đổi chu vi thành hệ chữ tự động | Như trên |
| Thời trang → Phụ kiện | túi, thắt lưng, khăn, găng tay, tất + Khác | chỉ chất liệu + quantity; tên từ mẫu | không ép nhập lại tên/màu/size/brand không cần thiết; tên đã chọn vẫn được sửa | Như trên |
| Gia dụng → nhà bếp | 13 mẫu + Khác | brand/material; phép đo riêng theo mẫu | vật liệu tùy chọn nếu không phân biệt SKU; không có Size chung | Không date fields thông thường |
| → Chảo | Chảo | diameter cm:16–32 bước2/Khác | diameter >0; chiều sâu có thể thêm sau | Như trên |
| → Nồi | Nồi | diameter cm; capacity l tùy chọn | không trộn hai phép đo | Như trên |
| → Dao / Thớt | Dao / Thớt | blade length cm / length+width cm | kích thước >0, không ml | Như trên |
| Gia dụng → điện | 10 mẫu + Khác | model; material; capacity/body dimensions khi phù hợp | model hoặc mã phiên bản, không suy kích thước từ tên | Như trên |
| → Lò vi sóng | Lò vi sóng | capacity 17/20/23/25/28/30/32 l; body L/W/H cm tùy chọn | capacity khác external dimensions | Như trên |
| → Nồi chiên | Nồi chiên không dầu | 3/4/5/5.5/6/7/8/10 l/Khác | capacity >0 | Như trên |
| → Nồi cơm / Bếp từ | Nồi cơm / Bếp từ | capacity l / L+W cm + số vùng integer + W tùy chọn | số vùng nguyên; không dùng capacity cho bếp | Như trên |
| Mỹ phẩm | 16 mẫu + Khác | net content ml/g; brand; son có màu/mã màu | >0, giữ dimension thực; g không chuyển ml | receipt theo lô; PAO để sau |
| Khác → Điện tử | 10 mẫu + Khác | brand tùy chỉnh, model, config, màu tùy chọn | model/config phân biệt; serial tương lai ở unit level | Không date fields thông thường |
| Khác → Đồ ăn khô | 10 mẫu + Khác | mass g/kg, flavour, brand, count-unit | khối lượng >0; kg→g chỉ thuộc tính | receipt theo lô |
| Khác → Chất tẩy rửa | 8 mẫu + Khác | net content ml/l/g/kg; scent; brand; unit | kiểm tra đúng nhãn; không tự xem là hàng nguy hiểm | chỉ khi nhãn/warehouse policy yêu cầu |
| Khác → Nhập thủ công | Khác | name, mô tả; typed attributes text/number/date + unit | không ép mọi field; custom key không ghi đè field hệ thống; phải duyệt schema chung | tùy policy, không tự quyết |

Các mức required trong prototype là đề xuất để thử, không là quy định pháp luật. Thuộc tính bổ sung (serial, bảo hành, chứng từ xử lý, pin, PAO, bảo quản chi tiết) chưa giả là được hệ thống thực quản lý. Không có cơ sở áp dụng một size chuẩn cho mọi hãng hoặc gán HSD mặc định.

## D. User Flow

```text
Đăng nhập / xác minh quyền bằng OMS
  → chọn ngành (5 nhóm)
  → chọn nhóm phụ, tìm mẫu / Khác / sản phẩm đã có
  → kiểm tra product + variant attributes
  → đối chiếu trùng: chọn SKU có sẵn hoặc tiếp tục đăng ký
  → lưu product theo requestId, cấp/giữ barcode đúng nguồn
  → detail / xem tem / xuất PNG / yêu cầu in

Nếu có nhận hàng: receipt draft riêng
  → chọn SKU → chọn/nhập lô và NSX/HSD phù hợp
  → dùng vị trí kho hiện có → confirm receiving → movement / balance

Tem hỏng → tìm SKU/tên/brand/lô/vị trí
  → xác nhận đúng SKU và barcode cũ
  → chọn bản tem + số tem + lý do
  → export/request print theo quyền → history
  → tuyệt đối không gọi createProduct/confirmReceiving
```

Back giữ nháp, đổi mẫu có nháp tách theo template; trường không áp dụng không được gửi. Nếu schema version đổi giữa nhập và lưu: 409 + chỉ rõ field cần xác nhận, không tự xóa người dùng nhập. Mạng mất: giữ nháp, không báo lưu/in thành công; kiểm tra requestId trước retry. Barcode SKU không giúp chọn lô tự động khi một SKU có nhiều lô. Sản phẩm đã có cần exact lookup cả khi chưa có balance; prototype có lựa chọn fixture trong form (mã/thuộc tính chỉ xem) và ở phục hồi, chưa là live lookup.

## E. Database & API Design — đề xuất, chưa migration

### Mở rộng tương thích

- Giữ `oms_products.id` làm **SKU ID hiện hành**, SKU/barcode cũ giữ nguyên. Không buộc mọi SKU cũ có product family. `oms_product_families` optional để gom biến thể; không đổi các FK lot/balance/order cũ.
- Thêm `oms_product_categories`, `oms_product_subcategories`, `oms_product_templates` với immutable ID, label, version, active; `oms_brands` có scope category khi cần.
- `oms_attribute_definitions`: stable key, label, type (text/decimal/integer/enum/date), dimension, allowed_units, required/conditional, scope (SKU/LOT/UNIT/RECEIPT), active. Tránh thực thi biểu thức arbitrary JS từ cấu hình; chỉ DSL conditions được whitelist.
- `oms_attribute_options`: definition FK, value, display label, order, active; `oms_template_attributes`: template/definition FK, requirements, order, options/profile version.
- `oms_product_metadata`: product_id PK/FK, category/subcategory/template/version, brand_id hoặc brand custom, expiry_policy (`UNVERIFIED/NONE/REQUIRED/OPTIONAL`), product_family_id nullable. Không date NSX/HSD tại đây.
- `oms_product_attribute_values`: product_id+definition_id; typed columns theo type, value/unit gốc, canonical value/unit/dimension riêng. Không lẫn volume, mass, length; decimal/canonical chuẩn hóa với giới hạn và precision xác định ở server, không hash float tùy tiện.
- Tái sử dụng `oms_lots` cho batch; hoàn thiện API nhận/đọc manufacture_date, calendar validation, conflict protection. `lot_tracking_required` cũ giữ nguyên, không mặc định theo category.
- Tái sử dụng balance/receiving/movement. Quantity dự kiến không được nhét vào product metadata. Serial nghiệp vụ tương lai: `oms_product_units` có serial unique theo quy định, product_id/lot_id; không một serial chung của SKU.
- `oms_barcode_records`: product_id, value TEXT UNIQUE, symbology, source, primary, immutable issuance metadata. Ban đầu mirror barcode cũ như primary, legacy source=UNVERIFIED; không đoán numeric string là GTIN. Không bật alias/serial barcode trong P0.
- `oms_label_templates`: version, physical size, margins, font, barcode type; history snapshot giữ render params để tái hiện bản cũ. `oms_label_events`: actor, product, barcode value/symbology, count, reason, device, requested/exported/printed/failed status, timestamps, requestId unique. Không chứa quantity_change.
- `oms_catalog_proposals`: private custom templates/values, submitted_by, review status; không tự thêm option chung. `oms_product_create_replays` đảm bảo cùng requestId+payload trả cùng product, payload khác →409.

Tối thiểu bản đầu không cần một bảng cho mỗi danh mục hàng. Các lớp trên là logical design, DDL cụ thể và index phải review riêng. Lot/receipt/product scope là whitelist, không cho custom field tên “quantity_on_hand” vượt qua nghiệp vụ tồn.

### API dự kiến (chưa tồn tại)

| API | Chức năng / quyền dự kiến |
|---|---|
| GET /api/oms/mobile/catalog?version=... | danh mục/schema active, authenticated; ETag/version, cache read-only, không lưu SKU offline |
| GET /api/oms/mobile/products/lookup?barcode=... | exact match từ products/barcode records, không balance join và không bị search LIMIT; trả 0/1 record |
| GET /api/oms/mobile/products?q=... | tìm kiếm phân trang; separate fuzzy candidate matching với structured attributes |
| POST /api/oms/mobile/products/register | requestId + schemaVersion + typed values + unit + barcodeSource; gated capability, không tăng tồn |
| GET /api/oms/mobile/products/:id/label | server-verified barcode data + labelVersion, authenticated; chưa in |
| POST /api/oms/mobile/label-events | số tem/lý do/requestId; capability label.export; stock invariant; xuất/request không đồng nghĩa in thành công |
| /api/oms/catalog/* (web) | admin cấu hình/version/archive/review custom; CSRF và capability |
| /api/oms/lots và /receiving hiện có | nâng cấp backwards-compatible NSX/policy; không đổi ID/mã hoặc xóa endpoints cũ |

Legacy POST /products giữ payload cũ; route mới nhận extended metadata có transaction chung. barcode autoissue phải phân bổ ở server trong transaction, giới hạn charset/length của Code128 nội bộ, sequence unique không dựa timestamp đơn thuần. GTIN external phải kiểm tra đúng symbology/check digit và dữ liệu chủ sở hữu; client không tự cấp.

### Chống trùng và snapshot

Canonical candidate signature: tenant scope được duyệt + brand đã xác nhận + template/variant keys + normalized measurements + colour/size system/model + count-unit/pack definition. Không gồm NSX/HSD/location/stock hoặc serial. Unknown brand/spec không đủ để chặn tất cả tên giống nhau. Exact sku/barcode uniqueness là hàng rào trong DB transaction; near match chỉ gợi ý chọn SKU. Không tự relabel các variant thành cùng barcode vì 1 l = 1000 ml nếu packaging/brand/type khác.

### Migration / rollback

1. Chụp backup SQLite nhất quán bằng backup API/.backup, kiểm tra integrity và hash; bao gồm WAL bằng phương thức an toàn, không copy file đang ghi tùy tiện. Kiểm tra deployment persistence trước khi migrate Render.
2. Snapshot counts và danh sách ID+SKU+barcode+FK; clone database **riêng cho test**, không đổi đường dẫn database live.
3. Additive tables/nullable metadata + schema_migrations version. Mapping SKU cũ không suy category, brand, HSD hoặc symbology. Chỉ backfill định danh và liên kết cũ đã biết.
4. Dual-read/mirror primary barcode trong transaction; parity checks old/new. Feature flag OFF mặc định; consumer cũ tiếp tục hoạt động.
5. Test rollback trên clone, foreign_key_check/integrity_check, row/ID/barcode preservation, balance/movement equality và replay semantics. Chỉ bật sau nghiệm thu.
6. Rollback ưu tiên tắt feature và dùng legacy readers giữ nguyên ID/barcode; không drop bảng mới có dữ liệu. Restore snapshot chỉ sau reconciliation các giao dịch phát sinh, maintenance window và phê duyệt. Không khôi phục mù làm mất movements mới.

**Lượt này không chạy migration, tạo tables, backfill hoặc backup database live.**

## F. Web vs Unity Architecture

| Unity — thao tác nhanh | Web — quản trị | Backend — nguồn sự thật |
|---|---|---|
| catalog cache/version, schema renderer uGUI/TMP, numeric/date input, lookup camera, chọn SKU/mẫu, private draft, xem/xuất tem | schema editor, brand/options, version/review custom, archive, filters, history/export/reprint | validators typed values/units, auth capabilities, duplicates, issuance, transactions, receipt/lot conflicts, audit |

Không lấy web DOM hoặc ảnh chụp làm giao diện Unity. DTO update đồng thời backend và OmsMobileApi; dùng template stable IDs, tách schema khỏi renderer. Xử lý schema lỗi bằng fallback read-only sản phẩm cũ, không render mã code động. Be Vietnam Pro/tokens/route scanner giữ nguyên. Web publish schema tăng version, Unity tải snapshot mới; không database/cache viết master dữ liệu riêng. Không polling khi màn đóng; draft lưu local không là warehouse source.

Quyền đề xuất: admin configure; manager review/lookup (mutations tùy capability cần duyệt); employee lookup và submit draft, export label nếu được cấp quyền riêng. Server quyết định, không kiểm role ở client thay cho authorization. Scope owner/warehouse của records phải kiểm cả lookup lẫn export. Serial/GS1 serial code ngoài barcode SKU hiện tại.

## G. UI/UX Prototype — hướng dẫn duyệt

Mở **http://127.0.0.1:4197/catalog.html** sau khi chạy `node serve.cjs 4197` tại `Docs/ProductFormMockup`. Server whitelist chỉ đọc file prototype/font trên localhost; CSP không cho gọi backend/network. Bản cũ 4196 vẫn giữ nguyên để so sánh, không đóng phiên đang có.

1. **Tạo sản phẩm:** chọn ngành → nhóm/mẫu có search → dynamic form → bản xác nhận/tem minh họa. Đủ 134 lựa chọn, mọi nhóm phụ có Khác. Thời trang và gia dụng không hiện NSX/HSD.
2. **Đồ uống:** dung tích 180/390/500/1000 ml và Khác với ml/l, brand/hương vị/mức đường, quantity+count-unit; không còn câu hỏi “Mỗi hộp chứa bao nhiêu”. Ngày ở receipt draft.
3. **Gia dụng:** chảo diameter cm, lò vi sóng capacity l + optional body dimensions; không dropdown Size chung. Model chưa biết để trống, không bịa model.
4. **Mỹ phẩm:** ml/g, son default g; date fields khi kèm receipt; thiếu date chưa xác minh, không tự tạo ngày.
5. **Khác:** electronics/dry food/cleaning/manual; custom typed field và “giữ mẫu trong phiên” có thể chọn lại ở danh sách. Reload không giữ dữ liệu; chưa phải persistence nghiệp vụ.
6. **Phục hồi tem:** dùng fixtures minh họa được gắn nhãn, search nhiều định danh → confirm product → cùng barcode → copies/reason/role → simulated history. Không in hay cấp mã thật.
7. **Web · Danh mục:** xem schema theo category/template, đề xuất option/brand và áp dụng **trong RAM phiên thử**, rồi quay lại form kiểm tra; mẫu custom chưa xuất bản.
8. **Web · In tem:** tìm, xác nhận, label preview, simulated export/history; không báo PRINTED khi không có thiết bị.

Giữ nền trắng/điểm nhấn xanh/font cũ, thao tác ≥44px, form cuộn, footer hành động tách biệt. Không dùng fake barcode graphic để chứng minh camera quét được: prototype chỉ trình bày bố cục tỷ lệ 50×30 mm, không cam kết máy in/pixel density. Loading/offline/API-error thật cần adapter trong implementation; prototype chưa kết nối API nên không giả live-success.

Kho/vị trí minh họa ở phục hồi chỉ để thử tìm kiếm, không thêm input kho dư thừa vào form sản phẩm. Barcode fixtures `JV-DEMO-001/002` không tồn tại được xác minh trong live DB và không gọi là GTIN.

## H. Implementation Roadmap

| Mốc | Nội dung | Điều kiện / rủi ro |
|---|---|---|
| Duyệt hiện tại | A–J + prototype, xung đột vị trí cleaning, quantity/receipt, permissions | Không sửa app/schema trước duyệt |
| P0a | exact product lookup cả SKU zero-stock; đọc barcode cũ; verified encoder PNG/export; recovery + history không movement | Kiểm tra barcode-format legacy, kích thước/DPI thực, capability. Chưa cần catalog phức tạp để phục hồi tem |
| P0b | catalog schema/version + typed validators + additive migration trên clone, normalization, metadata, creation replay/duplicate suggestion | Review schema và rollback; không rewrite IDs/units/stock. SQL precision, namespace SKU cần quyết định |
| P0c | API register + form Unity + web đăng ký chung; ghi attributes; barcode allocation server-side | Đóng P0b và auth; literal templates từ prompt chỉ là gợi ý |
| P0d | NSX/HSD calendar validation, receipt draft/lot conflicts, giữ quy trình nhập hiện có | Chính sách nhãn chỉ tháng/năm, expired/quarantine cần kho duyệt; không auto receiving từ registration |
| P1 | schema/brand/options admin, custom review/private reuse, capabilities, print device/status/history management | Audit permissions và field migrations, không xóa lịch sử |
| P2 | near-expiry configurable, filters/data quality/AI read-only cần quyền | Không tự chọn ngày ngưỡng hoặc gửi nội bộ cho AI; serial/bảo hành/packing conversion là nghiệp vụ riêng |

Sau mỗi nhóm **implementation thật**: backend unit/API bằng DB tạm, Unity compile focused, rồi UI verify affected routes; Android build sau compile sạch. Samsung mic/camera/IME và printer acceptance riêng. Không cài package, deploy, gọi model hay commit trong lượt prototype này.

## I. Acceptance & Test Plan

| Tiêu chí master | Kiểm tra prototype / kế hoạch thật |
|---|---|
| 1–2: 5 nhóm, mẫu đủ | schema unique IDs; so danh sách prompt, group manual; UI render/search |
| 3: Khác nhập/lưu | validate custom; giữ và chọn lại mẫu trong RAM; persistence/review API cần test ở P1 |
| 4–7: dynamic/size/units | giày chỉ EU; pan cm/microwave l; ml/l và g/kg normalize không cross-dimension; numeric bounds |
| 8–10: lô/ngày, nhóm không HSD, bỏ câu cũ | date calendar/leap/NSX≤HSD; receipt date fields; fashion/home absent; template text audit |
| 11: kho không nhập lại | product form không có location/warehouse input; adapter receipt dùng location IDs cũ |
| 12: duplicates | prototype nêu server gate, không giả đã chống trùng; real parallel register/replay/exact duplicate/near-match cần API tests |
| 13–16: barcode/reprint/no stock | fixtures giữ mã/history copies; thật encode→decode PNG, old-code scan, unchanged product/lot/balance/movement/issuance count sau reprint |
| 17: web/Unity chung | schema API contract/version và concurrent changes; cùng DB/session guards; chưa chứng minh đồng bộ bằng prototype |
| 18–19: UI/flow | browser QA 390/412/768/1280, touch/focus/VI, back/drafts/search/error/permission/mock history; Samsung riêng |
| 20: regression | chạy tests có sẵn trên DB tạm; migration parity; Unity focused scanner/routes compile sau code thật |

Unit thêm: input numeric 0/âm/NaN/overflow, canonical 1 l=1000 ml và 1 kg=1000 g nhưng không ml=g, dates 2026-02-30/2024-02-29, unit whitelist theo dimension, schema corruption/future version safe fallback.

API cần test: 401/403 mọi write/export; CSRF web; product zero-stock lookup; unrelated lot/product mismatch; requestId cùng payload replay/khác payload409; racing same code; serial scoped đúng unit; inactive schema cannot create; hidden irrelevant fields reject/ignore theo contract; native/network reconnect không tự resubmit mutation.

Migration cần test: legacy IDs/barcodes/lots/balances unchanged; foreign keys; mirror consistency; rollback có movements mới không mất; không seed live. Print real: scale integer modules, quiet zones, black bars/white background, Unicode font glyphs, aspect ratio và decoder roundtrip ở định dạng được hỗ trợ; quyền lưu Android/Samsung, thiết bị printer và printer acknowledgment. “Exported/requested” không đồng nghĩa “printed”.

Kết quả đã chạy của lượt này được ghi riêng ở `ProductFormMockup/CATALOG_REVIEW.json` và `ProductFormMockup/CATALOG_TEST_RESULTS.md`; không biến kế hoạch tests thành PASS nếu chưa chạy. Không có migration/integration live/Unity compile/APK mới trong lượt này vì chỉ đổi tài liệu/prototype.

## J. Recommendation — làm gì có giá trị trước?

**Không chỉ làm dropdown, nhưng cũng không triển khai cả ERP một lượt.** Nên làm exact lookup + phục hồi/PNG barcode cũ trước (P0a), vì SKU/barcode đã có và nghiệp vụ không cần biến đổi tồn. Song song chốt schema cho đăng ký sản phẩm (P0b), rồi nối form/API/Unity (P0c). Không dựng EAV tùy ý cho toàn bộ warehouse, không tạo variant tree bắt buộc cho SKU legacy.

Một SKU có attributes và nguồn barcode ổn định đủ cho scope hiện tại. Product family là optional; catalog admin version/review ở P1. Serial/packing conversion/PAO automation chưa phải điều kiện để in lại barcode SKU. Không mở rộng barcode đơn hàng/lô/kệ/nhân viên/pallet.

### Cần chủ dự án phê duyệt

- Chất tẩy rửa ở **Khác** theo master mới hay giữ **Gia dụng** như lần trước?
- Quantity là **receipt draft riêng**, không tăng tồn khi đăng ký; date ở lô khi nhận, đúng chứ?
- Bắt đầu P0a in lại barcode có sẵn rồi P0b/c đăng ký đầy đủ?
- Ai được tạo SKU/xuất/in tem? Giữ admin-only hiện tại cho đến khi capabilities được duyệt?
- SKU/barcode uniqueness đa chủ hàng, expired/quarantine, nhãn tháng/năm, ngưỡng cận hạn và quy cách thùng cần quyết định trước implementation tương ứng.

### Nguồn và giới hạn

Nguồn chính thức đọc ngày 2026-10-08: [GS1 GTIN Management 1.1, Sep 2023](https://ref.gs1.org/standards/gtin-management/) (thay đổi net content và cấp đóng gói); [GS1 GDM 1.16, May 2026](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/) (measurement/PAO); [ShipBob Receiving API 2026-07](https://developer.shipbob.com/guides/receiving); [Shopify taxonomy](https://help.shopify.com/en/manual/products/details/product-category) (không thấy version/date rõ). Odoo expiration 19.0 mở không thành công ở lượt này, không dùng làm bằng chứng mới và không lặp request. Chưa đối chiếu pháp luật Việt Nam, carrier/SDS, manuals từng model hoặc deployment Render đang chạy; không tự gọi là GS1-certified, legally compliant hoặc production-ready.
