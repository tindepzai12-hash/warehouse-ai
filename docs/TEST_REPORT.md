# Báo cáo kiểm thử Milestone A

Ngày cập nhật: 2026-09-23. Windows, Python 3.12.14, dependencies theo requirements.

`python -m pytest -q --tb=short`: **50 passed**. Một DeprecationWarning từ Starlette TestClient/AnyIO, không gây failure.

Chuẩn bị deploy: kiểm tra user demo quyền staff được khôi phục từ SHA-256 cấu hình khi filesystem mới, token sai bị từ chối và hash không đúng định dạng chặn khởi động. Đã đẩy repository private lên GitHub và tạo service Render Free tại Singapore; chi tiết ở DEPLOYMENT.md. Kiểm thử model cục bộ bên dưới không tự chứng minh model hoạt động trên Render.

Bổ sung regression cho câu “cần 5 hộp kem nhưng chỉ có 3 tôi phải làm gì ?”: truy xuất SOP-SHORT-PICK và đề xuất EXC-001. Thông báo 401 trên UI hướng dẫn nhập lại token ứng dụng.

Đã kiểm tra năm scenario tiếng Anh, truy xuất tiếng Việt/Trung, authentication, schema/size, forged roles/quantities, task ownership, cross-user session isolation, topic continuity, SQLite reopen, retry caching/inflight lock, rate limits, missing coverage, unverified-number exclusion, occupied-location limitation, Responses URL/JSON Schema/store=false/history/context bằng HTTPX MockTransport, redacted provider errors, timeout/refusal/incomplete/malformed/citation/action/target rejection, UI/static/source endpoints và mock không gọi provider.

Đã thực hiện 3 cuộc gọi OpenAI thật được người dùng cho phép với gpt-4o-mini: hai lượt đầu bị validation chặn (tham số action dư và citation không hợp lệ), lượt cuối HTTP 200 cho câu hỏi thiếu hàng viết tự nhiên, có nguồn SOP-SHORT-PICK/reporting. Đã bổ sung schema citation với cặp ID cho phép và loại bỏ exceptionCode dư trên action không phải mở báo cáo. Phản hồi thật còn có cách diễn đạt chưa rõ về partial pick và clarification; prompt đã được siết lại, chưa gọi thêm để đánh giá thay đổi này. Không công bố điểm chính xác. Follow-up/greeting chỉ kiểm thử transport giả lập, chưa kiểm thử thành công bằng model thật. Không deploy Render, không chạy Unity/Android, không test inventory mutations thuộc B.

Chế độ thật gửi corpus SOP nhỏ, đã lọc theo ngôn ngữ và approval, không phụ thuộc khớp từ khóa. Kiểm thử gồm paraphrase, thiếu nguồn/ngoài phạm vi, citation schema, tham số action dư; mock hỗ trợ câu 5/3 và 10/4 cùng câu không dấu. File .env.example đã được trả về placeholder rỗng; cấu hình riêng nằm trong .env.

Browser smoke test: mở localhost:8000, nhập token demo và câu hỏi Short Pick tiếng Việt, nhận EXC-001 và nguồn thật; hỏi tiếp “Tại sao vậy?” giữ cùng session và contextTurns=1. Kiểm tra giao diện/nguồn bằng accessibility tree và screenshot. Không kiểm tra mọi browser hoặc mobile viewport. `pip check`: không phát hiện dependency hỏng. Tests bổ sung xác minh giới hạn cooldown, lưu sự kiện dưới nhãn worker report và metadata tài liệu chưa duyệt.
