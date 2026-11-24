#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
using ZXing;
using ZXing.Common;

namespace ProjectBootstrap
{
    public static class DeliveryWorkflowReview
    {
        public static void RunSaved()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
            var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
            var language=JarvisLocalization.CurrentLanguage;
            try{foreach(var next in new[]{SupportedLanguage.Vi,SupportedLanguage.En}){
                JarvisLocalization.SetLanguage(next);Logs="Logs/RoompresentVPS/Delivery042/"+(next==SupportedLanguage.Vi?"VI/":"EN/");
                Run(guide,UnityEngine.Object.FindFirstObjectByType<RoompresentVpsLocalization>());
            }}finally{JarvisLocalization.SetLanguage(language);}
        }
        static string Logs="Logs/RoompresentVPS/Delivery042/";
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static void Run(SmallRoomAR guide,RoompresentVpsLocalization vps)
        {
            Directory.CreateDirectory(Logs);
            var asset=Resources.Load<TextAsset>("RoompresentCheckedRegistration");
            var saved=JsonUtility.FromJson<RoompresentQrRegistration>(asset.text);
            Check(saved.Validate(vps,guide.registrationLandmarks,out _,out _),"Saved real B/C registration changed or invalid");
            var gate=new RoompresentBarcodeGate();var restored=Ready();
            Check(restored.Loaded&&!gate.Complete,"Restored products bypassed fresh scans");
            Check(!gate.Observe(RoompresentDeliveryRecord.A1,"Manual")&&!gate.Observe("JWM:MAIN:E","RearCamera"),"Manual or QR counted as product");
            Check(gate.Observe(RoompresentDeliveryRecord.A1,"RearCamera")&&!gate.Complete&&gate.Contains(RoompresentDeliveryRecord.A1),"A1 gate");
            Check(gate.Observe(RoompresentDeliveryRecord.A1,"RearCamera")&&!gate.Complete,"Duplicate A1 counted as A2");
            Check(gate.Observe(RoompresentDeliveryRecord.A2,"RearCamera")&&gate.Complete,"Both products missing");
            Check(!gate.Ready,"Two scans bypassed required OK");gate.Acknowledge();Check(gate.Ready,"OK did not unlock next step");
            gate.Reset();Check(!gate.Complete&&!gate.Ready,"Fresh visit retained scan proof");
            foreach(string first in new[]{"B","C"})
            {
                var record=Ready();var proof=new RoompresentDeliveryVerification();
                Check(!proof.Begin(new RoompresentDeliveryRecord(),first),"Unaccepted/unloaded verification started");
                string second=first=="B"?"C":"B";
                foreach(string stop in new[]{first,second})
                {
                    string product=stop=="B"?RoompresentDeliveryRecord.A1:RoompresentDeliveryRecord.A2;
                    string other=stop=="B"?RoompresentDeliveryRecord.A2:RoompresentDeliveryRecord.A1;
                    Check(!record.Confirm("JWM:MAIN:"+stop,stop,true,.4f),"QR-only legacy confirmation accepted");
                    Check(proof.Begin(record,stop),"Cannot start unfinished stop");
                    Check(proof.Observe(record,product,"RearCamera",true,.4f)==DeliveryScanOutcome.WrongDestination,"Product before QR accepted");
                    Check(proof.Observe(record,"JWM:MAIN:"+(stop=="B"?"C":"B"),"RearCamera",true,.4f)==DeliveryScanOutcome.WrongDestination,"Wrong destination accepted");
                    Check(proof.Observe(record,"JWM:MAIN:"+stop,"Manual",true,.4f)==DeliveryScanOutcome.Unavailable,"Manual verification accepted");
                    Check(proof.Observe(record,"JWM:MAIN:"+stop,"RearCamera",true,.4f)==DeliveryScanOutcome.DestinationVerified&&!record.Complete,"QR step prematurely completed");
                    Check(proof.Observe(record,other,"RearCamera",true,.4f)==DeliveryScanOutcome.WrongProduct&&proof.WaitingForProduct,"Wrong product accepted");
                    Check(proof.Observe(record,product,"RearCamera",false,.4f)==DeliveryScanOutcome.Unavailable&&!proof.WaitingForProduct,"Tracking loss retained QR proof");
                    Check(proof.Observe(record,product,"RearCamera",true,.4f)==DeliveryScanOutcome.WrongDestination,"Product bypassed rescanning after interruption");
                    foreach(float d in new[]{float.NaN,float.PositiveInfinity,-1,1.21f})Check(proof.Observe(record,"JWM:MAIN:"+stop,"RearCamera",true,d)==DeliveryScanOutcome.Unavailable,"Invalid distance accepted");
                    Check(proof.Observe(record,"JWM:MAIN:"+stop,"RearCamera",true,.4f)==DeliveryScanOutcome.DestinationVerified,"QR not accepted");
                    proof.Cancel();Check(!proof.Active&&proof.Observe(record,product,"RearCamera",true,.4f)==DeliveryScanOutcome.Unavailable,"Canceled proof completed");
                    Check(proof.Begin(record,stop),"Cannot restart canceled proof");
                    proof.Observe(record,"JWM:MAIN:"+stop,"RearCamera",true,.4f);
                    Check(proof.Observe(record,product,"RearCamera",true,.4f)==DeliveryScanOutcome.Completed&&!proof.Active,"Ordered QR/product completion failed");
                    Check(!proof.Begin(record,stop),"Completed stop accessible again");
                    record=JsonUtility.FromJson<RoompresentDeliveryRecord>(JsonUtility.ToJson(record));
                    Check((stop=="B"?record.deliveredB:record.deliveredC),"Completion lost on save/reload");
                }
                Check(record.Complete,"Both-stop workflow incomplete");
            }
            foreach(string barcode in new[]{RoompresentDeliveryRecord.A1,RoompresentDeliveryRecord.A2})
            {
                var bits=new MultiFormatWriter().encode(barcode,BarcodeFormat.EAN_13,640,260);
                var gray=new byte[640*260];for(int y=0;y<260;y++)for(int x=0;x<640;x++)gray[y*640+x]=(byte)(bits[x,y]?0:255);
                Check(RoompresentDeliveryVerification.DecodeProduct(gray,640,260)?.Text==barcode,"AR EAN decoder cannot read book");
            }
            foreach(float heading in new[]{0f,90f,180f,270f})
            {
                var old=new Pose(new Vector3(2,.7f,-1),Quaternion.Euler(0,heading,0));
                foreach(int stop in new[]{1,2})
                {
                    var shift=new Vector3(.12f,.02f,-.08f);
                    var measured=old.position+shift+old.rotation*saved.fit.observed[stop];
                    Check(RoompresentQrLocalization.TryDestinationOrigin(old,saved.fit.observed[stop],measured,out var correction),"Valid destination correction rejected");
                    Check(Vector3.Distance(correction.position,old.position+shift)<.0001f&&Quaternion.Angle(correction.rotation,old.rotation)<.001f,"Destination correction changed room heading or wrong origin");
                    int other=stop==1?2:1;
                    Check(Vector3.Distance(correction.position+correction.rotation*saved.fit.observed[other],old.position+shift+old.rotation*saved.fit.observed[other])<.0001f,"Remaining destination lost across reference transfer");
                    Check(!RoompresentQrLocalization.TryDestinationOrigin(old,saved.fit.observed[stop],measured+Vector3.right*2,out _),"Large destination jump accepted");
                }
            }
            VerifyHandoff(guide);
            var routeMethod=typeof(SmallRoomAR).GetMethod("Route",BindingFlags.Instance|BindingFlags.NonPublic);
            guide.surface.RemoveData();guide.surface.AddData();
            foreach(var pair in new[]{new[]{guide.basinAccess,guide.bagAccess},new[]{guide.bagAccess,guide.basinAccess}})
            {
                var nearShelf=guide.mapRoot.TransformPoint(pair[0]+new Vector3(.45f,0,0));
                object[] args={nearShelf,guide.mapRoot.TransformPoint(pair[1]),null,0f};
                Check((bool)routeMethod.Invoke(guide,args),"Cannot connect shelf-side camera to remaining cart route");
            }
            var fontPath=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.Contains("BeVietnamPro Figma SDF")&&!p.Contains("Fallback"));
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            typeof(SmallRoomAR).GetMethod("MakeUi",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(guide,new object[]{font});
            var canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
            try
            {
                Check(!guide.HasStartFix&&!guide.DeliveryTracking,"Guidance opened without QR A pose");
                Check(canvas.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Rescan QR A")&&canvas.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Exit AR"),"Missing A rescan or exit");
                var frame=canvas.GetComponentInChildren<RoompresentCodeFrame>(true);
                frame.SetBarcode(false);Check(Mathf.Abs(frame.rectTransform.sizeDelta.x-frame.rectTransform.sizeDelta.y)<.01f,"QR frame is not square");
                frame.SetBarcode(true);Check(frame.rectTransform.sizeDelta.x/frame.rectTransform.sizeDelta.y>2,"Product frame is not wide barcode shape");frame.SetBarcode(false);
                Check(canvas.GetComponentsInChildren<Button>(true).Count(b=>b.name.StartsWith("Navigate ")||b.name.StartsWith("Confirm "))==4,"Missing B/C navigation/confirm controls");
                var currentField=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
                var previousRecord=currentField.GetValue(null);
                foreach(var size in new[]{new Vector2Int(1080,2400),new Vector2Int(1080,1920)})
                {
                    Render(canvas,size,Logs+"start-"+size.y+".png");
                    var hud=canvas.GetComponentInChildren<RoompresentDeliveryHud>();
                    Field<RectTransform>(hud,"start").gameObject.SetActive(false);Field<RectTransform>(hud,"dock").gameObject.SetActive(true);
                    Render(canvas,size,Logs+"delivery-"+size.y+".png");
                    Field<RectTransform>(hud,"dock").gameObject.SetActive(false);
                    var record=Ready();record.deliveredB=true;currentField.SetValue(null,record);hud.Refresh();
                    Field<RectTransform>(hud,"start").gameObject.SetActive(false);Field<RectTransform>(hud,"dock").gameObject.SetActive(true);
                    Check(!canvas.GetComponentsInChildren<Button>(true).First(b=>b.name.StartsWith("Confirm B")).interactable,"Completed confirm not locked");
                    Render(canvas,size,Logs+"completed-B-"+size.y+".png");
                    guide.Verification.Begin(record,"C");hud.Refresh();
                    Field<RectTransform>(hud,"start").gameObject.SetActive(false);Field<RectTransform>(hud,"dock").gameObject.SetActive(false);Field<RectTransform>(hud,"proof").gameObject.SetActive(true);
                    Render(canvas,size,Logs+"proof-QR-"+size.y+".png");
                    typeof(SmallRoomAR).GetField("<PendingQrSuccess>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(guide,"C");hud.Refresh();
                    Field<RectTransform>(hud,"start").gameObject.SetActive(false);Field<RectTransform>(hud,"success").gameObject.SetActive(true);
                    Field<RectTransform>(hud,"scanFrame").gameObject.SetActive(false);
                    Render(canvas,size,Logs+"QR-success-"+size.y+".png");
                    canvas.GetComponentsInChildren<Button>().Single(b=>b.name=="Acknowledge QR").onClick.Invoke();
                    Check(guide.PendingQrSuccess==null,"QR success OK did not resume scanning");
                    guide.Verification.Observe(record,"JWM:MAIN:C","RearCamera",true,.5f);hud.Refresh();
                    Field<RectTransform>(hud,"start").gameObject.SetActive(false);Field<RectTransform>(hud,"dock").gameObject.SetActive(false);Field<RectTransform>(hud,"proof").gameObject.SetActive(true);
                    Render(canvas,size,Logs+"proof-product-"+size.y+".png");
                    guide.Verification.Cancel();currentField.SetValue(null,previousRecord);Field<RectTransform>(hud,"proof").gameObject.SetActive(false);Field<RectTransform>(hud,"dock").gameObject.SetActive(false);
                    int clicks=0;
                    var popup=RoompresentScanPopup.Create(canvas.transform,font,true,ImportedAppLocalization.IsEnglish,RoompresentDeliveryRecord.A1,false,()=>clicks++);
                    Render(canvas,size,Logs+"popup-"+size.y+".png");
                    var ok=popup.GetComponentsInChildren<Button>().Single();ok.onClick.Invoke();ok.onClick.Invoke();Check(clicks==1,"Repeated OK acted twice");UnityEngine.Object.DestroyImmediate(popup);
                    hud.Refresh();
                }
                File.WriteAllText(Logs+"result.txt","PASS QR square / barcode wide frame and A rescan/exit controls; explicit A rescan retains native reference and checked registration; metric destination origin correction for both stops/four headings, remaining destination invariant, large jump rejection; native reference and map transform retained across B-to-C and C-to-B handoff; shelf-side NavMesh route both directions; QR success popup OK; fresh two-book gate; both B-C/C-B ordered delivery; premature QR-only/wrong QR/wrong product/manual/far/NaN/tracking/cancel/repeat rejection; persisted completion; runtime AR EAN13 decoder on both books; real saved coordinate validation; four destination action controls. Actual UI canvases rendered at 1080x2400 and 1080x1920. Device spatial alignment and camera taps remain separate physical tests.\n");
            }
            finally{UnityEngine.Object.DestroyImmediate(canvas.gameObject);}
        }
        static void VerifyHandoff(SmallRoomAR guide)
        {
            var currentField=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
            var previous=currentField.GetValue(null);
            string file=(string)typeof(RoompresentDeliveryMission).GetProperty("FilePath",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            byte[] bytes=File.Exists(file)?File.ReadAllBytes(file):null;
            var parent=guide.mapRoot.parent;var position=guide.mapRoot.position;var rotation=guide.mapRoot.rotation;
            var originalReference=guide.qrLocalization;
            var fixture=guide.gameObject.AddComponent<RoompresentQrLocalization>();fixture.enabled=false;
            var anchorObject=new GameObject("Isolated handoff reference");anchorObject.SetActive(false);
            var native=anchorObject.AddComponent<UnityEngine.XR.ARFoundation.ARAnchor>();
            typeof(RoompresentQrLocalization).GetField("anchor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fixture,native);
            typeof(RoompresentQrLocalization).GetField("<Calibrated>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(fixture,true);
            guide.qrLocalization=fixture;var reference=fixture;var frame=fixture.Frame;
            var originalStop=Field<string>(guide,"activeStop");
            fixture.RequestReacquireA();
            Check(fixture.ReacquiringA&&fixture.Frame==frame&&fixture.Calibrated,"A rescan erased saved reference");
            try
            {
                foreach(string first in new[]{"B","C"})
                {
                    var record=Ready();record.deliveredB=first=="B";record.deliveredC=first=="C";currentField.SetValue(null,record);
                    guide.CompleteDeliveryHandoff();string expected=first=="B"?"C":"B";
                    Check(guide.SelectedStop==expected&&record.preferredStop==expected,"Handoff did not select remaining cart");
                    Check(guide.qrLocalization==reference&&reference.Frame==frame&&reference.Calibrated&&guide.mapRoot.parent==parent&&guide.mapRoot.position==position&&guide.mapRoot.rotation==rotation,"Handoff changed spatial reference");
                    var persisted=JsonUtility.FromJson<RoompresentDeliveryRecord>(File.ReadAllText(file));
                    Check(persisted.preferredStop==expected&&(first=="B"?persisted.deliveredB:persisted.deliveredC),"Handoff progress was not saved");
                }
            }
            finally
            {
                guide.qrLocalization=originalReference;UnityEngine.Object.DestroyImmediate(fixture);UnityEngine.Object.DestroyImmediate(anchorObject);
                currentField.SetValue(null,previous);typeof(SmallRoomAR).GetField("activeStop",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(guide,originalStop);
                if(bytes==null){if(File.Exists(file))File.Delete(file);}else File.WriteAllBytes(file,bytes);
            }
        }
        static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        static RoompresentDeliveryRecord Ready()=>new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};
        static void Render(Canvas canvas,Vector2Int size,string output)
        {
            var go=new GameObject("Delivery UI review camera",typeof(Camera));var camera=go.GetComponent<Camera>();
            var rt=new RenderTexture(size.x,size.y,24);rt.Create();var previous=RenderTexture.active;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");
                camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.17f,.21f);camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();
                foreach(var effect in canvas.GetComponentsInChildren<PrototypeGlassShadow>(true))typeof(PrototypeGlassShadow).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(effect,null);
                foreach(var surface in canvas.GetComponentsInChildren<UniformUiSurface>(true))typeof(UniformUiSurface).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(surface,null);
                Canvas.ForceUpdateCanvases();foreach(var t in canvas.GetComponentsInChildren<TMP_Text>())
                {
                    t.ForceMeshUpdate();Check(!t.isTextOverflowing,"UI text overflows: "+t.text);
                }
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(output,image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
