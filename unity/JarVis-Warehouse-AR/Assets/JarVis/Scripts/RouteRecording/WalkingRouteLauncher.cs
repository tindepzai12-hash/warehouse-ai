using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteLauncher : MonoBehaviour
    {
        public const string SceneName = "JarVisRouteRecorder";
        public static bool Embedded => instance != null;
        private static WalkingRouteLauncher instance;
        private readonly List<GameObject> suspended = new List<GameObject>();
        private bool closing;
        private int frameRate;

        public static void Open()
        {
            if (instance != null || SceneManager.GetSceneByName(SceneName).isLoaded) return;
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Debug.LogWarning("[JarVis Routes] Route scene is not included in this build.");
                return;
            }
            instance = new GameObject("JarVis route bridge").AddComponent<WalkingRouteLauncher>();
            instance.StartCoroutine(instance.Launch());
        }

        private IEnumerator Launch()
        {
            frameRate = Application.targetFrameRate;
            for (int index = 0; index < SceneManager.sceneCount; index++)
                foreach (GameObject root in SceneManager.GetSceneAt(index).GetRootGameObjects())
                    if (root != gameObject && root.activeSelf)
                    {
                        suspended.Add(root);
                        root.SetActive(false);
                    }
            AsyncOperation load = null;
            try { load = SceneManager.LoadSceneAsync(SceneName,LoadSceneMode.Additive); }
            catch (System.Exception) { Restore(); Destroy(gameObject); }
            if (load != null) yield return load;
            else if (this != null) { Restore(); Destroy(gameObject); }
        }

        public static void Close()
        {
            if (instance == null || instance.closing) return;
            instance.closing = true;
            instance.StartCoroutine(instance.Return());
        }

        private IEnumerator Return()
        {
            Scene route = SceneManager.GetSceneByName(SceneName);
            if (route.isLoaded)
            {
                foreach (GameObject root in route.GetRootGameObjects())
                {
                    WalkingRouteRecorder recorder = root.GetComponentInChildren<WalkingRouteRecorder>();
                    if (recorder != null && recorder.Route.Active) recorder.Secondary();
                }
                yield return SceneManager.UnloadSceneAsync(route);
            }
            Restore();
            Destroy(gameObject);
        }

        private void Restore()
        {
            foreach (GameObject root in suspended) if (root != null) root.SetActive(true);
            suspended.Clear();
            Application.targetFrameRate = frameRate;
        }

        private void OnDestroy()
        {
            Restore();
            if (instance == this) instance = null;
        }
    }
}
