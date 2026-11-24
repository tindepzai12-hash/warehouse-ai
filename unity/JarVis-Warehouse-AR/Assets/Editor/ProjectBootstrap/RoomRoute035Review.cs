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
    public static class RoomRoute035Review
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool pass,string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        private static void Set(object owner,string property,object value) => owner.GetType().GetProperty(property).SetValue(owner,value);
        private static void Invoke(object owner,string method,params object[] arguments) => owner.GetType().GetMethod(method,BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner,arguments);

        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor busy");
            Checks.Clear(); Directory.CreateDirectory("Logs/RoomRoutes035");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                GameObject root = new GameObject("Routes 035 regression fixtures");
                SceneManager.MoveGameObjectToScene(root,scene);
                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                Set(recorder,"Ready",true); recorder.BeginRecording();
                RoomRouteReadinessReview.Prepared(recorder.Environment);
                Check(!recorder.PreparingStart,"Real observation moves from preparation to A scanner");
                for (int step = 33; step <= 240; step++)
                {
                    double now = step * 0.05;
                    recorder.Environment.Frame(now,step * 50000000L);
                    recorder.Environment.Features(0,0,now);
                    recorder.Environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.Euler(0,19.2f,0),now,true);
                    Check(!recorder.PreparingStart && recorder.Environment.Progress == 1,"Low-feature QR view does not restart completed preparation frame=" + step);
                }
                Invoke(recorder,"HandleQr",RoomRouteAlignment.StartQr,12d);
                Check(!recorder.PreparingStart && recorder.ScanningStart && !recorder.Route.Active,"QR still needs measured marker pose; no forced alignment");
                recorder.Environment.Frame(12.05,12050000000L);
                recorder.Environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.Euler(0,19.2f,0),12.05,false);
                Check(recorder.PreparingStart && !recorder.Environment.Ready,"Actual tracking loss revokes preparation latch");
                recorder.Environment.Frame(12.1,12100000000L);
                recorder.Environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.Euler(0,19.2f,0),12.1,true);
                Check(recorder.PreparingStart,"One recovered frame cannot bypass real re-observation");
                recorder.Cancel();
                RoomEnvironmentReadiness environment = new RoomEnvironmentReadiness();
                for (int step = 0; step <= 240; step++)
                {
                    double now = step * 0.05;
                    environment.Frame(now,step * 50000000L);
                    environment.Features(0,0,now);
                    environment.Observe(new Vector3(step * 0.003f,1.3f,0),Quaternion.Euler(0,step * 0.6f,0),now,true);
                }
                Check(!environment.Ready && environment.Progress < 1,"Latch never accepts observation with no real features in the first place");

                Camera camera = new GameObject("Moving fixture camera",typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform,false);
                camera.fieldOfView = 60; camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f,0.17f,0.23f);
                foreach (int side in new[] { -1,1 })
                foreach (float approach in new[] { 0f,0.4f,1,1.6f,2.1f,2.5f })
                {
                    WalkingRouteData data = RoomRouteReview.Fixture();
                    data.points = new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 2.8f },new RoutePoint { x = side * 3,z = 2.8f } };
                    data.destinationInRoute = new Vector3(side * 3,0,2.8f);
                    typeof(WalkingRouteRecorder).GetField("recovered",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(recorder,data);
                    recorder.NavigateSaved();
                    WalkingRouteFollower follower = recorder.Follower;
                    Check(follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true),"Corner fixture aligns");
                    Set(recorder,"GuidanceStarted",true);
                    for (int step = 1; step <= Mathf.RoundToInt(approach * 10); step++) follower.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
                    Check(follower.ShowArrow && !follower.RouteCaution,"Normal corner approach retains safe guidance " + approach);
                    Check(RoomRoutePresentation.Arrow(follower,0,0.025f,out Vector3 position,out Vector3 direction,out float distance),"Corner approach always has a route-based candidate " + approach);
                    Check(Mathf.Abs(position.x) < 0.001f && position.z <= 2.801f && distance >= follower.TravelledMetres,"Candidate never jumps to a future branch or shortcut");
                    bool turning = 2.8f - approach <= 1.201f;
                    Check(Vector3.Angle(direction,turning ? Vector3.right * side : Vector3.forward) < 1,"Cue changes to actual next leg only near recorded corner");
                    Check(Mathf.Abs(RoomRoutePresentation.CueAngle(follower) - (turning ? side * 90 : 0)) < 1,"Onscreen arrow conveys actual left/right maneuver");
                    if (turning)
                    {
                        Check(RoomRoutePresentation.Instruction(follower).Contains(side > 0 ? "phải" : "trái"),"Instruction agrees with turn arrow");
                        follower.Observe(new Vector3(0,1.3f,approach),side * 90,approach + 0.05,true);
                        Check(Mathf.Abs(RoomRoutePresentation.CueAngle(follower) - side * 90) < 1 && RoomRoutePresentation.CameraHint(follower) == "","Looking around a real corner does not flip instruction back towards previous leg");
                        follower.Observe(new Vector3(0,1.3f,approach),0,approach + 0.1,true);
                    }
                    WalkingRouteLiveView live = new GameObject("Corner live view").AddComponent<WalkingRouteLiveView>();
                    live.transform.SetParent(root.transform,false); live.BuildVisuals(follower,0);
                    Set(recorder,"LiveView",live);
                    WalkingRouteRecorderUi ui = root.AddComponent<WalkingRouteRecorderUi>(); ui.Recorder = recorder; Invoke(ui,"Start");
                    foreach (int height in new[] { 915,732 })
                    foreach (float pitch in new[] { 35f,55 })
                    {
                        RenderTexture target = new RenderTexture(412,height,24);
                        RenderTexture previous = RenderTexture.active;
                        try
                        {
                            camera.targetTexture = target; camera.aspect = 412f / height;
                            camera.transform.position = new Vector3(0,1.3f,approach); camera.transform.rotation = Quaternion.Euler(pitch,0,0);
                            live.UpdatePresentation(follower,camera,true);
                            Canvas canvas = root.transform.Find("Room route HUD").GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
                            Invoke(ui,"Update"); Invoke(ui,"UpdateDirection",follower,true);
                            Text status = canvas.transform.Find("Safe area/Header").GetComponentsInChildren<Text>()[1]; status.text = "Mô phỏng Editor · v0.3.5";
                            Canvas.ForceUpdateCanvases();
                            RoomRouteDirectionGlyph glyph = ui.GetComponentInChildren<RoomRouteDirectionGlyph>();
                            Check(glyph != null && Mathf.Abs(glyph.Degrees - RoomRoutePresentation.CueAngle(follower)) < 1,"Visible footer cue survives offscreen/too-close AR at " + approach);
                            foreach (Text text in canvas.GetComponentsInChildren<Text>())
                            {
                                float needed = text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size)) / text.pixelsPerUnit;
                                Check(needed <= text.rectTransform.rect.height + 1,"Corner HUD text fits " + height + " " + text.text);
                            }
                            Check(live.VisibleArrowCount <= 1,"Short-room corner never floods camera with arrows");
                            if (pitch == 35 && approach >= 0.4f && approach <= 1) Check(live.VisibleArrowCount == 1,"Previously blank world-grid gap now has a visible arrow");
                            if (pitch == 55 && approach >= 1.6f && approach <= 2.1f) Check(live.VisibleArrowCount == 1,"Looking at actual corner shows a real AR turn cue, not just screen fallback");
                            if (live.VisibleArrowCount == 0) Check(!string.IsNullOrEmpty(live.ViewHint),"Hidden world arrow explains why while footer cue remains visible");
                            live.AnimateGlyphs(0.45f,false); camera.Render(); RenderTexture.active = target;
                            Texture2D capture = new Texture2D(412,height,TextureFormat.RGB24,false); capture.ReadPixels(new Rect(0,0,412,height),0,0); capture.Apply();
                            File.WriteAllBytes("Logs/RoomRoutes035/corner-" + side + "-at-" + approach.ToString("F1",System.Globalization.CultureInfo.InvariantCulture) + "-" + height + (pitch == 35 ? "" : "-55") + ".png",capture.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(capture);
                            Invoke(ui,"UpdateDirection",follower,false);
                            Check(!glyph.gameObject.activeSelf,"Tracking/caution gate hides footer cue too");
                        }
                        finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                    }
                    double after = approach + 0.2;
                    for (int step = Mathf.RoundToInt(approach * 10) + 1; step <= 28; step++) { after += 0.1; follower.Observe(new Vector3(0,1.3f,step * 0.1f),0,after,true); }
                    for (int step = 1; step <= 4; step++) { after += 0.1; follower.Observe(new Vector3(side * step * 0.1f,1.3f,2.8f),side * 90,after,true); }
                    Check(follower.Index == 2 && follower.ShowArrow && !follower.RouteCaution,"Physically traversing corner advances to adjacent leg without losing guidance");
                    Check(RoomRoutePresentation.Arrow(follower,0,0,out Vector3 afterCorner,out Vector3 afterDirection,out _) && Mathf.Abs(afterCorner.z - 2.8f) < 0.001f && Vector3.Angle(afterDirection,Vector3.right * side) < 1,"After turn the world cue follows recorded next leg, not previous corner");
                    Check(Mathf.Abs(RoomRoutePresentation.CueAngle(follower)) < 1,"Screen instruction returns to straight after completing actual turn");
                    UnityEngine.Object.DestroyImmediate(ui.transform.Find("Room route HUD").gameObject);
                    UnityEngine.Object.DestroyImmediate(ui); recorder.Cancel(); Set(recorder,"LiveView",null); UnityEngine.Object.DestroyImmediate(live.gameObject);
                }
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
            File.WriteAllLines("Logs/RoomRoutes035/focused.txt",Checks);
        }
    }
}
