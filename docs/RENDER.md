# Deploy lên Render — hướng dẫn, chưa thực hiện

Ứng dụng đã tạo: **`run.py`**, **`requirements.txt`**, factory **`app.main:create_app`**. Không upload riêng specification để deploy. Không tạo tài nguyên trả phí trong đợt triển khai Milestone A này.

## Chuẩn bị repository

Tạo Git repository với nội dung thư mục `E:\warehouse-ai` ở root. Không commit `.env`, `.venv`, `data`, `.local-token*`. Xem danh sách file trước khi push; project chưa tự push lên Git provider của bạn. Nếu bạn đặt cả thư mục vào repository lớn hơn, Root Directory trên Render phải là `warehouse-ai`.

## Web Service

Trong Render Dashboard: New → Web Service → liên kết repository → chọn branch.

| Trường | Giá trị chính xác |
|---|---|
| Runtime | Python 3 |
| Root Directory | Để trống nếu `run.py` ở root; nếu không, `warehouse-ai` |
| Build Command | `pip install -r requirements.txt` |
| Start Command | `python run.py` |
| Health Check Path | `/api/health` |
| Instances | 1 (SQLite) |

`run.py` đọc PORT do Render cung cấp và bind `0.0.0.0`. Lệnh tương đương nếu dùng Uvicorn trực tiếp: `uvicorn app.main:create_app --factory --host 0.0.0.0 --port $PORT --workers 1`. Render chạy shell Linux nên `$PORT` đúng ở đây; trên PowerShell dùng `run.py` như README.

Đặt Environment Variables qua Dashboard, không đưa secret vào Build/Start command:

```dotenv
PYTHON_VERSION=3.12.14
AI_MODE=mock
DATABASE_PATH=/var/data/warehouse.sqlite3
MAX_REQUESTS_PER_MINUTE=20
MAX_REQUESTS_PER_DAY=100
MAX_OUTPUT_TOKENS=1200
REQUEST_TIMEOUT_SECONDS=30
```

`OPENAI_API_KEY` và `OPENAI_MODEL` để trống/chưa tạo khi chạy mock. `CHINESE_LOCALE` tùy chọn. Không đặt key trong static JS hoặc Unity.

## Lưu trữ bền vững và lựa chọn chi phí

Để giữ users/sessions/quota/cache qua restart và deploy, ứng dụng SQLite này cần **Persistent Disk gắn tại `/var/data`**. Render yêu cầu service hỗ trợ disk (có chi phí); chỉ lựa chọn khi bạn chủ động triển khai sau này. Tạo disk trong Dashboard, đặt mount path `/var/data`, chọn dung lượng phù hợp. Disk chỉ xuất hiện lúc runtime, không phải build. Giữ một instance; disk cũng ảnh hưởng khả năng deploy không gián đoạn theo tài liệu Render.

Nếu chỉ thử Web Service không có disk, đổi `DATABASE_PATH=data/warehouse.sqlite3`: dữ liệu ephemeral có thể mất khi restart/deploy. Phương án này **không đạt tiêu chí persistence Milestone C**, user token/quota cũng có thể mất. Không có cam kết lưu bền vững miễn phí với cấu hình hiện tại. Muốn dùng PostgreSQL sau này cần viết adapter persistence; không thể chỉ thay DATABASE_PATH bằng URL PostgreSQL.

## Tạo người dùng sau khi deploy

Nếu dùng Render Free không có Shell, xem `UPDATING.md`: cấu hình `PROTOTYPE_TOKEN_SHA256` để tạo user demo quyền staff lúc khởi động. Đây chỉ khôi phục đăng nhập; lịch sử/quota vẫn mất cùng SQLite ephemeral. Cách dùng Shell dưới đây dành cho service hỗ trợ Shell.

Với service có Render Shell và persistent disk, mở Shell:

```bash
python -m app.cli create-user --user worker-demo --token-file /var/data/.local-token-worker-demo
cat /var/data/.local-token-worker-demo
```

Đọc token riêng trong Shell rồi dán vào UI HTTPS của service; không gửi qua chat hay commit. Lệnh `cat` hiển thị token cá nhân trong Shell của bạn; không đưa vào build log. Có thể chạy lại CLI để rotate/revoke token cũ. Chưa có màn hình đăng ký công khai hoặc admin endpoint cấp token.

## Kiểm tra trước khi kết luận deploy thành công

1. Mở `https://<service>.onrender.com/api/health`: mode mock, database OK.
2. Mở trang `/`, thử không có/sai token: feedback phải bị từ chối.
3. Dùng token đúng, hỏi năm tình huống, FIFO/FEFO và câu ngoài kiến thức; xem ID nguồn thực tế.
4. Hỏi tiếp trong cùng session; đổi user phải không đọc được phiên cũ.
5. Restart service rồi dùng cùng token/session; phải giữ ngữ cảnh nếu disk cấu hình đúng.
6. Khi sẵn sàng trả phí OpenAI: thêm key/model có quyền vào Environment, đổi `AI_MODE=openai`, redeploy, thử một yêu cầu thật có nguồn và follow-up. Health không chứng minh bước này.
7. Kiểm tra lỗi key/model/quota được che dữ liệu nhạy cảm, logs chỉ có request ID/status/source ID/latency. Ghi riêng rằng paid API test đã hoặc chưa chạy.

Thử nghiệm ở repo hiện tại chỉ dùng transport giả lập, **chưa xác minh bước 1–7 trên Render**. Milestone C vẫn còn nguyên.

Nguồn: [Render FastAPI](https://render.com/docs/deploy-fastapi), [Web Services](https://render.com/docs/web-services), [Environment Variables](https://render.com/docs/configure-environment-variables), [Persistent Disks](https://render.com/docs/disks).
