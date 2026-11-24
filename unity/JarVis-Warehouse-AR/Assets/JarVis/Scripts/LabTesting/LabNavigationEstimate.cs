using System;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabPoseEstimate
    {
        public LabPoint raw,accepted,display;
        public float rawHeading,displayHeading,correction;
        public string reason="NOT_LOCATED";
        public double time;
        public bool valid,headingValid;
    }
    // Pure numeric presentation filter. Never changes the AR camera, anchor or map registration.
    public sealed class LabNavigationEstimate
    {
        public LabPoseEstimate Current {get;private set;}=new LabPoseEstimate();
        LabPoint previousRaw;double previousTime=-1;
        public void Reset(){Current=new LabPoseEstimate();previousRaw=null;previousTime=-1;}
        static float LerpAngle(float a,float b,float t){float d=(b-a+540)%360-180;return (a+d*t+360)%360;}
        public static float HorizontalHeading(float x,float z,float fallback,out bool valid)
        {valid=x*x+z*z>.04f;return valid?(float)((Math.Atan2(x,z)*180/Math.PI+360)%360):fallback;}
        public LabPoseEstimate Update(LabFloorRouter router,LabPoint raw,float fx,float fz,double now,bool preview)
        {
            float heading=HorizontalHeading(fx,fz,Current.rawHeading,out bool headingValid);
            var next=new LabPoseEstimate{raw=raw,rawHeading=heading,headingValid=headingValid,time=now};
            double dt=previousTime>=0?Math.Max(0,now-previousTime):1;
            if(!LabPaths.Valid(raw)){next.reason="INVALID_POSITION";Current=next;return next;}
            if(previousRaw!=null&&dt>0&&dt<.25&&LabPaths.Distance(raw,previousRaw)>.75f)
            {next.reason="POSE_JUMP";previousRaw=raw;previousTime=now;Current=next;return next;}
            previousRaw=new LabPoint(raw.x,raw.y,raw.z);previousTime=now;
            next.valid=router.TryMarginProjection(raw,Current.accepted,preview?.30f:.15f,out var accepted,out var reason);
            next.reason=reason;next.accepted=accepted;
            if(next.valid)
            {
                next.correction=LabPaths.Distance(raw,accepted);
                float t=Current.valid?(float)(1-Math.Exp(-Math.Min(.25,dt)/.14)):1;
                var old=Current.display??accepted;
                next.display=new LabPoint(old.x+(accepted.x-old.x)*t,accepted.y,old.z+(accepted.z-old.z)*t);
                // Do not animate the dot through a table when turning a narrow corner.
                if(!router.KnownFloorSegment(next.display,accepted))next.display=accepted;
                next.displayHeading=LerpAngle(Current.valid?Current.displayHeading:heading,heading,t);
            }
            else {next.display=Current.display;next.displayHeading=Current.displayHeading;}
            Current=next;return next;
        }
    }
    public sealed class LabHeadingConfirmation
    {
        double started=-1;float firstX,firstZ;LabPoint first;
        public float Progress {get;private set;}
        public void Reset(){started=-1;Progress=0;first=null;}
        public bool Observe(double now,LabPoint camera,float x,float z,bool tracking)
        {
            double len=Math.Sqrt(x*x+z*z);
            if(!tracking||!LabPaths.Valid(camera)||len<.35){Reset();return false;}
            x/=(float)len;z/=(float)len;
            if(started<0){started=now;firstX=x;firstZ=z;first=new LabPoint(camera.x,camera.y,camera.z);}
            float angle=(float)(Math.Acos(Math.Max(-1,Math.Min(1,firstX*x+firstZ*z)))*180/Math.PI);
            if(angle>3||LabPaths.Distance(first,camera)>.15f){Reset();return false;}
            Progress=Math.Min(1,(float)((now-started)/.6));return Progress>=1;
        }
    }
}
