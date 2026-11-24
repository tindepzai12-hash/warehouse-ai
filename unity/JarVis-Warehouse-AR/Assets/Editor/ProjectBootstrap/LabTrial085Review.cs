using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using UnityEngine;
namespace ProjectBootstrap
{
    public static class LabTrial085Review
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("TRIAL085 "+message);checks++;}
        static void Field(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
        static void Invoke(object o,string n)=>o.GetType().GetMethod(n,Flags).Invoke(o,null);
        public static void RunAndBuild(){Run();LabFixedMapReview.ReviewTrialUi();LabTestBuild.Build();}
        public static void Run()
        {
            checks=0;const string output="Logs/LabTest/Trial085";Directory.CreateDirectory(output);
            var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
            var preset=JsonUtility.FromJson<LabTrialPreset>(Resources.Load<TextAsset>("LabTest/LabTrialNavigation").text);
            var map=preset.map;Check(LabTrialNavigation.Validate(map,floor)==null,"Trial layout matches current floor and all 5 walkable approaches");
            Check(!map.loopChecked&&!map.physicalAccepted&&!map.survey.loopChecked,"Never invent loop or physical acceptance");
            Check(map.Validate(floor,true)!=null,"Strict published-map validation still refuses unchecked trial");
            var planner=new LabRoutePlanner(floor,JsonUtility.FromJson<LabCorridorGraph>(Resources.Load<TextAsset>("LabTest/LabCorridorsNew").text));
            var router=new LabFloorRouter(floor);
            foreach(var a in map.markers)foreach(var b in map.markers.Where(b=>b.id!=a.id))
            {
                string error=planner.Find(a.access,b.access,out var choices);
                Check(error==null&&choices.Count>0,"Route "+a.id+" to "+b.id);
                Check(choices.All(c=>c.points.Length>1&&Enumerable.Range(1,c.points.Length-1).All(i=>router.Segment(c.points[i-1],c.points[i]))),"No route crosses authored obstacles "+a.id+b.id);
            }
            foreach(var m in map.markers)
            {
                var q=new Quaternion(m.rotation[0],m.rotation[1],m.rotation[2],m.rotation[3]);var rotation=Quaternion.Euler(0,63,0);var origin=new Vector3(3,.2f,-4);
                var observed=new Pose(origin+rotation*new Vector3(m.centre.x,m.centre.y,m.centre.z),rotation*q);
                Check(LabFixedPose.TryWorldFromMap(observed,m.centre,m.rotation,out var aligned)&&Vector3.Distance(aligned.position,origin)<.001f&&Quaternion.Angle(aligned.rotation,rotation)<.05f,"Can align from "+m.id+" without A");
            }
            var root=new GameObject("085 navigation trial runtime");
            try
            {
                var owner=root.AddComponent<LabTestSession>();owner.GetType().GetProperty("Floor").SetValue(owner,floor);Field(owner,"ready",true);
                Invoke(owner,"LoadFixedTrial");Check(owner.FixedReady&&owner.TrialNavigation&&!owner.FixedPublishedReady,"Trial unlocks navigation without publishing raw draft");
                foreach(string id in new[]{"A","B","C","D","E"})
                {
                    Check(owner.CanChooseFixed(id),"Destination selectable "+id);owner.StartFixedDestination(id);
                    Check(owner.Exercise.Target==id&&owner.ProfileReady&&owner.Message.Contains("xấp xỉ"),"Cold start "+id+" asks nearby marker with trial notice");
                    Check(!Field<LabCalibrationProfile>(owner,"profile").loopChecked,"Runtime profile does not claim independent loop "+id);
                }
                Check(!owner.CanChooseFixed("Z"),"Unknown destination rejected");
                Field(owner,"fixedActive",map);Invoke(owner,"LoadFixedTrial");Check(!owner.TrialNavigation&&Field<LabFixedMap>(owner,"fixedTrial")==null,"An existing published map takes precedence");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            var bad=JsonUtility.FromJson<LabFixedMap>(JsonUtility.ToJson(map));bad.floorHash="wrong";Check(LabTrialNavigation.Validate(bad,floor)!=null,"Wrong floor refused");
            bad=JsonUtility.FromJson<LabFixedMap>(JsonUtility.ToJson(map));bad.markers.RemoveAt(0);Check(LabTrialNavigation.Validate(bad,floor)!=null,"Missing marker refused");
            File.WriteAllText(output+"/result.txt","PASS "+checks+" trial runtime / 20 ordered routes / 5 any-marker alignment checks. Physical AR accuracy NOT VERIFIED.\n");
        }
    }
}
