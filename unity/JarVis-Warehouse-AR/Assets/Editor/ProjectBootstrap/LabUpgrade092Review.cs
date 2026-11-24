using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class LabUpgrade092Review
    {
        const string Output="Logs/LabTest/Upgrade092";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
        static void Field(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name).SetValue(o,value);
        static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);checks++;}
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);checks=0;File.WriteAllText(Output+"/result.txt","RUNNING");
            LabNavigationJournalReview.Run();ReviewExportFailure();ReviewReplanning();
            LabLiveCueRuntimeReview.Run();
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" new export/replanning checks plus journal and real controller/projection/UI replay regressions. Synthetic poses only; physical map alignment/tracking/AI obstacles NOT VERIFIED.\n");
        }
        static void ReviewExportFailure()
        {
            var root=new GameObject("Missing journal export fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();
                string folder=Path.Combine(Path.GetFullPath(Output),"empty-"+Guid.NewGuid().ToString("N"));
                Field(owner,"journal",new LabNavigationJournal(folder));owner.ExportNavigationRun();
                Check(!owner.NavigationExportBusy,"An export failure unlocks retry");
                Check(!owner.NavigationRecordingFailed,"A missing export cannot report a live recording write failure");
                Check(owner.NavigationExportStatus.Contains("Chưa xuất"),"Missing data gets an actionable export message");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        static void ReviewReplanning()
        {
            var root=new GameObject("Failed replan fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();
                var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
                var map=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
                Prop(owner,"Floor",floor);Prop(owner,"Map",map);Field(owner,"mapFrame",root.transform);
                owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=new GameObject("Fixture camera").AddComponent<Camera>();owner.CameraOwner.arCamera.transform.SetParent(root.transform);
                owner.EditorReviewNavigation("ĐI THẲNG",true,false,false);Field(owner,"reviewRuntime",true);Field(owner,"journeyStarted",true);
                var planner=(LabRoutePlanner)Field(owner,"routePlanner");var graph=(LabCorridorGraph)Field(owner,"corridorGraph");
                Check(planner.GraphStatus=="VALID","Fixture uses the current authored multi-route graph");
                var original=(LabRouteChoice)Field(owner,"activeRoute");
                var stale=new LabRouteChoice{id="STALE_FAR_SECTION",points=original.points.Skip(original.points.Length-2).ToArray()};
                planner.SetBlocked(graph.edges.Select(e=>e.id));Field(owner,"activeRoute",stale);Field(owner,"deviationSince",0d);Field(owner,"nextPlan",0d);
                Call(owner,"UpdateFloorRoute",3d);
                Check(owner.Decision!=null&&!owner.Decision.usable&&owner.Route.Count==0,"Failed replan must not reuse stale progress or emit movement");
                Check(owner.Exercise.Target=="C"&&owner.ManeuverTitle=="CHƯA CÓ TUYẾN","Blocked route keeps destination and names the actual problem");
                planner.SetBlocked(Array.Empty<string>());
                Call(owner,"UpdateFloorRoute",5d);
                Check(owner.Decision.usable&&owner.Route.Count>=2,"Reopening the aisle yields a new usable route");
                original=(LabRouteChoice)Field(owner,"activeRoute");stale=new LabRouteChoice{id="STALE_AGAIN",points=original.points.Skip(original.points.Length-2).ToArray()};
                Field(owner,"activeRoute",stale);Field(owner,"deviationSince",0d);Field(owner,"nextPlan",0d);
                Call(owner,"UpdateFloorRoute",8d);
                Check(owner.Decision.usable&&owner.Decision.reason==null&&owner.ManeuverTitle!="ĐANG CẬP NHẬT TUYẾN","Successful replan recomputes deviation and exits the old hold in the same decision");
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Field(owner,"ui",ui);Call(ui,"Start");Field(ui,"showingNavigationLog",true);
                owner.RefreshPresentation();Check(owner.CameraGuidanceOccluded&&owner.Presentation.reason=="PANEL_OPEN"&&owner.Presentation.visibleTokens==0,"Export panel suppresses movement overlays without clearing the target");
                Check(owner.Exercise.Target=="C"&&owner.Route.Count>=2,"Opening export preserves the route and destination");
                Field(ui,"showingNavigationLog",false);owner.RefreshPresentation();Check(!owner.CameraGuidanceOccluded,"Returning to guidance releases the panel hold");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
