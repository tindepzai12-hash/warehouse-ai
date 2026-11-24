#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Demo2026;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace ProjectBootstrap
{
    public static class JarVisSystemDemoReview
    {
        public const string Output = @"C:\Users\Windows\Documents\ChatGPT\jarvis\ui-system-demo-20261121";
        public const string ScenePath = "Assets/JarVis/Demos/SystemReview/JarVisSystemDemo.unity";
        const string PrefabPath = "Assets/JarVis/Demos/SystemReview/JarVisSystemDemo.prefab";
        static readonly List<string> checks = new List<string>();
        static readonly List<string> manifest = new List<string>{"id\tgroup\tlabel\theight\tpath"};
        static readonly BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
        static JarVisSystemDemo view;
        static Canvas canvas;
        static void Check(bool value,string label){checks.Add((value?"PASS ":"FAIL ")+label);if(!value)throw new InvalidOperationException(label);}
        static TMP_FontAsset Font(bool bold)
        {
            var paths=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains("BeVietnamPro")&&!p.Contains("Fallback")).ToArray();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(paths.First(p=>bold?p.Contains("Bold"):p.Contains("Figma SDF")));
        }
        static Canvas MakeCanvas()
        {
            var go=new GameObject("JarVis presentation canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;
            var s=go.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(412,915);s.matchWidthOrHeight=0;
            var root=new GameObject("Safe area",typeof(RectTransform));root.transform.SetParent(go.transform,false);var rt=(RectTransform)root.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            root.AddComponent<SafeAreaPanel>();view=root.AddComponent<JarVisSystemDemo>();view.RegularFont=Font(false);view.BoldFont=Font(true);view.Rehearsal=true;
            view.ReferencePhoto=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/JarVisReview/RoomReference.jpg");
            return c;
        }
        [MenuItem("Tools/JarVis/UI Demo/Open complete review")]
        public static void OpenDemo(){EditorSceneManager.OpenScene(ScenePath);JarVisDemoPlayReview.SetPortrait();JarVisDemoControls.Open();}
        public static void BuildAndReview()
        {
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Path.Combine(Output,"captures"));checks.Clear();
            string[] guards={"Assets/Scenes/JarVisFigmaApp.unity","Assets/Scenes/Roompresent_VPS.unity","Assets/Resources/RoompresentCheckedRegistration.json","Assets/JarVis/Scripts/UI/ImportedFigmaNavigation.cs","Assets/JarVis/Scripts/UI/RoompresentDeliveryMission.cs","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryVerification.cs","Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs",Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json")};
            var bytes=guards.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);canvas=MakeCanvas();
                new GameObject("UI input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                // Saved view remains lightweight; it builds its UGUI objects at runtime from the reusable scripts.
                PrefabUtility.SaveAsPrefabAsset(canvas.gameObject,PrefabPath);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                // Review on the real AR scene owners in Edit Mode. No native tracking is fabricated by the view.
                EditorSceneManager.OpenScene("Assets/Scenes/Roompresent_VPS.unity");
                var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();var origin=UnityEngine.Object.FindFirstObjectByType<XROrigin>();var camera=origin.Camera;var manager=camera.GetComponent<ARCameraManager>();var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
                Check(session!=null&&manager!=null&&guide!=null,"Actual Roompresent AR owners available for lifecycle retention check");
                int handle=guide.gameObject.scene.handle;var camMatrix=camera.transform.localToWorldMatrix;var xrMatrix=origin.transform.localToWorldMatrix;var mapMatrix=guide.mapRoot.localToWorldMatrix;var mapParent=guide.mapRoot.parent;bool sessionEnabled=session.enabled,managerEnabled=manager.enabled,cameraEnabled=camera.enabled;
                var existingAnchors=UnityEngine.Object.FindObjectsByType<ARAnchor>(FindObjectsInactive.Include,FindObjectsSortMode.None);
                var referenceFixture=new GameObject("Non-native retained reference fixture");referenceFixture.SetActive(false);referenceFixture.transform.SetPositionAndRotation(new Vector3(.7f,.1f,-1.2f),Quaternion.Euler(0,17,0));var anchorFixture=referenceFixture.AddComponent<ARAnchor>();
                var qr=guide.qrLocalization;if(qr==null){qr=guide.gameObject.AddComponent<RoompresentQrLocalization>();qr.enabled=false;guide.qrLocalization=qr;}typeof(RoompresentQrLocalization).GetField("anchor",F).SetValue(qr,anchorFixture);typeof(RoompresentQrLocalization).GetField("referenceFrame",F).SetValue(qr,referenceFixture.transform);var referenceMatrix=referenceFixture.transform.localToWorldMatrix;
                canvas=MakeCanvas();view.Configure();view.PreviewPhase=.42f;
                void Retained(){Check(session.enabled==sessionEnabled&&manager.enabled==managerEnabled&&camera.enabled==cameraEnabled&&guide.gameObject.scene.handle==handle,"Same session/camera/scene remain enabled");Check(camera.transform.localToWorldMatrix==camMatrix&&origin.transform.localToWorldMatrix==xrMatrix&&guide.mapRoot.localToWorldMatrix==mapMatrix&&guide.mapRoot.parent==mapParent,"Camera/XR/map transforms retained");Check(existingAnchors.All(a=>a!=null)&&qr.Frame==referenceFixture.transform&&referenceFixture.transform.localToWorldMatrix==referenceMatrix&&anchorFixture!=null,"Non-null reference and anchor fixture retained (not native tracking evidence)");}
                string[] pages={"splash","login","home","tasks","detail","inventory","product","labels","transactions","inbound","outbound","incident","map","chat","profile","activity","tools","settings","scanner"};
                for(int i=0;i<pages.Length;i++){view.Go(pages[i]);Shot("page-"+pages[i],"01-screens",LabelFor(pages[i]));}
                view.StartMission();Shot("scan-products-empty","02-scanners","Barcode A1/A2 · chưa quét");
                Check(!view.SubmitScan("JWM:MAIN:E","RearCamera",true,.5f),"Location QR cannot bypass product scan");
                Check(!view.SubmitScan(RoompresentDeliveryRecord.A1,"Manual",true,.5f),"Manual code cannot bypass rear camera requirement");
                Check(view.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",false,0),"Actual A1 barcode accepted by existing product gate");Shot("scan-A1-success","03-popups","Quét thành công A1");
                Check(!view.SubmitScan(RoompresentDeliveryRecord.A2,"RearCamera",false,0),"Second scan blocked until OK");
                view.AcknowledgeScan();Check(!view.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",false,0),"Duplicate A1 rejected");Shot("scan-A1-ready-A2","02-scanners","Đã quét 1/2 · chờ A2");
                Check(view.SubmitScan(RoompresentDeliveryRecord.A2,"RearCamera",false,0),"Actual A2 barcode accepted");Shot("scan-A2-success","03-popups","Quét thành công A2");view.AcknowledgeScan();Check(view.ScreenId=="scan-A"&&view.Gate.Ready,"Final product OK opens QR A step");Shot("scan-A","02-scanners","QR A · khung vuông");
                Check(!view.SubmitScan("JWM:MAIN:E","RearCamera",false,.5f),"Decoded QR A without measured pose cannot open AR");
                Check(view.SubmitScan("JWM:MAIN:E","RearCamera",true,.5f),"Explicit Editor trusted-pose fixture accepted");Shot("scan-A-success","03-popups","Đã nhận vị trí A");Click("Mở chỉ dẫn AR");Check(view.ScreenId=="ar"&&view.SelectedStop=="B","AR after both product acknowledgements and measured A fixture");Shot("ar-B","04-ar","Dẫn đường tới B");Retained();
                view.SelectStop("C");Check(view.SelectedStop=="C","User can switch route C");Shot("ar-C","04-ar","Dẫn đường tới C");view.SelectStop("B");
                view.Assistant("voice");Shot("assistant-listening","05-assistant","JarVis · đang nghe");Retained();view.VoiceState("thinking");Shot("assistant-thinking","05-assistant","JarVis · đang suy nghĩ");view.VoiceState("speaking");Shot("assistant-speaking","05-assistant","JarVis · đang nói");view.Assistant("chat");Shot("assistant-chat","05-assistant","Chat nổi trên camera");
                view.RequestMessage("Gửi lại bản đồ 2d");Check(view.Messages.Any(m=>m.Key=="map"),"Rehearsal map request adds image message");Shot("assistant-map","05-assistant","Chat kèm bản đồ 2D");view.EnlargeMap();Shot("assistant-map-large","05-assistant","Mở lớn ảnh 2D");Retained();view.IncidentPopup();Shot("assistant-incident","05-assistant","Báo cáo sự cố nổi");view.CloseModal();
                view.BeginDelivery("B");Shot("proof-B-QR","02-scanners","QR B · bước 1/2");Check(!view.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",true,.5f)&&!view.Record.deliveredB,"Barcode cannot precede QR B");Check(!view.SubmitScan("JWM:MAIN:C","RearCamera",true,.5f),"Wrong QR C rejected while verifying B");
                Check(view.SubmitScan("JWM:MAIN:B","RearCamera",true,.5f),"Correct QR B accepted");Shot("proof-B-QR-success","03-popups","Đã quét QR B");Click("OK · Quét barcode");Shot("proof-B-barcode","02-scanners","Barcode A1 · bước 2/2");Check(!view.SubmitScan(RoompresentDeliveryRecord.A2,"RearCamera",true,.5f),"Wrong product A2 rejected at B");Check(view.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",true,.5f)&&view.Record.deliveredB&&!view.Record.deliveredC&&view.TrackingTrusted,"B done preserves pending C and trusted fixture");Shot("ar-B-done","04-ar","B đã xong · C còn lại");Retained();
                view.BeginDelivery("C");Check(!view.SubmitScan("JWM:MAIN:C","RearCamera",true,3.5f),"Far away QR rejected by existing proximity guard");view.ApplyTracking(false);Check(!view.Proof.Active&&!view.Record.deliveredC,"Tracking loss discards unfinished proof, preserves task");Shot("ar-recovering","04-ar","Đang khôi phục vị trí");view.ApplyTracking(true);view.BeginDelivery("C");Check(view.SubmitScan("JWM:MAIN:C","RearCamera",true,.5f),"Correct QR C accepted");Shot("proof-C-QR-success","03-popups","Đã quét QR C");Click("OK · Quét barcode");Shot("proof-C-barcode","02-scanners","Barcode A2 · bước 2/2");Check(view.SubmitScan(RoompresentDeliveryRecord.A2,"RearCamera",true,.5f)&&view.Record.Complete&&view.OverlayKind=="completion","Only both correct QR/book pairs open completion");Shot("task-complete","03-popups","Hoàn thành nhiệm vụ");Retained();
                Check(!view.overlayRoot.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Đã lấy tại")),"No obsolete pickup checklist");Click("Về A");Check(view.Returning&&view.SelectedStop=="A"&&view.Record.Complete&&view.TrackingTrusted,"Return A preserves completed mission and tracking fixture");Shot("ar-return-A","04-ar","Về A · ghim đỏ lớn");Retained();
                view.PreviewPhase=.12f;view.Go("scanner");Shot("scan-motion-1","06-motion","Vệt quét · pha 1");view.PreviewPhase=.72f;view.Go("scanner");Shot("scan-motion-2","06-motion","Vệt quét · pha 2");
                var reversed=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};var ordered=new RoompresentDeliveryVerification();Check(ordered.Begin(reversed,"C")&&ordered.Observe(reversed,"JWM:MAIN:C","RearCamera",true,.5f)==DeliveryScanOutcome.DestinationVerified&&ordered.Observe(reversed,RoompresentDeliveryRecord.A2,"RearCamera",true,.5f)==DeliveryScanOutcome.Completed&&!reversed.Complete,"C-first delivery remains supported");Check(ordered.Begin(reversed,"B")&&ordered.Observe(reversed,"JWM:MAIN:B","RearCamera",true,.5f)==DeliveryScanOutcome.DestinationVerified&&ordered.Observe(reversed,RoompresentDeliveryRecord.A1,"RearCamera",true,.5f)==DeliveryScanOutcome.Completed&&reversed.Complete,"C-first then B completes same mandatory flow");
                foreach(var p in guards)Check(bytes[p]==null?!File.Exists(p):File.Exists(p)&&bytes[p].SequenceEqual(File.ReadAllBytes(p)),"Production file preserved: "+Path.GetFileName(p));
                File.WriteAllLines(Path.Combine(Output,"manifest.tsv"),manifest);File.WriteAllLines(Path.Combine(Output,"RESULT.txt"),new[]{"PASS "+DateTime.UtcNow.ToString("O")}.Concat(checks).Concat(new[]{"LIMIT: presentation rehearsal; explicit code/pose fixtures. No native Samsung acceptance, provider call, production owner replacement, saved registration/progress change, APK install or backend write."}));
                UnityEngine.Object.DestroyImmediate(canvas.gameObject);EditorSceneManager.OpenScene(ScenePath);EditorApplication.Exit(0);
            }
            catch(Exception ex){File.WriteAllLines(Path.Combine(Output,"RESULT.txt"),new[]{"FAIL "+DateTime.UtcNow.ToString("O"),ex.ToString()}.Concat(checks));Debug.LogException(ex);EditorApplication.Exit(1);}
        }
        static string LabelFor(string id)
        {
            string[] ids={"splash","login","home","tasks","detail","inventory","product","labels","transactions","inbound","outbound","incident","map","chat","profile","activity","tools","settings","scanner"};
            string[] labels={"Mở ứng dụng","Đăng nhập","Trang chủ","Nhiệm vụ #00001","Chi tiết A1/A2","Tồn kho","Chi tiết sản phẩm","Sản phẩm và tem","Nhập / Xuất kho","Nhập kho","Xuất kho","Báo cáo sự cố","Bản đồ 2D","JarVis hội thoại","Hồ sơ","Hoạt động","Công cụ","Cài đặt","Quét barcode"};return labels[Array.IndexOf(ids,id)];
        }
        static void Click(string text){var buttons=(view.ModalOpen?view.overlayRoot.GetComponentsInChildren<Button>():view.GetComponentsInChildren<Button>()).Where(b=>b.interactable&&b.GetComponentInChildren<TMP_Text>()?.text==text).ToArray();Check(buttons.Length==1,"Unique actionable button in active layer "+text);buttons[0].onClick.Invoke();}
        static void Shot(string id,string group,string label)
        {
            string current=view.ScreenId,overlay=view.OverlayKind;var record=view.Record;var phase=view.PreviewPhase;
            foreach(int height in new[]{2400,1920})
            {
                // The view supports a second portrait height; no forced landscape layout.
                view.H=412f*height/1080; view.Go(current);
                if(overlay=="proof")typeof(JarVisSystemDemo).GetMethod("ProofOverlay",F).Invoke(view,null);
                else if(overlay=="completion")view.Completion();
                else if(overlay=="product-success")view.OpenModal("Quét thành công "+(record.scannedA2?"A2":"A1"),(record.scannedA2?"Vở A2 · CPNB-1003\nGiao tới C\n":"Vở A1 · CPNB-1004\nGiao tới B\n")+(record.scannedA2?RoompresentDeliveryRecord.A2:RoompresentDeliveryRecord.A1),"OK · Tiếp tục",view.AcknowledgeScan);
                else if(overlay=="A-success")view.OpenModal("Đã nhận vị trí A","Đã căn vị trí và hướng.\nSẵn sàng chỉ đường tới B và C.","Mở chỉ dẫn AR",()=>{typeof(JarVisSystemDemo).GetProperty("AwaitingAck").GetSetMethod(true).Invoke(view,new object[]{false});view.SelectStop("B");});
                else if(overlay=="destination-success")view.OpenModal("Đã quét QR "+view.Proof.Stop,"Đúng điểm giao "+view.Proof.Stop+".\nTiếp theo: barcode sách "+view.Proof.ProductName+".","OK · Quét barcode",()=>{typeof(JarVisSystemDemo).GetProperty("AwaitingAck").GetSetMethod(true).Invoke(view,new object[]{false});typeof(JarVisSystemDemo).GetMethod("ProofOverlay",F).Invoke(view,null);});
                else if(overlay?.StartsWith("assistant-")==true){if(id.Contains("thinking"))view.VoiceState("thinking");else if(id.Contains("speaking"))view.VoiceState("speaking");else view.Assistant(overlay.EndsWith("voice")?"voice":"chat");}
                else if(overlay=="incident")view.IncidentPopup();else if(overlay=="map-image")view.EnlargeMap();
                foreach(var motion in view.GetComponentsInChildren<JarVisMotion>()){motion.enabled=false;var cg=motion.GetComponent<CanvasGroup>();if(cg!=null)cg.alpha=1;motion.transform.localScale=Vector3.one;}
                string file="captures/"+group+"/"+id+"-"+height+".png";Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Output,file)));
                Render(new Vector2Int(1080,height),Path.Combine(Output,file));manifest.Add(id+"\t"+group+"\t"+label+"\t"+height+"\t"+file);
            }
            // Restore original label identity after the renderer re-built an overlay.
            typeof(JarVisSystemDemo).GetProperty("OverlayKind").GetSetMethod(true).Invoke(view,new object[]{overlay});
        }
        static void Render(Vector2Int size,string path)
        {
            var go=new GameObject("UI capture camera",typeof(Camera));var camera=go.GetComponent<Camera>();var rt=new RenderTexture(size.x,size.y,24);rt.Create();var previous=RenderTexture.active;var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");
                camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;Canvas.ForceUpdateCanvases();
                foreach(var surface in canvas.GetComponentsInChildren<UniformUiSurface>(true))typeof(UniformUiSurface).GetMethod("LateUpdate",F).Invoke(surface,null);
                foreach(var sweep in canvas.GetComponentsInChildren<JarVisScanSweep>())sweep.RefreshVisual();foreach(var face in canvas.GetComponentsInChildren<JarVisFace>())face.RefreshVisual();
                Canvas.ForceUpdateCanvases();
                foreach(var t in canvas.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Check(!t.isTextOverflowing,"Text fits "+size.y+": "+t.text);}
                camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(tex);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
    public sealed class JarVisDemoControls : EditorWindow
    {
        [MenuItem("Tools/JarVis/UI Demo/Rehearsal controls")]
        public static void Open(){GetWindow<JarVisDemoControls>("JarVis Demo").Show();}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Bảng thử riêng của Editor. Không phải nút trong giao diện app. Bật Play để xem chuyển động.",MessageType.Info);
            var ui=UnityEngine.Object.FindFirstObjectByType<JarVisSystemDemo>();if(ui==null){if(GUILayout.Button("Mở demo Unity"))JarVisSystemDemoReview.OpenDemo();return;}
            if(GUILayout.Button("Nhiệm vụ #00001"))ui.Go("tasks");if(GUILayout.Button("Bắt đầu luồng"))ui.StartMission();
            if(GUILayout.Button("Camera fixture: barcode A1"))ui.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",ui.TrackingTrusted,.5f);
            if(GUILayout.Button("Camera fixture: barcode A2"))ui.SubmitScan(RoompresentDeliveryRecord.A2,"RearCamera",ui.TrackingTrusted,.5f);
            if(GUILayout.Button("Camera + pose fixture: QR A"))ui.SubmitScan("JWM:MAIN:E","RearCamera",true,.5f);
            if(GUILayout.Button("Camera fixture: QR B"))ui.SubmitScan("JWM:MAIN:B","RearCamera",ui.TrackingTrusted,.5f);
            if(GUILayout.Button("Camera fixture: QR C"))ui.SubmitScan("JWM:MAIN:C","RearCamera",ui.TrackingTrusted,.5f);
            if(GUILayout.Button("Mất/khôi phục tracking fixture"))ui.ApplyTracking(!ui.TrackingTrusted);
            EditorGUILayout.Space();EditorGUILayout.LabelField("Chuyển màn để duyệt thiết kế");
            foreach(string id in new[]{"home","tasks","detail","scanner","map","chat","inventory","labels","transactions","profile","activity","tools","settings"})if(GUILayout.Button(id))ui.Go(id);
        }
    }
}
#endif
