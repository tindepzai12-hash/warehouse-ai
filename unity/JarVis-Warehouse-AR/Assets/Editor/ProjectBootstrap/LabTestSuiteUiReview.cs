using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class LabTestSuiteUiReview
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Call(object o,string name)=>o.GetType().GetMethod(name,Flags).Invoke(o,null);
        static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        public static void Run()
        {
            foreach(var size in new[]{new Vector2Int(412,915),new Vector2Int(360,640)})
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
                var rt=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;LabTestSuite suite=null;
                try
                {
                    var root=new GameObject("Guided test UI fixture");SceneManager.MoveGameObjectToScene(root,scene);
                    var owner=root.AddComponent<LabTestSession>();var camera=new GameObject("UI camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.23f,.28f);
                    owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=camera;
                    var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
                    var map=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
                    Prop(owner,"Floor",floor);Prop(owner,"Map",map);Set(owner,"floorRouter",new LabFloorRouter(floor));Call(owner,"InitializePlanner");
                    suite=new LabTestSuite(Path.GetFullPath("Logs/LabTest/Upgrade094/ui-"+Guid.NewGuid().ToString("N")),"0.9.4","SYNTHETIC_UI");suite.Select(7);Set(owner,"testSuite",suite);
                    var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Set(owner,"ui",ui);Call(ui,"Start");Set(ui,"showingSuite",true);
                    var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
                    for(int pass=0;pass<4;pass++){Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();}
                    if(!ui.BlocksCameraGuidance)throw new InvalidOperationException("Test panel must stop walking overlays");
                    foreach(var button in root.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy))
                    {
                        var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                        foreach(var corner in corners){var uv=camera.WorldToViewportPoint(corner);if(uv.y<0||uv.y>1||uv.x<0||uv.x>1)throw new InvalidOperationException("Test button clipped: "+button.name);}
                    }
                    camera.Render();RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes("Logs/LabTest/Upgrade094/suite-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                    suite.Begin(1,new System.Collections.Generic.Dictionary<string,string>());Set(ui,"showingSuite",false);Call(ui,"LateUpdate");
                    var status=(Text)ui.GetType().GetField("status",Flags).GetValue(ui);status.transform.parent.GetComponent<Button>().onClick.Invoke();
                    if(!ui.BlocksCameraGuidance)throw new InvalidOperationException("Active test must remain finishable during scan/heading/recovery");
                    suite.Finish("STOPPED",2);
                }
                finally{RenderTexture.active=previous;EditorSceneManager.CloseScene(scene,true);suite?.Dispose();rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }
    }
}
