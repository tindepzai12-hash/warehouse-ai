using System;
using System.Collections;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class LabScanHotfixReview
    {
        static int checks;
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("SCAN_081: "+label);checks++;Debug.Log("SCAN_081 PASS "+label);}
        static void Field(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
        static void Set(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        static object Decode(byte[] pixels,int width,int height)=>typeof(LabTestSession).GetMethod("DecodeMarkerFrame",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{pixels,width,height});
        static string Id(object result)=>(string)result.GetType().GetField("id").GetValue(result);
        static int Count(object result)=>(int)result.GetType().GetField("count").GetValue(result);
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            checks=0;Directory.CreateDirectory("Logs/LabTest/Scan081");ReviewReferenceAliases();ReviewQrImages();
            File.WriteAllText("Logs/LabTest/Scan081/result.txt","PASS "+checks+" focused scanner checks. Includes five reference hypotheses for one card, flat/wall poses, rotated/slanted QR images, two real QR identities, stale/invalid evidence. Physical Samsung camera remains NOT VERIFIED.\n");
        }
        static void ReviewReferenceAliases()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                var root=new GameObject("Scanner regression: one paper, five hypotheses");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();var cameraOwner=root.AddComponent<VisualMapSession>();
                var camera=root.AddComponent<Camera>();cameraOwner.arCamera=camera;owner.CameraOwner=cameraOwner;
                Set(owner,"Floor",new LabFloorPlan{id="LAB-GLB-20261028"});
                var visible=(IDictionary)typeof(LabTestSession).GetField("visible",Private).GetValue(owner);
                var type=typeof(LabTestSession).GetNestedType("Observation",BindingFlags.NonPublic);
                var method=typeof(LabTestSession).GetMethod("VisibleMarker",Private);
                foreach(var mount in new[]{Quaternion.identity,Quaternion.Euler(90,0,0)})foreach(float yaw in new[]{0f,90f,180f,270f})
                {
                    double now=Time.realtimeSinceStartupAsDouble;visible.Clear();
                    foreach(string id in new[]{"A","B","C","D","E"})
                    {
                        var sample=Activator.CreateInstance(type,true);type.GetField("pose").SetValue(sample,new Pose(new Vector3(0,0,1),mount*Quaternion.Euler(0,yaw,0)));
                        type.GetField("tracking").SetValue(sample,true);type.GetField("updated").SetValue(sample,now);visible[id]=sample;
                    }
                    Field(owner,"decodedMarker","A");Field(owner,"decodedMarkerCount",1);Field(owner,"decodedMarkerAt",now);
                    Field(owner,"decodedObservation",visible["A"]);
                    object[] args={"A",now,null};
                    Check((bool)method.Invoke(owner,args)&&ReferenceEquals(args[2],visible["A"]),"One A paper accepted with five similar reference hypotheses; mount/yaw="+mount.eulerAngles+"/"+yaw);
                    Field(owner,"decodedMarker","C");Field(owner,"decodedObservation",visible["C"]);args=new object[]{"C",now,null};
                    Check((bool)method.Invoke(owner,args)&&ReferenceEquals(args[2],visible["C"]),"QR C selects C pose, never A pose");
                    args=new object[]{"A",now,null};Check(!(bool)method.Invoke(owner,args),"Wrong QR/pose identity refused");
                    Field(owner,"decodedMarkerCount",2);args=new object[]{"C",now,null};Check(!(bool)method.Invoke(owner,args),"Two decoded QR identities require selection");
                    Field(owner,"decodedMarkerCount",1);Field(owner,"decodedMarkerAt",now-2);Check(!(bool)method.Invoke(owner,args),"Stale decoded QR refused");
                    Field(owner,"decodedMarkerAt",now);Field(owner,"decodedObservation",null);Check(!(bool)method.Invoke(owner,args),"Reading a QR without a bound image pose never invents location");
                }
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        static byte[] Blank(int width,int height){var pixels=new byte[width*height];for(int i=0;i<pixels.Length;i++)pixels[i]=255;return pixels;}
        static void Paint(byte[] destination,int width,int height,Color32[] source,int sourceWidth,int sourceHeight,int left,int top,int cardWidth,int cardHeight,int rotation,bool slant)
        {
            for(int y=0;y<cardHeight;y++)
            {
                float row=(y+.5f)/cardHeight;
                // Inverse homography for a trapezoid with top width .72 of its base.
                // Linear UV interpolation here would bend the QR grid, unlike a flat sheet.
                float v=slant?row/(.72f+.28f*row):row;float inset=slant?(1-row)*cardWidth*.14f:0;float span=cardWidth-2*inset;
                for(int x=0;x<cardWidth;x++)
                {
                    int dx=left+x,dy=top+y;if(dx<0||dx>=width||dy<0||dy>=height)continue;
                    float u=(x-inset)/span;if(u<0||u>=1)continue;float a=u,b=v;
                    for(int r=0;r<rotation;r++){float prior=a;a=b;b=1-prior;}
                    int sx=Mathf.Clamp((int)(a*sourceWidth),0,sourceWidth-1),sy=Mathf.Clamp((int)(b*sourceHeight),0,sourceHeight-1);
                    var c=source[(sourceHeight-1-sy)*sourceWidth+sx];destination[dy*width+dx]=(byte)((77*c.r+150*c.g+29*c.b)>>8);
                }
            }
        }
        static void ReviewQrImages()
        {
            foreach(string id in new[]{"A","B","C","D","E"})
            {
                var image=Resources.Load<Texture2D>("LabTest/Lab-"+id);Check(image!=null,"Reference present "+id);var source=image.GetPixels32();
                for(int rotation=0;rotation<4;rotation++)foreach(bool slant in new[]{false,true})
                {
                    var pixels=Blank(640,640);Paint(pixels,640,640,source,image.width,image.height,100,70,420,500,rotation,slant);
                    float inset=slant?420*.14f:0;
                    var baseQuad=new[]{100+inset,70f,520-inset,70,520,570,100,570};var quad=new float[8];
                    for(int k=0;k<4;k++){quad[k*2]=baseQuad[((k+rotation)%4)*2];quad[k*2+1]=baseQuad[((k+rotation)%4)*2+1];}
                    var result=DecodeBound(pixels,640,640,new[]{quad},new[]{id});Check(Id(result)==id&&Count(result)==1,"Bound QR "+id+" rotation="+rotation*90+" slant="+slant);
                    Check(result.GetType().GetField("observation").GetValue(result)!=null,"QR identity must be read inside its projected card ROI");
                }
            }
            var a=Resources.Load<Texture2D>("LabTest/Lab-A");var b=Resources.Load<Texture2D>("LabTest/Lab-B");var pair=Blank(800,600);
            Paint(pair,800,600,a.GetPixels32(),a.width,a.height,20,90,330,396,0,false);Paint(pair,800,600,b.GetPixels32(),b.width,b.height,450,90,330,396,0,false);
            var two=DecodeBound(pair,800,600,new[]{new[]{20f,90,350,90,350,486,20,486},new[]{450f,90,780,90,780,486,450,486}},new[]{"A","B"});Check(Count(two)==2&&Id(two)==null,"Two physical A/B QR codes are not accepted as one card");
            Paint(pair,800,600,a.GetPixels32(),a.width,a.height,450,90,330,396,0,false);
            var conflict=DecodeBound(pair,800,600,new[]{new[]{20f,90,350,90,350,486,20,486},new[]{450f,90,780,90,780,486,450,486}},new[]{"A","A"});
            Check(Count(conflict)==1&&Id(conflict)=="A"&&conflict.GetType().GetField("observation").GetValue(conflict)==null,"Conflicting poses of the same ID wait for a reliable pose without falsely reporting multiple QR identities");
            var empty=Decode(Blank(320,320),320,320);Check(Count(empty)==0&&Id(empty)==null,"Blank camera frame does not retain old identity");
        }
        static object DecodeBound(byte[] pixels,int width,int height,float[][] quads,string[] references)
        {
            var type=typeof(LabTestSession).GetNestedType("MarkerPoseJob",BindingFlags.NonPublic);
            var observationType=typeof(LabTestSession).GetNestedType("Observation",BindingFlags.NonPublic);
            var jobs=Array.CreateInstance(type,quads.Length);
            for(int i=0;i<quads.Length;i++)
            {
                var job=Activator.CreateInstance(type,true);var sample=Activator.CreateInstance(observationType,true);
                observationType.GetField("pose").SetValue(sample,new Pose(new Vector3(i,0,1),Quaternion.identity));
                type.GetField("quad").SetValue(job,quads[i]);type.GetField("sample").SetValue(job,sample);
                type.GetField("reference").SetValue(job,references[i]);jobs.SetValue(job,i);
            }
            return typeof(LabTestSession).GetMethod("DecodeBoundMarkerFrame",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{pixels,width,height,jobs});
        }
    }
}
