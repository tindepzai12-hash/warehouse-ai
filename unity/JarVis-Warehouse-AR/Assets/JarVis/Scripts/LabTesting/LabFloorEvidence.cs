using System;
using System.Collections.Generic;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    // Short-lived positive floor observations. Missing observations remain unknown.
    public sealed class LabFloorEvidence
    {
        struct Seen {public int votes;public double last;}
        readonly Dictionary<long,Seen> seen=new Dictionary<long,Seen>();
        readonly HashSet<long> frame=new HashSet<long>();
        readonly List<long> expired=new List<long>();
        readonly LabFloorRouter router;double lastTimestamp=-1;int epoch=-1;
        public LabFloorEvidence(LabFloorRouter floor){router=floor;}
        long Key(int x,int z)=>((long)x<<32)^(uint)z;
        public void Reset(int reference){seen.Clear();frame.Clear();lastTimestamp=-1;epoch=reference;}
        public void Observe(int reference,double timestamp,double now,IReadOnlyList<LabPoint> points)
        {
            if(epoch!=reference)Reset(reference);
            if(timestamp<=lastTimestamp||double.IsNaN(timestamp)||double.IsInfinity(timestamp))return;lastTimestamp=timestamp;frame.Clear();
            expired.Clear();foreach(var item in seen)if(now-item.Value.last>3)expired.Add(item.Key);
            foreach(var key in expired)seen.Remove(key);
            foreach(var point in points)
            {
                if(!LabPaths.Valid(point)||Math.Abs(point.y)>.12f||!router.Walkable(point))continue;
                long key=Key((int)Math.Floor(point.x/.2f),(int)Math.Floor(point.z/.2f));
                if(!frame.Add(key))continue;
                seen.TryGetValue(key,out var value);if(now-value.last>.8)value.votes=0;
                value.last=now;value.votes=Math.Min(3,value.votes+1);seen[key]=value;
            }
        }
        public bool Supports(LabPoint point,double now)
        {
            int x=(int)Math.Floor(point.x/.2f),z=(int)Math.Floor(point.z/.2f);
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                if(seen.TryGetValue(Key(x+dx,z+dz),out var value)&&value.votes>=2&&now-value.last<=3&&
                    Math.Pow((x+dx+.5)*.2-point.x,2)+Math.Pow((z+dz+.5)*.2-point.z,2)<=.18*.18)return true;
            return false;
        }
        public float FrontCoverage(IList<LabPoint> path,double now)
        {
            if(path==null||path.Count<2)return 0;
            float length=Math.Min(1.8f,LabPaths.Length(path));int total=0,valid=0;
            // The user's standing area is not inferred from a camera that cannot see underneath itself.
            for(float distance=.6f;distance<=length+.001f;distance+=.2f)
            {
                total++;var centre=LabPaths.Sample(path,distance);var before=LabPaths.Sample(path,Math.Max(0,distance-.1f));
                var after=LabPaths.Sample(path,Math.Min(LabPaths.Length(path),distance+.1f));
                float dx=after.x-before.x,dz=after.z-before.z,n=(float)Math.Sqrt(dx*dx+dz*dz);
                if(n<1e-5f)continue;
                bool across=true;
                for(int lane=-1;lane<=1;lane++)
                    across&=Supports(new LabPoint(centre.x+dz/n*lane*.30f,0,centre.z-dx/n*lane*.30f),now);
                if(across)valid++;
            }
            return total>0?valid/(float)total:1;
        }
    }
}
