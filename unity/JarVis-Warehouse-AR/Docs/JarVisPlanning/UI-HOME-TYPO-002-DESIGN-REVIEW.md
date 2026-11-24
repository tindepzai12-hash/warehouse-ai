# UI-HOME-TYPO-002 — nhận xét thiết kế trước khi sửa

Review owner: `thiết kế hình ảnh (ui/ux)`; 2026-10-24 Asia/Saigon. Chỉ đọc hai ảnh và mã, không mở Unity hoặc sửa project. Đây là spec đề xuất; chat coding phải đối chiếu kết quả TMP/runtime trước khi chốt.

## Quan sát và nguyên nhân đã xác nhận

- Hai quick action dài EN chia hai dòng, hai ô còn lại một dòng. Cần căn giữa **khối chữ** trong mỗi thẻ, không ép cả bốn ô có cùng số dòng.
- `ImportedHomeDashboard.Shortcut()` cho label x=49, y=13, 104×40; auto-size từ 11 đến `13×TextScale`. Khung hẹp và autosize có thể làm cỡ chữ của ô dài khác ô ngắn; ảnh chưa chứng minh cỡ thực sau TMP.
- Câu admin trong task card nằm cao hơn icon/chevron. `PrototypeUi.Text()` mặc định `TopLeft`, và Home chưa ghi đè. Ở cỡ chữ L, thẻ tăng cao nhưng icon/chevron giữ y cố định, nguy cơ lệch trục tăng.
- Font Regular có bảng weight 700 trỏ tới Bold SDF, nên chưa có cơ sở nói font asset hỏng. Cần xác nhận asset/material/fallback runtime và dấu tiếng Việt; giữ Be Vietnam Pro trước khi có bằng chứng khác.

## Thông số đề xuất tại viewport logic 412×915

### Quick actions

Giữ grid 2 cột, gap 12; mỗi ô 177×63. Icon 24×24 tại x=13, y≈19.5; chevron 14×14 tại x=157, y≈24.5. Nhãn ở x=49, y≈10, 104×43, căn trái giữa dọc theo font metrics, margin 0. Bold 700, tracking 0. Tắt autosize để bốn ô cùng cỡ; dùng `13×TextScale` (S=11.7, M=13, L=14.56). Mục tiêu baseline hai dòng ≈1.27–1.30 lần cỡ chữ; `TMP.lineSpacing` không phải trực tiếp tỉ lệ này, phải đo render thực.

Ngắt dòng đề xuất: EN `Inbound /` + `Outbound`, `Check` + `inventory`, `Tasks`, `Report issue`; VI `Nhập /` + `Xuất kho`, `Tra tồn kho`, `Nhiệm vụ`, `Báo cáo` + `sự cố` nếu cần tại cỡ L. Ký hiệu `+` ở đây biểu thị hai dòng, không phải ký tự hiển thị. Giữ nghĩa và bản dịch hiện tại. Đo sơ bộ bằng TTF Bold: ở cỡ L `Report issue` ≈92.2 px, `Tra tồn kho` ≈84.3 px, còn `Báo cáo sự cố` ≈103.3 px trên khung 104; phải đối chiếu TMP.

### Task card

Card rộng 366, cao tối thiểu 68. Nhãn x=56, y≈12, rộng khoảng 265, cao H−24, Regular 400, căn trái giữa dọc; cỡ gợi ý `14×TextScale` (S=12.6, M=14, L=15.68). Line-height thị giác mục tiêu ≈1.4 lần cỡ. Cho phép xuống dòng theo từ, không tự co chữ. Nếu nội dung dài, H nên tính từ preferredHeight + padding 24; icon 24×24 ở x=16 và chevron 18×18 ở x=337 đều căn giữa theo H. Áp dụng cho admin, chưa đăng nhập, loading, lỗi, rỗng và có nhiệm vụ; giữ nguyên callback.

## Kiểm tra sau sửa

Chụp 12 cấu hình EN/VI × Sáng/Tối × Nhỏ/Vừa/Lớn tại 412×915. Dùng cùng cấu hình cho ảnh trước/sau. Xem task card ở sáu trạng thái, đặc biệt câu lỗi dài VI cỡ L. Ghi cỡ font thực, lineCount, `isTextOverflowing`, font/material sau TMP mesh update. Kiểm tra dấu, dòng thứ ba, ellipsis, chạm mép, scroll và callback; chụp thêm 1080×2400 nếu môi trường cho phép. Review này chưa xác nhận compile, runtime sau sửa hoặc Samsung.
