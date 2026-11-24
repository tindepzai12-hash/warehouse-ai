#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
namespace ProjectBootstrap
{
    public static class ArThemeLogoReview
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\compact-ui-20261121\theme-and-logo";
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> checks=new List<string>();
        static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,F).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
        static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
        static void Check(bool ok,string m){checks.Add((ok?"PASS ":"FAIL ")+m);if(!ok)throw new Exception(m);}
        public static void Run()
        {
            Directory.CreateDirectory(Dir);checks.Clear();Canvas canvas=null;GameObject reference=null;Texture2D photo=null;
            var lang=JarvisLocalization.CurrentLanguage;string theme=PlayerPrefs.HasKey("JarVis.Theme")?PlayerPrefs.GetString("JarVis.Theme"):null;
            var current=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.NonPublic|BindingFlags.Static);var original=current.GetValue(null);
            string disk=Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json");
            string[] guards={"Assets/Resources/RoompresentCheckedRegistration.json","Assets/Scenes/JarVisFigmaApp.unity",RoompresentVpsBuild.ScenePath,"Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryVerification.cs","Assets/JarVis/Scripts/Navigation/RoompresentQrLocalization.cs","Assets/JarVis/Scripts/UI/ImportedPresence.cs","Assets/Resources/Prototype/JarVis.png","Assets/Resources/Prototype/JarVisExpressionBase.png",disk};
            var bytes=guards.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            try
            {
                foreach(string themeMode in new[]{"light","dark"})
                {
                bool dark=themeMode=="dark";PlayerPrefs.SetString("JarVis.Theme",themeMode);Check(ImportedSettings.CurrentDark==dark,"Shared app appearance resolver: "+themeMode);
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");var app=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);var font=app.GetComponentInChildren<TMP_Text>(true).font;
                checks.Add("FONT "+AssetDatabase.GetAssetPath(font));
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);JarvisLocalization.SetLanguage(SupportedLanguage.Vi);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();var origin=UnityEngine.Object.FindFirstObjectByType<XROrigin>();var camera=origin.Camera;var manager=camera.GetComponent<ARCameraManager>();
                var qr=guide.qrLocalization;if(qr==null){qr=guide.gameObject.AddComponent<RoompresentQrLocalization>();qr.enabled=false;guide.qrLocalization=qr;}
                reference=new GameObject("Inactive Editor reference fixture");reference.SetActive(false);reference.transform.SetPositionAndRotation(new Vector3(.7f,.1f,-1.2f),Quaternion.Euler(0,17,0));var anchor=reference.AddComponent<ARAnchor>();Set(qr,"anchor",anchor);Set(qr,"referenceFrame",reference.transform);
                var record=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true,preferredStop="C"};current.SetValue(null,record);Set(guide,"activeStop","C");Set(guide,"<StartAligned>k__BackingField",true);
                Call(guide,"MakeUi",font);canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
                var safe=canvas.GetComponentInChildren<RoompresentDeliveryHud>().transform;var hud=safe.GetComponent<RoompresentDeliveryHud>();var assistant=safe.GetComponent<RoompresentArAssistant>();
                foreach(string s in new[]{"start","proof","success","scanFrame"})Get<RectTransform>(hud,s).gameObject.SetActive(false);Get<RectTransform>(hud,"dock").gameObject.SetActive(true);
                var bg=JarVisUi.Rect(safe,"Historical room photo fixture",0,0,412,915);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;bg.SetAsFirstSibling();photo=new Texture2D(2,2);ImageConversion.LoadImage(photo,File.ReadAllBytes(@"C:\Users\Windows\Documents\ChatGPT\jarvis\ui-ux-directions-20261121\room-reference.jpg"));var raw=bg.gameObject.AddComponent<RawImage>();raw.texture=photo;raw.uvRect=new Rect(0,.245f,1,.58f);raw.raycastTarget=false;
                PrototypeUi.Text(safe,font,"UNITY · NỀN THAM CHIẾU · ÂM THANH THỬ",214,16,182,30,7,true,true);
                var map=guide.mapRoot.localToWorldMatrix;var frame=qr.Frame.localToWorldMatrix;var cp=camera.transform.localToWorldMatrix;var xp=origin.transform.localToWorldMatrix;bool se=session.enabled,ce=camera.enabled,me=manager.enabled;int scene=guide.gameObject.scene.handle;string progress=JsonUtility.ToJson(record);
                void Retained(string s){Check(session.enabled==se&&camera.enabled==ce&&manager.enabled==me,s+": camera/session enabled states retained");Check(guide.mapRoot.localToWorldMatrix==map&&camera.transform.localToWorldMatrix==cp&&origin.transform.localToWorldMatrix==xp&&qr.Frame==reference.transform&&qr.Frame.localToWorldMatrix==frame&&Get<ARAnchor>(qr,"anchor")==anchor&&guide.gameObject.scene.handle==scene,s+": same reference, map, camera and scene");Check(JsonUtility.ToJson(record)==progress,s+": delivered B / pending C retained");}
                void Shot(string n){foreach(int h in new[]{2400,1920})Render(canvas,new Vector2Int(1080,h),Path.Combine(Dir,n+"-"+themeMode+"-"+h+".png"));}
                var pill=Get<RectTransform>(assistant,"pill");Check(pill.GetComponent<Image>().color.a<.01f&&pill.GetComponentsInChildren<TMP_Text>().Length==0&&pill.GetComponentsInChildren<Image>().Count(i=>i.sprite==Resources.Load<Sprite>("Prototype/JarVis"))==1,"Launcher only shows unchanged logo; no border or text");
                Check(pill.sizeDelta==new Vector2(72,72)&&pill.Find("JarVis").GetComponent<RectTransform>().sizeDelta==new Vector2(64,64),"Larger original logo: 64px art / 72px hit target");
                Check(Get<bool>(hud,"dark")==dark&&Get<bool>(assistant,"dark")==dark,"AR HUD and assistant use chosen app mode");
                Check(Get<RectTransform>(hud,"dock").GetComponent<Image>().material.GetColor("_Fill")==PrototypeUi.Surface(dark)&&Get<TMP_Text>(hud,"progress").color==PrototypeUi.Ink(dark),"Dock surface and title contrast match "+themeMode);
                Check(((RectTransform)hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Rescan QR A").transform).sizeDelta==new Vector2(48,48),"A rescan has 48px target");Check(hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Rescan QR A").GetComponentInChildren<TMP_Text>().text=="A","A button has only A label");Shot("01-guidance");
                pill.GetComponent<Button>().onClick.Invoke();var widget=Get<RectTransform>(assistant,"widget");Check(widget.rect.width==300&&widget.rect.height<154&&Get<TMP_Text>(assistant,"state").fontSize==14,"Old style widget is smaller with larger original-font text");Check(widget.GetComponentsInChildren<ImportedUtilityIcon>().Any(i=>i.symbol=="alert-triangle")&&widget.GetComponentsInChildren<ImportedUtilityIcon>().Any(i=>i.symbol=="message-square")&&widget.GetComponentInChildren<ChatActionIcon>()!=null,"Report, chat and voice use vector symbols without glyph boxes");Shot("02-jarvis");
                Call(assistant,"SetStatus","Đang nghe · 28s / 30s · Gửi hoặc × hủy");Shot("03-voice-status");Call(assistant,"SetStatus",new string('W',80));Get<TMP_Text>(assistant,"state").ForceMeshUpdate();Check(!Get<TMP_Text>(assistant,"state").isTextOverflowing,"Long status expands compact card");Call(assistant,"SetStatus","Hỏi JarVis về nhiệm vụ");
                widget.GetComponentsInChildren<Button>().Single(b=>b.name=="Chat with JarVis").onClick.Invoke();Check(Get<RectTransform>(assistant,"chat").gameObject.activeSelf,"Chat icon opens existing chat");Shot("07-chat");var input=Get<TMP_InputField>(assistant,"input");input.text="Gửi lại ảnh bản đồ 2D";Get<Button>(assistant,"send").onClick.Invoke();Check(Get<RectTransform>(assistant,"content").GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null),"Existing map resend remains functional offline");Retained("Chat/map");
                assistant.OpenWidget();widget.GetComponentsInChildren<Button>().Single(b=>b.name=="Report incident").onClick.Invoke();Check(Get<RectTransform>(assistant,"report").gameObject.activeSelf,"Incident icon opens original report popup");Shot("08-report");assistant.Close();Retained("Close");
                var motion=Get<RoompresentAssistantMotion>(assistant,"motion");motion.Levels=new float[48];for(int i=0;i<24;i++)Call(motion,"Update");var quiet=motion.EyeOffset;motion.Levels=Enumerable.Range(0,48).Select(i=>i<36?0f:1f).ToArray();Set(motion,"strength",1f);Call(motion,"Update");Check(motion.Levels.Max()==1&&widget.Find("Listening JarVis").GetComponent<Image>().sprite==Resources.Load<Sprite>("Prototype/JarVisExpressionBase"),"Existing audio-driven eye/wave owner and original expression asset retained");
                Call(hud,"UpdateTrackingNotice",false,false,false,0f);Check(!Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"No tracking warning on initial startup");Call(hud,"UpdateTrackingNotice",true,false,false,0f);Call(hud,"UpdateTrackingNotice",false,false,false,1f);Check(!Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"Brief tracking interruption does not flash popup");Call(hud,"UpdateTrackingNotice",false,false,false,2.6f);Check(Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"Sustained loss opens JarVis warning");
                var notice=Get<RectTransform>(hud,"trackingLost");Check(notice.GetComponentInChildren<Button>().GetComponentInChildren<TMP_Text>().text=="OK","Tracking popup action is labelled only OK");Check(notice.GetComponentsInChildren<Image>().Any(i=>i.sprite==Resources.Load<Sprite>("Prototype/JarVisExpressionBase"))&&notice.GetComponentInChildren<MascotSmileArc>()!=null,"Worried face uses original brand artwork and native expression");Shot("04-tracking-lost");Retained("Tracking warning");
                notice.gameObject.SetActive(false);Get<RectTransform>(hud,"completionOverlay").gameObject.SetActive(true);Shot("09-completion");Get<RectTransform>(hud,"completionOverlay").gameObject.SetActive(false);
                Call(hud,"UpdateTrackingNotice",true,false,false,3f);Check(!Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"Automatic recovery dismisses stale warning");Call(hud,"UpdateTrackingNotice",false,true,false,4f);Call(hud,"UpdateTrackingNotice",false,true,false,7f);Check(!Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"Explicit A scan never repeats loss warning");
                Call(hud,"UpdateTrackingNotice",false,false,true,8f);Call(hud,"UpdateTrackingNotice",false,false,true,11f);Check(!Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"Camera refresh never produces loss warning");Call(hud,"UpdateTrackingNotice",false,false,false,12f);Call(hud,"UpdateTrackingNotice",false,false,false,14f);
                Get<RectTransform>(hud,"trackingLost").GetComponentsInChildren<Button>().Single().onClick.Invoke();Check(qr.ReacquiringA&&!Get<RectTransform>(hud,"trackingLost").gameObject.activeSelf,"OK closes warning and requests actual QR A reacquisition");Retained("Rescan A");
                // Render each genuine code frame shape; neither animation nor elapsed time completes a scan.
                foreach(string s in new[]{"start","proof","success","dock"})Get<RectTransform>(hud,s).gameObject.SetActive(false);pill.gameObject.SetActive(false);var frameRect=Get<RectTransform>(hud,"scanFrame");frameRect.gameObject.SetActive(true);Get<RectTransform>(hud,"scanCaption").gameObject.SetActive(false);var code=frameRect.GetComponent<RoompresentCodeFrame>();
                foreach(bool barcode in new[]{false,true}){code.SetBarcode(barcode);Check(code.rectTransform.sizeDelta==(barcode?new Vector2(300,132):new Vector2(236,236)),"QR square / barcode wide dimensions retained "+barcode);Set(code,"phase",.38f);Shot(barcode?"06-barcode":"05-qr");}
                using(var vh=new VertexHelper()){RoompresentCodeFrame.DrawSweep(vh,new Rect(-150,-66,300,132),Color.cyan,.2f,false);Check(vh.currentVertCount>4,"Sweep has layered soft trail and sharp centre");vh.Clear();RoompresentCodeFrame.DrawSweep(vh,new Rect(-150,-66,300,132),Color.cyan,.5f,true);Check(vh.currentVertCount==4,"Reduced motion uses still scan line");}
                // Frames for a short UI-only scan animation.
                if(!dark){Directory.CreateDirectory(Path.Combine(Dir,"motion"));for(int i=0;i<36;i++){Set(code,"phase",.5f-.5f*Mathf.Cos(i/18f*Mathf.PI));Render(canvas,new Vector2Int(432,960),Path.Combine(Dir,"motion",i.ToString("000")+".png"),false);}}
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);canvas=null;
                foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");JarvisLocalization.SetLanguage(language);app=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);app.Initialize();
                    var settings=app.gameObject.AddComponent<ImportedSettings>();Set(settings,"nav",app);Set(settings,"font",font);var presence=app.gameObject.AddComponent<ImportedPresence>();Set(app,"<Settings>k__BackingField",settings);Set(app,"<Presence>k__BackingField",presence);Set(presence,"<TodayClock>k__BackingField","01:24:18");
                    app.RegisterPrototypeScreen("prototype:activity",JarVisUi.Rect(app.screens[0].root.transform.parent,"Activity review",0,0,412,915).gameObject);
                    var workspace=app.gameObject.AddComponent<PrototypeWorkspace>();Set(workspace,"nav",app);Set(workspace,"font",font);Call(workspace,"Build","activity");app.Show("prototype:activity",false);Get<TMP_Text>(workspace,"badge").text=language==SupportedLanguage.Vi?"○ Đã tạm dừng":"○ Paused";
                    var root=app.screens.First(s=>s.id=="prototype:activity").root;var text=root.GetComponentsInChildren<TMP_Text>().Select(t=>t.text).ToArray();Check(!text.Any(t=>t.Contains("UTC")||t.Contains("Đài Loan")||t.Contains("Taiwan")||t.Contains("payroll")||t.Contains("chấm công")),"Activity removes requested timezone and support text "+language);Check(text.Contains("01:24:18")&&root.GetComponentsInChildren<Button>().Any(),"Presence clock and controls retained "+language);
                    Check(Get<TMP_Text>(workspace,"clock").color==PrototypeUi.Hex(dark?"68e6bb":"008755"),"Activity clock is green "+language);if(app.BottomNavigation!=null)ImportedUiPolish.Footer(app.BottomNavigation);
                    canvas=app.GetComponentInParent<Canvas>();foreach(int h in new[]{2400,1920})Render(canvas,new Vector2Int(1080,h),Path.Combine(Dir,"activity-"+language+"-"+themeMode+"-"+h+".png"));canvas=null;
                }
                }
                foreach(var p in guards)Check(bytes[p]==null?!File.Exists(p):File.Exists(p)&&bytes[p].SequenceEqual(File.ReadAllBytes(p)),"Unchanged data/owner: "+Path.GetFileName(p));
                checks.Add("LIMIT Editor UI with declared non-native anchor, reference photo, audio and tracking-state fixtures. No physical camera/microphone, tracking recovery, server/API, APK or Samsung test. No mission/scene save.");File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),new[]{"PASS "+DateTime.UtcNow.ToString("O")}.Concat(checks));
            }
            catch(Exception e){checks.Add("FAIL "+e);File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),checks);Debug.LogException(e);EditorApplication.Exit(1);return;}
            finally{current.SetValue(null,original);JarvisLocalization.SetLanguage(lang);if(theme==null)PlayerPrefs.DeleteKey("JarVis.Theme");else PlayerPrefs.SetString("JarVis.Theme",theme);if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas.gameObject);if(reference!=null)UnityEngine.Object.DestroyImmediate(reference);if(photo!=null)UnityEngine.Object.DestroyImmediate(photo);}
            EditorApplication.Exit(0);
        }
        static void Render(Canvas canvas,Vector2Int size,string path,bool checkText=true)
        {
            var go=new GameObject("UI-only review camera",typeof(Camera));var camera=go.GetComponent<Camera>();var rt=new RenderTexture(size.x,size.y,24);rt.Create();var previous=RenderTexture.active;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try{foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();foreach(var v in canvas.GetComponentsInChildren<ImportedFigmaViewport>(true))v.Fit();Canvas.ForceUpdateCanvases();foreach(var s in canvas.GetComponentsInChildren<PrototypeGlassShadow>(true))Call(s,"LateUpdate");foreach(var s in canvas.GetComponentsInChildren<UniformUiSurface>(true))Call(s,"LateUpdate");Canvas.ForceUpdateCanvases();if(checkText)foreach(var t in canvas.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Check(!t.isTextOverflowing,"Text fits "+size.y+": "+t.text);}camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(tex);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
