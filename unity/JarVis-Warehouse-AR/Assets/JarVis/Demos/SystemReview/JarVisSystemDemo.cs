using System;
using System.Collections.Generic;
using System.Linq;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Demo2026
{
    // Isolated presentation rehearsal. No disk writes, backend calls, or XR lifecycle ownership.
    public sealed partial class JarVisSystemDemo : MonoBehaviour
    {
        public Texture2D ReferencePhoto;
        public Texture2D ReferenceScan;
        public TMP_FontAsset RegularFont, BoldFont;
        public bool Rehearsal = true;
        public event Action<string> CommandRequested;
        public event Action<string> RouteRequested;
        public event Action<string> MessageRequested;
        public event Action CameraRefreshRequested;
        public event Action RescanARequested;
        public event Action ReturnARequested;
        public event Action ExitRequested;
        public event Action<string> IncidentRequested;
        public RoompresentDeliveryRecord Record = new RoompresentDeliveryRecord();
        public readonly RoompresentBarcodeGate Gate = new RoompresentBarcodeGate();
        public readonly RoompresentDeliveryVerification Proof = new RoompresentDeliveryVerification();
        public bool TrackingTrusted { get; private set; }
        public string ScreenId { get; private set; } = "home";
        public string SelectedStop { get; private set; } = "B";
        public bool Returning { get; private set; }
        public bool AwaitingAck { get; private set; }
        public bool Dark, English, LargeText, ReduceMotion;
        public float H = 915;
        public float PreviewPhase = -1;
        public float Distance = 5.4f;
        public string LastCommand { get; private set; }
        public RectTransform page, overlayRoot;
        public CanvasGroup ModalGroup { get; private set; }
        public bool ModalOpen => overlayRoot != null && overlayRoot.childCount > 0;
        public string OverlayKind { get; private set; }
        static readonly Color Ink = new Color32(21,38,64,255), Muted = new Color32(83,101,123,255), Blue = new Color32(21,88,232,255), Green = new Color32(0,135,91,255), Bg = new Color32(243,246,251,255);
        RectTransform root, toastRoot;
        bool ready, guidanceAcknowledged;
        string initialPopupBook, taskFilter = "all";
        readonly List<KeyValuePair<string,string>> messages = new List<KeyValuePair<string,string>>();
        TMP_InputField messageInput;
        public IReadOnlyList<KeyValuePair<string,string>> Messages => messages;

        void Start() { if (!ready) Configure(); }
        public void Configure()
        {
            ready = true; root = (RectTransform)transform;
            if (RegularFont != null) JarVisVisuals.Regular = RegularFont;
            if (BoldFont != null) JarVisVisuals.Bold = BoldFont;
            JarVisVisuals.EnsureFonts();
            if (root.rect.height > 600) H = root.rect.height;
            if (ReferencePhoto == null) ReferencePhoto = Resources.Load<Texture2D>("JarVisReview/RoomReference");
            if (messages.Count == 0) messages.Add(new KeyValuePair<string,string>("ai", "Mình đang hỗ trợ nhiệm vụ #00001: A1 tới B, A2 tới C. Bạn cần giúp gì?"));
            Go(ScreenId);
        }
        void LateUpdate()
        {
            if (!ready || !Application.isPlaying) return;
            if (Mathf.Abs(root.rect.height - H) > 1 && root.rect.height > 600) { H = root.rect.height; Go(ScreenId); }
        }
        void Clear(Transform p)
        {
            if (p == null) return;
            foreach (Transform t in p.Cast<Transform>().ToArray()) { t.gameObject.SetActive(false); if (Application.isPlaying) Destroy(t.gameObject); else DestroyImmediate(t.gameObject); }
        }
        public void Go(string id)
        {
            if (root == null) root = (RectTransform)transform;
            CloseModal();
            if (page != null) { page.gameObject.SetActive(false); if (Application.isPlaying) Destroy(page.gameObject); else DestroyImmediate(page.gameObject); }
            if (overlayRoot != null) { if (Application.isPlaying) Destroy(overlayRoot.gameObject); else DestroyImmediate(overlayRoot.gameObject); }
            if (toastRoot != null) { if (Application.isPlaying) Destroy(toastRoot.gameObject); else DestroyImmediate(toastRoot.gameObject); }
            ScreenId = id;
            page = JarVisVisuals.Rect(root, "Page " + id, 0, 0, 412, H);
            var bg = page.gameObject.AddComponent<Image>(); bg.color = Dark ? JarVisVisuals.Hex("101c2d") : Bg; bg.raycastTarget = false;
            overlayRoot = JarVisVisuals.Rect(root, "Overlays - camera stays active", 0, 0, 412, H);
            toastRoot = JarVisVisuals.Rect(root, "Feedback", 0, 0, 412, H);
            if (!Secondary(id)) switch (id)
            {
                case "splash": Splash(); break;
                case "login": Login(); break;
                case "home": Home(); break;
                case "tasks": Tasks(); break;
                case "detail": Detail(); break;
                case "scan-products": Scan(false, "Quét barcode sản phẩm", "Quét đủ A1 và A2 để bắt đầu."); break;
                case "scan-A": Scan(true, "Quét QR vị trí A", "Căn phòng trước khi mở chỉ dẫn AR."); break;
                case "scanner": Scan(false, "Quét mã", "Đưa mã barcode vào khung."); break;
                case "ar": Ar(); break;
                case "map": MapPage(); break;
                case "chat": ChatPage(); break;
                default: Home(); break;
            }
            StatusBar(id == "ar" || id.StartsWith("scan") || id == "scanner");
            overlayRoot.SetAsLastSibling(); toastRoot.SetAsLastSibling();
        }
        void StatusBar(bool onCamera)
        {
            if (Rehearsal) Label(page, "DEMO UNITY", 20, 15, 150, 18, 10, true, onCamera ? Color.white : Muted);
            Label(page, "JarVis", 300, 15, 92, 18, 10, true, onCamera ? Color.white : Muted).alignment = TextAlignmentOptions.Right;
        }
        public RectTransform Card(string name, float x, float y, float w, float h, Color? fill = null)
            => JarVisVisuals.Panel(page, name, x, y, w, h, fill ?? (Dark ? JarVisVisuals.Hex("1b2c43") : Color.white), 22);
        public TMP_Text Label(Transform p, string text, float x, float y, float w, float h, float size = 15, bool bold = false, Color? color = null)
            => JarVisVisuals.Text(p, text, x, y, w, h, LargeText ? size * 1.08f : size, color ?? (Dark ? Color.white : Ink), bold);
        public Button Action(Transform p, string text, float x, float y, float w, float h, Action callback, bool primary = false, string icon = null)
            => JarVisVisuals.Button(p, text, x, y, w, h, primary ? Blue : (Dark ? JarVisVisuals.Hex("2c405b") : JarVisVisuals.Hex("edf2f9")), primary ? Color.white : Dark ? Color.white : Ink, callback, icon);
        public void Emit(string command)
        {
            LastCommand = command;
            if (CommandRequested != null) CommandRequested(command);
            else Toast("Bản demo: thao tác được giữ để nối với chức năng hiện có.");
        }
        public void Header(string title, string subtitle = null)
        {
            IconButton(page, "back", 14, 48, 48, () => Go("home"), false);
            Label(page, title, 72, 54, 280, 38, 26, true);
            if (!string.IsNullOrEmpty(subtitle)) Label(page, subtitle, 20, 103, 372, 32, 13, false, Dark ? Color.white : Muted);
        }
        public void BottomNav(string selected)
        {
            var dock = Card("Navigation", 12, H - 88, 388, 76);
            string[] labels = { "Trang chủ", "Nhiệm vụ", "Quét mã", "Bản đồ", "JarVis" }, ids = { "home", "tasks", "scanner", "map", "chat" }, icons = { "home", "tasks", "scan", "map", "chat" };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i]; float x = i * 76 + 4;
                var b = Action(dock, "", x, 4, 76, 66, () => Go(id));
                UniformUiSurface.Use(b.GetComponent<Image>(), 17, id == selected ? JarVisVisuals.Hex("e9f0ff") : Dark ? JarVisVisuals.Hex("1b2c43") : Color.white, Color.clear, 0, false, false);
                Icon(b.transform, icons[i], 26, 8, 24, id == selected ? Blue : Dark ? Color.white : Muted);
                Label(b.transform, labels[i], 0, 40, 76, 20, 10, id == selected, id == selected ? Blue : Dark ? Color.white : Muted).alignment = TextAlignmentOptions.Center;
            }
        }
        Image Icon(Transform p, string icon, float x, float y, float size, Color color)
        {
            var rt = JarVisVisuals.Rect(p, "Icon " + icon, x, y, size, size); var im = rt.gameObject.AddComponent<Image>(); im.sprite = Resources.Load<Sprite>("Prototype/Icons/" + icon); im.color = color; im.preserveAspect = true; im.raycastTarget = false; return im;
        }
        Button IconButton(Transform p, string icon, float x, float y, float size, Action action, bool red)
        {
            var b = JarVisVisuals.Button(p, "", x, y, size, size, red ? JarVisVisuals.Hex("d82c46") : Color.white, Ink, action);
            if (!string.IsNullOrEmpty(icon)) Icon(b.transform, icon, (size - 22) / 2, (size - 22) / 2, 22, red ? Color.white : Ink); return b;
        }
        TMP_InputField Input(Transform p, string placeholder, float x, float y, float w, float h, bool multiline = false)
        {
            var rt = JarVisVisuals.Panel(p, placeholder, x, y, w, h, Dark ? JarVisVisuals.Hex("243954") : JarVisVisuals.Hex("f1f4f9"), 16);
            var field = rt.gameObject.AddComponent<TMP_InputField>(); field.targetGraphic = rt.GetComponent<Image>();
            var viewport = JarVisVisuals.Rect(rt, "Text viewport", 16, 7, w - 32, h - 14); viewport.gameObject.AddComponent<RectMask2D>();
            field.textViewport = viewport; var txt = Label(viewport, "", 0, 0, w - 32, h - 14, 15); txt.alignment = TextAlignmentOptions.MidlineLeft;
            field.textComponent = (TextMeshProUGUI)txt;
            var hint = Label(viewport, placeholder, 0, 0, w - 32, h - 14, 14, false, Muted); hint.alignment = TextAlignmentOptions.MidlineLeft; field.placeholder = hint;
            field.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine; return field;
        }
        void Face(Transform p, float x, float y, float w, float h, bool speaking = false)
        {
            var rt = JarVisVisuals.Rect(p, "JarVis eyes and waveform", x, y, w, h); var face = rt.gameObject.AddComponent<JarVisFace>(); face.Amplitude = speaking ? .65f : .15f; face.Speaking = speaking; face.PreviewPhase = PreviewPhase; face.DemoAmplitude = Rehearsal;
        }
        void Splash()
        {
            Face(page, 86, H * .28f, 240, 100);
            Label(page, "JarVis", 20, H * .28f + 124, 372, 64, 46, true).alignment = TextAlignmentOptions.Center;
            Label(page, "Trợ lý kho thông minh", 20, H * .28f + 196, 372, 34, 18, false, Muted).alignment = TextAlignmentOptions.Center;
            Action(page, "Bắt đầu", 20, H - 132, 372, 56, () => Go("login"), true, "arrowUp");
        }
        void Login()
        {
            Face(page, 20, 80, 160, 70);
            Label(page, "Chào mừng\ntrở lại.", 20, 176, 372, 108, 38, true);
            Label(page, "Đăng nhập để tiếp tục công việc tại kho.", 20, 302, 372, 46, 15, false, Muted);
            var user = Input(page, "Tên đăng nhập", 20, 370, 372, 56); var password = Input(page, "Mật khẩu", 20, 440, 372, 56); password.contentType = TMP_InputField.ContentType.Password;
            Action(page, "Đăng nhập", 20, 520, 372, 56, () => { if (Rehearsal) Go("home"); else Emit("auth.login"); }, true);
            Action(page, "Cấu hình kết nối", 20, 590, 372, 50, () => OpenModal("Kết nối máy chủ", "Giữ địa chỉ và phiên đăng nhập hiện có của JarVis.", "Mở cấu hình", () => Emit("settings.endpoint")), false, "settings");
        }
        void Home()
        {
            Label(page, "Chào Justin", 20, 57, 290, 42, 30, true); IconButton(page, "settings", 344, 52, 48, () => Go("profile"), false);
            Label(page, "Sẵn sàng cho ca làm hôm nay?", 20, 108, 372, 30, 15, false, Muted);
            var hero = Card("Current mission", 20, 162, 372, 226, Blue);
            Label(hero, "CÔNG VIỆC ĐANG LÀM", 22, 22, 328, 24, 12, true, Color.white);
            Label(hero, "Cất hàng\nđúng vị trí.", 22, 58, 328, 90, 32, true, Color.white);
            Action(hero, "Mở nhiệm vụ #00001", 20, 164, 332, 48, () => Go("tasks"), false, "tasks");
            Label(page, "Truy cập nhanh", 20, 416, 372, 34, 22, true);
            string[] names = { "Tra tồn kho", "Nhập / Xuất", "Sản phẩm & tem", "Báo sự cố" }, ids = { "inventory", "transactions", "labels", "incident" }, icons = { "box", "arrowUp", "tag", "alert" };
            for (int i = 0; i < 4; i++) { string id = ids[i]; float x = 20 + i % 2 * 192, y = 468 + i / 2 * 87; var c = Card(names[i], x, y, 180, 76); Icon(c, icons[i], 16, 14, 23, Blue); Label(c, names[i], 16, 44, 150, 22, 13, true); JarVisVisuals.Button(c, "", 0, 0, 180, 76, Color.clear, Ink, () => Go(id)).transform.SetAsFirstSibling(); }
            if (H > 810) { var ai = Card("Assistant shortcut", 20, 668, 372, 99); Face(ai, 16, 18, 114, 54); Label(ai, "JarVis luôn sẵn sàng", 142, 16, 212, 28, 16, true); Action(ai, "Hỏi JarVis", 140, 48, 210, 40, () => Go("chat")); }
            BottomNav("home");
        }
        void Tasks()
        {
            Header("Nhiệm vụ", "Theo dõi và hoàn thành từng công việc.");
            int completed = Record.Complete ? 1 : 0;
            string[] tabs = { "Tất cả (1)", "Đang làm (" + (1 - completed) + ")", "Đã giao (" + completed + ")" }, keys = { "all", "doing", "done" };
            for (int i = 0; i < 3; i++) { string k = keys[i]; var b = Action(page, tabs[i], 20 + i * 126, 149, 120, 48, () => { taskFilter = k; Go("tasks"); }, taskFilter == k); b.GetComponentInChildren<TMP_Text>().fontSize = 12; }
            if ((taskFilter == "done" && !Record.Complete) || (taskFilter == "doing" && Record.Complete)) { Label(page, "Chưa có nhiệm vụ trong mục này.", 20, 252, 372, 60, 18, true); BottomNav("tasks"); return; }
            var card = Card("Task 00001", 20, 222, 372, 358);
            Label(card, "NHIỆM VỤ #00001 CẤT HÀNG", 20, 20, 332, 30, 17, true);
            Label(card, "A1 → B  ·  A2 → C", 20, 63, 332, 40, 26, true);
            Label(card, "ROOMPRESENT", 20, 119, 332, 24, 12, true, Blue);
            Label(card, "1   Quét barcode hai sản phẩm A1, A2.\n2   Quét QR vị trí A để căn phòng.\n3   Giao đúng B/C, xác thực QR rồi barcode.", 20, 155, 332, 90, 14, false, Muted);
            Action(card, Record.Complete ? "Xem kết quả nhiệm vụ" : "Bắt đầu nhiệm vụ", 20, 276, 332, 56, () => { if (Record.Complete) { Go("ar"); Completion(); } else StartMission(); }, true, "scan");
            Action(page, "Chi tiết nhiệm vụ", 20, 606, 372, 50, () => Go("detail"), false, "file");
            BottomNav("tasks");
        }
        void Detail()
        {
            Header("Chi tiết nhiệm vụ", "#00001 · CẤT HÀNG · Roompresent");
            var c = Card("Task summary", 20, 155, 372, 148); Label(c, "A1 → B  ·  A2 → C", 20, 22, 332, 40, 26, true); Label(c, "2 quyển · Xác thực từng sản phẩm\nƯu tiên quãng đường tối ưu", 20, 77, 332, 52, 15, false, Muted);
            ProductRow(page, "A1", "CPNB-1004", "B · Xe sách trái", 20, 325, 372);
            ProductRow(page, "A2", "CPNB-1003", "C · Xe sách phải", 20, 429, 372);
            Label(page, "QUY TRÌNH BẮT BUỘC", 20, 558, 372, 24, 12, true, Blue);
            Label(page, "Barcode A1/A2 → QR A → dẫn đường → QR điểm giao → barcode đúng sách.", 20, 596, 372, 72, 16, true);
            Action(page, "Bắt đầu nhiệm vụ", 20, H - 76, 372, 56, StartMission, true, "scan");
        }
        void ProductRow(Transform p, string book, string sku, string dest, float x, float y, float w)
        {
            var c = JarVisVisuals.Panel(p, book, x, y, w, 88, Dark ? JarVisVisuals.Hex("1b2c43") : Color.white, 20);
            var badge = JarVisVisuals.Panel(c, "Book badge", 16, 16, 54, 56, JarVisVisuals.Hex("e9f0ff"), 14); Label(badge, book, 0, 9, 54, 36, 23, true, Blue).alignment = TextAlignmentOptions.Center;
            Label(c, "Vở " + book + " · " + sku, 86, 16, w - 100, 28, 16, true); Label(c, dest, 86, 48, w - 100, 25, 14, false, Muted);
        }
        public void StartMission()
        {
            Record.accepted = true; Gate.Reset(); AwaitingAck = false; TrackingTrusted = false; guidanceAcknowledged = false; Returning = false; Proof.Cancel(); Go("scan-products");
        }
        void CameraBackdrop()
        {
            var rt = JarVisVisuals.Rect(page, "Reference photograph - not live camera", 0, 0, 412, H); var img = rt.gameObject.AddComponent<RawImage>(); img.texture = ReferencePhoto; img.color = ReferencePhoto != null ? Color.white : JarVisVisuals.Hex("33475a"); img.raycastTarget = false;
            rt.SetAsFirstSibling();
            if (ReferencePhoto != null) { float aspect = 412 / H, texAspect = (float)ReferencePhoto.width / ReferencePhoto.height; float u = aspect / texAspect; img.uvRect = u < 1 ? new Rect((1 - u) / 2, 0, u, 1) : new Rect(0, (1 - 1 / u) / 2, 1, 1 / u); }
            if (Rehearsal) { var badge = JarVisVisuals.Panel(page, "Reference notice", 20, 112, 372, 32, new Color(0.05f,.09f,.14f,.83f), 12); Label(badge, "ẢNH PHÒNG THAM CHIẾU · KHÔNG PHẢI CAMERA TRỰC TIẾP", 8, 7, 356, 19, 8, true, Color.white).alignment = TextAlignmentOptions.Center; }
        }
        void Scan(bool qr, string title, string subtitle)
        {
            CameraBackdrop();
            var shade = JarVisVisuals.Panel(page, "Scanner shade", 0, 0, 412, H, new Color(0.02f,.05f,.10f,.60f), 0); shade.GetComponent<Image>().raycastTarget = false;
            IconButton(page, "close", 16, 49, 48, () => OpenExit(), true);
            Label(page, qr ? "QUÉT QR VỊ TRÍ" : "QUÉT BARCODE SẢN PHẨM", 80, 60, 312, 30, 14, true, Color.white);
            float y = H * .27f;
            Label(page, title, 24, y - 58, 364, 42, 27, true, Color.white).alignment = TextAlignmentOptions.Center;
            ScanWindow(page, qr, y + 16);
            float after = y + (qr ? 262 : 160);
            Label(page, qr ? "Đặt toàn bộ QR vào khung vuông" : "Giữ mã vạch nằm ngang trong khung", 20, after + 12, 372, 54, 15, false, Color.white).alignment = TextAlignmentOptions.Center;
            var lower = Card("Scan instructions", 20, H - 204, 372, 184);
            Label(lower, subtitle, 18, 17, 336, 48, 16, true);
            if (!qr) { string count = (Gate.Contains(RoompresentDeliveryRecord.A1) ? 1 : 0) + (Gate.Contains(RoompresentDeliveryRecord.A2) ? 1 : 0) + "/2 sản phẩm"; Label(lower, count, 18, 74, 336, 26, 17, true, Blue); }
            else Label(lower, "QR A · Mốc cố định trên bàn", 18, 74, 336, 26, 15, true, Blue);
            Action(lower, "Làm mới camera", 18, 118, 336, 48, RefreshCamera, false, "refresh");
        }
        public JarVisScanSweep ScanWindow(Transform p, bool qr, float y)
        {
            float w = qr ? 236 : 324, h = qr ? 236 : 126;
            var frame = JarVisVisuals.Rect(p, qr ? "QR square frame" : "Barcode wide frame", (412 - w) / 2, y, w, h);
            var corners = frame.gameObject.AddComponent<RoompresentCodeFrame>(); corners.SetBarcode(!qr); frame.sizeDelta = new Vector2(w, h);
            var sweep = frame.gameObject.AddComponent<JarVisScanSweep>(); sweep.IsBarcode = !qr; sweep.Scanning = true; sweep.PreviewPhase = PreviewPhase; sweep.RefreshVisual();
            return sweep;
        }
        public bool SubmitScan(string code, string source, bool measuredTracking, float distance)
        {
            if (AwaitingAck || source != "RearCamera") return false;
            if (ScreenId == "scan-products")
            {
                if (Gate.Contains(code)) { Toast("Mã này đã quét. Hãy quét quyển còn lại."); return false; }
                if (!Gate.Observe(code, source)) { Toast("Sai sản phẩm. Cần barcode A1 hoặc A2."); return false; }
                Record.ScanProduct(code); AwaitingAck = true; initialPopupBook = code == RoompresentDeliveryRecord.A1 ? "A1" : "A2";
                OpenModal("Quét thành công " + initialPopupBook, "Vở " + initialPopupBook + "\n" + (initialPopupBook == "A1" ? "CPNB-1004 · Giao tới B\n" : "CPNB-1003 · Giao tới C\n") + code, "OK · Tiếp tục", AcknowledgeScan);
                OverlayKind = "product-success"; return true;
            }
            if (ScreenId == "scan-A")
            {
                if (code != "JWM:MAIN:E" && code != "JWM:MAIN:A") { Toast("Cần QR vị trí A trên bàn xuất phát."); return false; }
                if (!measuredTracking) { Toast("Đã đọc QR A. Giữ máy để hoàn tất căn vị trí và hướng."); return false; }
                if (!Gate.Ready || !Record.Loaded) { Toast("Cần xác minh đủ hai sản phẩm trước."); return false; }
                TrackingTrusted = true; AwaitingAck = true;
                OpenModal("Đã nhận vị trí A", "Đã căn vị trí và hướng.\nSẵn sàng chỉ đường tới B và C.", "Mở chỉ dẫn AR", () => { AwaitingAck = false; SelectedStop = OptimalStop(); Go("ar"); }); OverlayKind = "A-success"; return true;
            }
            if (ScreenId == "ar" && Proof.Active)
            {
                var outcome = Proof.Observe(Record, code, source, measuredTracking && TrackingTrusted, distance);
                if (outcome == DeliveryScanOutcome.DestinationVerified)
                {
                    AwaitingAck = true; OpenModal("Đã quét QR " + Proof.Stop, "Đúng điểm giao " + Proof.Stop + ".\nTiếp theo: barcode sách " + Proof.ProductName + ".", "OK · Quét barcode", () => { AwaitingAck = false; CloseModal(); ProofOverlay(); }); OverlayKind = "destination-success"; return true;
                }
                if (outcome == DeliveryScanOutcome.Completed)
                {
                    CloseModal(); SelectedStop = Record.deliveredB ? "C" : "B"; Go("ar");
                    if (Record.Complete) Completion(); else Toast("Đã giao thành công. Dẫn tới " + SelectedStop + "."); return true;
                }
                Toast(outcome == DeliveryScanOutcome.WrongProduct ? "Sai sách. Hãy quét đúng sản phẩm của điểm giao." : outcome == DeliveryScanOutcome.WrongDestination ? "Sai QR. Hãy quét đúng điểm giao đang chọn." : "Chưa đủ điều kiện tracking/khoảng cách. Hãy căn lại A nếu cần."); return false;
            }
            if (ScreenId == "scanner") { OpenModal("Đã đọc mã", code, "OK", CloseModal); return true; }
            return false;
        }
        public void AcknowledgeScan()
        {
            if (!AwaitingAck || ScreenId != "scan-products") return;
            AwaitingAck = false; Gate.Acknowledge(); CloseModal(); Go(Gate.Ready ? "scan-A" : "scan-products");
        }
        public void ApplyTracking(bool trusted)
        {
            TrackingTrusted = trusted;
            if (!trusted && Proof.Active) { Proof.Cancel(); AwaitingAck = false; CloseModal(); }
            if (ScreenId == "ar") Go("ar");
        }
        string OptimalStop() => Record.deliveredB ? "C" : Record.deliveredC ? "B" : "B"; // Existing route owner may override via SelectStop; fixture uses checked A/B/C.
        public void SelectStop(string stop)
        {
            if ((stop == "B" && Record.deliveredB) || (stop == "C" && Record.deliveredC)) return;
            SelectedStop = stop; Returning = false; Proof.Cancel(); RouteRequested?.Invoke(stop); Go("ar");
        }
        public void BeginDelivery(string stop)
        {
            if (!TrackingTrusted) { Toast("Cần căn vị trí trước khi xác nhận."); return; }
            if (!Proof.Begin(Record, stop)) return; SelectedStop = stop; Returning = false; ProofOverlay();
        }
        void Ar()
        {
            CameraBackdrop();
            Action(page, "Thoát", 16, 49, 72, 48, OpenExit); var exit = page.Find("Thoát"); if (exit != null) { UniformUiSurface.Use(exit.GetComponent<Image>(), 16, JarVisVisuals.Hex("d82c46"), Color.clear, 0, false, false); exit.GetComponentInChildren<TMP_Text>().color = Color.white; }
            IconButton(page, "refresh", 100, 49, 48, RescanA, false);
            Action(page, "Làm mới camera", 236, 49, 160, 48, RefreshCamera, false, "refresh").GetComponentInChildren<TMP_Text>().fontSize = 12;
            var assistant = Action(page, "JarVis", 16, 158, 135, 54, () => Assistant("voice")); assistant.GetComponentInChildren<TMP_Text>().rectTransform.anchoredPosition = new Vector2(48, 0); assistant.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta = new Vector2(75, 54); Face(assistant.transform, 5, 12, 39, 28);
            if (TrackingTrusted)
            {
                var route = JarVisVisuals.Rect(page, "Demo route projection - host supplies live world projection", 38, H * .33f, 336, H * .45f); var line = route.gameObject.AddComponent<JarVisRouteGraphic>(); line.Returning = Returning; line.ToC = SelectedStop == "C"; line.color = JarVisVisuals.Hex("4e80ff");
                var pin = JarVisVisuals.Rect(page, "Spatial pin " + (Returning ? "A" : SelectedStop), Returning ? 80 : SelectedStop == "B" ? 130 : 288, H * .33f - 12, 38, 52); var p = pin.gameObject.AddComponent<JarVisPinGraphic>(); p.color = JarVisVisuals.Hex("ff334e"); p.raycastTarget = false;
                Label(pin, Returning ? "A" : SelectedStop, 0, 6, 38, 24, 13, true, Color.white).alignment = TextAlignmentOptions.Center;
            }
            float dh = Record.Complete ? 116 : 248; var dock = Card("Guidance dock", 12, H - dh - 12, 388, dh, JarVisVisuals.Hex("13283f"));
            string heading = !TrackingTrusted ? "Đang khôi phục vị trí" : Returning ? "Về bàn A" : Record.Complete ? "Đã hoàn thành" : "Tới " + SelectedStop + " · " + (SelectedStop == "B" ? "A1" : "A2");
            Label(dock, heading, 18, 17, 264, 38, TrackingTrusted ? 26 : 20, true, Color.white);
            Action(dock, "Về A", 296, 14, 76, 48, GoA).GetComponentInChildren<TMP_Text>().fontSize = 13;
            Label(dock, !TrackingTrusted ? "Camera tiếp tục tìm mốc để kết nối lại." : "Đi theo mũi tên · " + Distance.ToString("0.0") + " m", 18, 64, 352, 32, 15, false, JarVisVisuals.Hex("c0d5ec"));
            if (!Record.Complete) { DeliveryRow(dock, "B", "A1", 108, Record.deliveredB); DeliveryRow(dock, "C", "A2", 176, Record.deliveredC); }
        }
        void DeliveryRow(Transform p, string stop, string book, float y, bool done)
        {
            var route = Action(p, "Tới " + stop, 14, y, 82, 56, () => SelectStop(stop), SelectedStop == stop && !Returning && !done);
            var confirm = Action(p, done ? "Đã giao " + book + " tại " + stop : "Xác nhận giao " + book + " tại " + stop, 106, y, 268, 56, () => BeginDelivery(stop), !done);
            route.interactable = confirm.interactable = !done;
            confirm.GetComponentInChildren<TMP_Text>().fontSize = 15;
            if (done) { var g = confirm.gameObject.AddComponent<CanvasGroup>(); g.alpha = .48f; var rg = route.gameObject.AddComponent<CanvasGroup>(); rg.alpha = .48f; }
        }
        public void GoA()
        {
            Proof.Cancel(); AwaitingAck = false; guidanceAcknowledged = Record.Complete; Returning = true; SelectedStop = "A"; ReturnARequested?.Invoke(); Go("ar");
        }
        void RescanA()
        {
            Proof.Cancel(); AwaitingAck = false;
            RescanARequested?.Invoke();
            if (Rehearsal) Go("scan-A");
        }
        void RefreshCamera()
        {
            CameraRefreshRequested?.Invoke(); Toast("Đang mở lại camera để lấy nét…"); // Existing owner cycles feed, never ARSession.
        }
        void ProofOverlay()
        {
            CloseModal(); OverlayKind = "proof";
            var shade = JarVisVisuals.Panel(overlayRoot, "Scan dimmer", 0, 0, 412, H, new Color(0.02f,.05f,.1f,.62f), 0); shade.GetComponent<Image>().raycastTarget = true;
            bool barcode = Proof.WaitingForProduct; float frameY = H * .28f;
            Label(overlayRoot, barcode ? "Quét barcode " + Proof.ProductName : "Quét QR điểm " + Proof.Stop, 20, 166, 372, 42, 27, true, Color.white).alignment = TextAlignmentOptions.Center;
            ScanWindow(overlayRoot, !barcode, frameY);
            var c = JarVisVisuals.Panel(overlayRoot, "Verification card", 20, H - 204, 372, 184, Color.white, 24);
            Label(c, "XÁC NHẬN GIAO TẠI " + Proof.Stop, 18, 18, 336, 25, 13, true, Blue);
            Label(c, barcode ? "2/2 · Barcode " + Proof.ProductName : "1/2 · QR vị trí " + Proof.Stop, 18, 58, 336, 34, 23, true, Ink);
            Label(c, barcode ? "Đối chiếu đúng sách trước khi hoàn thành." : "Quét QR điểm giao trước, rồi tới barcode sách.", 18, 101, 336, 32, 12, false, Muted);
            Action(c, "Hủy", 18, 134, 94, 40, () => { Proof.Cancel(); CloseModal(); }); Action(c, "Làm mới camera", 122, 134, 232, 40, RefreshCamera, false, "refresh");
        }
        public void Completion()
        {
            if (!Record.Complete) return;
            OpenModal("ĐÃ HOÀN THÀNH\nNHIỆM VỤ", "#00001 · CẤT HÀNG\nHai sản phẩm đã giao đúng vị trí.", "Về A", GoA); OverlayKind = "completion";
        }
        public void OpenModal(string title, string description, string primaryText, Action primary)
        {
            CloseModal(); OverlayKind = "message";
            var shade = JarVisVisuals.Panel(overlayRoot, "Modal backdrop", 0, 0, 412, H, new Color(.03f,.07f,.12f,.23f), 0); shade.GetComponent<Image>().raycastTarget = true;
            float h = 330; var c = JarVisVisuals.Panel(overlayRoot, "Modal " + title, 24, (H - h) / 2, 364, h, Color.white, 28);
            IconButton(c, "close", 308, 12, 44, () => { if (!AwaitingAck) CloseModal(); else Toast("Bấm OK để xác nhận và tiếp tục."); }, true);
            var badge = JarVisVisuals.Panel(c, "Success badge", 20, 22, 48, 48, JarVisVisuals.Hex("e3f6ed"), 24); Icon(badge, "check", 13, 13, 22, Green);
            Label(c, title, 20, 88, 324, 78, 24, true, Ink);
            Label(c, description, 20, 174, 324, 76, 14, false, Muted);
            Action(c, primaryText, 20, 260, 324, 52, primary, true);
            ModalGroup = c.gameObject.AddComponent<CanvasGroup>(); var motion = c.gameObject.AddComponent<JarVisMotion>(); motion.OverlayEnter();
        }
        public void CloseModal() { if (overlayRoot != null) Clear(overlayRoot); ModalGroup = null; OverlayKind = null; }
        void OpenExit() => OpenModal("Thoát chỉ dẫn?", "Tiến độ nhiệm vụ được giữ. Bạn có thể quay lại để tiếp tục.", "Thoát về nhiệm vụ", () => { ExitRequested?.Invoke(); Go("tasks"); });
        public void Toast(string message)
        {
            if (toastRoot == null) return; Clear(toastRoot);
            var c = JarVisVisuals.Panel(toastRoot, "Notice", 20, 214, 372, 82, JarVisVisuals.Hex("13283f"), 18);
            Label(c, message, 16, 12, 340, 58, 13, true, Color.white);
            if (Application.isPlaying) Destroy(c.gameObject, 3.5f);
        }
        void MapPage()
        {
            Header("Bản đồ Roompresent", "A: bàn xuất phát · B/C: hai xe sách");
            var card = Card("Room map", 20, 154, 372, Mathf.Min(440, H - 360)); BuildMap(card, 12, 12, 348, Mathf.Min(416, H - 384));
            Label(page, "Tuyến được chọn", 20, H - 190, 372, 26, 17, true);
            Action(page, "A → B → C", 20, H - 154, 180, 48, () => { SelectedStop = "B"; Toast("Đã chọn ưu tiên B."); }); Action(page, "A → C → B", 212, H - 154, 180, 48, () => { SelectedStop = "C"; Toast("Đã chọn ưu tiên C."); }); BottomNav("map");
        }
        void BuildMap(Transform p, float x, float y, float w, float h)
        {
            var rt = JarVisVisuals.Rect(p, "2D registered route schematic", x, y, w, h); var map = rt.gameObject.AddComponent<JarVisMapGraphic>(); map.color = Color.white;
            Label(rt, "ROOMPRESENT", 16, 12, w - 32, 24, 11, true, Muted);
            string[] ids = { "A", "B", "C" };
            for (int i = 0; i < 3; i++) { var point = JarVisMapGraphic.Point(i,w,h); var b = JarVisVisuals.Panel(rt, "Map marker " + ids[i], point.x - 18, point.y - 18, 36, 36, i == 0 ? JarVisVisuals.Hex("fff0c3") : i == 1 ? Blue : Green, 8); Label(b, ids[i], 0, 4, 36, 28, 18, true, i == 0 ? Ink : Color.white).alignment = TextAlignmentOptions.Center; }
            Label(rt, "Sơ đồ tuyến · Không phải tường đo đạc", 10, h - 24, w - 20, 20, 9, false, Muted).alignment = TextAlignmentOptions.Center;
        }
        void ChatPage()
        {
            Header("JarVis", "Trợ lý đang theo dõi nhiệm vụ #00001"); ChatMessages(page, 20, 154, 372, H - 360);
            Compose(page, 20, H - 180, 372); BottomNav("chat");
        }
        void ChatMessages(Transform p, float x, float y, float w, float h)
        {
            var viewport = JarVisVisuals.Rect(p, "Messages viewport", x, y, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var sr = viewport.gameObject.AddComponent<ScrollRect>(); sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 26;
            var content = JarVisVisuals.Rect(viewport, "Messages", 0, 0, w, 0); sr.viewport = viewport; sr.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var msg in messages)
            {
                bool user = msg.Key == "user", image = msg.Key == "map"; float height = image ? 224 : msg.Value.Length > 100 ? 118 : 90;
                var bubble = JarVisVisuals.Panel(content, "Message " + msg.Key, 0, 0, w, height, user ? Blue : Dark ? JarVisVisuals.Hex("1b2c43") : Color.white, 20); var le = bubble.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = height; le.minHeight = height;
                if (image) { BuildMap(bubble, 8, 8, w - 16, height - 16); var b = bubble.gameObject.AddComponent<Button>(); b.targetGraphic = bubble.GetComponent<Image>(); b.onClick.AddListener(EnlargeMap); }
                else Label(bubble, msg.Value, 16, 13, w - 32, height - 26, 14, false, user ? Color.white : Dark ? Color.white : Ink);
            }
            Canvas.ForceUpdateCanvases(); sr.verticalNormalizedPosition = 0;
        }
        void Compose(Transform p, float x, float y, float w)
        {
            messageInput = Input(p, "Hỏi JarVis về nhiệm vụ…", x, y, w - 58, 56);
            Action(p, "", x + w - 50, y, 50, 56, SendDraft, true, "send");
            Action(p, "Nói với JarVis", x, y + 64, w, 48, () => Assistant("voice"), false, "chat");
        }
        void SendDraft()
        {
            string text = messageInput == null ? "" : messageInput.text.Trim(); if (text.Length == 0) return; RequestMessage(text);
        }
        public void RequestMessage(string text)
        {
            messages.Add(new KeyValuePair<string,string>("user", text));
            if (MessageRequested != null) MessageRequested(text);
            else if (Rehearsal)
            {
                if (text.IndexOf("map", StringComparison.OrdinalIgnoreCase) >= 0 || text.Contains("bản đồ") || text.Contains("2d") || text.Contains("tracking")) { messages.Add(new KeyValuePair<string,string>("ai", "Mình gửi lại sơ đồ A/B/C bên dưới. Nếu tracking bị mất, giữ camera ở vùng nhiều chi tiết hoặc quét lại A để căn chính xác.")); messages.Add(new KeyValuePair<string,string>("map", "Roompresent")); }
                else messages.Add(new KeyValuePair<string,string>("ai", "Nhiệm vụ #00001: A1 tới B, A2 tới C. Tại điểm giao, hãy quét QR vị trí rồi barcode đúng quyển để xác nhận."));
            }
            if (ScreenId == "ar") Assistant("chat"); else Go("chat");
        }
        public void ReceiveMessage(string text) { messages.Add(new KeyValuePair<string,string>("ai", text)); if (ScreenId == "ar") Assistant("chat"); else Go("chat"); }
        public void ReceiveMap() { messages.Add(new KeyValuePair<string,string>("map", "Roompresent")); if (ScreenId == "ar") Assistant("chat"); else Go("chat"); }
        public void Assistant(string mode)
        {
            CloseModal(); OverlayKind = "assistant-" + mode;
            float height = mode == "voice" ? 256 : Mathf.Min(H - 245, 530); float top = mode == "voice" ? 232 : 178;
            var c = JarVisVisuals.Panel(overlayRoot, "JarVis floating " + mode, 16, top, 380, height, Color.white, 28);
            Face(c, 18, 17, 114, 50, mode == "voice");
            IconButton(c, "alert", 144, 18, 48, IncidentPopup, false); IconButton(c, "chat", 204, 18, 48, () => Assistant("chat"), false); var mic = IconButton(c, "", 264, 18, 48, () => { Emit("jarvis.voice.toggle"); if (Rehearsal) VoiceState("listening"); }, false); var micon = JarVisVisuals.Rect(mic.transform, "Microphone", 13, 13, 22, 22).gameObject.AddComponent<ChatActionIcon>(); micon.symbol = ChatActionIcon.Symbol.Microphone; micon.color = Ink; micon.raycastTarget = false;
            IconButton(c, "close", 324, 18, 48, CloseModal, true);
            if (mode == "voice") { Label(c, "Mình đang nghe bạn", 22, 90, 336, 36, 23, true, Ink); Label(c, "Hỏi về A1/A2, đường đi hoặc sự cố tracking.", 22, 137, 336, 48, 14, false, Muted); Action(c, "Mở hội thoại", 22, 196, 336, 44, () => Assistant("chat")); }
            else { ChatMessages(c, 14, 86, 352, height - 226); Compose(c, 14, height - 130, 352); }
            ModalGroup = c.gameObject.AddComponent<CanvasGroup>(); c.gameObject.AddComponent<JarVisMotion>().OverlayEnter();
        }
        public void VoiceState(string state)
        {
            Assistant("voice"); var texts = overlayRoot.GetComponentsInChildren<TMP_Text>(); foreach (var t in texts) if (t.text == "Mình đang nghe bạn") t.text = state == "thinking" ? "Đang suy nghĩ…" : state == "speaking" ? "JarVis đang trả lời" : "Mình đang nghe bạn";
        }
        public void IncidentPopup()
        {
            CloseModal(); OverlayKind = "incident"; var c = JarVisVisuals.Panel(overlayRoot, "Report incident popup", 20, 204, 372, 336, Color.white, 28);
            Label(c, "Báo cáo sự cố", 20, 20, 280, 36, 24, true, Ink); IconButton(c, "close", 308, 14, 48, CloseModal, true);
            Label(c, "Mô tả điều đang cản trở nhiệm vụ.", 20, 69, 332, 36, 14, false, Muted); var input = Input(c, "Ví dụ: mã C bị mờ…", 20, 119, 332, 118, true);
            Action(c, "Ghi nhận sự cố", 20, 266, 332, 52, () => { if (input.text.Trim().Length == 0) { Toast("Nhập mô tả sự cố trước."); return; } IncidentRequested?.Invoke(input.text); Emit("incident.draft"); CloseModal(); Toast("Chờ xử lý · Chưa xác nhận gửi tới quản trị."); }, true); ModalGroup = c.gameObject.AddComponent<CanvasGroup>(); c.gameObject.AddComponent<JarVisMotion>().OverlayEnter();
        }
        public void EnlargeMap()
        {
            CloseModal(); OverlayKind = "map-image"; var c = JarVisVisuals.Panel(overlayRoot, "Map attachment enlarged", 16, 144, 380, Mathf.Min(540, H - 184), Color.white, 26); Label(c, "Bản đồ Roompresent", 18, 18, 276, 32, 21, true, Ink); IconButton(c, "close", 314, 10, 48, CloseModal, true); BuildMap(c, 12, 72, 356, Mathf.Min(448, H - 276));
        }
    }
}
