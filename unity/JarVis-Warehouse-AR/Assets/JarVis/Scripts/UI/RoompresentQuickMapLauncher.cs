using System.Collections;
using System.Collections.Generic;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JarVis.Warehouse.UI
{
    public sealed class RoompresentQuickMapLauncher : MonoBehaviour
    {
        public const string SceneName = "Roompresent_VPS";
        static RoompresentQuickMapLauncher instance;
        readonly List<GameObject> suspended = new List<GameObject>();
        Scene home;
        int frameRate;
        bool closing, setupMode;
        public static void OpenSetup(ImportedFigmaNavigation owner){Open(owner,true);}
        ImportedFigmaNavigation nav;
        public static void SetupCurrent()
        {
            if(instance==null)return;
            var scene=SceneManager.GetSceneByName(SceneName);
            var guide=System.Array.Find(System.Array.ConvertAll(scene.GetRootGameObjects(),r=>r.GetComponentInChildren<SmallRoomAR>(true)),g=>g!=null);
            if(guide==null||guide.GetComponent<RoompresentVpsSetup>()!=null)return;
            foreach(var root in scene.GetRootGameObjects())if(root.name=="Small room guidance"){root.SetActive(false);Destroy(root);}
            guide.enabled=false;guide.gameObject.AddComponent<RoompresentVpsSetup>().Configure(guide,instance.nav.GetComponentInChildren<TMP_Text>(true)?.font);
        }
        public static void ResumeCurrent()
        {
            if(instance==null)return;
            var scene=SceneManager.GetSceneByName(SceneName);
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.name=="Roompresent installation"){root.SetActive(false);Destroy(root);}
                var guide=root.GetComponentInChildren<SmallRoomAR>(true);
                if(guide==null)continue;
                var setup=guide.GetComponent<RoompresentVpsSetup>();if(setup!=null){setup.enabled=false;Destroy(setup);}
                guide.ResumeGuidance(instance.nav.GetComponentInChildren<TMP_Text>(true)?.font);
            }
        }
        public static void Open(ImportedFigmaNavigation owner,bool setup=false)
        {
            if (instance != null || SceneManager.GetSceneByName(SceneName).isLoaded) return;
            if(!setup&&!RoompresentDeliveryMission.CanOpenGuidance){RoompresentDeliveryMission.Open(owner);return;}
            if (!Application.CanStreamedLevelBeLoaded(SceneName)) { owner.Notice(ImportedAppLocalization.IsEnglish ? "Roompresent map is missing from this build." : "Bản này chưa có map Roompresent."); return; }
            instance = new GameObject("JarVis Roompresent quick-test bridge").AddComponent<RoompresentQuickMapLauncher>();
            instance.nav = owner;instance.setupMode=setup; instance.StartCoroutine(instance.Launch());
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
                if (guide != null)
                {
                    var font=nav.GetComponentInChildren<TMP_Text>(true)?.font;
                    if(setupMode){guide.enabled=false;guide.gameObject.AddComponent<RoompresentVpsSetup>().Configure(guide,font);}
                    else guide.Configure(font);
                    break;
                }
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
        void OnDestroy() { RoompresentDeliveryMission.EndGuidance();Restore(); if (instance == this) instance = null; }
    }
}
