# JarVis Product UX Review

Mở **http://127.0.0.1:4198/**. Bản cũ giữ tại **http://127.0.0.1:4197/catalog.html**.

Nếu server chưa chạy, PowerShell:

```powershell
cd 'E:\unity of teep\JarVis-Warehouse-AR\Docs\ProductUxReview'
& 'C:\Program Files\nodejs\node.exe' serve.cjs 4198
```

Sản phẩm → Tạo → chọn ngành/mẫu → Điền ví dụ mẫu (hoặc tự nhập) → Kiểm tra → Lưu demo. Tem sản phẩm → chọn hàng → In / Tem hỏng. Lịch sử nằm trong Tem sản phẩm. Quản lý chỉ cho vai trò quản lý demo. Công cụ demo ở header để thử permission/offline/error.

Server chỉ GET trên whitelist, localhost; không API, không camera, không in thật. Tải lại trang xóa dữ liệu RAM. Barcode chỉ preview bố cục, chưa scan được; đừng dùng tem này trong kho thật.

Kiểm thử: chạy `node review.cjs` với cả server 4197/4198 mở, Playwright dependency sẵn trên máy; `node --test ../ProductFormMockup/catalog-schema.test.cjs`. Bằng chứng: QA_RESULTS.json và screenshots/. Báo cáo trước/sau, quyết định, kế hoạch migration/rollback: REVIEW.md.

Cập nhật sau phê duyệt 12–13/10/2026: đã review AI và đưa UI native vào route5:450 trong Unity. Chi tiết mới nhất: NOVICE_REVIEW.md. Không sửa backend/DB; mobile tạo SKU/in thật vẫn chưa nối. Play scene JarVisFigmaApp → Công cụ → Sản phẩm & tem. APK kiểm tra UI ở Builds/Android/JarVis-Warehouse.apk; build thành công không chứng minh chức năng in hoặc ghi OMS đã hoạt động.
