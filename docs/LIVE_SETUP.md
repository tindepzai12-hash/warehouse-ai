# Chuyển sang hỏi đáp OpenAI

Cấu hình thực nằm ở `.env` (gitignored). `.env.example` là mẫu không có key. Runtime luôn gọi OpenAI Responses API và mặc định model `gpt-4o-mini` nếu `OPENAI_MODEL` để trống; không còn `AI_MODE`.

Luồng mới không trả lời theo keyword cố định. Backend dùng lexical retrieval để chọn tối đa ba SOP runtime-approved liên quan, rồi chỉ gửi các SOP đó, lịch sử gần đây và facts giả lập tới OpenAI. Không gửi specification đầy đủ, SOP draft hoặc file unverified. Với corpus lớn, cần bổ sung semantic retrieval và budget context thay vì tăng corpus vô hạn.

Mỗi lượt hỏi thật gửi câu hỏi, tối đa 10 lượt lịch sử gần đây, các SOP dự án và facts giả lập tới OpenAI; có thể phát sinh phí. Không dùng model để cập nhật tồn kho. Câu ngoài phạm vi phải nêu giới hạn; không bịa nguồn. UI vẫn cần token người dùng ứng dụng, tách biệt OpenAI key.

Khởi động từ PowerShell:

```powershell
Set-Location E:\warehouse-ai
npm start
```

Tải lại trang localhost:8000 và chọn Phiên mới khi muốn bỏ ngữ cảnh cũ. Trạng thái đầu trang phải hiện OPENAI. `liveApiVerified` chỉ chuyển true sau một phản hồi model qua kiểm tra trong tiến trình hiện tại; restart đặt lại false.

Kiểm thử frontend hiện có thể chạy bằng `node --test tests/*.test.cjs`. Kiểm thử provider thật là tùy chọn, có thể phát sinh phí; không tự chạy như một phần build/deploy.
