using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class LabTestReview
    {
        static int checks;
        static void Check(bool condition,string name){if(!condition)throw new InvalidOperationException(name);checks++;}
        static void Set(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
        static void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        public static void RunUpgrade()
        {
            if(Application.isBatchMode)EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            checks=0;Directory.CreateDirectory("Logs/LabTest/UI");File.WriteAllText("Logs/LabTest/upgrade-review.txt","RUNNING\n");
            foreach(var size in new[]{new Vector2Int(412,915),new Vector2Int(412,732),new Vector2Int(360,800)})
                foreach(var view in new[]{"previewScan","previewHeading","navStraight","navRight","navLeft","navUTurn","navEdge","navInvalid","navArrived","trackingWait","recoveryHeadingB"})Render(size,view);
            ReviewGuide();ReviewRecoveryDeadline();
            File.WriteAllText("Logs/LabTest/upgrade-review.txt","PASS "+checks+" focused checks; 33 synthetic UI renders + 3 world cue renders. Device AR/performance pending.\n");
        }
        static void ReviewRecoveryDeadline()
        {
            var root=new GameObject("Recovery controller regression");
            try
            {
                var owner=root.AddComponent<LabTestSession>();
                Set(owner,"Floor",JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text));
                Set(owner,"Map",JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text));
                owner.EditorReviewRecovery(false);string target=owner.Exercise.Target;
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var gate=(LabTrackingRecovery)typeof(LabTestSession).GetField("trackingRecovery",flags).GetValue(owner);
                gate.Begin(0,"fixture",owner.LastKnownPosition,true);
                typeof(LabTestSession).GetMethod("CheckRecoveryDeadline",flags).Invoke(owner,new object[]{21d});
                Check(owner.RecoveryNeedsMarker&&!owner.RecoveringTracking,"Controller recovery deadline exits even without tracking samples");
                Check(owner.Exercise.Target==target,"Recovery deadline preserves the requested destination");
                owner.RefreshPresentation();Check(owner.Presentation.intent==LabCueIntent.Hold,"Recovery timeout cannot show movement guidance");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        public static void RunTracking()
        {
            if(Application.isBatchMode&&string.IsNullOrEmpty(SceneManager.GetActiveScene().path))EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            checks=0;Directory.CreateDirectory("Logs/LabTest/UI");File.WriteAllText("Logs/LabTest/tracking-review.txt","RUNNING\n");
            foreach(var size in new[]{new Vector2Int(412,915),new Vector2Int(412,732)})
                foreach(var view in new[]{"previewScan","previewHeading","trackingWait","trackingMap","recoveryHeadingB"})Render(size,view);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                foreach(float yaw in new[]{0f,90f,-90f,173f})
                {
                    var old=new GameObject("Old native anchor").transform;SceneManager.MoveGameObjectToScene(old.gameObject,scene);old.position=new Vector3(4,.2f,-3);old.rotation=Quaternion.Euler(0,yaw,0);
                    var frame=new GameObject("Map frame").transform;frame.SetParent(old,false);frame.localPosition=new Vector3(1,.4f,2);frame.localRotation=Quaternion.Euler(0,27,0);
                    var replacement=new GameObject("Near native anchor").transform;SceneManager.MoveGameObjectToScene(replacement.gameObject,scene);replacement.position=new Vector3(-2,0,7);replacement.rotation=Quaternion.Euler(0,-35,0);
                    var cameraWorld=new Vector3(5,1.35f,10);var local=frame.InverseTransformPoint(cameraWorld);var destination=frame.TransformPoint(new Vector3(2,0,3));var heading=frame.InverseTransformDirection(Vector3.forward);
                    frame.SetParent(replacement,true);
                    Check(Vector3.Distance(local,frame.InverseTransformPoint(cameraWorld))<.0001f,"Rebase preserves phone coordinates yaw "+yaw);
                    Check(Vector3.Distance(destination,frame.TransformPoint(new Vector3(2,0,3)))<.0001f,"Rebase preserves destination yaw "+yaw);
                    Check(Vector3.Angle(heading,frame.InverseTransformDirection(Vector3.forward))<.01f,"Rebase preserves heading yaw "+yaw);
                    UnityEngine.Object.DestroyImmediate(old.gameObject);Check(frame!=null&&frame.parent==replacement,"Old anchor removal does not destroy map frame");
                    UnityEngine.Object.DestroyImmediate(replacement.gameObject);
                }
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
            File.WriteAllText("Logs/LabTest/tracking-review.txt","PASS "+checks+" focused checks, 10 UI renders and 4 anchor frame rotations. Previous full UI/core suites NOT rerun. Device AR still pending.\n");
        }
        public static void Run()
        {
            if(Application.isBatchMode&&string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            checks=0;Directory.CreateDirectory("Logs/LabTest/UI");File.WriteAllText("Logs/LabTest/ui-review.txt","RUNNING - result pending\n");
            foreach(var size in new[]{new Vector2Int(412,915),new Vector2Int(412,732),new Vector2Int(360,800)})
                foreach(string view in new[]{"previewScan","previewHeading","home","locate","setupA","setupB","setupReturn","done","map","destinations","navigation","navStraight","navRight","navLeft","navEdge","navInvalid","navArrived","navMap"})Render(size,view);
            var preset=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
            var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
            Check(LabPaths.ValidMap(preset)&&LabRegistration.Solve(preset,floor).valid,"Bundled real survey and provisional metric fit valid");
            Check(!preset.loopChecked&&preset.automaticMarkerSize&&preset.markerWidthMetres==0,"Do not falsify incomplete real survey");
            foreach(var start in new[]{"A","B","C"})foreach(var end in new[]{"A","B","C"})if(start!=end)
                Check(new LabFloorRouter(floor).Find(preset.markers.First(m=>m.id==start).access,preset.markers.First(m=>m.id==end).access,out var route)==null&&route.Count>1,"Actual survey route "+start+" to "+end);
            ReviewGuide();
            File.WriteAllText("Logs/LabTest/ui-review.txt","PASS "+checks+" checks, 54 synthetic UI renders and authored layout routes. NOT device AR acceptance.\n");
        }

        static void ReviewGuide()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                var root=new GameObject("Guide projection regression");SceneManager.MoveGameObjectToScene(root,scene);
                var camera=root.AddComponent<Camera>();camera.fieldOfView=60;camera.aspect=412f/915;camera.transform.position=new Vector3(0,1.5f,0);
                var guide=new GameObject("Guide").AddComponent<LabGuideVisual>();SceneManager.MoveGameObjectToScene(guide.gameObject,scene);
                guide.Initialize(Resources.Load<Material>("Routes/LiveViewUnlit"));
                var safe=new Rect(.08f,.2f,.84f,.6f);
                Check(guide.Show(camera,new Vector3(0,1.5f,2),0,false,safe),"forward connected triplet fits camera");
                Check(!guide.Show(camera,new Vector3(8,1.5f,2),75,true,safe)&&!guide.CueVisible,"side guide hidden with explicit fallback");
                Check(!guide.Show(camera,new Vector3(0,1.5f,-2),180,false,safe),"behind camera guide never rendered as front");
                Check(!guide.Show(camera,new Vector3(0,1.5f,2),0,false,new Rect(.01f,.01f,.05f,.05f)),"UI occlusion check uses whole arrow not centre");
                Check(guide.GetComponentsInChildren<LabRouteCueRig>(true).Length==1&&guide.Rig.transform.childCount==3,"Exactly one cluster of three chevrons");
                guide.Hide();Check(!guide.CueVisible,"tracking loss hides old guide");
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.13f,.22f);
                foreach(float bearing in new[]{0f,90f,-90f})
                {
                    guide.Hide();Check(guide.Show(camera,new Vector3(0,1.5f,2),bearing,true,safe),"Triplet visible at front bearing "+bearing);
                    guide.ShowDestination(camera,new Vector3(0,1.5f,3),"C","Bàn cạnh cửa sổ");
                    var target=new RenderTexture(412,915,24);var previous=RenderTexture.active;camera.targetTexture=target;
                    camera.Render();
                    var pinBody=guide.transform.Find("Destination pin/Blue pin").GetComponent<MeshRenderer>();
                    var pinLetter=guide.transform.Find("Destination pin/Destination ID").GetComponent<MeshRenderer>();
                    Check(pinLetter.bounds.size.x<pinBody.bounds.size.x*.8f&&pinLetter.bounds.size.y<pinBody.bounds.size.y*.7f,"Destination letter remains inside pin silhouette");
                    RenderTexture.active=target;var picture=new Texture2D(412,915,TextureFormat.RGB24,false);
                    picture.ReadPixels(new Rect(0,0,412,915),0,0);picture.Apply();File.WriteAllBytes("Logs/LabTest/UI/triplet-"+bearing+".png",picture.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(picture);camera.targetTexture=null;RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);
                }
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }

        static void Render(Vector2Int size,string view)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);var target=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("Synthetic Lab UI review");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();
                var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);Set(owner,"Floor",floor);
                var map=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
                Set(owner,"Map",map);
                if(view.StartsWith("setup")){typeof(LabTestSession).GetField("setupStage",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,view=="setupA"?1:view=="setupB"?2:3);Set(owner,"ScanProgress",.68f);Set(owner,"ScanLabel","Đã nhận A · 68% kiểm tra pose\nDịch máy nhẹ, giữ trọn khung thẻ");}
                if(view=="record"){Set(owner,"Recording",true);Set(owner,"ScanProgress",.48f);Set(owner,"ScanLabel","Nhận mốc C · 48% ổn định");}
                if(view=="locate")Set(owner,"Locating",true);
                if(view=="transfer"||view=="putaway"||view=="done")
                {
                    var exercise=new LabExercise(view=="transfer");if(view=="done"){exercise.Scan(LabExercise.Product,false);exercise.Scan("JWM:MAIN:B",true);}Set(owner,"Exercise",exercise);
                }
                if(view=="navigation"||view=="done"){var direct=new LabExercise("B");if(view=="done")direct.ConfirmArrival(true);Set(owner,"Exercise",direct);}
                if(view=="locate"){Set(owner,"ScanProgress",.68f);Set(owner,"ScanLabel","Ước lượng thẻ A · 68% ổn định\nDịch máy nhẹ, giữ toàn khung trong ảnh");}
                Set(owner,"Message",view=="orient"?"Bước 2/2 · giữ vị trí tại A, nâng máy nhìn vào giữa bàn B ở tường trắng đối diện, rồi xác nhận.":view=="locate"||view=="navigation"?"Bước 1/2 · quét toàn thẻ A, dịch máy chậm vài cm để ước lượng kích thước.":view=="done"?"Bạn đã xác nhận tới B. Chưa xác thực bằng QR đích.":"Scan mới 28/10 · bắt đầu tại bàn máy tính A. Chọn xem vị trí hoặc đi tới B / C.");
                if(view.StartsWith("setup")){Set(owner,"Message",view=="setupB"?"Đã giữ A. Để nguyên thẻ A, đi theo lối thật tới B và quét B.":"Giữ A và B cố định. Quét A → B → quay về A để kiểm tra.");Set(owner,"NavigationState",owner.SetupStep);}
                if(view.StartsWith("preview"))
                {
                    Set(owner,"PreviewMode",true);Set(owner,"Exercise",new LabExercise("C"));
                    Set(owner,"Message","Thử mũi tên · quét A rồi đứng trước A và nhìn về B.");
                    Set(owner,"ScanLabel","Đã nhận A · 68% giữ QR trong khung");Set(owner,"ScanProgress",.68f);
                    if(view=="previewHeading")Set(owner,"AwaitingDirection",true);
                }
                if(view.StartsWith("nav")&&view!="navigation")owner.EditorReviewNavigation(view=="navRight"?"SẮP RẼ PHẢI":view=="navLeft"?"RẼ TRÁI":view=="navUTurn"?"QUAY ĐẦU":view=="navInvalid"?"KIỂM TRA VỊ TRÍ":"ĐI THẲNG",view!="navInvalid",view=="navArrived",view=="navEdge");
                if(view.StartsWith("tracking")||view=="recoveryHeadingB")
                {
                    owner.EditorReviewRecovery(view!="trackingWait");
                    Check(!owner.MarkerScanRequested,"Temporary loss/recovery does not demand QR A again");
                    Check(owner.Route.Count>1&&owner.Exercise.Target=="C","Recovery retains route and destination");
                    if(view=="recoveryHeadingB"){owner.BeginPreviewRecoveryAt("B");Check(owner.AwaitingDirection&&owner.PreviewOriginId=="B"&&owner.Exercise.Target=="C","Manual B recovery keeps C destination without QR");}
                }
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Invoke(ui,"Start");
                if(view=="destinations")typeof(LabTestUi).GetField("choosingDestination",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,true);
                var cameraObject=new GameObject("UI review camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.GetComponent<Camera>();camera.targetTexture=target;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.14f,.22f);
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;
                for(int pass=0;pass<3;pass++)
                {
                    Invoke(ui,"LateUpdate");foreach(var text in root.GetComponentsInChildren<Text>())text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);Canvas.ForceUpdateCanvases();
                    foreach(var mini in root.GetComponentsInChildren<LabFloorViewport>()){mini.Refresh();mini.Overlay.Rebuild(CanvasUpdate.PreRender);}
                }
                if(view=="map"||view=="navMap"||view=="trackingMap")
                {
                    ui.OpenMap(false);Canvas.ForceUpdateCanvases();var panel=root.GetComponentInChildren<LabMapPanel>();Invoke(panel,"Update");Canvas.ForceUpdateCanvases();
                    foreach(var viewport in panel.GetComponentsInChildren<LabFloorViewport>()){viewport.Refresh();viewport.Overlay.Rebuild(CanvasUpdate.PreRender);}
                }
                if(view=="trackingMap")Check(root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Đang ở "))==3,"Recovery map exposes A/B/C position confirmation");
                foreach(var text in root.GetComponentsInChildren<Text>())
                {
                    float preferred=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;
                    Check(preferred<=text.rectTransform.rect.height+1,"Clipped "+view+" "+size+" text="+text.text+" need="+preferred+" have="+text.rectTransform.rect.height);
                }
                foreach(var button in root.GetComponentsInChildren<Button>())Check(button.GetComponent<RectTransform>().rect.height>=44,"Touch target too small");
                foreach(var mini in root.GetComponentsInChildren<LabFloorViewport>())
                {
                    var mesh=mini.Overlay.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>20,"Map must render markers and measured paths");
                    Check(mini.GetComponentInChildren<RawImage>().texture!=null,"Actual GLB floor texture must be shown");
                    var point=new LabPoint(-2,0,1);var screen=mini.Project(point);Check(LabPaths.Distance(point,mini.Unproject(screen))<.001,"Map tap inverse uses metric grid coordinates");
                    mini.ChangeZoom(3);screen=mini.Project(point);Check(LabPaths.Distance(point,mini.Unproject(screen))<.001,"Zoomed map tap inverse remains correct");mini.ResetView();
                }
                var hud=root.transform.Find("Lab test HUD/Safe area");var header=hud.Find("Header").GetComponent<RectTransform>();var actions=hud.Find("Actions").GetComponent<RectTransform>();
                var top=new Vector3[4];var bottom=new Vector3[4];header.GetWorldCorners(top);actions.GetWorldCorners(bottom);Check(top[0].y>bottom[1].y,"Header overlaps actions "+view+size);
                var roundTrip=JsonUtility.FromJson<LabSurvey>(JsonUtility.ToJson(map));Check(LabPaths.ValidMap(roundTrip)&&roundTrip.markers.Count==3,"Survey JSON round trip");
                Check(LabRegistration.Solve(roundTrip,floor).valid,"Placement survives saved JSON with same map identity");
                var frame=hud.Find("Marker frame").GetComponent<RectTransform>();if(frame.gameObject.activeSelf)
                {
                    var corners=new Vector3[4];frame.GetWorldCorners(corners);Check(corners[0].y>bottom[1].y,"Frame hidden by action card "+view+size);
                    Check(frame.GetComponentsInChildren<Text>()[0].rectTransform.rect.height>=60,"Scan feedback height");
                }
                foreach(var arrow in root.GetComponentsInChildren<LabScreenDirection>()){arrow.SetVerticesDirty();arrow.Rebuild(CanvasUpdate.PreRender);Check(arrow.canvasRenderer.GetMesh()!=null&&arrow.canvasRenderer.GetMesh().vertexCount>=12,"HUD graphic must render the selected travel or camera-coach icon");}
                camera.Render();RenderTexture.active=target;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes("Logs/LabTest/UI/"+view+"-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=previous;EditorSceneManager.CloseScene(scene,true);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}
