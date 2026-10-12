using System;
using System.Collections;
using UnityEngine;

namespace JarVis.Warehouse.Backend
{
    public sealed class MockJarvisBackend : IJarvisBackend
    {
        public IEnumerator SendChat(
            ChatRequest request,
            Action<ChatResponse> completed,
            Action<string> failed
        )
        {
            if (request == null || string.IsNullOrWhiteSpace(request.question))
            {
                failed?.Invoke("chat_question_required");
                yield break;
            }

            yield return new WaitForSecondsRealtime(0.65f);
            bool vietnamese = string.Equals(request.language, "vi", StringComparison.OrdinalIgnoreCase);
            string message = CreateMockResponse(request.question, vietnamese);
            completed?.Invoke(new ChatResponse
            {
                message = message,
                sources = Array.Empty<WarehouseSource>(),
                action = new WarehouseAction { type = "show_picking_checklist", payload = string.Empty },
                needsClarification = false,
                inventoryVerified = false,
                isMock = true
            });
        }

        private static string CreateMockResponse(string question, bool vietnamese)
        {
            string normalized = question.Trim().ToLowerInvariant();
            if (normalized.Contains("5s") || normalized.Contains("5 s"))
            {
                return vietnamese
                    ? "5S là phương pháp tổ chức nơi làm việc của Nhật Bản: "
                      + "Seiri (Sàng lọc), Seiton (Sắp xếp), Seiso (Sạch sẽ), "
                      + "Seiketsu (Săn sóc/Chuẩn hóa) và Shitsuke (Sẵn sàng/Duy trì kỷ luật). "
                      + "Trong kho, 5S giúp loại bỏ vật dụng thừa, đặt hàng và dụng cụ đúng vị trí, "
                      + "giữ khu vực sạch, chuẩn hóa quy trình và duy trì kiểm tra hằng ngày."
                    : "5S is a Japanese workplace organization method: Seiri (Sort), "
                      + "Seiton (Set in order), Seiso (Shine), Seiketsu (Standardize), "
                      + "and Shitsuke (Sustain). In a warehouse it keeps inventory, tools, "
                      + "aisles, and daily checks organized and safe.";
            }

            bool inventoryQuestion = normalized.Contains("tồn kho")
                || normalized.Contains("kiểm kê")
                || normalized.Contains("thiếu hàng")
                || normalized.Contains("inventory")
                || normalized.Contains("short pick")
                || normalized.Contains("barcode");
            if (inventoryQuestion)
            {
                return vietnamese
                    ? "Hãy đếm lại tại vị trí, đối chiếu barcode và tạo báo cáo hàng thiếu nếu số lượng vẫn không đủ. Tồn kho này chưa được xác minh trực tiếp."
                    : "Recount at the location, verify the barcode, and create a short-pick report if the quantity is still insufficient. This inventory is not directly verified.";
            }

            return vietnamese
                ? "JarVis hiện đang chạy ở chế độ demo ngoại tuyến nên chưa thể trả lời đáng tin cậy câu hỏi này. Hãy kết nối backend JarVis để sử dụng trợ lý AI đầy đủ."
                : "JarVis is currently running in offline demo mode and cannot answer this reliably. Connect the JarVis backend to use the full AI assistant.";
        }
    }
}
