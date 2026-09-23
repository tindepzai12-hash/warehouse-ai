# Giọng nói và giao diện bản thử

Website: https://warehouse-ai-fmjc.onrender.com — Render Singapore, Free.

1. Mở website, nhập mã truy cập thử nghiệm từ file .local-token (không phải OpenAI key).
2. Chọn ngôn ngữ trả lời: Việt, Anh hoặc Trung.
3. Nhập câu hỏi, hoặc bấm **Nói câu hỏi** rồi cho phép micro nếu muốn.
4. Nói xong, bấm **Dừng ghi giọng nói**. Kiểm tra/sửa văn bản rồi bấm **Gửi câu hỏi**. Không tự gửi bản nhận dạng.
5. Bấm **Nghe trả lời** để đọc câu trả lời được chọn, hoặc **Nghe câu trả lời này** dưới từng tin nhắn. **Dừng đọc** dừng âm thanh. **Bật giọng đọc tự động** mặc định tắt; lựa chọn được ghi nhớ trong trình duyệt (không lưu token).
6. Hỏi tiếp trong cùng phiên để giữ ngữ cảnh. Nút **Phiên mới** bắt đầu lại.
7. Nguồn tham khảo ở dưới hội thoại. Nhiệm vụ, sự kiện giả lập, locale và JSON nằm trong phần nâng cao; không cần chọn sự kiện mỗi lần hỏi.

Nhân vật minh họa chuyển trạng thái sẵn sàng / nghe / xử lý / đọc. Thiết bị bật giảm chuyển động sẽ không chạy hoạt ảnh.

## Giới hạn

- Đây là nhập câu hỏi bằng giọng nói và đọc câu trả lời, chưa phải voice chat hai chiều liên tục.
- SpeechRecognition phụ thuộc trình duyệt, quyền micro, mạng và ngôn ngữ hỗ trợ. Khi không hỗ trợ, nhập chữ vẫn hoạt động. Dùng HTTPS trên Render hoặc localhost khi chạy máy cá nhân.
- Trình duyệt có thể gửi âm thanh tới dịch vụ nhận dạng của nhà cung cấp. Backend của ứng dụng chỉ nhận văn bản; không lưu hoặc gửi audio tới OpenAI. Không đảm bảo xử lý giọng nói offline.
- Micro chỉ bật khi bấm nút, tối đa 45 giây trước khi yêu cầu dừng (thêm 3 giây chờ kết quả cuối). Đổi ngôn ngữ, gửi câu hỏi, đổi phiên, chuyển tab hoặc rời trang sẽ hủy thu âm.
- Giọng đọc lấy từ thiết bị; thử tải/cài giọng đúng ngôn ngữ trên hệ điều hành nếu thiếu. Ứng dụng thông báo nếu không có giọng phù hợp. Nếu không có đúng locale, chọn giọng cùng ngôn ngữ và hiển thị tên/locale thực tế.
- Gửi văn bản tới AI vẫn sử dụng OpenAI như trước và có phí theo cấu hình. Không bổ sung API âm thanh trả phí.
- Kiểm thử tự động dùng browser API giả lập, không xác nhận micro/loa vật lý trên từng thiết bị.

## Kiểm thử cho lập trình viên

Trong PowerShell tại E:\warehouse-ai:

```powershell
.\.venv\Scripts\python.exe -m pytest -q --tb=short
node --test tests/voice.test.cjs
node --test tests/mic-check.test.cjs
```

Lệnh Node cần Node.js được cài và có trong PATH; server Python không cần Node để chạy.
Các kiểm thử giọng nói không gọi OpenAI và không mở micro thật.

Tham khảo: https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition
và https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesis

## JarVis và micro trên Edge

- Tên trợ lý là JarVis. Các dạng nhận dạng gần tên như “gia vít”, “gia vis”, “cha vít”, “ja vít”, “jar vit” ở đầu lời gọi được chuẩn hóa thành JarVis. Hiển thị lời nhận dạng ban đầu nếu có sửa tên. Không sửa số lượng, mã hàng hoặc từ ở giữa câu. Đây là danh sách nhận dạng có giới hạn, không đảm bảo mọi cách phát âm đều được nhận đúng.
- Ảnh người dùng cung cấp được giữ nguyên trong static/assets. CSS hiển thị từng phần của ảnh ba trạng thái theo thứ tự trái: lỗi, giữa: suy nghĩ, phải: có kết quả. Ảnh chào riêng; chuyển mờ 550 ms và chuyển động nhẹ, không phải hoạt ảnh 3D khớp miệng.
- **Kiểm tra micro (5 giây)** chỉ chạy khi bấm nút, đo âm lượng trên máy bằng Web Audio, không lưu/đăng tải âm thanh. Thu âm dừng khi xong, bấm dừng, gửi câu hỏi hoặc rời tab; quyền được chấp nhận muộn sau khi hủy cũng được giải phóng.
- Nếu thanh micro có tín hiệu nhưng nhận dạng Edge báo network/service-not-allowed, nguyên nhân có thể ở dịch vụ nhận dạng, không phải thiếu quyền micro. Thử Chrome hoặc Windows + H trong ô câu hỏi nếu Windows hỗ trợ ngôn ngữ đã chọn.
- Nếu không có tín hiệu, kiểm tra micro vật lý, thiết bị đầu vào và quyền Windows. Mức tín hiệu không xác nhận chắc chắn có tiếng nói, vì có thể có tiếng nền.
- Tiếng Việt mặc định sử dụng vi-VN. Lỗi giữ mã cụ thể; không tuyên bố micro thật đã được sửa chỉ dựa trên kiểm thử giả lập.
