#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Immersal;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoompresentVideoSetup
    {
        const string RequestPath = "Library/RoompresentVideoSetup.request";
        const string ReportPath = "Logs/ImmersalVideoSetup/roompresent-import.txt";
        static AddRequest packageRequest;
        static bool working;
        static RoompresentVideoSetup() { EditorApplication.update += Tick; }

        [MenuItem("Tools/JarVis/Immersal/04 Import Roompresent Video Assets")]
        public static void RequestImport() { File.WriteAllText(RequestPath, "import"); }

        static void Tick()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try
            {
                if (File.ReadAllText(RequestPath).Trim() == "restart")
                {
                    working = true;
                    var scene = SceneManager.GetActiveScene();
                    if (scene.path != ImmersalVideoSetup.ScenePath) throw new InvalidOperationException("Open the video setup scene before restarting.");
                    Directory.CreateDirectory("Library/ImmersalVideoSetup");
                    EditorSceneManager.SaveScene(scene, "Library/ImmersalVideoSetup/BeforeBurstRestart.unity", true);
                    var sdk = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ImmersalSDK>(true)).Single();
                    if (!string.IsNullOrEmpty(sdk.developerToken)) File.WriteAllText("Library/ImmersalVideoSetup/UserToken.local", sdk.developerToken);
                    sdk.developerToken = "";
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sdk);
                    EditorSceneManager.SaveScene(scene);
                    File.Delete(RequestPath);
                    Debug.Log("[RoompresentVideoSetup] Saved current scene and local-only credential backup before Editor restart.");
                    EditorApplication.delayCall += () => EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                    return;
                }
                if (packageRequest != null)
                {
                    if (!packageRequest.IsCompleted) return;
                    if (packageRequest.Status == StatusCode.Failure) throw new InvalidOperationException(packageRequest.Error.message);
                    packageRequest = null;
                    working = false;
                }
                if (working) return;
                bool installed = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p => p.name == "com.unity.cloud.gltfast");
                if (!installed)
                {
                    working = true;
                    Debug.Log("[RoompresentVideoSetup] Installing official glTFast package required by the video.");
                    packageRequest = Client.Add("com.unity.cloud.gltfast");
                    return;
                }
                working = true;
                AssetDatabase.Refresh();
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, "PASS official glTFast installed; original Roompresent map, metadata, sparse point cloud and Scaniverse GLB imported.\n");
                File.Delete(RequestPath);
                Debug.Log("[RoompresentVideoSetup] PASS imported original Roompresent assets and official glTFast.");
                working = false;
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, "FAIL " + e.Message);
                if (File.Exists(RequestPath)) File.Move(RequestPath, RequestPath + ".failed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                working = false;
                Debug.LogError("[RoompresentVideoSetup] " + e.Message);
            }
        }
    }
}
#endif
