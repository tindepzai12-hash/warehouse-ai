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
    // Layout fixture only. Physical camera acceptance is deliberately not inferred from it.
    public static class LabUpgrade095UiReview
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
        static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
        static void Call(object o,string n)=>o.GetType().GetMethod(n,Flags).Invoke(o,null);
        public static void Run()
        {
            Directory.CreateDirectory(LabUpgrade095Review.Output);
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(412,915)})
                foreach(var state in new[]{"pose-rejected","heading","hud","look"})Capture(size,state);
        }
        static void Capture(Vector2Int size,string state)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var rt=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("Lab 095 UI layout fixture");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();
                var camera=new GameObject("UI camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.23f,.27f);
                owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=camera;
                Prop(owner,"Floor",JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text));
                Prop(owner,"Map",JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text));
                owner.EditorReviewNavigation("RẼ PHẢI",state!="pose-rejected",false,state=="look");Set(owner,"journeyStarted",true);
                if(state=="pose-rejected")
                {
                    var estimate=(LabNavigationEstimate)Get(owner,"poseEstimate");estimate.Current.reason="OBSTACLE";estimate.Current.valid=false;
                    Prop(owner,"ManeuverTitle",LabPoseFeedback.Title("OBSTACLE"));Set(owner,"reviewRuntime",true);
                }
                if(state=="heading")
                {
                    Set(owner,"trialHeadingPending",true);Set(owner,"trialOrigin","B");Set(owner,"trialLook","E");
                    var confirmation=(LabHeadingConfirmation)Get(owner,"previewHeading");
                    confirmation.Observe(1,new LabPoint(0,0,0),0,1,true);confirmation.Observe(1.384,new LabPoint(0,0,0),0,1,true);
                    Prop(owner,"AwaitingDirection",true);Prop(owner,"Message","Đứng gần thẻ B, ngắm tâm thẻ E. Giữ yên 64%, rồi xác nhận. % là độ ổn định, không kiểm chứng bạn ngắm đúng thẻ.");
                }
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Set(owner,"ui",ui);Call(ui,"Start");
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
                for(int i=0;i<4;i++){Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();}
                if(state=="hud"||state=="look")
                {
                    // Capture the settled animation, not its deliberately transparent first frame.
                    var motion=(LabCueChoreography)Get(ui,"hudMotion");
                    var tokens=(LabCueToken[])Get(ui,"hudTokens");double now=Time.realtimeSinceStartupAsDouble;
                    motion.Invalidate();motion.Context(0,0,null,state=="look"?LabCueMode.Look:LabCueMode.Hud,now-.8);
                    motion.Sync(tokens,3,now-.8);Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();
                }
                foreach(var text in root.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy))
                {
                    text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
                    float height=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;
                    if(height>text.rectTransform.rect.height+1)throw new InvalidOperationException("095 UI text clipped "+size+" "+state+": "+text.text);
                }
                foreach(var button in root.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy))
                {
                    var corners=new Vector3[4];((RectTransform)button.transform).GetWorldCorners(corners);
                    foreach(var corner in corners){var p=camera.WorldToViewportPoint(corner);if(p.x<0||p.x>1||p.y<0||p.y>1)throw new InvalidOperationException("095 UI button clipped: "+button.name);}
                }
                if(state=="pose-rejected"&&(owner.Presentation.intent!=LabCueIntent.Hold||owner.Exercise.Target!="C"))throw new InvalidOperationException("Pose rejection must hold and retain the target");
                camera.Render();RenderTexture.active=rt;var texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes(LabUpgrade095Review.Output+"/"+state+"-"+size.x+"x"+size.y+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            }
            finally{foreach(var go in scene.GetRootGameObjects())foreach(var c in go.GetComponentsInChildren<Camera>())c.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
