using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class WalkingRouteUiReview
    {
        public static void Run()
        {
            string folder = Path.GetFullPath("Logs/WalkingRoutes");
            Directory.CreateDirectory(folder);
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity", OpenSceneMode.Single);
            List<string> checks = new List<string>();
            foreach (Vector2Int dimensions in new[] { new Vector2Int(412, 915), new Vector2Int(412, 732) })
            foreach (string mode in new[] { "record", "waiting", "confirm", "right", "left", "guiding", "off-route", "lost", "arrived", "saved", "recording", "calibration" })
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                RenderTexture target = new RenderTexture(dimensions.x, dimensions.y, 24);
                RenderTexture previous = RenderTexture.active;
                try
                {
                    GameObject root = new GameObject("Recorder UI QA");
                    SceneManager.MoveGameObjectToScene(root, scene);
                    WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                    WalkingRouteRecorderUi ui = root.AddComponent<WalkingRouteRecorderUi>();
                    ui.Recorder = recorder;
                    if (mode != "record")
                    {
                        WalkingRouteData route = JsonUtility.FromJson<WalkingRouteData>(Resources.Load<TextAsset>("Routes/RecordedPrefix").text);
                        if (mode == "saved" || mode == "recording")
                        {
                            typeof(WalkingRouteRecorder).GetField("recovered",BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recorder,route);
                            if (mode == "recording") recorder.Route.Begin(route.startQr,Vector3.zero,0,0,true);
                        }
                        recorder.Follower.Prepare(route);
                        typeof(WalkingRouteRecorder).GetProperty("NavigationMode").SetValue(recorder, true);
                        typeof(WalkingRouteRecorder).GetProperty("Ready").SetValue(recorder, true);
                        if (mode != "waiting") recorder.Follower.AcceptQr(route.startQr);
                        if (mode != "waiting" && mode != "confirm")
                        {
                            recorder.Follower.Align(Vector3.zero, 0, 0, true);
                            recorder.Follower.Observe(Vector3.zero, mode == "right" ? -150 : 0, 0.1, true);
                        }
                        if (mode == "off-route")
                            for (int sample = 1; sample <= 20; sample++) recorder.Follower.Observe(new Vector3(sample * 0.15f,0,0), 0, 0.1 + sample * 0.1, true);
                        if (mode == "lost") recorder.Follower.Observe(Vector3.zero, 0, 0.2, false);
                        if (recorder.Follower.Active)
                        {
                            WalkingRouteLiveView live = root.AddComponent<WalkingRouteLiveView>();
                            typeof(WalkingRouteRecorder).GetProperty("LiveView").SetValue(recorder,live);
                            live.TryAutoPlace(recorder.Follower,true);
                        }
                        if (mode == "arrived")
                        {
                            typeof(WalkingRouteFollower).GetProperty("Active").SetValue(recorder.Follower, false);
                            typeof(WalkingRouteFollower).GetProperty("Arrived").SetValue(recorder.Follower, true);
                        }
                    }
                    typeof(WalkingRouteRecorderUi).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                    if (mode == "saved" || mode == "recording") typeof(WalkingRouteRecorder).GetProperty("NavigationMode").SetValue(recorder,false);
                    if (mode == "calibration") typeof(WalkingRouteRecorderUi).GetField("adjusting",BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui,true);
                    GameObject cameraObject = new GameObject("UI QA camera", typeof(Camera));
                    SceneManager.MoveGameObjectToScene(cameraObject, scene);
                    Camera camera = cameraObject.GetComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.08f, 0.16f, 0.25f);
                    camera.targetTexture = target;
                    camera.orthographic = true;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 100;
                    Canvas canvas = root.transform.Find("Route recorder canvas").GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 5;
                    typeof(WalkingRouteRecorderUi).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, null);
                    Transform world = root.transform.Find("World route - manually aligned, estimated floor");
                    if (world != null) world.gameObject.SetActive(false);
                    Canvas.ForceUpdateCanvases();
                    foreach (Text text in root.GetComponentsInChildren<Text>())
                    {
                        if (text.font == null) throw new InvalidOperationException("Missing native font: " + text.name);
                        var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                        float desired = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
                        if (desired > text.rectTransform.rect.height + 1) checks.Add("OVERFLOW " + dimensions + " " + mode + " " + text.name + " needs=" + desired + " height=" + text.rectTransform.rect.height);
                    }
                    camera.Render();
                    RenderTexture.active = target;
                    Texture2D screenshot = new Texture2D(dimensions.x, dimensions.y, TextureFormat.RGB24, false);
                    screenshot.ReadPixels(new Rect(0, 0, dimensions.x, dimensions.y), 0, 0);
                    screenshot.Apply();
                    File.WriteAllBytes(Path.Combine(folder, "ui-" + mode + "-" + dimensions.x + "x" + dimensions.y + ".png"), screenshot.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(screenshot);
                    checks.Add("RENDERED " + dimensions + " " + mode + " native uGUI with synthetic poses/state; physical safe area / camera NOT validated");
                }
                finally
                {
                    RenderTexture.active = previous;
                    target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            File.WriteAllLines(Path.Combine(folder, "ui-review.txt"), checks);
            if (checks.Exists(check => check.StartsWith("OVERFLOW"))) throw new InvalidOperationException("UI text overflow, see Logs/WalkingRoutes/ui-review.txt");
            Debug.Log("[WalkingRouteUiReview] Native renders completed.");
        }
    }
}
