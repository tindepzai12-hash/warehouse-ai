#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
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
    // Exercises the shipping HUD, not the canceled system redesign or preview UI.
    public static class ArFinishAppliedReview
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\ar-finish-applied-20261121";
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> checks=new List<string>();
        static T Get<T>(object o,string name)=>(T)o.GetType().GetField(name,F).GetValue(o);
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,F).SetValue(o,value);
        static object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,F).Invoke(o,args);
        static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new Exception(label);}
        public static void Run()
        {
            Directory.CreateDirectory(Dir);checks.Clear();
            var current=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
            object old=current.GetValue(null);var language=JarvisLocalization.CurrentLanguage;
            string disk=Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json");
            string[] guards={"Assets/Resources/RoompresentCheckedRegistration.json",RoompresentVpsBuild.ScenePath,"Assets/Scenes/JarVisFigmaApp.unity","Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs","Assets/JarVis/Scripts/Navigation/RoompresentArAssistant.cs","Assets/Resources/Prototype/JarVis.png",disk};
            var saved=guards.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            Canvas canvas=null;GameObject fixture=null;Texture2D photo=null;
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
                var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);
                var font=nav.GetComponentInChildren<TMP_Text>(true).font;
                Check(font!=null,"Original font resolved from the actual app navigation, as launcher does");
                checks.Add("FONT "+AssetDatabase.GetAssetPath(font));
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
                var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();
                var origin=UnityEngine.Object.FindFirstObjectByType<XROrigin>();var camera=origin.Camera;
                var manager=camera.GetComponent<ARCameraManager>();
                var qr=guide.qrLocalization;
                if(qr==null){qr=guide.gameObject.AddComponent<RoompresentQrLocalization>();qr.enabled=false;guide.qrLocalization=qr;}
                fixture=new GameObject("Declared non-native reference fixture");fixture.SetActive(false);
                fixture.transform.SetPositionAndRotation(new Vector3(.7f,.1f,-1.2f),Quaternion.Euler(0,17,0));
                var anchor=fixture.AddComponent<ARAnchor>();Set(qr,"anchor",anchor);Set(qr,"referenceFrame",fixture.transform);
                var a=guide.pins.GetComponentsInChildren<IsNavigationTarget>(true).Single(p=>p.targetName=="A");
                // A declared Editor camera pose; never a claim about the historical photo's alignment.
                camera.transform.position=a.transform.position+new Vector3(0,1.3f,5.2f);
                camera.transform.LookAt(a.transform.position+new Vector3(-.4f,-.12f,0));camera.fieldOfView=60;
                var aPose=a.transform.localToWorldMatrix;var map=guide.mapRoot.localToWorldMatrix;var parent=guide.mapRoot.parent;
                var cp=camera.transform.localToWorldMatrix;var xp=origin.transform.localToWorldMatrix;var rp=qr.Frame.localToWorldMatrix;
                bool se=session.enabled,ce=camera.enabled,me=manager.enabled;int scene=guide.gameObject.scene.handle;
                void Retained(string state)
                {
                    Check(session==UnityEngine.Object.FindFirstObjectByType<ARSession>()&&session.enabled==se&&camera.enabled==ce&&manager.enabled==me,state+": same enabled camera and AR session");
                    Check(camera.transform.localToWorldMatrix==cp&&origin.transform.localToWorldMatrix==xp&&guide.mapRoot.localToWorldMatrix==map&&guide.mapRoot.parent==parent,state+": unchanged camera/XR/map pose");
                    Check(qr.Frame==fixture.transform&&qr.Frame.localToWorldMatrix==rp&&Get<ARAnchor>(qr,"anchor")==anchor&&a.transform.localToWorldMatrix==aPose&&guide.gameObject.scene.handle==scene,state+": same non-null anchor/reference, saved A and scene");
                }
                // Verify both delivery orders with unchanged production proof requirements.
                foreach(string first in new[]{"B","C"})
                {
                    var r=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};var p=new RoompresentDeliveryVerification();
                    foreach(string stop in new[]{first,first=="B"?"C":"B"})
                    {
                        string book=stop=="B"?RoompresentDeliveryRecord.A1:RoompresentDeliveryRecord.A2;
                        Check(p.Begin(r,stop),"Begin pending "+stop);
                        Check(p.Observe(r,book,"RearCamera",true,.5f)==DeliveryScanOutcome.WrongDestination,"Barcode cannot replace destination QR "+stop);
                        Check(p.Observe(r,"JWM:MAIN:"+stop,"Manual",true,.5f)==DeliveryScanOutcome.Unavailable,"Manual proof rejected "+stop);
                        Check(p.Observe(r,"JWM:MAIN:"+stop,"RearCamera",false,.5f)==DeliveryScanOutcome.Unavailable,"Untrusted tracking rejected "+stop);
                        Check(p.Observe(r,"JWM:MAIN:"+stop,"RearCamera",true,1.21f)==DeliveryScanOutcome.Unavailable,"Distance guard retained "+stop);
                        Check(p.Observe(r,"JWM:MAIN:"+stop,"RearCamera",true,.5f)==DeliveryScanOutcome.DestinationVerified,"Correct QR "+stop);
                        Check(!r.Complete,"QR alone cannot complete task");
                        Check(p.Observe(r,book,"RearCamera",true,.5f)==DeliveryScanOutcome.Completed,"Correct ordered barcode "+stop);
                        Check(!p.Begin(r,stop),"Completed destination cannot be reused "+stop);
                    }
                    Check(r.Complete,"Both orders complete only with both pairs");
                }
                photo=new Texture2D(2,2);ImageConversion.LoadImage(photo,File.ReadAllBytes(@"C:\Users\Windows\Documents\ChatGPT\jarvis\ui-ux-directions-20261121\room-reference.jpg"));
                foreach(var lang in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
                {
                    JarvisLocalization.SetLanguage(lang);
                    var record=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};current.SetValue(null,record);
                    Set(guide,"activeStop","B");Set(guide,"<StartAligned>k__BackingField",true);
                    Call(guide,"MakeUi",font);
                    canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
                    canvas.GetComponent<CanvasScaler>().matchWidthOrHeight=0;
                    var safe=canvas.transform.Find("Safe area").GetComponent<RectTransform>();safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;
                    var hud=safe.GetComponent<RoompresentDeliveryHud>();var assistant=safe.GetComponent<RoompresentArAssistant>();
                    var bg=JarVisUi.Rect(safe,"Declared historical photo fixture",0,0,412,915);
                    bg.anchorMin=Vector2.zero;bg.anchorMax=Vector2.one;bg.offsetMin=bg.offsetMax=Vector2.zero;bg.SetAsFirstSibling();
                    var image=bg.gameObject.AddComponent<RawImage>();image.texture=photo;image.uvRect=new Rect(0,.245f,1,.58f);image.raycastTarget=false;
                    var stamp=PrototypeUi.Text(safe,font,"UNITY · ẢNH PHÒNG THAM CHIẾU",138,78,258,26,8,true,true);stamp.raycastTarget=false;
                    var marker=JarVisUi.Rect(safe,"Saved A projection fixture",0,0,40,56).gameObject.AddComponent<Image>();
                    marker.sprite=a.icon;marker.color=PrototypeUi.Hex("ff3341");marker.preserveAspect=true;marker.raycastTarget=false;marker.gameObject.SetActive(false);
                    // This projection is only to illustrate the real saved A pin on a historical photo.
                    void Frame(string state,bool displayDock=true)
                    {
                        hud.Refresh();
                        foreach(string f in new[]{"start","proof","success","scanFrame"})Get<RectTransform>(hud,f).gameObject.SetActive(false);
                        Get<RectTransform>(hud,"dock").gameObject.SetActive(displayDock);
                        Retained(state);Check(hud.GetComponentsInChildren<TMP_Text>(true).All(t=>t.font==font),state+": all existing and new text uses original app font");
                        foreach(int height in new[]{2400,1920})Render(canvas,new Vector2Int(1080,height),Path.Combine(Dir,(lang==SupportedLanguage.Vi?"VI-":"EN-")+state+"-"+height+".png"),()=>{
                            camera.aspect=1080f/height;
                            var v=camera.WorldToViewportPoint(a.transform.position);
                            marker.rectTransform.anchoredPosition=new Vector2(v.x*safe.rect.width,-(1-v.y)*safe.rect.height);
                        });
                    }
                    Check(!Get<RectTransform>(hud,"completionOverlay").gameObject.activeSelf,"No completion popup before task complete");
                    Frame("01-guidance");
                    var verifier=guide.Verification;Check(verifier.Begin(record,"B"),"B proof starts");
                    verifier.Observe(record,"JWM:MAIN:B","RearCamera",true,.5f);
                    verifier.Observe(record,RoompresentDeliveryRecord.A1,"RearCamera",true,.5f);Set(guide,"activeStop","C");hud.Refresh();
                    Check(!Get<RectTransform>(hud,"completionOverlay").gameObject.activeSelf,"B alone does not open popup");
                    Check(!Get<Button[]>(hud,"confirm")[0].interactable&&Get<Button[]>(hud,"route")[1].interactable,"B disabled, pending C route remains accessible");Frame("02-B-done-C-pending");
                    Check(verifier.Begin(record,"C"),"C proof starts");verifier.Observe(record,"JWM:MAIN:C","RearCamera",true,.5f);hud.Refresh();
                    Check(!Get<RectTransform>(hud,"completionOverlay").gameObject.activeSelf,"QR C alone does not open popup");
                    verifier.Observe(record,RoompresentDeliveryRecord.A2,"RearCamera",true,.5f);Set(guide,"activeStop",null);hud.Refresh();
                    var modal=Get<RectTransform>(hud,"completionOverlay");Check(record.Complete&&modal.gameObject.activeSelf,"Both ordered pairs open production completion popup");
                    Check(!Get<RectTransform>(hud,"deliveryRows").gameObject.activeSelf,"Completed B/C rows removed");
                    Check(Get<RectTransform>(hud,"dock").sizeDelta.y==88,"Finished dock is compact");
                    Check(modal.GetComponent<Image>().raycastTarget&&modal.GetSiblingIndex()==safe.childCount-1,"Completion overlay blocks taps behind and is on top");
                    Frame("03-completion-popup");
                    hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Close mission completion").onClick.Invoke();
                    Check(!modal.gameObject.activeSelf&&record.Complete,"Close popup retains completed task");Frame("04-closed-popup");
                    Set(hud,"completionAcknowledged",false);hud.Refresh();
                    hud.GetComponentsInChildren<Button>(true).Single(b=>b.name=="Return A from completion").onClick.Invoke();
                    Check(guide.ReturningToStart&&a.gameObject.activeSelf&&!modal.gameObject.activeSelf&&record.Complete,"Popup Return A selects actual saved A and retains completion");
                    Set(guide,"remaining",5.5f);
                    marker.gameObject.SetActive(true);marker.rectTransform.pivot=new Vector2(.5f,0);
                    Frame("05-return-A-red-pin");
                    Check(AssetDatabase.GetAssetPath(a.icon).Contains("Pin")||a.icon!=null,"Original A pin resource retained");
                    assistant.OpenWidget();Frame("06-original-JarVis-logo");assistant.Close();
                    UnityEngine.Object.DestroyImmediate(canvas.gameObject);canvas=null;
                }
                foreach(var p in guards)Check(saved[p]==null?!File.Exists(p):File.Exists(p)&&saved[p].SequenceEqual(File.ReadAllBytes(p)),"Unchanged original data: "+Path.GetFileName(p));
                checks.Add("LIMIT Editor UI fixtures only; no native tracking, camera, microphone or Samsung motion accuracy certified. No disk progress writes, scene saves, APK change, server or AI call.");
                File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),new[]{"PASS "+DateTime.UtcNow.ToString("O")}.Concat(checks));
            }
            catch(Exception e){checks.Add("FAIL "+e);File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),checks);Debug.LogException(e);EditorApplication.Exit(1);return;}
            finally{current.SetValue(null,old);JarvisLocalization.SetLanguage(language);if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas.gameObject);if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);if(photo!=null)UnityEngine.Object.DestroyImmediate(photo);}
            EditorApplication.Exit(0);
        }
        static void Render(Canvas canvas,Vector2Int size,string path,Action project)
        {
            var go=new GameObject("UI capture",typeof(Camera));var camera=go.GetComponent<Camera>();var rt=new RenderTexture(size.x,size.y,24);rt.Create();var previous=RenderTexture.active;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");
                camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();project();
                foreach(var s in canvas.GetComponentsInChildren<UniformUiSurface>(true))Call(s,"LateUpdate");
                Canvas.ForceUpdateCanvases();
                foreach(var text in canvas.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Text fits "+size.y+": "+text.text);}
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
