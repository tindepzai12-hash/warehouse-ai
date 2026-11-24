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
    // Real decision -> presentation -> UI code with synthetic poses, NOT native AR acceptance.
    public static class LabLiveCueRuntimeReview
    {
        static int checks;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
        static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
        static void Field(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        public static void Run()
        {
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            LabTestBuild.Prepare();Directory.CreateDirectory("Logs/LabTest/Live070/G2");checks=0;File.WriteAllText("Logs/LabTest/Live070/G2/render-trace.txt","");
            File.WriteAllText("Logs/LabTest/Live070/G2/result.txt","RUNNING\n");LabTestReview.RunUpgrade();ReviewPartialClipping();
            foreach(var size in new[]{new Vector2Int(412,915),new Vector2Int(412,732),new Vector2Int(360,800)})
                foreach(string target in new[]{"B","C"})Replay(size,target);
            File.WriteAllText("Logs/LabTest/Live070/G2/result.txt","PASS "+checks+" actual controller/presentation checks plus legacy UI regression. Synthetic camera/poses only. Samsung walk, tracking, GC/frame time and glare remain pending.\n");
        }
        static void ReviewPartialClipping()
        {
            var root=new GameObject("Partial clipping fixture");
            try
            {
                var camera=root.AddComponent<Camera>();camera.transform.position=new Vector3(0,1.35f,0);camera.transform.rotation=Quaternion.Euler(18,0,0);camera.fieldOfView=60;camera.aspect=412f/915;camera.nearClipPlane=.05f;
                var frame=new GameObject("Fixture frame").transform;frame.SetParent(root.transform,false);
                var guide=new GameObject("Fixture visual").AddComponent<LabGuideVisual>();guide.transform.SetParent(root.transform,false);guide.Initialize(Resources.Load<Material>("Routes/LiveViewUnlit"));
                var context=new LabRouteVisualContext(new[]{new LabPoint(0,0,0),new LabPoint(0,0,10)},1,1,"B");
                var d=new LabNavigationDecision(1,1,1,1,true,false,"B","ĐI THẲNG",null,new LabPoint(0,0,2),10,10,0,LabTurnPhase.Cruise,null,context);
                var tokens=new[]{new LabCueToken(3,1.65f,0,1.65f,0,1),new LabCueToken(4,2.2f,0,2.2f,0,1),new LabCueToken(5,2.75f,0,2.75f,0,1)};
                bool shown=guide.ShowPath(camera,frame,d,tokens,3,.08f,new Rect(.03f,.15f,.94f,.80f),null,1,true);
                Check(!shown&&guide.VisibleCount==0,"A clipped cluster uses the complete HUD instruction instead of edge fragments");
                for(int i=0;i<3;i++)tokens[i]=new LabCueToken(i,0,0,-1-i,0,1);
                Check(!guide.ShowPath(camera,frame,d,tokens,3,.08f,new Rect(0,0,1,1),null,2,true)&&guide.VisibleCount==0,"Behind-camera glyphs are hidden together");
                var stale=new LabNavigationDecision(2,2,2,2,true,false,"C","ĐI THẲNG",null,new LabPoint(0,0,2),10,10,0,LabTurnPhase.Cruise,null,context);
                Check(!guide.ShowPath(camera,frame,stale,tokens,3,.08f,new Rect(0,0,1,1),null,3,true),"Stale visual context cannot render after target/revision change");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void Replay(Vector2Int size,string destination)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var rt=new RenderTexture(size.x,size.y,24);var old=RenderTexture.active;
            try
            {
                var root=new GameObject("Runtime pipeline replay");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();var frame=new GameObject("Metric map frame").transform;frame.SetParent(root.transform,false);
                var camera=new GameObject("Replay camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.targetTexture=rt;camera.fieldOfView=60;camera.nearClipPlane=.05f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.20f,.24f,.28f);
                owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=camera;
                var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
                var map=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
                Prop(owner,"Floor",floor);Prop(owner,"Map",map);Prop(owner,"Exercise",new LabExercise(destination));Prop(owner,"PreviewMode",true);
                Field(owner,"reviewLocated",true);Field(owner,"reviewRuntime",true);Field(owner,"journeyStarted",true);Field(owner,"epoch",17);Field(owner,"mapFrame",frame);Field(owner,"floorRouter",new LabFloorRouter(floor));
                Field(owner,"preferencesLoaded",true);Field(owner,"reducedMotion",true);((LabFloorVisualReference)Field(owner,"floorVisual")).Begin(true);Call(owner,"InitializePlanner");
                var estimate=(LabNavigationEstimate)Field(owner,"poseEstimate");var router=(LabFloorRouter)Field(owner,"floorRouter");
                var start=map.markers.First(m=>m.id=="A").access;
                Field(owner,"reviewPosition",start);estimate.Update(router,start,0,1,1,true);Call(owner,"UpdateFloorRoute",1d);
                Check(owner.Decision!=null&&owner.Decision.VisualMatches,"Initial route snapshot matches epoch/revision/target");
                var route=owner.Decision.visualRoute;Check(route!=null&&route.Length>1,"Authored route found for "+destination);
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Field(owner,"ui",ui);Call(ui,"Start");
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
                int frameIndex=0,worldFrames=0,turnFrames=0;double clock=2;
                for(float at=0;at<route.Length-.5f;at+=.12f)
                {
                    route.Sample(at,out var token);var here=new LabPoint(token.x,0,token.z);Field(owner,"reviewPosition",here);
                    camera.transform.position=new Vector3(here.x,1.35f,here.z);camera.transform.rotation=Quaternion.LookRotation(new Vector3(token.dx,-.35f,token.dz));
                    estimate.Update(router,here,token.dx,token.dz,clock,true);Call(owner,"UpdateFloorRoute",clock);clock+=.12;
                    for(int pass=0;pass<3;pass++){Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();}
                    var sample=owner.Presentation;
                    Check(owner.Decision.VisualMatches,"Replay snapshot stays aligned");Check(sample.visibleTokens<=3,"Never more than three world glyphs");
                    Check(sample.decisionId==owner.Decision.id,"Labels and glyph use same decision");
                    var visual=Field(owner,"navigationVisual") as LabGuideVisual;
                    if(sample.hud&&sample.intent!=LabCueIntent.Hold)Check(((RectTransform)Field(ui,"edge")).gameObject.activeSelf,"Fallback is actually visible");
                    if(visual!=null&&visual.CueVisible)for(int k=0;k<3;k++)if(visual.Rig.Glyph(k).gameObject.activeSelf){Check(visual.Rig.ProjectedBounds(camera,k,out var bounds)&&ui.GuideBoundsAllowed(bounds),"Every visible whole marker stays outside UI");}
                    if(sample.visibleTokens>0)worldFrames++;
                    if(sample.mode==LabCueMode.TurnBoard)turnFrames++;
                    if(frameIndex%25==0||sample.mode==LabCueMode.TurnBoard&&frameIndex%6==0)Save(root,camera,rt,destination+"-"+size.x+"x"+size.y+"-"+frameIndex.ToString("D3"));
                    if(frameIndex==25){
                // Turn the camera without moving: spatial route and accepted position must not change.
                var accepted=owner.Position;int rev=owner.Decision.routeRevision;camera.transform.Rotate(0,180,0,Space.World);Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();
                Check(owner.Decision.routeRevision==rev&&owner.Position==accepted,"Camera rotation cannot reroute or move accepted pose");
                Check(owner.Presentation.intent==LabCueIntent.LookToRoute,"Looking away gives a camera coach");Save(root,camera,rt,destination+"-"+size.x+"-look-away");camera.transform.Rotate(0,-180,0,Space.World);Call(ui,"LateUpdate");
                    }
                    frameIndex++;
                }
                Check(worldFrames>0,"World glyphs appear on actual route "+destination+" "+size);
                if(destination=="C")Check(turnFrames>0,"Turn board reached on "+destination+" "+size);
                estimate.Current.valid=false;Call(ui,"LateUpdate");
                Check(owner.Presentation.intent==LabCueIntent.Hold&&owner.Presentation.visibleTokens==0,"Invalid pose hides movement immediately");
                Check(owner.Exercise.Target==destination,"Pose hold preserves target");Save(root,camera,rt,destination+"-"+size.x+"-invalid");
                Field(owner,"epoch",18);owner.RefreshPresentation();Check(owner.Presentation.intent==LabCueIntent.Hold,"Epoch change rejects an old decision immediately");
                estimate.Reset();estimate.Update(router,owner.RawPosition,0,1,clock+1,true);Call(owner,"UpdateFloorRoute",clock+1);owner.RefreshPresentation();
                Check(owner.Decision.VisualMatches,"New epoch republishes a matching visual route");
                owner.ChangeDestination(destination=="B"?"C":"B");Call(owner,"UpdateFloorRoute",clock+2);owner.RefreshPresentation();
                Check(owner.Decision.target==owner.Exercise.Target&&owner.Decision.VisualMatches,"Changing destination synchronizes route and visuals");
                estimate.Current.valid=false;Call(ui,"LateUpdate");
                ui.OpenVisualSettings();Canvas.ForceUpdateCanvases();Call(ui,"LateUpdate");Save(root,camera,rt,destination+"-"+size.x+"-settings");
                var oldPos=owner.RawPosition;var before=frame.localToWorldMatrix;owner.AdjustVisualHeight(.1f);owner.ResetVisualHeight();
                Check(owner.RawPosition==oldPos&&frame.localToWorldMatrix==before,"Height controls cannot move map or camera");
                foreach(var t in root.GetComponentsInChildren<Text>())
                {
                    float need=t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text,t.GetGenerationSettings(t.rectTransform.rect.size))/t.pixelsPerUnit;
                    Check(need<=t.rectTransform.rect.height+1,"Replay text clipped: "+t.text);
                }
                foreach(var b in root.GetComponentsInChildren<Button>())Check(b.GetComponent<RectTransform>().rect.height>=44,"Replay touch target");
            }
            finally{foreach(var rootObject in scene.GetRootGameObjects())foreach(var c in rootObject.GetComponentsInChildren<Camera>())c.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.CloseScene(scene,true);}
        }
        static void Save(GameObject root,Camera camera,RenderTexture rt,string name)
        {
            foreach(var text in root.GetComponentsInChildren<Text>())text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
            foreach(var mini in root.GetComponentsInChildren<LabFloorViewport>()){mini.Refresh();mini.Overlay.Rebuild(CanvasUpdate.PreRender);}
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;var pic=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);pic.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pic.Apply();File.WriteAllBytes("Logs/LabTest/Live070/G2/"+name+".png",pic.EncodeToPNG());UnityEngine.Object.DestroyImmediate(pic);
            var owner=root.GetComponent<LabTestSession>();var ui=root.GetComponent<LabTestUi>();var sample=owner.Presentation;
            var info=name+" intent="+sample.intent+" mode="+sample.mode+" hud="+sample.hud+" tokens="+sample.visibleTokens+" reason="+sample.reason+" safe="+ui.GuideViewport+" camera="+camera.transform.position+" scale="+root.GetComponentInChildren<Canvas>().scaleFactor;
            var g=Field(owner,"navigationVisual") as LabGuideVisual;
            if(g!=null)for(int i=0;i<3;i++){g.Rig.ProjectedBounds(camera,i,out var r);info+="\n slot "+i+" active="+g.Rig.Glyph(i).gameObject.activeInHierarchy+" bounds="+r+" allowed="+ui.GuideBoundsAllowed(r);}
            var edge=(RectTransform)Field(ui,"edge");info+="\n hud="+edge.gameObject.activeSelf+" local="+edge.anchoredPosition+" rect="+edge.rect;
            File.AppendAllText("Logs/LabTest/Live070/G2/render-trace.txt",info+"\n");
        }
    }
}
