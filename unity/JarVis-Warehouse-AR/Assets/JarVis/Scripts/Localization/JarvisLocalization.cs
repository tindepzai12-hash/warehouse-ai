using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Localization
{
    public enum SupportedLanguage
    {
        Vi,
        En
    }

    public static class JarvisLocalization
    {
        private static readonly IReadOnlyDictionary<string, string> Vietnamese =
            new Dictionary<string, string>
            {
                ["app_title"] = "JarVis Warehouse",
                ["login_title"] = "Đăng nhập nhân viên",
                ["login_subtitle"] = "BẢN DEMO — chưa kết nối hệ thống kho thật",
                ["employee_id"] = "Mã nhân viên",
                ["employee_placeholder"] = "VD: JW-9502",
                ["access_code"] = "Mã truy cập",
                ["access_code_placeholder"] = "Nhập mã gồm 6 chữ số",
                ["role"] = "Vai trò",
                ["language"] = "Ngôn ngữ",
                ["associate"] = "Nhân viên",
                ["supervisor"] = "Giám sát",
                ["inventory"] = "Kiểm kê",
                ["vietnamese"] = "Tiếng Việt",
                ["english"] = "English",
                ["login"] = "Đăng nhập",
                ["loading"] = "Đang tải...",
                ["login_required"] = "Vui lòng nhập mã nhân viên và mã truy cập.",
                ["login_invalid"] = "Mã truy cập demo cần ít nhất 4 ký tự.",
                ["network_error"] = "Không thể kết nối mạng.",
                ["camera_error"] = "Không thể truy cập camera.",
                ["scan_barcode"] = "Quét barcode",
                ["create_barcode"] = "Tạo barcode",
                ["tasks"] = "Nhiệm vụ",
                ["map"] = "Bản đồ kho",
                ["chat"] = "Chat JarVis",
                ["exceptions"] = "Ngoại lệ",
                ["thinking"] = "JarVis đang suy nghĩ...",
                ["inventory_unverified"] = "Tồn kho hiện chưa được xác minh trực tiếp.",
                ["barcode_not_found"] = "Không tìm thấy barcode này trong dữ liệu hiện tại.",
                ["short_pick"] = "Hàng thiếu",
                ["damaged"] = "Hàng hỏng",
                ["misplaced"] = "Hàng sai vị trí",
                ["exception_report"] = "Báo cáo ngoại lệ",
                ["navigation"] = "Điều hướng",
                ["welcome"] = "Xin chào",
                ["assigned"] = "Đã giao",
                ["in_progress"] = "Đang thực hiện",
                ["completed"] = "Hoàn thành",
                ["needs_review"] = "Cần kiểm tra",
                ["previous"] = "TRƯỚC",
                ["next"] = "SAU",
                ["complete"] = "HOÀN THÀNH",
                ["mock_lookup"] = "TRA CỨU DEMO",
                ["mock_data_ready"] = "DỮ LIỆU DEMO — sẵn sàng để nhân viên kiểm tra",
                ["viewing_previous"] = "Đang xem nhiệm vụ demo trước đó",
                ["viewing_next"] = "Đang xem nhiệm vụ demo tiếp theo",
                ["mock_match"] = "TRA CỨU DEMO — đã khớp",
                ["tasks_complete"] = "NHIỆM VỤ HOÀN THÀNH",
                ["high_priority"] = "ƯU TIÊN CAO",
                ["required"] = "Yêu cầu",
                ["actual"] = "Thực tế",
                ["location"] = "Vị trí",
                ["priority"] = "Ưu tiên",
                ["mock_unverified"] = "MOCK — CHƯA XÁC MINH TỒN KHO"
            };

        private static readonly IReadOnlyDictionary<string, string> English =
            new Dictionary<string, string>
            {
                ["app_title"] = "JarVis Warehouse",
                ["login_title"] = "Employee sign in",
                ["login_subtitle"] = "DEMO — not connected to a live warehouse system",
                ["employee_id"] = "Employee ID",
                ["employee_placeholder"] = "e.g. JW-9502",
                ["access_code"] = "Access code",
                ["access_code_placeholder"] = "Enter 6-digit code",
                ["role"] = "Role",
                ["language"] = "Language",
                ["associate"] = "Associate",
                ["supervisor"] = "Supervisor",
                ["inventory"] = "Inventory",
                ["vietnamese"] = "Tiếng Việt",
                ["english"] = "English",
                ["login"] = "Sign In",
                ["loading"] = "Loading...",
                ["login_required"] = "Enter an employee ID and access code.",
                ["login_invalid"] = "The demo access code must contain at least 4 characters.",
                ["network_error"] = "Network connection is unavailable.",
                ["camera_error"] = "Camera access is unavailable.",
                ["scan_barcode"] = "Scan barcode",
                ["create_barcode"] = "Create barcode",
                ["tasks"] = "Tasks",
                ["map"] = "Warehouse map",
                ["chat"] = "Chat with JarVis",
                ["exceptions"] = "Exceptions",
                ["thinking"] = "JarVis is thinking...",
                ["inventory_unverified"] = "Inventory has not been verified directly.",
                ["barcode_not_found"] = "This barcode was not found in the current data.",
                ["short_pick"] = "Short pick",
                ["damaged"] = "Damaged",
                ["misplaced"] = "Wrong location",
                ["exception_report"] = "Exception report",
                ["navigation"] = "Navigation",
                ["welcome"] = "Welcome",
                ["assigned"] = "Assigned",
                ["in_progress"] = "In progress",
                ["completed"] = "Completed",
                ["needs_review"] = "Needs review",
                ["previous"] = "PREV",
                ["next"] = "NEXT",
                ["complete"] = "COMPLETE",
                ["mock_lookup"] = "DEMO LOOKUP",
                ["mock_data_ready"] = "DEMO DATA — ready for operator review",
                ["viewing_previous"] = "Viewing previous demo task",
                ["viewing_next"] = "Viewing next demo task",
                ["mock_match"] = "DEMO LOOKUP — matched",
                ["tasks_complete"] = "TASKS COMPLETE",
                ["high_priority"] = "HIGH PRIORITY",
                ["required"] = "Required",
                ["actual"] = "Actual",
                ["location"] = "Location",
                ["priority"] = "Priority",
                ["mock_unverified"] = "MOCK — INVENTORY UNVERIFIED"
            };

        public static SupportedLanguage CurrentLanguage { get; private set; } = SupportedLanguage.En;
        public static event Action LanguageChanged;

        public static string Get(string key)
        {
            IReadOnlyDictionary<string, string> table = CurrentLanguage == SupportedLanguage.Vi
                ? Vietnamese
                : English;
            return table.TryGetValue(key, out string value) ? value : key;
        }

        public static void SetLanguage(SupportedLanguage language)
        {
            if (CurrentLanguage == language)
                return;

            CurrentLanguage = language;
            LanguageChanged?.Invoke();
        }
    }
}
