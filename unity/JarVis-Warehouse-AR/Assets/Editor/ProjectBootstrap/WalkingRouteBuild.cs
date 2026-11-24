using System;
using System.IO;
using JarVis.Warehouse.RouteRecording;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.Android;
using UnityEditor.XR.ARCore;
using System.Xml;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    public static class WalkingRouteBuild
    {
        public const string ScenePath = "Assets/Scenes/JarVisRouteRecorder.unity";
        public const string ApkPath = "Builds/Android/JarVis-Routes-0.3.5.apk";

        public static void Release()
        {
            const string materialPath = "Assets/Resources/Routes/LiveViewUnlit.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) throw new InvalidOperationException("Thiếu shader URP Unlit cho đường AR.");
                Material material = new Material(shader);
                material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, materialPath);
                AssetDatabase.SaveAssets();
            }
            WalkingRouteFollowerReview.Run();
            WalkingRouteLiveViewReview.Run();
            WalkingRouteUiReview.Run();
            WalkingRouteUpgradeReview.Run();
            Build();
        }

        [MenuItem("Tools/JarVis/Routes/Build Android APK")]
        public static void Build()
        {
            if (EditorApplication.isPlaying || EditorUtility.scriptCompilationFailed) throw new InvalidOperationException("Dừng Play và sửa lỗi biên dịch trước khi build.");
            EnsureScene();
            string name = PlayerSettings.productName;
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string version = PlayerSettings.bundleVersion;
            int versionCode = PlayerSettings.Android.bundleVersionCode;
            bool keystore = PlayerSettings.Android.useCustomKeystore;
            bool appBundle = EditorUserBuildSettings.buildAppBundle;
            AndroidSdkVersions minSdk = PlayerSettings.Android.minSdkVersion;
            UIOrientation orientation = PlayerSettings.defaultInterfaceOrientation;
            RenderPipelineAsset graphics = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset quality = QualitySettings.renderPipeline;
            ARCoreSettings arCore = ARCoreSettings.currentSettings;
            ARCoreSettings.Requirement depth = arCore != null ? arCore.depth : ARCoreSettings.Requirement.Optional;
            WalkingRouteReview.Run();
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android)) throw new InvalidOperationException("Không chuyển được Android.");
                PlayerSettings.productName = "JarVis Routes";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.teep.jarvisroutes");
                PlayerSettings.bundleVersion = "0.3.5";
                PlayerSettings.Android.bundleVersionCode = 14;
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                RenderPipelineAsset mobile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if (mobile == null) throw new InvalidOperationException("Thiếu URP cho camera AR.");
                GraphicsSettings.defaultRenderPipeline = mobile;
                QualitySettings.renderPipeline = mobile;
                if (arCore != null) arCore.depth = ARCoreSettings.Requirement.Optional;
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ApkPath)));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, locationPathName = Path.GetFullPath(ApkPath), options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC });
                Directory.CreateDirectory("Logs/WalkingRoutes");
                File.WriteAllText("Logs/WalkingRoutes/build-result.txt", "result=" + report.summary.result + "\nbytes=" + report.summary.totalSize + "\nerrors=" + report.summary.totalErrors + "\nseconds=" + report.summary.totalTime.TotalSeconds + "\n");
                if (report.summary.result != BuildResult.Succeeded || !File.Exists(ApkPath)) throw new BuildFailedException("Route APK: " + report.summary.result);
                Debug.Log("[JarVis Routes] Built " + Path.GetFullPath(ApkPath));
            }
            finally
            {
                PlayerSettings.productName = name;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, identifier);
                PlayerSettings.bundleVersion = version;
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.Android.useCustomKeystore = keystore;
                PlayerSettings.Android.minSdkVersion = minSdk;
                PlayerSettings.defaultInterfaceOrientation = orientation;
                EditorUserBuildSettings.buildAppBundle = appBundle;
                GraphicsSettings.defaultRenderPipeline = graphics;
                QualitySettings.renderPipeline = quality;
                if (arCore != null) arCore.depth = depth;
                AssetDatabase.SaveAssets();
            }
        }

        public static void EnsureScene()
        {
            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path) && !SceneManager.GetActiveScene().isDirty)
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity", OpenSceneMode.Single);
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("Có scene chưa lưu. Hãy lưu trước khi tạo scene ghi tuyến.");
            bool existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null;
            Scene scene = existing ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                if (existing)
                {
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (TrackedPoseDriver existingDriver in root.GetComponentsInChildren<TrackedPoseDriver>(true)) ConfigureDriver(existingDriver);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                    return;
                }
                GameObject sessionObject = new GameObject("Route AR Session", typeof(ARSession), typeof(ARInputManager));
                SceneManager.MoveGameObjectToScene(sessionObject, scene);
                ARSession session = sessionObject.GetComponent<ARSession>();
                session.enabled = false;
                session.matchFrameRateRequested = false;
                GameObject originObject = new GameObject("Route XR Origin", typeof(XROrigin));
                SceneManager.MoveGameObjectToScene(originObject, scene);
                GameObject offset = new GameObject("Camera Offset");
                offset.transform.SetParent(originObject.transform, false);
                GameObject cameraObject = new GameObject("Route AR Camera", typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver));
                cameraObject.transform.SetParent(offset.transform, false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 50;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.06f, 0.12f, 0.2f);
                ARCameraManager cameraManager = cameraObject.GetComponent<ARCameraManager>();
                cameraManager.enabled = false;
                cameraManager.requestedFacingDirection = CameraFacingDirection.World;
                cameraManager.autoFocusRequested = true;
                TrackedPoseDriver driver = cameraObject.GetComponent<TrackedPoseDriver>();
                ConfigureDriver(driver);
                XROrigin origin = originObject.GetComponent<XROrigin>();
                origin.Origin = originObject;
                origin.CameraFloorOffsetObject = offset;
                origin.Camera = camera;
                origin.CameraYOffset = 0;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                GameObject recorderObject = new GameObject("JarVis route recorder", typeof(WalkingRouteRecorder));
                SceneManager.MoveGameObjectToScene(recorderObject, scene);
                WalkingRouteRecorder recorder = recorderObject.GetComponent<WalkingRouteRecorder>();
                recorder.session = session;
                recorder.cameraManager = cameraManager;
                recorder.arCamera = camera;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void ConfigureDriver(TrackedPoseDriver driver)
        {
            driver.positionInput = new InputActionProperty(new InputAction("AR Position", InputActionType.Value, "<HandheldARInputDevice>/devicePosition", expectedControlType: "Vector3"));
            driver.rotationInput = new InputActionProperty(new InputAction("AR Rotation", InputActionType.Value, "<HandheldARInputDevice>/deviceRotation", expectedControlType: "Quaternion"));
            driver.trackingStateInput = new InputActionProperty(new InputAction("AR Tracking", InputActionType.Value, expectedControlType: "Integer"));
        }
    }

    public sealed class WalkingRouteManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => int.MaxValue;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != "com.teep.jarvisroutes") return;
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            XmlDocument document = new XmlDocument();
            document.Load(manifest);
            const string android = "http://schemas.android.com/apk/res/android";
            foreach (XmlNode node in document.SelectNodes("/manifest/uses-permission"))
            {
                string permission = node.Attributes?["name", android]?.Value;
                if (permission == "android.permission.RECORD_AUDIO" || permission == "android.permission.MODIFY_AUDIO_SETTINGS" || permission == "android.permission.BLUETOOTH") node.ParentNode.RemoveChild(node);
            }
            foreach (XmlNode node in document.SelectNodes("/manifest/uses-feature"))
                if (node.Attributes?["name", android]?.Value == "android.hardware.microphone") node.ParentNode.RemoveChild(node);
            document.Save(manifest);
        }
    }
}
