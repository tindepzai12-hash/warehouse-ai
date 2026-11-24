using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabCalibratedMarker
    {
        public string id;
        public float width;
        public float[] rotationInMap;
    }
    [Serializable] public sealed class LabCalibrationProfile
    {
        public int schema=1,revision;
        public string mapId,geometrySha256,utc;
        public bool loopChecked,independentCChecked;
        public float baselineError,loopPositionError,loopAngleError;
        public List<LabCalibratedMarker> markers=new List<LabCalibratedMarker>();
        public bool Matches(LabFloorPlan floor)
        {
            if(floor==null||(schema!=1&&schema!=2)||!loopChecked||mapId!=floor.id||revision!=floor.revision||geometrySha256!=floor.geometrySha256||markers==null||markers.Count<2||markers.Count>5)return false;
            var ids=new HashSet<string>();
            foreach(var m in markers)
            {
                if(m==null||m.id==null||m.id.Length!=1||m.id[0]<'A'||m.id[0]>'E'||!ids.Add(m.id)||!Finite(m.width)||m.width<.025f||m.width>.4f||m.rotationInMap==null||m.rotationInMap.Length!=4)return false;
                float norm=0;foreach(var q in m.rotationInMap){if(!Finite(q))return false;norm+=q*q;}
                if(Math.Abs(norm-1)>.02f)return false;
            }
            return (schema==2||ids.Contains("A")&&ids.Contains("B"))&&Finite(baselineError)&&baselineError<=.225f&&Finite(loopPositionError)&&loopPositionError<=.25f&&Finite(loopAngleError)&&loopAngleError<=(schema==2?5:3);
        }
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
    }
    public static class LabNavigationPolicy
    {
        // Determine yaw from two measured marker centres; never stretch the source map.
        public static LabMapFit AlignPair(LabPoint mapA,LabPoint mapB,LabPoint observedA,LabPoint observedB)
        {
            var fit=new LabMapFit{reason="MISSING_PAIR"};
            if(!LabPaths.Valid(mapA)||!LabPaths.Valid(mapB)||!LabPaths.Valid(observedA)||!LabPaths.Valid(observedB))return fit;
            double source=LabPaths.Distance(mapA,mapB),target=LabPaths.Distance(observedA,observedB);
            if(source<2||target<2){fit.reason="SHORT_BASELINE";return fit;}
            fit.scaleRatio=(float)(target/source);
            if(Math.Abs(target-source)>.45||Math.Abs(fit.scaleRatio-1)>.03){fit.reason="BASELINE_MISMATCH";return fit;}
            fit.yaw=(float)((Math.Atan2(observedB.x-observedA.x,observedB.z-observedA.z)-Math.Atan2(mapB.x-mapA.x,mapB.z-mapA.z))*180/Math.PI);
            var mid=new LabPoint((mapA.x+mapB.x)*.5f,0,(mapA.z+mapB.z)*.5f);
            var rotated=fit.Transform(mid);fit.tx=(observedA.x+observedB.x)*.5f-rotated.x;fit.tz=(observedA.z+observedB.z)*.5f-rotated.z;
            fit.maxError=Math.Max(LabPaths.Distance(fit.Transform(mapA),observedA),LabPaths.Distance(fit.Transform(mapB),observedB));
            fit.valid=fit.maxError<=.225f;fit.reason=fit.valid?"PAIR_ESTIMATED":"PAIR_RESIDUAL";return fit;
        }
        public static bool MayResume(bool sameReference,bool stable,double lostSeconds,float displacement,bool preview=false)
            =>sameReference&&stable&&lostSeconds>=0&&lostSeconds<=(preview?12:8)&&displacement>=0&&displacement<=(preview?1.5f:.6f);
        public static bool NeedsPlan(bool empty,bool recovered,bool contextChanged,bool moved,bool firstSegmentInvalid,double now,double next)
            =>now>=next&&(empty||recovered||contextChanged||moved||firstSegmentInvalid);
        public static float TurnAngle(LabPoint a,LabPoint b,LabPoint c)
        {
            double ax=b.x-a.x,az=b.z-a.z,bx=c.x-b.x,bz=c.z-b.z;
            return (float)(Math.Atan2(az*bx-ax*bz,ax*bx+az*bz)*180/Math.PI);
        }
    }
}
