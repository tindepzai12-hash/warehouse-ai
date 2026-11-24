#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class Task00001Demo
    {
        static Task00001Demo()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if(s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("JarVisTask00001Demo",false))
                { SessionState.SetBool("JarVisTask00001Demo",false);new GameObject("Editor-only task card preview").AddComponent<Task00001DemoRunner>(); }
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("JarVisTask00001Demo",true);EditorApplication.isPlaying=true;
        }
    }
    public sealed class Task00001DemoRunner:MonoBehaviour
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\task-00001-demo-20261121";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        readonly List<string> report=new List<string>();
        readonly Dictionary<string,string> preferences=new Dictionary<string,string>();
        readonly string[] strings={"JarVis.Theme","JarVis.Language"};
        readonly string[] ints={"JarVis.TextSize","JarVis.ReduceGlass","JarVis.ReduceMotion"};
        readonly string[] paths={"Assets/Scenes/JarVisFigmaApp.unity","Assets/Scenes/Roompresent_VPS.unity","Assets/Resources/RoompresentCheckedRegistration.json","Assets/JarVis/Scripts/UI/RoompresentDeliveryMission.cs","Assets/JarVis/Scripts/UI/ImportedWarehouseLists.cs"};
        Dictionary<string,byte[]> before;
        ImportedFigmaNavigation nav; TMP_FontAsset heavy; RectTransform card,content;TMP_Text feedback;List<Button> chips;
        string missionFile;byte[] missionBefore;bool missionExisted,started;
        T Get<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Hidden).GetValue(owner);
        void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);
            before=paths.ToDictionary(p=>p,File.ReadAllBytes);
            missionFile=Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json");missionExisted=File.Exists(missionFile);missionBefore=missionExisted?File.ReadAllBytes(missionFile):null;
            foreach(string p in strings)preferences[p]=PlayerPrefs.HasKey(p)?PlayerPrefs.GetString(p):null;
            foreach(string p in ints)preferences[p]=PlayerPrefs.HasKey(p)?PlayerPrefs.GetInt(p).ToString():null;
            yield return null;yield return null;
            try
            {
                nav=FindAnyObjectByType<ImportedFigmaNavigation>();Check(nav!=null&&!OmsMobileSession.IsReady,"Preview uses existing UI without an authenticated backend session");
                nav.Presence.StopAllCoroutines();nav.Presence.enabled=false;
                nav.GetComponent<ImportedAppLocalization>().Select(SupportedLanguage.Vi);nav.Settings.SelectTheme("light");nav.Settings.SelectSize(1);
                foreach(var scanner in nav.GetComponentsInChildren<ImportedCameraScanner>(true))scanner.enabled=false;
                nav.Show("48:244",false);
            }
            catch(Exception e){Finish(e);yield break;}
            yield return null;yield return null;yield return new WaitForSecondsRealtime(.2f);
            try
            {
                var screen=nav.screens.First(s=>s.id=="48:244").root;
                var list=screen.GetComponent<ImportedWarehouseLists>();list.StopAllCoroutines();list.enabled=false;
                content=Get<RectTransform>(list,"content");feedback=Get<TMP_Text>(list,"feedback");chips=Get<List<Button>>(list,"chips");
                var font=screen.GetComponentsInChildren<TMP_Text>().First().font;
                heavy=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/BeVietnamPro-Bold SDF.asset");Check(heavy!=null,"Existing Vietnamese bold font available");
                var heading=screen.GetComponentsInChildren<TMP_Text>().First(t=>t.text=="Nhiệm vụ");heading.font=heavy;heading.fontStyle=FontStyles.Normal;heading.characterSpacing=0;heading.fontSize=22;
                card=content.Cast<Transform>().Single(t=>t.name=="Roompresent two deliveries").GetComponent<RectTransform>();
                foreach(Transform child in content)if(child!=card){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                foreach(Transform child in card){child.gameObject.SetActive(false);Destroy(child.gameObject);}
                card.sizeDelta=new Vector2(366,234);content.sizeDelta=new Vector2(366,250);
                UniformUiSurface.Use(card.GetComponent<Image>(),20,Color.white,PrototypeUi.Hex("DFE7F1"),1,false,false);
                var viewport=(RectTransform)content.parent;viewport.anchoredPosition=new Vector2(23,-190);viewport.sizeDelta=new Vector2(366,612);
                var title=PrototypeUi.Text(card,font,"NHIỆM VỤ #00001 CẤT HÀNG",18,15,330,29,14,false,true);UseBold(title);
                var route=PrototypeUi.Text(card,font,"A1 → B  •  A2 → C",18,53,330,35,23,false,true);UseBold(route);
                var help=PrototypeUi.Text(card,font,"• Quét barcode của hai sản phẩm A1 và A2.\n• Quét QR tại vị trí hiện tại để căn bản đồ.\n• Đi theo chỉ dẫn: A1 tới B, A2 tới C.",18,99,330,69,12,false);help.color=PrototypeUi.Hex("465B75");help.characterSpacing=0;
                var start=PrototypeUi.Action(card,font,"Bắt đầu nhiệm vụ",18,177,330,44,false,()=>{started=true;nav.Notice("Demo: tiếp theo quét barcode A1 và A2, rồi quét QR vị trí.",false,4);},true);start.name="Start mission 00001";UseBold(start.GetComponentInChildren<TMP_Text>());
                // The button's newly-created sibling shadow used its temporary (0,0) placement
                // in immediate captures. Use flat surfaces locally for this requested uniform demo.
                foreach(var shadow in card.GetComponentsInChildren<PrototypeGlassShadow>(true))shadow.enabled=false;
                UniformUiSurface.Use(start.GetComponent<Image>(),15,PrototypeUi.Accent,Color.clear,0,false,false);
                float[] widths={86,100,82,86};float x=0;
                for(int i=0;i<4;i++)
                {
                    int selected=i;var b=chips[i];b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>Select(selected));var r=(RectTransform)b.transform;r.anchoredPosition=new Vector2(x,-4);r.sizeDelta=new Vector2(widths[i],38);r.localScale=Vector3.one;
                    var t=b.GetComponentInChildren<TMP_Text>();t.rectTransform.anchoredPosition=new Vector2(4,0);t.rectTransform.sizeDelta=new Vector2(widths[i]-8,38);t.fontSize=11;t.characterSpacing=0;UseBold(t);x+=widths[i]+4;
                }
                Get<RectTransform>(list,"filters").sizeDelta=new Vector2(366,54);
                Select(0);
                Check(!card.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Thử từ đầu")||t.text.Contains("Tiếp tục")),"No restart or Continue control on the preview card");
                Check(!feedback.gameObject.activeSelf&&feedback.text=="","Unrequested helper line removed without retaining the empty row");
                Check(!card.GetComponentsInChildren<PrototypeGlassShadow>().Any(s=>s.enabled),"No active title-overlapping button shadow");
                Capture("01-all");Select(1);Capture("02-working");Select(2);Capture("03-assigned-zero");Select(3);Capture("04-completed-zero");Select(0);
                start.onClick.Invoke();Check(started,"Start button accepts a click without writing the real mission");
                foreach(var p in before)Check(File.ReadAllBytes(p.Key).SequenceEqual(p.Value),"Unchanged production: "+p.Key);
                Check(File.Exists(missionFile)==missionExisted&&(!missionExisted||File.ReadAllBytes(missionFile).SequenceEqual(missionBefore)),"Saved mission data unchanged");
                Finish(null);
            }
            catch(Exception e){Finish(e);}
        }
        void UseBold(TMP_Text t){t.font=heavy;t.fontStyle=FontStyles.Normal;t.characterSpacing=0;}
        void Select(int choice)
        {
            string[] names={"Tất cả (1)","Đang làm (1)","Đã giao (0)","Hoàn tất (0)"};
            for(int i=0;i<4;i++)
            {
                var t=chips[i].GetComponentInChildren<TMP_Text>();t.text=names[i];t.color=PrototypeUi.Hex(i==choice?"1255D5":"52657B");
                UniformUiSurface.Use(chips[i].GetComponent<Image>(),14,PrototypeUi.Hex(i==choice?"E1ECFF":"FFFFFF"),PrototypeUi.Hex(i==choice?"A4BFE9":"DFE7F1"),1,false,false);
            }
            card.gameObject.SetActive(choice<2);
            feedback.text=choice<2?"":choice==2?"Chưa có nhiệm vụ đã giao.":"Chưa có nhiệm vụ hoàn tất.";
            feedback.gameObject.SetActive(choice>=2);
        }
        void Capture(string state)
        {
            Directory.CreateDirectory("Logs/PrototypeParity");
            foreach(int height in new[]{2400,1920})
            {
                Canvas.ForceUpdateCanvases();
                foreach(var t in card.GetComponentsInChildren<TMP_Text>())
                {t.ForceMeshUpdate();var size=t.GetPreferredValues(t.text,t.rectTransform.rect.width,10000);Check(size.y<=t.rectTransform.rect.height+2,"Text bounds "+state+": "+t.text.Replace('\n',' '));}
                string name="task00001-"+state+"-"+height+".png";
                PrototypeParityRunner.Capture(nav,name,1080,height);File.Copy("Logs/PrototypeParity/"+name,Path.Combine(Dir,name),true);
            }
        }
        void Finish(Exception e)
        {
            foreach(string p in strings){if(preferences.TryGetValue(p,out var value)&&value!=null)PlayerPrefs.SetString(p,value);else PlayerPrefs.DeleteKey(p);}
            foreach(string p in ints){if(preferences.TryGetValue(p,out var value)&&value!=null)PlayerPrefs.SetInt(p,int.Parse(value));else PlayerPrefs.DeleteKey(p);}PlayerPrefs.Save();
            File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),new[]{(e==null?"PASS ":"FAIL ")+DateTime.UtcNow.ToString("O")}.Concat(report).Concat(new[]{e==null?"UI-only Editor preview on actual Tasks screen. Counts are requested presentation fixtures, not backend totals. No scene save, stock write, task reset, Android install or camera test.":e.ToString()}));
            if(e!=null)Debug.LogException(e);EditorApplication.Exit(e==null?0:1);
        }
    }
}
#endif
