using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class LabUpgrade096Review
    {
        public const string Output="Logs/LabTest/Upgrade096";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;static int checks;
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);checks++;}
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Flags).Invoke(o,null);
        static T Clone<T>(T value)=>JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        static LabFloorPlan Floor()=>JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
        static LabFixedMap Trial()=>JsonUtility.FromJson<LabTrialPreset>(Resources.Load<TextAsset>("LabTest/LabTrialNavigation").text).map;
        static LabFixedMap Draft()
        {
            var map=Clone(Trial());map.survey.id="position-review-096";map.survey.purpose="Position recording draft";
            map.markers=map.markers.Where(m=>m.id=="A").ToList();map.survey.markers=map.survey.markers.Where(m=>m.id=="A").ToList();map.survey.legs.Clear();return map;
        }
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            var floor=Floor();var draft=Draft();Check(draft.Validate(floor,false)==null,"One recorded point remains a valid draft");
            var placement=Clone(draft.survey.placement);var centre=placement.pins.First(p=>p.id=="A").position;
            string x=centre.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture),z=centre.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
            Check(LabPositionEntry.TrySet(placement,floor,"A",x,z,"73,5","50",out _),"Approximate entry accepts comma height");
            var pin=placement.pins.First(p=>p.id=="A");Check(pin.approximate&&pin.heightEntered&&Math.Abs(pin.position.y-.735f)<.00001f&&pin.expectedErrorMetres==.5f,"Entry stores units and estimated tolerance explicitly");
            Check(LabPositionEntry.Number(" −5,82 ",out var negative)&&Math.Abs(negative+5.82f)<.00001f,"Signed decimal coordinate accepted");
            Check(LabPositionEntry.Number("1E-05",out _),"Round-trip float near zero is accepted");
            string before=JsonUtility.ToJson(placement);
            foreach(var bad in new[]{"","NaN","Infinity","1.2.3"})
            {Check(!LabPositionEntry.TrySet(placement,floor,"A",bad,z,"","50",out _),"Invalid numeric entry rejected");Check(JsonUtility.ToJson(placement)==before,"Rejected edit preserves previous values");}
            Check(!LabPositionEntry.TrySet(placement,floor,"A","10000",z,"","50",out _),"Outside-map coordinate rejected");
            Check(!LabPositionEntry.TrySet(placement,floor,"A",x,z,"301","50",out _),"Unreasonable height rejected");
            Check(!LabPositionEntry.TrySet(placement,floor,"A",x,z,"","0",out _),"Invalid tolerance rejected");
            Check(!LabPositionEntry.TrySet(placement,floor,"F",x,z,"","50",out _),"Unknown marker rejected");
            Check(LabPositionEntry.TrySet(placement,floor,"A",x,z,"","5",out _)&&LabPositionEntry.Validate(placement,floor)==null,"Minimum tolerance remains valid after float conversion");
            Check(!pin.heightEntered&&Math.Abs(pin.position.y-.735f)<.00001f,"Blank height is explicitly unknown, not fabricated floor zero");
            pin.expectedErrorMetres=.5f;
            var go=new GameObject("Position persistence fixture");
            try
            {
                var owner=go.AddComponent<LabTestSession>();Prop(owner,"Floor",floor);Prop(owner,"Map",draft.survey);Set(owner,"fixedDraft",draft);var active=Trial();Set(owner,"fixedActive",active);
                var repo=new LabFixedRepository(Path.GetFullPath(Output+"/repository-"+Guid.NewGuid().ToString("N")),floor,v=>JsonUtility.ToJson(v),v=>JsonUtility.FromJson<LabFixedMap>(v),v=>JsonUtility.ToJson(v),v=>JsonUtility.FromJson<LabFixedPointer>(v));Set(owner,"fixedRepository",repo);
                string original=JsonUtility.ToJson(draft.markers),activeBefore=JsonUtility.ToJson(active);
                Check(owner.SavePositionEntryDraft(placement),"One-point draft saves without loop or registration");
                var restored=repo.LoadDraft();Check(restored!=null&&restored.survey.placement.pins.First(p=>p.id=="A").expectedErrorMetres==.5f,"Entry survives restart/JSON persistence");
                Check(JsonUtility.ToJson(draft.markers)==original&&JsonUtility.ToJson(active)==activeBefore,"Input cannot rewrite raw observations or active map");
                placement.pins.First(p=>p.id=="A").position.x+=1;Check(JsonUtility.ToJson(restored)==JsonUtility.ToJson(repo.LoadDraft()),"Saved placement owns a private copy");
                var wrong=Clone(placement);wrong.revision++;Check(!owner.SavePositionEntryDraft(wrong),"Different map revision rejected");
                Check(!File.Exists(Path.Combine(repo.DirectoryPath,"active.json")),"Draft cannot silently publish an active map");
                string folder=Path.GetFullPath(Output+"/export");Directory.CreateDirectory(folder);Prop(owner,"Folder",folder);Call(owner,"ExportFixedMaps");
                Check(File.Exists(folder+"/position-entry.json")&&File.Exists(folder+"/fixed-map-draft.json")&&File.Exists(folder+"/POSITION-ENTRY-README.txt"),"Export has entered coordinates and original poses");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(412,915)})foreach(var state in new[]{"map","entry","data"})Capture(size,state);
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" focused entry/persistence/export/layout checks. Six UI fixtures. Camera, QR, tracking and position accuracy on Samsung NOT verified.");
            Debug.Log("JARVIS_096_PASS "+checks);
        }
        static void Capture(Vector2Int size,string state)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);var rt=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("Lab 096 position-entry UI fixture");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();Prop(owner,"Floor",Floor());var draft=Draft();Prop(owner,"Map",draft.survey);Set(owner,"fixedDraft",draft);Set(owner,"fixedTrial",Trial());
                owner.CameraOwner=root.AddComponent<VisualMapSession>();var camera=new GameObject("UI camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.23f,.27f);owner.CameraOwner.arCamera=camera;
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Set(owner,"ui",ui);Call(ui,"Start");
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
                if(state=="data")Set(ui,"showingData",true);
                for(int i=0;i<4;i++){Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();}
                Transform scope=root.transform;
                if(state!="data")
                {
                    ui.OpenMap(true);var panel=root.GetComponentInChildren<LabMapPanel>();scope=panel.transform;
                    if(state=="entry"){Call(panel,"OpenEntry");scope=root.GetComponentInChildren<LabPositionEntryPanel>().transform;}
                    Canvas.ForceUpdateCanvases();
                }
                foreach(var text in scope.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy))
                {
                    if(text.text=="")continue;text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
                    float height=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;
                    Check(height<=text.rectTransform.rect.height+1,"096 text clips "+state+" "+size+": "+text.text);
                }
                foreach(var button in scope.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy))
                {
                    var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach(var c in corners){var p=camera.WorldToViewportPoint(c);Check(p.x>=-.001&&p.x<=1.001&&p.y>=-.001&&p.y<=1.001,"096 button clipped "+state+": "+button.name);}
                }
                camera.Render();RenderTexture.active=rt;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+state+"-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            finally{foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Camera>())c.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
