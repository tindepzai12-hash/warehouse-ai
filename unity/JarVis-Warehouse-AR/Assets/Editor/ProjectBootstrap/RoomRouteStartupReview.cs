using System;
using System.Collections.Generic;
using System.IO;
using JarVis.Warehouse.RouteRecording;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoomRouteStartupReview
    {
        private const string Key = "JarVis.Room.Startup.QA";
        private static readonly List<string> Errors = new List<string>();
        private static double began;
        private static int firstFrame;
        static RoomRouteStartupReview()
        {
            if (!SessionState.GetBool(Key,false)) return;
            EditorApplication.playModeStateChanged += State;
            Application.logMessageReceived += Log;
            EditorApplication.update += Tick;
        }
        public static void Run()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Editor busy");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Unsaved scene");
            if (!Application.isBatchMode && UnityEngine.SceneManagement.SceneManager.sceneCount != 1) throw new InvalidOperationException("Interactive startup review requires one saved scene");
            SessionState.SetString(Key + ".Scene",UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            Errors.Clear(); began = 0;
            SessionState.SetBool(Key,true);
            SessionState.SetString(Key + ".Errors","");
            SessionState.SetString(Key + ".StartScene",EditorSceneManager.playModeStartScene == null ? "" : AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WalkingRouteBuild.ScenePath);
            EditorSceneManager.OpenScene(WalkingRouteBuild.ScenePath,OpenSceneMode.Single);
            EditorApplication.playModeStateChanged -= State; EditorApplication.playModeStateChanged += State;
            Application.logMessageReceived -= Log; Application.logMessageReceived += Log;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }
        private static void State(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode) { Application.runInBackground = true; began = EditorApplication.timeSinceStartup; firstFrame = Time.frameCount; }
            if (change == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Key,false))
            {
                SessionState.SetBool(Key,false);
                Directory.CreateDirectory("Logs/RoomRoutes");
                string failure = SessionState.GetString(Key + ".Errors","");
                File.WriteAllText("Logs/RoomRoutes/startup.txt",failure.Length == 0 ? "PASS native Unity PlayMode startup across advancing frames; two-mode HUD, image library and real-feature sampler wired. AR camera / physical navigation NOT tested.\n" : failure);
                EditorApplication.playModeStateChanged -= State;
                Application.logMessageReceived -= Log;
                EditorApplication.update -= Tick;
                string startScene = SessionState.GetString(Key + ".StartScene","");
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(startScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(startScene);
                if (Application.isBatchMode) EditorApplication.Exit(failure.Length == 0 ? 0 : 1);
                else
                {
                    string restore = SessionState.GetString(Key + ".Scene","");
                    if (!string.IsNullOrEmpty(restore)) EditorSceneManager.OpenScene(restore,OpenSceneMode.Single);
                }
            }
        }
        private static void Log(string message,string stack,LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) SessionState.SetString(Key + ".Errors",SessionState.GetString(Key + ".Errors","") + type + " " + message + "\n" + stack + "\n");
        }
        private static void Tick()
        {
            if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying || began <= 0 || EditorApplication.timeSinceStartup - began < 4) return;
            if (Time.frameCount <= firstFrame + 2)
            {
                if (EditorApplication.timeSinceStartup - began < 12) return;
                Errors.Add("FAIL PlayMode frames did not advance");
            }
            began = 0;
            WalkingRouteRecorder recorder = UnityEngine.Object.FindFirstObjectByType<WalkingRouteRecorder>();
            if (recorder == null || recorder.Markers == null || recorder.Markers.Error != null || !recorder.Home || recorder.Ready) Errors.Add("FAIL editor startup state");
            ARTrackedImageManager[] managers = UnityEngine.Object.FindObjectsByType<ARTrackedImageManager>(FindObjectsSortMode.None);
            if (managers.Length != 1 || managers[0].referenceLibrary == null || managers[0].referenceLibrary.count != 2) Errors.Add("FAIL measured A/B library wiring");
            if (UnityEngine.Object.FindObjectsByType<ARPointCloudManager>(FindObjectsSortMode.None).Length != 1 || UnityEngine.Object.FindFirstObjectByType<RoomFeatureSampler>() == null) Errors.Add("FAIL real environment feature sampler wiring");
            if (UnityEngine.Object.FindFirstObjectByType<WalkingRouteRecorderUi>() == null || GameObject.Find("Room route HUD") == null) Errors.Add("FAIL native two-mode UI missing");
            if (Errors.Count > 0) SessionState.SetString(Key + ".Errors",SessionState.GetString(Key + ".Errors","") + string.Join("\n",Errors));
            EditorApplication.ExitPlaymode();
        }
    }
}
