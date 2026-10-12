# JarVis — Form sản phẩm/SKU/barcode theo nhóm hàng

Ngày nghiên cứu: 2026-10-08. Trạng thái: **ĐỀ XUẤT CHỜ CHỦ DỰ ÁN DUYỆT**.

Chỉ tạo tài liệu nghiên cứu. Chưa sửa Unity, API, schema hay dữ liệu kho; chưa triển khai các chức năng được mô tả dưới đây. Các ví dụ là dữ liệu minh họa, không được seed vào kho thật.

### Quyết định tổ chức mockup của chủ dự án — 2026-10-08

Danh sách bảy nhóm trong nghiên cứu gốc bên dưới là lịch sử đề xuất, không còn là danh sách cấp đầu của mockup. Chủ dự án chọn **bốn nhóm chính + Khác**, theo thứ tự:

1. **Đồ uống**: sữa, nước uống, nước trái cây/nước ngọt; định lượng theo nhãn, hương vị, đơn vị quản lý.
2. **Quần áo**: áo, quần; giày dép giữ như loại phụ; màu, size/hệ size, chất liệu tùy chọn.
3. **Đồ gia dụng – dụng cụ**: dụng cụ, thiết bị, **chất tẩy rửa** chung một nhóm; giữ bộ thuộc tính riêng theo từng loại như ma trận mục 3.
4. **Mỹ phẩm**: chăm sóc da, trang điểm, chăm sóc cá nhân; định lượng, dòng/màu/mùi; PAO riêng, không thay HSD.
5. **Khác**: quy cách tự mô tả; thực phẩm và điện tử nằm dưới nhóm này, vẫn dùng thuộc tính phù hợp khi được chọn.

Đây là lựa chọn tổ chức giao diện JarVis, không tuyên bố tương đương taxonomy GS1/Shopify. Không bỏ thông tin nghiệp vụ chỉ vì gộp nhóm. Chưa áp dụng migration, Unity hay API. Bản tương tác và quy tắc hiện hành nằm trong `ProductFormMockup/README.md`.

## 1. Kết luận và giới hạn bằng chứng

- Dùng một form động, chọn nhóm/loại hàng trước, không tạo bảy màn nhập độc lập. Shopify có thuộc tính theo danh mục và liên kết thuộc tính với biến thể. Đây là căn cứ cho cách tổ chức, không phải một chuẩn bắt buộc cho mọi kho. [Shopify — Product taxonomy](https://help.shopify.com/en/manual/products/details/product-category)
- SKU nhận diện mặt hàng cụ thể cần lấy/đóng gói. Shopify yêu cầu SKU khác nhau cho các biến thể trong hệ thống của họ. JarVis đề xuất mỗi biến thể quản lý riêng có SKU riêng. [Shopify — SKU](https://help.shopify.com/en/manual/products/details/sku)
- ShipBob yêu cầu tên và reference_id khi tạo channel product, khuyến nghị dùng SKU cho reference_id; barcode là trường tùy chọn được khuyến nghị. Không suy ra rằng cả ngành fulfillment bắt buộc cùng bộ trường. [ShipBob — Products](https://developer.shipbob.com/guides/products)
- GS1 định nghĩa định lượng trên bao bì là giá trị kèm đơn vị, có thể thể hiện cấu hình multipack; cũng phân biệt thời gian sử dụng sau mở nắp. JarVis chọn phần thuộc tính cần cho kho, không sao chép toàn bộ hồ sơ bán hàng. [GS1 — GDM 1.16](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/)
- ShipBob gắn lot_number và lot_date với dòng hàng nhận; Odoo 19 quản lý ngày hết hạn trên lô/serial. Hai nguồn hỗ trợ tách dữ liệu lô khỏi dữ liệu sản phẩm, không chứng minh chức năng này đã hoàn chỉnh trong JarVis. [ShipBob — Receiving](https://developer.shipbob.com/guides/receiving), [Odoo 19 — Expiration dates](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/product_tracking/expiration_dates.html)

Các chữ **bắt buộc**, **chặn** và **mặc định** trong phần thiết kế bên dưới là chính sách JarVis được đề xuất để duyệt, trừ khi ghi rõ quy định của nguồn. Chưa nghiên cứu đầy đủ pháp luật Việt Nam hay hợp đồng từng chủ hàng; không tuyên bố bộ form này đáp ứng mọi yêu cầu pháp lý/vận chuyển.

## 2. Trường chung và hành vi trên điện thoại

| Trường | Kiểu nhập | Điều kiện JarVis đề xuất | Ví dụ | Căn cứ |
|---|---|---|---|---|
| Nhóm hàng | Chọn một trong bảy nhóm | Bắt buộc với sản phẩm mới | Đồ uống | Tổ chức theo danh mục của Shopify; danh sách nhóm là lựa chọn JarVis |
| Loại hàng | Chọn loại trong nhóm; có Khác | Bắt buộc; Khác phải mô tả ngắn | Sữa hộp | Đề xuất JarVis để chọn đúng mẫu thuộc tính |
| Tên sản phẩm | Văn bản | Bắt buộc; gợi ý ghép thuộc tính, người dùng xác nhận | Sữa tươi không đường 180 ml | ShipBob Products |
| Đơn vị quản lý tồn | Danh sách kiểm soát | Bắt buộc; hiển thị rõ một đơn vị đang tính là gì | Hộp | Đề xuất JarVis; không đồng nhất với ml/kg định lượng |
| SKU | Tự cấp hoặc nhập mã đang dùng | Phải có khi lưu; người dùng không cần nhập nếu tự cấp | JV-000001 | Shopify SKU; cơ chế tự cấp là đề xuất JarVis |
| Nguồn barcode | Mã nội bộ / mã có sẵn | Bắt buộc chọn; ưu tiên giữ mã đã dùng | Mã nhà sản xuất | Đề xuất JarVis dựa trên nhu cầu nhận diện sản phẩm |
| Giá trị barcode | Tự cấp hoặc nhập/quét | Chỉ nhập khi dùng mã có sẵn; lưu dưới dạng chuỗi | Giữ nguyên các số 0 đầu | Đề xuất JarVis; không ép mọi mã thành GTIN |
| Thương hiệu | Văn bản hoặc chọn giá trị đã có | Tùy chọn; nên nhập nếu giúp tránh nhầm hàng | Thương hiệu A | GDM có thông tin thương hiệu; mức bắt buộc là lựa chọn JarVis |
| Theo dõi HSD | Có / Không | Người tạo phải xác nhận; không tự quyết theo nhóm | Có | Phân tách cấu hình sản phẩm/ngày lô theo Odoo; UI là đề xuất JarVis |
| Theo dõi lô | Hiển thị cấu hình đang có | Giữ chính sách cũ; HSD= Có thì phải có lô; HSD= Không không tự tắt theo dõi lô | Đang theo dõi lô | Đề xuất tương thích dữ liệu JarVis hiện tại |
| Bảo quản | Không yêu cầu riêng / theo hướng dẫn nhà sản xuất | Bắt buộc mô tả nếu đã xác nhận có yêu cầu riêng | Giữ mát theo bao bì | GDM có thuộc tính điều kiện/nhiệt độ; không tự gán nhiệt độ |
| Ghi chú nhận diện | Văn bản ngắn | Tùy chọn | Bao bì mới, cùng mã đã xác nhận | Đề xuất JarVis |

Liên kết nguồn cho bảng: [Shopify taxonomy](https://help.shopify.com/en/manual/products/details/product-category), [Shopify SKU](https://help.shopify.com/en/manual/products/details/sku), [ShipBob Products](https://developer.shipbob.com/guides/products), [GS1 GDM](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/), [Odoo expiration](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/product_tracking/expiration_dates.html).

Trong UI: một màn cuộn với ba cụm Thông tin chung / Thuộc tính theo nhóm / Mã và quản lý. Trường không liên quan được ẩn; không dùng một biểu mẫu dài chứa mọi thuộc tính. Trước khi đổi nhóm làm mất dữ liệu đã nhập, hỏi xác nhận. Không tự xóa thương hiệu, tên, SKU/barcode đang có. Nút xem trước không lưu backend; nút Xác nhận lưu mới tạo dữ liệu. Sản phẩm có sẵn được tự điền, không yêu cầu tạo lại mã.

## 3. Ma trận thuộc tính theo nhóm

Đây là ma trận tối thiểu do JarVis đề xuất, không phải danh sách bắt buộc ngành/pháp luật. Cách tổ chức tham khảo [Shopify category metafields](https://help.shopify.com/en/manual/products/details/product-category); thuật ngữ định lượng/quy cách tham khảo [GS1 GDM](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/).

| Nhóm/loại | Trường hiện thêm | Bắt buộc có điều kiện | Tùy chọn/phần nâng cao |
|---|---|---|---|
| Đồ uống | Dung tích hoặc định lượng thực ghi trên nhãn; đơn vị; hương vị; quy cách | Với hàng đóng gói: khai báo quy cách nhận diện; hương vị nếu là thuộc tính phân biệt SKU | Vật liệu bao bì, dễ vỡ; nhiệt độ theo nhà sản xuất |
| Thực phẩm | Định lượng g/kg/ml/đếm hoặc quy cách ghi trên nhãn; loại/hương vị | Quy cách nhận diện hàng đóng gói; thuộc tính phân biệt các SKU | Dị ứng, điều kiện bảo quản, giới hạn ngày còn hạn nếu hợp đồng yêu cầu |
| Quần áo/giày dép | Màu; size; hệ size nếu cần; chất liệu | Màu/size khi hàng có biến thể tương ứng; cho chọn Một cỡ/Không áp dụng | Chất liệu; số đo thêm; không ép giới tính, mùa, mô tả quảng cáo |
| Gia dụng — dụng cụ | Chất liệu; kích thước/dung tích; màu; model | Thuộc tính cần phân biệt hàng, ví dụ nồi 20 cm và 24 cm | Dễ vỡ, hướng đặt, thông tin đóng gói |
| Gia dụng — thiết bị | Model; thông số phân biệt phiên bản | Model hoặc mã phiên bản nhận diện; thông số nếu có nhiều phiên bản | Serial, điện áp/công suất, bảo hành; chỉ bật theo nghiệp vụ đã duyệt |
| Gia dụng — chất tẩy rửa | Khối lượng/dung tích theo nhãn; mùi/dòng; quy cách | Quy cách và thuộc tính phân biệt SKU; không mặc định mọi chất tẩy rửa không có HSD | Yêu cầu xử lý, chứng từ an toàn nếu hợp đồng/vận chuyển cần |
| Mỹ phẩm/chăm sóc | Định lượng; màu/mùi/dòng; thông tin PAO nếu nhãn có | Quy cách và biến thể nếu có; HSD được xác nhận riêng | PAO, điều kiện bảo quản; không dùng PAO thay HSD |
| Điện tử | Model; cấu hình như dung lượng/màu nếu có | Model/cấu hình cần để phân biệt SKU | Serial, pin, phụ kiện kèm, đóng gói/vận chuyển |
| Khác | Loại hàng; quy cách; thuộc tính nhận diện | Loại và thông tin đủ phân biệt hàng; không ép size/dung tích | Thuộc tính bổ sung đã có tên/kiểu dữ liệu được duyệt |

Nguyên tắc: không bắt mọi món gia dụng điền chất liệu/kích thước, không bắt mọi thực phẩm nhập ml, không tự suy điều kiện bảo quản từ từ khóa. Với size không áp dụng phải chọn rõ, không dùng ô trống như một biến thể hợp lệ.

Khối lượng vận chuyển cả bao bì khác định lượng sản phẩm. Bản đầu ưu tiên hàng đóng gói đếm bằng số nguyên; hàng bán cân theo số lẻ cần nâng cấp hợp đồng số lượng tồn trước khi bật. Database hiện tại dùng số lượng nguyên, không được âm thầm chuyển kg/ml thành số lượng tồn.

## 4. Tách dữ liệu sản phẩm, lô, nhập kho và tem

| Lớp dữ liệu | Thông tin nằm ở đây | Không đặt ở đây |
|---|---|---|
| Sản phẩm/SKU | ID ổn định; nhóm/loại; tên; đơn vị tồn; thuộc tính biến thể/quy cách; SKU/barcode; chính sách HSD/lô; hướng dẫn bảo quản | HSD cụ thể của mọi lô; tồn kho nhập tay |
| Lô của sản phẩm | Số lô; ngày sản xuất nếu có; HSD/ngày theo nhãn; liên kết sản phẩm | SKU mới chỉ vì khác HSD |
| Phiếu nhập/dòng nhận | Sản phẩm; lô; số lượng; đơn vị nhận; vị trí; thời điểm/nhân viên/chứng từ theo nghiệp vụ | Số bản tem như số lượng hàng |
| Cấu hình xuất tem | Mẫu; kích thước; thông tin in; số bản nếu có; PNG barcode của sản phẩm đã lưu | Tự đổi SKU/barcode; tự cộng tồn |

Căn cứ tách lô/nhận: [ShipBob Receiving](https://developer.shipbob.com/guides/receiving), [Odoo 19 Expiration](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/product_tracking/expiration_dates.html). ShipBob lot_date có thể là ngày hết hạn hoặc sản xuất theo hợp đồng của họ; JarVis không gộp hai nghĩa đó thành một trường mơ hồ.

Đề xuất nhận hàng có HSD: chọn sản phẩm → nhập số lô, HSD, số lượng, vị trí; NSX tùy chọn. Nhiều lô/HSD trong một lần nhận thì tách dòng. Lô cũ phải hiện dữ liệu hiện có, không cho ghi đè HSD khác bằng một lần nhập thông thường. Barcode sản phẩm chỉ nhận diện SKU; sau quét vẫn phải xác nhận lô khi nghiệp vụ cần. Không thêm barcode cho lô trong phạm vi này.

PAO như 12M là khoảng thời gian sử dụng sau mở nắp, không phải ngày hết hạn của lô hàng chưa mở. Không tính HSD = ngày nhận + PAO. [GS1 GDM 1.16, mục 7.7/BMS 3709](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/)

## 5. SKU, GTIN, barcode và đóng gói

- SKU nội bộ: tự cấp từ backend khi lưu hoặc giữ mã có sẵn; không nhúng tên/nhóm/HSD vào mã cấp tự động. ID sản phẩm là liên kết ổn định, đổi tên không tạo lại mã. Đề xuất một SKU cho mỗi phiên bản hàng cần lấy riêng. [Shopify SKU](https://help.shopify.com/en/manual/products/details/sku)
- Barcode nội bộ: đề xuất Code128 mang mã định danh ổn định, không chứa giá/tồn/vị trí/HSD trong phiên bản này. Đây là quyết định thiết kế JarVis, không phải chứng nhận GS1. Code128 thông thường không tự trở thành GS1-128; GS1-128 có cấu trúc FNC1/AI riêng. [GS1 US — GS1-128](https://www.gs1us.org/upcs-barcodes-prefixes/gs1-128)
- GTIN là định danh; EAN/UPC là dạng ký hiệu mang dữ liệu nhận diện. Giữ mã nhà sản xuất hợp lệ; không tự tạo GTIN giả hoặc tuyên bố mã nội bộ dùng được trên mọi sàn. [GS1 — Barcodes](https://www.gs1.org/standards/barcodes), [GS1 — EAN/UPC](https://www.gs1.org/standards/barcodes/ean-upc)
- GS1 yêu cầu GTIN mới khi định lượng khai báo thay đổi theo quy tắc của tiêu chuẩn; thay số lượng trong case ảnh hưởng GTIN ở cấp đóng gói tương ứng. Không suy ra rằng sửa mọi nội dung mô tả đều phải đổi barcode. [GS1 GTIN Management 1.1, mục 2.3 và 2.8](https://ref.gs1.org/standards/gtin-management/)

JarVis đề xuất tách SKU khi màu/size/model/dung tích/hương vị/quy cách là phiên bản hàng được quản lý riêng. Không tách SKU chỉ vì lô mới, HSD mới hoặc chuyển kệ. Việc cấp/đổi GTIN của nhà sản xuất không do JarVis tự quyết.

Phân biệt đóng gói:

- Một hộp sữa là một đơn vị; 180 ml là định lượng trong hộp, không phải 180 đơn vị tồn.
- Lốc cố định được đặt hàng/lấy hàng như một mặt hàng riêng: đề xuất SKU riêng, ghi rõ số hộp trong lốc; không tự tạo quan hệ trừ tồn hộp ở bản đầu.
- Thùng chỉ để vận chuyển các hộp hiện có: không tự tạo SKU sản phẩm mới. Muốn nhận 2 thùng = 96 hộp cần quy đổi đã được duyệt, không đoán mỗi thùng bao nhiêu hộp.
- Nếu thùng là mặt hàng bán nguyên thùng: xác nhận rõ đơn vị quản lý và chính sách tồn; không quản lý đồng thời thùng và hộp như hai kho độc lập cùng một lượng hàng.
- In 100 tem chỉ xuất bản tem, không nhập 100 sản phẩm. Số bản là tùy chọn xuất/in, không là trường tạo sản phẩm. Bản đầu ưu tiên xuất một PNG chuẩn, chưa cam kết tích hợp máy in.

## 6. Bốn mẫu form để duyệt

### 6.1 Sữa hộp 180 ml có HSD

```text
Nhóm / Loại          Đồ uống / Sữa hộp
Tên                  Sữa tươi không đường
Đơn vị tồn           Hộp
Thương hiệu          [Tùy chọn]
Dung tích            180 ml
Hương vị             Không đường
Quy cách             1 hộp
Theo dõi HSD         Có
Theo dõi lô          Bắt buộc theo chính sách HSD
Bảo quản             Điền theo nhãn; không tự giả định sữa phải giữ mát
SKU                  Tự cấp / mã đang có
Barcode              Tự cấp nội bộ / nhập hoặc quét mã nhà sản xuất
                     [Xem trước] [Xác nhận lưu]
```

Lần nhập minh họa sau khi sản phẩm được lưu: lô SUA-A; 100 hộp; HSD 20/12/2026; vị trí chọn từ kho thật; NSX nếu có. Lô SUA-B cùng SKU có thể có HSD 15/02/2027. Không nhập hai HSD đó vào trường dung tích hay tạo hai SKU vì khác lô.

### 6.2 Áo thun đen size M

```text
Nhóm / Loại          Quần áo–giày dép / Áo thun
Tên                  Áo thun cổ tròn
Đơn vị tồn           Cái
Màu                  Đen
Size                 M
Chất liệu            [Tùy chọn]
Tên nhận diện gợi ý   Áo thun cổ tròn – Đen – M
Theo dõi HSD         Không, do người tạo xác nhận
Theo dõi lô          Giữ cấu hình kho hiện tại
SKU / Barcode        Tự cấp hoặc dùng mã đã có
```

Áo đen L được lưu thành SKU khác. Không yêu cầu điền dung tích/ngày hết hạn; không tự bỏ các lô đã tồn tại.

### 6.3 Nồi inox

```text
Nhóm / Loại          Gia dụng / Dụng cụ nấu ăn
Tên                  Nồi inox
Đơn vị tồn           Cái
Chất liệu            Inox [tùy chọn nếu không cần để phân biệt SKU]
Đường kính           20 cm [bắt buộc khi kho có phiên bản khác đường kính]
Model                [Nếu dùng để phân biệt phiên bản]
Theo dõi HSD         Không, do người tạo xác nhận
SKU / Barcode        Tự cấp hoặc dùng mã đã có
```

Nồi 24 cm là SKU khác nếu được bán/lấy riêng. Không bắt nhập size quần áo hay HSD chỉ vì cùng một form.

### 6.4 Nước giặt

```text
Nhóm / Loại          Gia dụng / Chất tẩy rửa / Nước giặt
Tên                  Nước giặt hương hoa
Đơn vị tồn           Chai
Định lượng           3 l [minh họa; nhãn ghi kg thì dùng kg]
Mùi / Dòng           Hương hoa [nếu phân biệt SKU]
Quy cách             1 chai
Theo dõi HSD         Có/Không — đối chiếu nhãn, không mặc định
Bảo quản / xử lý      Theo hướng dẫn nhà sản xuất
SKU / Barcode        Tự cấp hoặc dùng mã đã có
```

Nếu chọn HSD= Có: yêu cầu lô/HSD ở lần nhận; không nhập một ngày hết hạn chung cho mọi chai của SKU này. Không tự phân loại mọi nước giặt là hàng nguy hiểm vận chuyển.

## 7. Kiểm tra khi lưu và khi nhận hàng

Đây là quy tắc JarVis đề xuất, phải kiểm tra cả phía app và backend; app không phải cơ quan cấp mã hay nguồn xác nhận duy nhất.

| Trường hợp | Xử lý đề xuất |
|---|---|
| Thiếu nhóm, loại, tên, đơn vị | Báo ngay dưới trường; không gửi lưu |
| SKU hoặc barcode trùng | Backend kiểm tra uniqueness trong giao dịch; hiện sản phẩm đang sở hữu mã, không ghi đè |
| Hai người cùng dùng tự cấp mã | Cấp tại backend; ràng buộc duy nhất; requestId chống tạo hai sản phẩm khi gửi lại cùng yêu cầu |
| Định lượng/kích thước | Nếu có nhập thì phải > 0 và kèm đơn vị hợp lệ; không âm, không đổi ml thành tồn hộp |
| Thiếu thuộc tính phân biệt phiên bản | Yêu cầu hoàn thiện; không gán cùng SKU cho đen/M và đen/L |
| Cùng bộ thuộc tính nhưng chưa chắc cùng sản phẩm | Gợi ý kiểm tra trùng; không tự hợp nhất chỉ dựa vào tên/màu/size |
| Mã có sẵn | Giữ số 0 đầu; kiểm tra theo loại mã thực tế. EAN/UPC kiểm tra độ dài/check digit tương ứng; check digit đúng không chứng minh quyền sở hữu GTIN |
| Chưa có mạng | Giữ nháp nếu hỗ trợ; không hiển thị đã cấp mã/đã đồng bộ khi backend chưa xác nhận |
| HSD= Có nhưng thiếu lô/HSD khi nhận | Chặn xác nhận dòng nhận; không tự đoán |
| NSX/HSD | Ngày lịch hợp lệ; NSX không sau HSD nếu cả hai được cung cấp; NSX tương lai cần xử lý lỗi khi nhận |
| Nhãn chỉ có tháng/năm | Không tự thêm ngày 01 hoặc cuối tháng; giữ thông tin gốc/chờ chính sách được duyệt trước khi dùng để xuất hàng |
| Lô cũ, nhập HSD khác | Báo xung đột; sửa thông tin lô phải là thao tác có quyền, xác nhận và audit riêng |
| Hàng hết hạn | Cảnh báo/chặn nhận vào luồng hàng có thể xuất; luồng nhận cách ly/đổi trả cần được duyệt riêng. Không tạo SKU mới để lách cảnh báo |
| Quét sản phẩm | Lookup chính xác mã đã lưu, kể cả sản phẩm chưa có balance/tồn bằng 0; sau đó chọn lô nếu cần, không tự cộng/trừ tồn |
| Đổi nhóm/tên hoặc xuất lại PNG | Không tự đổi mã đã cấp; xác nhận nếu bỏ thuộc tính đã nhập; xuất lại cùng giá trị barcode |

Nguồn cho khái niệm check digit: [GS1 check digit calculator](https://www.gs1.org/services/check-digit-calculator) — tìm kiếm chính thức trả về mô tả, nhưng lần mở toàn trang gặp lỗi; không dùng làm bằng chứng cho quyền sở hữu mã. Quy tắc cụ thể sẽ phải kiểm thử theo định dạng hỗ trợ, không dùng một regex chung thay cho kiểm tra GTIN.

## 8. Phạm vi làm trước, làm sau và các khoảng trống hiện tại

### Bản tối thiểu sau khi chủ dự án duyệt

1. Form động theo nhóm/loại, các trường chung và thuộc tính nhận diện; một lần lưu một SKU. Không tạo hàng loạt biến thể tự động.
2. Bổ sung metadata/policy cần thiết có migration tương thích, giữ nguyên sản phẩm/lô/balance hiện tại và ID liên kết.
3. API theo session/quyền hiện có để cấp SKU/barcode, kiểm tra trùng, lưu bền và trả xác nhận. Không giả thành công khi chỉ tạo ảnh local.
4. Product Detail có barcode, copy/xuất PNG sạch; mã nhà sản xuất giữ đúng symbology hỗ trợ. Không phủ kính, tô xanh vạch hay kéo giãn mã.
5. Scanner lookup sản phẩm chính xác; phân biệt mã không hợp lệ, không tìm thấy và lỗi kết nối; hàng chưa có tồn vẫn tìm được.
6. Luồng nhập kho hiện có nhận đúng lô/HSD khi policy yêu cầu; không nhét thao tác tăng tồn vào nút tạo barcode.
7. Kiểm tra theo nhóm thay đổi: backend/unit/API và Unity compile; kiểm tra ảnh PNG giải mã được. Camera/quyền/bàn phím thực tế trên Samsung kiểm tra khi chủ dự án sẵn sàng.

### Để sau hoặc cần nghiệp vụ riêng

- Quy đổi hộp/lốc/thùng, tách/gộp đóng gói, bundle và hàng bán cân theo số lẻ.
- FEFO, ngưỡng gần hết hạn, số ngày còn hạn tối thiểu theo chủ hàng và ngày cảnh báo. Odoo có FEFO, nhưng đây không phải bằng chứng JarVis đã hỗ trợ. [Odoo 19 expiration](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/product_tracking/expiration_dates.html)
- Serial, bảo hành, pin/hàng nguy hiểm, chứng từ an toàn, kích thước/khối lượng vận chuyển, hãng vận chuyển.
- Ảnh sản phẩm, import Excel, nhiều barcode alias, nhiều chủ hàng/namespace SKU, cấp biến thể hàng loạt, máy in tem.
- Barcode lô/đơn/vị trí/nhân viên/pallet ngoài phạm vi lần này.

### Đối chiếu project đang có

- `oms_products` hiện có id, sku, name, barcode, unit, lot_tracking_required; SKU và barcode đã có ràng buộc duy nhất toàn bảng. Chưa có nhóm/loại/thuộc tính/HSD-policy/brand trong bảng này. Căn cứ: `E:/warehouse-ai/oms/schema.sql:52`.
- `oms_lots` đã có lot_code, manufacture_date, expiry_date; balance liên kết sản phẩm/lô/vị trí. Giữ cấu trúc và dữ liệu đó. Căn cứ: `E:/warehouse-ai/oms/schema.sql:68`.
- Create product đang yêu cầu mã có sẵn và quyền manager ở POST /products; chưa có bằng chứng luồng tự cấp + form mobile hoàn chỉnh. Căn cứ: `E:/warehouse-ai/oms/routes.js:315`.
- Inventory hiện tìm LIKE qua balance; không thể dùng nó như bằng chứng lookup chính xác mọi sản phẩm chưa có stock. Căn cứ: `E:/warehouse-ai/oms/store.js:363`.
- Không chuyển database sang demo, không seed các ví dụ, không đổi credentials, không deploy hay gọi dịch vụ AI từ việc nghiên cứu này.

### Những quyết định cần chủ kho duyệt

1. Duyệt bảy nhóm, tách Gia dụng thành dụng cụ/thiết bị/chất tẩy rửa, và form động tối thiểu?
2. Duyệt SKU tự cấp mặc định, cho giữ SKU/mã nhà sản xuất đang dùng; một mã chính cho một SKU ở bản đầu?
3. Đơn vị tồn ban đầu là hàng đóng gói đếm nguyên, chưa tự quy đổi thùng/lốc, chưa quản lý hàng cân lẻ?
4. Ai có quyền tạo sản phẩm/mã trong app? Giữ quyền quản lý hiện tại cho đến khi chủ kho quyết định, không tự cấp quyền cho nhân viên.
5. Nếu nhiều chủ hàng: phạm vi uniqueness SKU/barcode thế nào? Hiện database dùng global uniqueness, chưa xác minh nghiệp vụ đa chủ hàng.
6. Hàng hết hạn, tháng/năm không có ngày, PAO, ngưỡng gần hết hạn và ngày kho theo múi giờ nào: cần chính sách cụ thể, không tự đặt 30 ngày hay đoán ngày.

## 9. Nguồn, phiên bản và hạn chế

Các nguồn được tra ngày 2026-10-08. Không dùng kết quả AI khác làm bằng chứng.

| Nguồn | Phiên bản/ngày có thể xác nhận | Dùng để làm gì |
|---|---|---|
| [GS1 GDM Attribute Implementation Guideline](https://ref.gs1.org/guidelines/gdm-implementation/1.16.0/) | Release 1.16, ratified May 2026 | Định lượng, quy cách, thuộc tính bảo quản, PAO; không biến mọi thuộc tính thành bắt buộc JarVis |
| [GS1 GTIN Management](https://ref.gs1.org/standards/gtin-management/) | Release 1.1, Sep 2023; bản tiêu chuẩn đọc từ URL hiện hành | Thay đổi định lượng và pack/case; không mặc định là bản mới hơn chỉ vì năm truy cập |
| [ShipBob Products](https://developer.shipbob.com/guides/products) | API 2026-07; trang ghi updated July 17, 2026 | SKU/reference, tên, barcode; chỉ là hợp đồng ShipBob |
| [ShipBob Receiving](https://developer.shipbob.com/guides/receiving) | API 2026-07; trang ghi updated July 17, 2026 | Lô/ngày/số lượng ở dòng nhận |
| [Shopify category](https://help.shopify.com/en/manual/products/details/product-category), [SKU](https://help.shopify.com/en/manual/products/details/sku) | Trang trợ giúp hiện hành; không thấy ngày phát hành rõ | Danh mục/thuộc tính/biến thể; không tuyên bố là chuẩn chung ngành |
| [Odoo expiration](https://www.odoo.com/documentation/19.0/applications/inventory_and_mrp/inventory/product_management/product_tracking/expiration_dates.html) | Tài liệu Odoo 19.0; không khẳng định là phiên bản Odoo mới nhất | Tách cấu hình sản phẩm với ngày của lô, FEFO |
| [GS1 US GS1-128](https://www.gs1us.org/upcs-barcodes-prefixes/gs1-128) | Trang chính thức; không thấy ngày phát hành rõ | Phân biệt Code128 và GS1-128 |
| [GS1 barcodes](https://www.gs1.org/standards/barcodes), [EAN/UPC](https://www.gs1.org/standards/barcodes/ean-upc) | Kết quả tìm kiếm chính thức; không suy ngày cập nhật từ ngày crawl | Phân biệt định danh và dạng ký hiệu |

Một số trang GS1 và Odoo Packaging mở trực tiếp không thành công; không lặp request hay coi nội dung không đọc được là đã xác minh. Quy tắc pack/case dùng bản GS1 GTIN PDF đã đọc. Chưa đối chiếu yêu cầu pháp lý Việt Nam, từng carrier, SDS cụ thể, hoặc dữ liệu hàng thật của chủ kho. Chưa chạy integration/Unity compile/Android build vì tài liệu này chưa thay đổi code hay hợp đồng chạy thật.
