using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Warehouse;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    public sealed class WarehouseMapScreen : MonoBehaviour
    {
        ImportedFigmaNavigation nav;
        TMP_FontAsset font;
        OmsMobileApi.WarehouseMapReply map;
        Texture2D floorTexture;
        string textureFloorId, floorId, destinationCode, destinationDraft, markerId, message;
        int markerMapVersion, generation;
        bool showLegacyTests;
        bool configured, loading;

        bool En => ImportedAppLocalization.IsEnglish;
        string T(string vi, string en) => En ? en : vi;

        public void Configure(ImportedFigmaNavigation owner)
        {
            nav = owner;
            font = GetComponentInChildren<TMP_Text>(true).font;
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            configured = true;
            if (isActiveAndEnabled && nav.CurrentScreen == "215:2") StartCoroutine(Load());
        }

        public void SetDestination(string code)
        {
            destinationCode = code?.Trim();
            destinationDraft = destinationCode;
            var target = map?.document?.locations?.FirstOrDefault(item =>
                string.Equals(item.code, destinationCode, StringComparison.OrdinalIgnoreCase));
            if (target != null) floorId = target.floorId;
            if (configured && isActiveAndEnabled) Render();
        }

        public bool AcceptMarkerCode(string code)
        {
            if (!WarehouseMapRoute.TryParseMarkerCode(code, out string scannedId)) return false;
            markerId = scannedId;
            markerMapVersion = map?.version ?? 0;
            message = null;
            return true;
        }

        void OnEnable()
        {
            if (configured && nav.CurrentScreen == "215:2") StartCoroutine(Load());
        }

        void OnDisable()
        {
            generation++;
            StopAllCoroutines();
            loading = false;
            ReleaseTexture();
        }

        void OnDestroy() { ReleaseTexture(); }

        void ReleaseTexture()
        {
            if (floorTexture != null) Destroy(floorTexture);
            floorTexture = null;
            textureFloorId = null;
        }

        IEnumerator Load() { Render(); yield break; }

        void Render()
        {
            if (!configured || nav?.Settings == null) return;
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Prototype ")) continue;
                child.gameObject.SetActive(false); Destroy(child.gameObject);
            }
            PrototypeUi.Shell(transform,font,T("Bản đồ kho","Warehouse map"),nav.Settings.Dark,nav.Back,
                ()=>nav.Show("prototype:tools"),out var content);
            RoompresentMapPanel.Create(content,font,nav.Settings.Dark,En);
            content.sizeDelta=new Vector2(366,500);
        }

        void RenderPublishedMapLegacy()
        {
            if (!configured || nav?.Settings == null) return;
            foreach (Transform child in transform)
            {
                if (!child.name.StartsWith("Prototype ")) continue;
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            bool dark = nav.Settings.Dark;
            PrototypeUi.Shell(transform, font, T("Bản đồ kho", "Warehouse map"), dark, nav.Back,
                () => nav.Show("prototype:tools"), out var content);
            RoompresentMapPanel.Create(content,font,dark,En);
            var roomControls=new GameObject("Roompresent map controls",typeof(RectTransform)).GetComponent<RectTransform>();roomControls.SetParent(content,false);
            roomControls.anchorMin=roomControls.anchorMax=roomControls.pivot=new Vector2(0,1);roomControls.anchoredPosition=new Vector2(0,-616);roomControls.sizeDelta=new Vector2(366,0);
            var pageContent=content;content=roomControls;
            var presentation = PrototypeUi.Panel(content, "Roompresent presentation", 0, 0, 366, 208, dark);
            PrototypeUi.Text(presentation, font, T("HƯỚNG DẪN AR","AR GUIDANCE"), 18, 12, 330, 28, 18, dark, true);
            PrototypeUi.Text(presentation, font, T("B: xe trái · C: xe phải.\nCăn B/C một lần; dùng QR A cố định để mở AR.", "B: left cart · C: right cart.\nRegister B/C once; show fixed QR A to start AR."), 18, 46, 330, 52, 13, dark, false, true);
            PrototypeUi.Action(presentation, font, T("Nhiệm vụ · A1 → B, A2 → C", "Mission · A1 → B, A2 → C"), 18, 108, 330, 38, dark, () => RoompresentDeliveryMission.Open(nav), true);
            PrototypeUi.Action(presentation,font,T("Kiểm tra định vị phòng","Check room localization"),18,154,190,36,dark,()=>RoompresentQuickMapLauncher.Open(nav));
            PrototypeUi.Action(presentation,font,T("Thiết lập map","Room setup"),218,154,130,36,dark,()=>RoompresentQuickMapLauncher.OpenSetup(nav));
            PrototypeUi.Action(content,font,T("Bài test cũ / nâng cao","Old tests / advanced"),0,224,366,38,dark,()=>{showLegacyTests=!showLegacyTests;Render();});
            if(showLegacyTests)
            {
            var small = PrototypeUi.Panel(content, "Small room test", 0, 278, 366, 142, dark);
            PrototypeUi.Text(small, font, T("Phòng nhỏ · bài test tại cửa", "Small room · door test"), 18, 12, 330, 28, 18, dark, true);
            PrototypeUi.Text(small, font, T("A: trước cửa · B: chậu · C: túi đen.\nQuét hai sách để mở AR của phòng nhỏ.", "A: door · B: basin · C: black bag.\nScan both books to open the small room AR."), 18, 44, 330, 43, 12, dark, false, true);
            PrototypeUi.Action(small, font, T("Mở bài test phòng nhỏ", "Open small room test"), 18, 94, 330, 36, dark, () => SmallRoomDeliveryMission.Open(nav));
            var immersal = PrototypeUi.Panel(content, "Immersal advanced", 0, 436, 366, 94, dark);
            PrototypeUi.Text(immersal, font, T("Immersal · cấu hình nâng cao", "Immersal · advanced setup"), 18, 8, 330, 24, 14, dark, true);
            PrototypeUi.Action(immersal, font, T("Mở scene Immersal cũ", "Open original Immersal scene"), 18, 43, 330, 36, dark, () => ImmersalMapLauncher.Open(nav));
            }
            var mapContent = new GameObject("Published map content", typeof(RectTransform));
            pageContent.sizeDelta = new Vector2(366,(showLegacyTests?1324:1056)+616);
            mapContent.transform.SetParent(content, false);
            var mapRect = mapContent.GetComponent<RectTransform>();
            mapRect.anchorMin = mapRect.anchorMax = new Vector2(0,1);
            mapRect.pivot = new Vector2(0,1);
            mapRect.anchoredPosition = new Vector2(0,showLegacyTests?-546:-278);
            mapRect.sizeDelta = new Vector2(366,0);
            content = mapRect;
            if (loading)
            {
                var waiting = PrototypeUi.Panel(content, "Loading map", 0, 0, 366, 200, dark);
                PrototypeUi.Text(waiting, font, T("Đang tải bản đồ đã công bố…", "Loading the published map…"),
                    24, 72, 318, 55, 15, dark, true).alignment = TextAlignmentOptions.Center;
                return;
            }
            if (map?.document == null || map.version < 1 || map.document.schemaVersion != 1)
            {
                var empty = PrototypeUi.Panel(content, "No published map", 0, 0, 366, 305, dark);
                PrototypeUi.Icon(empty, "map", 151, 42, 64, PrototypeUi.Muted(dark));
                PrototypeUi.Text(empty, font, message ?? T("Chưa có bản đồ kho được công bố.", "No warehouse map has been published."),
                    24, 128, 318, 66, 16, dark, true).alignment = TextAlignmentOptions.Center;
                PrototypeUi.Text(empty, font, T("Quản lý nhập sơ đồ và đo tọa độ trên web. Không cần tải app scan map.",
                    "A manager imports the floorplan and measured coordinates on the web. No map-scan app is required."),
                    24, 202, 318, 68, 12, dark, false, true).alignment = TextAlignmentOptions.Center;
                PrototypeUi.Action(content, font, T("Tải lại", "Retry"), 0, 322, 366, 48, dark,
                    () => StartCoroutine(Load()), true);
                return;
            }
            var document = map.document;
            var floors = document.floors ?? Array.Empty<OmsMobileApi.WarehouseMapFloor>();
            var destination = document.locations?.FirstOrDefault(item =>
                string.Equals(item.code, destinationCode, StringComparison.OrdinalIgnoreCase));
            if (!floors.Any(item => item.id == floorId)) floorId = destination?.floorId ?? floors.FirstOrDefault()?.id;
            var floor = floors.FirstOrDefault(item => item.id == floorId);
            if (floor == null || floor.width <= 0 || floor.depth <= 0)
            {
                PrototypeUi.Text(content, font, T("Bản đồ thiếu tầng hợp lệ.", "The map has no valid floor."),
                    0, 20, 366, 70, 15, dark, true);
                return;
            }
            PrototypeUi.Text(content, font, T("Phiên bản ", "Version ") + map.version + " · " + floor.name,
                0, 0, 366, 26, 12, dark, true);
            if (!document.surveyVerified)
                PrototypeUi.Text(content, font, T("CHƯA ĐO KIỂM · Chỉ xem trước, không dùng để di chuyển thực tế.",
                    "UNVERIFIED · Preview only; do not use for physical navigation."),
                    0, 28, 366, 42, 11, dark, true);
            float panelY = document.surveyVerified ? 34 : 72;
            var panel = PrototypeUi.Panel(content, "Measured floor plan", 0, panelY, 366, 376, dark);
            PrototypeUi.Text(panel, font, floor.name + " · " + floor.width.ToString("0.#") + " × " +
                floor.depth.ToString("0.#") + " m", 16, 12, 334, 27, 12, dark, true);
            float width = Mathf.Min(334, 276 * floor.width / floor.depth);
            float height = width * floor.depth / floor.width;
            float left = (366 - width) / 2;
            float top = 49 + (276 - height) / 2;
            var drawing = JarVisUi.Rect(panel, "Floor coordinates", left, top, width, height);
            var background = drawing.gameObject.AddComponent<Image>();
            background.color = dark ? PrototypeUi.Hex("182b40") : PrototypeUi.Hex("f1f6fc");
            background.raycastTarget = false;
            var texture = GetFloorTexture(floor);
            if (texture != null)
            {
                var image = JarVisUi.Rect(drawing, "Imported floorplan", 0, 0, width, height)
                    .gameObject.AddComponent<RawImage>();
                image.texture = texture;
                image.raycastTarget = false;
            }
            else PrototypeUi.Text(drawing, font, T("Chưa nhập ảnh sơ đồ", "No floorplan image"),
                8, height / 2 - 16, width - 16, 32, 11, dark, false, true).alignment = TextAlignmentOptions.Center;
            var nodes = (document.nodes ?? Array.Empty<OmsMobileApi.WarehouseMapNode>())
                .Where(item => item.floorId == floor.id).ToDictionary(item => item.id);
            var edges = document.edges ?? Array.Empty<OmsMobileApi.WarehouseMapEdge>();
            if (edges.Length <= 300)
                foreach (var edge in edges)
                    if (nodes.TryGetValue(edge.from, out var start) && nodes.TryGetValue(edge.to, out var end))
                        Line(drawing, Point(start.x, start.z, floor, width, height),
                            Point(end.x, end.z, floor, width, height), edge.blocked ?
                            PrototypeUi.Hex("dc5862") : PrototypeUi.Hex(dark ? "7086a5" : "a2b4c8"), edge.blocked ? 2 : 3);
            List<OmsMobileApi.WarehouseMapNode> route = null;
            WarehouseMapRoute.Failure failure = WarehouseMapRoute.Failure.None;
            if (!string.IsNullOrEmpty(markerId) && !string.IsNullOrEmpty(destinationCode))
                failure = WarehouseMapRoute.Find(map, markerMapVersion, markerId, destinationCode, out route);
            if (route != null && failure == WarehouseMapRoute.Failure.None)
                for (int index = 1; index < route.Count; index++)
                    Line(drawing, Point(route[index - 1].x, route[index - 1].z, floor, width, height),
                        Point(route[index].x, route[index].z, floor, width, height), PrototypeUi.Accent, 6);
            var marker = document.markers?.FirstOrDefault(item => item.id == markerId && item.floorId == floor.id);
            if (marker != null) Dot(drawing, Point(marker.x, marker.z, floor, width, height),
                PrototypeUi.Hex("0aa77a"), "START");
            if (destination != null && destination.floorId == floor.id)
                Dot(drawing, Point(destination.x, destination.z, floor, width, height),
                    PrototypeUi.Hex("ed8a2e"), destination.code);
            PrototypeUi.Text(panel, font, T("Tuyến kết thúc tại lối tiếp cận kệ, không đi xuyên qua kệ.",
                "Route ends at the aisle access node, not inside the rack."), 16, 337, 334, 31, 10, dark, false, true);
            float y = panelY + 392;
            if (floors.Length > 1)
            {
                float x = 0;
                foreach (var option in floors.Take(3))
                {
                    var choice = option;
                    PrototypeUi.Action(content, font, option.name, x, y, 116, 38, dark,
                        () => { floorId = choice.id; Render(); }, choice.id == floor.id);
                    x += 125;
                }
                y += 48;
            }
            string routeMessage = message ?? (destinationCode != null && destination == null
                ? T("Không tìm thấy ô kệ này trong bản đồ.", "This storage location is not on the map.")
                : string.IsNullOrEmpty(destinationCode)
                    ? T("Chọn đích từ Nhiệm vụ/Tồn kho hoặc nhập mã ô kệ.", "Choose a destination from Tasks/Inventory or enter a bin code.")
                    : string.IsNullOrEmpty(markerId)
                        ? T("Đích: ", "Destination: ") + destination.code + T(" · Quét mốc gần bạn để bắt đầu.", " · Scan a nearby checkpoint to start.")
                        : RouteMessage(failure, route));
            PrototypeUi.Text(content, font, routeMessage, 0, y, 366, 61, 13, dark, failure == WarehouseMapRoute.Failure.None && route != null);
            y += 68;
            PrototypeUi.Action(content, font, T("Quét QR điểm mốc", "Scan checkpoint QR"), 0, y, 366, 48,
                dark, () => nav.Show("5:331"), true, "scan");
            y += 64;
            var inputBox = PrototypeUi.Panel(content, "Destination code", 0, y, 248, 48, dark, 14);
            var area = JarVisUi.Rect(inputBox, "Text viewport", 12, 0, 224, 48);
            area.gameObject.AddComponent<RectMask2D>();
            var value = PrototypeUi.Text(area, font, "", 0, 0, 224, 48, 14, dark);
            value.alignment = TextAlignmentOptions.MidlineLeft;
            var hint = PrototypeUi.Text(area, font, T("Mã ô kệ, ví dụ A-12", "Bin code, e.g. A-12"),
                0, 0, 224, 48, 12, dark, false, true);
            hint.alignment = TextAlignmentOptions.MidlineLeft;
            var input = inputBox.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = value;
            input.placeholder = hint;
            input.targetGraphic = inputBox.GetComponent<Image>();
            input.characterLimit = 64;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = destinationDraft ?? destinationCode ?? "";
            input.onValueChanged.AddListener(copy => destinationDraft = copy);
            input.onSubmit.AddListener(copy => SetDestination(copy));
            inputBox.gameObject.AddComponent<ImportedInputCaretRepair>().Configure(input);
            PrototypeUi.Action(content, font, T("Tìm", "Find"), 258, y, 108, 48, dark,
                () => SetDestination(destinationDraft), true);
            y += 63;
            PrototypeUi.Text(content, font, T("Mốc QR xác nhận vị trí bắt đầu theo bản đồ; chưa đo hướng camera hoặc tạo dẫn đường AR.",
                "Checkpoint QR identifies the mapped start point; it does not measure camera heading or enable AR guidance."),
                0, y, 366, 62, 11, dark, false, true);
            content.sizeDelta = new Vector2(366, y + 80);
            ((RectTransform)content.parent).sizeDelta = new Vector2(366,y + 238);
            nav.Settings.ApplyTheme();
        }

        string RouteMessage(WarehouseMapRoute.Failure failure, List<OmsMobileApi.WarehouseMapNode> route)
        {
            if (failure == WarehouseMapRoute.Failure.InvalidMarker) return T("Mốc không có trong bản đồ hiện tại. Quét mốc khác.", "Checkpoint is not on this map. Scan another one.");
            if (failure == WarehouseMapRoute.Failure.VersionChanged) return T("Bản đồ đổi phiên bản. Quét lại mốc.", "Map version changed. Scan again.");
            if (failure == WarehouseMapRoute.Failure.DifferentFloor) return T("Đích ở tầng khác; chưa có tuyến liên tầng.", "Destination is on another floor; cross-floor routing is unavailable.");
            if (failure != WarehouseMapRoute.Failure.None) return T("Không có tuyến đi bộ hợp lệ đến ô kệ này.", "No valid walking route reaches this bin.");
            float metres = 0;
            for (int index = 1; index < route.Count; index++)
            {
                float dx = route[index].x - route[index - 1].x;
                float dz = route[index].z - route[index - 1].z;
                metres += Mathf.Sqrt(dx * dx + dz * dz);
            }
            return T("Đường đến ", "Route to ") + destinationCode + " · " + metres.ToString("0.0") + " m · " +
                route.Count + T(" mốc đường đi", " walkway nodes");
        }

        Texture2D GetFloorTexture(OmsMobileApi.WarehouseMapFloor floor)
        {
            if (textureFloorId == floor.id) return floorTexture;
            ReleaseTexture();
            textureFloorId = floor.id;
            if (string.IsNullOrEmpty(floor.image?.base64)) return null;
            try
            {
                byte[] data = Convert.FromBase64String(floor.image.base64);
                floorTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!floorTexture.LoadImage(data, true)) ReleaseTexture();
            }
            catch (Exception)
            {
                ReleaseTexture();
            }
            return floorTexture;
        }

        static Vector2 Point(float x, float z, OmsMobileApi.WarehouseMapFloor floor, float width, float height)
            => new Vector2(x / floor.width * width, z / floor.depth * height);

        static void Line(Transform parent, Vector2 start, Vector2 end, Color color, float thickness)
        {
            var delta = end - start;
            if (delta.sqrMagnitude < .01f) return;
            var line = JarVisUi.Rect(parent, "Walkway segment", 0, 0, delta.magnitude, thickness);
            line.pivot = new Vector2(.5f, .5f);
            line.anchoredPosition = new Vector2((start.x + end.x) / 2, -(start.y + end.y) / 2);
            line.localEulerAngles = new Vector3(0, 0, -Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var image = line.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        void Dot(Transform parent, Vector2 point, Color color, string label)
        {
            var dot = PrototypeUi.Panel(parent, "Map point " + label, point.x - 6, point.y - 6, 12, 12,
                nav.Settings.Dark, 6);
            UniformUiSurface.Use(dot.GetComponent<Image>(), 6, color, Color.white, 1, false, false);
            if (point.y > 22) PrototypeUi.Text(parent, font, label, Mathf.Clamp(point.x - 35, 0, 270), point.y - 26,
                100, 22, 10, nav.Settings.Dark, true);
        }
    }
}
