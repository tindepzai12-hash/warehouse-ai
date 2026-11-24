using System;
using System.IO;
using System.Linq;
using JarVis.Warehouse.Lab;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ProjectBootstrap
{
    public static class LabFixedMap082Review
    {
        static int checks;
        static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("MAP_082 "+label);checks++;Debug.Log("MAP_082 PASS "+label);}
        [Serializable] sealed class Trace { public string utc;public double seconds;public long cameraSampleId;public bool located;public LabPoint position; }
        [Serializable] sealed class Destination { public string id;public LabPoint position,access; }
        [Serializable] sealed class Layout { public Destination[] destinations; }
        public static void RunAndBuild()
        {
            LabTestBuild.Prepare();LabScanHotfixReview.Run();Run();
            LabFixedMapReview.OutputPath="Logs/LabTest/Fixed082";LabFixedMapReview.Run();LabTestBuild.Build();
        }
        public static void Run()
        {
            checks=0;Directory.CreateDirectory("Logs/LabTest/Map082");
            string input=Environment.GetEnvironmentVariable("JARVIS_LAB_AUDIT_INPUT");
            if(string.IsNullOrEmpty(input))throw new InvalidOperationException("Missing user input audit directory");
            var draft=JsonUtility.FromJson<LabFixedMap>(File.ReadAllText(Path.Combine(input,"fixed-map-draft.json")));
            var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
            var controls=JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text);
            var layoutText=Resources.Load<TextAsset>("LabTest/LabConfirmedDestinations").text;
            var layout=JsonUtility.FromJson<Layout>(layoutText);var placement=JsonUtility.FromJson<LabMapPlacement>(layoutText);
            Check(layout.destinations.Length==5&&placement.pins.Count==5,"All five owner annotated positions available on 2D floor");
            foreach(var old in controls.markers)
            {var point=placement.pins.First(p=>p.id==old.id);Check(LabPaths.Distance(point.position,old.centre)<.0001&&!point.approximate,"Preserve old confirmed "+old.id);}
            Check(placement.pins.Where(p=>p.id=="D"||p.id=="E").All(p=>p.approximate),"D/E annotation cannot masquerade as precise AR registration");
            var graph=JsonUtility.FromJson<LabCorridorGraph>(Resources.Load<TextAsset>("LabTest/LabCorridorsNew").text);var planner=new LabRoutePlanner(floor,graph);var router=new LabFloorRouter(floor);
            Check(planner.GraphStatus=="VALID","Existing multi-aisle graph matches unchanged obstacle floor");
            using(var routeLog=new StreamWriter("Logs/LabTest/Map082/route-checks.txt"))
            foreach(var from in layout.destinations)
            {
                Check(router.Walkable(from.access),"Accessible standing point "+from.id);
                foreach(var to in layout.destinations.Where(p=>p.id!=from.id))
                {
                    string routeError=planner.Find(from.access,to.access,out var choices);
                    Check(routeError==null&&choices.Count>0,"Route "+from.id+" to "+to.id+" from that point, not forced through A");
                    foreach(var choice in choices)for(int i=1;i<choice.points.Length;i++)Check(router.Segment(choice.points[i-1],choice.points[i]),"Route segment avoids furniture "+from.id+to.id);
                    routeLog.WriteLine(from.id+" -> "+to.id+": "+choices.Count+" choices, first="+choices[0].length.ToString("F2")+"m; geometry only, physical walk not verified");
                }
            }
            Check(draft.mapId==floor.id&&draft.floorHash==floor.geometrySha256&&draft.floorRevision==floor.revision,"New ZIP uses the exact existing floor revision/hash");
            Check(draft.markers.Count==5&&draft.Validate(floor,false)==null,"Preserve all five raw draft markers without claiming activation");
            Check(LabSurveyIntegrity.Check(draft)=="COINCIDENT_A_B","Real user draft A/B overlap found");
            Check(draft.Validate(floor,true)=="COINCIDENT_A_B","Invalid real draft cannot become active");
            Check(draft.survey.placement.pins.Count==0&&!draft.loopChecked,"Real draft has no registration or independent loop check");
            draft.survey.placement=controls.placement;
            Check(!LabRegistration.Solve(draft.survey,floor).valid,"Old confirmed A/B/C cannot be rigidly fitted to contradictory new observations");
            var continuity=new LabSurveyContinuity();Trace previous=null;string first=null;int faults=0;
            foreach(string line in File.ReadLines(Path.Combine(input,"navigation-trace.jsonl")))
            {
                var row=JsonUtility.FromJson<Trace>(line.Replace(":Infinity",":0").Replace(":-Infinity",":0"));
                if(!row.located||previous!=null&&row.seconds<=previous.seconds){continuity.Reset();previous=row;continue;}
                string error=continuity.Observe(row.seconds,row.cameraSampleId,row.position,true);
                if(error!=null){faults++;if(first==null)first=row.utc;continuity.Reset();}
                previous=row;
            }
            Check(faults>=1&&first.StartsWith("2026-11-02T07:28:30.768",StringComparison.Ordinal),"Replay stops survey at the 13.8m jump, before B is saved");
            foreach(string id in new[]{"A","B","C","D","E"})
            {
                var library=Resources.Load<XRReferenceImageLibrary>("LabTest/LabFixedMarker-"+id);
                Check(library!=null&&library.count==1&&library[0].name==id,"Only selected QR template in pose library "+id);
                Check(Math.Abs(library[0].size.x-LabPrintedCard.ImageWidthMetres)<.00001f,"Same accepted physical card scale "+id);
                // Each ID is equally capable of recovering the same map frame.
                foreach(var mount in new[]{Quaternion.identity,Quaternion.Euler(90,35,0)})
                {
                    var centre=new LabPoint(2+id[0]-'A',.8f,-3);var rotation=Quaternion.Euler(0,62,0);var origin=new Vector3(4,.1f,7);
                    var observed=new Pose(origin+rotation*new Vector3(centre.x,centre.y,centre.z),rotation*mount);
                    Check(LabFixedPose.TryWorldFromMap(observed,centre,new[]{mount.x,mount.y,mount.z,mount.w},out var recovered)&&Vector3.Distance(recovered.position,origin)<.001f&&Quaternion.Angle(recovered.rotation,rotation)<.05f,"Cold-start map frame from "+id+" flat/wall, no A-only dependency");
                }
            }
            File.WriteAllText("Logs/LabTest/Map082/result.txt","PASS "+checks+" input/registration/continuity/single-card/arbitrary-start checks. Uses real supplied ZIP plus explicit synthetic pose fixtures. Physical Samsung survey/navigation NOT VERIFIED.\n");
        }
    }
}
