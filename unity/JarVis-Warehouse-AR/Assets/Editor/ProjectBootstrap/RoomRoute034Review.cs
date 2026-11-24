using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class RoomRoute034Review
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool pass,string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor busy");
            Checks.Clear();
            Directory.CreateDirectory("Logs/RoomRoutes034");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                GameObject root = new GameObject("Routes 034 focused QA");
                SceneManager.MoveGameObjectToScene(root,scene);
                Camera camera = new GameObject("QA camera",typeof(Camera)).GetComponent<Camera>();
                camera.transform.SetParent(root.transform,false);
                camera.transform.position = new Vector3(0,1.3f,0);
                camera.fieldOfView = 60;
                foreach (float aspect in new[] { 412f / 915,412f / 732 })
                foreach (float pitch in new[] { 0f,20,35,55,75 })
                foreach (float yaw in new[] { -75f,0,75 })
                foreach (float depth in new[] { 1.2f,2.2f,4.5f,6 })
                {
                    camera.aspect = aspect;
                    camera.transform.rotation = Quaternion.Euler(pitch,0,0);
                    Vector3 position = new Vector3(0,0.025f,depth);
                    Quaternion rotation = Quaternion.Euler(0,yaw,0);
                    float scale = RoomRoutePresentation.FitArrow(camera,position,rotation,0.38f,out Rect projected);
                    Check(scale > 0 && scale <= 0.38f && projected.width <= 0.1401f && projected.height <= 0.1201f,"Full projected chevron including motion fits screen pitch=" + pitch + " yaw=" + yaw + " depth=" + depth + " aspect=" + aspect);
                }
                Check(!RoomRoutePresentation.InGuidanceWindow(new Rect(0.4f,0.1f,0.1f,0.08f)),"Arrows cannot overlap action card");
                Check(!RoomRoutePresentation.InGuidanceWindow(new Rect(0.4f,0.82f,0.1f,0.08f)),"Arrows cannot overlap header");
                Check(!RoomRoutePresentation.Separated(new Rect(0.4f,0.5f,0.1f,0.08f),new Rect(0.45f,0.52f,0.1f,0.08f)),"Overlapping glyphs excluded");
                Check(RoomRoutePresentation.Separated(new Rect(0.4f,0.5f,0.1f,0.08f),new Rect(0.4f,0.7f,0.1f,0.08f)),"Spaced glyphs accepted");

                RoomEnvironmentReadiness environment = new RoomEnvironmentReadiness();
                environment.Begin(); environment.Observe(Vector3.zero,Quaternion.identity,0,false);
                Check(!environment.WaitingTooLong(7.9) && environment.WaitingTooLong(8),"Eight-second help threshold is not fake completion");
                Check(!environment.Ready && environment.Progress == 0,"Elapsed time alone never completes scan");
                RoomRouteReadinessReview.Prepared(environment);
                Check(environment.Ready && !environment.WaitingTooLong(20),"Successful readiness does not retain timeout warning");

                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                typeof(WalkingRouteRecorder).GetProperty("Ready").SetValue(recorder,true);
                recorder.BeginRecording();
                typeof(WalkingRouteRecorder).GetMethod("HandleQr",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recorder,new object[] { RoomRouteAlignment.StartQr,0d });
                Check(recorder.ScanningStart && !recorder.Route.Active && !recorder.Environment.Ready,"Early correct QR cannot bypass environment or pose lock");

                foreach (string scenario in new[] { "straight-5.5m","corner-5.8m","loop-12m" })
                {
                    WalkingRouteData data = RoomRouteReview.Fixture();
                    data.points = scenario == "straight-5.5m" ? new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 5.5f } } : scenario == "corner-5.8m" ? new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 2.8f },new RoutePoint { x = 3,z = 2.8f } } : new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 3 },new RoutePoint { x = 3,z = 3 },new RoutePoint { x = 3 },new RoutePoint() };
                    RoutePoint end = data.points[data.points.Count - 1];
                    data.destinationInRoute = new Vector3(end.x,0,end.z);
                    WalkingRouteFollower follower = new WalkingRouteFollower(); follower.Prepare(data);
                    Check(follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true),"Fixture aligns " + scenario);
                    WalkingRouteLiveView live = new GameObject("QA live view").AddComponent<WalkingRouteLiveView>();
                    live.transform.SetParent(root.transform,false); live.BuildVisuals(follower,0);
                    Transform visual = live.transform.Find("World route - manually aligned, estimated floor");
                    foreach (int height in new[] { 915,732 })
                    foreach (float pitch in new[] { 20f,35,55 })
                    {
                        RenderTexture target = new RenderTexture(412,height,24);
                        RenderTexture previous = RenderTexture.active;
                        try
                        {
                            camera.targetTexture = target; camera.aspect = 412f / height;
                            camera.transform.rotation = Quaternion.Euler(pitch,0,0);
                            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.12f,0.17f,0.23f);
                            live.UpdatePresentation(follower,camera,true);
                            int count = 0;
                            foreach (Transform arrow in visual)
                                if (arrow.name.StartsWith("AR chevron") && arrow.gameObject.activeSelf)
                                {
                                    count++;
                                    Check(Vector3.Angle(arrow.forward,Vector3.forward) < 1,"No conflicting future-branch arrow " + scenario);
                                    Check(RoomRoutePresentation.ProjectArrow(camera,arrow.position,arrow.rotation,arrow.localScale.x,out Rect bounds) && RoomRoutePresentation.InGuidanceWindow(bounds),"Entire moving arrow stays in guidance window " + scenario);
                                }
                            Check(count <= RoomRoutePresentation.Budget(follower.Remaining),"Live arrow count respects actual remaining distance " + scenario);
                            Check(count == live.VisibleArrowCount,"Visible count agrees with actual objects " + scenario);
                            if (count == 0) Check(!string.IsNullOrEmpty(live.ViewHint),"Offscreen floor gives actionable camera hint " + scenario);
                            if (pitch == 35) Check(count >= 1,"Useful arrow remains visible looking down route " + scenario);
                            live.AnimateGlyphs(0.45f,false); camera.Render(); RenderTexture.active = target;
                            Texture2D screenshot = new Texture2D(412,height,TextureFormat.RGB24,false);
                            screenshot.ReadPixels(new Rect(0,0,412,height),0,0); screenshot.Apply();
                            File.WriteAllBytes("Logs/RoomRoutes034/" + scenario + "-" + height + "-" + pitch + ".png",screenshot.EncodeToPNG());
                            UnityEngine.Object.DestroyImmediate(screenshot);
                        }
                        finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                    }
                    UnityEngine.Object.DestroyImmediate(live.gameObject);
                }
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
            File.WriteAllLines("Logs/RoomRoutes034/focused.txt",Checks);
        }
    }
}
