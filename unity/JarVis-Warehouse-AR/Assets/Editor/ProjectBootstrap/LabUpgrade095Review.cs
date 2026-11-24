using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class LabUpgrade095Review
    {
        public const string Output="Logs/LabTest/Upgrade095";static int checks;
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
        static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static Vector3 V(LabPoint p)=>new Vector3(p.x,p.y,p.z);
        static LabPoint P(Vector3 p)=>new LabPoint(p.x,p.y,p.z);
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;
            ReviewHeading();ReviewProgress();ReviewDepth();ReviewFloor();ReviewExports();ReviewRouteRevision();
            LabUpgrade094Review.Run();
            LabUpgrade095UiReview.Run();
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" focused 0.9.5 checks + 0.9.4 regression + 0.9.5 UI at two sizes. Synthetic only. Physical room/QR/depth/tracking acceptance NOT verified.");
            Debug.Log("JARVIS_095_PASS "+checks);
        }
        static void ReviewHeading()
        {
            var origin=new LabPoint(7,.75f,-2);var target=new LabPoint(-8,.75f,1);
            foreach(float yaw in new[]{-160f,-70f,0f,35f,170f})foreach(var offset in new[]{new Vector3(.3f,.9f,.4f),new Vector3(-.7f,.65f,.2f),new Vector3(.1f,1.1f,-.5f)})
            {
                var q=Quaternion.Euler(0,yaw,0);var t=new Vector3(2,.4f,-4);
                var observed=q*V(origin)+t;var camera=q*(V(origin)+offset)+t;
                var f=Vector3.ProjectOnPlane(q*V(target)+t-camera,Vector3.up).normalized;
                Check(LabMarkerHeadingAlignment.TrySolve(origin,target,P(observed),P(camera),f.x,f.z,out var actual,out var translation,out _),"Offset camera can align from the observed marker");
                Check(Mathf.Abs(Mathf.DeltaAngle(actual,yaw))<.002f,"Offset camera must not bias yaw towards an access assumption");
                Check(Vector3.Distance(Quaternion.Euler(0,actual,0)*V(origin)+V(translation),observed)<.0001f,"QR centre must stay at its measured world location");
                Check(Vector3.Distance(V(translation),t)<.001f,"Translation agrees with independent synthetic transform");
            }
            Check(!LabMarkerHeadingAlignment.TrySolve(origin,target,origin,new LabPoint(float.NaN,1,0),0,1,out _,out _,out _),"NaN camera rejected");
            Check(!LabMarkerHeadingAlignment.TrySolve(origin,origin,origin,origin,0,1,out _,out _,out _),"Same marker cannot establish heading");
            Check(!LabMarkerHeadingAlignment.TrySolve(origin,target,origin,origin,0,0,out _,out _,out _),"Vertical/zero horizontal bearing rejected");
            Check(!LabMarkerHeadingAlignment.TrySolve(origin,target,origin,new LabPoint(15,1,1),0,1,out _,out _,out _),"Stale marker after walking away rejected");
        }
        static void ReviewProgress()
        {
            var path=new List<LabPoint>{new LabPoint(1,0,1),new LabPoint(1,0,5),new LabPoint(1.45f,0,5),new LabPoint(1.45f,0,1)};
            var progress=new LabRouteProgress();progress.Reset(path);progress.Update(path,new LabPoint(1,0,2));
            for(int i=0;i<15;i++)progress.Update(path,new LabPoint(1.44f,0,2));
            Check(progress.Segment==0&&progress.Along<1.3f,"Nearby parallel leg cannot steal progress across the U");
            for(float z=2.2f;z<=5;z+=.2f)progress.Update(path,new LabPoint(1,0,z));
            progress.Update(path,new LabPoint(1,0,5));progress.Update(path,new LabPoint(1.2f,0,5));progress.Update(path,new LabPoint(1.45f,0,5));
            for(int i=0;i<9;i++)progress.Update(path,new LabPoint(1.45f,0,4.8f-i*.2f));
            Check(progress.Segment==2&&progress.Along>5.8f,"Real motion around the corner enters the outgoing leg");
            float along=progress.Along;for(int i=0;i<10;i++)progress.Update(path,new LabPoint(1.45f,0,3.2f));
            Check(Math.Abs(progress.Along-along)<.01f,"Camera rotation represented by an unchanged position adds no distance");
            for(float z=3.4f;z<=4.4f;z+=.2f)progress.Update(path,new LabPoint(1.45f,0,z));
            Check(progress.Along<along-.8f,"Walking back reduces along instead of forcing forward progress");
        }
        static void ReviewDepth()
        {
            var position=new Vector3(2,1.4f,3);var rotation=Quaternion.Euler(12,40,0);
            var frame=new LabDepthCameraFrame{timestamp=100,received=1,epoch=7,position=position,rotation=rotation,
                projection=Matrix4x4.Perspective(60,.5f,.1f,30),cameraToWorld=Matrix4x4.TRS(position,rotation,Vector3.one)*Matrix4x4.Scale(new Vector3(1,1,-1)),display=Matrix4x4.identity};
            Check(frame.Project(.5f,.5f,2,out var world),"Perspective depth projects");
            Check(Vector3.Distance(world,position+rotation*new Vector3(0,0,2))<.001f,"Depth is optical Z in the recorded camera pose");
            Check(frame.Project(.8f,.7f,2,out var edge)&&Vector3.Dot(edge-position,rotation*Vector3.forward)>1.999f,"Off-axis depth retains optical Z");
            Check(!frame.Project(.5f,.5f,float.NaN,out _),"NaN depth rejected");
            var history=new LabDepthFrames();history.Record(frame);
            Check(history.Select(100.04,1.1,7,position,rotation,out var selected,out _),"Close timestamp and stable motion accepted");
            Check(selected.Project(.5f,.5f,2,out var selectedWorld)&&Vector3.Distance(selectedWorld,world)<.001f,"Selected projection remains tied to the captured frame");
            Check(!history.Select(100.1,1.1,7,position,rotation,out _,out _),"Depth with >60 ms gap rejected");
            Check(!history.Select(100,1.3,7,position,rotation,out _,out _),"Old camera frame rejected");
            Check(!history.Select(100,1.1,8,position,rotation,out _,out _),"Different reference epoch rejected");
            Check(!history.Select(100,1.1,7,position,Quaternion.Euler(12,50,0),out _,out _),"Camera turning between frame and projection rejected");
            history.Clear();Check(!history.Select(100,1.1,7,position,rotation,out _,out _),"Pause clears camera frame evidence");
        }
        static LabFloorPlan Floor()=>new LabFloorPlan{width=80,height=40,resolution=.1f,requiredClearance=.25f,cells=Enumerable.Repeat(1,3200).ToArray(),clearance=Enumerable.Repeat(2f,3200).ToArray()};
        static void ReviewFloor()
        {
            var evidence=new LabFloorEvidence(new LabFloorRouter(Floor()));var line=new List<LabPoint>{new LabPoint(1,0,2),new LabPoint(7,0,2)};
            var centre=new List<LabPoint>();for(float x=1.5f;x<3.2f;x+=.1f)centre.Add(new LabPoint(x,0,2));
            evidence.Observe(1,1,1,centre);evidence.Observe(1,1.2,1.2,centre);
            Check(evidence.FrontCoverage(line,1.2)<.7f,"A single line of depth cannot certify body-width clearance");
            var wide=new List<LabPoint>();for(float x=1.5f;x<3.2f;x+=.1f)for(float z=1.6f;z<2.4f;z+=.1f)wide.Add(new LabPoint(x,0,z));
            evidence.Observe(1,1.4,1.4,wide);evidence.Observe(1,1.6,1.6,wide);
            Check(evidence.FrontCoverage(line,1.6)>=.7f,"Repeated observations across the corridor permit progress");
            Check(evidence.FrontCoverage(line,5)==0,"Depth corridor evidence expires");
        }
        static void ReviewExports()
        {
            string root=Path.GetFullPath(Output+"/log-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            string file=Path.Combine(root,"interrupted.jsonl");File.WriteAllText(file,"{\"value\":1}\n{\"value\":");long original=new FileInfo(file).Length;
            var snapshot=new LabNavigationJournal.Snapshot{id="partial-test"};snapshot.AddFile(file);
            File.AppendAllText(file,"2}\n{\"value\":3}\n");
            string zip=LabNavigationJournal.WriteZip(snapshot,root);
            using(var archive=ZipFile.OpenRead(zip))
            {
                using(var reader=new StreamReader(archive.GetEntry("interrupted.jsonl").Open()))Check(reader.ReadToEnd()=="{\"value\":1}\n","Snapshot exports complete records only and excludes later appends");
                using(var reader=new StreamReader(archive.GetEntry("export-integrity.json").Open()))
                {var integrity=JsonUtility.FromJson<LabNavigationJournal.ExportIntegrity>(reader.ReadToEnd());Check(integrity.trimmed.Count==1&&integrity.trimmed[0].droppedTailBytes==original-12,"Incomplete crash tail is disclosed");}
            }
            Check(File.ReadAllText(file).Contains("\"value\":2"),"Capture never rewrites the source evidence");
            var old=new LabTestSuite(Path.Combine(root,"suites"),"0.9.4","TEST");string oldFolder=old.Folder;old.Dispose();
            var current=new LabTestSuite(Path.Combine(root,"suites"),"0.9.5","TEST");
            Check(current.Folder!=oldFolder&&current.Manifest.version=="0.9.5"&&File.Exists(Path.Combine(oldFolder,"suite-manifest.json")),"New app version has a new manifest and retains old suite");current.Dispose();
        }
        static void ReviewRouteRevision()
        {
            var root=new GameObject("Route stability fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();
                var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
                var trial=JsonUtility.FromJson<LabTrialPreset>(Resources.Load<TextAsset>("LabTest/LabTrialNavigation").text);
                Prop(owner,"Floor",floor);Prop(owner,"Map",trial.map.survey);Prop(owner,"Exercise",new LabExercise("C"));
                owner.GetType().GetField("floorRouter",Flags).SetValue(owner,new LabFloorRouter(floor));
                Call(owner,"InitializePlanner");var b=owner.Map.markers.First(m=>m.id=="B").access;var c=owner.Map.markers.First(m=>m.id=="C").access;
                Call(owner,"PlanRoutes",b,c,1d);var selected=Field(owner,"activeRoute");int revision=(int)Field(owner,"routeRevision");
                Check(selected!=null,"Actual authored B-C map has a route");
                Call(owner,"PlanRoutes",b,c,2d);
                Check(ReferenceEquals(selected,Field(owner,"activeRoute"))&&(int)Field(owner,"routeRevision")==revision,"Replanning a safe unchanged route preserves revision and animation context");
                var route=(LabRouteChoice)selected;var manual=(HashSet<string>)Field(owner,"manualBlocks");manual.Add(route.edges[0]);Call(owner,"ApplyAisleBlocks");Call(owner,"PlanRoutes",b,c,3d);
                Check(!ReferenceEquals(selected,Field(owner,"activeRoute")),"A blocked active edge cannot be preserved merely for visual stability");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
