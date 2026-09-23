# Hợp đồng API cho Unity — JarVis (nền tảng Milestone A)

Tài liệu này mô tả **đúng API Node/Express đang có** để nhóm Unity tích hợp sau này. Nó chỉ phục vụ trợ lý hỏi–đáp/RAG. Không có endpoint nào ở đây xác nhận quét, đổi tồn kho, tạo tuyến đường, hay hoàn tất nghiệp vụ kho.

## 1. Ranh giới và địa chỉ

| Môi trường | Base URL |
|---|---|
| Render hiện tại | `https://warehouse-ai-fmjc.onrender.com` |
| Chạy máy phát triển | `http://localhost:8000` |

- Gọi `GET /api/health` trước để báo trạng thái dịch vụ; endpoint này không cần token.
- Các endpoint còn lại dưới đây cần `Authorization: Bearer <prototype-user-token>`.
- Token là token người dùng thử nghiệm do server kiểm tra bằng hash; **không phải OpenAI key**. Không nhúng OpenAI key vào Unity, Git, log hoặc asset build.
- `GET /api/me` trả `{ "id", "role", "tasks" }`. Unity phải lấy danh sách `tasks` ở đây, không tự gửi `role` hoặc tự tạo task ID.
- Response hiện luôn có `synthetic: true` và `operationalMutation: false`. Không hiển thị nó như dữ liệu WMS thật.
- `GET /api/health` thành công trả các trường `status`, `mode`, `milestone`, `synthetic`, `liveApiVerified`; `liveApiVerified: false` không phải lỗi, chỉ có nghĩa service chưa hoàn tất một yêu cầu OpenAI thành công trong tiến trình hiện tại.

## 2. Gửi câu hỏi hoặc sự kiện

`POST /api/feedback`

Headers:

```http
Authorization: Bearer <prototype-user-token>
Content-Type: application/json
```

Body chỉ được có các trường sau; thêm trường như `role`, `action`, `inventory`, `route`, `systemPrompt` sẽ bị `422`.

```json
{
  "requestId": "6b0f15e9-1bba-4e23-a840-8a719e66a5e7",
  "sessionId": null,
  "taskId": "TEST-PICK-001",
  "language": "vi",
  "locale": "vi-VN",
  "eventType": "user_question",
  "occurredAtUtc": "2026-09-24T10:30:00.000Z",
  "question": "Đơn cần năm hộp nhưng trên kệ còn ba, tôi cần làm gì?",
  "observation": {
    "reportedVisibleQuantity": 3
  }
}
```

### Quy tắc request

| Trường | Quy tắc thực tế |
|---|---|
| `requestId` | Bắt buộc, UUID v1–v5. Một request mới dùng UUID mới. |
| `sessionId` | Tùy chọn/null. Dùng `sessionId` server trả về để giữ hội thoại; nó phải thuộc đúng token và cùng task. |
| `taskId` | Mẫu `TEST-<CHỮ>-<3 số>` và phải nằm trong `GET /api/me.tasks`. Server demo hiện cấp các task `TEST-PICK-001`, `TEST-PUT-001`, `TEST-COUNT-001`. |
| `language` | `vi`, `en`, hoặc `zh`; mặc định server là `vi`. |
| `locale` | Tùy chọn, phải cùng gốc với `language`. Khuyến nghị `vi-VN`, `en-GB`, `zh-TW`. Với `zh`, server **luôn** trả `zh-TW` (Hoa phồn thể, Đài Loan). |
| `eventType` | Một trong: `user_question`, `scan_success`, `scan_failure`, `shelf_mismatch`, `route_deviation`, `task_completion`, `summary_request`. Đây là báo cáo/ngữ cảnh, không xác nhận giao dịch. |
| `occurredAtUtc` | Tùy chọn, ISO-8601 UTC có hậu tố `Z`. |
| `question` | Bắt buộc, sau trim dài 1–2.000 ký tự. |
| `observation` | Object chỉ có `reportedVisibleQuantity` (số nguyên 0–100.000), `scanCode` (chuỗi ≤100), `scanFailureCount` (số nguyên 0–1.000). Đây là **worker report**, không phải tồn kho/tuyến được xác minh. |

Toàn bộ JSON request bị giới hạn 16 KiB. Unity nên kiểm tra độ dài trước khi gửi.

Server dùng giờ địa phương để JarVis chào hỏi: `vi` → `Asia/Ho_Chi_Minh`, `en` → `Europe/London`, `zh` → `Asia/Taipei`. Unity chỉ truyền language/locale; không tự gửi system prompt hoặc giờ tin cậy.

## 3. Response thành công

```json
{
  "message": "…hướng dẫn JarVis…",
  "sources": [{ "documentId": "SOP-SHORT-PICK", "sectionId": "reporting" }],
  "action": {
    "type": "open_exception_report",
    "exceptionCode": "EXC-001",
    "targetId": null
  },
  "needsClarification": false,
  "clarifyingQuestion": null,
  "feedbackType": "explanation",
  "priority": "normal",
  "cooldownSeconds": 0,
  "sessionId": "…",
  "requestId": "…",
  "language": "vi",
  "locale": "vi-VN",
  "mode": "openai",
  "synthetic": true,
  "operationalMutation": false,
  "latencyMs": 0,
  "contextTurns": 0,
  "contextWindowTurns": 10,
  "actionExecution": "proposal_only",
  "sourceDetails": []
}
```

- `message` dài tối đa 8.000 ký tự và phải hiển thị theo `language` đã chọn.
- `sources` có tối đa bốn cặp ID; server chỉ chấp nhận nguồn có trong kết quả RAG đã truy xuất.
- `sourceDetails` chỉ chứa các nguồn đã được trích dẫn. Mỗi phần tử có metadata như `documentId`, `sectionId`, `source_status`, `runtime_approved`, `site_approved`, `review_status`, `sourceLanguage`, `exceptionCode`, và `text`.
  - Trong response `/api/feedback` hiện tại, `sourceDetails[].text` là **chuỗi đã chọn cho ngôn ngữ**, không phải object `{vi,en,zh}`. Unity phải hiển thị trực tiếp chuỗi này.
  - `GET /api/knowledge/:documentId/:sectionId` trả bản nguồn RAG theo cấu trúc nội bộ; chỉ gọi sau khi đã xác thực và chỉ tin nội dung có metadata nguồn đi kèm.
- Nếu `needsClarification` là `true`, ưu tiên hiển thị `clarifyingQuestion`; đừng suy đoán dữ liệu kho bị thiếu.
- Chỉ giữ tối đa mười lượt gần nhất trong hội thoại hiện tại. Gọi phiên mới bằng cách không gửi `sessionId`.

## 4. Ánh xạ action an toàn cho Unity

`actionExecution` luôn là `proposal_only`. Unity chỉ được coi các action này là chỉ dẫn trình bày; mọi thao tác kho phải chờ API Milestone B có xác thực và xác nhận công nhân.

| `action.type` | Hành vi Unity được phép ở giai đoạn này |
|---|---|
| `none` | Chỉ hiển thị câu trả lời/nguồn. |
| `show_station_intro` | Mở phần giới thiệu trạm hoặc avatar. |
| `show_item_details` | Mở panel thông tin chỉ đọc. |
| `highlight_storage_location` | Tô sáng vị trí đã có trong UI hiện tại; không xác nhận vị trí đó còn hợp lệ. |
| `show_navigation_route` | Chỉ hiển thị route đã có từ nguồn tin cậy trong tương lai; API A không trả route được duyệt. |
| `request_rescan` | Hiển thị yêu cầu quét lại; không tự sửa `scanCode`. |
| `show_picking_checklist` | Mở checklist tham khảo. |
| `explain_sop_step` | Mở nội dung SOP/nguồn được trích dẫn. |
| `show_safety_warning` | Hiển thị cảnh báo rõ ràng, ưu tiên cao nếu `priority: "high"`. |
| `open_exception_report` | Chỉ mở **bản nháp** form ngoại lệ. Không submit, không đổi inventory. |
| `show_task_summary` | Hiển thị tóm tắt chỉ đọc. |

- `exceptionCode` chỉ có thể là `null` hoặc đúng một trong: `EXC-001`, `EXC-002`, `EXC-003`, `EXC-004`, `EXC-005`. Với `open_exception_report`, server bắt buộc mã đó phải khớp một SOP được trích dẫn. Với mọi action khác, server chuẩn hóa nó thành `null`.
- `targetId` là `string` hoặc `null`, nhưng API A chưa xác minh ngữ nghĩa/permission của target. Trước khi Unity dùng nó để chọn vật thể, đường đi hoặc màn hình, kiểm tra local allowlist, trạng thái hiện tại và quyền; nếu không xác minh được thì chỉ hiển thị text, không điều hướng/tác động hệ thống.
- Không được biến `message`, `sources`, `targetId`, hoặc bất kỳ text do model tạo thành code, URL tùy ý, lệnh thiết bị, hay nghiệp vụ tự động.

## 5. Lỗi và retry có idempotency

Server trả JSON `{ "detail": "…" }`; lỗi `500` còn có `traceId` để báo cho backend. Lưu toàn bộ payload gửi lần đầu trong bộ nhớ tạm của client.

| HTTP | Ý nghĩa | Cách Unity xử lý |
|---|---|---|
| `401` | Thiếu/sai prototype token | Bỏ token cũ, yêu cầu đăng nhập lại; không tự retry. |
| `403` | Task không được cấp | Làm mới `/api/me`, để người dùng chọn task hợp lệ; không retry payload cũ. |
| `404` | Session không thuộc user/không còn, hoặc nguồn không tồn tại | Với session: bắt đầu phiên mới sau khi báo rõ; với source: chỉ ẩn link nguồn lỗi. |
| `409` | Session đang xử lý, request đang xử lý, đổi task trong session, hoặc cùng `requestId` nhưng body khác | Chờ ngắn rồi retry **nguyên payload cùng `requestId`** khi body không đổi. Nếu người dùng sửa câu hỏi/dữ liệu, tạo UUID mới. |
| `413` | Body quá 16 KiB | Yêu cầu rút ngắn câu hỏi; không retry. |
| `422` | Schema/giá trị không hợp lệ | Sửa local DTO; không retry cùng body. |
| `429` | Hết quota prototype theo phút/ngày | Báo chờ; không vòng retry dồn dập. |
| `502` | Kết nối/provider/structured output lỗi | Có thể cho người dùng bấm thử lại cùng payload và `requestId`; không có thao tác kho nào xảy ra. |
| `503` | Quota/rate limit OpenAI | Chờ rồi thử lại cùng payload; tránh tự động retry liên tục. |
| `504` | Provider timeout | Hiển thị “chưa có thao tác kho nào xảy ra”; cho retry cùng payload. |

Khi nhận `200`, cùng `requestId` + body sẽ trả cache response; đổi body với `requestId` cũ trả `409`. Điều này giúp Unity không gọi AI trùng khi mất mạng hoặc người dùng bấm lại.

## 6. Thứ tự triển khai Unity sau này

1. Tạo một `JarvisApiClient` có base URL cấu hình theo Development/Production, timeout và DTO đúng các bảng trên.
2. Tạo `SessionStore` chỉ giữ `sessionId`, payload lỗi gần nhất và language/locale; không lưu OpenAI key.
3. Gọi `/api/me`, dùng task server cấp; sau đó gọi `/api/feedback` với UUID mới cho mỗi payload mới.
4. Đưa response qua `AIFeedbackPresenter` và `AIActionRouter`; router áp dụng allowlist ở mục 4, mặc định `none` khi parse/validation client thất bại.
5. Hiển thị nguồn, trạng thái `synthetic` và lỗi/retry. Chỉ bỏ nhãn synthetic khi backend có dữ liệu kho thực và contract mới.

## 7. Cố ý hoãn đến khi có đầu vào của giáo sư

- Không tạo project/scene Unity, avatar/empathy behavior, camera, AR, barcode hardware, voice input hay voice output mới.
- Không thêm Unity Gaming Services, Cloud Code, Cloud Save, đăng nhập production, refresh token, hay đưa token prototype dùng chung vào bản Android phát hành.
- Không làm WMS/ERP integration, tồn kho trực tiếp, route được duyệt, task/mutation/exception/count/clearance endpoint. Các endpoint này thuộc Milestone B và phải có giao dịch, idempotency nghiệp vụ, quyền và audit.
- Không coi SQLite/Render Free là persistent production storage; users, sessions và retry cache có thể mất sau restart/deploy.
- Không đánh dấu SOP draft/unverified là quy định chính thức. Runtime RAG hiện chỉ đưa tài liệu `runtime_approved: true` với status cho phép vào model.
- Khi chuẩn bị web build/multi-domain, CORS hiện đang mở rộng (`cors()`); cần khóa origin chính xác và thay token prototype bằng xác thực ngắn hạn do server cấp.

Tài liệu được đối chiếu với `server.js` tại thời điểm tạo, không phải API versioned cam kết bất biến. Mọi thay đổi schema sau này cần cập nhật file này cùng test DTO Unity.
