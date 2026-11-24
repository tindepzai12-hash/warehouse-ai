#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.Preview;
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
    public static class JarVisGrokCardReview
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\jarvis-grok-demo-20261121";
        const string Prefab="Assets/JarVis/Demos/GrokAssistantReview/JarVisGrokCard.prefab";
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> checks=new List<string>();
        static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,F).GetValue(o);
        static void Set(object o,string field,object value)=>o.GetType().GetField(field,F).SetValue(o,value);
        static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,F).Invoke(o,args);
        static void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);if(!ok)throw new Exception(message);}
        public static void Run()
        {
            Directory.CreateDirectory(Dir);checks.Clear();Canvas canvas=null;Texture2D photo=null;GameObject reference=null;
            var current=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);object originalRecord=current.GetValue(null);var lang=JarvisLocalization.CurrentLanguage;
            string disk=Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json");
            string[] paths={"Assets/Resources/RoompresentCheckedRegistration.json","Assets/Scenes/JarVisFigmaApp.unity",RoompresentVpsBuild.ScenePath,"Assets/JarVis/Scripts/Navigation/RoompresentArAssistant.cs","Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryHud.cs","Assets/Resources/Prototype/JarVis.png","Assets/Resources/Prototype/JarVisExpressionBase.png",disk};
            var bytes=paths.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);var font=nav.GetComponentInChildren<TMP_Text>(true).font;
                checks.Add("FONT "+AssetDatabase.GetAssetPath(font));
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);JarvisLocalization.SetLanguage(SupportedLanguage.Vi);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();var origin=UnityEngine.Object.FindFirstObjectByType<XROrigin>();var camera=origin.Camera;var manager=camera.GetComponent<ARCameraManager>();
                var qr=guide.qrLocalization;if(qr==null){qr=guide.gameObject.AddComponent<RoompresentQrLocalization>();qr.enabled=false;guide.qrLocalization=qr;}
                reference=new GameObject("Declared non-native Editor reference");reference.SetActive(false);reference.transform.SetPositionAndRotation(new Vector3(.7f,.1f,-1.2f),Quaternion.Euler(0,17,0));var anchor=reference.AddComponent<ARAnchor>();Set(qr,"anchor",anchor);Set(qr,"referenceFrame",reference.transform);
                var map=guide.mapRoot.localToWorldMatrix;var parent=guide.mapRoot.parent;var cp=camera.transform.localToWorldMatrix;var xp=origin.transform.localToWorldMatrix;var rp=qr.Frame.localToWorldMatrix;bool se=session.enabled,ce=camera.enabled,me=manager.enabled;int scene=guide.gameObject.scene.handle;
                void Retained(string label)
                {
                    Check(session==UnityEngine.Object.FindFirstObjectByType<ARSession>()&&session.enabled==se&&camera.enabled==ce&&manager.enabled==me,label+": same camera and AR session enabled state");
                    Check(camera.transform.localToWorldMatrix==cp&&origin.transform.localToWorldMatrix==xp&&guide.mapRoot.localToWorldMatrix==map&&guide.mapRoot.parent==parent,label+": unchanged camera/XR/map transform");
                    Check(qr.Frame==reference.transform&&qr.Frame.localToWorldMatrix==rp&&Get<ARAnchor>(qr,"anchor")==anchor&&guide.gameObject.scene.handle==scene,label+": same non-null anchor/reference/scene");
                }
                var record=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true};current.SetValue(null,record);Set(guide,"activeStop","C");
                Call(guide,"MakeUi",font);canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();canvas.GetComponent<CanvasScaler>().matchWidthOrHeight=0;
                var safe=canvas.transform.Find("Safe area").GetComponent<RectTransform>();safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;
                var assistant=safe.GetComponent<RoompresentArAssistant>();Get<RectTransform>(assistant,"pill").gameObject.SetActive(false);
                var hud=safe.GetComponent<RoompresentDeliveryHud>();foreach(string n in new[]{"start","proof","success","scanFrame"})Get<RectTransform>(hud,n).gameObject.SetActive(false);Get<RectTransform>(hud,"dock").gameObject.SetActive(true);
                var bg=JarVisUi.Rect(safe,"Reference photo fixture",0,0,412,915);bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;bg.SetAsFirstSibling();
                photo=new Texture2D(2,2);ImageConversion.LoadImage(photo,File.ReadAllBytes(@"C:\Users\Windows\Documents\ChatGPT\jarvis\ui-ux-directions-20261121\room-reference.jpg"));var image=bg.gameObject.AddComponent<RawImage>();image.texture=photo;image.uvRect=new Rect(0,.245f,1,.58f);image.raycastTarget=false;
                var stamp=PrototypeUi.Text(safe,font,"DEMO UNITY · ÂM THANH MÔ PHỎNG · NỀN THAM CHIẾU",16,66,380,16,7,true,true);stamp.alignment=TextAlignmentOptions.Center;
                var target=JarVisUi.Rect(safe,"JarVis Grok presentation",0,0,412,915);target.anchorMin=Vector2.zero;target.anchorMax=Vector2.one;target.offsetMin=target.offsetMax=Vector2.zero;
                var view=target.gameObject.AddComponent<JarVisGrokCard>();view.Font=font;view.Logo=Resources.Load<Sprite>("Prototype/JarVis");view.Expression=Resources.Load<Sprite>("Prototype/JarVisExpressionBase");view.Map=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/JarVis/Demos/GrokAssistantReview/RoompresentRoute2D.png");
                // Use the current packaged reference if its import name differs.
                if(view.Map==null)
                {
                    string p=AssetDatabase.FindAssets("t:Texture2D").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.Contains("Roompresent")&&p.Contains("2D")&&!p.Contains("Fallback"));
                    if(p!=null)view.Map=AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
                Check(view.Map!=null,"Current saved map texture is reused");
                var commands=new List<string>();view.CommandRequested=commands.Add;var messages=new List<string>();view.MessageRequested=messages.Add;var incidents=new List<string>();view.IncidentRequested=incidents.Add;
                var silence=new float[64];var loud=Enumerable.Range(0,64).Select(i=>.15f+.82f*Mathf.Sin(i*.12f)*Mathf.Sin(i*.12f)).ToArray();
                view.Show("listening");view.AudioLevels=silence;view.ApplyMotion(1,1,true);Check(view.WavePeak==0,"Silence has no fabricated voice waveform");var eye=view.EyePosition;
                view.AudioLevels=loud;view.ApplyMotion(1,1,true);Check(view.WavePeak>.7f&&(view.EyePosition-eye).magnitude>.5f,"Eyes follow measured audio energy; loud input produces taller waves");
                view.ReducedMotion=true;view.ApplyMotion(1,1,true);Check((view.EyePosition-new Vector2(18.9f,-26.8f)).sqrMagnitude<.01f&&view.Group.alpha==1,"Reduced motion removes eye movement and entrance tween");view.ReducedMotion=false;
                void Click(string name){view.GetComponentsInChildren<Button>().Single(b=>b.name==name).onClick.Invoke();}
                foreach(var action in new[]{"report","chat","voice"}){view.Show("ready");Click("Action "+action);Check(commands.Last()==action,"Existing owner command exposed: "+action);Retained(action);}
                view.Show("chat");view.GetComponentInChildren<TMP_InputField>().text="Gửi lại ảnh 2D";Click("Send");Check(messages.Single()=="Gửi lại ảnh 2D","Composer emits typed message without a provider call");Click("Map attachment");Check(view.Mode=="map","Chat map image opens a local enlarged view");Retained("Map");
                view.Show("report");view.GetComponentInChildren<TMP_InputField>().text="Đường tới C chưa rõ";Click("Submit incident");Check(incidents.Single()=="Đường tới C chưa rõ","Incident emits description without a disk/server write");
                view.Show("ready");Click("Action close");Check(view.Mode=="closed"&&!view.Card.gameObject.activeSelf,"Red close cancels only presentation panel");Retained("Close");
                foreach(string mode in new[]{"ready","listening","thinking","speaking","chat","map","report"})
                {
                    view.Show(mode);view.AudioLevels=mode=="ready"||mode=="thinking"?silence:loud;view.ApplyMotion(1.2f,1,true);Retained(mode);
                    Check(view.GetComponentsInChildren<TMP_Text>().All(t=>t.font==font),mode+": original app font retained");
                    foreach(int h in new[]{2400,1920})Render(canvas,new Vector2Int(1080,h),Path.Combine(Dir,mode+"-"+h+".png"));
                }
                // Sample the exact same UGUI motion function used by Update; sound is an explicit fixture.
                Directory.CreateDirectory(Path.Combine(Dir,"motion"));view.Show("listening");float start=Time.unscaledTime;
                for(int i=0;i<96;i++)
                {
                    for(int j=0;j<loud.Length;j++){float envelope=i<16?0:.2f+.6f*Mathf.Pow(Mathf.Sin((i-16)*.095f),2);loud[j]=envelope*Mathf.Pow(Mathf.Sin(j*.14f+i*.11f),2);}
                    view.AudioLevels=loud;view.ApplyMotion(start+i/24f,1f/24);Render(canvas,new Vector2Int(432,960),Path.Combine(Dir,"motion",i.ToString("000")+".png"),false);
                }
                Check(view.Group.alpha>.99f,"Entrance reaches full opacity on the unscaled timeline");Retained("Animation");
                view.Show("ready");view.AudioLevels=null;view.ApplyMotion(Time.unscaledTime,0,true);
                // Runtime builds the card once; avoid baking a second generated copy into the prefab.
                UnityEngine.Object.DestroyImmediate(view.Card.gameObject);PrefabUtility.SaveAsPrefabAsset(target.gameObject,Prefab);
                foreach(var p in paths)Check(bytes[p]==null?!File.Exists(p):File.Exists(p)&&bytes[p].SequenceEqual(File.ReadAllBytes(p)),"Unchanged production source/data: "+Path.GetFileName(p));
                checks.Add("LIMIT Isolated presentation candidate, not installed in main app. Audio/timing and camera pose are Editor fixtures. No microphone, server, AI, live location or APK test. Motion frames sample Unity Update's UI function, not a device recording.");
                File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),new[]{"PASS "+DateTime.UtcNow.ToString("O")}.Concat(checks));
            }
            catch(Exception e){checks.Add("FAIL "+e);File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),checks);Debug.LogException(e);EditorApplication.Exit(1);return;}
            finally{current.SetValue(null,originalRecord);JarvisLocalization.SetLanguage(lang);if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas.gameObject);if(photo!=null)UnityEngine.Object.DestroyImmediate(photo);if(reference!=null)UnityEngine.Object.DestroyImmediate(reference);}
            EditorApplication.Exit(0);
        }
        static void Render(Canvas canvas,Vector2Int size,string path,bool checkText=true)
        {
            var go=new GameObject("UI capture",typeof(Camera));var camera=go.GetComponent<Camera>();var rt=new RenderTexture(size.x,size.y,24);rt.Create();var previous=RenderTexture.active;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");
                camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                foreach(var s in canvas.GetComponentsInChildren<UniformUiSurface>(true))Call(s,"LateUpdate");Canvas.ForceUpdateCanvases();
                if(checkText)foreach(var t in canvas.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Check(!t.isTextOverflowing,"Text fits "+size.y+": "+t.text);}
                camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(tex);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
