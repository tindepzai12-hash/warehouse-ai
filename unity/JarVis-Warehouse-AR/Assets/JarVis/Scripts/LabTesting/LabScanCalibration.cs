using System;

namespace JarVis.Warehouse.Lab
{
    // Unique provider observations and elapsed stable time, never a fabricated room coverage %.
    public sealed class LabScanCalibration
    {
        public float Progress { get; private set; }
        public bool Ready=>Progress>=1;
        public float Width { get; private set; }
        public int Samples=>count;
        double first,last;
        int count;
        LabPoint camera,marker;
        float firstWidth;
        long sourceFrame=-1;
        public void Reset(){count=0;Progress=0;Width=0;camera=marker=null;sourceFrame=-1;}
        public void ObserveFrame(long frame,double now,bool tracking,float width,LabPoint position,LabPoint cameraPosition)
        {if(frame<0||frame<=sourceFrame)return;sourceFrame=frame;Observe(now,tracking,width,position,cameraPosition);}
        public void Observe(double now,bool tracking,float width,LabPoint position,LabPoint cameraPosition)
        {
            if(!tracking||!LabPaths.Valid(position)||!LabPaths.Valid(cameraPosition)||float.IsNaN(width)||float.IsInfinity(width)||width<.025f||width>.4f){Reset();return;}
            if(count>0&&now==last)return;
            bool changed=count==0||now-last>.7||now<last||LabPaths.Distance(position,marker)>.05f||Math.Abs(position.y-marker.y)>.05f||Math.Abs(width-firstWidth)>firstWidth*.08f;
            if(changed){first=now;count=0;camera=cameraPosition;marker=position;firstWidth=width;}
            last=now;count++;Width=width;
            float time=Math.Min(1,(float)(now-first)/.8f),samples=Math.Min(1,count/8f);
            Progress=Math.Min(time,samples);
        }
        static float Distance3(LabPoint a,LabPoint b)=> (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
    }
    public static class LabPhoneAlignment
    {
        // Explicit manual heading confirmation. The flat card normal does not define map north.
        public static LabMapFit Solve(LabPoint mapA,LabPoint mapB,LabPoint observedA,LabPoint cameraForward,LabPoint observedCamera=null)
        {
            var fit=new LabMapFit();
            if(!LabPaths.Valid(mapA)||!LabPaths.Valid(mapB)||!LabPaths.Valid(observedA)||!LabPaths.Valid(cameraForward)||LabPaths.Distance(mapA,mapB)<2||LabPaths.Distance(cameraForward,new LabPoint())<.65f)return fit;
            // The camera is offset from the QR centre. Intersect its bearing ray with the known
            // A-to-B distance circle; otherwise a one-metre offset can introduce several degrees.
            double fx=cameraForward.x,fz=cameraForward.z,len=Math.Sqrt(fx*fx+fz*fz);fx/=len;fz/=len;
            var camera=observedCamera??observedA;if(!LabPaths.Valid(camera))return fit;
            double ox=camera.x-observedA.x,oz=camera.z-observedA.z,d=LabPaths.Distance(mapA,mapB),dot=ox*fx+oz*fz;
            double discriminant=dot*dot+d*d-ox*ox-oz*oz;if(discriminant<=0)return fit;
            double distance=-dot+Math.Sqrt(discriminant);if(distance<=0)return fit;
            double a=Math.Atan2(ox+distance*fx,oz+distance*fz)-Math.Atan2(mapB.x-mapA.x,mapB.z-mapA.z);
            fit.yaw=(float)(a*180/Math.PI);var rotated=fit.Transform(mapA);fit.tx=observedA.x-rotated.x;fit.tz=observedA.z-rotated.z;fit.valid=true;return fit;
        }
    }
}
