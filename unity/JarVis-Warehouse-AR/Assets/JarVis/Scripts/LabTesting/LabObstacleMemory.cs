using System;
using System.Collections.Generic;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    // Camera evidence can close an authored aisle temporarily. It never edits the static floor or localization.
    public sealed class LabObstacleMemory
    {
        sealed class Cell {public LabPoint point;public int votes;public double first,last;}
        readonly Dictionary<long,Cell> cells=new Dictionary<long,Cell>();
        readonly Dictionary<long,List<LabPoint>> frameCells=new Dictionary<long,List<LabPoint>>();
        readonly LabDepthPolicy policy;readonly LabFloorRouter floor;
        double lastFrame=-1,groundAt=-1;int groundFrames,epoch=-1;
        public string Status {get;private set;}="WAITING_DEPTH";
        public int GroundFrames=>groundFrames;
        public int ConfirmedCount {get;private set;}
        public LabObstacleMemory(LabDepthPolicy rules,LabFloorRouter router){policy=rules;floor=router;}
        public void Reset(int referenceEpoch)
        {cells.Clear();frameCells.Clear();lastFrame=groundAt=-1;groundFrames=0;ConfirmedCount=0;epoch=referenceEpoch;Status="WAITING_DEPTH";}
        long Key(LabPoint p)=>((long)(int)Math.Floor(p.x/policy.cellSize)<<32)^(uint)(int)Math.Floor(p.z/policy.cellSize);
        public void Observe(int referenceEpoch,double timestamp,double now,IReadOnlyList<LabPoint> points)
        {
            if(referenceEpoch!=epoch)Reset(referenceEpoch);
            Expire(now);
            if(double.IsNaN(timestamp)||timestamp<=lastFrame||points==null){Status="NO_NEW_DEPTH";return;}
            lastFrame=timestamp;frameCells.Clear();int ground=0;
            foreach(var p in points)
            {
                if(!LabPaths.Valid(p))continue;
                if(Math.Abs(p.y)<=.12f&&floor.Walkable(p)){ground++;continue;}
                if(p.y<policy.minHeight||p.y>policy.maxHeight||!floor.Walkable(p))continue;
                long k=Key(p);if(!frameCells.TryGetValue(k,out var list)){list=new List<LabPoint>();frameCells.Add(k,list);}list.Add(p);
            }
            if(now-groundAt>.8)groundFrames=0;
            if(ground>=6){groundAt=now;groundFrames=Math.Min(3,groundFrames+1);}
            if(groundFrames<3||now-groundAt>.8){Status="GROUND_UNCONFIRMED";return;}
            foreach(var pair in frameCells)
            {
                if(pair.Value.Count<policy.minPixels)continue;
                if(!cells.TryGetValue(pair.Key,out var cell)){cell=new Cell{first=now};cells.Add(pair.Key,cell);}
                if(now-cell.last>.7) {cell.votes=0;cell.first=now;}
                cell.last=now;cell.votes=Math.Min(policy.confirmFrames,cell.votes+1);
                var p=pair.Value[0];cell.point=new LabPoint(p.x,0,p.z);
            }
            Expire(now);Status=ConfirmedCount>0?"OBSTACLES_CONFIRMED":"OBSERVING_KNOWN_FLOOR";
        }
        void Expire(double now)
        {foreach(var k in cells.Where(p=>now-p.Value.last>policy.ttl).Select(p=>p.Key).ToArray())cells.Remove(k);ConfirmedCount=cells.Values.Count(c=>c.votes>=policy.confirmFrames&&c.last-c.first>=.25);}
        public LabPoint[] ConfirmedPoints(double now)
        {Expire(now);return cells.Values.Where(c=>c.votes>=policy.confirmFrames&&c.last-c.first>=.25).Select(c=>c.point).ToArray();}
        public string[] BlockedEdges(LabCorridorGraph graph,double now)
        {
            Expire(now);
            if(graph?.edges==null)return Array.Empty<string>();
            var blocked=new HashSet<string>();
            foreach(var c in cells.Values)
            {
                if(c.votes<policy.confirmFrames||c.last-c.first<.25)continue;
                foreach(var e in graph.edges)
                    for(int i=1;i<e.points.Length;i++)if(DistanceToSegment(c.point,e.points[i-1],e.points[i])<=Math.Max(policy.edgeRadius,LabLocalPathfinder.BodyRadius+LabLocalPathfinder.ObjectRadius)){blocked.Add(e.id);break;}
            }
            return blocked.OrderBy(s=>s).ToArray();
        }
        public bool Intersects(IReadOnlyList<LabPoint> path,double now)
        {
            Expire(now);if(path==null)return false;
            foreach(var c in cells.Values)
            {
                if(c.votes<policy.confirmFrames||c.last-c.first<.25)continue;
                for(int i=1;i<path.Count;i++)if(DistanceToSegment(c.point,path[i-1],path[i])<=Math.Max(policy.edgeRadius,LabLocalPathfinder.BodyRadius+LabLocalPathfinder.ObjectRadius))return true;
            }
            return false;
        }
        public static float DistanceToSegment(LabPoint p,LabPoint a,LabPoint b)
        {float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz,t=n<1e-8f?0:Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/n));return LabPaths.Distance(p,new LabPoint(a.x+t*dx,0,a.z+t*dz));}
    }
}
