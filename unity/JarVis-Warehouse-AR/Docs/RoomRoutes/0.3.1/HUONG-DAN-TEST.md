# JarVis Routes 0.3.1 — kiểm tra lại trong phòng

## Cập nhật
- Mở `JarVis-Routes-0.3.1.apk` và chọn cập nhật ứng dụng **JarVis Routes**.
- Không gỡ bản cũ hoặc xóa dữ liệu: cần giữ tuyến A → B đang có để so sánh. Đây không phải APK ứng dụng Warehouse chính.
- Hai thẻ A/B không đổi so với 0.3.0. Dùng toàn bộ thẻ vuông **18 × 18 cm**, cố định thẳng đứng. Giữ nguyên vị trí và kích thước như lúc ghi tuyến; không chỉ dùng riêng phần QR nhỏ ở giữa.

## Thử tuyến hiện tại trước
1. Quay về A, mở **Chỉ đường**, đưa cả thẻ A vào khung.
2. Khung cyan có vạch chuyển động: camera đang được đọc để tìm mã. Vàng: đang chờ ảnh camera hoặc AR chưa ổn định. Đỏ: sai thẻ.
3. **Đã đọc A · đang khóa vị trí** chỉ xác nhận đã đọc ID; chưa được đi. Giữ máy yên, đủ sáng và thấy hết thẻ.
4. Chờ **Đã khóa A** màu xanh. Sau đó xoay máy chậm về phía lối đi, hơi hướng xuống sàn; mũi tên đầu ở phía trước, không sát chân.
5. Tuyến thẳng phải báo **ĐI THẲNG**. Xoay điện thoại không tự tạo chỉ dẫn rẽ đường; lời nhắc xoay máy được tách riêng. Tuyến có góc rẽ thật vẫn hiển thị rẽ phù hợp.
6. Đi chậm theo tuyến tới B, kiểm tra khoảng cách giảm và biểu tượng B giữ đúng vị trí. Khoảng cách/độ cao AR là ước lượng, không phải phép đo chính xác.

## Nếu cần ghi tuyến khác
**Ghi tuyến → xác nhận ghi lại → khóa A → đi tới B → khóa B → xác nhận đã lưu.** Chỉ thay tuyến hoàn chỉnh hiện tại sau khi lượt ghi mới kết thúc và lưu thành công. Hủy hoặc mất tracking trong lúc ghi không được nối bừa vào tuyến cũ.

## Kiểm tra các lỗi vừa sửa
- Che camera rất ngắn khi đang đứng yên: mũi tên phải tạm ẩn, tiến độ không tự chạy; chỉ hiện lại sau khi tracking ổn định và vị trí đủ gần.
- Nếu mất vị trí lâu, nhảy vị trí hoặc rời ứng dụng: app phải nói rõ nguyên nhân và mở lại quét A ngay trong màn chỉ đường. Quay về A để khóa lại; không quét A từ vị trí khác hoặc tiếp tục đi mù.
- Khóa lại A phải đưa tiến độ về đầu tuyến, không giữ phần đã đi trong lần dẫn trước.
- Đưa B vào khi đang chờ A: phải báo sai thẻ, không bắt đầu dẫn.
- Tới gần B: khung quét A không được còn treo trên camera.

Hãy quay **màn hình Samsung** từ trước lúc chọn Chỉ đường, qua lúc khóa A đến khi tới B hoặc xuất hiện lỗi. Lượt này là để xác nhận camera/tracking ngoài đời: kiểm thử Unity trên máy tính chưa chứng minh điện thoại đã hết lỗi. Không thử ở cầu thang/đường xe chạy; ứng dụng chưa phát hiện vật cản.
