# Deploy lên Render bằng Node.js

Backend đã migrate sang Express tại `server.js`; `static/` được phục vụ bằng `express.static`. Không cần Python, Uvicorn hoặc frontend build step.

## Web Service

Trong Render Dashboard chọn **New -> Web Service**, liên kết repository và branch. Dùng đúng các giá trị sau:

| Trường | Giá trị |
|---|---|
| Environment | **Node** |
| Root Directory | Trống nếu `package.json` ở root; nếu đặt project trong thư mục con thì nhập thư mục đó |
| Build Command | `npm install` |
| Start Command | `npm start` |
| Health Check Path | `/api/health` |
| Instances | 1 (SQLite) |

Render tự cấp `PORT`; server bind `0.0.0.0` và đọc biến này khi khởi động.

## Environment variables

Đặt trong Dashboard, không đưa secret vào command:

```dotenv
AI_MODE=mock
DATABASE_PATH=/var/data/warehouse.sqlite3
MAX_REQUESTS_PER_MINUTE=20
MAX_REQUESTS_PER_DAY=100
MAX_OUTPUT_TOKENS=1200
REQUEST_TIMEOUT_SECONDS=30
CHINESE_LOCALE=
PROTOTYPE_TOKEN_SHA256=
```

Chỉ khi đã kiểm tra quyền model mới đặt `AI_MODE=openai`, `OPENAI_API_KEY` và `OPENAI_MODEL`. Không đặt key trong `static/`.

## SQLite persistence

Để giữ user, session, quota và response cache qua deploy, tạo Persistent Disk và mount tại `/var/data`, sau đó dùng `DATABASE_PATH=/var/data/warehouse.sqlite3`. Không có disk, dữ liệu Render Free là ephemeral. Chạy một instance duy nhất.

## Tạo user demo

Nếu service có Shell:

```bash
npm run create-user -- create-user --user worker-demo --token-file /var/data/.local-token-worker-demo
cat /var/data/.local-token-worker-demo
```

Nếu không có Shell, tính SHA-256 lowercase của một token riêng rồi đặt vào `PROTOTYPE_TOKEN_SHA256`. Lúc boot, server tạo hoặc cập nhật user `render-demo` với role `staff`; raw token không được lưu.

## Kiểm tra sau deploy

1. Mở `https://<service>.onrender.com/api/health` và xác nhận `status=ok`, `mode=mock`.
2. Mở `/`, dùng token hợp lệ và thử các câu hỏi trong README.
3. Kiểm tra token sai trả `401`, task không được cấp trả `403`, request trùng payload được cache và request trùng `requestId` nhưng khác payload trả `409`.
4. Nếu bật OpenAI, kiểm tra riêng lỗi key/model/quota; health không chứng minh provider đã hoạt động.

Render Node runtime docs: https://render.com/docs/node-express-app
