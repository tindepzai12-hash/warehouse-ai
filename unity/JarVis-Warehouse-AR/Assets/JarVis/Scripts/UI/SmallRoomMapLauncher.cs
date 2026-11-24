using System.Collections;
using System.Collections.Generic;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JarVis.Warehouse.UI
{
    public sealed class SmallRoomMapLauncher : MonoBehaviour
    {
        public const string SceneName = "SmallRoom_AR_225317";
        static SmallRoomMapLauncher instance;
        readonly List<GameObject> suspended = new List<GameObject>();
        Scene home;
        int frameRate;
        bool closing;
        ImportedFigmaNavigation nav;
        public static void Open(ImportedFigmaNavigation owner)
        {
            if (instance != null || SceneManager.GetSceneByName(SceneName).isLoaded) return;
            if (!Application.CanStreamedLevelBeLoaded(SceneName)) { owner.Notice(ImportedAppLocalization.IsEnglish ? "Small room map is missing from this build." : "Bản này chưa có map phòng nhỏ."); return; }
            instance = new GameObject("JarVis small room bridge").AddComponent<SmallRoomMapLauncher>();
            instance.nav = owner; instance.StartCoroutine(instance.Launch());
        }
        IEnumerator Launch()
        {
            home = SceneManager.GetActiveScene(); frameRate = Application.targetFrameRate;
            foreach (var root in home.GetRootGameObjects()) if (root != gameObject && root.activeSelf) { suspended.Add(root); root.SetActive(false); }
            yield return null; // Release the barcode camera before starting AR Foundation.
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive); } catch (System.Exception) { }
            if (operation == null) { Restore(); nav.Notice(ImportedAppLocalization.IsEnglish ? "Could not open AR; try again." : "Chưa mở được AR; thử lại."); Destroy(gameObject); yield break; }
            yield return operation;
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded) { Close(); yield break; }
            SceneManager.SetActiveScene(scene);
            foreach (var root in scene.GetRootGameObjects())
            {
                var guide = root.GetComponentInChildren<SmallRoomAR>(true);
                if (guide != null) { guide.Configure(nav.GetComponentInChildren<TMP_Text>(true)?.font); break; }
            }
        }
        void Update() { if (!closing && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close(); }
        public static void Close() { if (instance == null || instance.closing) return; instance.closing = true; instance.StartCoroutine(instance.Return()); }
        IEnumerator Return()
        {
            if (home.IsValid() && home.isLoaded) SceneManager.SetActiveScene(home);
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Restore(); Destroy(gameObject);
        }
        void Restore() { foreach (var root in suspended) if (root != null) root.SetActive(true); suspended.Clear(); Application.targetFrameRate = frameRate; }
        void OnDestroy() { Restore(); if (instance == this) instance = null; }
    }
}
