# Kết quả kiểm tra bản duyệt Catalog — 2026-10-08

Chỉ kiểm tra prototype, schema và baseline OMS bằng database tạm. Không dùng database live, không gọi provider AI, không migration/deploy hoặc build Unity/Android.

| Lệnh | Kết quả |
|---|---|
| `node --check catalog.js` và syntax schema/review/server | PASS, exit 0 |
| `node --test catalog-schema.test.cjs` | 7/7 PASS, exit 0 |
| `node catalog-review.cjs` | 56/56 PASS, exit 0; `CATALOG_REVIEW.json` mới nhất |
| `node --test --test-isolation=none tests/oms.test.cjs tests/oms-mobile.test.cjs` tại `E:/warehouse-ai` | 2/2 PASS, exit 0; DB tạm của test; log `BASELINE_OMS_TESTS.log` |

Browser QA: đủ category/template render, search, chọn SKU minh họa chỉ đọc mã, back/draft, ml→l giữ định lượng, canonical units, không g→ml, negative quantity, NSX>HSD, expired warning, garment/shoe sizes, accessory minimal form, appliance dimensions/model, cosmetic g/receipt dates, manual typed attribute/reuse, admin config→form cùng schema, barcode cũ/copies/history/permission mô phỏng. Kiểm tra 390/412/768/1280, không overflow ngang, category targets ≥44px; không page error, external request hoặc mutation request.

Ảnh duyệt: `Catalog-Categories.png`, `Catalog-Drink.png`, `Catalog-Fashion.png`, `Catalog-Pan.png`, `Catalog-Microwave.png`, `Catalog-Cosmetics.png`, `Catalog-Other.png`, `Catalog-Recovery.png`, `Catalog-Web-Catalog.png`, `Catalog-Web-Labels.png`, `Catalog-Mobile-390.png` trong `screenshots/`. Một template-selection timeout ở kịch bản QA đã được sửa để thao tác mở nhóm trước click; không phải lỗi API. Log cuối cùng ở JSON có ưu tiên hơn báo cáo chạy thử thất bại trước đó.

Chưa được kiểm thử: encoder/PNG/barcode roundtrip mới, in vật lý, live duplicates/mutations/auth permission mới, schema migration/rollback, custom persistence, Unity compile, Samsung camera/IME, API Render hoặc APK mới. Hai test OMS có sẵn không chứng minh các chức năng mới chưa triển khai đã hoạt động.

File tạo mới: `catalog.html`, `catalog.css`, `catalog.js`, `catalog-schema.js`, `catalog-schema.test.cjs`, `catalog-review.cjs`, tài liệu này, `../PRODUCT_SYSTEM_A_J.md`; output JSON/log/ảnh ở cùng thư mục. File chỉnh: `serve.cjs` (route whitelist + port tùy chọn), `README.md` (link bản duyệt). Không thay đổi prototype cũ HTML/JS/CSS, Unity, backend hoặc dữ liệu.

APK hiện có ở `E:/unity of teep/JarVis-Warehouse-AR/Builds/Android/JarVis-Warehouse.apk` không được build hoặc xác nhận mới trong lượt này; prototype này chưa có trong APK. TODO sau khi duyệt: P0a exact product lookup + recovery/export barcode thật, rồi P0b/c schema/API/Unity và kiểm thử migration riêng trên clone.
