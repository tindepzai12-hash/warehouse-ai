using System.Collections;
using System.Collections.Generic;
using AtlasAR;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    public sealed class JarvisLabLauncher:MonoBehaviour
    {
        public const string SceneName="AtlasAR";
        static JarvisLabLauncher instance;
        readonly List<GameObject> suspended=new List<GameObject>();
        Scene home; int oldFrameRate; bool closing,starting,started;
        float retryAt; AtlasSession session; Text summary; Button complete;
        public static void Open()
        {
            if(instance!=null)return;
            if(!Application.CanStreamedLevelBeLoaded(SceneName)){Debug.LogError("[JarVis Lab] Lab scene missing from APK.");return;}
            instance=new GameObject("JarVis lab bridge").AddComponent<JarvisLabLauncher>();instance.StartCoroutine(instance.Launch());
        }
        IEnumerator Launch()
        {
            home=SceneManager.GetActiveScene();oldFrameRate=Application.targetFrameRate;
            foreach(var root in home.GetRootGameObjects())if(root!=gameObject&&root.activeSelf){suspended.Add(root);root.SetActive(false);}
            SceneManager.sceneLoaded+=Loaded;
            AsyncOperation load=null;
            try{load=SceneManager.LoadSceneAsync(SceneName,LoadSceneMode.Additive);}
            catch(System.Exception){Restore();Destroy(gameObject);}
            if(load==null)yield break;
            yield return load;yield return null;
            if(this==null)yield break;
            session=FindAnyObjectByType<AtlasSession>();
            if(session==null||session.SavedMap==null||!JarvisLabMission.BindMap(session.SavedMap.id,session.SavedMap.revision))
            {Debug.LogError("[JarVis Lab] Map identity changed or failed to load; navigation blocked.");Close();yield break;}
            session.SelectTarget("B");
            var ui=session.GetComponent<AtlasUi>();
            var safe=ui.RootCanvas.transform.Find("Safe area");
            var row=new GameObject("JarVis milk trial",typeof(RectTransform),typeof(LayoutElement),typeof(HorizontalLayoutGroup));row.transform.SetParent(safe,false);row.transform.SetAsFirstSibling();row.GetComponent<LayoutElement>().preferredHeight=44;
            var layout=row.GetComponent<HorizontalLayoutGroup>();layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;
            var back=Control(row.transform,"← JarVis",Close);back.gameObject.AddComponent<LayoutElement>().preferredWidth=100;
            complete=Control(row.transform,"Xác nhận giao tại B",()=>{
                float distance=DistanceToB();
                if(JarvisLabMission.Complete(session.PositionValid,distance,session.Navigation.Target))session.StopNavigation();
            });complete.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            var note=new GameObject("Milk trial status",typeof(RectTransform),typeof(Text),typeof(LayoutElement));note.transform.SetParent(safe,false);note.transform.SetSiblingIndex(1);note.GetComponent<LayoutElement>().preferredHeight=32;summary=note.GetComponent<Text>();summary.font=Resources.Load<Font>("Atlas/Regular");summary.fontSize=12;summary.color=new Color32(20,39,65,255);summary.alignment=TextAnchor.MiddleCenter;summary.raycastTarget=false;
        }
        static Button Control(Transform parent,string title,UnityEngine.Events.UnityAction click)
        {
            var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=Color.white;var b=go.GetComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(click);
            var text=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();text.transform.SetParent(go.transform,false);var r=text.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(4,2);r.offsetMax=new Vector2(-4,-2);text.font=Resources.Load<Font>("Atlas/Bold");text.fontSize=13;text.alignment=TextAnchor.MiddleCenter;text.color=new Color32(22,91,232,255);text.text=title;text.raycastTarget=false;return b;
        }
        void Loaded(Scene scene,LoadSceneMode mode){if(scene.name==SceneName)SceneManager.SetActiveScene(scene);}
        float DistanceToB()
        {
            if(session==null||!session.PositionValid||session.Navigation==null)return float.PositiveInfinity;
            var delta=session.MapPosition-session.Navigation.Arrival("B");delta.y=0;return delta.magnitude;
        }
        void Update()
        {
            if(closing)return;
            if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true){Close();return;}
            if(session==null||session.Navigation==null)return;
            if(summary!=null)summary.text=JarvisLabMission.Status;
            if(complete!=null)complete.interactable=JarvisLabMission.Current.state=="carrying"&&session.PositionValid&&DistanceToB()<=.8f;
            if(!started&&!starting&&session.PositionValid&&JarvisLabMission.Current.state=="carrying"&&Time.unscaledTime>=retryAt)
            {
                starting=true;session.SelectTarget("B");session.StartNavigation();started=session.Navigation.Active;starting=false;retryAt=Time.unscaledTime+2;
            }
        }
        public static void Close(){if(instance==null||instance.closing)return;instance.closing=true;instance.StartCoroutine(instance.Return());}
        IEnumerator Return()
        {
            if(home.IsValid()&&home.isLoaded)SceneManager.SetActiveScene(home);
            var lab=SceneManager.GetSceneByName(SceneName);
            if(lab.isLoaded)yield return SceneManager.UnloadSceneAsync(lab);
            Restore();Destroy(gameObject);
        }
        void Restore(){foreach(var root in suspended)if(root!=null)root.SetActive(true);suspended.Clear();Application.targetFrameRate=oldFrameRate;}
        void OnDestroy(){SceneManager.sceneLoaded-=Loaded;Restore();if(instance==this)instance=null;}
    }
}
