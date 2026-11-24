#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Immersal;
using Immersal.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    // Copies the official SDK sample rather than recreating SDK internals.
    public static class ImmersalVideoSetup
    {
        public const string ScenePath = "Assets/Scenes/ARNavigation_Immersal_Video.unity";
        const string Source = "Assets/Samples/Immersal SDK/2.5.0/Core Samples/Scenes/SimpleSample.unity";
        const string ReportPath = "Logs/ImmersalVideoSetup/validation.json";

        [MenuItem("Tools/JarVis/Immersal/03 Add to Map AR and Open Login")]
        public static void IntegrateAndOpenLogin()
        {
            Create();
            var entries = EditorBuildSettings.scenes.ToList();
            int index = entries.FindIndex(s => s.path == ScenePath);
            if (index < 0) entries.Add(new EditorBuildSettingsScene(ScenePath, true));
            else entries[index] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = entries.ToArray();
            Require(EditorBuildSettings.scenes.Any(s => s.path == ScenePath && s.enabled), "Immersal scene missing from build list.");
            EditorApplication.ExecuteMenuItem("Immersal SDK/Login");
            Debug.Log("[ImmersalVideoSetup] Map AR scene registered; opened user login. No credentials read or localization claimed.");
        }

        [MenuItem("Tools/JarVis/Immersal/01 Create or Open Video Setup")]
        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating setup.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save your edited scenes first; setup will not overwrite them.");
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset(Source, ScenePath))
                    throw new InvalidOperationException("Could not copy the official Immersal sample.");
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var space = All<XRSpace>().Single();
                space.name = "XRSpace";
                // The official sample has Filter -> Smoother wired in that order.
                space.ProcessPoses = true;
                var map = All<XRMap>().Single();
                if (map.IsConfigured || map.mapFile != null || map.mapId != -1)
                    throw new InvalidOperationException("Unexpected map data in setup source. No map was substituted.");
                Selection.activeGameObject = map.gameObject;
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Selection.activeGameObject = All<XRMap>().Single().gameObject;
            }
            Validate();
            AssetDatabase.SaveAssets();
            Debug.Log("[ImmersalVideoSetup] Setup scene saved. Map/token are not configured; no localization or device test claimed.");
        }

        [MenuItem("Tools/JarVis/Immersal/02 Validate Video Setup")]
        public static void Validate()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open the video setup scene before validation.");
            Require(All<ARSession>().Length == 1, "Exactly one ARSession required.");
            Require(All<ARCameraManager>().Length == 1, "Exactly one AR camera required.");
            Require(All<ImmersalSDK>().Length == 1, "Exactly one genuine ImmersalSDK required.");
            var sdk = All<ImmersalSDK>().Single();
            Require(sdk.Session != null && sdk.PlatformSupport != null && sdk.Localizer != null &&
                    sdk.SceneUpdater != null && sdk.TrackingAnalyzer != null, "SDK subsystem references missing.");
            var space = All<XRSpace>().Single();
            var processors = space.SceneDataProcessors;
            Require(space.ProcessPoses && processors.Length == 2 && processors[0] is PoseFilter &&
                    processors[1] is PoseSmoother, "PoseFilter -> PoseSmoother chain is not wired.");
            var map = All<XRMap>().Single();
            Require(map.transform.IsChildOf(space.transform), "XR Map must be inside XRSpace.");
            Require(All<Canvas>().Length > 0, "Official localization-status Canvas missing.");
            var missing = SceneManager.GetActiveScene().GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Transform>(true))
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            Require(missing == 0, "Scene has missing scripts.");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, JsonUtility.ToJson(new Report {
                unityVersion = Application.unityVersion,
                rootObjects = SceneManager.GetActiveScene().GetRootGameObjects().Select(go => go.name).ToArray(),
                processors = processors.Select(p => p.GetType().Name).ToArray(),
                mapConfigured = map.IsConfigured,
                mapFileAssigned = map.mapFile != null,
                localizationMethodAssigned = map.LocalizationMethod != null,
                sdkReferencesValid = true,
                missingScriptCount = missing,
                savedScene = ScenePath,
                sourceScene = Source,
                note = "SETUP VALIDATED ONLY. A venue-specific Immersal map and authorized user configuration remain required. No AR/device/localization acceptance test was run."
            }, true));
            Debug.Log("[ImmersalVideoSetup] PASS: genuine SDK, AR camera, XRSpace processors, XRMap, Canvas and script references.");
        }

        static T[] All<T>() where T : Component => SceneManager.GetActiveScene().GetRootGameObjects()
            .SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
        static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException(reason); }
        [Serializable] class Report
        {
            public string unityVersion, savedScene, sourceScene, note;
            public string[] rootObjects, processors;
            public bool mapConfigured, mapFileAssigned, localizationMethodAssigned, sdkReferencesValid;
            public int missingScriptCount;
        }
    }
}
#endif
