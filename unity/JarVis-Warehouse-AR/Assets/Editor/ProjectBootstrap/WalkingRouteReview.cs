using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using JarVis.Warehouse.RouteRecording;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using ZXing;

namespace ProjectBootstrap
{
    public static class WalkingRouteReview
    {
        private static readonly List<string> Passed = new List<string>();
        [MenuItem("Tools/JarVis/Routes/Test Recorder Offline")]
        public static void Run()
        {
            Passed.Clear();
            string folder = Path.GetFullPath("Logs/WalkingRoutes");
            Directory.CreateDirectory(folder);
            WalkingRouteBuild.EnsureScene();
            Scene routeScene = EditorSceneManager.OpenScene(WalkingRouteBuild.ScenePath, OpenSceneMode.Additive);
            try
            {
                int drivers = 0;
                foreach (GameObject root in routeScene.GetRootGameObjects())
                    foreach (TrackedPoseDriver driver in root.GetComponentsInChildren<TrackedPoseDriver>(true))
                    {
                        drivers++;
                        Check(driver.positionInput.action.bindings.Count == 1 && driver.positionInput.action.bindings[0].path == "<HandheldARInputDevice>/devicePosition", "ARCore handheld position binding");
                        Check(driver.rotationInput.action.bindings.Count == 1 && driver.rotationInput.action.bindings[0].path == "<HandheldARInputDevice>/deviceRotation", "ARCore handheld rotation binding");
                        Check(driver.trackingStateInput.action.bindings.Count == 0, "no unsupported handheld tracking-state binding");
                    }
                Check(drivers == 1, "exactly one AR camera pose driver");
            }
            finally { EditorSceneManager.CloseScene(routeScene, true); }
            foreach (string invalid in new[] { "", "https://example.org", "JWM:MAIN:", "JWM:MAIN:A:B", "JWM:MAIN:../A", "JWM:MAIN:A\n", "JWM: MAIN:A", "JWM:MAIN:🎉", "jwm:MAIN:A" }) Check(!WalkingRoute.TryQr(invalid, out _), "reject invalid QR " + invalid.Replace("\n", "newline"));
            Check(WalkingRoute.TryQr("JWM:MAIN:A", out string warehouse) && warehouse == "MAIN", "valid QR A");
            Check(WalkingRoute.TryQr("JWM:AREA-2:A.01", out _), "warehouse/marker identifiers");
            Check(!WalkingRoute.TryQr("JWM:MAIN:" + new string('A', 65), out _), "marker length bound");
            WalkingRoute route = new WalkingRoute();
            Check(!route.Begin("JWM:MAIN:A", Vector3.zero, 0, 0, false), "start requires tracking");
            Check(!route.Begin("bad", Vector3.zero, 0, 0, true), "start requires marker");
            Check(!route.Begin("JWM:MAIN:A", new Vector3(float.NaN, 0, 0), 0, 0, true), "reject NaN pose");
            Check(route.Begin("JWM:MAIN:A", new Vector3(10, 2, 20), 90, 10, true), "start valid route");
            Check(!route.Begin("JWM:MAIN:B", Vector3.zero, 0, 10, true), "cannot overwrite active route");
            for (int index = 1; index <= 100; index++) Check(route.Observe(new Vector3(10 + index * 0.1f, 2, 20), 90, 10 + index * 0.1, true), "observe forward " + index);
            RoutePoint last = route.Data.points[route.Data.points.Count - 1];
            Check(Mathf.Abs(last.z - 10) < 0.25f && Mathf.Abs(last.x) < 0.001f && last.y == 0, "start heading normalized / camera-relative height");
            Check(Mathf.Abs(route.Data.horizontalMetres - 10) < 0.25f, "10m synthetic distance estimate");
            Check(!route.Finish("JWM:MAIN:B", new Vector3(20, 2, 20), 90, 20, true), "end requires intentional scan mode");
            route.ArmEnd(true);
            Check(!route.Finish("JWM:MAIN:A", new Vector3(20, 2, 20), 90, 20, true), "same QR cannot end");
            Check(!route.Finish("JWM:OTHER:B", new Vector3(20, 2, 20), 90, 20, true), "different warehouse cannot end");
            Check(route.Finish("JWM:MAIN:B", new Vector3(20, 2, 20), 90, 20, true), "end B completes A to B");
            Check(route.Data.state == "complete" && !route.Active && !route.AwaitingEnd, "completed state terminal");
            Check(!route.Observe(Vector3.zero, 0, 20.1, true), "cannot append completed route");
            string storageRoot = Path.Combine(folder, "StorageFixture");
            string saved = WalkingRouteStorage.Save(storageRoot, route.Data);
            Check(File.Exists(saved), "JSON saved");
            WalkingRouteData loaded = WalkingRouteStorage.LoadLast(storageRoot);
            Check(loaded.id == route.Data.id && loaded.endQr == "JWM:MAIN:B" && loaded.points.Count == route.Data.points.Count, "JSON roundtrip");
            string zip = WalkingRouteStorage.Export(storageRoot, route.Data);
            using (ZipArchive archive = ZipFile.OpenRead(zip)) Check(archive.GetEntry("route.json") != null && archive.GetEntry("trajectory.csv") != null && archive.GetEntry("README.txt") != null && archive.Entries.Count == 3, "ZIP contains only trajectory and schema notes");
            WalkingRoute loss = Started();
            Check(!loss.Observe(Vector3.zero, 0, 0.1, false) && loss.Data.state == "interrupted", "tracking loss interrupts without bridging");
            loss.ArmEnd(true);
            Check(!loss.Finish("JWM:MAIN:B", Vector3.one, 0, 2, true), "lost route cannot become complete");
            WalkingRoute jump = Started();
            Check(!jump.Observe(new Vector3(5, 0, 0), 0, 0.1, true), "position jump rejected");
            WalkingRoute gap = Started();
            Check(!gap.Observe(Vector3.zero, 0, 1.1, true), "time gap rejected");
            WalkingRoute backwards = Started();
            Check(!backwards.Observe(Vector3.zero, 0, -0.1, true), "backwards clock rejected");
            WalkingRoute invalidPose = Started();
            Check(!invalidPose.Observe(new Vector3(float.PositiveInfinity, 0, 0), 0, 0.1, true), "infinite pose rejected");
            WalkingRoute shortRoute = Started();
            shortRoute.ArmEnd(true);
            Check(!shortRoute.Finish("JWM:MAIN:B", Vector3.zero, 0, 0.1, true) && shortRoute.Active, "zero length cannot complete");
            WalkingRoute pause = Started();
            pause.Interrupt("background");
            Check(!pause.Active && pause.Data.state == "interrupted", "background cannot resume silently");
            WalkingRoute capacity = Started();
            while (capacity.Data.points.Count < WalkingRoute.MaximumPoints) capacity.Data.points.Add(new RoutePoint());
            Check(!capacity.Observe(Vector3.zero, 0, 0.1, true), "point cap stops safely");
            WalkingRoute longRoute = Started();
            bool longActive = true;
            for (int index = 1; index <= 1801; index++) longActive = longRoute.Observe(Vector3.zero, 0, index, true);
            Check(!longActive && longRoute.Data.state == "interrupted", "30 minute limit enforced");
            WalkingRoute stationary = Started();
            for (int index = 1; index <= 20; index++) stationary.Observe(new Vector3(0.01f * (index % 2), 0, 0), 0, index * 0.1, true);
            Check(stationary.Data.horizontalMetres == 0 && stationary.Data.points.Count == 1, "small stationary jitter not counted");
            WalkingRoute draft = Started();
            string draftRoot = Path.Combine(folder, "DraftFixture-" + Guid.NewGuid().ToString("N"));
            string draftFile = WalkingRouteStorage.Save(draftRoot, draft.Data);
            Check(WalkingRouteStorage.LoadLast(draftRoot).state == "interrupted", "process restart cannot resume AR frame");
            WalkingRouteStorage.Save(draftRoot, draft.Data);
            File.WriteAllText(draftFile, "invalid-json");
            Check(WalkingRouteStorage.LoadLast(draftRoot)?.id == draft.Data.id, "recover previous snapshot if latest corrupted");
            for (int rotation = 0; rotation < 4; rotation++)
            {
                const int size = 400;
                var matrix = new MultiFormatWriter().encode("JWM:MAIN:A", BarcodeFormat.QR_CODE, size, size);
                byte[] pixels = new byte[size * size];
                for (int row = 0; row < size; row++)
                    for (int column = 0; column < size; column++) pixels[row * size + column] = matrix[column, row] ? (byte)0 : (byte)255;
                for (int turn = 0; turn < rotation; turn++)
                {
                    byte[] rotated = new byte[pixels.Length];
                    for (int row = 0; row < size; row++)
                        for (int column = 0; column < size; column++) rotated[column * size + size - row - 1] = pixels[row * size + column];
                    pixels = rotated;
                }
                Check(WalkingRouteRecorder.Decode(pixels, size, size) == "JWM:MAIN:A", "production grayscale QR decoder at " + rotation * 90);
            }
            Check(WalkingRouteRecorder.Decode(new byte[2], 10, 10) == null, "invalid camera buffer rejected");
            File.WriteAllLines(Path.Combine(folder, "offline-result.txt"), new[] { "PASS " + Passed.Count + " assertions", "UTC=" + DateTime.UtcNow.ToString("O"), "Synthetic/offline only. NOT a physical ARCore/camera acceptance test." });
            File.WriteAllLines(Path.Combine(folder, "offline-checks.txt"), Passed);
            Debug.Log("[WalkingRouteReview] PASS " + Passed.Count);
        }

        private static WalkingRoute Started()
        {
            WalkingRoute result = new WalkingRoute();
            if (!result.Begin("JWM:MAIN:A", Vector3.zero, 0, 0, true)) throw new InvalidOperationException("Fixture start failed.");
            return result;
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("[WalkingRouteReview] FAIL " + name);
            Passed.Add(name);
        }
    }
}
