using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class ProductWorkspaceReview
    {
        static double buildAfter=-1;
        static ProductWorkspaceReview()
        {
            EditorApplication.update+=()=>
            {
                if(buildAfter<0||EditorApplication.timeSinceStartup<buildAfter)return;
                buildAfter=-1;
                if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Preserve scene and stop Play before queued build.");
                BuildAndroid();
            };
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("ProductWorkspaceReview", false))
                {
                    SessionState.SetBool("ProductWorkspaceReview", false);
                    new GameObject("Product workspace focused review").AddComponent<ProductWorkspaceReviewRunner>();
                }
            };
        }
        public static void QueueAndroidBuild()
        {
            if(buildAfter>=0||UnityEditor.BuildPipeline.isBuildingPlayer)throw new InvalidOperationException("Build already pending.");
            buildAfter=EditorApplication.timeSinceStartup+3;
        }
        [MenuItem("Tools/JarVis/Review Products and Labels")]
        public static void Run()
        {
            if (!Application.isBatchMode && (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)) throw new InvalidOperationException("Stop Play and preserve unsaved scene edits before review.");
            Directory.CreateDirectory("Logs/ProductWorkspace");
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("ProductWorkspaceReview", true); EditorApplication.isPlaying = true;
        }
        public static void BuildAndroid()
        {
            const string output = "Builds/Android/JarVis-Warehouse.apk";
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0 || scenes[0] != "Assets/Scenes/JarVisFigmaApp.unity") throw new InvalidOperationException("Preserve the current JarVis entry scene before building.");
            Directory.CreateDirectory("Builds/Android"); Directory.CreateDirectory("Logs/ProductWorkspace");
            if (File.Exists(output) && !File.Exists("Builds/Android/JarVis-Warehouse.previous.apk")) File.Copy(output, "Builds/Android/JarVis-Warehouse.previous.apk");
            var report = UnityEditor.BuildPipeline.BuildPlayer(new UnityEditor.BuildPlayerOptions
            {
                scenes = scenes, locationPathName = output, target = BuildTarget.Android,
                options = UnityEditor.BuildOptions.StrictMode | UnityEditor.BuildOptions.CompressWithLz4HC
            });
            bool success = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded && File.Exists(output) && new FileInfo(output).Length > 0;
            File.WriteAllText("Logs/ProductWorkspace/AndroidBuild.txt", report.summary.result + "\n" + DateTime.UtcNow.ToString("O") + "\n" + Path.GetFullPath(output) + "\n" + report.summary.totalSize + " bytes\nExisting project settings retained; product writes/printing not connected; no device acceptance.\n");
            if (!success) throw new InvalidOperationException("Android build failed. See ProductWorkspaceAndroidBuild.log.");
        }
    }
    public sealed class ProductWorkspaceReviewRunner : MonoBehaviour
    {
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        void Check(bool pass, string text) { checks.Add((pass ? "PASS " : "FAIL ") + text); if (!pass) errors.Add(text); }
        void Error(string condition, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error) errors.Add(condition); }
        IEnumerator Start()
        {
            Application.logMessageReceived += Error;
            yield return null; yield return null;
            var nav = FindAnyObjectByType<ImportedFigmaNavigation>();
            var language = nav.GetComponent<ImportedAppLocalization>();
            var originalLanguage = JarvisLocalization.CurrentLanguage;
            int originalReducedMotion = PlayerPrefs.GetInt("JarVis.ReduceMotion", 0);
            language.Select(SupportedLanguage.Vi); yield return null;
            string originalTheme = PlayerPrefs.GetString("JarVis.Theme", "light");
            var root = nav.screens.First(screen => screen.id == "5:450").root;
            var workspace = root.GetComponent<WarehouseProductWorkspace>();
            Check(workspace != null, "Existing label route now uses native product workspace");
            Check(root.GetComponent<PrototypeOperationPresentation>() == null, "No competing label presentation");
            Check(workspace.TemplateCount == 134, "All 134 source templates imported");
            Check(nav.screens.First(screen => screen.id == "5:935").root.GetComponent<PrototypeOperationPresentation>() != null, "Inbound owner preserved");
            Check(!OmsMobileSession.IsReady, "Editor review has no authenticated backend session");
            var dock = nav.BottomNavigation;
            workspace.AcceptInventory(new[] { new OmsMobileApi.InventoryRow { productId = "qa-milk", name = "Sữa tươi 180 ml", sku = "QA-MILK", barcode = "QA-CODE", unit = "Hộp", lotCode = "A" }, new OmsMobileApi.InventoryRow { productId = "qa-milk", name = "Sữa tươi 180 ml", sku = "QA-MILK", barcode = "QA-CODE", unit = "Hộp", lotCode = "B" } });
            nav.Show("5:450", false); yield return null;
            Check(root.GetComponentsInChildren<UnityEngine.UI.Button>().Count(button => button.name == "Product row QA-MILK") == 1, "Two lots become one product entry without stock mutation");
            Check(((RectTransform)root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Product row QA-MILK").transform).rect.height == 88, "Product identity and label action share one compact 88px row");
            Check(WarehouseProductWorkspace.SearchKey("sữa180ml") == WarehouseProductWorkspace.SearchKey("Sữa 180 ml"), "Novice search ignores accents and spaces");
            Capture(nav, "Products-412.png", 412, 915);
            workspace.BeginCreate(); yield return null; Capture(nav, "Templates-412.png", 412, 915);
            workspace.SelectTemplate("drink-beverages-sua-tuoi"); yield return null;
            workspace.AcceptInventory(new[] { new OmsMobileApi.InventoryRow { productId = "qa-milk", name = "Sữa tươi 180 ml", sku = "QA-MILK", barcode = "QA-CODE", unit = "Hộp" }, new OmsMobileApi.InventoryRow { productId = "qa-milk-390", name = "Sữa tươi 390 ml", sku = "QA-MILK-390", barcode = "QA-CODE-390", unit = "Hộp" } }); yield return null;
            workspace.SetValue("volume", "390");
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Có hàng tương tự · kiểm tra trước").onClick.Invoke(); yield return null;
            Check(root.GetComponentsInChildren<UnityEngine.UI.Button>().Count(button => button.name.StartsWith("Product row QA-MILK")) == 2, "Similar-product inspection shows both variants, not just the first SKU");
            Check(workspace.TryBack(), "System back exits related-product inspection inside draft"); yield return null;
            Check(root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input volume").text == "390", "Inspecting similar items preserves current draft input");
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Có hàng tương tự · kiểm tra trước").onClick.Invoke(); yield return null;
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Product row QA-MILK-390").onClick.Invoke(); yield return null;
            Check(workspace.CurrentMode == "label" && root.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.name == "Quay lại bản nháp"), "Related-product label inspection exposes an explicit draft return");
            Check(workspace.TryBack(), "Hardware Back from inspected label returns to draft"); yield return null;
            Check(workspace.CurrentMode == "create" && root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input volume").text == "390", "Selecting a related row preserves draft flow and entered values");
            workspace.SetValue("volume", "");
            Check(!workspace.ReviewDraft(), "Missing required content rejected");
            workspace.SetValue("volume", "0"); Check(!workspace.ReviewDraft(), "Zero measure rejected");
            workspace.SetValue("volume", "180"); Check(workspace.ReviewDraft(), "Valid milk draft reaches confirmation");
            yield return null; Capture(nav, "Confirm-412.png", 412, 915); Capture(nav, "Confirm-390.png", 390, 844);
            workspace.Back(); yield return null;
            var input = root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input volume");
            Check(input.text == "180", "Back keeps entered measurement");
            typeof(WarehouseProductWorkspace).GetMethod("ChangeChoice", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workspace, new object[] { "volumeUnit", "l" });
            workspace.SetValue("skuMode", "manual"); workspace.SetValue("sku", "QA-MILK"); Check(!workspace.ReviewDraft(), "Duplicate warehouse SKU blocked");
            workspace.SetValue("skuMode", "auto"); workspace.SetValue("pack", "case"); workspace.SetValue("contained", "1.5"); Check(!workspace.ReviewDraft(), "Fractional pack count rejected");
            workspace.SetValue("pack", "single"); workspace.ReviewDraft(); workspace.Back(); yield return null;
            Check(root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input volume").text == "0.18", "Native unit conversion preserves physical amount");
            workspace.SetValue("barcodeMode", "package"); workspace.SetValue("barcode", "4006381333932"); Check(!workspace.ReviewDraft(), "Invalid GTIN check digit rejected without ownership claim");
            workspace.SetValue("barcodeMode", "internal"); workspace.SetValue("barcode", ""); workspace.ReviewDraft(); workspace.Back(); yield return null;
            Capture(nav, "Form-412.png", 412, 915);
            foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>()) Check(((RectTransform)button.transform).rect.height >= 44, "Touch target " + button.name);
            Check(nav.BottomNavigation == dock, "Shared dock not replaced");
            Check(!root.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("MOCK-JV")), "No generated mock barcode in production workspace");
            Check(WarehouseProductWorkspace.MatchesSearch("Sữa tươi 180 ml", "sữa180ml"), "Compact search across descriptive words");
            Check(WarehouseProductWorkspace.SearchRank(new OmsMobileApi.InventoryRow { sku = "QA-MILK", barcode = "QA-CODE" }, "QA-CODE") == 0, "Exact original barcode prioritized");
            var scroll = root.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
            float offset = Mathf.Min(160, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height)); scroll.content.anchoredPosition = new Vector2(0, offset);
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Product choice volumeUnit").onClick.Invoke(); yield return null;
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "ml").onClick.Invoke(); yield return null;
            Check(Mathf.Abs(root.GetComponentInChildren<UnityEngine.UI.ScrollRect>().content.anchoredPosition.y - offset) < 2, "Choice returns to previous form scroll position");
            typeof(WarehouseProductWorkspace).GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workspace, true);
            nav.Show("prototype:tools", false); yield return null; Check(!workspace.Loading, "Leaving screen clears interrupted loading flag");
            nav.Show("5:450", false); yield return null; Check(!workspace.Loading, "Returning to cached products does not stick loading");
            nav.Back(); yield return null; Check(nav.CurrentScreen == "5:450", "System back returns inside product flow before leaving module");
            workspace.SelectTemplate("drink-beverages-sua-tuoi"); yield return null;
            nav.Settings.SelectTheme("dark"); yield return null; yield return null; Capture(nav, "Form-dark-390.png", 390, 844);
            language.Select(SupportedLanguage.En); yield return null; yield return null;
            var catalog = JsonUtility.FromJson<WarehouseProductWorkspace.Catalog>(Resources.Load<TextAsset>("JarVisProductTemplates").text);
            var schemaText = catalog.categories.Select(item => item.label).Concat(catalog.templates.SelectMany(item => new[] { item.name, item.familyLabel }.Concat(item.units).Concat(item.attributes.SelectMany(attribute => new[] { attribute.label }.Concat(attribute.choices ?? Array.Empty<string>()).Concat(attribute.units ?? Array.Empty<string>()))))).Distinct().ToArray();
            Check(schemaText.All(source => WarehouseProductWorkspace.SearchKey(ImportedAppLocalization.Text(source)) == new string(ImportedAppLocalization.Text(source).Where(character => !char.IsWhiteSpace(character)).Select(char.ToLowerInvariant).ToArray())), "All schema category/family/template/field/option/unit captions have English displays");
            Check(ImportedAppLocalization.Text("Đen") == "Black" && ImportedAppLocalization.Text("Dung tích trên nhãn") == "Labelled volume", "Specific English warehouse terminology");
            workspace.BeginCreate(); yield return null;
            root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input templateSearch").text = "fresh milk";
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Submit templateSearch").onClick.Invoke(); yield return null;
            Check(root.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.name == "Template drink-beverages-sua-tuoi"), "English search finds the same canonical milk template");
            Capture(nav, "Templates-English-412.png", 412, 915);
            workspace.SelectTemplate("drink-beverages-sua-tuoi"); yield return null;
            Check(root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input name").text == "Sữa tươi", "Existing Vietnamese draft name is never silently translated");
            workspace.SetValue("name", "Sữa khách nhập QA"); workspace.SetValue("volume", "180"); workspace.SetValue("volumeUnit", "ml"); workspace.ReviewDraft(); workspace.Back(); yield return null;
            Check(!root.GetComponentsInChildren<TMP_InputField>().Any(field => field.name == "Product input brand"), "Optional brand collapsed by default");
            Check(!root.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.name == "Product choice skuMode"), "Default code settings collapsed but accessible");
            var measure = (RectTransform)root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input volume").transform;
            var unit = (RectTransform)root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Product choice volumeUnit").transform;
            Check(Mathf.Abs(measure.anchoredPosition.y - unit.anchoredPosition.y) < 1 && measure.rect.width + unit.rect.width <= 366, "Measure and unit share one accessible row");
            Capture(nav, "Form-English-dark-390.png", 390, 844);
            nav.Settings.SelectTheme("light"); yield return null; yield return null;
            Capture(nav, "Form-English-412.png", 412, 915);
            workspace.SetValue("volume", "0"); Check(!workspace.ReviewDraft() && workspace.CurrentMessage.StartsWith("Labelled volume"), "English validation uses translated attribute name");
            language.Select(SupportedLanguage.Vi); yield return null; yield return null;
            Check(workspace.CurrentMessage.StartsWith("Dung tích trên nhãn"), "Validation message follows locale switch");
            language.Select(SupportedLanguage.En); workspace.SetValue("volume", "180"); Check(workspace.ReviewDraft(), "English draft keeps canonical unit values valid"); yield return null;
            Check(root.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("Sữa khách nhập QA")), "Confirmation preserves user-authored product name");
            Capture(nav, "Confirm-English-412.png", 412, 915);
            workspace.BeginCreate(); workspace.SelectTemplate(catalog.templates.First(item => item.category == "fashion" && item.attributes.Any(attribute => attribute.id == "colour")).id); yield return null;
            Check(root.GetComponentsInChildren<TMP_InputField>().First(field => field.name == "Product input name").text.All(character => character < 128), "New English template draft is initially named in English");
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Product choice colour").onClick.Invoke(); yield return null;
            root.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Black").onClick.Invoke(); yield return null;
            var values = (Dictionary<string, string>)typeof(WarehouseProductWorkspace).GetField("values", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workspace);
            Check(values["colour"] == "Đen", "English option click stores original canonical enum, not display text");
            Capture(nav, "Clothing-English-390.png", 390, 844);
            workspace.Back(); workspace.Back(); yield return null;
            Check(root.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("Sữa tươi 180 ml")), "API product names remain Vietnamese in English interface");
            nav.Show("126:8", false); yield return null; yield return null;
            var presence = new OmsMobileApi.PresenceReply { status = "ONLINE", serverNow = DateTimeOffset.UtcNow.ToString("O"), nextResetAt = DateTimeOffset.UtcNow.AddHours(8).ToString("O"), leaseUntil = DateTimeOffset.UtcNow.AddMinutes(2).ToString("O"), streak = new OmsMobileApi.ActivityStreak { currentStreak = 1, todayQualified = true } };
            typeof(ImportedPresence).GetMethod("Accept", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(nav.Presence, new object[] { presence }); yield return null;
            var mascot = nav.screens.First(screen => screen.id == "126:8").root.GetComponentInChildren<PrototypeMascot>();
            mascot.PreviewTime = 2; PlayerPrefs.SetInt("JarVis.ReduceMotion", 0); yield return null;
            Canvas.ForceUpdateCanvases();
            var canvas = nav.GetComponent<Canvas>();
            var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, ((RectTransform)mascot.transform).TransformPoint(((RectTransform)mascot.transform).rect.center));
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = point, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && hits[0].gameObject == mascot.gameObject, "Mascot is the actual first pointer raycast target");
            if (hits.Count > 0) UnityEngine.EventSystems.ExecuteEvents.Execute(hits[0].gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Check(mascot.IsHappy, "One pointer tap makes JarVis happy immediately without a frame delay");
            yield return new WaitForSecondsRealtime(.3f);
            Check(((RectTransform)mascot.transform.Find("Transparent JarVis actor")).anchoredPosition.y > 0, "Happy tap animates a gentle hop");
            Capture(nav, "Home-English-happy-412.png", 412, 915);
            Check(nav.screens.First(screen => screen.id == "126:8").root.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "1 consecutive active day"), "English streak singular grammar");
            mascot.OnPointerClick(pointer); Check(mascot.IsHappy, "Repeated taps restart happy feedback");
            PlayerPrefs.SetInt("JarVis.ReduceMotion", 1); mascot.OnPointerClick(pointer); yield return null;
            Check(mascot.IsHappy && ((RectTransform)mascot.transform.Find("Transparent JarVis actor")).anchoredPosition == Vector2.zero, "Reduced motion keeps immediate static smile without hop");
            Capture(nav, "Home-English-static-happy-390.png", 390, 844);
            yield return new WaitForSecondsRealtime(1.7f); yield return null; Check(!mascot.IsHappy, "Tap feedback expires cleanly back to preview idle"); mascot.PreviewTime = -1;
            PlayerPrefs.SetInt("JarVis.ReduceMotion", originalReducedMotion); language.Select(originalLanguage);
            nav.Settings.SelectTheme(originalTheme);
            File.WriteAllText("Logs/ProductWorkspace/Result.txt", (errors.Count == 0 ? "PASS" : "FAIL") + "\n" + DateTime.UtcNow.ToString("O") + "\nEditor-only fixture, no DB/model/mutation/printing.\n" + string.Join("\n", checks) + "\nErrors:\n" + string.Join("\n", errors));
            Application.logMessageReceived -= Error;
            if (Application.isBatchMode) EditorApplication.Exit(errors.Count == 0 ? 0 : 1); else EditorApplication.isPlaying = false;
        }
        void Capture(ImportedFigmaNavigation nav, string name, int width, int height)
        {
            var canvas = nav.GetComponent<Canvas>(); var camera = FindAnyObjectByType<Camera>();
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active; var oldDistance = canvas.planeDistance;
            var target = new RenderTexture(width, height, 24); var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; camera.targetTexture = target;
                Canvas.ForceUpdateCanvases(); nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit(); Canvas.ForceUpdateCanvases(); camera.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply(); File.WriteAllBytes("Logs/ProductWorkspace/" + name, image.EncodeToPNG());
            }
            finally { camera.targetTexture = oldTarget; canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance; RenderTexture.active = oldActive; Destroy(image); Destroy(target); }
        }
    }
}
