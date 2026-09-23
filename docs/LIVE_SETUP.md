# Chuyển sang hỏi đáp OpenAI

Cấu hình thực nằm ở `.env` (gitignored). `.env.example` là mẫu không có key. Ứng dụng đọc `AI_MODE=openai` và model do người dùng chọn, hiện là `gpt-4o-mini`.

Luồng mới không chặn câu hỏi theo từ khóa. Vì corpus hiện nhỏ (12 SOP), backend ưu tiên các mục khớp từ khóa rồi gửi cả corpus đã duyệt theo specification, chỉ giữ bản dịch ngôn ngữ được chọn, để model hiểu các cách diễn đạt khác và câu hỏi tiếp nối. Không gửi specification đầy đủ hay file unverified. Với corpus lớn, cần bổ sung semantic retrieval và budget context thay vì tăng corpus vô hạn.

Mỗi lượt hỏi thật gửi câu hỏi, tối đa 10 lượt lịch sử gần đây, các SOP dự án và facts giả lập tới OpenAI; có thể phát sinh phí. Không dùng model để cập nhật tồn kho. Câu ngoài phạm vi phải nêu giới hạn; không bịa nguồn. UI vẫn cần token người dùng ứng dụng, tách biệt OpenAI key.

Khởi động từ PowerShell:

```powershell
cd E:\warehouse-ai
.\.venv\Scripts\python.exe run.py
```

Tải lại trang localhost:8000, chọn Phiên mới để tránh ngữ cảnh mock trước đó. Trạng thái đầu trang phải hiện OPENAI. `liveApiVerified` chỉ chuyển true sau một phản hồi model qua kiểm tra trong tiến trình hiện tại; restart đặt lại false.

Kiểm thử frontend hiện có thể chạy bằng `node --test tests/*.test.cjs`. Kiểm thử provider thật là tùy chọn, có thể phát sinh phí; không tự chạy như một phần build/deploy.
