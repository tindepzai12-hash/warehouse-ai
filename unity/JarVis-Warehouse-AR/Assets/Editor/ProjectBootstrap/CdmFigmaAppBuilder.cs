using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Cdm.Figma.UI;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class CdmFigmaAppBuilder
    {
        [Serializable] sealed class Geometry { public string id; public float x,y,w,h,fontSize; public bool clip; }
        [Serializable] sealed class GeometryFile { public Geometry[] nodes; }
        const string Source = "Assets/JarVis/DesignImports/JarVis-App-Imported.figma";
        const string ScenePath = "Assets/Scenes/JarVisFigmaApp.unity";
        [MenuItem("Tools/JarVis/Build Imported Figma App")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (asset == null) throw new InvalidOperationException("cdmvision import is missing: " + Source);
            var design = asset.GetComponent<FigmaDesign>();
            if (design == null || design.document == null) throw new InvalidOperationException("Imported Figma document is missing.");
            var candidates = design.document.pages.SelectMany(p => p.GetComponentsInChildren<FigmaNode>(true)).ToArray();
            var geometry = JsonUtility.FromJson<GeometryFile>(File.ReadAllText("Assets/JarVis/DesignImports/JarVis-App-Geometry.json")).nodes.ToDictionary(n => n.id);
            string[] ids = { "5:11","5:36","85:14","126:8","48:244","83:89","14:130","14:215","14:300","32:149","5:331","5:375","5:450","5:780","182:2","5:935","14:387","5:1029","215:2","5:1216","182:151","5:1308" };
            foreach (string id in ids) if (!candidates.Any(n => n.nodeId == id)) throw new InvalidOperationException("Missing imported frame " + id);
            var camera = new GameObject("Figma App Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera"; camera.orthographic = true; camera.backgroundColor = Color.white; camera.clearFlags = CameraClearFlags.SolidColor;
            var canvasObject = new GameObject("Figma App Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(412,915); scaler.matchWidthOrHeight = .5f;
            var navigation = canvasObject.AddComponent<ImportedFigmaNavigation>();
            var viewport = new GameObject("Figma 412x915 Viewport", typeof(RectTransform), typeof(ImportedFigmaViewport));
            viewport.transform.SetParent(canvasObject.transform, false);
            var screens = new List<ImportedFigmaNavigation.Screen>();
            foreach (string id in ids)
            {
                var original = candidates.First(n => n.nodeId == id);
                var imported = UnityEngine.Object.Instantiate(original.gameObject, viewport.transform, false);
                imported.name = original.nodeName + " [" + id + "]";
                var rect = imported.GetComponent<RectTransform>(); rect.localScale = Vector3.one; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                RestoreGeometry(imported, geometry);
                ConfigureScrolling(imported);
                foreach (FigmaNode node in imported.GetComponentsInChildren<FigmaNode>(true))
                {
                    if (node.nodeName == "status-bar") node.gameObject.SetActive(false);
                }
                foreach (TMP_Text text in imported.GetComponentsInChildren<TMP_Text>(true))
                { text.raycastTarget = false; if (id != "5:36" && id != "5:11") ResetValue(text); }
                if (id != "5:36" && id != "5:11") ResetLists(imported);
                if (id == "48:244") foreach (var node in imported.GetComponentsInChildren<FigmaNode>(true)) if (node.nodeName == "greeting-bar") node.gameObject.SetActive(false);
                imported.SetActive(id == "5:36");
                screens.Add(new ImportedFigmaNavigation.Screen { id = id, root = imported });
            }
            navigation.screens = screens.ToArray(); navigation.initialScreen = "5:36";
            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            eventObject.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>().AssignDefaultActions();
            canvasObject.transform.localScale = Vector3.one;
            navigation.Initialize();
            EditorSceneManager.SaveScene(scene, ScenePath);
            // Exercise the same Button callbacks that Play Mode will use, not only route strings.
            // Authentication must use the actual backend in Play Mode; edit-time layout checks do not log in.
            navigation.Show("85:14",false);
            Click(navigation.screens.First(s => s.id == "85:14").root, "BẬT TRỰC TUYẾN");
            Require(navigation.CurrentScreen == "126:8", "Offline -> online Home");
            foreach (var pair in new[] { new[] { "tab-Nhiệm vụ", "48:244" }, new[] { "tab-Bản đồ", "215:2" }, new[] { "tab-JarVis", "5:1216" }, new[] { "center-scan-btn", "5:331" } })
            {
                navigation.Home();
                var node = navigation.screens.First(s => s.id == "126:8").root.GetComponentsInChildren<FigmaNode>(true).First(n => n.nodeName == pair[0] && n.gameObject.activeInHierarchy);
                node.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Require(navigation.CurrentScreen == pair[1], pair[0]);
                navigation.Back(); Require(navigation.CurrentScreen == "126:8", "Back from " + pair[1]);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/FigmaAppNavigation.txt", "PASS: 22 native Figma frames; " + navigation.BoundButtonCount + " button listeners; Offline -> Online; Tasks/Map/AI/Scanner; Back.\nSource: " + Source + "\nLayout/navigation checks only. Camera decode verified separately. Authentication and stock lookup require configured backend and actual credentials; AI and stock mutations remain pending.\n");
            Debug.Log("[JarVis Figma] App import + navigation PASS. Frames=" + screens.Count + " buttons=" + navigation.BoundButtonCount);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ConfigureEntry();
        }
        static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException("Navigation check failed: " + message); }
        static void Click(GameObject root, string label)
        {
            var text = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.text.Trim() == label && t.gameObject.activeInHierarchy);
            var button = text.GetComponentInParent<UnityEngine.UI.Button>();
            if (button == null) throw new InvalidOperationException("Button absent: " + label);
            button.onClick.Invoke();
        }
        static void ResetValue(TMP_Text text)
        {
            string value = text.text.Trim();
            bool step = text.GetComponentsInParent<FigmaNode>(true).Any(n => n.nodeName != null && n.nodeName.StartsWith("step-"));
            if (step) return;
            if (Regex.IsMatch(value, @"^(Tất cả|Đang làm|Đã nhận|Đã hoàn thành|Sắp hết|Quá HSD|Sản phẩm.*) \(\d+\)$")) value = Regex.Replace(value, @"\(\d+\)", "(0)");
            else if (Regex.IsMatch(value, @"^[+−-]?[\d,.]+(\s*(Hộp|hộp|Thùng|thùng|lô|gói|₫|%))?$")) value = Regex.Replace(value, @"^[+−-]?[\d,.]+", "0");
            else if (Regex.IsMatch(value, @"^\d+/\d+")) value = Regex.Replace(value, @"\d+/\d+", "0/0");
            else if (value.StartsWith("Đã trực tuyến")) value = "Đã trực tuyến 0:00:00";
            else if (value.StartsWith("Xin chào,")) value = "Xin chào 👋";
            else if (value.StartsWith("SKU:") || value.StartsWith("SKU-") || value.StartsWith("VNM-") || value.StartsWith("LOT-") || value.StartsWith("Mã NV:")) value = "—";
            else if (value.StartsWith("Mã nhiệm vụ:")) value = "Mã nhiệm vụ: —";
            else if (value.StartsWith("Thời gian làm nhiệm vụ:")) value = "Thời gian làm nhiệm vụ: —\nNội dung nhiệm vụ: Chưa có nhiệm vụ.";
            else if (value.StartsWith("Nhập hàng - Lô")) value = "Chi tiết nhiệm vụ";
            else if (value.StartsWith("Xem lại kiểm kê")) value = "Xem lại nhiệm vụ";
            else if (value.StartsWith("Kiểm kê khu vực") || value.StartsWith("Picking đơn hàng")) value = "Chưa có nhiệm vụ";
            else if (value.StartsWith("Khu D") || value.StartsWith("Khu C")) value = "—";
            else if (value.Contains("Vinamilk") || value.Contains("VINAMILK") || value.Contains("Hảo Hảo") || value.Contains("Coca-Cola") || value.Contains("Omachi")) value = "Chưa có hàng hóa";
            else if (value.StartsWith("Kệ ") || value.StartsWith("Khu vực A (") || value.StartsWith("Còn 45") || value.StartsWith("HSD:") || value.StartsWith("HSD ") || value.StartsWith("NSX:")) value = "—";
            else if (Regex.IsMatch(value, @"^\d{2}/\d{2}/\d{4}$") || value.EndsWith("phút nữa")) value = "—";
            else if (value.StartsWith("Đã nhập đủ") || value.StartsWith("Đã xác nhận đủ")) value = "Chưa có kết quả thực hiện";
            else if (value == "NHIỆM VỤ HOÀN TẤT") value = "KẾT QUẢ NHIỆM VỤ";
            else if (value.StartsWith("Nguyễn Justin")) value = "Thông tin từ tài khoản";
            text.text = value;
        }
        static void ResetLists(GameObject root)
        {
            string[] lists = { "item-list", "chat-area", "scan-results", "history-list", "recent-transactions", "map-canvas", "tasks-container" };
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                if (!lists.Contains(node.nodeName)) continue;
                Empty(node.gameObject, node.nodeName == "chat-area" ? "Chưa có cuộc trò chuyện" : node.nodeName == "map-canvas" ? "Chưa có sơ đồ kho" : node.nodeName == "scan-results" ? "Chưa có mã đã quét" : node.nodeName == "item-list" ? "Chưa có hàng hóa" : node.nodeName == "tasks-container" ? "Chưa có nhiệm vụ" : "Chưa có dữ liệu");
            }
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
                if (node.nodeName == "task-card" || node.nodeName == "activity-item" || node.nodeName == "product-item-card" || node.nodeName == "transaction-item" || node.nodeName == "prod-img" || node.nodeName == "label-preview-section") node.gameObject.SetActive(false);
            var taskList = root.GetComponentsInChildren<FigmaNode>(true).FirstOrDefault(n => n.nodeName == "task-list-main");
            if (taskList != null)
            {
                foreach (var node in taskList.GetComponentsInChildren<FigmaNode>(true)) if (node.nodeName.Contains("card") && node.nodeName != "filter-card") node.gameObject.SetActive(false);
            }
        }
        static void Empty(GameObject container, string message)
        {
            var sample = container.GetComponentInChildren<TMP_Text>(true);
            if (sample == null) sample = container.transform.root.GetComponentInChildren<TMP_Text>(true);
            if (sample == null) return;
            var clone = UnityEngine.Object.Instantiate(sample.gameObject, container.transform, false);
            foreach (Transform child in container.transform) if (child.gameObject != clone) child.gameObject.SetActive(false);
            clone.name = "Empty State"; clone.SetActive(true);
            var rect = clone.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(16,16); rect.offsetMax = new Vector2(-16,-16); rect.localScale = Vector3.one;
            var label = clone.GetComponent<TMP_Text>(); label.text = message; label.fontSize = 16; label.alignment = TextAlignmentOptions.Center; label.color = new Color(.35f,.42f,.52f); label.raycastTarget = false;
        }
        static void RestoreGeometry(GameObject root, Dictionary<string,Geometry> geometry)
        {
            // Use source Figma bounds; the importer's auto-layout conversion collapses some TMP widths.
            foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true)) layout.enabled = false;
            foreach (var fitter in root.GetComponentsInChildren<ContentSizeFitter>(true)) fitter.enabled = false;
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                if (!geometry.TryGetValue(node.nodeId, out var g)) continue;
                var mask = node.GetComponent<RectMask2D>(); if (mask != null) mask.enabled = g.clip;
                if (node.gameObject == root) continue;
                var parent = node.transform.parent.GetComponentInParent<FigmaNode>();
                if (parent == null || !geometry.TryGetValue(parent.nodeId, out var p)) continue;
                var rect = node.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0,1); rect.pivot = new Vector2(0,1);
                rect.anchoredPosition = new Vector2(g.x-p.x,-(g.y-p.y)); rect.sizeDelta = new Vector2(g.w,g.h); rect.localScale = Vector3.one;
                var image = node.GetComponent<UnityEngine.UI.Image>(); if (image != null) image.useSpriteMesh = false;
                var text = node.GetComponent<TMP_Text>();
                if (text != null) { if (g.fontSize > 0) text.fontSize = g.fontSize; text.enableWordWrapping = text.text.Contains("\n") || g.h > 40; text.overflowMode = TextOverflowModes.Overflow; }
            }
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
                if (node.nodeName == "bottom-navigation")
                {
                    var rect = node.GetComponent<RectTransform>(); var height = rect.sizeDelta.y;
                    rect.anchorMin = new Vector2(0,0); rect.anchorMax = new Vector2(1,0); rect.pivot = new Vector2(.5f,0);
                    rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(0,height);
                }
        }
        static void ConfigureScrolling(GameObject root)
        {
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                bool filters = node.nodeName == "filter-chips-row";
                bool tall = node.nodeName == "body-scroll" && node.rectTransform.rect.height > 815;
                if (!filters && !tall) continue;
                var content = node.rectTransform;
                // Reserve the raised scan action, not just the navigation bar itself.
                var size = filters ? new Vector2(Mathf.Min(380,content.rect.width),content.rect.height) : new Vector2(412,782);
                var wrapper = new GameObject(filters ? "Horizontal filter viewport" : "Scrollable content viewport",typeof(RectTransform),typeof(RectMask2D),typeof(UnityEngine.UI.Image),typeof(ScrollRect));
                wrapper.transform.SetParent(content.parent,false);
                var viewport = wrapper.GetComponent<RectTransform>(); viewport.anchorMin = viewport.anchorMax = new Vector2(0,1); viewport.pivot = new Vector2(0,1); viewport.anchoredPosition = content.anchoredPosition; viewport.sizeDelta = size;
                wrapper.GetComponent<UnityEngine.UI.Image>().color = new Color(1,1,1,.001f);
                content.SetParent(viewport,false); content.anchorMin = content.anchorMax = new Vector2(0,1); content.pivot = new Vector2(0,1); content.anchoredPosition = Vector2.zero;
                if (filters)
                {
                    float width = content.rect.width;
                    foreach (RectTransform child in content) width = Mathf.Max(width,child.anchoredPosition.x+child.rect.width);
                    content.sizeDelta = new Vector2(width,content.sizeDelta.y);
                }
                var scroll = wrapper.GetComponent<ScrollRect>(); scroll.content = content; scroll.viewport = viewport; scroll.horizontal = filters; scroll.vertical = !filters; scroll.movementType = ScrollRect.MovementType.Clamped;
            }
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
                if (node.nodeName == "bottom-navigation") node.transform.SetAsLastSibling();
        }
        public static void BuildAndCapture() { Build(); Capture(); }
        [MenuItem("Tools/JarVis/Open Imported Figma App")]
        public static void Open() { ConfigureEntry(); EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); EditorApplication.ExecuteMenuItem("Window/General/Game"); Debug.Log("[JarVis Figma] Opened functional Figma app: " + ScenePath); }
        public static void OpenAndPlay()
        {
            Open();
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var assembly = typeof(Editor).Assembly;
                    var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                    var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    var instance = sizesType.GetProperty("instance",flags | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
                    var groupType = sizesType.GetProperty("currentGroupType",flags).GetValue(instance);
                    var group = sizesType.GetMethod("GetGroup",flags).Invoke(instance,new[] { groupType });
                    var type = group.GetType(); int total = (int)type.GetMethod("GetTotalCount",flags).Invoke(group,null); int selected = -1;
                    for (int i=0;i<total;i++)
                    {
                        var size = type.GetMethod("GetGameViewSize",flags).Invoke(group,new object[] { i });
                        if ((int)size.GetType().GetProperty("width",flags).GetValue(size)==412 && (int)size.GetType().GetProperty("height",flags).GetValue(size)==915) { selected=i; break; }
                    }
                    if (selected<0)
                    {
                        var sizeType = assembly.GetType("UnityEditor.GameViewSize"); var modeType = assembly.GetType("UnityEditor.GameViewSizeType");
                        var size = Activator.CreateInstance(sizeType,flags,null,new object[] { Enum.Parse(modeType,"FixedResolution"),412,915,"JarVis 412x915" },null);
                        type.GetMethod("AddCustomSize",flags).Invoke(group,new[] { size }); selected=total;
                    }
                    var gameType = assembly.GetType("UnityEditor.GameView"); var window = EditorWindow.GetWindow(gameType);
                    gameType.GetProperty("selectedSizeIndex",flags).SetValue(window,selected); window.Focus();
                }
                catch (Exception error) { Debug.LogWarning("[JarVis Figma] Game View preset unavailable; viewport still fits 412x915: " + error.Message); }
                EditorApplication.isPlaying = true;
                Debug.Log("[JarVis Figma] Starting imported app in Play Mode.");
            };
        }
        public static void ConfigureEntry()
        {
            var entries = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            entries.Insert(0, new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.scenes = entries.ToArray();
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[JarVis Figma] Entry scenes: " + string.Join(",",EditorBuildSettings.scenes.Select(s=>s.path)) + " cwd=" + Directory.GetCurrentDirectory());
        }
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var navigation = UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>();
            var canvas = navigation.GetComponent<Canvas>();
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            navigation.Initialize(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(412,915,24);
            var texture = new Texture2D(412,915,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture = target;
                foreach (var pair in new[] { new[] { "5:36", "Login" }, new[] { "126:8", "Home" }, new[] { "48:244", "Tasks" } })
                {
                    navigation.Show(pair[0], false); Canvas.ForceUpdateCanvases(); navigation.GetComponentInChildren<ImportedFigmaViewport>(true).Fit(); Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0,0,412,915),0,0); texture.Apply();
                    File.WriteAllBytes("Logs/FigmaApp-" + pair[1] + ".png",texture.EncodeToPNG());
                }
            }
            finally { camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(target); }
        }
    }
}
