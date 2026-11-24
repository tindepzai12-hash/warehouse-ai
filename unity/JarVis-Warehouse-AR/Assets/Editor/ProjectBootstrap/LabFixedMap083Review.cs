using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class LabFixedMap083Review
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static string output="Logs/LabTest/Fixed083";
        static void Check(bool value,string label){if(!value)throw new InvalidOperationException("FIXED083 "+label);checks++;Debug.Log("FIXED083 PASS "+label);}
        static void Field(object owner,string name,object value)=>owner.GetType().GetField(name,Flags).SetValue(owner,value);
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        static void Set(object owner,string name,object value)=>owner.GetType().GetProperty(name).SetValue(owner,value);
        static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
        static LabPoint P(float x,float z)=>new LabPoint(x,0,z);
        public static void RunAndBuild(){Run();LabFixedMapReview.OutputPath="Logs/LabTest/Ui083";LabFixedMapReview.Run();LabTestBuild.Build();}
        public static void Run()
        {
            checks=0;Directory.CreateDirectory(output);ReviewRecovery();ReviewRealDraft();
            File.WriteAllText(output+"/result.txt","PASS "+checks+" focused survey retry/recovery/real-draft/approach/publication checks. Simulated tracking/poses and native runtime calls; Samsung camera and physical walking NOT VERIFIED.\n");
        }
        static void ReviewRecovery()
        {
            var gate=new LabSurveyRecovery();
            Check(gate.Observe(0,1,P(0,0),true,"a")==null&&!gate.Holding,"Healthy baseline allows capture");
            Check(gate.Observe(.1,2,P(0,0),false,"a")==null&&gate.Holding,"Transient loss freezes capture, keeps reference");
            Check(gate.Observe(.3,3,P(.1f,0),true,"a")==null&&gate.Holding,"Returning tracking must stabilize");
            for(int i=1;i<=7;i++)Check(gate.Observe(.3+i*.1,3+i,P(.1f+i*.01f,0),true,"a")==null,"Fresh stable recovery frame "+i);
            Check(!gate.Holding,"Stable recovery permits capture");
            gate.Reset();gate.Observe(0,1,P(0,0),true,"a");gate.Observe(.1,2,P(0,0),false,"a");
            Check(gate.Observe(2.11,3,P(0,0),false,"a")=="SURVEY_TRACKING_LOST","Long outage requires known reference");
            gate.Reset();gate.Observe(0,1,P(0,0),true,"a");gate.Observe(.1,2,P(0,0),false,"a");
            Check(gate.Observe(.3,3,P(13.8f,0),true,"a")=="SURVEY_POSE_JUMP","No invented offset for 13.8m return jump");
            gate.Reset();gate.Observe(0,1,P(0,0),true,"a");
            Check(gate.Observe(.23,2,P(13.8f,0),true,"a")=="SURVEY_POSE_JUMP","Healthy flag cannot hide original real trace jump");
            gate.Reset();gate.Observe(0,1,P(0,0),true,"a");
            Check(gate.Observe(.1,2,P(0,0),true,"b")=="SURVEY_ANCHOR_CHANGED","Changed reference cannot splice surveys");
            gate.Reset();gate.Observe(0,1,P(0,0),true,"a");gate.Observe(.1,2,P(0,0),false,"a");gate.Observe(.3,3,P(0,0),true,"a");
            for(int i=1;i<=5;i++)gate.Observe(.3+i*.1,3,P(0,0),true,"a");
            Check(gate.Holding,"Repeated frame never completes recovery");
            gate.Reset();gate.Observe(0,1,P(0,0),true,"a");
            Check(gate.Observe(-.1,2,P(0,0),true,"a")=="SURVEY_CLOCK_CHANGED","Backward time requires reference");
        }
        static void ReviewRealDraft()
        {
            string input=Environment.GetEnvironmentVariable("JARVIS_LAB_AUDIT_INPUT");
            if(string.IsNullOrEmpty(input))throw new InvalidOperationException("Missing supplied draft folder");
            string raw=File.ReadAllText(Path.Combine(input,"fixed-map-draft.json"));
            var draft=JsonUtility.FromJson<LabFixedMap>(raw);
            var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
            var layout=JsonUtility.FromJson<LabApproachLayout>(Resources.Load<TextAsset>("LabTest/LabConfirmedDestinations").text);
            Check(draft.markers.Count==2&&draft.markers.Any(m=>m.id=="A")&&draft.markers.Any(m=>m.id=="B"),"Use real two-marker draft; no fabricated CDE");
            Check(draft.Validate(floor,false)==null&&LabSurveyIntegrity.Check(draft)==null,"Real AB kept as valid raw draft");
            Check(Math.Abs(LabPaths.Distance(draft.markers[0].centre,draft.markers[1].centre)-14.3163f)<.001f,"Real 14.316m AB separation retained");
            var fit=LabRegistration.Solve(draft.survey,floor);Check(fit.valid&&fit.maxError<.225f&&Math.Abs(fit.scaleRatio-1)<.03f,"AB rigid registration meets metric tolerances");
            var router=new LabFloorRouter(floor);
            foreach(var marker in draft.markers)
            {
                var centre=fit.Transform(marker.centre);var camera=fit.Transform(marker.access);
                Check(!router.Walkable(camera),"Reproduce camera-over-table access "+marker.id);
                Check(LabFixedApproach.TryChoose(floor,centre,camera,layout,marker.id,out var access,out var source)&&router.Walkable(access)&&source=="CONFIRMED_LAYOUT_APPROACH","Resolve explicit walkable approach "+marker.id);
                Check(LabPaths.Distance(centre,access)<1.6f,"Approach remains near same physical destination "+marker.id);
            }
            var stale=JsonUtility.FromJson<LabApproachLayout>(JsonUtility.ToJson(layout));stale.geometrySha256="stale";
            Check(!LabFixedApproach.TryChoose(floor,fit.Transform(draft.markers[0].centre),fit.Transform(draft.markers[0].access),stale,"A",out _,out _),"Do not borrow approach from another geometry");
            Check(!LabFixedApproach.TryChoose(floor,new LabPoint(5,0,4),fit.Transform(draft.markers[0].access),layout,"A",out _,out _),"No remote nearest-aisle teleport");
            var root=new GameObject("Fixed083 actual runtime fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();Set(owner,"Floor",floor);Set(owner,"Map",draft.survey);Field(owner,"fixedDraft",draft);Field(owner,"fixedSurvey",true);Field(owner,"ready",true);Field(owner,"lastSurveyMarker","B");
                var mapRoot=new GameObject("Synthetic map frame");mapRoot.transform.SetParent(root.transform);Field(owner,"mapFrame",mapRoot.transform);
                var cameraRoot=new GameObject("Synthetic camera");cameraRoot.transform.SetParent(root.transform);var camera=cameraRoot.AddComponent<Camera>();
                owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=camera;
                string repository=Path.GetFullPath(output+"/repository-"+Guid.NewGuid().ToString("N"));
                var repo=new LabFixedRepository(repository,floor,v=>JsonUtility.ToJson(v,true),v=>JsonUtility.FromJson<LabFixedMap>(v),v=>JsonUtility.ToJson(v,true),v=>JsonUtility.FromJson<LabFixedPointer>(v));
                Field(owner,"fixedRepository",repo);
                Call(owner,"AcceptFixedObservationCore","C",new Pose(new Vector3(0,.9f,0),Quaternion.identity));
                Check(draft.markers.Count==2&&Field<string>(owner,"lastSurveyMarker")=="B","Reject C without poisoning last successful marker");
                Check(owner.ScanProgress==0&&Field<float>(owner,"fixedLatchProgress")==0,"Rejected latch never displays 100 percent success");
                Check(Field<string>(owner,"fixedRejectId")=="C"&&Field<string>(owner,"fixedRejectReason").StartsWith("COINCIDENT"),"Trace retains rejection ID and specific reason");
                var pin=draft.survey.placement.pins.First(p=>p.id=="C").position;
                var rotation=Quaternion.Euler(0,fit.yaw,0);var inverse=Quaternion.Inverse(rotation)*(new Vector3(pin.x,.8f,pin.z)-new Vector3(fit.tx,0,fit.tz));camera.transform.position=inverse+new Vector3(.3f,.5f,.3f);
                var samples=Field<LabScanCalibration>(owner,"calibration");
                for(int i=0;i<8;i++)samples.ObserveFrame(i+1,i*.15,true,LabPrintedCard.ImageWidthMetres,new LabPoint(inverse.x,inverse.y,inverse.z),new LabPoint(camera.transform.position.x,camera.transform.position.y,camera.transform.position.z));
                Check(samples.Ready&&samples.Samples==8,"Retry fixture supplies eight actual stable calibration samples");
                Call(owner,"AcceptFixedObservationCore","C",new Pose(inverse,Quaternion.identity));
                Check(draft.markers.Count==3&&Field<string>(owner,"lastSurveyMarker")=="C","Same rejected ID can successfully save on retry");
                Check(owner.ScanProgress==1&&Field<string>(owner,"fixedScanStage")=="SAVED","Success follows actual repository write");
                Check(repo.LoadDraft().markers.Count==3,"Retry persists marker, not just UI feedback");
                Check(Field<string>(owner,"fixedRejectReason")=="","Saved retry clears stale rejection reason");
                // Publication checks use a separate clone of the raw supplied AB; loop success is
                // explicitly synthetic. They must never mutate the supplied evidence or claim field acceptance.
                draft=JsonUtility.FromJson<LabFixedMap>(raw);Field(owner,"fixedDraft",draft);Set(owner,"Map",draft.survey);
                Check(!owner.PublishFixedPlacement(draft.survey.placement)&&repo.LoadActive(out _)==null,"No active navigation until independent loop check");
                draft.loopChecked=true;draft.survey.loopChecked=true;draft.survey.interrupted=false;
                Field(owner,"mapFrame",null);
                Check(owner.PublishFixedPlacement(draft.survey.placement),"Synthetic valid AB loop publishes with explicit approaches");
                var active=repo.LoadActive(out string reason);
                Check(active!=null&&reason=="ACTIVE"&&active.markers.Count==2,"Published snapshot reloads after repository restart");
                Check(!active.physicalAccepted,"Local checks never mark physical acceptance");
                foreach(var marker in active.markers)
                    Check(router.Walkable(marker.access)&&marker.observedAccess!=null&&marker.accessSource=="CONFIRMED_LAYOUT_APPROACH","Preserve camera observation separately from route destination "+marker.id);
                Check(owner.CanChooseFixed("A")&&owner.CanChooseFixed("B")&&!owner.CanChooseFixed("C"),"Only actually recorded published destinations unlock");
                Check(JsonUtility.FromJson<LabFixedMap>(raw).markers[0].access.x==draft.markers[0].access.x,"Raw camera draft preserved during publication");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
