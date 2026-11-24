using System;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class WalkingRouteUpgradeReview
    {
        public static void Run()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            int checks = 0;
            try
            {
                GameObject root = new GameObject("Room route selection QA");
                SceneManager.MoveGameObjectToScene(root,scene);
                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                typeof(WalkingRouteRecorder).GetProperty("Ready").SetValue(recorder,true);
                recorder.NavigateSaved();
                if (recorder.NavigationMode) throw new InvalidOperationException("No local route must not silently use bundled data");
                checks++;
                WalkingRouteData local = JsonUtility.FromJson<WalkingRouteData>(Resources.Load<TextAsset>("Routes/RecordedPrefix").text);
                local.state = "complete";
                local.endQr = "JWM:MAIN:BEDROOM";
                typeof(WalkingRouteRecorder).GetField("recovered",BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recorder,local);
                recorder.NavigateSaved();
                if (!recorder.NavigationMode || !recorder.UsingSavedRoute || recorder.Follower.Destination != "BEDROOM" || !recorder.Follower.HasEndQr) throw new InvalidOperationException("Local room route and destination selection");
                checks += 4;
                recorder.Primary();
                recorder.NavigateExample();
                if (!recorder.NavigationMode || recorder.UsingSavedRoute || recorder.Follower.HasEndQr || recorder.Follower.Data.points.Count != 71) throw new InvalidOperationException("Bundled incomplete route must stay explicit");
                checks += 4;
                string ui = File.ReadAllText("Assets/JarVis/Scripts/RouteRecording/WalkingRouteRecorderUi.cs");
                if (ui.Contains("placeFloor") || ui.Contains("needFloor") || ui.Contains("arrowCard")) throw new InvalidOperationException("Legacy floor gate / hidden HUD remains");
                checks++;
                string map = File.ReadAllText("Assets/JarVis/Scripts/UI/WarehouseMapScreen.cs");
                if (!map.Contains("WalkingRouteLauncher.Open()")) throw new InvalidOperationException("Main JarVis map AR entry missing");
                checks++;
                Directory.CreateDirectory("Logs/WalkingRoutes");
                File.WriteAllText("Logs/WalkingRoutes/upgrade-result.txt","PASS " + checks + " local selection / integration source checks. Native device bridge, permissions, and route alignment still require Samsung.\nUTC=" + DateTime.UtcNow.ToString("O"));
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }

        public static void BuildBoth()
        {
            WalkingRouteBuild.Release();
            string name = PlayerSettings.productName;
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string version = PlayerSettings.bundleVersion;
            int code = PlayerSettings.Android.bundleVersionCode;
            bool customKey = PlayerSettings.Android.useCustomKeystore;
            try
            {
                PlayerSettings.productName = "JarVis Warehouse";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.teep.jarviswarehousear");
                PlayerSettings.bundleVersion = "0.2.0";
                PlayerSettings.Android.bundleVersionCode = 4;
                PlayerSettings.Android.useCustomKeystore = false;
                string path = Path.GetFullPath("Builds/Android/JarVis-Warehouse-0.2.0.apk");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/JarVisFigmaApp.unity",WalkingRouteBuild.ScenePath },
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    locationPathName = path,
                    options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
                });
                File.WriteAllText("Logs/WalkingRoutes/main-build-result.txt","result=" + report.summary.result + "\nerrors=" + report.summary.totalErrors + "\napkBytes=" + new FileInfo(path).Length);
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("JarVis main APK failed");
            }
            finally
            {
                PlayerSettings.productName = name;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,identifier);
                PlayerSettings.bundleVersion = version;
                PlayerSettings.Android.bundleVersionCode = code;
                PlayerSettings.Android.useCustomKeystore = customKey;
                AssetDatabase.SaveAssets();
            }
        }
    }
}
