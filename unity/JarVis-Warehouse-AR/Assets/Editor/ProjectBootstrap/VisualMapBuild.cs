using System;
using System.IO;
using System.Xml;
using JarVis.Warehouse.RouteRecording;
using JarVis.Warehouse.VisualMaps;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    public static class VisualMapBuild
    {
        public const string ScenePath = "Assets/Scenes/JarVisVisualMaps.unity";
        public const string ApkPath = "Builds/Android/JarVis-Maps-0.2.1.apk";

        public static void EnsureScene()
        {
            if (EditorApplication.isPlaying || EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Dừng Play và sửa lỗi compile trước.");
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty) EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity",OpenSceneMode.Single);
            for (int index = 0; index < SceneManager.sceneCount; index++) if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Cần lưu scene hiện tại trước.");
            Scene scene = EditorSceneManager.OpenScene(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null ? ScenePath : WalkingRouteBuild.ScenePath,OpenSceneMode.Additive);
            try
            {
                WalkingRouteRecorder recorder = null;
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.GetComponentInChildren<WalkingRouteRecorder>(true) != null) recorder = root.GetComponentInChildren<WalkingRouteRecorder>(true);
                if (recorder == null)
                {
                    bool exists = false;
                    foreach (GameObject root in scene.GetRootGameObjects()) exists |= root.GetComponentInChildren<VisualMapSession>(true) != null;
                    if (!exists) throw new InvalidOperationException("Không có cấu hình camera để tạo Visual Maps.");
                    return;
                }
                ARSession session = recorder.session;
                ARCameraManager cameraManager = recorder.cameraManager;
                Camera camera = recorder.arCamera;
                GameObject owner = recorder.gameObject;
                UnityEngine.Object.DestroyImmediate(recorder);
                VisualMapSession maps = owner.AddComponent<VisualMapSession>();
                maps.session = session; maps.cameraManager = cameraManager; maps.arCamera = camera;
                XROrigin origin = camera.GetComponentInParent<XROrigin>();
                maps.planes = origin.GetComponent<ARPlaneManager>() ?? origin.gameObject.AddComponent<ARPlaneManager>();
                maps.raycasts = origin.GetComponent<ARRaycastManager>() ?? origin.gameObject.AddComponent<ARRaycastManager>();
                maps.anchors = origin.GetComponent<ARAnchorManager>() ?? origin.gameObject.AddComponent<ARAnchorManager>();
                maps.planes.enabled = maps.raycasts.enabled = maps.anchors.enabled = false;
                owner.name = "JarVis COLMAP hloc session";
                EditorSceneManager.SaveScene(scene,ScenePath,true);
            }
            finally { EditorSceneManager.CloseScene(scene,true); }
        }

        [MenuItem("Tools/JarVis/Visual Maps/Build Android APK")]
        public static void Build()
        {
            EnsureScene();
            VisualMapReview.Run();
            string name = PlayerSettings.productName;
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string version = PlayerSettings.bundleVersion;
            int versionCode = PlayerSettings.Android.bundleVersionCode;
            bool keystore = PlayerSettings.Android.useCustomKeystore,appBundle = EditorUserBuildSettings.buildAppBundle;
            AndroidSdkVersions sdk = PlayerSettings.Android.minSdkVersion;
            AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
            ScriptingImplementation scripting = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            UIOrientation orientation = PlayerSettings.defaultInterfaceOrientation;
            RenderPipelineAsset graphics = GraphicsSettings.defaultRenderPipeline,quality = QualitySettings.renderPipeline;
            InsecureHttpOption http = PlayerSettings.insecureHttpOption;
            ARCoreSettings core = ARCoreSettings.currentSettings;
            ARCoreSettings.Requirement depth = core != null ? core.depth : ARCoreSettings.Requirement.Optional;
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android)) throw new BuildFailedException("Không chuyển được Android.");
                PlayerSettings.productName = "JarVis Maps";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.teep.jarvismaps");
                PlayerSettings.bundleVersion = VisualMapData.Version; PlayerSettings.Android.bundleVersionCode = 4;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.Android.useCustomKeystore = false;
                PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
                EditorUserBuildSettings.buildAppBundle = false;
                RenderPipelineAsset mobile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if (mobile == null || Resources.Load<Material>("Routes/LiveViewUnlit") == null) throw new BuildFailedException("Thiếu vật liệu AR / URP mobile.");
                GraphicsSettings.defaultRenderPipeline = QualitySettings.renderPipeline = mobile;
                if (core != null) core.depth = ARCoreSettings.Requirement.Optional;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ApkPath)));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath },target = BuildTarget.Android,locationPathName = Path.GetFullPath(ApkPath),options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC });
                Directory.CreateDirectory("Logs/VisualMaps");
                File.WriteAllText("Logs/VisualMaps/build-result.txt","result=" + report.summary.result + "\nbytes=" + report.summary.totalSize + "\nerrors=" + report.summary.totalErrors + "\nseconds=" + report.summary.totalTime.TotalSeconds);
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Visual Maps build: " + report.summary.result);
            }
            finally
            {
                PlayerSettings.productName = name; PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,identifier);
                PlayerSettings.bundleVersion = version; PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.Android.useCustomKeystore = keystore; PlayerSettings.Android.minSdkVersion = sdk;
                PlayerSettings.Android.targetArchitectures = architectures;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,scripting);
                PlayerSettings.defaultInterfaceOrientation = orientation; PlayerSettings.insecureHttpOption = http;
                EditorUserBuildSettings.buildAppBundle = appBundle;
                GraphicsSettings.defaultRenderPipeline = graphics; QualitySettings.renderPipeline = quality;
                if (core != null) core.depth = depth;
                AssetDatabase.SaveAssets();
            }
        }
    }

    public sealed class VisualMapManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => int.MaxValue;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.teep.jarvismaps") return;
            string target = Path.Combine(path,"src/main/AndroidManifest.xml");
            XmlDocument document = new XmlDocument(); document.Load(target);
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (XmlNode permission in document.SelectNodes("/manifest/uses-permission"))
                if (permission.Attributes?["name",android]?.Value is string name && (name == "android.permission.RECORD_AUDIO" || name == "android.permission.MODIFY_AUDIO_SETTINGS" || name == "android.permission.BLUETOOTH")) permission.ParentNode.RemoveChild(permission);
            foreach (XmlNode feature in document.SelectNodes("/manifest/uses-feature"))
                if (feature.Attributes?["name",android]?.Value == "android.hardware.microphone") feature.ParentNode.RemoveChild(feature);
            XmlElement application = document.SelectSingleNode("/manifest/application") as XmlElement;
            application?.SetAttribute("usesCleartextTraffic",android,"true");
            document.Save(target);
        }
    }
}
