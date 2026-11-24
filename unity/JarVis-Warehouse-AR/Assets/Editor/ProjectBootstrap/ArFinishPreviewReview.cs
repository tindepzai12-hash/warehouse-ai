#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Preview;
using JarVis.Warehouse.Localization;
using Immersal.Samples.Navigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
namespace ProjectBootstrap
{
    public static class ArFinishPreviewReview
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\ar-finish-demo-20261121";
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,F).GetValue(o);
        static void Set(object o,string field,object v)=>o.GetType().GetField(field,F).SetValue(o,v);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
        static readonly List<string> checks=new List<string>();
        static void Check(bool yes,string text){checks.Add((yes?"PASS ":"FAIL ")+text);if(!yes)throw new InvalidOperationException(text);}
        public static void Run()
        {
            Directory.CreateDirectory(Dir);var rf=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.NonPublic|BindingFlags.Static);
            var oldRecord=rf.GetValue(null);var oldLang=JarvisLocalization.CurrentLanguage;Canvas canvas=null;GameObject fixture=null;Texture2D photo=null;
            string disk=Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json");
            string[] guards={"Assets/Resources/RoompresentCheckedRegistration.json",RoompresentVpsBuild.ScenePath,"Assets/Scenes/JarVisFigmaApp.unity","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryHud.cs","Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs",disk};
            var saved=guards.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            try
            {
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);JarvisLocalization.SetLanguage(SupportedLanguage.Vi);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();var origin=UnityEngine.Object.FindFirstObjectByType<XROrigin>();var camera=origin.Camera;var cm=camera.GetComponent<ARCameraManager>();
                Check(guide!=null&&guide.presentationRoom&&session!=null&&cm!=null,"Actual Roompresent AR owners present");
                var a=guide.pins.GetComponentsInChildren<IsNavigationTarget>(true).Single(p=>p.targetName=="A").transform;var savedA=a.position;
                // Explicit Editor pose only: reference photo is not a measured Samsung camera feed.
                camera.transform.position=savedA+new Vector3(0,1.3f,5.2f);camera.transform.LookAt(savedA+new Vector3(-.4f,-.12f,0));camera.fieldOfView=60;
                var qr=guide.qrLocalization;if(qr==null){qr=guide.gameObject.AddComponent<RoompresentQrLocalization>();qr.enabled=false;guide.qrLocalization=qr;}
                fixture=new GameObject("Non-native Editor reference fixture");fixture.SetActive(false);fixture.transform.SetPositionAndRotation(new Vector3(.7f,.1f,-1.2f),Quaternion.Euler(0,17,0));
                var anchor=fixture.AddComponent<ARAnchor>();Set(qr,"anchor",anchor);Set(qr,"referenceFrame",fixture.transform);
                var record=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};rf.SetValue(null,record);Set(guide,"activeStop","B");
                var fonts=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Where(p=>!p.Contains("Fallback")).ToArray();
                var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fonts.First(p=>p.Contains("BeVietnamPro Figma SDF")));
                var boldPath=fonts.FirstOrDefault(p=>p.Contains("BeVietnamPro")&&(p.Contains("Bold")||p.Contains("bold")));
                var bold=boldPath==null?font:AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(boldPath);
                Call(guide,"MakeUi",font);canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
                canvas.GetComponent<CanvasScaler>().matchWidthOrHeight=0;
                var safe=canvas.transform.Find("Safe area").GetComponent<RectTransform>();safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;
                foreach(Transform child in safe.Cast<Transform>().ToArray())child.gameObject.SetActive(false);
                foreach(var behaviour in safe.GetComponents<MonoBehaviour>())behaviour.enabled=false;
                var bg=new GameObject("Historical room photo - not camera feed",typeof(RectTransform),typeof(RawImage));bg.transform.SetParent(safe,false);var br=(RectTransform)bg.transform;br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;
                photo=new Texture2D(2,2);ImageConversion.LoadImage(photo,File.ReadAllBytes(@"C:\Users\Windows\AppData\Local\Temp\codex-clipboard-062885ce-2a1a-409d-a464-3091b0ede118.jpg"));
                var image=bg.GetComponent<RawImage>();image.texture=photo;image.uvRect=new Rect(0,.245f,1,.58f);image.raycastTarget=false;
                var go=new GameObject("Isolated AR completion review",typeof(RectTransform));go.transform.SetParent(safe,false);var rr=(RectTransform)go.transform;rr.anchorMin=Vector2.zero;rr.anchorMax=Vector2.one;rr.offsetMin=rr.offsetMax=Vector2.zero;
                var ui=go.AddComponent<ArDeliveryFinishPreview>();ui.Configure(record,font,bold);ui.ReturnRequested=guide.ReturnToStart;
                ui.StartPin.Guide=guide;ui.StartPin.SourceCamera=camera;ui.StartPin.SavedTarget=a;
                var map=guide.mapRoot.localToWorldMatrix;var parent=guide.mapRoot.parent;var cameraPose=camera.transform.localToWorldMatrix;var xrPose=origin.transform.localToWorldMatrix;var refPose=qr.Frame.localToWorldMatrix;bool sessionOn=session.enabled,cmOn=cm.enabled,cameraOn=camera.enabled;int scene=guide.gameObject.scene.handle;
                void Retained(string s)
                {
                    Check(session==UnityEngine.Object.FindFirstObjectByType<ARSession>()&&session.enabled==sessionOn,s+": same AR session and enabled state");
                    Check(cm.enabled==cmOn&&camera.enabled==cameraOn&&camera.transform.localToWorldMatrix==cameraPose&&origin.transform.localToWorldMatrix==xrPose,s+": same enabled camera and XR pose");
                    Check(qr.Frame==fixture.transform&&qr.Frame.localToWorldMatrix==refPose&&Get<ARAnchor>(qr,"anchor")==anchor,s+": non-null reference and anchor retained (fixture)");
                    Check(guide.mapRoot.localToWorldMatrix==map&&guide.mapRoot.parent==parent&&a.position==savedA&&guide.gameObject.scene.handle==scene,s+": saved A/map/scene retained");
                }
                void Shot(string s)
                {
                    Retained(s);
                    foreach(int h in new[]{2400,1920}){camera.aspect=1080f/h;Render(canvas,new Vector2Int(1080,h),Path.Combine(Dir,s+"-"+h+".png"),()=>ui.StartPin.RefreshProjection(true));}
                }
                void Click(string name){ui.GetComponentsInChildren<Button>(true).Single(b=>b.name==name).onClick.Invoke();}
                Click("Return A compact");Shot("00-return-A-pending");
                Call(ui,"Choose","B");Set(guide,"activeStop","B");guide.UpdateStartPin();
                Shot("01-guidance");Click("Verify B");
                Check(ui.ObserveDemo(RoompresentDeliveryRecord.A1,true,.5f)==DeliveryScanOutcome.WrongDestination&&!record.deliveredB,"Barcode cannot precede destination QR");
                Check(ui.ObserveDemo("JWM:MAIN:C",true,.5f)==DeliveryScanOutcome.WrongDestination,"Wrong destination QR rejected");
                ui.Refresh();Shot("02-qr-B");
                Check(ui.ObserveDemo("JWM:MAIN:B",true,.5f)==DeliveryScanOutcome.DestinationVerified&&!record.deliveredB,"QR B alone does not complete B");Shot("03-barcode-A1");
                Check(ui.ObserveDemo(RoompresentDeliveryRecord.A2,true,.5f)==DeliveryScanOutcome.WrongProduct&&!record.deliveredB,"Wrong book rejected");
                Check(ui.ObserveDemo(RoompresentDeliveryRecord.A1,true,.5f)==DeliveryScanOutcome.Completed&&record.deliveredB&&!record.deliveredC&&!ui.CompletionVisible,"Only B completed; C remains pending");Shot("04-B-done-C-pending");
                Click("Verify C");Check(ui.ObserveDemo("JWM:MAIN:C",false,.5f)==DeliveryScanOutcome.Unavailable&&!record.deliveredC,"Missing trusted tracking cannot confirm C");
                Check(ui.ObserveDemo("JWM:MAIN:C",true,.5f)==DeliveryScanOutcome.DestinationVerified,"QR C accepted with tracking fixture");Shot("05-barcode-A2");
                Check(ui.ObserveDemo(RoompresentDeliveryRecord.A2,true,.5f)==DeliveryScanOutcome.Completed&&record.Complete&&ui.CompletionVisible,"Completion popup opens only after both QR plus barcode pairs");Shot("06-completion-popup");
                Check(!ui.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Đã lấy tại")||t.text.Contains("Đã xác thực QR")),"No old pickup checklist rows");
                Click("Close completion");Check(!ui.CompletionVisible&&record.Complete,"Close popup preserves completed mission");Shot("07-completed-compact");
                Set(ui,"acknowledged",false);ui.Refresh();Click("Return A from completion");
                Check(ui.Returning&&guide.SelectedStop=="A"&&a.gameObject.activeSelf,"Popup Về A invokes actual ReturnToStart and activates saved A target");Shot("08-return-A-red-pin");
                Check(ui.StartPin.Visible,"Red A pin projects actual saved A position in Editor fixture");var point=ui.StartPin.rectTransform.anchoredPosition;
                camera.transform.position+=camera.transform.right*.35f;ui.StartPin.RefreshProjection(true);Check((ui.StartPin.rectTransform.anchoredPosition-point).magnitude>1&&a.position==savedA,"Pin follows world projection when camera moves; A stays fixed");camera.transform.position=cameraPose.GetColumn(3);
                ui.StartPin.RefreshProjection(false);Check(!ui.StartPin.Visible,"Without native tracking or explicit fixture, pin does not pretend localization");Retained("After return and projection checks");
                foreach(var p in guards)Check(saved[p]==null?!File.Exists(p):File.Exists(p)&&saved[p].SequenceEqual(File.ReadAllBytes(p)),"Original data unchanged: "+Path.GetFileName(p));
                checks.Add("LIMIT: Editor-only isolated UI candidate. Scans/poses are explicit fixtures. Historical photo is not live camera or actual measured A alignment. No Samsung tracking accuracy is certified. No production scene, HUD, calibration, mission disk write, APK or provider request.");
                File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),new[]{"PASS "+DateTime.UtcNow.ToString("O")}.Concat(checks));
            }
            catch(Exception e){File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),checks.Concat(new[]{"FAIL "+e}));Debug.LogException(e);EditorApplication.Exit(1);return;}
            finally{rf.SetValue(null,oldRecord);JarvisLocalization.SetLanguage(oldLang);if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas.gameObject);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);if(photo!=null)UnityEngine.Object.DestroyImmediate(photo);}
            EditorApplication.Exit(0);
        }
        static void Render(Canvas canvas,Vector2Int size,string path,Action project)
        {
            var go=new GameObject("UI capture camera",typeof(Camera));var cam=go.GetComponent<Camera>();var rt=new RenderTexture(size.x,size.y,24);rt.Create();var prev=RenderTexture.active;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");
                cam.cullingMask=1<<LayerMask.NameToLayer("UI");cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.orthographic=true;cam.nearClipPlane=.1f;cam.farClipPlane=10;cam.transform.position=new Vector3(0,0,-5);cam.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();project();
                foreach(var s in canvas.GetComponentsInChildren<UniformUiSurface>(true))typeof(UniformUiSurface).GetMethod("LateUpdate",F).Invoke(s,null);
                Canvas.ForceUpdateCanvases();foreach(var t in canvas.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Check(!t.isTextOverflowing,"Text fits "+size.y+": "+t.text);}
                cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally{RenderTexture.active=prev;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(tex);cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
