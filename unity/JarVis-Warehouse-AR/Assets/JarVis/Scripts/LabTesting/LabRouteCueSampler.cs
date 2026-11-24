using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Lab
{
    public enum LabCueMode { None, Path, TurnBoard, Hud, Look, Arrival }

    // A renderer owns no mutable LabPoint from the planner.
    public sealed class LabRouteVisualContext
    {
        readonly float[] x,z,s;
        public readonly int epoch,revision;
        public readonly string target;
        public int Count=>x.Length;
        public float Length=>s.Length==0?0:s[s.Length-1];
        public LabRouteVisualContext(IList<LabPoint> points,int epoch,int revision,string target)
        {
            if(points==null||points.Count<2)throw new ArgumentException("A visual route requires two points.");
            this.epoch=epoch;this.revision=revision;this.target=target;
            x=new float[points.Count];z=new float[points.Count];s=new float[points.Count];
            for(int i=0;i<points.Count;i++)
            {
                if(!LabPaths.Valid(points[i]))throw new ArgumentException("Non-finite route point.");
                x[i]=points[i].x;z[i]=points[i].z;
                if(i>0){float dx=x[i]-x[i-1],dz=z[i]-z[i-1];s[i]=s[i-1]+(float)Math.Sqrt(dx*dx+dz*dz);}
            }
        }
        public bool Sample(float at,out LabCueToken token)
        {
            token=default;
            if(float.IsNaN(at)||float.IsInfinity(at)||Length<.001f)return false;
            at=Math.Max(0,Math.Min(Length,at));
            for(int i=1;i<Count;i++)
            {
                float length=s[i]-s[i-1];
                if(length<.00001f)continue;
                if(at<=s[i]||i==Count-1)
                {
                    float t=Math.Max(0,Math.Min(1,(at-s[i-1])/length));
                    token=new LabCueToken(-1,at,x[i-1]+(x[i]-x[i-1])*t,z[i-1]+(z[i]-z[i-1])*t,
                        (x[i]-x[i-1])/length,(z[i]-z[i-1])/length);return true;
                }
            }
            return false;
        }
    }
    public readonly struct LabCueToken
    {
        public readonly int cell;
        public readonly float along,x,z,dx,dz;
        public LabCueToken(int cell,float along,float x,float z,float dx,float dz)
        {this.cell=cell;this.along=along;this.x=x;this.z=z;this.dx=dx;this.dz=dz;}
    }

    // Fixed world cells. Only the window changes with accepted progress.
    public sealed class LabRouteCueSampler
    {
        public const float Spacing=.55f,Width=.28f,Depth=.40f,Lead=1.5f;
        LabRouteVisualContext route;
        int first=-1;
        public void Reset(){route=null;first=-1;}
        public int Select(LabRouteVisualContext context,float along,float stopAt,LabCueToken[] output)
        {
            if(context==null||output==null||output.Length<3||float.IsNaN(along)||float.IsInfinity(along)||
                float.IsNaN(stopAt)||float.IsInfinity(stopAt))return 0;
            along=Math.Max(0,Math.Min(context.Length,along));stopAt=Math.Min(context.Length,stopAt);
            float available=stopAt-along;
            if(available<1)return 0;
            int desired=Math.Max(1,(int)Math.Ceiling((along+Lead)/Spacing));
            if(route!=context||first<0){route=context;first=desired;}
            else
            {
                // A small dead band prevents 2/3-token flicker at a cell boundary.
                if(first*Spacing-along<Lead-.12f||first*Spacing-along>Lead+Spacing+.12f)first=desired;
            }
            int limit=available<2.5f?2:3,count=0;
            for(int cell=first;count<limit;cell++)
            {
                float s=cell*Spacing;
                if(s+Depth*.5f>stopAt+.001f)break;
                if(!context.Sample(s,out var sample))break;
                output[count++]=new LabCueToken(cell,s,sample.x,sample.z,sample.dx,sample.dz);
            }
            return count;
        }
        // Validate the whole rectangular footprint, including the middle cross section.
        public static bool Footprint(LabCueToken token,LabFloorRouter floor,LabPoint a,LabPoint b)
        {
            if(floor==null)return false;
            float nx=token.dz,nz=-token.dx;
            for(int i=-1;i<=1;i++)
            {
                float forward=i*Depth*.5f;
                a.x=token.x+token.dx*forward+nx*Width*.55f;a.y=0;a.z=token.z+token.dz*forward+nz*Width*.55f;
                b.x=token.x+token.dx*forward-nx*Width*.55f;b.y=0;b.z=token.z+token.dz*forward-nz*Width*.55f;
                if(!floor.KnownFloorSegment(a,b))return false;
            }
            for(int side=-1;side<=1;side+=2)
            {
                float edge=side*Width*.55f;
                a.x=token.x-token.dx*Depth*.5f+nx*edge;a.z=token.z-token.dz*Depth*.5f+nz*edge;
                b.x=token.x+token.dx*Depth*.5f+nx*edge;b.z=token.z+token.dz*Depth*.5f+nz*edge;
                if(!floor.KnownFloorSegment(a,b))return false;
            }
            return true;
        }
    }
}

