using System;
using System.Collections.Generic;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabPoint
    {
        public float x,y,z;
        public LabPoint() { }
        public LabPoint(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
    }
    [Serializable] public sealed class LabMarker
    {
        public string id,photo;
        public LabPoint centre,access;
        public float yaw;
    }
    [Serializable] public sealed class LabLeg
    {
        public string from,to;
        public bool valid;
        public LabPoint[] points;
    }
    [Serializable] public sealed class LabObservation
    {
        public string marker,utc;
        public float positionError,yawError;
    }
    [Serializable] public sealed class LabPhoto
    {
        public string file,utc,marker;
        public LabPoint cameraPosition;
        public float[] cameraRotation;
        public int width,height;
        public float fx,fy,cx,cy;
        public double cameraTime;
    }
    [Serializable] public sealed class LabSurvey
    {
        public int schema=1;
        public string id,utc,device;
        public string coordinateSystem="metres; gravity aligned; origin at printed A centre; +Z is A outward normal";
        public string purpose="OFFLINE_TEST: measured marker poses and walked lines, not verified obstacle geometry";
        public float markerWidthMetres=.2f;
        public bool automaticMarkerSize;
        public bool interrupted,loopChecked;
        public List<LabMarker> markers=new List<LabMarker>();
        public List<LabLeg> legs=new List<LabLeg>();
        public List<LabObservation> observations=new List<LabObservation>();
        public List<LabPhoto> photos=new List<LabPhoto>();
        public LabMapPlacement placement;
    }

    public static class LabPaths
    {
        static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n);
        public static bool Valid(LabPoint p)=>p!=null&&Finite(p.x)&&Finite(p.y)&&Finite(p.z);
        public static float Distance(LabPoint a,LabPoint b)
            =>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.z-b.z)*(a.z-b.z));
        public static float Length(IList<LabPoint> points)
        { float n=0;for(int i=1;i<points.Count;i++)n+=Distance(points[i-1],points[i]);return n; }
        public static bool ValidMap(LabSurvey map)
        {
            if(map==null||map.schema!=1||string.IsNullOrEmpty(map.id)||map.markers==null||map.legs==null||map.markers.Count>5||map.legs.Count>100||
               !Finite(map.markerWidthMetres)||(map.automaticMarkerSize ? map.markerWidthMetres!=0&&(map.markerWidthMetres<.025f||map.markerWidthMetres>.4f) : Math.Abs(map.markerWidthMetres-.2f)>.001f)) return false;
            var ids=new HashSet<string>();
            foreach(var m in map.markers)
                if(m==null||m.id==null||m.id.Length!=1||m.id[0]<'A'||m.id[0]>'E'||!ids.Add(m.id)||!Valid(m.centre)||!Valid(m.access)||!Finite(m.yaw))return false;
            foreach(var e in map.legs)
                if(e==null||e.from==e.to||!ids.Contains(e.from)||!ids.Contains(e.to)||e.points==null||e.points.Length<2||e.points.Length>10000||e.points.Any(p=>!Valid(p)))return false;
            return true;
        }
        static LabPoint Lerp(LabPoint a,LabPoint b,float t)=>new LabPoint(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t);
        public static LabPoint Sample(IList<LabPoint> points,float distance)
        {
            if(points==null||points.Count==0)return null;
            for(int i=1;i<points.Count;i++) {float n=Distance(points[i-1],points[i]);if(distance<=n)return Lerp(points[i-1],points[i],n>.0001f?Math.Max(0,distance)/n:0);distance-=n;}
            return points[points.Count-1];
        }

        // Attach to the observed walked line only. No straight shortcut to a bin or unobserved aisle.
        public static string Find(LabSurvey map,LabPoint current,string target,out List<LabPoint> path)
        {
            path=new List<LabPoint>();
            if(!ValidMap(map)||!Valid(current))return "INVALID_MAP";
            var dest=map.markers.FirstOrDefault(m=>m.id==target);if(dest==null)return "UNKNOWN_TARGET";
            LabLeg attached=null;LabPoint projection=null;int segment=0;float error=float.PositiveInfinity,fromDistance=0;
            foreach(var leg in map.legs.Where(e=>e.valid))
            {
                float walked=0;
                for(int i=1;i<leg.points.Length;i++)
                {
                    var a=leg.points[i-1];var b=leg.points[i];float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz;
                    float t=n>.00001f?Math.Max(0,Math.Min(1,((current.x-a.x)*dx+(current.z-a.z)*dz)/n)):0;
                    var p=Lerp(a,b,t);float d=Distance(current,p);
                    if(d<error){error=d;attached=leg;projection=p;segment=i;fromDistance=walked+(float)Math.Sqrt(n)*t;}
                    walked+=(float)Math.Sqrt(n);
                }
            }
            if(attached==null)return "NO_WALKED_PATH";
            if(error>.65f)return "OFF_WALKED_PATH";
            var distance=new Dictionary<string,float>{{attached.from,fromDistance},{attached.to,Length(attached.points)-fromDistance}};
            var previous=new Dictionary<string,Tuple<string,LabLeg>>();var visited=new HashSet<string>();
            while(true)
            {
                string node=null;float best=float.PositiveInfinity;
                foreach(var entry in distance)if(!visited.Contains(entry.Key)&&entry.Value<best){node=entry.Key;best=entry.Value;}
                if(node==null)return "NO_ROUTE";if(node==target)break;visited.Add(node);
                foreach(var leg in map.legs.Where(e=>e.valid))
                {
                    string next=leg.from==node?leg.to:leg.to==node?leg.from:null;
                    if(next==null||visited.Contains(next))continue;
                    float candidate=best+Length(leg.points);
                    if(distance.TryGetValue(next,out float known)&&known<=candidate)continue;
                    distance[next]=candidate;previous[next]=Tuple.Create(node,leg);
                }
            }
            var tail=new List<Tuple<string,LabLeg>>();string cursor=target;
            while(previous.TryGetValue(cursor,out var link)){tail.Add(Tuple.Create(cursor,link.Item2));cursor=link.Item1;}
            path.Add(current);path.Add(projection);
            if(cursor==attached.from){for(int i=segment-1;i>=0;i--)path.Add(attached.points[i]);}
            else {for(int i=segment;i<attached.points.Length;i++)path.Add(attached.points[i]);}
            tail.Reverse();
            foreach(var link in tail)
            {
                var points=link.Item2.points;
                if(link.Item1==link.Item2.to)path.AddRange(points);
                else for(int i=points.Length-1;i>=0;i--)path.Add(points[i]);
            }
            // The final target is its recorded standing/access point, never the marker mounted on a wall.
            return null;
        }
    }

    public sealed class LabExercise
    {
        public const string Product="LAB-PACKAGE-001";
        public readonly bool Transfer;
        public readonly string Destination;
        public bool DestinationOnly=>Destination!=null;
        public int Phase { get; private set; }
        public bool Complete=>Phase==2;
        public string Target=>Complete?null:Destination??(Phase==0?(Transfer?"B":null):(Transfer?"C":"B"));
        public LabExercise(bool transfer){Transfer=transfer;}
        public LabExercise(string destination){if(destination==null||destination.Length!=1||destination[0]<'A'||destination[0]>'E')throw new ArgumentException("Unknown test destination");Destination=destination;Phase=1;}
        public bool ConfirmArrival(bool atTarget){if(!DestinationOnly||Complete||!atTarget)return false;Phase=2;return true;}
        public string Scan(string code,bool atTarget)
        {
            if(Complete)return "ALREADY_COMPLETED";
            if(Target!=null&&!atTarget)return "NOT_AT_TARGET";
            string expected=Phase==0?Product:"JWM:MAIN:"+Target;
            if(code!=expected)return Phase==0?"WRONG_PRODUCT":"WRONG_DESTINATION";
            Phase++;return null;
        }
    }
}
