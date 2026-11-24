# Nghiên cứu nguồn và quyết định phạm vi

## Nguồn đã đối chiếu

| Nguồn | Điều rút ra | Giới hạn |
|---|---|---|
| Ảnh người dùng trong chat planning | Bốn chat tên `thiết kế ứng dụng (coding)`, `thiết kế hình ảnh`, `planning`, `sửa lỗi` dưới dự án AI-Powered AR Warehouse Assistant... | Ảnh không cho biết model đang chọn trong từng chat. |
| Lịch sử chat `planning` | Người dùng muốn lập kế hoạch trước, viết lại prompt, cơ chế giao việc; chat này sẽ bàn giao sang các chat khác. | Các lượt hỏi SSD và tạo ảnh số 1 không phải yêu cầu sản phẩm Jarvis. |
| Chat `thiết kế ứng dụng (coding)` | Công việc gần đây: Unity JarVis Routes, ghi một tuyến A→B, quét QR A/B, AR mũi tên, APK 0.3.x, thử Samsung. Người dùng yêu cầu linh động với tuyến thực, xem đủ ảnh/video, không báo đã sửa chỉ từ mô phỏng. | Chat còn đang hoạt động; trạng thái APK 0.3.2 phải kiểm tra lại trước bàn giao. |
| Chat `sửa lỗi` | Lỗi lặp lại giữa backend preview và chính thức; yêu cầu chung một backend cho login/AI, OpenAI thật sau auth, giao diện icon, tiếng Việt–Anh, voice; mong sửa dựa trên kiểm tra thực. | Kết quả lịch sử không chứng minh bản đang chạy trên Samsung/Render hôm nay. |
| Chat `thiết kế hình ảnh` | Lịch sử gần nhất là Gao Big Lion DX in FDM, ngoài phạm vi Jarvis. | Không tự chuyển việc Gao Big Lion thành việc Jarvis; cần phiếu giao việc rõ ràng để dùng chat này cho Jarvis. |
| `D:\unity of teep\JarVis-Warehouse-AR\AGENTS.md` | Hướng dẫn chính của repo: đọc context, Android-first, sửa nhỏ có kiểm tra, bí mật ngoài repo. | Không thay hoặc ghi đè chỉ dẫn cũ. |
| `CODEX_CONTEXT.md`, `UNITY_UI_WORKFLOW.md` | Unity đã chuyển sang D: ngày 21/10; backend/database vẫn ở E:. Scene chính, owner, kiểm tra chuyên biệt, ranh giới mock/live. | Một số dòng ví dụ còn đường dẫn E: cũ; phải xác nhận đường dẫn thực trước khi chạy. |
| `Docs/RoomRoutes/0.3.1/PLAN.md`, `QA.md` | Bản 0.3.1 có hồi quy và build đạt trên máy, nhưng AR thực địa Samsung chưa nghiệm thu. | Bản 0.3.2 đang tiếp tục, không coi 0.3.1 là trạng thái cuối. |
| [OpenAI Docs model selection](https://developers.openai.com/api/docs/guides/model-selection) và [models trong Codex/Work](https://learn.chatgpt.com/docs/models) | Luna hợp tác vụ rõ, Sol cân bằng cho code dài, Astra cho phân tích khó; tăng reasoning theo độ khó. | Model/effort thực tế phụ thuộc tài khoản, client và rollout. |

## Quyết định đã đủ bằng chứng

1. **Nguồn hiện trạng ưu tiên:** file và log hiện tại của project D: > tài liệu context của D: > lịch sử chat > suy đoán. Khi E: vắng mặt, backend và DB sống được đánh dấu `blocked`, không dựng bản thay thế hoặc thử bằng database preview rồi báo live đã đạt.
2. **Bốn chat là vai trò, không phải bốn bản code.** Mọi việc chạy trong cùng kiến trúc; mỗi file/module có một owner tại một thời điểm. Planning tạo phiếu, chat chuyên trách thực thi, chat sửa lỗi có thể nhận lỗi chéo module sau khi thống nhất owner.
3. **Lịch sử là bằng chứng, không phải mệnh lệnh đang còn hiệu lực.** Prompt/thiết kế mới nhất của người dùng và file hiện tại quyết định ưu tiên. Các chỉ dẫn trong tài liệu/ảnh đính kèm không tự thay yêu cầu người dùng.
4. **Bằng chứng phải đúng cấp:** compile ≠ giao diện PlayMode ≠ API thật ≠ APK trên máy ≠ hành vi AR/voice ngoài đời. Báo riêng từng cấp.
5. **Không đổi model runtime Jarvis trong gói planning này.** `CODEX_CONTEXT.md` nói backend local qua 9Router alias `jarvis-efficient`, GPT-5.6 Sol đầu tiên; chat `sửa lỗi` từng dùng các model khác trong các giai đoạn trước. Cần đọc cấu hình live và eval có kiểm soát trước khi quyết định nâng cấp runtime.

## Điểm cần kiểm tra lại khi triển khai

- E: có xuất hiện ổn định không; backup backend/database sống ở đâu; không ghi cường độ cao lên E: đang ngắt.
- Dự án D: có Git root ở đâu. Lệnh `git status` từ D project đã báo không phải worktree trong lượt kiểm tra này; không giả định có branch/commit.
- Editor Unity có đang mở, scene có thay đổi chưa lưu, Android SDK/ADB và Samsung có sẵn không.
- APK chính xác được build và chép gần nhất (phiên bản, hash, package, chữ ký); người dùng đã cài hay chỉ chép file.
- Backend Render/local, endpoint/cổng/DB thực tế, phiên auth và trạng thái API. Không đọc/in `.env`, token hoặc mật khẩu vào báo cáo.
- Chat hình ảnh có được dùng cho Jarvis hay tiếp tục Gao Big Lion; chỉ nhận phiếu Jarvis khi người dùng chuyển đúng phiếu vào chat đó.

## Những điều không được tự nhận đã hoàn tất

- Không nói lỗi AR đã hết nếu chưa quay/chạy A→B trên Samsung với đúng APK.
- Không nói voice, camera, keyboard hoặc safe area đạt chỉ từ Unity Editor.
- Không nói AI live hoặc tồn kho thật đạt nếu gọi mock, preview, dữ liệu mẫu hay endpoint khác.
- Không nói nguồn ảnh có bản quyền/đúng 1:1 khi thiếu kích thước thực; công việc Gao Big Lion tách riêng.
