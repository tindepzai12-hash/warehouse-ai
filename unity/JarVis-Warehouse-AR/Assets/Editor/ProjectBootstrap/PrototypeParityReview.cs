using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class PrototypeParityReview
    {
        static double deadline;static bool running;
        static PrototypeParityReview(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("PrototypeParityReview",false)){SessionState.SetBool("PrototypeParityReview",false);new GameObject("Native prototype QA").AddComponent<PrototypeParityRunner>();}};EditorApplication.update+=()=>{if(running&&EditorApplication.timeSinceStartup>deadline){File.WriteAllText("Logs/PrototypeParity/Result.txt","FAIL: review watchdog timeout");EditorApplication.Exit(1);}};}
        public static void Run(){Directory.CreateDirectory("Logs/PrototypeParity");deadline=EditorApplication.timeSinceStartup+180;running=true;EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("PrototypeParityReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class PrototypeParityRunner:MonoBehaviour
    {
        readonly List<string> failures=new List<string>();readonly List<string> checks=new List<string>();
        void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures.Add(name);}
        void Error(string message,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)failures.Add(message.Split('\n')[0]);}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Error;yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var language=nav.GetComponent<ImportedAppLocalization>();
            Check(nav.CurrentScreen!=null,"Navigation initialization completed before fixtures");
            string oldTheme=nav.Settings.Theme;int oldSize=PlayerPrefs.GetInt("JarVis.TextSize",1);var oldLanguage=JarvisLocalization.CurrentLanguage;
            string[] ids={"126:8","48:244","5:780","prototype:profile","prototype:activity","5:1308","prototype:tools","prototype:transactions","5:935","14:387","5:450","5:1029","5:331","215:2","5:1216","5:36"};
            language.Select(SupportedLanguage.Vi);nav.Settings.SelectTheme("light");PlayerPrefs.SetInt("JarVis.TextSize",1);
            var presence=new OmsMobileApi.PresenceReply{status="ONLINE",todaySeconds=5058,sessionSeconds=120,serverNow=DateTimeOffset.UtcNow.ToString("O"),nextResetAt=DateTimeOffset.UtcNow.AddHours(8).ToString("O"),leaseUntil=DateTimeOffset.UtcNow.AddMinutes(2).ToString("O"),streak=new OmsMobileApi.ActivityStreak{currentStreak=5,minimumSeconds=60,todayQualified=true,week=Enumerable.Range(0,7).Select(i=>new OmsMobileApi.ActivityDay{qualified=i<5}).ToArray()}};
            typeof(ImportedPresence).GetMethod("Accept",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(nav.Presence,new object[]{presence});
            // Fixture exists only in this Editor runner; no database/session/API or model calls.
            var home=nav.screens.First(s=>s.id=="126:8").root.GetComponent<ImportedHomeDashboard>();
            typeof(ImportedHomeDashboard).GetField("tasks",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(home,new[]{new OmsMobileApi.TaskRow{title="Kiểm đếm hàng tại cửa A",task_code="TK-DEMO-001",status="IN_PROGRESS",locationCode="A-01-01",task_type="Kiểm đếm nhập"}});
            foreach(string id in ids)
            {
                nav.Show(id,false);yield return new WaitForSecondsRealtime(.45f);Canvas.ForceUpdateCanvases();
                var root=nav.screens.First(s=>s.id==id).root;Check(root.activeSelf,"Route "+id);
                if(id=="48:244"||id=="5:780")
                {
                    var lists=root.GetComponent<ImportedWarehouseLists>();var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    typeof(ImportedWarehouseLists).GetField("hasData",flags).SetValue(lists,true);
                    if(id=="48:244")typeof(ImportedWarehouseLists).GetField("taskRows",flags).SetValue(lists,new[]{new OmsMobileApi.TaskRow{task_code="TK-DEMO-001",title="Kiểm đếm hàng tại cửa A",status="IN_PROGRESS",locationCode="A-01-01",task_type="Kiểm đếm nhập",sku="SKU-DEMO-01",productName="Sản phẩm mẫu A",employeeCode="NV-DEMO-01",expected_quantity=12,unit="cái"},new OmsMobileApi.TaskRow{task_code="TK-DEMO-002",title="Đối chiếu lô hàng tại khu B",status="ASSIGNED",locationCode="B-03-02",task_type="Xuất kho"},new OmsMobileApi.TaskRow{task_code="TK-DEMO-003",title="Xác nhận số lượng hàng xuất",status="COMPLETED",locationCode="A-01-01",task_type="QC"}});
                    else typeof(ImportedWarehouseLists).GetField("stockRows",flags).SetValue(lists,new[]{new OmsMobileApi.InventoryRow{sku="SKU-DEMO-01",name="Sản phẩm mẫu A",lotCode="LOT-DEMO-A",locationCode="A-01-01",available=24,onHand=26,reserved=2,unit="cái"},new OmsMobileApi.InventoryRow{sku="SKU-DEMO-02",name="Sản phẩm mẫu B",lotCode="LOT-DEMO-B",locationCode="B-03-02",available=12,onHand=12,unit="cái"}});
                    typeof(ImportedWarehouseLists).GetMethod("Render",flags).Invoke(lists,null);yield return null;
                    Check(root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith(id=="48:244"?"Task TK-":"Stock SKU-"))==(id=="48:244"?3:2),"Live list template renders Editor fixture "+id);
                }
                foreach(var text in root.GetComponentsInChildren<TMP_Text>(false)){text.ForceMeshUpdate();if(text.isTextTruncated)Check(false,id+" clipped: "+text.text);}
                Check(root.GetComponentsInChildren<Button>(false).Any(),"Interactive controls "+id);
                Capture(nav,id.Replace(':','-')+"-light.png",824,1830);
                if(id=="48:244"||id=="5:780")
                {
                    root.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith(id=="48:244"?"Task TK-":"Stock SKU-")).onClick.Invoke();yield return null;Capture(nav,id=="48:244"?"Task-details.png":"Stock-details.png",824,1830);
                    Check(root.GetComponentInChildren<PrototypeDialog>()!=null,"Details use prototype modal "+id);Check(root.GetComponent<ImportedWarehouseLists>().CloseDetail(),"Detail close returns to list "+id);yield return null;
                }
                if(id=="126:8")
                {
                    Check(root.GetComponentsInChildren<TMP_Text>(false).Any(t=>t.name=="Presence state"&&t.text=="Trực tuyến"),"Home status updates immediately from server snapshot");
                    Check(!root.GetComponentsInChildren<TMP_Text>(false).Any(t=>t.name=="Today online"),"Home hides timer");
                    var quick=root.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("Quick action")).ToArray();Check(quick.Length==4,"Four quick actions");
                    Check(quick.Any(b=>b.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Nhập / Xuất kho")),"Inbound/outbound replaces quick scan");
                    var avatar=root.GetComponentInChildren<UserPortraitGraphic>();Check(avatar!=null&&avatar.isActiveAndEnabled,"Justin avatar visible");
                    var shadowImages=root.GetComponentsInChildren<Image>().Where(i=>i.name=="Prototype glass shadow").ToArray();File.WriteAllLines("Logs/PrototypeParity/ShadowMaterials.txt",shadowImages.Select(i=>i.material.shader.name+" / rendered "+i.materialForRendering.shader.name+" size "+i.material.GetVector("_Size")+" components "+string.Join(",",i.GetComponents<Component>().Select(c=>c.GetType().Name))));
                    foreach(var s in shadowImages)s.enabled=false;Capture(nav,"Home-without-shadow.png",824,1830);foreach(var s in shadowImages)s.enabled=true;
                    var profile=root.GetComponentsInChildren<Button>(false).First(b=>b.name=="Profile");Check(Hit(profile),"Avatar profile receives pointer input");
                    var operation=quick.First(b=>b.name=="Quick action arrowDown");operation.onClick.Invoke();yield return null;Check(nav.CurrentScreen=="prototype:transactions","Quick action opens transaction selector");nav.Show(id,false);yield return null;
                    PrototypeDialog.Presence(nav);yield return null;Check(root.GetComponentInChildren<PrototypeDialog>()!=null,"Go offline confirmation opens");Capture(nav,"Confirm-offline.png",824,1830);root.GetComponentInChildren<PrototypeDialog>().GetComponentsInChildren<Button>().First().onClick.Invoke();yield return null;Check(nav.Presence.CanWork,"Cancel keeps online state");
                    var actor=home.GetComponentInChildren<PrototypeMascot>();actor.PreviewTime=.5f;yield return null;Capture(nav,"JarVis-happy.png",824,1830);var rect=(RectTransform)actor.transform.Find("Transparent JarVis actor");float happyY=rect.anchoredPosition.y;actor.PreviewTime=5.5f;yield return null;Check(Mathf.Abs(rect.anchoredPosition.y-happyY)<.01f&&happyY>0,"Mascot celebration repeats every five seconds");actor.PreviewTime=3.4f;yield return null;Capture(nav,"JarVis-blink.png",824,1830);Check(actor.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Eyelid")).All(i=>i.enabled),"Mascot blinks");actor.PreviewTime=-1;
                    var fire=home.GetComponentInChildren<PrototypeFlame>();fire.PreviewTime=0;yield return null;Capture(nav,"Flame-0.png",824,1830);fire.PreviewTime=.24f;yield return null;Capture(nav,"Flame-1.png",824,1830);fire.PreviewTime=-1;
                }
                if(id=="5:331")
                {
                    Check(nav.BottomNavigation.activeSelf,"Scanner keeps shared dock");var sweep=root.GetComponentInChildren<ScannerSweep>();var line=(RectTransform)sweep.transform.Find("Red scanning sweep");var frame=(RectTransform)sweep.transform;
                    for(int i=0;i<8;i++){yield return new WaitForSecondsRealtime(.15f);Check(line.rect.width<=frame.rect.width&&-line.anchoredPosition.y-line.rect.height/2>=0&&-line.anchoredPosition.y+line.rect.height/2<=frame.rect.height,"Scan sweep remains in blue frame "+i);}
                    var lookup=root.GetComponentsInChildren<Button>().First(b=>b.name=="Lookup code");Check(lookup.GetComponent<Image>().enabled&&lookup.GetComponentInChildren<TMP_Text>().isActiveAndEnabled,"Manual code lookup visible");
                }
                if(id=="5:36")Check(!root.GetComponentsInChildren<ImportedUtilityIcon>(false).Any(i=>i.enabled),"Login has no leftover overlapping input icons");
                if(id=="prototype:activity")Check(root.GetComponentsInChildren<TMP_Text>(false).Any(t=>t.text.StartsWith("01:24:")),"Activity uses existing presence total");
                if(id=="5:1216")
                {
                    var chat=root.GetComponent<ImportedJarvisChat>();Check(chat.Media.ExpandButton.gameObject.activeSelf&&!chat.Media.PhotoButton.gameObject.activeSelf&&!chat.Media.MicButton.gameObject.activeSelf,"Only plus shown in composer");
                    chat.Media.ExpandButton.onClick.Invoke();yield return null;Check(chat.Media.PlusMenuOpen,"Plus menu opens");Capture(nav,"JarVis-plus-menu.png",824,1830);chat.Media.TogglePlusMenu();
                }
            }
            foreach(string theme in new[]{"light","dark"})foreach(int size in new[]{1,2})
            {
                nav.Settings.SelectTheme(theme);PlayerPrefs.SetInt("JarVis.TextSize",size);nav.Show("126:8",false);yield return new WaitForSecondsRealtime(.6f);
                Capture(nav,"Home-"+theme+"-"+size+".png",824,1830);Capture(nav,"Home-"+theme+"-"+size+"-alternate.png",780,1688);
                foreach(var text in home.GetComponentsInChildren<TMP_Text>(false)){text.ForceMeshUpdate();if(text.isTextTruncated)Check(false,theme+" "+size+" clipped: "+text.text);}
            }
            nav.Settings.SelectTheme("light");PlayerPrefs.SetInt("JarVis.TextSize",1);nav.Show("126:8",false);yield return new WaitForSecondsRealtime(.4f);Capture(nav,"Home-device.png",1080,2400);
            language.Select(SupportedLanguage.En);foreach(string id in new[]{"126:8","prototype:activity","5:331","5:1308","5:36"}){nav.Show(id,false);yield return new WaitForSecondsRealtime(.4f);Capture(nav,id.Replace(':','-')+"-English.png",824,1830);foreach(var text in nav.screens.First(s=>s.id==id).root.GetComponentsInChildren<TMP_Text>(false)){text.ForceMeshUpdate();if(text.isTextTruncated)Check(false,"English clipped: "+text.text);}}language.Select(SupportedLanguage.Vi);
            nav.Show("85:14",false);yield return new WaitForSecondsRealtime(.6f);Capture(nav,"Offline.png",824,1830);
            nav.Show("prototype:profile",false);yield return null;nav.Show("prototype:activity",false);yield return new WaitForSecondsRealtime(.3f);Check(nav.CurrentScreen=="prototype:activity","Profile/activity navigation");
            var accept=typeof(ImportedPresence).GetMethod("Accept",BindingFlags.Instance|BindingFlags.NonPublic);var paint=typeof(ImportedPresence).GetMethod("Paint",BindingFlags.Instance|BindingFlags.NonPublic);
            var paused=new OmsMobileApi.PresenceReply{status="OFFLINE",todaySeconds=123};accept.Invoke(nav.Presence,new object[]{paused});paint.Invoke(nav.Presence,null);string pausedClock=nav.Presence.TodayClock;yield return new WaitForSecondsRealtime(1.2f);paint.Invoke(nav.Presence,null);Check(nav.Presence.TodayClock==pausedClock&&pausedClock=="00:02:03","Offline pauses existing daily time");
            var resumed=new OmsMobileApi.PresenceReply{status="ONLINE",todaySeconds=123,serverNow=DateTimeOffset.UtcNow.ToString("O"),nextResetAt=DateTimeOffset.UtcNow.AddHours(2).ToString("O"),leaseUntil=DateTimeOffset.UtcNow.AddMinutes(2).ToString("O")};accept.Invoke(nav.Presence,new object[]{resumed});yield return new WaitForSecondsRealtime(1.2f);paint.Invoke(nav.Presence,null);Check(string.CompareOrdinal(nav.Presence.TodayClock,"00:02:03")>0,"Going online continues the same daily total");
            resumed.todaySeconds=7200;resumed.serverNow=DateTimeOffset.UtcNow.ToString("O");resumed.nextResetAt=DateTimeOffset.UtcNow.AddSeconds(-2).ToString("O");accept.Invoke(nav.Presence,new object[]{resumed});paint.Invoke(nav.Presence,null);Check(nav.Presence.TodayClock.StartsWith("00:00:"),"New day uses server Taiwan-midnight boundary instead of previous total");
            nav.Settings.SelectTheme(oldTheme);PlayerPrefs.SetInt("JarVis.TextSize",oldSize);language.Select(oldLanguage);nav.Show("5:36",false);
            Application.logMessageReceived-=Error;File.WriteAllText("Logs/PrototypeParity/Result.txt",(failures.Count==0?"PASS":"FAIL")+" "+DateTime.UtcNow.ToString("O")+"\nNative uGUI; fixture values only, no API/model calls. Main 412x915, alternate 390x844.\n"+string.Join("\n",checks)+"\nErrors:\n"+string.Join("\n",failures));EditorApplication.Exit(failures.Count==0?0:1);
        }
        bool Hit(Button button)
        {
            var canvas=button.GetComponentInParent<Canvas>();var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center));
            var pointer=new PointerEventData(EventSystem.current){position=point};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
        }
        public static void Capture(ImportedFigmaNavigation nav,string name,int width,int height)
        {
            var canvas=nav.GetComponent<Canvas>();var camera=FindAnyObjectByType<Camera>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;Canvas.ForceUpdateCanvases();nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();Canvas.ForceUpdateCanvases();foreach(var t in nav.GetComponentsInChildren<TMP_Text>(false))t.ForceMeshUpdate();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes("Logs/PrototypeParity/"+name,image.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;RenderTexture.active=oldActive;Canvas.ForceUpdateCanvases();nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();Destroy(image);Destroy(target);}
        }
    }
}
