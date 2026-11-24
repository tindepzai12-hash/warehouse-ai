#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Preview;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ProjectBootstrap
{
    public static class PickupPreviewBuild
    {
        const string Out = @"C:\Users\Windows\Documents\ChatGPT\jarvis\pickup-demo-20261121";
        const string AssetRoot = "Assets/JarVis/Demos/PickupReview";
        static readonly List<string> results = new List<string>();
        static void Check(bool ok, string description) { results.Add((ok ? "PASS " : "FAIL ") + description); if (!ok) throw new InvalidOperationException(description); }
        static void Click(PickupFlowPreview ui, string name)
        {
            var b = ui.GetComponentsInChildren<Button>().Single(v => v.name == name);
            Check(b.interactable, "Button enabled: " + name); b.onClick.Invoke();
        }
        [MenuItem("Tools/JarVis/Demo/Build and capture pickup preview")]
        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(Out); Directory.CreateDirectory(AssetRoot);
                var guarded = new[] { "Assets/Scenes/JarVisFigmaApp.unity", "Assets/Scenes/Roompresent_VPS.unity", "Assets/Resources/RoompresentCheckedRegistration.json", "ProjectSettings/EditorBuildSettings.asset", "Assets/JarVis/Scripts/Navigation/RoompresentVpsLocalization.cs", "Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs" };
                var before = guarded.ToDictionary(p => p, File.ReadAllBytes);
                CameraSequenceChecks();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cameraGo = new GameObject("Preview UI camera", typeof(Camera)); var camera = cameraGo.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.94f, .96f, .98f); camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 10; camera.transform.position = new Vector3(0, 0, -5);
                var root = new GameObject("JarVis pickup preview - isolated", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PickupFlowPreview), typeof(PreviewCameraRestart));
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(412, 915); scaler.matchWidthOrHeight = 0;
                var ui = root.GetComponent<PickupFlowPreview>();
                ui.regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/JarVis/DesignImports/BeVietnamPro Figma SDF.asset");
                ui.bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/BeVietnamPro-Bold SDF.asset");
                ui.roomPhoto = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + "/RoomPhoto.png");
                ui.cameraRestart = root.GetComponent<PreviewCameraRestart>(); ui.Initialize(canvas);
                Check(ui.regular != null && ui.bold != null && ui.roomPhoto != null, "Existing project fonts and owner room image available");
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                ui.Build(915);
                EditorSceneManager.SaveScene(scene, AssetRoot + "/PickupReview.unity");
                Capture(ui, canvas, camera, "01-task");
                Click(ui, "Accept mission"); Check(ui.Step == PickupPreviewStep.AlignA, "Accept leads to QR A, not books scanned at A");
                Capture(ui, canvas, camera, "02-qr-a");
                Click(ui, "Demo scan invalid"); Check(ui.Step == PickupPreviewStep.AlignA && ui.Error != null, "Wrong QR retains A scan step"); Capture(ui, canvas, camera, "03-wrong-qr");
                Click(ui, "Demo scan valid"); Check(ui.Step == PickupPreviewStep.Aligned, "Correct QR A shows success"); Capture(ui, canvas, camera, "04-a-success");
                Click(ui, "Open AR"); Capture(ui, canvas, camera, "05-guidance-b");
                Click(ui, "Begin pickup"); Capture(ui, canvas, camera, "06-qr-b");
                Click(ui, "Demo scan valid"); Check(ui.Step == PickupPreviewStep.LocationAccepted, "QR B accepted before barcode"); Capture(ui, canvas, camera, "07-b-qr-success");
                Click(ui, "Continue barcode"); Capture(ui, canvas, camera, "08-barcode-a1");
                ui.Scan("4714551570603"); Check(ui.Step == PickupPreviewStep.Barcode && !ui.PickedB && ui.Error != null, "A2 barcode rejected while collecting A1");
                Click(ui, "Demo scan valid"); Check(!ui.PickedB, "Success popup does not prematurely complete pickup"); Capture(ui, canvas, camera, "09-a1-success");
                Click(ui, "Confirm book"); Check(ui.PickedB && !ui.PickedC && ui.Stop == "C", "Confirm A1 selects C and keeps B complete");
                Check(!ui.GetComponentsInChildren<Button>().Single(b => b.name == "Go B").interactable, "Completed B disabled"); Capture(ui, canvas, camera, "10-guidance-c");
                Click(ui, "Begin pickup"); Capture(ui, canvas, camera, "11-qr-c"); Click(ui, "Demo scan valid"); Click(ui, "Continue barcode"); Capture(ui, canvas, camera, "12-barcode-a2"); Click(ui, "Demo scan valid"); Capture(ui, canvas, camera, "13-a2-success"); Click(ui, "Confirm book");
                Check(ui.PickedB && ui.PickedC && ui.Step == PickupPreviewStep.ReturnA, "Both pickups lead to return A"); Capture(ui, canvas, camera, "14-return-a");
                Click(ui, "Confirm return"); Capture(ui, canvas, camera, "15-return-qr-a"); Click(ui, "Demo scan valid"); Check(ui.Step == PickupPreviewStep.Complete, "Complete only after return QR A"); Capture(ui, canvas, camera, "16-complete");
                ui.ResetDemo(); Click(ui, "Accept mission"); Click(ui, "Demo scan valid"); Click(ui, "Open AR"); Click(ui, "Go C"); Click(ui, "Begin pickup"); Click(ui, "Demo scan valid"); Click(ui, "Continue barcode"); Click(ui, "Demo scan valid"); Click(ui, "Confirm book");
                Check(ui.PickedC && !ui.PickedB && ui.Stop == "B", "C-first route leads to remaining B");
                Click(ui, "Begin pickup"); Click(ui, "Demo scan valid"); Click(ui, "Continue barcode"); Click(ui, "Demo scan valid"); Click(ui, "Confirm book"); Check(ui.Step == PickupPreviewStep.ReturnA, "C-first then B supported");
                ui.ResetDemo(); Click(ui, "Accept mission"); Click(ui, "Refresh camera"); Check(ui.Error != null && ui.cameraRestart.Phase == CameraRestartPhase.Idle, "Unconnected demo camera never reports fake refresh success");
                ui.ShowCameraPhase(CameraRestartPhase.Stopping); Capture(ui, canvas, camera, "17-camera-stopping");
                ui.ShowCameraPhase(CameraRestartPhase.Starting); Capture(ui, canvas, camera, "18-camera-starting");
                ui.ShowCameraPhase(CameraRestartPhase.WaitingForFrame); Capture(ui, canvas, camera, "19-camera-waiting");
                ui.ShowCameraPhase(CameraRestartPhase.Idle); ui.ResetDemo();
                foreach (var entry in before) Check(File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value), "Production unchanged: " + entry.Key);
                results.Add("LIMIT: UI simulation only. Scan buttons feed explicit demo identities. No API, inventory writes, production mission or A/B/C changes. Camera sequence fake-feed checks do not prove Samsung restart, optical focus or AR tracking continuity.");
                File.WriteAllLines(Out + "/RESULT.txt", new[] { "PASS · " + DateTime.UtcNow.ToString("O") }.Concat(results));
                Debug.Log("[PickupPreview] PASS isolated scene, 38 fresh renders, pickup flow and camera restart candidate");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllLines(Out + "/RESULT.txt", results.Concat(new[] { "FAIL " + e })); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static void Capture(PickupFlowPreview ui, Canvas canvas, Camera camera, string name)
        {
            foreach (int h in new[] { 2400, 1920 })
            {
                var rt = new RenderTexture(1080, h, 24); rt.Create(); var prev = RenderTexture.active;
                var texture = new Texture2D(1080, h, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = rt; canvas.scaleFactor = 1080f / 412; ui.Build(h * 412f / 1080);
                    Canvas.ForceUpdateCanvases();
                    foreach (var surface in ui.GetComponentsInChildren<UniformUiSurface>()) typeof(UniformUiSurface).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(surface, null);
                    Canvas.ForceUpdateCanvases();
                    foreach (var t in ui.GetComponentsInChildren<TMP_Text>())
                    {
                        t.ForceMeshUpdate(); var desired = t.GetPreferredValues(t.text, t.rectTransform.rect.width, 10000);
                        Check(desired.y <= t.rectTransform.rect.height + 2, name + " @" + h + " text bounds: " + t.text.Replace('\n', ' '));
                    }
                    camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1080, h), 0, 0); texture.Apply(); File.WriteAllBytes(Out + "/" + name + "-" + h + ".png", texture.EncodeToPNG());
                }
                finally { RenderTexture.active = prev; camera.targetTexture = null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
        sealed class Feed : IPreviewCameraFeed
        {
            public readonly List<string> calls = new List<string>(); public bool on = true; public int count; public bool focus;
            public bool Enabled { get => on; set { on = value; calls.Add(value ? "on" : "off"); } }
            public bool AutoFocus { get => focus; set { focus = value; calls.Add("focus"); } }
            public int FrameCount => count;
        }
        static void CameraSequenceChecks()
        {
            var f = new Feed(); float now = 0; CameraRestartPhase phase = CameraRestartPhase.Idle;
            var e = PreviewCameraRestartSequence.Run(f, () => now, p => phase = p);
            Check(e.MoveNext() && !f.on && phase == CameraRestartPhase.Stopping, "Camera restart actually disables feed first");
            Check(e.MoveNext() && f.on && f.focus && phase == CameraRestartPhase.WaitingForFrame, "Camera restarts feed and enables autofocus");
            f.count++; Check(!e.MoveNext() && phase == CameraRestartPhase.Ready, "Ready requires a new camera frame after restart");
            Check(f.calls.Take(3).SequenceEqual(new[] { "off", "on", "focus" }), "Camera calls ordered off -> on -> autofocus");
            f = new Feed(); now = 0; e = PreviewCameraRestartSequence.Run(f, () => now, p => phase = p); e.MoveNext(); e.MoveNext(); now = 7;
            Check(!e.MoveNext() && phase == CameraRestartPhase.Failed && f.on, "No fresh frame times out; camera remains enabled");
            f = new Feed(); e = PreviewCameraRestartSequence.Run(f, () => 0, p => phase = p); e.MoveNext(); (e as IDisposable).Dispose(); Check(f.on, "Cancel during camera-off restores camera");
            f = new Feed { on = false }; e = PreviewCameraRestartSequence.Run(f, () => 0, p => phase = p); Check(!e.MoveNext() && !f.on && phase == CameraRestartPhase.Failed, "Disabled camera is not force-enabled by a refresh request");
        }
    }
}
#endif
