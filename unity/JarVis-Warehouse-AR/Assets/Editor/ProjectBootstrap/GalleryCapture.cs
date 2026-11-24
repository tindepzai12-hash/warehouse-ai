#if UNITY_EDITOR
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
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class GalleryCapture
    {
        static double deadline;
        static GalleryCapture()
        {
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarvisGallery",false)){SessionState.SetBool("JarvisGallery",false);new GameObject("Gallery capture only").AddComponent<GalleryCaptureRunner>();}};
            EditorApplication.update+=()=>{if(deadline>0&&EditorApplication.timeSinceStartup>deadline)EditorApplication.Exit(2);};
        }
        public static void Run()
        {
            deadline=EditorApplication.timeSinceStartup+300;
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("JarvisGallery",true);EditorApplication.isPlaying=true;
        }
        public static void RunExtra(){SessionState.SetBool("JarvisGalleryExtra",true);Run();}
    }
    public sealed class GalleryCaptureRunner:MonoBehaviour
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\JarVis-UI-Gallery-20261121\captures";
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        ImportedFigmaNavigation nav;
        readonly List<string> errors=new List<string>();
        readonly List<string> manifest=new List<string>{"file\tgroup\tstate\tlanguage\ttheme\tframe\tvisible_text"};
        readonly Dictionary<string,string> prefs=new Dictionary<string,string>();
        readonly string[] stringKeys={"JarVis.Theme","JarVis.Language"};
        readonly string[] intKeys={"JarVis.TextSize","JarVis.ReduceGlass","JarVis.ReduceMotion"};
        void Error(string m,string t,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(m.Split('\n')[0]);}
        static object Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,Hidden).Invoke(owner,args);
        static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Hidden).SetValue(owner,value);
        IEnumerator Wait(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.12f);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);Application.logMessageReceived+=Error;
            foreach(string k in stringKeys)prefs[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetString(k):null;
            foreach(string k in intKeys)prefs[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetInt(k).ToString():null;
            yield return null;yield return null;
            nav=FindAnyObjectByType<ImportedFigmaNavigation>();
            if(nav==null||OmsMobileSession.IsReady){errors.Add("Unexpected authenticated session or missing UI");Finish();yield break;}
            nav.Presence.StopAllCoroutines();nav.Presence.enabled=false;
            var locale=nav.GetComponent<ImportedAppLocalization>();
            var original=JarvisLocalization.CurrentLanguage;
            foreach(var scanner in nav.GetComponentsInChildren<ImportedCameraScanner>(true))scanner.enabled=false;
            foreach(var input in nav.screens.First(s=>s.id=="5:36").root.GetComponentsInChildren<TMP_InputField>(true))input.text="";
            var presence=new OmsMobileApi.PresenceReply{status="ONLINE",todaySeconds=5058,sessionSeconds=120,serverNow=DateTimeOffset.UtcNow.ToString("O"),nextResetAt=DateTimeOffset.UtcNow.AddHours(8).ToString("O"),leaseUntil=DateTimeOffset.UtcNow.AddHours(1).ToString("O"),streak=new OmsMobileApi.ActivityStreak{currentStreak=5,todayQualified=true,minimumSeconds=60,week=Enumerable.Range(0,7).Select(i=>new OmsMobileApi.ActivityDay{qualified=i<5}).ToArray()}};
            Call(nav.Presence,"Accept",presence);
            var tasks=new[]{new OmsMobileApi.TaskRow{title="Giao vở A1 tại B và A2 tại C",task_code="TK-DEMO-001",status="IN_PROGRESS",locationCode="Roompresent",task_type="Giao hàng",sku="CPNB-1004",productName="Vở A1",expected_quantity=2,unit="quyển"},new OmsMobileApi.TaskRow{title="Kiểm đếm kệ hàng C",task_code="TK-DEMO-002",status="ASSIGNED",locationCode="C",task_type="Kiểm kê"},new OmsMobileApi.TaskRow{title="Đối chiếu hàng đã nhận",task_code="TK-DEMO-003",status="COMPLETED",locationCode="A",task_type="QC"}};
            var stocks=new[]{new OmsMobileApi.InventoryRow{productId="gallery-a1",sku="CPNB-1004",name="Vở A1",barcode=RoompresentDeliveryRecord.A1,lotCode="DEMO-A",locationCode="B",available=24,onHand=26,reserved=2,unit="quyển"},new OmsMobileApi.InventoryRow{productId="gallery-a2",sku="CPNB-1003",name="Vở A2",barcode=RoompresentDeliveryRecord.A2,lotCode="DEMO-B",locationCode="C",available=12,onHand=12,unit="quyển"}};
            if(SessionState.GetBool("JarvisGalleryExtra",false))
            {
                SessionState.SetBool("JarvisGalleryExtra",false);
                manifest.Clear();manifest.AddRange(File.ReadAllLines(Path.Combine(Dir,"manifest.tsv")));
                foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})foreach(string theme in new[]{"light","dark"})
                {
                    locale.Select(language);nav.Settings.SelectTheme(theme);nav.Settings.SelectSize(1);
                    nav.Show("5:450",false);yield return Wait();var workspace=nav.screens.First(s=>s.id=="5:450").root.GetComponent<WarehouseProductWorkspace>();workspace.AcceptInventory(stocks);
                    foreach(string tab in new[]{"labels","manage"}){Set(workspace,"tab",tab);Set(workspace,"mode","list");Call(workspace,"Build");yield return Wait();Capture("05-products","tab-"+tab,"fullscreen");}
                    workspace.BeginCreate();workspace.SelectTemplate("drink-beverages-sua-tuoi");Set(workspace,"additional",true);Set(workspace,"codesExpanded",true);Call(workspace,"Build");yield return Wait();Capture("05-products","form-additional-top","fullscreen");
                    var scroll=workspace.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return Wait();Capture("05-products","form-codes-bottom","fullscreen");
                    workspace.SetValue("volume","");workspace.ReviewDraft();yield return Wait();Capture("05-products","form-validation","fullscreen");
                    nav.Show("215:2",false);yield return Wait();var map=nav.screens.First(s=>s.id=="215:2").root.GetComponent<WarehouseMapScreen>();Set(map,"showLegacyTests",true);Call(map,"Render");yield return Wait();Capture("01-screens","map-advanced","fullscreen");
                    nav.Show("5:1308",false);nav.Settings.SelectSize(2);yield return Wait();Capture("03-settings","large-text","fullscreen");nav.Settings.SelectSize(1);
                }
                locale.Select(original);Finish();yield break;
            }
            Set(nav.screens.First(s=>s.id=="126:8").root.GetComponent<ImportedHomeDashboard>(),"tasks",tasks);
            foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})foreach(string theme in new[]{"light","dark"})
            {
                locale.Select(language);nav.Settings.SelectTheme(theme);nav.Settings.SelectSize(1);
                foreach(var screen in nav.screens)
                {
                    nav.Show(screen.id,false);yield return Wait();
                    var lists=screen.root.GetComponent<ImportedWarehouseLists>();
                    if(lists!=null){Set(lists,"hasData",true);Set(lists,"taskRows",tasks);Set(lists,"stockRows",stocks);Call(lists,"Render");yield return Wait();}
                    Capture("01-screens",screen.id.Replace(':','-'),"fullscreen");
                    if(screen.id=="48:244"||screen.id=="5:780")
                    {
                        var row=screen.root.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name.StartsWith(screen.id=="48:244"?"Task TK-":"Stock CPNB-"));
                        if(row!=null){row.onClick.Invoke();yield return Wait();Capture("02-popups",screen.id=="48:244"?"task-details":"stock-details","modal");lists.CloseDetail();yield return Wait();}
                    }
                }
                nav.Show("5:1308",false);yield return Wait();
                var sr=nav.screens.First(s=>s.id=="5:1308").root.GetComponentInChildren<ScrollRect>();
                if(sr!=null){sr.verticalNormalizedPosition=0;yield return Wait();Capture("03-settings","settings-bottom","fullscreen");sr.verticalNormalizedPosition=1;}
                foreach(string kind in new[]{"language","password","size","theme"}){nav.Settings.Open(kind);yield return Wait();Capture("03-settings",kind,"fullscreen");nav.Settings.Close();yield return Wait();}
                nav.Show("prototype:profile",false);yield return Wait();PrototypeDialog.SignOut(nav);yield return Wait();Capture("02-popups","sign-out","modal");ClearDialogs();yield return Wait();
                nav.Show("126:8",false);yield return Wait();PrototypeDialog.Presence(nav);yield return Wait();Capture("02-popups","go-offline","modal");ClearDialogs();yield return Wait();
                nav.Show("5:331",false);yield return Wait();
                foreach(string code in new[]{RoompresentDeliveryRecord.A1,RoompresentDeliveryRecord.A2})
                {RoompresentScanPopup.Show(nav,code,code==RoompresentDeliveryRecord.A2,()=>{});yield return Wait();Capture("02-popups",code==RoompresentDeliveryRecord.A1?"book-A1-success":"book-A2-success","modal");ClearDialogs();yield return Wait();}
                nav.Show("5:1216",false);yield return Wait();
                var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();
                chat.AddMessage(language==SupportedLanguage.Vi?"Tôi cần hỗ trợ nhiệm vụ giao A1 và A2.":"Help me with the A1 and A2 delivery task.",true);
                chat.AddMessage(language==SupportedLanguage.Vi?"A1 cần giao tại kệ B, A2 tại kệ C. Quét QR điểm giao rồi barcode sách tương ứng để xác nhận.":"Deliver A1 to B and A2 to C. Scan the destination QR, then the matching book barcode to confirm.",false);
                yield return Wait();Capture("04-chat","conversation","fullscreen");
                chat.Media.TogglePlusMenu();yield return Wait();Capture("04-chat","attachment-menu","popover");chat.Media.ClosePlusMenu();
                chat.Media.LoadPhoto("Assets/Resources/Figma/JarVisMascotLogin.png");yield return Wait();Capture("04-chat","photo-composer","fullscreen");chat.Media.ClearPhoto();
                Call(chat,"SendReferenceMap",language==SupportedLanguage.En);yield return Wait();Capture("04-chat","map-message","fullscreen");Call(chat,"ShowMapPreview");yield return Wait();Capture("04-chat","map-preview","modal");Call(chat,"CloseMapPreview");
                typeof(VoiceMessageReviewRunner).GetMethod("InstallStoppedFixture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{chat.Media.Recorder,3});chat.RefreshComposer();yield return Wait();Capture("04-chat","voice-recorded","composer");
                var voice=chat.Media.Recorder.FinalizeMessage();chat.AddVoiceMessage(voice,chat.Media.Playback,chat.Media.RetryVoice);chat.RefreshComposer();yield return Wait();Capture("04-chat","voice-message","fullscreen");chat.Media.CancelRecording();
                nav.Show("5:450",false);yield return Wait();var product=nav.screens.First(s=>s.id=="5:450").root.GetComponent<WarehouseProductWorkspace>();
                product.AcceptInventory(stocks);yield return Wait();Capture("05-products","product-list","fullscreen");
                var productRow=product.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Product row CPNB-1004");
                if(productRow!=null){productRow.onClick.Invoke();yield return Wait();Capture("05-products","label-preview","fullscreen");product.TryBack();yield return Wait();}
                product.BeginCreate();yield return Wait();Capture("05-products","templates","fullscreen");product.SelectTemplate("drink-beverages-sua-tuoi");yield return Wait();Capture("05-products","product-form","fullscreen");
                var choice=product.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Product choice volumeUnit");
                if(choice!=null){choice.onClick.Invoke();yield return Wait();Capture("05-products","unit-choice","fullscreen");product.TryBack();yield return Wait();}
                product.SetValue("volume","180");if(product.ReviewDraft()){yield return Wait();Capture("05-products","draft-confirmation","fullscreen");}
                product.Back();
                nav.Show("5:36",false);yield return Wait();nav.Notice(language==SupportedLanguage.Vi?"Vui lòng nhập tên đăng nhập và mật khẩu.":"Enter your username and password.",true,1);yield return Wait();Capture("02-popups","login-validation","notice");yield return new WaitForSecondsRealtime(1.1f);
            }
            locale.Select(original);Finish();
        }
        void ClearDialogs(){foreach(var d in nav.GetComponentsInChildren<PrototypeDialog>(true)){d.gameObject.SetActive(false);Destroy(d.gameObject);}}
        void Capture(string group,string state,string frame)
        {
            var language=JarvisLocalization.CurrentLanguage.ToString();string theme=nav.Settings.Dark?"dark":"light";
            string relative=group+"/"+language+"-"+theme+"/"+state+".png";string path=Path.Combine(Dir,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrototypeParityRunner.Capture(nav,"__gallery.png",824,1830);File.Copy("Logs/PrototypeParity/__gallery.png",path,true);
            var visible=nav.GetComponentsInChildren<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(t.text)).Select(t=>t.text.Replace('\t',' ').Replace('\n',' '));
            manifest.Add(relative+"\t"+group+"\t"+state+"\t"+language+"\t"+theme+"\t"+frame+"\t"+string.Join(" | ",visible));
            File.WriteAllLines(Path.Combine(Dir,"manifest.tsv"),manifest);
        }
        void Finish()
        {
            foreach(var k in stringKeys){if(prefs.TryGetValue(k,out var v)&&v!=null)PlayerPrefs.SetString(k,v);else PlayerPrefs.DeleteKey(k);}
            foreach(var k in intKeys){if(prefs.TryGetValue(k,out var v)&&v!=null)PlayerPrefs.SetInt(k,int.Parse(v));else PlayerPrefs.DeleteKey(k);}
            PlayerPrefs.Save();Application.logMessageReceived-=Error;
            File.WriteAllLines(Path.Combine(Dir,"capture-result.txt"),new[]{"Runtime UI captures; Editor-only in-memory sample data; no API, AI, scene save, mission reset or Android writes.","Images: "+(manifest.Count-1)}.Concat(errors));
            EditorApplication.Exit(errors.Count==0?0:1);
        }
    }
}
#endif
