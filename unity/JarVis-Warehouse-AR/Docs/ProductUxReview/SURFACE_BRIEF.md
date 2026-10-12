# Mockup Sản phẩm và tem — development only

Mode: Operate. White–blue JarVis, Be Vietnam Pro. Nhân viên dùng điện thoại dưới ánh sáng kho: nền trắng, mực navy, chọn/CTA xanh, tương phản rõ. Quyền tự quyết thiết kế đã được ghi trong prompt; không chờ thêm vòng hỏi thẩm mỹ.

## Direction contract
THESIS: Một sản phẩm, một nguồn mã; in và in lại cùng khu vực. Không dùng nhiều bản sao form theo thiết bị.
OWN-WORLD: White workspace, blue selected navigation, navy type, compact row lists, 48px controls. Không glass trang trí.
STORY: Chọn mẫu, nhận diện chính xác SKU, xác nhận; khi cần tem tìm sản phẩm đã lưu, không tạo lại sản phẩm.
FIRST VIEWPORT: Header JarVis và công cụ demo; ba mục Sản phẩm / Tem sản phẩm / Quản lý; bảng sản phẩm mẫu và CTA Tạo sản phẩm. Mobile chuyển nav thành hàng ngang. Signature interaction: lựa chọn mẫu hiện ngay trong đường tiến trình ba bước và bản xem tem giữ nguyên nguồn mã.
FORM: Standard operational workspace, code-led từ brief pinned; không concept random hoặc asset generation. Tất cả fixture gắn nhãn mẫu.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance.

## Architecture before implementation
Old: Tạo sản phẩm → ngành → mẫu → thông tin → review; Phục hồi tem; Web Danh mục; Web In tem/lịch sử.
New: Sản phẩm → danh sách / tạo (Chọn mẫu → Điền thông tin → Xác nhận) / thành công. Tem sản phẩm → tìm / xem / in hoặc in lại / lịch sử chi tiết. Quản lý → mẫu, thuộc tính, cấu hình nháp.
Separate: lô, HSD/NSX và nhận hàng; quyền thật từ backend; mất dữ liệu mã gốc không được tự sinh mã gọi là phục hồi.
Flows: A create; B print existing; C reprint damaged; D history/detail; E manual fallback. Each uses explicit back, demo busy/error/offline, empty search and success where relevant.
Unresolved: nhóm cleaning hiện nằm Other trong schema cũ; chủ kho cần duyệt chuyển home; định nghĩa packaging conversion; quyền in của nhân viên; máy in/encoder thật. Không thay schema nguồn.
