#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Immersal;
using Immersal.XR;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using ZXing;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoompresentFullAppBuild
    {
        const string Request = "Library/RoompresentFullApp.request";
        public const string Output = "Builds/Android/JarVis-Warehouse-0.2.9.apk";
        const string Logs = "Logs/RoompresentFullApp/";
        const string Main = "Assets/Scenes/JarVisFigmaApp.unity";
        static bool busy;
        static RoompresentFullAppBuild() { EditorApplication.update += Tick; EditorApplication.delayCall += () => Debug.Log("[RoompresentFullApp] VERSION 1 ready"); }
        static void Tick()
        {
            if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
            string command = File.ReadAllText(Request).Trim(); File.Move(Request, Request + ".running-" + DateTime.UtcNow.Ticks); busy = true;
            try { if (command == "prepare") Prepare(); else if (command == "build") Build(); else throw new InvalidOperationException("Unknown full-app command."); }
            catch (Exception e) { Directory.CreateDirectory(Logs); File.WriteAllText(Logs + "failure.txt", e.ToString()); Debug.LogError("[RoompresentFullApp] " + e); }
            finally { busy = false; }
        }
        [MenuItem("Tools/JarVis/Immersal/09 Prepare Full JarVis and Book Mission")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Compile and stop Play before preparing.");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save the active scene first; existing edits were not replaced.");
            Directory.CreateDirectory(Logs);
            var scene = EditorSceneManager.OpenScene(ImmersalVideoSetup.ScenePath, OpenSceneMode.Single);
            var venue = UnityEngine.Object.FindFirstObjectByType<RoompresentVenue>();
            if (venue == null || venue.targets == null) throw new InvalidOperationException("Roompresent venue/targets missing.");
            venue.alignmentVerified = false;
            var session = UnityEngine.Object.FindFirstObjectByType<ImmersalSession>();
            session.AutoStart = true; session.BurstMode = false; session.SessionUpdateInterval = 1.5f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(session);
            RoompresentSceneBuilder.SaveWithoutCredential();
            var required = new[] { Main, "Assets/Scenes/SplashScene.unity", "Assets/Scenes/LoginScene.unity", "Assets/Scenes/WarehouseScene.unity", "Assets/Scenes/AtlasAR.unity", ImmersalVideoSetup.ScenePath };
            foreach (string path in required) if (!File.Exists(path)) throw new InvalidOperationException("Required app scene missing: " + path);
            var extra = EditorBuildSettings.scenes.Where(s => s.enabled && !required.Contains(s.path)).ToArray();
            if (extra.Length != 0) throw new InvalidOperationException("Review additional enabled scenes before replacing build list.");
            EditorBuildSettings.scenes = required.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
            VerifyCore(); VerifyActualBookPhotos(); WriteQr(); VerifyMovedNavMesh(venue);
            AndroidProjectConfigurator.ApplySettings();
            JarvisProjectValidator.Validate();
            File.WriteAllText(Logs + "prepared.txt", "PASS full JarVis entry and six app scenes; book mission guards; actual uploaded barcode images; QR roundtrip; rigid map fit and moved NavMesh paths.\nPhysical camera/tracking/voice/network acceptance still requires device testing.\nUTC=" + DateTime.UtcNow.ToString("O"));
            Debug.Log("[RoompresentFullApp] PREPARED full JarVis 0.2.9 with two-book delivery.");
        }
        static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void VerifyCore()
        {
            Check(RoompresentDeliveryAR.IsStartQr("JWM:MAIN:E") && RoompresentDeliveryAR.IsStartQr("JWM:ROOMPRESENT:153613:A"), "Current and legacy start A QR not recognized");
            foreach (string value in new[] { "JWM:MAIN:A", "JWM:MAIN:B", "JWM:ROOMPRESENT:153613:C", RoompresentDeliveryRecord.A1, RoompresentDeliveryRecord.A2, null })
                Check(!RoompresentDeliveryAR.IsStartQr(value), "Wrong marker accepted as fixed start A");
            var m = new RoompresentDeliveryRecord();
            Check(!m.ScanProduct(RoompresentDeliveryRecord.A1), "Unaccepted product scan accepted"); m.accepted = true;
            Check(!m.ScanProduct("4710043004342"), "Milk must not verify a book");
            Check(m.ScanProduct(RoompresentDeliveryRecord.A1) && !m.Loaded, "A1 alone must not unlock routing");
            Check(m.ScanProduct(RoompresentDeliveryRecord.A1) && !m.Loaded, "Duplicate A1 counted as A2");
            Check(!m.Confirm("JWM:ROOMPRESENT:153613:B", "B", true, .2f), "Delivery before both books scanned");
            Check(m.ScanProduct(RoompresentDeliveryRecord.A2) && m.Loaded, "Both book scans required");
            Check(!m.Confirm("JWM:ROOMPRESENT:153613:C", "B", true, .2f), "Wrong destination accepted");
            Check(!m.Confirm("JWM:ROOMPRESENT:153613:B", "B", false, .2f), "Lost tracking accepted");
            foreach (float distance in new[] { float.NaN, float.PositiveInfinity, -1f, 1.21f }) Check(!m.Confirm("JWM:ROOMPRESENT:153613:B", "B", true, distance), "Invalid distance accepted");
            Check(m.Confirm("JWM:ROOMPRESENT:153613:B", "B", true, .2f) && !m.Complete, "B stage failed");
            Check(!m.Confirm("JWM:ROOMPRESENT:153613:B", "B", true, .2f), "Duplicate B accepted");
            Check(m.Confirm("JWM:ROOMPRESENT:153613:C", "C", true, .2f) && m.Complete, "C stage failed");
            var reversed = new RoompresentDeliveryRecord { accepted = true, scannedA1 = true, scannedA2 = true, route = "C" };
            Check(reversed.Confirm("JWM:MAIN:C", "C", true, .4f) && !reversed.Complete && reversed.Confirm("JWM:MAIN:B", "B", true, .4f), "C-first and existing QR aliases failed");
            Vector3[] s = { new Vector3(1.6321806f,.8264267f,-3.2412338f), new Vector3(-2.0507436f,.8546972f,1.8392259f), new Vector3(-.7009451f,.6700959f,2.8860228f) };
            var rotation = Quaternion.Euler(0, 37, 0); var offset = new Vector3(3, -1.3f, 2);
            Check(RoompresentDeliveryAR.TryFit(s, s.Select(p => rotation * p + offset).ToArray(), out var t, out var yaw, out var residual) && residual < .001f && (t-offset).magnitude < .001f && Mathf.Abs(Mathf.DeltaAngle(yaw,37)) < .001f, "Rigid registration failed");
            var wrong = s.Select(p => rotation*p+offset).ToArray(); wrong[2] += new Vector3(1,1,1);
            Check(!RoompresentDeliveryAR.TryFit(s, wrong, out _, out _, out _), "Bad registration accepted");
            File.WriteAllText(Logs + "mission-core.txt", "PASS guards, both delivery orders, existing checkpoint aliases, rigid registration, rejection of inconsistent landmarks.\nNo live stock mutation or provider request.");
        }
        static void VerifyActualBookPhotos()
        {
            string dir = "C:/Users/Windows/AppData/Local/Temp/";
            var photos = new[] { ("codex-clipboard-bb401bec-2b87-4198-9835-08959fb23516.png", RoompresentDeliveryRecord.A1), ("codex-clipboard-eb65edc0-483c-4dc4-bbfc-9deb6817e6aa.png", RoompresentDeliveryRecord.A2) };
            var report = new List<string>();
            foreach (var photo in photos)
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    Check(texture.LoadImage(File.ReadAllBytes(dir + photo.Item1)), "Cannot read uploaded book image");
                    var pixels = texture.GetPixels32(); Result result = null; string method = null;
                    // The supplied photographs include a large blank page above the label.
                    // Try the lower half at native resolution as well as the full image.
                    foreach (int divisor in new[] { 2, 1 })
                    {
                        int inputHeight = divisor == 1 ? texture.height / 2 : texture.height;
                        int w = texture.width / divisor, h = inputHeight / divisor; var down = new Color32[w*h];
                        for(int y=0;y<h;y++)for(int x=0;x<w;x++)down[y*w+x]=pixels[y*divisor*texture.width+x*divisor];
                        result = ImportedCameraScanner.Decode(down,w,h,true); method = divisor == 1 ? "native-resolution lower-half crop" : "full frame half resolution";
                        if(result != null)break;
                    }
                    Check(result != null && result.Text == photo.Item2 && result.BarcodeFormat == BarcodeFormat.EAN_13, "Actual book photo barcode not decoded: " + photo.Item1);
                    report.Add("PASS uploaded photo EAN-13 " + result.Text + " · " + method);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            File.WriteAllLines(Logs + "book-photo-decoder.txt", report);
        }
        static void WriteQr()
        {
            const string dir = "Docs/RoompresentQR/"; Directory.CreateDirectory(dir);
            foreach (string id in new[] { "A", "B", "C" })
            {
                string payload = "JWM:ROOMPRESENT:153613:" + id;
                var matrix = new MultiFormatWriter().encode(payload, BarcodeFormat.QR_CODE, 800, 800);
                var texture = new Texture2D(800, 800, TextureFormat.RGB24, false); var pixels = new Color32[800*800];
                for(int y=0;y<800;y++)for(int x=0;x<800;x++)pixels[y*800+x]=matrix[x,799-y]?new Color32(0,0,0,255):new Color32(255,255,255,255);
                texture.SetPixels32(pixels); texture.Apply();
                Check(ImportedCameraScanner.Decode(pixels,800,800)?.Text==payload, "Destination QR roundtrip failed");
                File.WriteAllBytes(dir + "Roompresent-" + id + ".png", texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            }
        }
        static void VerifyMovedNavMesh(RoompresentVenue venue)
        {
            var surface = venue.GetComponentInChildren<NavMeshSurface>(true); var root = venue.transform; var savedPosition = root.localPosition; var savedRotation = root.localRotation;
            try
            {
                root.localPosition = new Vector3(3.2f,.3f,-1.7f); root.localRotation = Quaternion.Euler(0,37,0); surface.RemoveData(); surface.AddData();
                var points = venue.targets.GetComponentsInChildren<Immersal.Samples.Navigation.IsNavigationTarget>(true).ToDictionary(t => t.targetName,t => t.transform.position);
                var rows = new List<string>();
                foreach(string a in new[]{"A","B","C"})foreach(string b in new[]{"A","B","C"})
                {
                    if(a==b)continue; var p = new NavMeshPath();
                    Check(NavMesh.SamplePosition(points[a],out var x,1.5f,NavMesh.AllAreas) && NavMesh.SamplePosition(points[b],out var y,1.5f,NavMesh.AllAreas) && NavMesh.CalculatePath(x.position,y.position,NavMesh.AllAreas,p) && p.status==NavMeshPathStatus.PathComplete,"Moved NavMesh disconnected: "+a+" -> "+b);
                    rows.Add("PASS rotated/translated floor " + a + " -> " + b + " corners=" + p.corners.Length);
                }
                File.WriteAllLines(Logs+"moved-navmesh.txt",rows);
            }
            finally { root.localPosition=savedPosition;root.localRotation=savedRotation;surface.RemoveData();surface.AddData(); }
        }
        [MenuItem("Tools/JarVis/Immersal/10 Build Full JarVis APK")]
        public static void Build()
        {
            Prepare();
            Check(EditorBuildSettings.scenes.First(s=>s.enabled).path==Main, "Full app entry must be JarVis");
            Directory.CreateDirectory(Path.GetDirectoryName(Output)); EditorUserBuildSettings.buildAppBundle=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(), target=BuildTarget.Android, targetGroup=BuildTargetGroup.Android, locationPathName=Path.GetFullPath(Output), options=BuildOptions.StrictMode|BuildOptions.CompressWithLz4HC });
            File.WriteAllText(Logs+"build.txt", "result="+report.summary.result+"\nbytes="+report.summary.totalSize+"\nerrors="+report.summary.totalErrors+"\nseconds="+report.summary.totalTime.TotalSeconds+"\nFull JarVis application; device acceptance pending.\n");
            Check(report.summary.result==BuildResult.Succeeded && File.Exists(Output), "Full JarVis Android build failed");
            Debug.Log("[RoompresentFullApp] APK READY " + Path.GetFullPath(Output));
        }
    }
    public sealed class RoompresentBuildCredential : IProcessSceneWithReport
    {
        public int callbackOrder => 800;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if(report==null || scene.path!=ImmersalVideoSetup.ScenePath)return;
            const string file="Library/ImmersalVideoSetup/UserToken.local";
            if(!File.Exists(file))throw new BuildFailedException("Local Immersal configuration missing; sign in inside the Editor.");
            string token=File.ReadAllText(file).Trim();
            if(token.Length<20 || token.Contains("\n"))throw new BuildFailedException("Local Immersal configuration is invalid.");
            // Build copy only: no credential is saved into scene source or logs.
            foreach(var root in scene.GetRootGameObjects())foreach(var sdk in root.GetComponentsInChildren<ImmersalSDK>(true))sdk.developerToken=token;
        }
    }
}
#endif
