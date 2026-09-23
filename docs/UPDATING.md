# Cập nhật bản thử sau khi lên Render

Website có thể phát triển từng phần. Khi repository và Render được liên kết, địa chỉ website không đổi theo mỗi lần cập nhật.

1. Sửa và chạy thử ở `E:\warehouse-ai` (localhost).
2. Chạy `npm install` rồi kiểm tra frontend bằng `node --test tests/*.test.cjs`.
3. Kiểm tra thay đổi, commit và push tới branch mà Render đang theo dõi. Không commit `.env`, token hoặc database.
4. Nếu bật auto-deploy qua Git provider đã liên kết, Render tự build/deploy. Nếu dùng public repository URL chưa liên kết Git provider hoặc tắt auto-deploy, dùng Manual Deploy.
5. Kiểm tra health, giao diện và một luồng hỏi đáp. Không chỉ xem build success. Có thể quay lại bản deploy trước qua Dashboard khi cần; rollback code không thay thế backup database.

Ví dụ các đợt cập nhật: tối giản màn hình chat; thêm nhấn để nói/đọc câu trả lời; thêm avatar/animation; triển khai WMS Milestone B. Mỗi đợt đều có kiểm thử riêng. Cấu hình key/model cập nhật trong Render Environment, không sửa key vào repository.

## Bản thử Render Free

SQLite cục bộ bị mất khi restart/redeploy/spin-down. Session/quota/retry cache có thể mất; không dùng cấu hình này cho vận hành kho thật. Render Free không có Shell, nên backend hỗ trợ `PROTOTYPE_TOKEN_SHA256`: cấu hình SHA-256 của token thử nghiệm đủ ngẫu nhiên để tạo lại user `render-demo` quyền staff mỗi lần khởi động. Chỉ hash được đặt trên Render, token gốc giữ riêng; người dùng vẫn nhập token gốc ở UI. Nếu không cấu hình hash, không tạo user tự động, feedback vẫn yêu cầu đăng nhập.

Lệnh lấy hash mà không hiển thị token gốc, chạy trên máy của bạn:

```powershell
cd E:\warehouse-ai
node -e "const fs=require('fs'),crypto=require('crypto'); console.log(crypto.createHash('sha256').update(fs.readFileSync('.local-token','utf8').trim()).digest('hex'))"
```

Thiết lập service: Environment **Node**, region Singapore, plan Free, build `npm install`, start `npm start`, health `/api/health`, database `data/warehouse.sqlite3` hoặc `/var/data/warehouse.sqlite3` khi có Persistent Disk. Environment: `OPENAI_API_KEY`, `OPENAI_MODEL`, `PROTOTYPE_TOKEN_SHA256`, `CORS_ALLOWED_ORIGINS` nếu có frontend khác domain; không đặt PORT cố định trên Render.

Muốn giữ lịch sử qua cập nhật: bổ sung database bền vững hoặc service có Persistent Disk khi được chấp thuận chi phí. Không coi việc khôi phục tài khoản demo là khôi phục lịch sử/quota.

Nguồn: https://render.com/docs/deploys và https://render.com/docs/free (đối chiếu 2026-09-23).
