using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.UI;

namespace JarVis.Warehouse.Preview
{
    public enum PickupPreviewStep { Task, AlignA, Aligned, Guidance, Location, LocationAccepted, Barcode, BookAccepted, ReturnA, ReturnScan, Complete }
    // Explicit UI-only demonstration: no production navigation, auth, mission file, registration or inventory calls.
    public sealed class PickupFlowPreview : MonoBehaviour
    {
        public TMP_FontAsset regular, bold;
        public Texture2D roomPhoto;
        public PreviewCameraRestart cameraRestart;
        public PickupPreviewStep Step { get; private set; }
        public string Stop { get; private set; } = "B";
        public bool PickedB { get; private set; }
        public bool PickedC { get; private set; }
        public string Error { get; private set; }
        public float LogicalHeight { get; private set; } = 915;
        RectTransform page;
        Canvas canvas;
        CameraRestartPhase cameraPhase;
        static Color C(string hex) => PrototypeUi.Hex(hex);
        void Start()
        {
            canvas = GetComponent<Canvas>();
            if (page == null) page = transform.Find("Pickup UI demo") as RectTransform;
            Build(Screen.width > 0 ? Screen.height * 412f / Screen.width : 915);
        }
        void Update()
        {
            if (canvas == null) return;
            float h = Screen.width > 0 ? Screen.height * 412f / Screen.width : 915;
            if (Mathf.Abs(h - LogicalHeight) > 3) Build(h);
        }
        public void Initialize(Canvas owner) { canvas = owner; }
        public void ResetDemo() { PickedB = PickedC = false; Stop = "B"; Go(PickupPreviewStep.Task); }
        public void Go(PickupPreviewStep step) { Step = step; Error = null; Build(LogicalHeight); }
        public void SelectStop(string stop)
        {
            if ((stop == "B" && PickedB) || (stop == "C" && PickedC)) return;
            Stop = stop; Go(PickupPreviewStep.Guidance);
        }
        public void Scan(string value)
        {
            string expected = Step == PickupPreviewStep.AlignA || Step == PickupPreviewStep.ReturnScan ? "JWM:MAIN:E" : Step == PickupPreviewStep.Location ? "JWM:MAIN:" + Stop : Step == PickupPreviewStep.Barcode ? (Stop == "B" ? "4714551570610" : "4714551570603") : null;
            if (expected == null) return;
            if (value != expected && !(expected == "JWM:MAIN:E" && value == "JWM:MAIN:A"))
            { Error = Step == PickupPreviewStep.Barcode ? "Chưa đúng sách " + Book + ". Hãy quét barcode trên sách." : "Chưa đúng điểm. Hãy quét QR " + (Step == PickupPreviewStep.Location ? Stop : "A") + "."; Build(LogicalHeight); return; }
            if (Step == PickupPreviewStep.AlignA) Go(PickupPreviewStep.Aligned);
            else if (Step == PickupPreviewStep.ReturnScan) Go(PickupPreviewStep.Complete);
            else if (Step == PickupPreviewStep.Location) Go(PickupPreviewStep.LocationAccepted);
            else Go(PickupPreviewStep.BookAccepted);
        }
        public void ConfirmBook()
        {
            if (Step != PickupPreviewStep.BookAccepted) return;
            if (Stop == "B") PickedB = true; else PickedC = true;
            if (PickedB && PickedC) Go(PickupPreviewStep.ReturnA);
            else { Stop = PickedB ? "C" : "B"; Go(PickupPreviewStep.Guidance); }
        }
        string Book => Stop == "B" ? "A1" : "A2";
        public void ShowCameraPhase(CameraRestartPhase phase) { cameraPhase = phase; Build(LogicalHeight); }
        void RefreshCamera()
        {
            if (cameraRestart == null || cameraRestart.cameraManager == null)
            { Error = "Demo giao diện: chưa kết nối camera thật."; Build(LogicalHeight); return; }
            cameraRestart.Changed -= ShowCameraPhase; cameraRestart.Changed += ShowCameraPhase; cameraRestart.Restart();
        }
        RectTransform Box(Transform p, string name, float x, float y, float w, float h, string fill = "FFFFFF", float radius = 16, string line = "DFE7F1")
        {
            var r = JarVisUi.Rect(p, name, x, y, w, h); var im = r.gameObject.AddComponent<Image>(); im.color = Color.white; im.raycastTarget = false;
            UniformUiSurface.Use(im, radius, C(fill), C(line), line == fill ? 0 : 1, false, false); return r;
        }
        TMP_Text Text(Transform p, string text, float x, float y, float w, float h, float size = 15, bool heavy = false, string color = "152640", bool center = false)
        {
            var t = JarVisUi.Text(p, heavy ? bold : regular, text, x, y, w, h, size);
            t.fontSize = size; t.fontStyle = FontStyles.Normal; t.fontWeight = heavy ? FontWeight.Bold : FontWeight.Regular;
            t.color = C(color); t.characterSpacing = 0; t.alignment = center ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineLeft;
            t.overflowMode = TextOverflowModes.Overflow; return t;
        }
        ImportedUtilityIcon Icon(Transform p, string name, float x, float y, float size, string color)
        { var i = JarVisUi.Icon(p, name, x, y, size); i.color = C(color); return i; }
        Button Btn(Transform p, string name, string label, float x, float y, float w, float h, Action action, bool primary = true, string icon = null)
        {
            var r = Box(p, name, x, y, w, h, primary ? "1255D5" : "F0F5FC", 14, primary ? "1255D5" : "DBE5F2");
            var b = PrototypeUi.Button(r, action); var feedback = b.GetComponent<ImportedPressFeedback>(); if (feedback != null) feedback.enabled = false;
            float iw = icon == null ? 0 : 28;
            // Icon and label share one centered group, not independent screen positions.
            var t = Text(r, label, 10, 0, w - 20, h, 14, true, primary ? "FFFFFF" : "152640", true);
            if (icon != null)
            {
                float group = Mathf.Min(w - 24, t.GetPreferredValues(label).x + iw); float start = (w - group) / 2;
                Icon(r, icon, start, (h - 21) / 2, 21, primary ? "FFFFFF" : "1255D5");
                t.rectTransform.anchoredPosition = new Vector2(start + iw, 0); t.rectTransform.sizeDelta = new Vector2(group - iw, h);
            }
            return b;
        }
        void Header(string title, bool camera = false)
        {
            var header = Box(page, "Header", 0, 30, 412, 66, "FFFFFF", 0, "FFFFFF");
            Btn(header, "Back", "", 12, 8, 44, 48, () => Go(PickupPreviewStep.Task), false, "arrow-left");
            Text(header, title, 69, 8, 327, 48, 19, true);
        }
        void Badge(Transform p, string text, float x, float y, float w, string fill, string color)
        { var r = Box(p, text, x, y, w, 29, fill, 10, fill); Text(r, text, 8, 0, w - 16, 29, 11, true, color, true); }
        void BookRow(string book, string stop, float y, string color)
        {
            var row = Box(page, "Book " + book, 20, y, 372, 94);
            var tag = Box(row, book, 14, 16, 56, 62, color, 12, color); Text(tag, book, 0, 0, 56, 62, 23, true, "FFFFFF", true);
            Text(row, "Sách " + book, 86, 15, 242, 28, 19, true); Text(row, "Kệ " + stop + " · 1 quyển", 86, 47, 242, 27, 14, false, "5A6B82");
        }
        void Photo()
        {
            Box(page, "Camera preview backing", 0, 30, 412, LogicalHeight - 62, "243546", 0, "243546");
            if (roomPhoto != null)
            {
                var r = JarVisUi.Rect(page, "Room photo - static review only", 0, 30, 412, LogicalHeight - 62); var raw = r.gameObject.AddComponent<RawImage>();
                raw.texture = roomPhoto; raw.raycastTarget = false;
                // Sample only the room region in an owner-provided photo; exclude the old UI controls.
                raw.uvRect = new Rect(0, .27f, 1, .59f); raw.color = new Color(.85f, .88f, .94f);
            }
        }
        void NavBackground()
        {
            Photo(); var bar = Box(page, "AR toolbar", 12, 40, 388, 48, "FFFFFF", 14);
            Btn(bar, "Exit", "Thoát", 4, 2, 76, 44, () => Go(PickupPreviewStep.Task), false);
            Btn(bar, "Align again", "Căn lại", 88, 2, 126, 44, () => Go(PickupPreviewStep.AlignA), false, "refresh-arrows");
            Text(bar, "JarVis", 272, 2, 100, 44, 16, true, "1255D5", true);
            Badge(page, "ẢNH PHÒNG MINH HỌA · KHÔNG PHẢI CAMERA LIVE", 35, 100, 342, "26394D", "FFFFFF");
            Text(page, "B", 91, LogicalHeight * .29f, 44, 44, 27, true, "E52242", true);
            Text(page, "C", 302, LogicalHeight * .31f, 44, 44, 27, true, "E52242", true);
        }
        void Dock()
        {
            var d = Box(page, "Pickup navigation dock", 16, LogicalHeight - 240, 380, 190, "172A42", 20, "334E6E");
            Text(d, "LẤY SÁCH · " + ((PickedB ? 1 : 0) + (PickedC ? 1 : 0)) + "/2", 16, 10, 348, 28, 14, true, "FFFFFF");
            var b = Btn(d, "Go B", PickedB ? "B · Đã lấy" : "Tới B", 16, 47, 168, 44, () => SelectStop("B"), Stop == "B"); b.interactable = !PickedB;
            var c = Btn(d, "Go C", PickedC ? "C · Đã lấy" : "Tới C", 196, 47, 168, 44, () => SelectStop("C"), Stop == "C"); c.interactable = !PickedC;
            if (PickedB) Gray(b); if (PickedC) Gray(c);
            Text(d, "Tới " + Stop + " · Sách " + Book, 16, 96, 348, 29, 15, true, "FFFFFF", true);
            Btn(d, "Begin pickup", "Lấy " + Book + " tại " + Stop, 16, 133, 348, 46, () => Go(PickupPreviewStep.Location), true, "scan-barcode");
        }
        static void Gray(Button b) { b.GetComponent<Image>().color = new Color(.6f, .64f, .69f); var g = b.GetComponent<CanvasGroup>(); if (g == null) g = b.gameObject.AddComponent<CanvasGroup>(); g.alpha = .62f; }
        RectTransform Modal(string title, float height, bool success = false)
        {
            var shade = Box(page, "Modal shade", 0, 30, 412, LogicalHeight - 62, "00000066", 0, "00000066"); shade.GetComponent<Image>().raycastTarget = true;
            var m = Box(page, "Modal " + title, 24, Mathf.Max(140, (LogicalHeight - height) / 2), 364, height);
            Text(m, title, 20, 15, 284, 34, 18, true, success ? "007C52" : "152640");
            var close = Btn(m, "Close modal", "", 308, 10, 44, 44, () => Go(PickupPreviewStep.Guidance), false, "x");
            close.GetComponentInChildren<ImportedUtilityIcon>().color = C("D32743");
            return m;
        }
        void Progress(Transform p, bool barcode)
        {
            string[] labels = { "QR " + Stop, "Barcode " + Book };
            for (int i = 0; i < 2; i++)
            {
                bool active = barcode ? i == 1 : i == 0; float x = 18 + i * 162;
                var dot = Box(p, "Step " + i, x, 63, 27, 27, active ? "1255D5" : "E1E7EF", 13, active ? "1255D5" : "E1E7EF");
                Text(dot, (i + 1).ToString(), 0, 0, 27, 27, 13, true, active ? "FFFFFF" : "586B82", true);
                Text(p, labels[i], x + 35, 63, 120, 27, 13, active, active ? "152640" : "586B82");
            }
        }
        void Frame(Transform p, float y, bool barcode)
        {
            float w = barcode ? 300 : 236, h = barcode ? 132 : 236;
            // A genuine camera viewport replaces this neutral well when connected after approval.
            // Keep the square QR / wide barcode framing visible against both white and camera backgrounds.
            var well = Box(p, "Camera well - preview only", (364 - w) / 2 - 8, y - 8, w + 16, h + 16, "233A50", 12, "233A50");
            var r = JarVisUi.Rect(p, barcode ? "Horizontal barcode frame" : "Square QR frame", (364 - w) / 2, y, w, h);
            r.gameObject.AddComponent<RoompresentCodeFrame>().SetBarcode(barcode);
            Icon(r, barcode ? "scan-barcode" : "qr-code", (w - 64) / 2, (h - 64) / 2, 64, "A4D5FF");
        }
        void RefreshButton(Transform p, float x, float y, float w)
        {
            string label = cameraPhase == CameraRestartPhase.Stopping ? "Đang tắt camera…" : cameraPhase == CameraRestartPhase.Starting ? "Đang bật camera…" : cameraPhase == CameraRestartPhase.WaitingForFrame ? "Đang chờ hình mới…" : "Làm mới camera";
            var b = Btn(p, "Refresh camera", label, x, y, w, 48, RefreshCamera, false, "refresh-arrows");
            b.interactable = cameraPhase != CameraRestartPhase.Stopping && cameraPhase != CameraRestartPhase.Starting && cameraPhase != CameraRestartPhase.WaitingForFrame;
        }
        public void Build(float height)
        {
            LogicalHeight = Mathf.Max(700, height);
            if (page != null) { page.gameObject.SetActive(false); if (Application.isPlaying) Destroy(page.gameObject); else DestroyImmediate(page.gameObject); }
            page = Box(transform, "Pickup UI demo", 0, 0, 412, LogicalHeight, "F6F8FC", 0, "F6F8FC");
            Badge(page, "DEMO GIAO DIỆN · CHƯA ÁP DỤNG VÀO APP", 0, 0, 412, "DDEBFF", "124CA7");
            if (Step == PickupPreviewStep.Task)
            {
                Header("Lấy sách về A"); Badge(page, "Chưa bắt đầu", 20, 112, 126, "E8EFFA", "365470");
                Text(page, "LẤY 2 QUYỂN SÁCH", 20, 154, 372, 49, 24, true);
                Text(page, "Mỗi điểm lấy 1 quyển, mang đủ về bàn A.", 20, 209, 372, 38, 14, false, "586B82");
                BookRow("A1", "B", 264, "1255D5"); BookRow("A2", "C", 372, "008B91");
                var dest = Box(page, "Return destination", 20, 486, 372, 58, "E6F0FF", 14, "E6F0FF"); Icon(dest, "map", 16, 17, 24, "1255D5"); Text(dest, "Mang về bàn A", 52, 0, 298, 58, 16, true);
                Btn(page, "Accept mission", "Nhận nhiệm vụ", 20, LogicalHeight - 104, 372, 52, () => Go(PickupPreviewStep.AlignA));
            }
            else if (Step == PickupPreviewStep.AlignA || Step == PickupPreviewStep.ReturnScan)
            {
                Photo(); Header(Step == PickupPreviewStep.AlignA ? "Căn vị trí tại A" : "Xác nhận đã về A", true);
                Badge(page, "DEMO · CAMERA CHƯA KẾT NỐI", 65, 112, 282, "26394D", "FFFFFF");
                float y = Mathf.Min(250, LogicalHeight * .29f); var host = JarVisUi.Rect(page, "QR A scanner", 24, y, 364, 320);
                Frame(host, 0, false); Badge(host, "Đưa QR A vào khung", 50, 257, 264, "26394D", "FFFFFF");
                RefreshButton(page, 40, LogicalHeight - 110, 332);
            }
            else if (Step == PickupPreviewStep.Complete)
            {
                Header("Kết quả nhiệm vụ");
                var check = Box(page, "Success badge", 170, 123, 72, 72, "00945D", 36, "00945D"); Icon(check, "check", 18, 18, 36, "FFFFFF");
                Badge(page, "HOÀN THÀNH", 116, 216, 180, "DDF6EA", "007C52");
                Text(page, "ĐÃ MANG ĐỦ 2/2\nQUYỂN VỀ A", 24, 264, 364, 96, 27, true, "152640", true);
                string[] rows = { "A1 · Đã lấy tại B", "A2 · Đã lấy tại C", "Điểm A · Đã xác thực QR" };
                for (int i = 0; i < 3; i++) { var row = Box(page, "Result " + i, 20, 390 + i * 67, 372, 55); Icon(row, "check", 16, 16, 23, "00945D"); Text(row, rows[i], 54, 0, 300, 55, 16, true); }
                Btn(page, "Restart demo", "Về danh sách nhiệm vụ", 20, LogicalHeight - 104, 372, 52, ResetDemo);
            }
            else
            {
                NavBackground(); Dock();
                if (Step == PickupPreviewStep.Aligned)
                { var m = Modal("ĐÃ CĂN VỊ TRÍ A", 206, true); Text(m, "Đã xác thực QR A. Sẵn sàng đi lấy sách.", 20, 63, 324, 56, 15); Btn(m, "Open AR", "Mở chỉ đường", 20, 137, 324, 49, () => Go(PickupPreviewStep.Guidance)); }
                else if (Step == PickupPreviewStep.Location || Step == PickupPreviewStep.Barcode)
                {
                    bool bar = Step == PickupPreviewStep.Barcode; var m = Modal("Xác thực tại " + Stop, bar ? 350 : 454); Progress(m, bar); Frame(m, 110, bar);
                    float footer = bar ? 250 : 354;
                    Text(m, bar ? "Quét barcode sách " + Book : "Quét QR trên kệ " + Stop, 18, footer, 328, 32, 14, true, "152640", true);
                    RefreshButton(m, 18, footer + 42, 328);
                }
                else if (Step == PickupPreviewStep.LocationAccepted)
                { var m = Modal("ĐÃ XÁC THỰC QR " + Stop, 216, true); Text(m, "Điểm lấy đã đúng. Tiếp theo quét barcode sách " + Book + ".", 20, 66, 324, 62, 15); Btn(m, "Continue barcode", "OK · Quét barcode " + Book, 20, 147, 324, 49, () => Go(PickupPreviewStep.Barcode)); }
                else if (Step == PickupPreviewStep.BookAccepted)
                { var m = Modal("ĐÚNG SÁCH " + Book, 274, true); Text(m, "Đã xác thực QR " + Stop + " và barcode " + Book, 20, 66, 324, 48, 15); Badge(m, "SỐ LƯỢNG: 1 QUYỂN", 20, 126, 324, "E6F0FF", "23405C"); Btn(m, "Confirm book", "Xác nhận lấy", 20, 174, 324, 49, ConfirmBook); Text(m, "Chỉ ghi nhận sau khi bạn xác nhận.", 20, 231, 324, 26, 11, false, "586B82", true); }
                else if (Step == PickupPreviewStep.ReturnA)
                {
                    // Cover the selection dock locally; both books have already been confirmed.
                    var d = Box(page, "Return A dock", 16, LogicalHeight - 240, 380, 190, "172A42", 20, "334E6E");
                    Text(d, "ĐÃ LẤY ĐỦ 2/2 QUYỂN", 16, 15, 348, 35, 18, true, "FFFFFF"); Text(d, "Đi theo chỉ đường về bàn A.", 16, 62, 348, 30, 15, false, "D5E4F5");
                    Btn(d, "Confirm return", "Xác nhận mang về A", 16, 124, 348, 50, () => Go(PickupPreviewStep.ReturnScan), true, "qr-code");
                }
            }
            if (Error != null) { var e = Box(page, "Scan feedback", 20, LogicalHeight - 188, 372, 66, "FFF0EE", 14, "F1B8B3"); Text(e, Error, 14, 6, 344, 54, 13, true, "A72D2D"); }
            // Review-only controls are explicit; never interpreted as camera recognition.
            var demo = Box(page, "Demo toolbar", 0, LogicalHeight - 32, 412, 32, "DDEBFF", 0, "DDEBFF");
            Text(demo, "MÃ THỬ", 8, 0, 64, 32, 10, true, "124CA7");
            Btn(demo, "Demo scan valid", "Quét đúng", 76, 0, 116, 32, () => Scan(Step == PickupPreviewStep.Location ? "JWM:MAIN:" + Stop : Step == PickupPreviewStep.Barcode ? (Stop == "B" ? "4714551570610" : "4714551570603") : "JWM:MAIN:E"), false);
            Btn(demo, "Demo scan invalid", "Quét sai", 198, 0, 106, 32, () => Scan("WRONG-DEMO-CODE"), false);
            Btn(demo, "Reset demo", "Làm lại", 310, 0, 94, 32, ResetDemo, false);
        }
    }
}
