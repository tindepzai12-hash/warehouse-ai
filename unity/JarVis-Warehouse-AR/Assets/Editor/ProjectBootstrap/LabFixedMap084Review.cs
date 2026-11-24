using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;
namespace ProjectBootstrap
{
    public static class LabFixedMap084Review
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static void Check(bool value,string label){if(!value)throw new InvalidOperationException("FIXED084 "+label);checks++;Debug.Log("FIXED084 PASS "+label);}
        static void Field(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetProperty(n).SetValue(o,v);
        public static void RunAndBuild(){Run();LabFixedMapReview.OutputPath="Logs/LabTest/Ui084";LabFixedMapReview.Run();LabTestBuild.Build();}
        public static void Run()
        {
            checks=0;const string output="Logs/LabTest/Fixed084";Directory.CreateDirectory(output);
            string input=Environment.GetEnvironmentVariable("JARVIS_LAB_AUDIT_INPUT"),path=Path.Combine(input,"fixed-map-draft.json"),raw=File.ReadAllText(path);
            var draft=JsonUtility.FromJson<LabFixedMap>(raw);var floor=JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
            var measured=LabSurveyDistances.Create(draft,floor);var ab=measured.pairs.First(p=>p.pair=="AB");
            Check(measured.pairs.Length==10&&measured.pairs.Count(p=>p.measured)==1,"Ten pair reports, only actually captured AB measured");
            Check(Math.Abs(ab.horizontalMetres-14.3163f)<.001f&&Math.Abs(ab.referenceHorizontalMetres-14.49777f)<.001f,"Real AB compared with confirmed 2D/GLB-derived reference");
            Check(measured.pairs.Where(p=>p.pair.Contains("D")||p.pair.Contains("E")).All(p=>p.referenceApproximate),"Owner marked DE remain approximate reference");
            File.WriteAllText(output+"/real-distance-review.json",JsonUtility.ToJson(measured,true));
            var root=new GameObject("084 draft-capture fixture");
            try
            {
                var owner=root.AddComponent<LabTestSession>();Set(owner,"Floor",floor);Set(owner,"Map",draft.survey);Field(owner,"fixedDraft",draft);Field(owner,"fixedSurvey",true);Field(owner,"ready",true);
                var frame=new GameObject("Synthetic frame");frame.transform.SetParent(root.transform);Field(owner,"mapFrame",frame.transform);
                var cameraRoot=new GameObject("Synthetic camera");cameraRoot.transform.SetParent(root.transform);var camera=cameraRoot.AddComponent<Camera>();
                owner.CameraOwner=root.AddComponent<VisualMapSession>();owner.CameraOwner.arCamera=camera;
                var repo=new LabFixedRepository(Path.GetFullPath(output+"/repo-"+Guid.NewGuid().ToString("N")),floor,v=>JsonUtility.ToJson(v,true),v=>JsonUtility.FromJson<LabFixedMap>(v),v=>JsonUtility.ToJson(v,true),v=>JsonUtility.FromJson<LabFixedPointer>(v));Field(owner,"fixedRepository",repo);
                foreach(string id in new[]{"C","D","E"})
                {
                    var calibration=Field<LabScanCalibration>(owner,"calibration");calibration.Reset();
                    for(int i=0;i<8;i++)calibration.ObserveFrame(i+1,i*.15,true,LabPrintedCard.ImageWidthMetres,new LabPoint(0,.9f,0),new LabPoint(0,1.3f,0));
                    owner.GetType().GetMethod("AcceptFixedObservationCore",Flags).Invoke(owner,new object[]{id,new Pose(new Vector3(0,.9f,0),Quaternion.identity)});
                    Check(draft.markers.Any(m=>m.id==id)&&repo.LoadDraft().markers.Any(m=>m.id==id),"Save suspect "+id+" to persistent draft instead of discarding");
                    Check(!string.IsNullOrEmpty(draft.markers.First(m=>m.id==id).captureIssue),"Keep explicit position quality warning for "+id);
                    Check(Field<string>(owner,"fixedScanStage")=="SAVED_NEEDS_REVIEW"&&owner.ScanLabel!="Chưa lưu","Saved-with-review is distinct from failed save "+id);
                }
                Check(draft.markers.Count==5&&repo.LoadDraft().markers.Count==5,"All five captured points retained after reload");
                Check(draft.Validate(floor,false)==null&&draft.Validate(floor,true)!=null,"Raw capture allowed; contradictory navigation activation still refused");
                Check(repo.LoadActive(out _)==null&&!owner.CanChooseFixed("C"),"No pretend active navigation from suspect draft");
                Check(LabSurveyDistances.Create(repo.LoadDraft(),floor).pairs.All(p=>p.measured),"Saved captures supply measured pair distances, including suspect pairs");
                Field(owner,"mapFrame",null);owner.BeginFixedSurvey(false);
                Check(owner.FixedSurvey&&Field<LabFixedMap>(owner,"fixedDraft").markers.Count==5,"Continue existing five-point draft despite quality warning");
                Check(File.ReadAllText(path)==raw,"Supplied raw evidence unchanged");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            File.WriteAllText(output+"/result.txt","PASS "+checks+" draft-persistence/quality-warning/distance/continue checks. Poses are synthetic runtime fixtures; physical Samsung tracking NOT VERIFIED.\n");
        }
    }
}
