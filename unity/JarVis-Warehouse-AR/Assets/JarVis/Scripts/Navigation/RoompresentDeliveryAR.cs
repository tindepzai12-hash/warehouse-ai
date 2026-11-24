using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Immersal;
using Immersal.XR;
using Immersal.Samples.Navigation;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;

namespace JarVis.Warehouse.Navigation
{
    // The camera remains owned by AR Foundation, including destination QR reading.
    public sealed class RoompresentDeliveryAR : MonoBehaviour
    {
        RoompresentVenue venue;
        ARCameraManager cameraManager;
        ARRaycastManager raycasts;
        Transform space;
        NavMeshSurface surface;
        NavigationManager navigation;
        Dictionary<string, IsNavigationTarget> targets;
        readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        readonly List<Vector3> measured = new List<Vector3>();
        readonly Queue<Vector3> stable = new Queue<Vector3>();
        Vector3[] reference;
        Vector3 aim;
        bool aimValid, reviewing, paused, scanning, savedStartGate;
        float nextSample, nextRoute, nextScan, candidateTime;
        int generation, decodeGeneration, candidateHits;
        string activeStop, candidate, notice;
        Task<Result> decode;
        RectTransform panel;
        TMP_Text title, body, tracking;
        TMP_FontAsset font;
        Button capture;
        int uiStage = -99;
        bool English => ImportedAppLocalization.IsEnglish;
        string T(string vi, string en) => English ? en : vi;
        bool Localized => !paused && venue != null && venue.IsLocalized;
        int Stage => !venue.alignmentVerified ? (savedStartGate ? 5 : reviewing ? 3 : measured.Count) : 4;
        public static bool IsStartQr(string value) => value == "JWM:ROOMPRESENT:153613:A" || value == "JWM:MAIN:E";
        [Serializable] sealed class Alignment { public int mapId = 153613, schema = 1; public Vector3 translation; public float yaw; }
        string AlignmentPath => System.IO.Path.Combine(Application.persistentDataPath, "roompresent-registration-v1.json");

        public void Configure(TMP_FontAsset appFont)
        {
            if (panel != null) return;
            font = appFont;
            venue = GetComponent<RoompresentVenue>();
            space = GetComponentInParent<XRSpace>().transform;
            surface = GetComponentInChildren<NavMeshSurface>(true);
            // The venue keeps navigation inactive until alignment is verified.
            // Resolve the scene component even before its Awake registers the singleton.
            navigation = FindObjectsByType<NavigationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(n => n.gameObject.scene == gameObject.scene);
            cameraManager = FindFirstObjectByType<ARCameraManager>();
            var origin = cameraManager.GetComponentInParent<XROrigin>();
            var planes = origin.GetComponent<ARPlaneManager>();
            if (planes == null) planes = origin.gameObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
            raycasts = origin.GetComponent<ARRaycastManager>();
            if (raycasts == null) raycasts = origin.gameObject.AddComponent<ARRaycastManager>();
            targets = venue.targets.GetComponentsInChildren<IsNavigationTarget>(true).ToDictionary(t => t.targetName, t => t);
            reference = new[] { "A", "B", "C" }.Select(id => transform.InverseTransformPoint(targets[id].transform.position)).ToArray();
            MakeCanvas();
            // Restore map registration, never a previous session's AR camera pose.
            try
            {
                if (System.IO.File.Exists(AlignmentPath))
                {
                    var a = JsonUtility.FromJson<Alignment>(System.IO.File.ReadAllText(AlignmentPath));
                    if (a != null && a.mapId == 153613 && a.schema == 1 && float.IsFinite(a.yaw) &&
                        float.IsFinite(a.translation.x) && float.IsFinite(a.translation.y) && float.IsFinite(a.translation.z) && a.translation.magnitude < 100)
                    { Apply(a.translation, a.yaw); savedStartGate = true; scanning = true; }
                }
            }
            catch (Exception) { notice = T("Chưa đọc được căn map đã lưu. Cần căn lại.", "Saved registration unavailable. Calibrate again."); }
            Render();
        }
        void MakeCanvas()
        {
            var go = new GameObject("JarVis Roompresent delivery", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 310;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(412, 915); scaler.matchWidthOrHeight = .5f;
            var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>(); safe.SetParent(go.transform, false);
            Rect area = Screen.safeArea; safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height); safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height); safe.offsetMin = safe.offsetMax = Vector2.zero;
            panel = new GameObject("Delivery controls", typeof(RectTransform)).GetComponent<RectTransform>(); panel.SetParent(safe, false); panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0); panel.pivot = new Vector2(.5f, 0); panel.anchoredPosition = new Vector2(0, 12); panel.sizeDelta = new Vector2(378, 310);
            var reticle = PrototypeUi.Text(safe, font, "+", 0, 0, 44, 44, 34, true, true); var r = reticle.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = Vector2.zero; reticle.alignment = TextAlignmentOptions.Center; reticle.color = new Color(.1f, 1f, .8f);
        }
        void Render()
        {
            uiStage = Stage;
            foreach (Transform child in panel) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var card = PrototypeUi.Panel(panel, "Delivery", 0, 0, 378, 310, true, 22);
            title = PrototypeUi.Text(card, font, "", 16, 12, 346, 28, 18, true, true);
            tracking = PrototypeUi.Text(card, font, "", 16, 44, 346, 22, 12, true);
            body = PrototypeUi.Text(card, font, "", 16, 71, 346, 92, 13, true); body.overflowMode = TextOverflowModes.Truncate;
            capture = null;
            if (Stage == 5)
            {
                PrototypeUi.Action(card, font, T("Căn lại sơ đồ phòng", "Set up the room again"), 16, 172, 346, 44, true, Restart);
            }
            else if (Stage < 3)
            {
                capture = PrototypeUi.Action(card, font, T("Ghi điểm ", "Capture ") + new[] { "A", "B", "C" }[measured.Count], 16, 172, 346, 44, true, Capture, true);
                PrototypeUi.Action(card, font, T("Làm lại ba điểm", "Restart calibration"), 16, 226, 168, 36, true, Restart);
            }
            else if (Stage == 3)
            {
                capture = PrototypeUi.Action(card, font, T("Đã đối chiếu ghim đúng vị trí", "I checked the pins in the room"), 16, 172, 346, 44, true, Verify, true);
                PrototypeUi.Action(card, font, T("Căn lại A/B/C", "Calibrate A/B/C again"), 16, 226, 168, 36, true, Restart);
            }
            else
            {
                foreach (var pair in new[] { ("AUTO", T("Tối ưu", "Optimal"), 16f), ("B", "B → C", 134f), ("C", "C → B", 252f) })
                {
                    string mode = pair.Item1;
                    PrototypeUi.Action(card, font, (RoompresentDeliveryMission.Current.route == mode ? "✓ " : "") + pair.Item2, pair.Item3, 170, 110, 36, true, () => { RoompresentDeliveryMission.Current.route = mode; activeStop = null; nextRoute = 0; RoompresentDeliveryMission.Save(); Render(); }, mode == RoompresentDeliveryMission.Current.route);
                }
                PrototypeUi.Action(card, font, scanning ? T("Dừng quét QR", "Stop QR scanning") : T("Quét QR điểm giao", "Scan destination QR"), 16, 216, 228, 42, true, () => { scanning = !scanning; generation++; candidate = null; candidateHits = 0; Render(); }, true);
                PrototypeUi.Action(card, font, T("Căn lại", "Calibrate"), 252, 216, 110, 42, true, Restart);
            }
            PrototypeUi.Action(card, font, T("Về JarVis", "Back to JarVis"), 192, 266, 170, 32, true, ImmersalMapLauncher.Close);
        }
        void Update()
        {
            if (panel == null || venue == null) return;
            if (uiStage != Stage) Render();
            tracking.text = Localized ? T("● Đã định vị trong Roompresent", "● Localized in Roompresent") : T("○ Đang tìm vị trí · từ từ nhìn quanh phòng", "○ Locating · slowly look around the room");
            tracking.color = Localized ? new Color(.2f, 1, .7f) : new Color(1, .8f, .4f);
            if (Stage == 5)
            {
                title.text = T("Quét mốc A để bắt đầu", "Scan A to start");
                body.text = T("Đã lưu phép căn phòng. Đưa QR cố định tại bàn A vào camera. Immersal tự tìm vị trí và hướng máy; không cần ghi lại B/C. Nếu kệ hoặc mốc đã đổi chỗ, hãy căn lại.", "Room registration saved. Show the fixed QR on table A. Immersal finds the camera pose; no B/C recapture is needed. Set up again if the carts or marker moved.") + "\n" + notice;
                ReadQr();
            }
            else if (Stage < 3)
            {
                SampleAim();
                string[] vi = { "Tâm QR trên bàn thuyết trình (A)", "Tâm mặt trên kệ sách bên trái (B)", "Tâm mặt trên kệ sách bên phải (C)" };
                string[] en = { "Centre of the QR on the presentation table (A)", "Centre of the top surface of the left book cart (B)", "Centre of the top surface of the right book cart (C)" };
                title.text = T("Thiết lập lần đầu · ", "First-time setup · ") + (measured.Count + 1) + "/3";
                body.text = (English ? en : vi)[measured.Count] + "\n" + T("Đưa dấu + vào đúng điểm trên vật thật. Giữ máy yên. Chỉ ghi khi đã định vị và nhận được mặt phẳng.", "Aim + at the exact point on the real object. Hold steady. Capture requires localization and a detected surface.") + (notice == null ? "" : "\n" + notice);
                capture.interactable = Localized && aimValid && stable.Count >= 6;
            }
            else if (Stage == 3)
            {
                title.text = T("Đối chiếu trước khi đi", "Check alignment before walking");
                body.text = T("Kiểm tra cả ba ghim A/B/C nằm đúng vật thật khi đi ngang và xoay máy. Nếu lệch, căn lại. Chưa xác nhận thì chưa dẫn đường.", "Check all three pins against the real objects while moving sideways and turning. Recalibrate if they drift. Guidance remains paused until you confirm.") + "\n" + notice;
                capture.interactable = Localized;
            }
            else
            {
                var mission = RoompresentDeliveryMission.Current;
                title.text = mission.Complete ? T("Đã giao cả hai quyển sách", "Both books delivered") : T("Giao sách · ", "Deliver books · ") + RoompresentDeliveryMission.Summary;
                if (!mission.accepted || !mission.Loaded) body.text = T("Về Nhiệm vụ, nhận bài thử và quét đủ hai quyển sách tại A trước.", "Return to Tasks, accept the test and scan both books at A first.");
                else if (!mission.Complete)
                {
                    if (Localized && Time.unscaledTime >= nextRoute) { nextRoute = Time.unscaledTime + .6f; Guide(); }
                    body.text = (activeStop == null ? T("Chưa tìm được đường đi đầy đủ.", "No complete route found yet.") : T("Đích ", "Destination ") + activeStop + (activeStop == "B" ? " · A1 / CPNB-1004" : " · A2 / CPNB-1003")) + "\n" + (scanning ? T("Đưa QR của điểm giao vào camera.", "Show the destination QR to the camera.") : T("Đến nơi, đặt sách xuống và quét QR của điểm đó.", "At the destination, place the book and scan that location's QR.")) + "\n" + notice;
                    if (scanning) ReadQr();
                }
                else { scanning = false; body.text = T("• A1 đã giao tại B\n• A2 đã giao tại C\nĐây là kết quả thử cục bộ, chưa ghi tồn kho.", "• A1 delivered at B\n• A2 delivered at C\nLocal test only; warehouse stock is unchanged."); }
            }
        }
        void SampleAim()
        {
            if (Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + .1f; aimValid = false;
            if (!Localized || !raycasts.Raycast(new Vector2(Screen.width * .5f, Screen.height * .5f), hits, TrackableType.PlaneWithinPolygon)) { stable.Clear(); return; }
            var hit = hits.FirstOrDefault(h => h.distance > .15f && h.distance < 3.5f);
            if (hit.distance <= .15f) { stable.Clear(); return; }
            var point = space.InverseTransformPoint(hit.pose.position);
            if (stable.Count > 0 && (point - stable.Peek()).magnitude > .035f) stable.Clear();
            stable.Enqueue(point); while (stable.Count > 10) stable.Dequeue();
            aim = stable.Aggregate(Vector3.zero, (sum, p) => sum + p) / stable.Count; aimValid = true;
        }
        void Capture()
        {
            if (!Localized || !aimValid || stable.Count < 6 || measured.Count >= 3) return;
            measured.Add(aim); stable.Clear();
            if (measured.Count == 3)
            {
                if (!TryFit(reference, measured.ToArray(), out var translation, out float yaw, out float error))
                { measured.Clear(); notice = T("Ba điểm chưa khớp hình học. Căn lại đúng mặt trên của từng vật.", "Landmark geometry does not match. Capture the exact top-surface points again."); }
                else { Apply(translation, yaw); reviewing = true; venue.previewAlignment = true; notice = T("Sai số khớp ba điểm: ", "Three-point fit residual: ") + error.ToString("F2") + " m"; Debug.Log("[Roompresent] Three-point registration awaiting physical review; RMS=" + error.ToString("F3")); }
            }
            Render();
        }
        public static bool TryFit(Vector3[] source, Vector3[] observed, out Vector3 translation, out float yaw, out float rms)
        {
            translation = Vector3.zero; yaw = 0; rms = float.PositiveInfinity;
            if (source == null || observed == null || source.Length != 3 || observed.Length != 3) return false;
            if (Vector3.Cross(source[1] - source[0], source[2] - source[0]).magnitude < .8f) return false;
            var s = (source[0] + source[1] + source[2]) / 3; var o = (observed[0] + observed[1] + observed[2]) / 3;
            float dot = 0, cross = 0;
            for (int i = 0; i < 3; i++) { var a = source[i] - s; var b = observed[i] - o; dot += a.x * b.x + a.z * b.z; cross += a.z * b.x - a.x * b.z; }
            yaw = Mathf.Atan2(cross, dot) * Mathf.Rad2Deg; var rotation = Quaternion.Euler(0, yaw, 0); translation = o - rotation * s;
            float sum = 0, maximum = 0;
            for (int i = 0; i < 3; i++) { float e = (rotation * source[i] + translation - observed[i]).magnitude; sum += e * e; maximum = Mathf.Max(maximum, e); }
            rms = Mathf.Sqrt(sum / 3);
            return float.IsFinite(rms) && rms <= .18f && maximum <= .25f && translation.magnitude < 100;
        }
        void Apply(Vector3 translation, float yaw)
        { transform.localPosition = translation; transform.localRotation = Quaternion.Euler(0, yaw, 0); transform.localScale = Vector3.one; surface.RemoveData(); surface.AddData(); }
        void Verify()
        {
            if (!Localized || !reviewing) return;
            venue.previewAlignment = false; venue.alignmentVerified = true;
            try { System.IO.File.WriteAllText(AlignmentPath, JsonUtility.ToJson(new Alignment { translation = transform.localPosition, yaw = transform.localEulerAngles.y })); }
            catch (Exception) { notice = T("Căn map chỉ giữ trong phiên này.", "Registration saved for this session only."); }
            Debug.Log("[Roompresent] User confirmed landmark alignment. Independent walking accuracy is not measured."); Render();
        }
        void Restart()
        { venue.alignmentVerified = false; venue.previewAlignment = false; reviewing = false; savedStartGate = false; measured.Clear(); stable.Clear(); scanning = false; generation++; activeStop = null; if (navigation != null && navigation.navigationActive) navigation.StopNavigation(); Apply(Vector3.zero, 0); notice = null; Render(); }
        float RouteLength(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out var a, 2.2f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var b, 1.5f, NavMesh.AllAreas)) return float.PositiveInfinity;
            var p = new NavMeshPath(); if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, p) || p.status != NavMeshPathStatus.PathComplete) return float.PositiveInfinity;
            float result = 0; for (int i = 1; i < p.corners.Length; i++) result += Vector3.Distance(p.corners[i - 1], p.corners[i]); return result;
        }
        void Guide()
        {
            var m = RoompresentDeliveryMission.Current;
            if (activeStop == null || activeStop == "B" && m.deliveredB || activeStop == "C" && m.deliveredC)
            {
                if (m.deliveredB) activeStop = "C";
                else if (m.deliveredC) activeStop = "B";
                else if (m.route != "AUTO") activeStop = m.route;
                else
                {
                    Vector3 b = targets["B"].transform.position, c = targets["C"].transform.position, start = cameraManager.transform.position;
                    float bc = RouteLength(start, b) + RouteLength(b, c), cb = RouteLength(start, c) + RouteLength(c, b);
                    if (float.IsInfinity(bc) && float.IsInfinity(cb)) { notice = T("Đang tìm đường trên sàn đã quét.", "Searching the scanned walkable floor."); return; }
                    activeStop = bc <= cb ? "B" : "C";
                    notice = T("Tuyến tối ưu theo NavMesh: ", "Shortest available NavMesh route: ") + (activeStop == "B" ? "B → C" : "C → B");
                }
                Debug.Log("[Roompresent] Test route selected " + activeStop + " mode=" + m.route);
            }
            navigation?.TryToFindPath(targets[activeStop]);
        }
        float DistanceTo(string id)
        { var delta = transform.InverseTransformPoint(cameraManager.transform.position) - reference[id == "A" ? 0 : id == "B" ? 1 : 2]; delta.y = 0; return delta.magnitude; }
        void ReadQr()
        {
            if (decode != null && decode.IsCompleted)
            {
                var pending = decode; decode = null;
                if (pending.IsFaulted) { _ = pending.Exception; return; }
                var result = pending.Result;
                if (decodeGeneration == generation && Localized && result != null && result.BarcodeFormat == BarcodeFormat.QR_CODE)
                {
                    if (candidate == result.Text && Time.unscaledTime - candidateTime < 4) candidateHits++; else { candidate = result.Text; candidateHits = 1; }
                    candidateTime = Time.unscaledTime;
                    if (candidateHits >= 2)
                    {
                        if (savedStartGate)
                        {
                            if (!IsStartQr(result.Text)) notice = T("Cần QR A tại bàn (thẻ E cũ), không phải barcode sách hay QR B/C.", "Use table A's QR (the old E card), not a book barcode or B/C QR.");
                            else if (DistanceTo("A") > 1.2f) notice = T("Đã đọc A nhưng vị trí định vị chưa khớp gần bàn A. Nhìn quanh phòng để tìm lại; nếu vẫn lệch, căn lại sơ đồ.", "A read, but localization is not near table A yet. Look around to relocalize; set up again if the mismatch persists.");
                            else
                            {
                                venue.alignmentVerified = true; venue.previewAlignment = false; savedStartGate = false;
                                scanning = false; generation++; activeStop = null;
                                notice = T("Đã xác nhận mốc A.", "Start A confirmed.");
                                Debug.Log("[Roompresent] Saved room registration confirmed at start A; no camera pose overwritten.");
                                Render();
                            }
                            candidateHits = 0; return;
                        }
                        string stop = RoompresentDeliveryRecord.Destination(result.Text);
                        if (stop == null) notice = T("QR này không phải B/C của nhiệm vụ.", "This QR is not a task destination B/C.");
                        else if (RoompresentDeliveryMission.Current.Confirm(result.Text, activeStop, venue.alignmentVerified && Localized, DistanceTo(stop)))
                        { RoompresentDeliveryMission.Save(); scanning = false; generation++; activeStop = null; notice = T("Đã xác nhận giao sách tại ", "Book delivery confirmed at ") + stop; navigation?.StopNavigation(); Debug.Log("[Roompresent] Local test delivery confirmed at " + stop); Render(); }
                        else notice = T("Chưa xác nhận: cần đúng đích đang dẫn, đã định vị và đứng gần điểm giao.", "Not confirmed: use the active destination, valid tracking, and stand near the delivery point.");
                        candidateHits = 0;
                    }
                }
            }
            if (!Localized || decode != null || Time.unscaledTime < nextScan || !cameraManager.TryAcquireLatestCpuImage(out var image)) return;
            nextScan = Time.unscaledTime + .5f;
            using (image)
            {
                var plane = image.GetPlane(0); int step = Math.Max(1, (image.width + 1279) / 1280); int w = image.width / step, h = image.height / step;
                var gray = new byte[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) gray[y * w + x] = plane.data[y * step * plane.rowStride + x * step * plane.pixelStride];
                decodeGeneration = generation;
                decode = Task.Run(() => { var reader = new BarcodeReaderGeneric { AutoRotate = true }; reader.Options.TryHarder = true; reader.Options.TryInverted = true; reader.Options.PossibleFormats = new[] { BarcodeFormat.QR_CODE }; return reader.Decode(new RGBLuminanceSource(gray, w, h, RGBLuminanceSource.BitmapFormat.Gray8)); });
            }
        }
        void OnApplicationPause(bool value) { paused = value; if (value) { generation++; stable.Clear(); candidate = null; candidateHits = 0; } }
        void OnDisable() { generation++; if (decode != null) { var pending = decode; _ = pending.ContinueWith(t => { if (t.IsFaulted) _ = t.Exception; }); decode = null; } }
    }
}
