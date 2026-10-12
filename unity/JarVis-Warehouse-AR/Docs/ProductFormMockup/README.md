# Mockup form sản phẩm JarVis

## Bản duyệt A–J mới — 2026-10-08

[Prototype master prompt](http://127.0.0.1:4197/catalog.html): chạy `node serve.cjs 4197`. Không đóng server/bản cũ 4196. Báo cáo đầy đủ ở `../PRODUCT_SYSTEM_A_J.md`, kết quả kiểm tra ở `CATALOG_TEST_RESULTS.md` và `CATALOG_REVIEW.json`. Bản mới tách ngành → mẫu → kiểm tra, có web schema/brand review và phục hồi/in lại mô phỏng. 134 lựa chọn (120 mẫu có tên, 14 Khác). Theo master mới: Thời trang thay Quần áo và chất tẩy rửa ở Khác, thay đổi này cần duyệt trước chuyển dữ liệu thật.

Schema-driven ở mức prototype (`catalog-schema.js`), không phải API catalog đã triển khai. Sản phẩm/receipt draft/label copies tách biệt; custom prototype chỉ ở RAM, không lưu OMS. Chỉ triển khai Unity/backend/migration sau phê duyệt. Phần bên dưới mô tả bản mockup cũ 4196 để so sánh, không là yêu cầu mới thay thế master prompt.

Bản thử giao diện để chủ dự án duyệt. Không sửa scene Unity, API hoặc dữ liệu OMS. Không cấp SKU/barcode, không gửi request lưu và không thay đổi tồn kho. Vạch ở bước xem trước chỉ minh họa bố cục, không dùng để quét.

Chạy từ thư mục này: `node serve.cjs`, mở [bản tương tác](http://127.0.0.1:4196) hoặc [các nhóm cạnh nhau](http://127.0.0.1:4196/?board=1). Server chỉ nghe localhost, chỉ phục vụ các file mockup và font đã có trong project; không phục vụ mã nguồn app hay database.

Giữ nền trắng, JarVis xanh, Be Vietnam Pro hiện có. Theo quyết định chủ dự án ngày 2026-10-08: bốn nhóm chính và một nhóm Khác, theo thứ tự Đồ uống / Quần áo / Đồ gia dụng – dụng cụ / Mỹ phẩm / Khác. Chọn nhóm trước, rồi chọn mẫu trong nhóm để điền sẵn tên và quy cách. SKU, barcode, thương hiệu và bảo quản vẫn tùy chọn. Ba bước là Chọn mẫu / Kiểm tra / Xem tem. [Xem cả ba bước](http://127.0.0.1:4196/?flow=1). Chưa triển khai vào Unity.

| Nhóm | Mẫu / loại bên trong | Trường phù hợp |
|---|---|---|
| Đồ uống | Sữa hộp, nước uống, nước trái cây; nước ngọt/khác có thể đổi ở phần tùy chọn | Dung tích/định lượng theo nhãn, hương vị, đơn vị hộp/chai |
| Quần áo | Áo, quần, giày dép | Màu, size; giày ghi cả hệ size; chất liệu tùy chọn |
| Đồ gia dụng – dụng cụ | Dụng cụ nấu ăn, thiết bị gia dụng, chất tẩy rửa | Dụng cụ: chất liệu/kích thước; thiết bị: model/phiên bản; tẩy rửa: định lượng l/kg và mùi/dòng |
| Mỹ phẩm | Chăm sóc da, trang điểm, chăm sóc cá nhân | Định lượng g/ml; dòng, màu/mã màu hoặc mùi theo loại; PAO sau mở nắp là tùy chọn riêng |
| Khác | Hàng khác, thực phẩm đóng gói, điện tử/phụ kiện | Quy cách tự mô tả; thực phẩm có định lượng/hương vị; điện tử có model/phiên bản |

15 mẫu là minh họa, không phải taxonomy chuẩn hoặc danh sách có sẵn của kho. Thực phẩm và điện tử là loại bên trong Khác, không còn nhóm cấp đầu riêng. Thiết bị vẫn nằm trong Gia dụng. Việc gộp nhóm chỉ áp dụng mockup, không đổi phân loại của dữ liệu OMS hiện có. Với hàng chưa có mẫu: sửa loại/quy cách trong mục tùy chọn hoặc chọn Hàng khác; không tự gán thuộc tính/HSD từ tên.

Tên và quy cách mẫu được điền sẵn để thử. Các mẫu thêm mới để HSD=Chưa rõ; mẫu cũ giữ ví dụ Có/Không đã có, không coi là kết luận về mọi hàng trong nhóm. Chọn PAO 12M không xác nhận HSD và không tính ngày hết hạn. Khi đổi nhóm/loại trong phần tùy chọn: giữ tên/mã, xóa thuộc tính cần xác nhận lại, HSD trở về Chưa rõ. Các mức bắt buộc vẫn là chính sách JarVis đề xuất: tên, định lượng hợp lệ khi có, màu/cỡ phù hợp cho quần áo, model cho thiết bị; thuộc tính phân biệt khác phải được người tạo đối chiếu với hàng thật. Mockup chưa có bộ kiểm tra biến thể đầy đủ của backend.

Chọn lại một mẫu sau khi đã đổi nhóm/loại của mẫu đó sẽ dùng thông tin gốc của mẫu để không mang nhầm thuộc tính sang nhóm khác. Back giữa bước kiểm tra/xem tem vẫn giữ phần đang sửa. Nháp này chỉ ở bộ nhớ trình duyệt, không bền sau reload; chưa phải lưu nháp sản phẩm thật.

Nhóm file cập nhật: `index.html`, `mockup.js`, `mockup.css`, `review.cjs`, `README.md` và ghi chú quyết định trong `../PRODUCT_FORM_RESEARCH.md`; báo cáo/ảnh sinh ra dưới `REVIEW.json` và `screenshots/`. Không sửa Unity, backend hay database.

Mẫu không phải danh mục hàng thật của kho. Có/Không HSD là dữ liệu ví dụ của mẫu, phải đối chiếu bao bì; chọn Chưa rõ thì chỉ được xem tem minh họa, chưa xác nhận. Không tự suy HSD từ nhóm hàng, không gán ngày hoặc nhiệt độ mặc định. Đổi mẫu có bản nháp riêng; sửa nhóm/loại giữ tên nhưng yêu cầu kiểm tra lại quy cách. Không có một form UI chuẩn chung bắt buộc; nội dung tham khảo ShipBob Products/Receiving và Shopify Product taxonomy, các mức bắt buộc JarVis vẫn chờ chủ kho duyệt.

Nguồn đối chiếu khi đơn giản hóa: [ShipBob Products](https://developer.shipbob.com/guides/products) yêu cầu tên và reference_id, khuyến nghị SKU; barcode tùy chọn được khuyến nghị trong hợp đồng ShipBob. [Shopify taxonomy](https://help.shopify.com/en/manual/products/details/product-category) hỗ trợ thuộc tính theo danh mục. [ShipBob Receiving](https://developer.shipbob.com/guides/receiving) gắn thông tin lô với dòng nhận. Những nguồn này không chứng nhận UI mockup hay bộ trường JarVis là chuẩn chung ngành.

Có kiểm tra tối thiểu ô bắt buộc, định lượng > 0 và giữ số 0 đầu của mã nhập thử. Chưa xác minh tính hợp lệ/ownership barcode, uniqueness SKU, phân quyền, camera, lưu bền, chuyển đổi đóng gói, cấu hình HSD hay Android. Lô/HSD thực tế vẫn thuộc nghiệp vụ nhập kho. Quy tắc đầy đủ nằm trong `../PRODUCT_FORM_RESEARCH.md`.

`node review.cjs` dùng Playwright/Edge và dependency runtime sẵn có; không tải package. Kết quả trong `REVIEW.json`, hình trong `screenshots/`. Các check này không chứng minh Unity hoặc Samsung hoạt động.
