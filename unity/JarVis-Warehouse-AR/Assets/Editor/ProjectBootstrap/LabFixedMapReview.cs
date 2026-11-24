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
    public static class LabFixedMapReview
    {
        static int checks;
        public static string OutputPath="Logs/LabTest/Fixed080";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Field(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        static void Invoke(object o,string name)=>o.GetType().GetMethod(name,Flags).Invoke(o,null);
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks++;}
        public static void ReviewAndBuild(){Run();LabTestBuild.Build();}
        public static void ReviewTrialUi()
        {
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            OutputPath="Logs/LabTest/Ui085";Directory.CreateDirectory(OutputPath);checks=0;
            foreach(var size in new[]{new Vector2Int(360,800),new Vector2Int(412,915)})
                foreach(string view in new[]{"trial-home","trial-destinations","trial-scan","trial-navigation"})Render(size,view);
            File.WriteAllText(Path.Combine(OutputPath,"result.txt"),"PASS "+checks+" focused trial UI checks; 8 rendered fixtures; physical camera NOT VERIFIED.\n");
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            Directory.CreateDirectory(OutputPath);checks=0;
            ReviewPoseMath();ReviewDestinationContinuity();
            foreach(var size in new[]{new Vector2Int(412,915),new Vector2Int(412,732),new Vector2Int(360,800)})
                foreach(string view in new[]{"home","destinations","survey","scan","saved","editing","navigation","recovery","data","import","scan-help","rejected","survey-recovery","need-reference","saved-review"})Render(size,view);
            File.WriteAllText(Path.Combine(OutputPath,"result.txt"),"PASS "+checks+" native UI checks; 45 rendered fixtures. Camera/physical accuracy/performance NOT RUN.\n");
        }
        static void ReviewPoseMath()
        {
            foreach(float yaw in new[]{0f,90f,-90f,179f})foreach(var mount in new[]{Quaternion.identity,Quaternion.Euler(90,0,0),Quaternion.Euler(0,0,90),Quaternion.Euler(90,42,0)})
            {
                var origin=new Vector3(4,.3f,-2);var rotation=Quaternion.Euler(0,yaw,0);var centre=new LabPoint(-1,.8f,3);
                var seen=new Pose(origin+rotation*new Vector3(centre.x,centre.y,centre.z),rotation*mount);
                Check(LabFixedPose.TryWorldFromMap(seen,centre,new[]{mount.x,mount.y,mount.z,mount.w},out var result),"Full pose for wall/flat card yaw="+yaw);
                Check(Vector3.Distance(result.position,origin)<.0001f&&Quaternion.Angle(result.rotation,rotation)<.05f,"Marker normal must not reverse map axes");
                var camera=new Vector3(6,1.4f,2);var map=Quaternion.Inverse(result.rotation)*(camera-result.position);
                Check(Vector3.Distance(result.position+result.rotation*map,camera)<.0001f,"Camera/map transform round trip");
            }
            Check(!LabFixedPose.TryWorldFromMap(new Pose(Vector3.zero,Quaternion.Euler(30,0,0)),new LabPoint(),new[]{0f,0,0,1},out _),"Reject unexplained gravity tilt");
            Check(!LabFixedPose.TryWorldFromMap(Pose.identity,new LabPoint(),new[]{0f,0,0,0},out _),"Reject invalid saved quaternion");
        }
        static void ReviewDestinationContinuity()
        {
            var root=new GameObject("Fixed destination continuity fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);var map=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
                Set(owner,"Floor",floor);Set(owner,"Map",map);Field(owner,"ready",true);var active=new LabFixedMap{survey=map};
                foreach(var m in map.markers)active.markers.Add(new LabFixedMarker{id=m.id});Field(owner,"fixedActive",active);
                owner.EditorReviewNavigation("ĐI THẲNG",true,false,false);int epoch=(int)typeof(LabTestSession).GetField("epoch",Flags).GetValue(owner);
                owner.StartFixedDestination("A");Check(owner.Exercise.Target=="A","A is a normal destination");Check((int)typeof(LabTestSession).GetField("epoch",Flags).GetValue(owner)==epoch,"Changing destination preserves valid map reference");
                owner.ChangeDestination("B");Check(owner.Exercise.Target=="B","Change destination from live position");
                typeof(LabTestSession).GetMethod("RequireTrackingReference",Flags).Invoke(owner,new object[]{"TEST_LOSS"});Check(owner.Exercise.Target=="B"&&owner.RecoveryNeedsMarker,"Recovery keeps B instead of forcing A");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void Render(Vector2Int size,string view)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var target=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("Fixed map UI review");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();
                var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
                var map=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
                Set(owner,"Floor",floor);Set(owner,"Map",map);Field(owner,"ready",true);
                var fixture=new LabFixedMap{mapId=floor.id,floorHash=floor.geometrySha256,floorRevision=floor.revision,survey=map};
                foreach(var m in map.markers)fixture.markers.Add(new LabFixedMarker{id=m.id,centre=m.centre,access=m.access,rotation=new[]{0f,0,0,1},samples=8});
                Field(owner,"fixedActive",fixture);Set(owner,"Message","Bản kiểm tra giao diện · chưa nghiệm thu camera thật");
                if(view=="survey"||view=="saved")
                {Field(owner,"fixedSurvey",true);Field(owner,"fixedDraft",fixture);Set(owner,"ScanProgress",view=="saved"?1f:.68f);Set(owner,"ScanLabel",view=="saved"?"Đã lưu mốc A · 3/5":"Đã nhận C · 68% kiểm tra vị trí\nGiữ trọn khung thẻ · 6/8 mẫu ổn định");}
                if(view=="scan-help")
                {
                    // A negative start time means "not scanning". Keep the fixture valid even
                    // when a fast batch editor has been open for less than ten seconds.
                    if(Time.realtimeSinceStartupAsDouble<10.1d)System.Threading.Thread.Sleep((int)Math.Ceiling((10.1d-Time.realtimeSinceStartupAsDouble)*1000));
                    Set(owner,"Locating",true);Field(owner,"scanAttemptAt",Time.realtimeSinceStartupAsDouble-10d);Set(owner,"ScanLabel","Đã nhận C · 68% kiểm tra vị trí\nGiữ trọn khung thẻ · 6/8 mẫu ổn định\nNếu % đứng yên: giữ thẻ phẳng, đưa máy chậm hơn");Set(owner,"ScanProgress",.68f);
                }
                if(view=="scan"){Set(owner,"Locating",true);Set(owner,"ScanProgress",.42f);Set(owner,"ScanLabel","Đã nhận B · 42% kiểm tra vị trí\nGiữ trọn khung thẻ · 4/8 mẫu ổn định");}
                if(view=="saved-review")
                {Field(owner,"fixedSurvey",true);Field(owner,"fixedDraft",fixture);Set(owner,"ScanProgress",1f);Set(owner,"ScanLabel","Đã lưu nháp C · 3/5\nVị trí cần đối chiếu map · có thể ghi tiếp thẻ khác.");Set(owner,"Message","Đã giữ dữ liệu C. Tiếp tục ghi D/E rồi xuất ZIP để đối chiếu.");}
                if(view=="rejected"||view=="survey-recovery"||view=="need-reference")
                {
                    Field(owner,"fixedSurvey",true);Field(owner,"fixedDraft",fixture);Field(owner,"fixedActive",null);
                    Set(owner,"ScanProgress",0f);
                    string label=view=="rejected"?"Chưa lưu C · khoảng cách tới mốc đã lưu chưa khớp map\nGiữ yên để thử lại; nếu còn lệch, quét mốc đã lưu gần bạn.":view=="survey-recovery"?"Tracking tạm gián đoạn · đang giữ các mốc\nDừng một lát, nhìn vùng vừa đi. Chưa lưu thêm mốc.":"Đã đọc D · chưa lưu vị trí\nCần nối lại bằng A / B đã lưu gần bạn.";
                    Set(owner,"ScanLabel",label);Set(owner,"Message",label);
                }
                if(view.StartsWith("trial-"))
                {
                    view=view.Substring(6);Field(owner,"fixedActive",null);Invoke(owner,"LoadFixedTrial");Invoke(owner,"ApplyFixedMap");
                    Set(owner,"Message",owner.FixedSummary);
                    if(view=="scan"){Set(owner,"Locating",true);Set(owner,"ScanProgress",.42f);Set(owner,"ScanLabel","Đã nhận B · 42% kiểm tra vị trí\nGiữ trọn khung thẻ · 4/8 mẫu ổn định");}
                }
                if(view=="navigation")owner.EditorReviewNavigation("RẼ PHẢI",true,false,false);
                if(view=="recovery"){owner.EditorReviewRecovery(false);Set(owner,"PreviewMode",false);}
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Invoke(ui,"Start");
                if(view=="destinations")Field(ui,"choosingDestination",true);
                if(view=="data")Field(ui,"showingData",true);
                if(view=="import")Field(owner,"importCandidate",fixture);
                var camera=new GameObject("UI render camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.targetTexture=target;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.14f,.22f);
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;
                for(int pass=0;pass<4;pass++){Invoke(ui,"LateUpdate");foreach(var text in root.GetComponentsInChildren<Text>())text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);Canvas.ForceUpdateCanvases();}
                if(view=="editing"){ui.OpenMap(true);Canvas.ForceUpdateCanvases();Invoke(root.GetComponentInChildren<LabMapPanel>(),"Update");Canvas.ForceUpdateCanvases();}
                foreach(var viewport in root.GetComponentsInChildren<LabFloorViewport>()){viewport.Refresh();viewport.Overlay.Rebuild(CanvasUpdate.PreRender);var p=new LabPoint(-2,0,1);Check(LabPaths.Distance(p,viewport.Unproject(viewport.Project(p)))<.001f,"Map coordinate round trip");}
                foreach(var text in root.GetComponentsInChildren<Text>())
                {
                    float need=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;
                    Check(need<=text.rectTransform.rect.height+1,"Clipped text "+view+" "+size+" "+text.text+" need="+need+" have="+text.rectTransform.rect.height);
                }
                foreach(var button in root.GetComponentsInChildren<Button>())Check(button.GetComponent<RectTransform>().rect.height>=44,"Touch area "+button.name);
                if(view=="scan-help")
                {Check(owner.ScanNeedsHelp,"Scan timeout fixture must enter real help state");Check(root.GetComponentsInChildren<Button>().Any(b=>b.name=="Quét lại · giữ dữ liệu và đích"),"Scan timeout must offer retry without clearing data");}
                if(view=="destinations")
                {Check(root.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("Tới "))==5,"Five destination choices");Check(root.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("Tới D")).interactable==owner.CanChooseFixed("D"),"D availability matches actual navigation map");if(owner.TrialNavigation)Check(root.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("Tới ")).All(b=>b.interactable),"All five trial destinations enabled");}
                if(view=="editing")Check(root.GetComponentsInChildren<Button>().Count(b=>b.name.Length==1&&b.name[0]>='A'&&b.name[0]<='E')==5,"All marker placement selectors present");
                var safe=root.transform.Find("Lab test HUD/Safe area");var frame=(RectTransform)safe.Find("Marker frame");var actions=(RectTransform)safe.Find("Actions");var a=new Vector3[4];var b=new Vector3[4];actions.GetWorldCorners(a);
                if(frame.gameObject.activeSelf){var label=frame.GetComponentInChildren<Text>().rectTransform;label.GetWorldCorners(b);Check(b[0].y>a[1].y,"Scan feedback must not overlap actions "+size);}
                camera.Render();RenderTexture.active=target;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(OutputPath+"/"+view+"-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=previous;EditorSceneManager.CloseScene(scene,true);target.Release();UnityEngine.Object.DestroyImmediate(target);}
        }
    }
}

