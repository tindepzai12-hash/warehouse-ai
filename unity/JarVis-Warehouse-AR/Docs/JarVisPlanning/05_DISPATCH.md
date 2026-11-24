# Cơ chế giao việc và bàn giao giữa bốn chat

## Vai trò

| Chat | Quyền sở hữu trong quy trình | Không tự làm |
|---|---|---|
| `planning` | Lập backlog, nghiên cứu, viết phiếu, khóa tiêu chí, chọn model, kiểm tra báo cáo, quyết định phiếu kế tiếp | Sửa code thay chat được giao, báo code đã chạy từ lời kể |
| `thiết kế ứng dụng (coding)` | Unity/backend implementation theo phiếu đã giao, tích hợp, compile/build | Tự đổi thiết kế đã duyệt, đổi DB/provider, nhận lỗi ngoài owner mà không bàn giao |
| `thiết kế hình ảnh` | Brief và tạo/sửa asset Jarvis cụ thể, nguồn/thư mục/bản quyền, kích cỡ và handoff asset | Tự thay logic Unity; trộn việc Gao Big Lion vào Jarvis |
| `sửa lỗi` | Tái hiện, root cause, sửa lỗi cục bộ/cross-module được phân owner, hồi quy | Vá ngưỡng để che nguyên nhân, báo hết lỗi khi thiếu kiểm tra bắt buộc |

## Trạng thái và thứ tự

`DRAFT → READY → ASSIGNED → IN_PROGRESS → REVIEW → DONE`. Nhánh khác: `BLOCKED` (phụ thuộc ngoài tầm), `NEEDS_DEVICE` (chờ kiểm tra Samsung), `PARTIAL` (phần độc lập đạt), `REOPENED` (bằng chứng mới/kiểm tra thất bại). Planning là nơi ghi trạng thái; chat thực thi đề xuất thay đổi và chứng cứ. Không đồng thời có hai phiếu `IN_PROGRESS` cùng owner file/scene/database.

## Mẫu phiếu giao việc bắt buộc

```md
# TASK <ID> — <kết quả người dùng thấy được>
Status: READY
Owner chat: <một trong 3 chat thực thi>
Model/effort: <gợi ý, chỉ chọn nếu có trong picker>
Priority: P0/P1/P2
Created: <YYYY-MM-DD Asia/Saigon>
Source: <tin nhắn người dùng / file / log và thời điểm>
Current truth: <đường dẫn project, branch nếu có, backend live/mock/unavailable>

## Outcome
<Một câu kiểm chứng được>

## In scope
<Module/file/flow được phép>

## Out of scope / invariants
<Dữ liệu, UI, scene, API, quyền, asset phải giữ>

## Dependencies and blockers
<E:, Unity Editor, Samsung, API, approval/cost, người dùng cần cung cấp>

## Investigation
<Các giả thuyết cần kiểm tra, bằng chứng đã có, điều chưa biết>

## Acceptance criteria
1. <Kết quả cụ thể và trạng thái lỗi/trống>
2. <Kiểm tra hồi quy cần thiết>
3. <Tầng xác thực tối thiểu: compile/unit/Unity/API/APK/device>

## Deliverables
<File, ảnh, log, APK, tài liệu và đường dẫn>

## Reporting
<Định dạng báo cáo bên dưới; nếu không đạt, ghi nguyên nhân và việc tiếp theo>
```

## Kiểm tra trước khi phát phiếu

1. Có một outcome rõ và một owner chính.
2. Có trạng thái nguồn E:/D:, live/mock và tài liệu authority.
3. Tiêu chí chấp nhận đo được; nếu cần Samsung, ghi `NEEDS_DEVICE` từ đầu.
4. Không đụng cùng file/scene/database với phiếu đang chạy.
5. Thứ tự dependency đúng; không đẩy phần backend vào chat UI nếu E: mất.
6. Không có key, mật khẩu hoặc thông tin riêng trong phiếu.
7. Model/effort là gợi ý cho chat, không tự đổi runtime Jarvis.

## Mẫu báo cáo từ chat thực thi

```md
TASK <ID> — <DONE/PARTIAL/BLOCKED/NEEDS_DEVICE>
Outcome: <đạt gì, chưa đạt gì>
Files changed: <đường dẫn tuyệt đối hoặc không đổi>
Validation: <lệnh/bước, thời điểm, PASS/FAIL, bằng chứng>
Runtime mode: <live/mock/offline/unavailable; endpoint/DB không lộ bí mật>
Device: <APK version, hash, máy, thử gì; hoặc chưa thử>
Limitations: <điều chưa xác nhận và rủi ro còn lại>
Rollback: <cách quay lại nếu cần>
Next: <một hành động hay phiếu phụ thuộc>
```

## Quy tắc gửi việc giữa chat

Trong thiết lập hiện tại, planning **không gửi tin nhắn tự động** sang chat khác; người dùng nói sẽ pass sau khi planning. Vì vậy gói này chuẩn bị prompt/phiếu, người dùng chọn thời điểm gửi. Khi có quyền gửi cụ thể ở lượt sau, gửi nguyên phiếu `READY` và prompt vai trò, không gửi chỉ mỗi câu “tiếp tục”. Người nhận phải trả lại ID trong báo cáo. Chat không được tự lấy một phiếu từ lịch sử chat khác và coi đó là ủy quyền mới.

Nếu cần chia việc thật thành subagent, chỉ chia các phần độc lập như đọc log/tài liệu/QA không đụng cùng file; một owner tổng hợp, kiểm tra từng đầu ra, báo rõ. Không dùng subagent để tránh quyền/chi phí hoặc thay người dùng phê duyệt một hành động.
