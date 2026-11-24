using System;
using System.Collections.Generic;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabCorridorNode {public string id;public LabPoint point;}
    [Serializable] public sealed class LabCorridorEdge {public string id,fromId,toId;public LabPoint[] points;}
    [Serializable] public sealed class LabCorridorGraph
    {
        public string mapId,floorHash,graphHash,source;
        public int floorRevision,graphRevision;
        public LabCorridorNode[] nodes;
        public LabCorridorEdge[] edges;
    }
    [Serializable] public sealed class LabRouteChoice
    {
        public string id;
        public string[] edges;
        public LabPoint[] points;
        public float length,cost;
        public int turns;
    }
    // Small confirmed-aisle graph; the floor grid remains the collision authority.
    public sealed class LabRoutePlanner
    {
        readonly LabFloorRouter router;
        readonly LabCorridorGraph graph;
        readonly HashSet<string> blocked=new HashSet<string>(StringComparer.Ordinal);
        public void SetBlocked(IEnumerable<string> edges){blocked.Clear();if(edges!=null)foreach(string edge in edges)if(graph?.edges?.Any(e=>e.id==edge)==true)blocked.Add(edge);}
        public string[] Blocked=>blocked.OrderBy(v=>v,StringComparer.Ordinal).ToArray();
        public string GraphStatus {get;private set;}
        sealed class Link {public string from,to,id;public LabPoint[] points;public float cost;}
        sealed class Path
        {
            public List<string> nodes=new List<string>();
            public List<Link> links=new List<Link>();
            public float cost;
            public string Key=>string.Join(">",nodes);
        }
        public LabRoutePlanner(LabFloorPlan floor,LabCorridorGraph source)
        {
            router=new LabFloorRouter(floor);graph=source;GraphStatus=Validate(floor,source);
        }
        string Validate(LabFloorPlan floor,LabCorridorGraph g)
        {
            if(floor?.Valid!=true||g==null)return "GRAPH_MISSING";
            if(g.mapId!=floor.id||g.floorRevision!=floor.revision||g.floorHash!=floor.geometrySha256)return "GRAPH_MAP_MISMATCH";
            if(g.nodes==null||g.edges==null||g.nodes.Length>128||g.edges.Length>384)return "GRAPH_INVALID";
            var ids=new HashSet<string>();var byId=new Dictionary<string,LabPoint>();
            foreach(var n in g.nodes)
            {if(n==null||string.IsNullOrEmpty(n.id)||!ids.Add(n.id)||!router.Walkable(n.point))return "GRAPH_NODE_INVALID";byId.Add(n.id,n.point);}
            ids.Clear();
            foreach(var e in g.edges)
            {
                if(e==null||string.IsNullOrEmpty(e.id)||!ids.Add(e.id)||e.fromId==e.toId||!byId.ContainsKey(e.fromId)||!byId.ContainsKey(e.toId)||e.points==null||e.points.Length<2||e.points.Length>512||e.points.Any(p=>!router.Walkable(p)))return "GRAPH_EDGE_INVALID";
                if(LabPaths.Distance(e.points[0],byId[e.fromId])>.05f||LabPaths.Distance(e.points[e.points.Length-1],byId[e.toId])>.05f)return "GRAPH_ENDPOINT_INVALID";
                for(int i=1;i<e.points.Length;i++)if(!router.Segment(e.points[i-1],e.points[i]))return "GRAPH_EDGE_BLOCKED";
            }
            return "VALID";
        }
        void Add(Dictionary<string,List<Link>> adj,string a,string b,string id,LabPoint[] points)
        {
            float n=LabPaths.Length(points);float clearance=router.Clearance(LabPaths.Sample(points,n*.5f));
            float cost=n*(1+.08f/Math.Max(.1f,clearance));
            adj[a].Add(new Link{from=a,to=b,id=id,points=points,cost=cost});
            adj[b].Add(new Link{from=b,to=a,id=id,points=points.Reverse().ToArray(),cost=cost});
        }
        string Attach(Dictionary<string,List<Link>> adj,LabPoint p,string name)
        {
            var exact=graph.nodes.FirstOrDefault(n=>LabPaths.Distance(p,n.point)<.04f);
            if(exact!=null)return exact.id;
            adj[name]=new List<Link>();
            foreach(var n in graph.nodes.OrderBy(n=>LabPaths.Distance(p,n.point)).Where(n=>blocked.Count==0&&LabPaths.Distance(p,n.point)<=2.6f&&router.Segment(p,n.point)).Take(4))
                Add(adj,name,n.id,name+"-"+n.id,new[]{p,n.point});
            // Split the nearest visible graph segment. Long aisles must not strand a
            // user simply because both end nodes are more than 2.6 metres away.
            LabCorridorEdge edge=null;LabPoint projection=null;int split=0;float best=.80f;
            foreach(var e in graph.edges.Where(e=>!blocked.Contains(e.id)))for(int i=1;i<e.points.Length;i++)
            {
                var a=e.points[i-1];var b=e.points[i];float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz;
                float t=n>1e-8f?Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/n)):0;
                var q=new LabPoint(a.x+t*dx,0,a.z+t*dz);float d=LabPaths.Distance(p,q);
                if(d<best&&router.Segment(p,q)){best=d;edge=e;projection=q;split=i;}
            }
            if(edge!=null)
            {
                var left=new List<LabPoint>{p,projection};for(int i=split-1;i>=0;i--)left.Add(edge.points[i]);
                var right=new List<LabPoint>{p,projection};for(int i=split;i<edge.points.Length;i++)right.Add(edge.points[i]);
                Add(adj,name,edge.fromId,edge.id,left.ToArray());Add(adj,name,edge.toId,edge.id,right.ToArray());
            }
            return name;
        }
        static Path Search(Dictionary<string,List<Link>> adj,string start,string goal,HashSet<string> blockedNodes,HashSet<string> blockedEdges)
        {
            if(blockedNodes.Contains(start)||blockedNodes.Contains(goal))return null;
            var costs=new Dictionary<string,float>{{start,0}};var prev=new Dictionary<string,Link>();var closed=new HashSet<string>();
            while(true)
            {
                string current=null;float best=float.PositiveInfinity;
                foreach(var pair in costs)if(!closed.Contains(pair.Key)&&pair.Value<best){current=pair.Key;best=pair.Value;}
                if(current==null)return null;if(current==goal)break;closed.Add(current);
                foreach(var link in adj[current])
                {
                    if(closed.Contains(link.to)||blockedNodes.Contains(link.to)||blockedEdges.Contains(link.from+">"+link.to))continue;
                    float next=best+link.cost;
                    if(!costs.TryGetValue(link.to,out float old)||next<old){costs[link.to]=next;prev[link.to]=link;}
                }
            }
            var path=new Path{cost=costs[goal]};path.nodes.Add(goal);
            for(string n=goal;n!=start;)
            {var edge=prev[n];path.links.Add(edge);n=edge.from;path.nodes.Add(n);}
            path.nodes.Reverse();path.links.Reverse();return path;
        }
        List<Path> Alternatives(Dictionary<string,List<Link>> adj,string start,string goal)
        {
            var accepted=new List<Path>();var pool=new List<Path>();var seen=new HashSet<string>();
            var first=Search(adj,start,goal,new HashSet<string>(),new HashSet<string>());if(first==null)return accepted;
            accepted.Add(first);seen.Add(first.Key);
            for(int iteration=1;iteration<24;iteration++)
            {
                var last=accepted[accepted.Count-1];
                for(int i=0;i<last.nodes.Count-1;i++)
                {
                    var root=last.nodes.Take(i+1).ToArray();var bannedEdges=new HashSet<string>();
                    foreach(var p in accepted)
                        if(p.nodes.Count>i+1&&root.SequenceEqual(p.nodes.Take(i+1)))bannedEdges.Add(p.nodes[i]+">"+p.nodes[i+1]);
                    var spur=Search(adj,last.nodes[i],goal,new HashSet<string>(last.nodes.Take(i)),bannedEdges);
                    if(spur==null)continue;
                    var candidate=new Path();
                    candidate.nodes.AddRange(last.nodes.Take(i));candidate.nodes.AddRange(spur.nodes);
                    candidate.links.AddRange(last.links.Take(i));candidate.links.AddRange(spur.links);candidate.cost=candidate.links.Sum(e=>e.cost);
                    if(seen.Add(candidate.Key))pool.Add(candidate);
                }
                if(pool.Count==0)break;
                var next=pool.OrderBy(p=>p.cost).ThenBy(p=>p.Key,StringComparer.Ordinal).First();pool.Remove(next);accepted.Add(next);
            }
            return accepted;
        }
        LabRouteChoice Choice(Path p,LabPoint start,LabPoint end)
        {
            var raw=new List<LabPoint>{start};
            foreach(var e in p.links)foreach(var v in e.points)if(LabPaths.Distance(raw[raw.Count-1],v)>.005f)raw.Add(v);
            if(LabPaths.Distance(raw[raw.Count-1],end)>.005f)raw.Add(end);
            var points=new List<LabPoint>();
            for(int i=0;i<raw.Count;i++)
            {
                if(i>0&&i<raw.Count-1&&Math.Abs(LabNavigationPolicy.TurnAngle(points[points.Count-1],raw[i],raw[i+1]))<5&&router.Segment(points[points.Count-1],raw[i+1]))continue;
                points.Add(raw[i]);
            }
            for(int i=1;i<points.Count;i++)if(!router.Segment(points[i-1],points[i]))return null;
            float length=LabPaths.Length(points);int turns=LabManeuver.Build(points).Count;
            return new LabRouteChoice{id=p.Key,points=points.ToArray(),edges=p.links.Where(e=>!e.id.StartsWith("$",StringComparison.Ordinal)).Select(e=>e.id).ToArray(),length=length,turns=turns,cost=p.cost+turns*.7f};
        }
        public string Find(LabPoint start,LabPoint end,out List<LabRouteChoice> choices)
        {
            choices=new List<LabRouteChoice>();
            if(!router.Walkable(start))return router.Classify(start);
            if(!router.Walkable(end))return "TARGET_NOT_ON_FLOOR";
            if(LabPaths.Distance(start,end)<.05f){choices.Add(new LabRouteChoice{id="arrival",points=new[]{start,end},edges=Array.Empty<string>(),length=LabPaths.Distance(start,end)});return null;}
            if(GraphStatus=="VALID")
            {
                var adj=graph.nodes.ToDictionary(n=>n.id,n=>new List<Link>());
                foreach(var e in graph.edges)if(!blocked.Contains(e.id))Add(adj,e.fromId,e.toId,e.id,e.points);
                string a=Attach(adj,start,"$start"),b=Attach(adj,end,"$goal");
                if(a!=b)foreach(var edge in graph.edges.Where(e=>!blocked.Contains(e.id)))
                {
                    if(!ProjectToEdge(edge,start,out float sa,out var sp)||!ProjectToEdge(edge,end,out float ea,out var ep))continue;
                    var points=new List<LabPoint>{start,sp};float along=0;
                    for(int i=1;i<edge.points.Length;i++){along+=LabPaths.Distance(edge.points[i-1],edge.points[i]);if(along>Math.Min(sa,ea)&&along<Math.Max(sa,ea))points.Add(edge.points[i]);}
                    if(sa>ea){var middle=points.Skip(2).Reverse().ToArray();points.RemoveRange(2,points.Count-2);points.AddRange(middle);}
                    points.Add(ep);points.Add(end);Add(adj,a,b,edge.id,points.ToArray());
                }
                var routes=Alternatives(adj,a,b).Select(p=>Choice(p,start,end)).Where(c=>c!=null).OrderBy(c=>c.cost).ThenBy(c=>c.id,StringComparer.Ordinal).ToArray();
                foreach(var route in routes)
                {
                    bool duplicate=choices.Any(other=>{
                        // Compare travelled geometry, not edge counts (one long edge
                        // must not count the same as a tiny connector).
                        float shared=0;for(int i=1;i<route.points.Length;i++)
                        {
                            float n=LabPaths.Distance(route.points[i-1],route.points[i]);int samples=Math.Max(1,(int)Math.Ceiling(n/.2f));
                            for(int j=0;j<samples;j++)
                            {var q=LabPaths.Sample(new[]{route.points[i-1],route.points[i]},n*(j+.5f)/samples);if(NearPolyline(q,other.points,.25f))shared+=n/samples;}
                        }
                        return shared/Math.Max(.01f,Math.Min(other.length,route.length))>.85f;
                    });
                    if(duplicate)continue;
                    choices.Add(route);if(choices.Count>=3)break;
                }
            }
            if(choices.Count>0)return null;
            // A grid fallback must not silently route through a user-blocked aisle.
            if(blocked.Count>0)return "NO_ROUTE_BLOCKED";
            string error=router.Find(start,end,out var fallback);
            if(error!=null)return error;
            float n=LabPaths.Length(fallback);int t=LabManeuver.Build(fallback).Count;
            choices.Add(new LabRouteChoice{id="GRID_FALLBACK",points=fallback.ToArray(),edges=Array.Empty<string>(),length=n,turns=t,cost=n+.7f*t});
            return null;
        }
        static bool NearPolyline(LabPoint p,LabPoint[] points,float limit)
        {
            for(int i=1;i<points.Length;i++)
            {var a=points[i-1];var b=points[i];float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz,t=n>1e-8f?Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/n)):0;if(LabPaths.Distance(p,new LabPoint(a.x+t*dx,0,a.z+t*dz))<limit)return true;}
            return false;
        }
        bool ProjectToEdge(LabCorridorEdge edge,LabPoint p,out float distance,out LabPoint projection)
        {
            distance=0;projection=null;float best=.8f,along=0;
            for(int i=1;i<edge.points.Length;i++)
            {var a=edge.points[i-1];var b=edge.points[i];float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz,t=n>1e-8f?Math.Max(0,Math.Min(1,((p.x-a.x)*dx+(p.z-a.z)*dz)/n)):0;var q=new LabPoint(a.x+t*dx,0,a.z+t*dz);float error=LabPaths.Distance(p,q);if(error<best&&router.Segment(p,q)){best=error;projection=q;distance=along+(float)Math.Sqrt(n)*t;}along+=(float)Math.Sqrt(n);}
            return projection!=null;
        }
    }
}
