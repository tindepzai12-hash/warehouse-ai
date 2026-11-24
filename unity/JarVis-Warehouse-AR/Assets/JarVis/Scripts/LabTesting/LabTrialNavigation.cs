using System;
using System.Linq;
using System.IO;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabTrialPreset
    {
        public int schema;
        public string sourceZipSha256,positionBasis;
        public LabFixedMap map;
    }
    // Explicit owner-requested approximate trial. Never committed as a verified map.
    public static class LabTrialNavigation
    {
        public const string Purpose="OWNER_ACCEPTED_APPROXIMATE_TRIAL_NOT_FIELD_VERIFIED";
        public static string Validate(LabFixedMap map,LabFloorPlan floor)
        {
            if(map==null||map.survey?.purpose!=Purpose||map.physicalAccepted||map.loopChecked||map.survey.loopChecked)return "NOT_TRIAL";
            string error=map.Validate(floor,false);if(error!=null)return error;
            if(map.markers.Count!=5||map.markers.Select(m=>m.id).OrderBy(id=>id).Aggregate("",(a,b)=>a+b)!="ABCDE")return "MISSING_TRIAL_POINTS";
            var router=new LabFloorRouter(floor);
            if(map.markers.Any(m=>!router.Walkable(m.access)||LabPaths.Distance(m.centre,m.access)>1.6f))return "TRIAL_ACCESS_INVALID";
            var fit=LabRegistration.Solve(map.survey,floor);
            return fit.valid&&fit.maxError<.001f&&Math.Abs(fit.scaleRatio-1)<.001f?null:"TRIAL_FRAME_MISMATCH";
        }
    }
    public sealed partial class LabTestSession
    {
        void LoadFixedTrial()
        {
            fixedTrial=null;if(fixedActive!=null)return;
            try
            {
                var text=Resources.Load<TextAsset>("LabTest/LabTrialNavigation");if(text==null)return;
                var preset=JsonUtility.FromJson<LabTrialPreset>(text.text);
                if(preset?.schema!=1||preset.sourceZipSha256?.Length!=64||LabTrialNavigation.Validate(preset.map,Floor)!=null)return;
                fixedTrial=preset.map;
            }
            catch(Exception){fixedTrial=null;}
        }
    }
}
