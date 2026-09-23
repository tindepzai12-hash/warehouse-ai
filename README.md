# Warehouse AI - Milestone A

Backend Node.js/Express và browser test UI. Chế độ mặc định là `mock`, không cần OpenAI key và không sử dụng dịch vụ trả phí. Frontend trong `static/` được giữ nguyên và được Express phục vụ trực tiếp.

## Chạy local

Cài Node.js 18+ rồi chạy PowerShell:

```powershell
Set-Location E:\warehouse-ai
npm install
Copy-Item .env.example .env
npm start
```

Mở `http://localhost:8000`. Nếu cổng bận, đặt `$env:PORT='8001'` trước `npm start`.

Tạo hoặc rotate user thử nghiệm; database chỉ lưu SHA-256 token:

```powershell
npm run create-user -- create-user --user worker-demo --token-file .local-token
Get-Content .local-token | Set-Clipboard
```

Token này là token ứng dụng prototype, không phải OpenAI key. Có thể thêm `--role icqa` hoặc `--role supervisor`. Không commit `.env`, `.local-token` hoặc database.

## Cấu hình

Các biến môi trường nằm trong `.env.example`. `DATABASE_PATH` mặc định là `data/warehouse.sqlite3`. `AI_MODE=mock` dùng RAG cục bộ và câu trả lời mẫu. Để dùng OpenAI, đặt `AI_MODE=openai`, `OPENAI_API_KEY` và `OPENAI_MODEL`; server gọi Responses API với structured output, không gửi raw provider error ra client.

Server giữ nguyên token SHA-256, session, idempotent retry theo `requestId`, quota 20 request/phút và 100 request/ngày mặc định, cùng các mã lỗi xác thực, session, quota và provider của phiên bản Python. SQLite lưu users, sessions, replies, usage và inflight requests.

## API

| Endpoint | Chức năng |
|---|---|
| `GET /api/health` | Kiểm tra service/database |
| `GET /api/me` | User, role và task được cấp |
| `POST /api/feedback` | Validation, auth, RAG, session và feedback |
| `GET /api/knowledge/{documentId}/{sectionId}` | Đọc nguồn SOP có xác thực |

Các endpoint cần token dùng header `Authorization: Bearer <token>`. `GET /` và `/static/*` được phục vụ từ `static/`; không cần build frontend.

## Render

Tạo Web Service từ repository và dùng đúng cấu hình:

| Trường | Giá trị |
|---|---|
| Environment | **Node** |
| Root Directory | Trống nếu `package.json` ở root |
| Build Command | `npm install` |
| Start Command | `npm start` |
| Health Check Path | `/api/health` |

Đặt `DATABASE_PATH=/var/data/warehouse.sqlite3` khi gắn Persistent Disk tại `/var/data`. Không có disk thì SQLite trên Render là ephemeral và có thể mất user/session sau restart hoặc deploy. Giữ một instance vì SQLite không dành cho nhiều instance ghi đồng thời.

Để tạo user demo trên Render Free không có Shell, đặt `PROTOTYPE_TOKEN_SHA256` bằng SHA-256 lowercase của token riêng của bạn. Server sẽ tạo user `render-demo` khi khởi động; raw token không được lưu trong mã nguồn.

## Giới hạn

Kho kiến thức chạy lexical RAG từ `knowledge/reviewed.json`; `knowledge/unverified.json` không được dùng. Prototype không có WMS live, không thay đổi tồn kho, không tạo route và không xác nhận transaction. Cần đánh giá người thật, identity provider, phân quyền WMS, retention và audit trước production.

Xem thêm [docs/RENDER.md](docs/RENDER.md), [docs/VOICE.md](docs/VOICE.md) và [docs/ROADMAP.md](docs/ROADMAP.md).
