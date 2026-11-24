# Chọn model cho bốn chat và các mức suy luận

Áp dụng cho **model của chat Codex dùng để phát triển Jarvis**. Không áp dụng trực tiếp cho model phục vụ người dùng trong app Jarvis. Kiểm tra model có trong bộ chọn của chính tài khoản; không suy ra khả dụng từ trang API. Nguồn: [OpenAI Docs model selection](https://developers.openai.com/api/docs/guides/model-selection), [ChatGPT Learn models](https://learn.chatgpt.com/docs/models), [reasoning effort](https://developers.openai.com/api/docs/guides/reasoning).

## Thang từ thấp đến cao

| Mức | Model / effort đề xuất | Công việc phù hợp | Nâng cấp khi |
|---|---|---|---|
| 1 — rất nhẹ | GPT-6 Luna · Low | Tóm tắt log đã lọc, phân loại phiếu, đổi nhãn, kiểm tra danh sách file. | Cần giải thích nhân quả hoặc sửa nhiều file. |
| 2 — nhẹ | GPT-6 Luna · Medium/High | Sửa một UI nhỏ có owner rõ, làm biến thể nội dung/ảnh theo brief đã khóa, checklist QA. | Có state, callback, dữ liệu thật hoặc nhiều nền tảng. |
| 3 — mặc định kỹ thuật | GPT-6.1 Sol · Medium | Một tính năng Unity/backend rõ phạm vi, kiểm thử tập trung, cập nhật tài liệu. | Lỗi khó tái hiện, mâu thuẫn nhiều nguồn, release. |
| 4 — kỹ thuật sâu | GPT-6.1 Sol · High/XHigh | Debug AR tracking, auth/backend, Android build, rà soát cross-module, lập kế hoạch có tradeoff. | Sau điều tra vẫn không rõ nguyên nhân hoặc yêu cầu nhiều hệ thống cùng lúc. |
| 5 — cao | GPT-6 Astra · Medium/High | Kiến trúc, phân tích bằng chứng mâu thuẫn, thiết kế nghiệm thu thực địa, review giải pháp quan trọng. | Ràng buộc nghiêm ngặt, quyết định khó đảo ngược hoặc cần audit toàn hệ thống. |
| 6 — cao nhất trong một luồng | GPT-6 Astra · XHigh/Max | Sự cố rất khó, quyết định kiến trúc lớn, kiểm tra prompt cuối với nhiều ràng buộc. | Chỉ dùng khi tăng chất lượng đã được chứng minh hữu ích; Max tốn thời gian/usage hơn. |

`Ultra` là chế độ chia việc cho subagent, **không phải thứ hạng chất lượng của một model**. Chỉ dùng khi có nhiều phần độc lập, có ranh giới file/đầu ra rõ và người dùng hoặc chỉ dẫn áp dụng yêu cầu giao song song. Không dùng Ultra để cùng sửa một Unity project/scene hoặc cùng database. [Nguồn Codex](https://learn.chatgpt.com/docs/models).

## Cấu hình gợi ý cho từng chat

| Chat | Mặc định | Tăng khi khó | Giảm khi rõ ràng | Lý do |
|---|---|---|---|---|
| `planning` | GPT-6.1 Sol · High | Astra · High/XHigh cho master plan, kiểm tra mâu thuẫn/handoff cuối | Sol Medium cho cập nhật một phiếu | Cần tổng hợp 4 luồng, ưu tiên và kiểm định; không nên trả giá Astra cho từng cập nhật nhỏ. |
| `thiết kế ứng dụng (coding)` | GPT-6.1 Sol · Medium | Sol High/XHigh cho AR, auth, release; Astra High nếu root cause/kiến trúc còn mơ hồ | Luna Medium cho chỉnh sửa thật nhỏ, độc lập | Phần lớn là Unity C# nhiều trạng thái và kiểm tra Editor/Android. |
| `thiết kế hình ảnh` | GPT-6.1 Sol · Medium để phân tích brief, bố cục, tính nhất quán | Astra Medium/High cho art direction phức tạp hoặc review nhiều nguồn | Luna Medium cho resize, crop, nhãn đã rõ | Model chat lập brief; tạo/sửa bitmap dùng công cụ image chuyên dụng khi sẵn có. Công việc Gao Big Lion hiện tại không phải Jarvis. |
| `sửa lỗi` | GPT-6.1 Sol · High | Astra High/XHigh cho lỗi liên hệ Unity–backend–Android, tái diễn hoặc mất dữ liệu | Sol Medium/Luna High cho lỗi cục bộ tái hiện rõ | Cần phân biệt nguyên nhân và bằng chứng, tránh vá triệu chứng. |

**Thiết lập nhanh:** mỗi chat chọn model trong bộ chọn → chọn reasoning phù hợp với phiếu → ghi model/effort vào báo cáo cuối để so chất lượng. Nếu không thấy GPT-6.1 Sol, dùng model sẵn có tương đương và ghi rõ; không đổi cài đặt account hoặc model runtime Jarvis để bù. GPT-6.1 Sol và Luna có mặt trong Work/Codex tùy rollout; Astra là mức năng lực cao. [Nguồn](https://learn.chatgpt.com/docs/models).

## Chọn mức theo rủi ro thay vì chỉ theo tên chat

- Thao tác đọc, lập danh sách, không sửa: mức 1–2.
- Sửa một owner, có test: mức 3.
- Liên quan camera, AR, auth, DB, dữ liệu thật hoặc build Android: mức 4.
- Quyết định phạm vi, kiến trúc, sự cố tái diễn nhiều bản: mức 5–6.
- Nếu một lần tăng model không đem lại bằng chứng tốt hơn, quay về mức thấp hơn cho các phiếu cùng loại. So trên cùng đầu vào và tiêu chí nghiệm thu, không dựa cảm giác.

## Riêng model chạy trong ứng dụng Jarvis

`CODEX_CONTEXT.md` ghi alias local `jarvis-efficient` qua 9Router, GPT-5.6 Sol ưu tiên. Đây là **ghi nhận cấu hình lịch sử**, không phải xác minh backend live ngày 24/10. Nâng model runtime đòi hỏi đọc cấu hình hiện tại khi E: trở lại, test câu hỏi kho VI/EN/ZH, nguồn RAG, latency, chi phí, an toàn tài khoản và fallback. Không tự chỉnh alias/provider, không xoay tài khoản hoặc đưa khóa API vào Unity. Nếu cần nghiên cứu model runtime, planning tạo phiếu riêng `MODEL-RUNTIME-*` với đo lường và ngân sách được chấp thuận.
