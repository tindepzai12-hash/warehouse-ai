# Warehouse AI — Milestone A

Backend Python/FastAPI và browser test UI. Mặc định **mock**, không cần OpenAI key, không sử dụng dịch vụ trả phí. Dự án nằm ở `E:\warehouse-ai`; không thay đổi specification gốc hay tạo Unity project.

## Chạy trên Windows / PowerShell

1. Cài Python 3.12 (64 bit), bật Python launcher. Mở PowerShell mới và kiểm tra `py -3.12 --version`.
2. Tạo môi trường và cài dependencies đã ghim phiên bản:

```powershell
Set-Location E:\warehouse-ai
py -3.12 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r requirements-dev.txt
Copy-Item .env.example .env
```

Không cần kích hoạt venv hoặc thay đổi PowerShell ExecutionPolicy. Chỉ sao chép `.env.example` lần đầu; đừng ghi đè `.env` đã cấu hình.

3. Tạo người dùng thử nghiệm; token được ghi vào file riêng, database chỉ lưu SHA-256:

```powershell
.\.venv\Scripts\python.exe -m app.cli create-user --user worker-demo --token-file .local-token
Get-Content .local-token | Set-Clipboard
```

Đây là **token ứng dụng thử nghiệm**, không phải OpenAI key. Lệnh chạy lại cùng user sẽ thu hồi token cũ. Có thể thêm `--role icqa` hoặc `--role supervisor`; role chỉ do CLI server cấp, không lấy từ JSON client. Mỗi tài khoản demo được gán ba nhiệm vụ TEST; chưa có phân quyền WMS sản xuất.

4. Chạy server, để cửa sổ PowerShell này mở:

```powershell
.\.venv\Scripts\python.exe run.py
```

Mở `http://localhost:8000`, dán token ứng dụng vào ô đầu tiên, chọn ngôn ngữ và đặt câu hỏi. Không nhập OpenAI key vào giao diện. Token chỉ nằm trong bộ nhớ trang, không lưu localStorage. Có thể xóa clipboard sau khi dán. Nhấn **Phiên mới** để đổi chủ đề/nhiệm vụ. Đổi nhiệm vụ tự tạo phiên mới. Ctrl+C dừng server.

Nếu cổng bận: `$env:PORT='8001'` rồi chạy lại, truy cập `http://localhost:8001`. `run.py` nghe `0.0.0.0` để dùng cùng cấu hình trên Render; chỉ sử dụng token cá nhân và HTTPS nếu mở cho người khác.

Trong máy hiện tại `.venv` đã được tạo và dependencies đã cài. Có thể bắt đầu từ bước 3; không có token mặc định trong mã nguồn.

## Thử các tình huống

- `Cần 5 hộp kem chống nắng nhưng chỉ có 3, tôi làm gì?`
- `Nước hoa bị rò rỉ, tôi có lấy đi đóng gói không?`
- `Mã vạch áo bị mờ, có nhập tay được không?`
- `A01 yêu cầu TV 55 inch nhưng có 65 inch.`
- `Pallet gãy chặn lối đi.`
- `Giải thích FIFO và FEFO`, sau đó `Tại sao vậy?` trong cùng phiên.
- Chọn TEST-PUT-001: `B-12-03 không khả dụng, tôi cất SKU-2048 ở đâu?`

Mock truy xuất SOP rồi trả các đoạn mẫu theo ngôn ngữ, không suy luận như LLM và không thực thi giao dịch. Giao diện có bốn vùng: mô phỏng sự kiện, JSON yêu cầu, phản hồi/nguồn/đề xuất, trạng thái/độ trễ. Mở bản nháp ngoại lệ không gửi ngoại lệ.

## Chuyển sang OpenAI khi bạn sẵn sàng

Sửa `.env` **trên máy/server**, không gửi key qua chat, không commit `.env`:

```dotenv
AI_MODE=openai
OPENAI_API_KEY=
OPENAI_MODEL=
```

Điền key và tên model bạn được cấp quyền ngay trong file riêng. Mẫu cố ý để trống model: chưa kiểm tra quyền truy cập project của bạn, chưa tự chọn model. Chọn model có Responses API và Structured Outputs, xác minh quyền trong OpenAI project. Sau đó khởi động lại server. Không có cơ chế tự chuyển sang trả lời mock để che lỗi provider.

Ứng dụng gọi `POST https://api.openai.com/v1/responses` bằng HTTPX, `text.format.type=json_schema`, `strict=true`, `store=false`; không dùng Assistants API, embeddings trả phí hoặc OpenAI File Search. RAG cục bộ vẫn được dùng trong cả hai mode. Timeout mặc định 30 giây; **không tự retry** provider để tránh lặp phí. Giới hạn mặc định 20 yêu cầu/phút và 100/ngày/người; lỗi provider cũng tiêu quota. `MAX_OUTPUT_TOKENS` giới hạn đầu ra, không thay thế budget của OpenAI project.

Lỗi 401/403 provider → HTTP 502 có hướng dẫn kiểm tra key/quyền; 429 provider → 503; timeout → 504. Không trả raw provider error/key. Chế độ OpenAI gửi câu hỏi cùng lịch sử và 12 mục SOP dự án (chỉ bản dịch theo ngôn ngữ đang chọn), ngay cả khi không khớp từ khóa. Model hiểu cách diễn đạt tự nhiên, chọn nguồn phù hợp hoặc giải thích phạm vi nếu chưa có kiến thức. Số liệu chưa kiểm chứng không được gửi. Key hợp lệ không tự tạo dữ liệu tồn kho thật.

## Ngôn ngữ và API

`language`: `en`, `vi`, `zh`; `locale` tùy chọn, ví dụ `zh-Hant` hoặc `zh-Hans` do người triển khai lựa chọn. `CHINESE_LOCALE` là mặc định tùy chỉnh cho model khi client không gửi locale; để trống thì chỉ dùng `zh`. Không gán mặc định vùng Trung Quốc/Đài Loan. Bản dịch mock là ví dụ cố định, chưa chuyển tự động chữ giản thể/phồn thể theo locale; cần người dùng ngôn ngữ duyệt trước triển khai thực tế. UI lab hiện dùng nhãn tiếng Việt, câu trả lời hỗ trợ cả ba ngôn ngữ.

Các endpoint thực tế:

| Endpoint | Chức năng |
|---|---|
| `GET /api/health` | Kiểm tra service/database, mode; không chứng minh gọi OpenAI thành công |
| `GET /api/me` | User/role/nhiệm vụ demo được cấp từ server |
| `POST /api/feedback` | Hỏi–đáp có Bearer token, validation, RAG, session |
| `GET /api/knowledge/{documentId}/{sectionId}` | Nguồn SOP có xác thực |
| `GET /docs` | OpenAPI tương tác (nhập Authorization header qua client khi gọi feedback) |

Ví dụ gọi trong PowerShell khác:

```powershell
Set-Location E:\warehouse-ai
$warehouseToken = (Get-Content -Raw .local-token).Trim()
$warehouseHeaders = @{ Authorization = "Bearer $warehouseToken" }
$warehouseBody = @{
  requestId = [guid]::NewGuid().ToString()
  taskId = 'TEST-PICK-001'
  language = 'vi'
  question = 'FIFO và FEFO khác nhau thế nào?'
  eventType = 'user_question'
  observation = @{}
} | ConvertTo-Json
$warehouseReply = Invoke-RestMethod -Uri http://localhost:8000/api/feedback -Method Post -Headers $warehouseHeaders -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($warehouseBody))
$warehouseReply
```

Gửi `sessionId` nhận được ở lượt sau và requestId mới. Retry đúng yêu cầu cũ dùng lại requestId và nguyên payload: phản hồi thành công được cache bền vững, không gọi lại model. requestId trùng nhưng payload khác → 409. Cùng phiên đang xử lý → 409; không gửi song song trong một phiên. Session của người khác → 404. Đổi task trong phiên → 409. Phiên giữ 10 lượt gần nhất và chủ đề SOP cuối; chưa có tóm tắt dài hạn đầy đủ. Inventory facts luôn lấy mới từ adapter, không lấy từ lời model cũ.

## Kiểm thử

```powershell
Set-Location E:\warehouse-ai
.\.venv\Scripts\python.exe -m pytest -q
```

Xem `docs/TEST_REPORT.md` và `docs/EVALUATION.md`. Tests dùng database tạm và HTTP giả lập; không cần key, không phát sinh phí. Chưa công bố điểm chính xác LLM, chưa có đánh giá con người hoặc xác minh OpenAI thật.

## Cấu trúc và giới hạn

`app/main.py`: API; `models.py`: schema; `provider.py`: mock/Responses/kiểm tra nguồn và action; `knowledge.py`: truy xuất từ khóa đa ngôn ngữ có điểm số; `store.py`: SQLite sessions/token hash/quota/retry cache; `warehouse.py`: adapter chỉ đọc dữ liệu giả lập; `app/cli.py`: cấp token; `static/`: UI không cần Node build.

`knowledge/reviewed.json` chứa SOP đã đối chiếu specification, có ID/version/provenance/approval. `knowledge/unverified.json` giữ số liệu chưa kiểm chứng và bị loại khỏi retrieval. **Đối chiếu specification không đồng nghĩa với được hiện trường phê duyệt**; `site_approved=false` được giữ rõ. `docs/WAREHOUSE_AI_CODEX_SPEC.md` lưu nguyên specification, không được index.

RAG lexical nhỏ, không phải tìm kiếm semantic toàn diện; cách diễn đạt ngoài bộ từ khóa có thể trả thiếu nguồn. Schema/citation/action checks không chứng minh mọi câu do LLM viết đều đúng: cần đánh giá con người trước dùng thực tế. Không gửi ảnh, không có video/voice/Unity/AR ở A. Không có endpoint thay đổi tồn kho. SQLite dùng một instance; token tĩnh theo người là giải pháp prototype, cần identity provider/token hết hạn, phân quyền WMS, retention và audit đầy đủ trước production. Database hiện lưu hội thoại để tái lập ngữ cảnh, không có job xóa theo thời hạn; không đưa dữ liệu cá nhân không cần thiết vào thử nghiệm.

Phạm vi B–D giữ ở `docs/ROADMAP.md`. Hướng dẫn deploy chính xác: **`docs/RENDER.md`**. Chưa deploy Render.

Tài liệu chính thức đã đối chiếu: [OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs), [Render FastAPI](https://render.com/docs/deploy-fastapi), [Render Persistent Disks](https://render.com/docs/disks).
