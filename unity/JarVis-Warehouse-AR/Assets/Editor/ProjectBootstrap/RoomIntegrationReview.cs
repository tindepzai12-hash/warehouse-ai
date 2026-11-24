using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.RouteRecording;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoomIntegrationReview
    {
        private static EditorBuildSettingsScene[] previous;
        static RoomIntegrationReview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("RoomIntegrationReview",false))
                {
                    SessionState.SetBool("RoomIntegrationReview",false);
                    new GameObject("Room integration QA").AddComponent<RoomIntegrationRunner>();
                }
                if (state == PlayModeStateChange.EnteredEditMode) Restore();
            };
            EditorApplication.quitting += Restore;
        }
        private static void Restore() { if (previous != null) { EditorBuildSettings.scenes = previous; previous = null; } }
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first");
            previous = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/JarVisFigmaApp.unity",true),new EditorBuildSettingsScene(WalkingRouteBuild.ScenePath,true) };
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("RoomIntegrationReview",true);
            EditorApplication.isPlaying = true;
        }
    }

    public sealed class RoomIntegrationRunner : MonoBehaviour
    {
        [Serializable] private sealed class Account { public string employeeCode,password,baseUrl; }
        private string result = "FAIL";
        private string failure;
        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.logMessageReceived += CaptureError;
            yield return null;
            yield return null;
            string path = Environment.GetEnvironmentVariable("JARVIS_TEST_ACCOUNT_FILE");
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) { Finish("Missing authorized test account file"); yield break; }
            Account account = JsonUtility.FromJson<Account>(File.ReadAllText(path));
            OmsMobileApi.LoginReply login = null;
            string error = null;
            yield return OmsMobileApi.Login(account.baseUrl,account.employeeCode,account.password,value => login = value,value => error = value);
            if (login == null) { Finish("Native login failed (details withheld)"); yield break; }
            OmsMobileSession.Set(account.baseUrl,login.accessToken,account.employeeCode,login.expiresAt);
            OmsMobileApi.TaskList tasks = null;
            yield return OmsMobileApi.GetTasks(value => tasks = value,value => error = value);
            const string taskId = "3a74b23f-7b89-49f2-b795-e04a19f1f24a";
            if (tasks == null || tasks.synthetic || !tasks.tasks.Any(task => task.id == taskId && task.status == "ASSIGNED"))
            { Finish("Native task endpoint did not return the assigned room test task"); yield break; }
            ImportedFigmaNavigation nav = FindAnyObjectByType<ImportedFigmaNavigation>();
            OmsMobileApi.PresenceReply online = null;
            yield return OmsMobileApi.SetPresence(true,value => online = value,value => error = value);
            if (online == null) { Finish("Test employee could not go online"); yield break; }
            nav.Presence.Restore();
            double presenceDeadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!nav.Presence.CanWork && Time.realtimeSinceStartupAsDouble < presenceDeadline) yield return null;
            if (!nav.Presence.CanWork) { Finish("Native presence did not confirm online"); yield break; }
            nav.Show("48:244",false);
            var screen = nav.screens.First(item => item.id == "48:244").root;
            double taskDeadline = Time.realtimeSinceStartupAsDouble + 45;
            while (!screen.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("TEST PHÒNG") || text.text.Contains("TASK-76834178-4F79")) && Time.realtimeSinceStartupAsDouble < taskDeadline) yield return null;
            if (!screen.GetComponentsInChildren<TMP_Text>().Any(text => text.text.Contains("TEST PHÒNG") || text.text.Contains("TASK-76834178-4F79")))
            {
                Directory.CreateDirectory("Logs/WalkingRoutes");
                File.WriteAllLines("Logs/WalkingRoutes/task-ui-diagnostic.txt",new[] { "Screen=" + nav.CurrentScreen,"Ready=" + OmsMobileSession.IsReady }.Concat(screen.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text)));
                Capture(nav,"live-task-ui-failed.png");
                Finish("Task API passed but native task list did not show the test task"); yield break;
            }
            yield return null;
            yield return null;
            Capture(nav,"live-task-ui.png");
            string qaTaskId = Environment.GetEnvironmentVariable("JARVIS_QA_TASK_ID");
            if (!string.IsNullOrEmpty(qaTaskId))
            {
                var qaTask = tasks.tasks.FirstOrDefault(task => task.id == qaTaskId);
                if (qaTask == null || qaTask.status != "ASSIGNED") { Finish("Separate QA task must be assigned before UI transition test"); yield break; }
                var rowButton = screen.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(button => button.name == "Task " + qaTask.task_code);
                if (rowButton == null) { Finish("QA task row missing in native UI"); yield break; }
                rowButton.onClick.Invoke();
                string[] captions = { "Nhận nhiệm vụ","Bắt đầu làm","Hoàn tất nhiệm vụ" };
                string[] englishCaptions = { "Accept task","Start task","Complete task" };
                string[] statuses = { "ACCEPTED","IN_PROGRESS","COMPLETED" };
                for (int step = 0; step < statuses.Length; step++)
                {
                    yield return null;
                    var action = screen.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(button => button.GetComponentsInChildren<TMP_Text>().Any(text => text.text == captions[step] || text.text == englishCaptions[step]));
                    if (action == null || !action.interactable) { Finish("Native task action missing: " + statuses[step]); yield break; }
                    action.onClick.Invoke();
                    bool confirmed = false;
                    double statusDeadline = Time.realtimeSinceStartupAsDouble + 30;
                    while (!confirmed && Time.realtimeSinceStartupAsDouble < statusDeadline)
                    {
                        yield return new WaitForSecondsRealtime(0.5f);
                        OmsMobileApi.TaskList updated = null;
                        yield return OmsMobileApi.GetTasks(value => updated = value,value => { });
                        confirmed = updated != null && updated.tasks.Any(task => task.id == qaTaskId && task.status == statuses[step]);
                    }
                    if (!confirmed) { Finish("Server did not confirm native task action: " + statuses[step]); yield break; }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                Capture(nav,"live-task-completed.png");
                screen.GetComponent<ImportedWarehouseLists>().CloseDetail();
            }
            nav.Show("215:2",false);
            yield return new WaitForSecondsRealtime(2);
            var map = nav.screens.First(item => item.id == "215:2").root;
            if (!map.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.name == "Mở ghi tuyến / dẫn AR" || button.name == "Open route recording / AR"))
            { Finish("Main map AR entry missing"); yield break; }
            Capture(nav,"main-map-ar-entry.png");
            Scene main = nav.gameObject.scene;
            WalkingRouteLauncher.Open();
            double timeout = Time.realtimeSinceStartupAsDouble + 20;
            while (!SceneManager.GetSceneByName(WalkingRouteLauncher.SceneName).isLoaded && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            yield return null;
            if (!SceneManager.GetSceneByName(WalkingRouteLauncher.SceneName).isLoaded || nav.gameObject.activeInHierarchy)
            { Finish("Additive AR bridge failed to load or suspend main UI"); yield break; }
            WalkingRouteLauncher.Close();
            timeout = Time.realtimeSinceStartupAsDouble + 20;
            while (WalkingRouteLauncher.Embedded && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
            yield return null;
            if (!nav.gameObject.activeInHierarchy || !OmsMobileSession.IsReady || SceneManager.GetSceneByName(WalkingRouteLauncher.SceneName).isLoaded)
            { Finish("Returning from AR did not restore main UI/session: UI=" + nav.gameObject.activeInHierarchy + ", session=" + OmsMobileSession.IsReady + ", routeLoaded=" + SceneManager.GetSceneByName(WalkingRouteLauncher.SceneName).isLoaded); yield break; }
            result = "PASS";
            yield return OmsMobileApi.SetPresence(false,value => { },value => { });
            Finish("Live Unity login + assigned task API + native task labels; " + (string.IsNullOrEmpty(qaTaskId) ? "no task transition; " : "separate QA task accepted, started and completed using native buttons with server confirmation; ") + "map AR action; additive route scene open/close and session preserved. No stock change. Physical camera / room alignment NOT tested.");
        }
        private void CaptureError(string text,string stack,LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error) failure = text;
        }
        private void Finish(string text)
        {
            if (failure != null) { result = "FAIL"; text += "\nConsole error: " + failure; }
            Application.logMessageReceived -= CaptureError;
            OmsMobileSession.Clear();
            Directory.CreateDirectory("Logs/WalkingRoutes");
            File.WriteAllText("Logs/WalkingRoutes/room-integration-result.txt",result + "\n" + text + "\nUTC=" + DateTime.UtcNow.ToString("O"));
            EditorApplication.Exit(result == "PASS" ? 0 : 1);
        }
        private static void Capture(ImportedFigmaNavigation nav,string name)
        {
            Canvas canvas = nav.GetComponent<Canvas>();
            Camera camera = FindAnyObjectByType<Camera>();
            RenderMode mode = canvas.renderMode;
            Camera oldCamera = canvas.worldCamera;
            RenderTexture oldTarget = camera.targetTexture,oldActive = RenderTexture.active;
            float plane = canvas.planeDistance;
            RenderTexture target = new RenderTexture(412,915,24);
            Texture2D image = new Texture2D(412,915,TextureFormat.RGBA32,false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                camera.targetTexture = target;
                nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,412,915),0,0);
                image.Apply();
                Directory.CreateDirectory("Logs/WalkingRoutes");
                File.WriteAllBytes("Logs/WalkingRoutes/" + name,image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                canvas.renderMode = mode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = plane;
                RenderTexture.active = oldActive;
                Destroy(image);
                Destroy(target);
            }
        }
    }
}
