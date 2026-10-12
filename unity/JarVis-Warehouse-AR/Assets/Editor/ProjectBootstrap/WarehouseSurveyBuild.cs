using System;
using System.IO;
using JarVis.Warehouse.AR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ProjectBootstrap
{
    public static class WarehouseSurveyBuild
    {
        private const string ScenePath = "Assets/Scenes/JarVisSurvey.unity";
        private const string OutputPath = "Builds/Android/JarVis-Survey.apk";

        [MenuItem("Tools/JarVis/Build Survey APK")]
        public static void BuildApk()
        {
            EnsureScene();
            string originalName = PlayerSettings.productName;
            string originalIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            bool originalKeystore = PlayerSettings.Android.useCustomKeystore;
            bool originalAppBundle = EditorUserBuildSettings.buildAppBundle;
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android
                    && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Không chuyển được sang Android build target.");

                PlayerSettings.productName = "JarVis Survey";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.teep.jarvissurvey");
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                string absolutePath = Path.GetFullPath(OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = absolutePath,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new UnityEditor.Build.BuildFailedException("Survey APK: " + report.summary.result);

                FileInfo apk = new FileInfo(absolutePath);
                if (!apk.Exists || apk.Length == 0)
                    throw new UnityEditor.Build.BuildFailedException("Survey APK không tồn tại sau build.");
                Debug.Log("[WarehouseSurveyBuild] APK: " + apk.FullName + " (" + apk.Length + " bytes)");
            }
            finally
            {
                PlayerSettings.productName = originalName;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, originalIdentifier);
                PlayerSettings.Android.useCustomKeystore = originalKeystore;
                EditorUserBuildSettings.buildAppBundle = originalAppBundle;
            }
        }

        private static void EnsureScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                GameObject sessionObject = new GameObject("AR Session", typeof(ARSession), typeof(ARInputManager));
                SceneManager.MoveGameObjectToScene(sessionObject, scene);
                ARSession session = sessionObject.GetComponent<ARSession>();
                session.enabled = false;

                GameObject originObject = new GameObject(
                    "XR Origin (Survey)", typeof(XROrigin), typeof(ARPlaneManager), typeof(ARRaycastManager)
                );
                SceneManager.MoveGameObjectToScene(originObject, scene);
                originObject.GetComponent<ARPlaneManager>().requestedDetectionMode = PlaneDetectionMode.Horizontal;

                GameObject offsetObject = new GameObject("Camera Offset");
                offsetObject.transform.SetParent(originObject.transform, false);
                GameObject cameraObject = new GameObject(
                    "AR Camera", typeof(Camera), typeof(AudioListener),
                    typeof(ARCameraManager), typeof(ARCameraBackground), typeof(TrackedPoseDriver)
                );
                cameraObject.transform.SetParent(offsetObject.transform, false);
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 30f;

                XROrigin origin = originObject.GetComponent<XROrigin>();
                origin.Origin = originObject;
                origin.CameraFloorOffsetObject = offsetObject;
                origin.Camera = camera;
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                origin.CameraYOffset = 0f;

                GameObject surveyObject = new GameObject("Survey Capture", typeof(WarehouseSurveyCapture));
                SceneManager.MoveGameObjectToScene(surveyObject, scene);
                SerializedObject surveyComponent = new SerializedObject(surveyObject.GetComponent<WarehouseSurveyCapture>());
                surveyComponent.FindProperty("arSession").objectReferenceValue = session;
                surveyComponent.FindProperty("raycastManager").objectReferenceValue = originObject.GetComponent<ARRaycastManager>();
                surveyComponent.FindProperty("arCamera").objectReferenceValue = camera;
                surveyComponent.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
