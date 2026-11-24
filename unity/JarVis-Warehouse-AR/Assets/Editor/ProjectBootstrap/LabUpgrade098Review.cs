using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class LabUpgrade098Review
    {
        const string Output="Logs/LabTest/Upgrade098";
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        static int checks;
        static void Check(bool okay,string why){checks++;if(!okay)throw new InvalidOperationException(why);}
        static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Flags).SetValue(owner,value);
        static void Prop(object owner,string field,object value)=>owner.GetType().GetProperty(field,Flags).SetValue(owner,value);
        static void Call(object owner,string method,params object[] values)=>owner.GetType().GetMethod(method,Flags).Invoke(owner,values);
        static LabFloorPlan Floor()=>JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
        static LabFixedMap Trial()=>JsonUtility.FromJson<LabTrialPreset>(Resources.Load<TextAsset>("LabTest/LabTrialNavigation").text).map;
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            Gate();PersistAndRows();
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(412,915)})
                foreach(string state in new[]{"waiting","ready","saved","empty"})Capture(size,state);
            LabUpgrade097Review.Run();
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" focused assertions: explicit-save gate, stale/changed-ID/epoch protection, persistence/reload, distinct AR/map coordinates, CSV export and eight UI fixtures. 097 and 096 regression passed. Physical QR, AR accuracy and crash-free Samsung operation NOT verified.");
            Debug.Log("JARVIS_098_PASS "+checks);
        }
        static void Gate()
        {
            var gate=new LabMarkerSaveGate();var pose=new Pose(new Vector3(1,2,3),Quaternion.identity);
            Check(!gate.Ready(true,true,"A",2,10),"No observation cannot enable save");
            gate.Offer("A",pose,2,10);
            Check(!gate.Ready(false,true,"A",2,10.1),"Tracking loss disables save");
            Check(!gate.Ready(true,false,"A",2,10.1),"Continuity loss disables save");
            Check(!gate.Ready(true,true,"B",2,10.1),"QR identity change disables save");
            Check(!gate.Ready(true,true,"A",3,10.1),"Epoch change disables save");
            Check(!gate.Ready(true,true,"A",2,10.51),"Expired observation disables save");
            Check(!gate.Ready(true,true,"A",2,9.9),"Clock rewind is not a fresh sample");
            Check(!gate.Ready(true,true,"A",2,double.NaN),"NaN clock rejected");
            Check(gate.Take(true,true,"A",2,10.2,out string id,out Pose captured)&&id=="A"&&captured.position==pose.position,"Explicit confirmation consumes exact observation");
            Check(!gate.Take(true,true,"A",2,10.21,out _,out _),"Repeated tap cannot duplicate save");
            gate.Offer("B",pose,3,11);gate.Clear();Check(!gate.Ready(true,true,"B",3,11),"Pause/reset clears candidate");
        }
        static void PersistAndRows()
        {
            var draft=Trial();draft.markers=draft.markers.Where(m=>m.id=="A"||m.id=="C").ToList();draft.survey.markers=draft.survey.markers.Where(m=>m.id=="A"||m.id=="C").ToList();draft.survey.legs.Clear();
            string directory=Path.GetFullPath(Output+"/repository-"+Guid.NewGuid().ToString("N"));
            Func<LabFixedRepository> open=()=>new LabFixedRepository(directory,Floor(),m=>JsonUtility.ToJson(m),t=>JsonUtility.FromJson<LabFixedMap>(t),m=>JsonUtility.ToJson(m),t=>JsonUtility.FromJson<LabFixedPointer>(t));
            var a=draft.survey.placement.pins.First(p=>p.id=="A");a.entrySource=LabPositionEntry.Source;a.expectedErrorMetres=.5f;a.heightEntered=true;a.position.y=.75f;
            string raw=JsonUtility.ToJson(draft.markers.First(m=>m.id=="A").centre);
            open().SaveDraft(draft);var restored=open().LoadDraft();
            Check(restored!=null&&restored.markers.Count==2,"Saved markers survive a fresh repository instance");
            Check(JsonUtility.ToJson(restored.markers.First(m=>m.id=="A").centre)==raw,"Raw coordinates round-trip unchanged");
            var rows=LabSavedMarkerRow.Create(restored);
            Check(rows.Count==5&&rows.Count(r=>r.captured)==2,"Five IDs report two actual captures");
            Check(!rows.Single(r=>r.id=="B").captured&&!rows.Single(r=>r.id=="B").text.Contains("AR tạm"),"Reference B pin cannot masquerade as a saved camera marker");
            Check(rows.Single(r=>r.id=="A").mapEntered&&rows.Single(r=>r.id=="A").text.Contains("Map đã nhập"),"Entered map coordinates labelled");
            Check(!rows.Single(r=>r.id=="C").mapEntered&&rows.Single(r=>r.id=="C").text.Contains("Chưa nhập"),"Reference C pin remains distinct from a user entry");
            rows.Single(r=>r.id=="A").observed.x+=100;
            Check(JsonUtility.ToJson(restored.markers.First(m=>m.id=="A").centre)==raw,"Rows own copies; UI cannot move saved observations");
            var go=new GameObject("098 export fixture");
            try
            {
                var owner=go.AddComponent<LabTestSession>();Prop(owner,"Floor",Floor());Prop(owner,"Map",restored.survey);Set(owner,"fixedDraft",restored);Set(owner,"fixedRepository",open());
                string folder=Path.GetFullPath(Output+"/export");Directory.CreateDirectory(folder);Prop(owner,"Folder",folder);
                Call(owner,"ExportFixedMaps");
                Check(File.Exists(folder+"/saved-marker-coordinates.csv"),"Coordinate CSV included in data export");
                string csv=File.ReadAllText(folder+"/saved-marker-coordinates.csv");
                Check(csv.Contains("AR_DRAFT")&&csv.Contains("USER_ESTIMATED_MAP_ENTRY"),"CSV distinguishes coordinate sources");
                Check(!File.Exists(directory+"/active.json"),"Saving a draft cannot silently activate navigation");
                Check(owner.SavedMarkerCount==2,"Session reports persisted marker count");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Capture(Vector2Int size,string state)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);var rt=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("098 marker saving "+state);SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();Prop(owner,"Floor",Floor());var map=Trial();Prop(owner,"Map",map.survey);Set(owner,"fixedDraft",map);
                owner.CameraOwner=root.AddComponent<VisualMapSession>();var camera=new GameObject("UI camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.23f,.27f);owner.CameraOwner.arCamera=camera;
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Set(owner,"ui",ui);Call(ui,"Start");
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
                bool scanning=state=="waiting"||state=="ready";
                if(scanning)
                {
                    map.markers.RemoveAll(m=>m.id=="B");map.survey.markers.RemoveAll(m=>m.id=="B");
                    Set(owner,"fixedSurvey",true);Set(owner,"ready",true);Set(owner,"reviewCameraTracking",true);
                    if(state=="ready"){Set(owner,"decodedMarker","B");Call(owner,"OfferMarkerSave","B",new Pose(new Vector3(1,2,3),Quaternion.identity),Time.realtimeSinceStartupAsDouble);}
                }
                if(state=="empty"){map.markers.Clear();map.survey.markers.Clear();}
                if(state=="saved")
                {
                    foreach(var p in map.survey.placement.pins){p.entrySource=LabPositionEntry.Source;p.expectedErrorMetres=.5f;}
                    map.markers[0].captureIssue="REVIEW_REQUIRED";
                }
                for(int i=0;i<4;i++){Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();}
                Transform scope=root.transform;
                if(!scanning){ui.OpenSavedMarkers();scope=root.GetComponentInChildren<LabSavedMarkersPanel>().transform;Canvas.ForceUpdateCanvases();}
                foreach(var text in scope.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy&&t.text.Length>0))
                {
                    text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);float h=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;
                    Check(h<=text.rectTransform.rect.height+1,"098 text clipped "+state+": "+text.text);
                }
                var scroll=scope.GetComponentInChildren<ScrollRect>();
                foreach(var b in scope.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy))
                {
                    if(scroll!=null&&b.transform.IsChildOf(scroll.content))continue;
                    var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);
                    foreach(var c in corners){var p=camera.WorldToViewportPoint(c);Check(p.x>=-.001&&p.x<=1.001&&p.y>=-.001&&p.y<=1.001,"098 button clipped "+state);}
                }
                if(scanning)Check(root.GetComponentsInChildren<Button>().Single(b=>b.name=="Lưu mốc").interactable==(state=="ready"),"Save CTA matches gate state");
                else Check(scope.GetComponentsInChildren<ScrollRect>().Length==1,"One list scroll owner");
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Output+"/"+state+"-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Camera>())c.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
