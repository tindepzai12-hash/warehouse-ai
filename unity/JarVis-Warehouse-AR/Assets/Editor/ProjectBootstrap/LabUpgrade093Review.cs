using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class LabUpgrade093Review
    {
        const string Output="Logs/LabTest/Upgrade093";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static object Field(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
        static void Field(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static void Prop(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
        static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        static T Load<T>(string name)=>JsonUtility.FromJson<T>(Resources.Load<TextAsset>("LabTest/"+name).text);
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;
            var floor=Load<LabFloorPlan>("LabFloorPlanNew");var graph=Load<LabCorridorGraph>("LabCorridorsNew");
            var trial=Load<LabTrialPreset>("LabTrialNavigation");var knowledge=LabMapKnowledge.Load();
            Check(knowledge.Matches(floor,graph),"Curated knowledge must match actual floor and graph hashes");
            var changed=JsonUtility.FromJson<LabMapKnowledge>(JsonUtility.ToJson(knowledge));changed.floorHash="DIFFERENT";
            Check(!changed.Matches(floor,graph),"Do not fuse history into a different map");
            Check(knowledge.markers.All(m=>!m.orientationVerified)&&!knowledge.globalVisualLocalizationReady,"Unverified marker angles and QR-free localization cannot be promoted silently");
            var router=new LabFloorRouter(floor);var planner=new LabRoutePlanner(floor,graph);
            foreach(var a in trial.map.markers)foreach(var b in trial.map.markers)
            {
                if(a.id==b.id)continue;
                Check(planner.Find(a.access,b.access,out var routes)==null&&routes.Count>0,a.id+" to "+b.id+" must route without A as origin");
                foreach(var route in routes)
                    for(int i=1;i<route.points.Length;i++)Check(router.Segment(route.points[i-1],route.points[i]),"Every alternative stays inside the authored walkable floor");
            }
            Check(planner.Find(trial.map.markers[1].access,trial.map.markers[2].access,out var bc)==null&&bc.Count>1,"B to C has multiple alternatives");
            var edge=graph.edges.First(e=>e.points.Length>=2&&LabPaths.Length(e.points)>.8f);
            var obstacle=LabPaths.Sample(edge.points.ToList(),LabPaths.Length(edge.points)*.5f);
            var memory=new LabObstacleMemory(knowledge.depth,router);var points=new List<LabPoint>();
            for(int i=0;i<8;i++)points.Add(new LabPoint(obstacle.x,0,obstacle.z));
            points.Add(new LabPoint(obstacle.x,.7f,obstacle.z));points.Add(new LabPoint(obstacle.x+.005f,.72f,obstacle.z));
            memory.Observe(1,1,1,points);memory.Observe(1,1,1.2,points);
            Check(memory.BlockedEdges(graph,1.2).Length==0,"Duplicate frames/pixels cannot confirm an obstacle");
            for(int i=1;i<=6;i++)memory.Observe(1,1+i*.2,1+i*.2,points);
            Check(memory.ConfirmedCount>0&&memory.BlockedEdges(graph,2.2).Contains(edge.id),"Fresh multi-frame depth closes the observed edge");
            Check(memory.Intersects(edge.points,2.2),"An attachment/route segment cannot cross a confirmed local obstacle");
            Check(memory.BlockedEdges(graph,6).Length==0,"Camera obstacle evidence expires");
            memory.Observe(2,4,6.1,points);Check(memory.ConfirmedCount==0&&memory.GroundFrames==1,"A new reference epoch drops all old votes and ground confirmation");
            var low=new List<LabPoint>();for(int i=0;i<8;i++)low.Add(new LabPoint(obstacle.x,0,obstacle.z));low.Add(new LabPoint(obstacle.x,2.5f,obstacle.z));
            for(int i=0;i<7;i++)memory.Observe(3,5+i*.2,7+i*.2,low);
            Check(memory.BlockedEdges(graph,8.2).Length==0,"Floor and ceiling samples cannot block an aisle");
            var matrix=Matrix4x4.zero;matrix.m10=1;matrix.m01=-1;matrix.m21=1;
            Check(LabCameraAi.DepthUv(matrix,.2f,.7f,out var uv)&&Vector2.Distance(uv,new Vector2(.7f,.8f))<.001,"Portrait display transform uses ARCore row-vector convention");
            Check(!LabCameraAi.DepthUv(matrix,2,.5f,out _),"Depth sample outside native image is rejected");
            ReviewTrialHeading(floor,trial.map.survey);
            LabNavigationJournalReview.Run();LabLiveCueRuntimeReview.Run();
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" curated-data / all-pairs / obstacle / trial-heading checks, plus journal and guidance runtime regressions.\nSynthetic evidence only; physical depth projection, room alignment and B-C walk still require Samsung lab test.\n");
        }
        static void ReviewTrialHeading(LabFloorPlan floor,LabSurvey map)
        {
            var root=new GameObject("Trial heading fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();Prop(owner,"Floor",floor);Prop(owner,"Map",map);
                var fixtureAnchor=new GameObject("Fixture QR anchor").AddComponent<UnityEngine.XR.ARFoundation.ARAnchor>();fixtureAnchor.transform.SetParent(root.transform);Field(owner,"anchor",fixtureAnchor);
                var frame=new GameObject("Independent fixture map").transform;frame.SetParent(fixtureAnchor.transform);Field(owner,"mapFrame",frame);
                owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=new GameObject("Fixture camera").AddComponent<Camera>();owner.CameraOwner.arCamera.transform.SetParent(root.transform);
                owner.EditorReviewNavigation("ĐI THẲNG",true,false,false);
                var origin=map.markers.First(m=>m.id=="B").centre;owner.CameraOwner.arCamera.transform.position=new Vector3(origin.x+.6f,origin.y+.9f,origin.z+.3f);
                Call(owner,"RequestTrialHeading","B");
                Check(owner.AwaitingDirection&&!owner.NavigationPoseValid&&owner.Exercise.Target=="C","Scan B holds movement for heading without resetting C destination");
                var b=map.markers.First(m=>m.id=="B").centre;var look=map.markers.First(m=>m.id==owner.DirectionReferenceId).centre;
                var expectedCamera=owner.CameraOwner.arCamera.transform.position;
                owner.CameraOwner.arCamera.transform.rotation=Quaternion.LookRotation(new Vector3(look.x-expectedCamera.x,0,look.z-expectedCamera.z));
                Call(owner,"UpdateTrialHeading",10d);Call(owner,"UpdateTrialHeading",10.7d);owner.ConfirmDirection();
                Check(!owner.AwaitingDirection&&owner.TrialAlignmentStatus.StartsWith("MANUAL_APPROXIMATE_B"),"Heading confirmation works from B");
                var local=owner.WorldToMapPoint(owner.CameraOwner.arCamera.transform.position);
                Check(Vector3.Distance(owner.WorldToMapPoint(new Vector3(b.x,b.y,b.z)),new Vector3(b.x,b.y,b.z))<.01f,"Manual heading keeps the observed B centre");
                Check(Vector3.Distance(local,expectedCamera)<.01f,"Manual heading must not force the camera onto an authored access point");
                Check(Math.Abs(local.y-expectedCamera.y)<.01f,"Camera floor-relative height preserved after confirmation");
                Check(owner.Exercise.Target=="C","Manual confirmation preserves C destination");
                var manual=(HashSet<string>)Field(owner,"manualBlocks");var graph=(LabCorridorGraph)Field(owner,"corridorGraph");manual.Add(graph.edges[0].id);Call(owner,"ApplyAisleBlocks");Call(owner,"ResetCameraEvidence");
                Check(((LabRoutePlanner)Field(owner,"routePlanner")).Blocked.Contains(graph.edges[0].id),"Resetting camera evidence does not clear manual blocks");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
