using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AtlasAR
{
    [Serializable] public class AtlasArrival { public string id,approachEvidence; public Vector3 position; public bool physicalVerified; }
    [Serializable] public class AtlasNavigationConfig { public string mapId,mapSha256; public int revision,agentTypeId; public float bodyRadius; public AtlasArrival[] arrivals; }

    // Query space remains the fixed metric map, never the moving XR Origin.
    // Only the returned polyline is rendered through the current map-to-AR transform.
    public sealed class AtlasNavMeshService:IDisposable
    {
        readonly AtlasRoutePlanner clearance;
        readonly AtlasMap map;
        readonly AtlasNavigationConfig config;
        NavMeshDataInstance instance;
        readonly NavMeshQueryFilter filter;
        readonly NavMeshPath path=new NavMeshPath();
        public bool Ready=>instance.valid;
        public AtlasNavigationConfig Config=>config;
        public AtlasNavMeshService(AtlasMap map)
        {
            this.map=map;
            clearance=new AtlasRoutePlanner(map);
            var text=Resources.Load<TextAsset>("Atlas/NavigationConfig");
            if(text==null)return;
            config=JsonUtility.FromJson<AtlasNavigationConfig>(text.text);
            if(config==null||config.mapId!=map.id||config.arrivals?.Length!=5||Mathf.Abs(config.bodyRadius-.25f)>.0001f)
                throw new InvalidOperationException("Navigation config/map mismatch");
            var ids=new HashSet<string>();
            foreach(var arrival in config.arrivals)
                if(map.Marker(arrival.id)==null||!ids.Add(arrival.id)||!clearance.CanWalk(arrival.position))
                    throw new InvalidOperationException("Invalid fixed arrival: "+arrival.id);
            filter=new NavMeshQueryFilter{agentTypeID=config.agentTypeId,areaMask=NavMesh.AllAreas};
            var data=Resources.Load<NavMeshData>("Atlas/RoomNavMesh");
            if(data!=null)instance=NavMesh.AddNavMeshData(data,Vector3.zero,Quaternion.identity);
        }
        public Vector3 Arrival(string id)
        {
            var arrival=config?.arrivals==null?null:Array.Find(config.arrivals,a=>a.id==id);
            if(arrival==null)throw new InvalidOperationException("Unknown arrival: "+id);
            return arrival.position;
        }
        public AtlasRouteResult Plan(Vector3 camera,string target)
        {
            if(!Ready)return Fail("NAVMESH_NOT_READY");
            if(!AtlasMap.Finite(camera))return Fail("INVALID_START");
            var destination=Array.Find(config.arrivals,a=>a.id==target);
            if(destination==null)return Fail("UNKNOWN_TARGET");
            return PlanTo(camera,destination.position);
        }
        AtlasRouteResult PlanTo(Vector3 camera,Vector3 end)
        {
            var start=new Vector3(camera.x,0,camera.z);
            if(!clearance.CanWalk(start))return Fail("START_BLOCKED");
            if(Vector3.Distance(start,end)<.015f)return Success(new[]{start});
            // Small rounding tolerance, followed by a continuous analytic clearance check.
            // The actual camera and live map dot are never snapped.
            if(!NavMesh.SamplePosition(start,out var source,.12f,filter)||!clearance.SegmentClear(start,source.position))return Fail("START_NOT_CONNECTED");
            if(!NavMesh.SamplePosition(end,out var goal,.12f,filter)||!clearance.SegmentClear(goal.position,end))return Fail("ARRIVAL_NOT_CONNECTED");
            if(!NavMesh.CalculatePath(source.position,goal.position,filter,path)||path.status!=NavMeshPathStatus.PathComplete)return Fail("NO_COMPLETE_PATH");
            var result=new List<Vector3>{start};
            foreach(var corner in path.corners){var p=new Vector3(corner.x,0,corner.z);if(Vector3.Distance(result[result.Count-1],p)>.005f)result.Add(p);}
            if(Vector3.Distance(result[result.Count-1],end)>.005f)result.Add(end);
            for(int i=1;i<result.Count;i++)if(!clearance.SegmentClear(result[i-1],result[i]))return Fail("PATH_CLEARANCE_FAILED");
            return Success(result.ToArray());
        }
        // Explicit owner action only: bounded search for genuinely distinct, connected
        // corridors. No AI inference or unknown floor becomes walkable here.
        public List<AtlasRouteResult> Alternatives(Vector3 camera,string target)
        {
            var first=Plan(camera,target);var choices=new List<AtlasRouteResult>();if(!first.valid)return choices;choices.Add(first);
            var end=Arrival(target);float minX=float.MaxValue,minZ=float.MaxValue,maxX=float.MinValue,maxZ=float.MinValue;
            foreach(var p in map.room){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);}
            var candidates=new List<Vector3>();var start=new Vector3(camera.x,0,camera.z);
            for(float x=minX+.5f;x<maxX;x+=1.25f)for(float z=minZ+.5f;z<maxZ;z+=1.25f)
            {var p=new Vector3(x,0,z);if(clearance.CanWalk(p)&&DistanceToRoute(p,first.points)>.85f&&Vector3.Distance(start,p)+Vector3.Distance(p,end)<first.length*1.65f+2)candidates.Add(p);}
            candidates.Sort((a,b)=>(Vector3.Distance(start,a)+Vector3.Distance(a,end)).CompareTo(Vector3.Distance(start,b)+Vector3.Distance(b,end)));
            for(int n=0;n<Mathf.Min(96,candidates.Count)&&choices.Count<3;n++)
            {
                var a=PlanTo(start,candidates[n]);if(!a.valid)continue;var b=PlanTo(candidates[n],end);if(!b.valid)continue;
                var joined=new List<Vector3>(a.points);for(int j=1;j<b.points.Length;j++)joined.Add(b.points[j]);
                // Remove avoidable doglegs in open floor before testing route diversity.
                var simple=new List<Vector3>{joined[0]};int at=0;
                while(at<joined.Count-1){int next=joined.Count-1;while(next>at+1&&!clearance.SegmentClear(joined[at],joined[next]))next--;simple.Add(joined[next]);at=next;}
                var result=Success(simple.ToArray());if(result.length>first.length*1.65f+2||HasReversal(result.points))continue;
                bool distinct=true;foreach(var old in choices)if(!Different(result.points,old.points)){distinct=false;break;}
                if(distinct)choices.Add(result);
            }
            choices.Sort((a,b)=>a.length.CompareTo(b.length));return choices;
        }
        static bool HasReversal(Vector3[] p)
        {
            for(int i=1;i<p.Length-1;i++)if(Vector3.Dot((p[i]-p[i-1]).normalized,(p[i+1]-p[i]).normalized)<-.65f)return true;
            // Reject loops, including an out-and-back spur, rather than offering fake alternatives.
            for(int i=1;i<p.Length;i++)for(int j=i+2;j<p.Length;j++)
            {
                var a=p[i-1];var b=p[i];var c=p[j-1];var d=p[j];var ab=b-a;var cd=d-c;
                float denominator=ab.x*cd.z-ab.z*cd.x;if(Mathf.Abs(denominator)<.0001f)continue;
                float t=((c.x-a.x)*cd.z-(c.z-a.z)*cd.x)/denominator,u=((c.x-a.x)*ab.z-(c.z-a.z)*ab.x)/denominator;
                if(t>0&&t<1&&u>0&&u<1)return true;
            }
            return false;
        }
        static bool Different(Vector3[] a,Vector3[] b)
        {foreach(var p in a)if(DistanceToRoute(p,b)>.85f)return true;foreach(var p in b)if(DistanceToRoute(p,a)>.85f)return true;return false;}
        static float DistanceToRoute(Vector3 p,Vector3[] route)
        {float best=float.MaxValue;for(int i=1;i<route.Length;i++){var v=route[i]-route[i-1];float t=Mathf.Clamp01(Vector3.Dot(p-route[i-1],v)/Mathf.Max(.00001f,v.sqrMagnitude));best=Mathf.Min(best,Vector3.Distance(p,route[i-1]+v*t));}return best;}
        static AtlasRouteResult Success(Vector3[] points)
        {float length=0;for(int i=1;i<points.Length;i++)length+=Vector3.Distance(points[i-1],points[i]);return new AtlasRouteResult{valid=true,reason="OK_NAVMESH",points=points,length=length};}
        static AtlasRouteResult Fail(string reason)=>new AtlasRouteResult{valid=false,reason=reason,points=Array.Empty<Vector3>()};
        public void Dispose(){if(instance.valid)instance.Remove();}
    }
}
