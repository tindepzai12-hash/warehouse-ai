using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class JarvisHomeLatestReview
    {
        static JarvisHomeLatestReview(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("HomeLatestReview",false)){SessionState.SetBool("HomeLatestReview",false);new GameObject("Home latest review").AddComponent<HomeLatestRunner>();}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("HomeLatestReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class HomeLatestRunner:MonoBehaviour
    {
        const string Dir="Logs/Home02420261110";
        const BindingFlags Instance=BindingFlags.NonPublic|BindingFlags.Instance,Static=BindingFlags.NonPublic|BindingFlags.Static;
        readonly List<string> result=new List<string>();bool failed;
        void Check(bool ok,string note){result.Add((ok?"PASS ":"FAIL ")+note);failed|=!ok;}
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,Instance).SetValue(target,value);
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();nav.Presence.StopAllCoroutines();nav.Presence.enabled=false;
            var loc=nav.GetComponent<ImportedAppLocalization>();var oldLanguage=JarvisLocalization.CurrentLanguage;string oldTheme=PlayerPrefs.GetString("JarVis.Theme","light");int oldSize=PlayerPrefs.GetInt("JarVis.TextSize",1);
            OmsMobileSession.Set("http://127.0.0.1:1","EDITOR-FIXTURE","admin",DateTime.UtcNow.AddHours(1).ToString("O"));
            var latest=typeof(ImportedHomeDashboard).GetMethod("LatestTask",Static);
            var older=new OmsMobileApi.TaskRow{id="older",title="Older task",created_at="2026-11-09T10:00:00Z"};
            var newer=new OmsMobileApi.TaskRow{id="newer",title="Latest test task",created_at="2026-11-10T10:00:00Z",status="ASSIGNED",locationCode="B"};
            Check(latest.Invoke(null,new object[]{new[]{older,null,newer}})==newer,"latest task sorted by real timestamp, not first array item");
            Check(latest.Invoke(null,new object[]{Array.Empty<OmsMobileApi.TaskRow>()})==null,"empty task result");
            var noDates=new[]{new OmsMobileApi.TaskRow{id="first"},new OmsMobileApi.TaskRow{id="second"}};
            Check(latest.Invoke(null,new object[]{noDates})==noDates[0],"undated tasks preserve API order");
            foreach(var lang in new[]{SupportedLanguage.En,SupportedLanguage.Vi})foreach(string theme in new[]{"dark","light"})foreach(int size in new[]{1,2})
            {
                Field(nav.Presence,"<State>k__BackingField",PresenceState.Online);
                loc.Select(lang);PlayerPrefs.SetInt("JarVis.TextSize",size);nav.Settings.SelectTheme(theme);nav.Show("126:8",false);yield return new WaitForSecondsRealtime(.4f);
                Check(nav.CurrentScreen=="126:8","Home route is active for visual review");
                var home=nav.screens.First(s=>s.id=="126:8").root.GetComponent<ImportedHomeDashboard>();home.StopAllCoroutines();
                Field(nav.Presence,"snapshot",new OmsMobileApi.PresenceReply{status="ONLINE",streak=new OmsMobileApi.ActivityStreak{currentStreak=1,todayQualified=true}});
                Field(home,"tasks",null);typeof(ImportedHomeDashboard).GetMethod("Paint",Instance).Invoke(home,null);yield return null;Canvas.ForceUpdateCanvases();
                var texts=home.GetComponentsInChildren<TMP_Text>();var label=texts.First(t=>t.text.Contains(lang==SupportedLanguage.En?"consecutive active day":"ngày hoạt động liên tiếp"));label.ForceMeshUpdate();
                var flame=home.GetComponentInChildren<PrototypeFlame>();Vector3 flameCenter=flame.rectTransform.TransformPoint(flame.rectTransform.rect.center),textCenter=label.rectTransform.TransformPoint(label.rectTransform.rect.center);
                Check(Mathf.Abs(flameCenter.y-textCenter.y)<1&&!label.isTextTruncated,lang+theme+size+": streak text vertically aligned with original flame position, text fits; delta="+Mathf.Abs(flameCenter.y-textCenter.y)+" truncated="+label.isTextTruncated);
                var flameTopLeft=flame.rectTransform.parent.parent.InverseTransformPoint(flame.rectTransform.TransformPoint(Vector3.zero));
                Check(Mathf.Abs(flameTopLeft.x-16)<.1f&&Mathf.Abs(flameTopLeft.y+16)<.1f,"flame restored to original x16/y16");
                Vector3 textLeft=flame.rectTransform.parent.InverseTransformPoint(label.rectTransform.TransformPoint(Vector3.zero));
                Check(Mathf.Abs(textLeft.x-38)<.1f,"text starts 10 units beside flame, left aligned");
                var card=home.GetComponentsInChildren<RectTransform>().First(t=>t.name=="Assigned tasks summary");
                var action=card.Find("Open latest task action");
                Check(action!=null&&card.GetComponent<Button>()!=null,"highlighted task card and visible action share existing task callback");
                var scrollTop=home.GetComponentInChildren<ScrollRect>();scrollTop.verticalNormalizedPosition=1;yield return null;Canvas.ForceUpdateCanvases();
                Check(texts.Any(t=>t.text==(lang==SupportedLanguage.En?"Move milk · A → B":"Chuyển hộp sữa · A → B")),"Home shows existing labelled trial instead of admin management notice");
                Capture(nav,lang+"-"+theme+"-"+size+"-top",1080,2400);
                var scroll=home.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return null;Canvas.ForceUpdateCanvases();Capture(nav,lang+"-"+theme+"-"+size+"-bottom",1080,2400);
                if(lang==SupportedLanguage.En&&theme=="dark"&&size==1)Capture(nav,"En-dark-alternate",1080,1920);
                Field(home,"tasks",new[]{older,newer});typeof(ImportedHomeDashboard).GetMethod("Paint",Instance).Invoke(home,null);
                Check(home.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==newer.title),"latest assigned task replaces trial when available");
            }
            var chatJson=typeof(OmsMobileApi).GetMethod("ChatJson",Static);var payloads=new List<string>();
            foreach(var item in new[]{("Where is this milk stored?",true),("Please explain FEFO for this photo",true),("Hộp sữa này nằm ở đâu?",false),("Explain this in Vietnamese",false)})
            {
                string json=(string)chatJson.Invoke(null,new object[]{item.Item1,new byte[]{255,216,255,217}});payloads.Add(json);
                Check(item.Item2?json.Contains("Reply in English."):json.Contains("Trả lời bằng tiếng Việt."),"photo payload keeps requested answer language: "+item.Item1);
            }
            Check(JarvisChatLanguage.English("Where is milk?",false)&&!JarvisChatLanguage.English("Sữa ở đâu?",true),"question language overrides UI fallback");
            Check(JarvisChatLanguage.English("SKU-42",true),"ambiguous input uses English UI");
            File.WriteAllLines(Dir+"/language-payloads.jsonl",payloads);
            nav.Settings.SelectTheme(oldTheme);PlayerPrefs.SetInt("JarVis.TextSize",oldSize);loc.Select(oldLanguage);OmsMobileSession.Clear();
            File.WriteAllLines(Dir+"/result.txt",result);EditorApplication.Exit(failed?1:0);
        }
        static void Capture(ImportedFigmaNavigation nav,string name,int width,int height)
        {
            var canvas=nav.GetComponent<Canvas>();var camera=FindAnyObjectByType<Camera>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            foreach(var view in nav.GetComponentsInChildren<ImportedFigmaViewport>(true))view.Fit();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Dir+"/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=oldActive;camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Destroy(image);Destroy(target);
        }
    }
}
