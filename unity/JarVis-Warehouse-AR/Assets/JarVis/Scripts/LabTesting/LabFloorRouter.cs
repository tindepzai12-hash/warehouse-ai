using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Lab
{
    // Experimental centreline routing on observed floor only. This is not a clearance certificate.
    public sealed class LabFloorRouter
    {
        public const float MinimumClearance=.15f;
        readonly LabFloorPlan floor;
        readonly float[] costs;
        readonly int[] predecessors;
        readonly bool[] visited;
        readonly Heap open=new Heap();
        static readonly int[] DeltaX={1,-1,0,0,1,1,-1,-1},DeltaZ={0,0,1,-1,1,-1,1,-1};
        public LabFloorRouter(LabFloorPlan plan){floor=plan;int n=plan?.cells?.Length??0;costs=new float[n];predecessors=new int[n];visited=new bool[n];}
        bool Inside(int x,int z)=>x>=0&&z>=0&&x<floor.width&&z<floor.height;
        bool Open(int x,int z)=>Inside(x,z)&&floor.cells[z*floor.width+x]==1&&floor.clearance!=null&&floor.clearance.Length==floor.cells.Length&&floor.clearance[z*floor.width+x]>=(floor.requiredClearance>0?floor.requiredClearance:MinimumClearance)-.001f;
        bool Cell(LabPoint p,out int x,out int z)
        {
            x=z=0;if(floor?.Valid!=true||!LabPaths.Valid(p))return false;
            x=(int)Math.Floor((p.x-floor.originX)/floor.resolution);z=(int)Math.Floor((p.z-floor.originZ)/floor.resolution);return Open(x,z);
        }
        public string Classify(LabPoint p)
        {
            if(floor?.Valid!=true||!LabPaths.Valid(p))return "INVALID_POSITION";
            int x=(int)Math.Floor((p.x-floor.originX)/floor.resolution),z=(int)Math.Floor((p.z-floor.originZ)/floor.resolution);
            if(!Inside(x,z))return "OUTSIDE_MAP";
            int type=floor.cells[z*floor.width+x];
            if(type==0)return "UNKNOWN_FLOOR";
            if(type!=1)return "OBSTACLE";
            return Open(x,z)?"VALID":"CLEARANCE_MARGIN";
        }
        public bool Walkable(LabPoint p)=>Classify(p)=="VALID";
        public float Clearance(LabPoint p)
        {
            if(!LabPaths.Valid(p)||floor?.Valid!=true)return 0;
            int x=(int)Math.Floor((p.x-floor.originX)/floor.resolution),z=(int)Math.Floor((p.z-floor.originZ)/floor.resolution);
            return Inside(x,z)&&floor.clearance?.Length==floor.cells.Length?floor.clearance[z*floor.width+x]:0;
        }
        public bool KnownFloorSegment(LabPoint a,LabPoint b)
        {
            if(!LabPaths.Valid(a)||!LabPaths.Valid(b)||floor?.Valid!=true)return false;
            int n=Math.Max(1,(int)Math.Ceiling(LabPaths.Distance(a,b)/(floor.resolution*.2f)));
            int px=-1,pz=-1;
            for(int i=0;i<=n;i++)
            {
                float t=i/(float)n;
                int x=(int)Math.Floor((a.x+(b.x-a.x)*t-floor.originX)/floor.resolution),z=(int)Math.Floor((a.z+(b.z-a.z)*t-floor.originZ)/floor.resolution);
                if(!Inside(x,z)||floor.cells[z*floor.width+x]!=1)return false;
                if(px>=0&&x!=px&&z!=pz&&(floor.cells[pz*floor.width+x]!=1||floor.cells[z*floor.width+px]!=1))return false;
                px=x;pz=z;
            }
            return true;
        }
        public bool TryMarginProjection(LabPoint raw,LabPoint previous,float limit,out LabPoint point,out string reason)
        {
            point=null;reason=Classify(raw);
            if(reason=="VALID"){point=raw;return true;}
            if(reason!="CLEARANCE_MARGIN")return false;
            int cx=(int)Math.Floor((raw.x-floor.originX)/floor.resolution),cz=(int)Math.Floor((raw.z-floor.originZ)/floor.resolution);
            int r=(int)Math.Ceiling(limit/floor.resolution);float best=float.PositiveInfinity;
            var options=new List<LabPoint>();
            for(int z=cz-r;z<=cz+r;z++)for(int x=cx-r;x<=cx+r;x++)
            {
                if(!Open(x,z))continue;var p=Point(z*floor.width+x);float d=LabPaths.Distance(raw,p);
                if(d>limit||!KnownFloorSegment(raw,p))continue;
                if(previous!=null&&LabPaths.Distance(previous,p)<1.1f&&!Segment(previous,p))continue;
                if(d<best){best=d;point=p;}
                options.Add(p);
            }
            if(point==null)return false;
            foreach(var p in options)
                if(LabPaths.Distance(raw,p)<=best+.025f&&LabPaths.Distance(point,p)>.35f&&!Segment(point,p))
                {point=null;reason="AMBIGUOUS_AISLE";return false;}
            reason="MARGIN_CORRECTION";return true;
        }
        LabPoint Point(int id)=>new LabPoint(floor.originX+(id%floor.width+.5f)*floor.resolution,0,floor.originZ+(id/floor.width+.5f)*floor.resolution);
        public bool Segment(LabPoint a,LabPoint b)
        {
            if(!Cell(a,out int px,out int pz)||!Cell(b,out _,out _))return false;
            int steps=Math.Max(1,(int)Math.Ceiling(LabPaths.Distance(a,b)/(floor.resolution*.2f)));
            for(int i=1;i<=steps;i++)
            {
                float t=i/(float)steps;
                if(!Cell(new LabPoint(a.x+(b.x-a.x)*t,0,a.z+(b.z-a.z)*t),out int x,out int z))return false;
                if(x!=px&&z!=pz&&(!Open(x,pz)||!Open(px,z)))return false;
                px=x;pz=z;
            }
            return true;
        }
        public string Find(LabPoint start,LabPoint end,out List<LabPoint> route)
        {
            route=new List<LabPoint>();
            if(!Cell(start,out int sx,out int sz))return "OFF_FLOOR";
            if(!Cell(end,out int ex,out int ez))return "TARGET_NOT_ON_FLOOR";
            int n=floor.cells.Length,begin=sz*floor.width+sx,goal=ez*floor.width+ex;
            var cost=costs;var previous=predecessors;var closed=visited;Array.Clear(closed,0,n);
            for(int i=0;i<n;i++){cost[i]=float.PositiveInfinity;previous[i]=-1;}cost[begin]=0;
            var heap=open;heap.Clear();heap.Add(begin,0);
            var dx=DeltaX;var dz=DeltaZ;
            while(heap.Count>0)
            {
                int id=heap.Pop();if(closed[id])continue;closed[id]=true;if(id==goal)break;
                int x=id%floor.width,z=id/floor.width;
                for(int d=0;d<8;d++)
                {
                    int xx=x+dx[d],zz=z+dz[d];if(!Open(xx,zz)||d>=4&&(!Open(xx,z)||!Open(x,zz)))continue;
                    int next=zz*floor.width+xx;if(closed[next])continue;
                    float step=floor.resolution*(d<4?1:1.414214f)*(1+.08f/(floor.clearance[next]+.02f)),g=cost[id]+step;
                    if(g>=cost[next])continue;cost[next]=g;previous[next]=id;
                    float h=(float)Math.Sqrt((xx-ex)*(xx-ex)+(zz-ez)*(zz-ez))*floor.resolution;heap.Add(next,g+h);
                }
            }
            if(!closed[goal])return "NO_FLOOR_ROUTE";
            var raw=new List<LabPoint>();raw.Add(end);for(int id=goal;id!=begin;id=previous[id]){if(id<0)return "NO_FLOOR_ROUTE";raw.Add(Point(id));}raw.Add(Point(begin));raw.Add(start);raw.Reverse();
            // Visibility checks include every crossed grid cell and forbid cutting obstacle corners.
            route.Add(start);int cursor=0;
            while(cursor<raw.Count-1)
            {
                int next=cursor+1;for(int i=next+1;i<raw.Count;i++){if(!Segment(raw[cursor],raw[i]))break;next=i;}
                if(!Segment(raw[cursor],raw[next])){route.Clear();return "NO_FLOOR_ROUTE";}
                if(LabPaths.Distance(route[route.Count-1],raw[next])>.001f)route.Add(raw[next]);cursor=next;
            }
            return null;
        }
        sealed class Heap
        {
            readonly List<int> ids=new List<int>();readonly List<float> keys=new List<float>();public int Count=>ids.Count;
            public void Clear(){ids.Clear();keys.Clear();}
            void Swap(int a,int b){int i=ids[a];ids[a]=ids[b];ids[b]=i;float k=keys[a];keys[a]=keys[b];keys[b]=k;}
            public void Add(int id,float key){int i=ids.Count;ids.Add(id);keys.Add(key);while(i>0){int p=(i-1)/2;if(keys[p]<=keys[i])break;Swap(p,i);i=p;}}
            public int Pop(){int result=ids[0],last=ids.Count-1;ids[0]=ids[last];keys[0]=keys[last];ids.RemoveAt(last);keys.RemoveAt(last);int i=0;while(i*2+1<ids.Count){int j=i*2+1;if(j+1<ids.Count&&keys[j+1]<keys[j])j++;if(keys[i]<=keys[j])break;Swap(i,j);i=j;}return result;}
        }
    }
}
