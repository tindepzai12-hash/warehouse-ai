using System;

namespace JarVis.Warehouse.Lab
{
    // Continuity checks are plausibility tests, not proof of absolute localization.
    public sealed class LabTrackingRecovery
    {
        public bool Active {get;private set;}
        public float Progress {get;private set;}
        public string Reason {get;private set;}="IDLE";
        public LabPoint Origin {get;private set;}
        public double Began {get;private set;}
        public float Budget {get;private set;}
        string reference;bool preview;
        double stableSince=-1,lastSample=-1;
        double mapSince=-1;
        public string Stage {get;private set;}="IDLE";
        public double Deadline=>Began+(preview?20:12);
        LabPoint stablePosition;float stableHeading;
        public static bool FrameUsable(bool ready,bool sessionTracking,bool hasFrame,double age,bool paused)
            =>ready&&sessionTracking&&hasFrame&&!paused&&!double.IsNaN(age)&&age>=0&&age<=1.5;
        public void Reset(){Active=false;Progress=0;Reason="IDLE";Stage="IDLE";Origin=null;reference=null;mapSince=-1;BreakStability();}
        public void Begin(double now,string anchorId,LabPoint origin,bool estimate)
        {Active=true;Began=now;reference=anchorId;Origin=origin;preview=estimate;Reason="WAIT_TRACKING";Stage="TRACKING_WAIT";Budget=0;mapSince=-1;BreakStability();}
        public void BreakStability(){stableSince=lastSample=-1;stablePosition=null;Progress=0;mapSince=-1;}
        public string Tick(double now)
        {
            if(!Active)return "IDLE";
            if(now<Began||now>=Deadline){Reason="RECOVERY_TIMEOUT";Stage="ALIGNMENT_NEEDED";return "NEED_REFERENCE";}
            if(mapSince>=0&&now-mapSince>=2){Reason="MAP_UNCERTAIN";Stage="ALIGNMENT_NEEDED";return "NEED_REFERENCE";}
            return Stage=="ALIGNMENT_NEEDED"?"NEED_REFERENCE":"WAIT";
        }
        // WAIT: keep reference, NEED_REFERENCE: explicit alignment, READY: may validate floor path.
        public string Observe(double now,bool healthy,string anchorId,LabPoint position,float heading,bool floorValid)
        {
            if(!Active)return "IDLE";
            if(Tick(now)=="NEED_REFERENCE")return "NEED_REFERENCE";
            if(string.IsNullOrEmpty(reference)||anchorId!=reference){Reason="REFERENCE_CHANGED";Stage="ALIGNMENT_NEEDED";return "NEED_REFERENCE";}
            double duration=now-Began;
            if(!healthy||!LabPaths.Valid(position)||!LabPaths.Valid(Origin)||float.IsNaN(heading)||float.IsInfinity(heading))
            {BreakStability();Reason="WAIT_TRACKING";Stage="TRACKING_WAIT";return "WAIT";}
            // A missed sample must not count as a stable observation interval.
            if(lastSample>=0&&(now-lastSample>.25||now<lastSample))BreakStability();
            lastSample=now;
            if(stableSince<0){stableSince=now;stablePosition=position;stableHeading=heading;}
            float angle=Math.Abs((heading-stableHeading+540)%360-180);
            if(LabPaths.Distance(position,stablePosition)>.18f||Math.Abs(position.y-stablePosition.y)>.15f||angle>8)
            {BreakStability();Reason="HOLD_STILL";Stage="POSE_STABILIZING";return "WAIT";}
            Progress=Math.Min(1,(float)((now-stableSince)/.65));Reason="STABILIZING";Stage="POSE_STABILIZING";
            if(Progress<1)return "WAIT";
            Budget=preview?Math.Min(3f,.6f+(float)duration*1.2f):Math.Min(1.2f,.3f+(float)duration*.5f);
            if(LabPaths.Distance(Origin,position)>Budget){Reason="DISPLACEMENT_TOO_LARGE";Stage="ALIGNMENT_NEEDED";return "NEED_REFERENCE";}
            Stage="MAP_VALIDATING";
            if(!floorValid){if(mapSince<0)mapSince=now;Reason="FLOOR_UNCERTAIN";return "WAIT";}
            Reason="CONTINUITY_PLAUSIBLE";return "READY";
        }
    }
}
