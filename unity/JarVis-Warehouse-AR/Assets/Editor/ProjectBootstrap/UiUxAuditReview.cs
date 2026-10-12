using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cdm.Figma.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class UiUxAuditReview
    {
        static UiUxAuditReview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("UiUxAuditReview", false)) return;
                SessionState.SetBool("UiUxAuditReview", false);
                new GameObject("JarVis offline UI audit").AddComponent<UiUxAuditRunner>().stage = SessionState.GetString("UiUxAuditStage", "before");
            };
        }
        public static void Before() { Run("before"); }
        public static void After() { Run("after"); }
        static void Run(string stage)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Preserve scene edits and stop Play before UI audit.");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/JarVisFigmaApp.unity") throw new InvalidOperationException("Use the current saved JarVis app scene.");
            Directory.CreateDirectory("Docs/UiUxAudit/screenshots/" + stage);
            Directory.CreateDirectory("Logs/UiUxAudit");
            SessionState.SetString("UiUxAuditStage", stage); SessionState.SetBool("UiUxAuditReview", true); EditorApplication.isPlaying = true;
        }
    }
    public sealed class UiUxAuditRunner : MonoBehaviour
    {
        public string stage;
        readonly List<string> checks = new List<string>();
        readonly List<string> errors = new List<string>();
        readonly List<string> observations = new List<string>();
        void Check(bool pass, string text) { checks.Add((pass ? "PASS " : "FAIL ") + text); if (!pass) errors.Add(text); }
        void Error(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) errors.Add(message); }
        IEnumerator Start()
        {
            Application.logMessageReceived += Error;
            yield return null; yield return null;
            var nav = FindAnyObjectByType<ImportedFigmaNavigation>();
            if (nav == null || OmsMobileSession.IsReady) { File.WriteAllText("Logs/UiUxAudit/" + stage + ".txt", "BLOCKED: UI audit requires unauthenticated saved app; never replace a live session with QA data."); Application.logMessageReceived -= Error; EditorApplication.isPlaying = false; yield break; }
            var language = nav.GetComponent<ImportedAppLocalization>();
            var oldLanguage = JarvisLocalization.CurrentLanguage;
            string oldTheme = nav.Settings.Theme; int oldSize = PlayerPrefs.GetInt("JarVis.TextSize", 1);
            foreach (var scanner in nav.GetComponentsInChildren<ImportedCameraScanner>(true)) scanner.enabled = false;
            language.Select(SupportedLanguage.En); nav.Settings.SelectTheme("light"); PlayerPrefs.SetInt("JarVis.TextSize", 1);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ImportedPresence).GetMethod("Accept", flags).Invoke(nav.Presence, new object[] { new OmsMobileApi.PresenceReply { status = "ONLINE", serverNow = DateTimeOffset.UtcNow.ToString("O"), nextResetAt = DateTimeOffset.UtcNow.AddHours(8).ToString("O"), leaseUntil = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"), streak = new OmsMobileApi.ActivityStreak { currentStreak = 3, todayQualified = true, week = Enumerable.Range(0, 7).Select(index => new OmsMobileApi.ActivityDay { qualified = index < 3 }).ToArray() } } });
            var lists = nav.GetComponentsInChildren<ImportedWarehouseLists>(true);
            foreach (var list in lists)
            {
                typeof(ImportedWarehouseLists).GetField("hasData", flags).SetValue(list, true);
                typeof(ImportedWarehouseLists).GetField("taskRows", flags).SetValue(list, new[] { new OmsMobileApi.TaskRow { task_code = "QA-TASK", title = "QA: check item location", status = "ASSIGNED", employeeCode = "QA-NV", productName = "QA product", sku = "QA-SKU", locationCode = "A-01", expected_quantity = 2, unit = "Piece" } });
                typeof(ImportedWarehouseLists).GetField("stockRows", flags).SetValue(list, new[] { new OmsMobileApi.InventoryRow { productId = "qa", name = "QA product", sku = "QA-SKU", barcode = "QA-CODE", lotCode = "QA-LOT", locationCode = "A-01", unit = "Piece", available = 12, onHand = 15, reserved = 3 } });
            }
            foreach (var screen in nav.screens)
            {
                nav.Show(screen.id, false); yield return new WaitForSecondsRealtime(.1f);
                Check(nav.CurrentScreen == screen.id, "Native route " + screen.id);
                Capture(nav, screen.id.Replace(':', '-') + "-en-light", 412, 915);
                foreach (var text in screen.root.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    if (text.isTextOverflowing) observations.Add(screen.id + " text overflow: " + text.text.Replace('\n', ' '));
                }
            }
            foreach (string route in new[] { "prototype:profile", "prototype:activity", "prototype:tools", "prototype:transactions" })
            {
                nav.Show(route, false); yield return null;
                Check(nav.CurrentScreen == route, "Runtime route " + route); Capture(nav, route.Replace(':', '-') + "-en-light", 412, 915);
            }
            nav.Show("126:8", false); yield return null;
            var home = nav.screens.First(screen => screen.id == "126:8").root;
            var shortcuts = home.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Quick action ")).ToArray();
            Check(shortcuts.Length == 4, "Four operational Home shortcuts, no additional marketing actions");
            string[] destinations = { "prototype:transactions", "5:780", "48:244", "5:1029" };
            for (int index = 0; index < shortcuts.Length; index++)
            {
                shortcuts[index].onClick.Invoke(); Check(nav.CurrentScreen == destinations[index], "Home shortcut callback " + destinations[index]); nav.Show("126:8", false); yield return null;
                shortcuts = home.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Quick action ")).ToArray();
            }
            var tabs = nav.BottomNavigation.GetComponentsInChildren<FigmaNode>(true).Where(node => node.nodeName.StartsWith("tab-")).ToArray();
            string[] routes = { "126:8", "48:244", "215:2", "5:1216" };
            for (int index = 0; index < tabs.Length; index++)
            {
                var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(tabs[index].gameObject, pointer, ExecuteEvents.pointerClickHandler);
                Check(nav.CurrentScreen == routes[index], "Dock real click handler " + routes[index]);
                if (stage == "after")
                {
                    Check(tabs.All(tab => tab.GetComponentInChildren<TMP_Text>(true).fontStyle.HasFlag(FontStyles.Bold)), "All dock labels remain bold after selecting " + routes[index]);
                    Check(tabs.Where(tab => tab.nodeName != "tab-JarVis").All(tab => tab.GetComponentsInChildren<ImportedUtilityIcon>(true).Any(icon => icon.enabled && icon.strokeWidth >= 2.3f)), "Dock uses bold crisp vector strokes");
                }
            }
            if (stage == "after")
            {
                CheckSessionAndInputs(nav, lists, flags);
                nav.Show("126:8", false); yield return null;
                string[] days = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
                Check(days.All(day => home.GetComponentsInChildren<TMP_Text>().Any(text => text.text == day)), "Seven distinct English weekday labels preserve Monday-first order");
                var font = home.GetComponentsInChildren<TMP_Text>().First(text => text.text.StartsWith("Hello")).font;
                Check(font.fontWeightTable[7].regularTypeface != null && font.fontWeightTable[7].regularTypeface.faceInfo.styleName == "Bold", "Real Be Vietnam Pro Bold face, not only synthetic weight");
            }
            foreach (var locale in new[] { SupportedLanguage.Vi, SupportedLanguage.En })
            {
                language.Select(locale); nav.Settings.SelectTheme("dark"); PlayerPrefs.SetInt("JarVis.TextSize", 2);
                foreach (string route in new[] { "126:8", "48:244", "5:780", "5:1308", "5:450", "5:1216" })
                {
                    nav.Show(route, false); yield return new WaitForSecondsRealtime(.35f);
                    Capture(nav, route.Replace(':', '-') + "-" + locale + "-dark-large", 390, 844);
                }
            }
            language.Select(oldLanguage); nav.Settings.SelectTheme(oldTheme); PlayerPrefs.SetInt("JarVis.TextSize", oldSize);
            File.WriteAllText("Logs/UiUxAudit/" + stage + ".txt", (errors.Count == 0 ? "PASS" : "FAIL") + "\n" + DateTime.UtcNow.ToString("O") + "\nUnauthenticated Editor-only fixture. No API/model/database mutation/camera/physical microphone.\n" + string.Join("\n", checks) + "\nObservations (inspect visually, not automatic failures):\n" + string.Join("\n", observations) + "\nErrors:\n" + string.Join("\n", errors));
            Application.logMessageReceived -= Error; EditorApplication.isPlaying = false;
        }
        void CheckSessionAndInputs(ImportedFigmaNavigation nav, ImportedWarehouseLists[] lists, BindingFlags flags)
        {
            var password=nav.GetComponentsInChildren<TMP_InputField>(true).First(field=>field.contentType==TMP_InputField.ContentType.Password);
            string draft=password.text;
            foreach(int length in new[]{65,256}){password.text=new string('x',length);Check(password.text.Length==length&&password.characterLimit==256,"Login accepts "+length+" password characters without truncation");}
            password.SetTextWithoutNotify(draft);
            try
            {
                foreach(var list in lists)
                {
                    var type=typeof(ImportedWarehouseLists);
                    var synchronize=type.GetMethod("SynchronizeSession",flags);
                    OmsMobileSession.Set("https://qa.invalid","editor-only-a","QA-A",DateTime.UtcNow.AddMinutes(5).ToString("O"));
                    synchronize.Invoke(list,null);
                    type.GetField("taskRows",flags).SetValue(list,new[]{new OmsMobileApi.TaskRow{status="ACCEPTED"}});
                    type.GetField("stockRows",flags).SetValue(list,new[]{new OmsMobileApi.InventoryRow{sku="PRIVATE-QA-A"}});
                    type.GetField("hasData",flags).SetValue(list,true);
                    int generation=(int)type.GetField("generation",flags).GetValue(list);
                    OmsMobileSession.Set("https://qa.invalid","editor-only-b","QA-B",DateTime.UtcNow.AddMinutes(5).ToString("O"));
                    synchronize.Invoke(list,null);
                    Check(((OmsMobileApi.TaskRow[])type.GetField("taskRows",flags).GetValue(list)).Length==0&&((OmsMobileApi.InventoryRow[])type.GetField("stockRows",flags).GetValue(list)).Length==0&&!(bool)type.GetField("hasData",flags).GetValue(list),"Account switch clears task/stock cache "+list.name);
                    Check(!(bool)type.GetMethod("CurrentRequest",flags).Invoke(list,new object[]{generation}),"Old account callback rejected "+list.name);
                    foreach(string state in new[]{"ACCEPTED","IN_PROGRESS","BLOCKED"})Check((bool)type.GetMethod("MatchesFilter",flags).Invoke(list,new object[]{new OmsMobileApi.TaskRow{status=state},1}),"Working filter includes "+state);
                    OmsMobileSession.Clear();synchronize.Invoke(list,null);
                    type.GetMethod("ReloadOrSignIn",flags).Invoke(list,null);
                    Check(nav.CurrentScreen=="5:36","Signed-out recovery goes directly to Login "+list.name);
                }
            }
            finally{OmsMobileSession.Clear();}
        }
        void Capture(ImportedFigmaNavigation nav, string name, int width, int height)
        {
            Directory.CreateDirectory("Logs/PrototypeParity");
            PrototypeParityRunner.Capture(nav, "UiUxAudit-" + stage + "-" + name + ".png", width, height);
            File.Copy("Logs/PrototypeParity/UiUxAudit-" + stage + "-" + name + ".png", "Docs/UiUxAudit/screenshots/" + stage + "/" + name + ".png", true);
        }
    }
}
