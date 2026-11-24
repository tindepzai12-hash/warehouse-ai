using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AtlasAR
{
    [Serializable] public class AtlasMarker { public string id,label,source;public Vector3 position;public bool physicalVerified; }
    [Serializable] public class AtlasObstacle {public string id;public Vector3[] points;}
    [Serializable] public class AtlasMap
    {
        public string id,coordinateSystem;public int revision;public bool approvedLayout,physicalLocalizationVerified;
        public Vector3[] room;public AtlasObstacle[] obstacles;public AtlasMarker[] markers;public float[] worldToView;
        public static AtlasMap Load()
        {
            var asset=Resources.Load<TextAsset>("Atlas/Map");if(asset==null)throw new InvalidOperationException("Missing saved map");
            var result=JsonUtility.FromJson<AtlasMap>(asset.text);
            Validate(result);return result;
        }
        public static void Validate(AtlasMap result)
        {
            if(result==null||string.IsNullOrEmpty(result.id)||result.markers==null||result.markers.Length!=5||result.room==null||result.room.Length<3||result.worldToView==null||result.worldToView.Length!=9||result.obstacles==null)throw new InvalidOperationException("Invalid map");
            var unique=new HashSet<string>();foreach(var m in result.markers)if(m==null||m.id==null||m.id.Length!=1||m.id[0]<'A'||m.id[0]>'E'||!unique.Add(m.id)||!Finite(m.position))throw new InvalidOperationException("Invalid saved marker");
            foreach(var p in result.room)if(!Finite(p))throw new InvalidOperationException("Invalid room polygon");
            foreach(var x in result.worldToView)if(!float.IsFinite(x))throw new InvalidOperationException("Invalid map projection");
            foreach(var ob in result.obstacles){if(ob==null||ob.points==null||ob.points.Length<3)throw new InvalidOperationException("Invalid obstacle");foreach(var p in ob.points)if(!Finite(p))throw new InvalidOperationException("Invalid obstacle point");}
            if(Mathf.Abs(result.worldToView[0]*result.worldToView[4]-result.worldToView[1]*result.worldToView[3])<1e-6f)throw new InvalidOperationException("Singular map projection");
        }
        public static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        public AtlasMarker Marker(string id)=>Array.Find(markers,m=>m.id==id);
        // Rotates only the screen presentation: A is top-right, B bottom-right. Metric data are unchanged.
        public Vector2 Portrait(Vector3 p,bool landscape=false)
        {
            float u=worldToView[0]*p.x+worldToView[1]*p.z+worldToView[2],v=worldToView[3]*p.x+worldToView[4]*p.z+worldToView[5];return landscape?new Vector2(u,-v):new Vector2(-v,-u);
        }
        public Vector2 Project(Vector3 p,Rect rect,bool landscape=false)
        {
            Bounds2(out var lo,out var hi,landscape);float s=Mathf.Min((rect.width-48)/(hi.x-lo.x),(rect.height-48)/(hi.y-lo.y));return rect.center+(Portrait(p,landscape)-(lo+hi)*.5f)*s;
        }
        void Bounds2(out Vector2 lo,out Vector2 hi,bool landscape)
        {
            lo=Vector2.one*float.MaxValue;hi=Vector2.one*float.MinValue;foreach(var p in room){var q=Portrait(p,landscape);lo=Vector2.Min(lo,q);hi=Vector2.Max(hi,q);}
        }
        public bool InRoom(Vector3 p,float margin=.65f)
        {
            bool inside=false;float closest=float.MaxValue;
            for(int i=0,j=room.Length-1;i<room.Length;j=i++)
            {
                var a=new Vector2(room[i].x,room[i].z);var b=new Vector2(room[j].x,room[j].z);var q=new Vector2(p.x,p.z);
                if((a.y>q.y)!=(b.y>q.y)&&q.x<(b.x-a.x)*(q.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                var ab=b-a;float t=ab.sqrMagnitude<1e-10f?0:Mathf.Clamp01(Vector2.Dot(q-a,ab)/ab.sqrMagnitude);closest=Mathf.Min(closest,Vector2.Distance(q,a+t*ab));
            }
            return inside||closest<=margin;
        }
    }
    public sealed class AtlasMapGraphic:MaskableGraphic
    {
        public AtlasMap map;public bool userValid,landscape;public Vector3 userPosition,userForward;public string selected="A";
        public Vector3[] route=Array.Empty<Vector3>();
        public Vector2 Local(Vector3 p)=>map.Project(p,rectTransform.rect,landscape);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(map==null)return;Polygon(vh,map.room,new Color32(255,255,255,255));
            for(int i=0;i<map.room.Length;i++)Line(vh,Local(map.room[i]),Local(map.room[(i+1)%map.room.Length]),3,new Color32(81,102,126,255));
            foreach(var ob in map.obstacles)Polygon(vh,ob.points,new Color32(207,218,231,255));
            if(userValid&&route!=null)for(int i=0;i<route.Length-1;i++)Line(vh,Local(route[i]),Local(route[i+1]),4,new Color32(22,91,232,255));
            if(userValid)
            {
                var p=Local(userPosition);Disc(vh,p,11,new Color32(255,255,255,255));Disc(vh,p,8,new Color32(15,117,224,255));
                var delta=Local(userPosition+userForward)-p;if(delta.sqrMagnitude>1)
                {
                    var d=delta.normalized;var r=new Vector2(d.y,-d.x);Triangle(vh,p+d*20,p+d*10+r*6,p+d*10-r*6,new Color32(15,117,224,255));
                }
            }
        }
        void Polygon(VertexHelper vh,Vector3[] points,Color c)
        {
            var p=new List<Vector2>();foreach(var v in points)p.Add(Local(v));
            float area=0;for(int i=0;i<p.Count;i++){var a=p[i];var b=p[(i+1)%p.Count];area+=a.x*b.y-b.x*a.y;}if(area<0)p.Reverse();
            int guard=1000;while(p.Count>2&&guard-->0)
            {
                bool clipped=false;for(int i=0;i<p.Count;i++)
                {
                    int a=(i+p.Count-1)%p.Count,b=i,cidx=(i+1)%p.Count;var ab=p[b]-p[a];var ac=p[cidx]-p[a];if(Cross(ab,ac)<=0)continue;
                    bool contains=false;for(int j=0;j<p.Count;j++)if(j!=a&&j!=b&&j!=cidx&&Inside(p[j],p[a],p[b],p[cidx])){contains=true;break;}
                    if(contains)continue;Triangle(vh,p[a],p[b],p[cidx],c);p.RemoveAt(b);clipped=true;break;
                }
                if(!clipped)break;
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        static bool Inside(Vector2 p,Vector2 a,Vector2 b,Vector2 c)=>Cross(b-a,p-a)>=0&&Cross(c-b,p-b)>=0&&Cross(a-c,p-c)>=0;
        static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color color){int i=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);}
        static void Disc(VertexHelper vh,Vector2 p,float r,Color c){for(int i=0;i<24;i++){float a=i*Mathf.PI/12,b=(i+1)*Mathf.PI/12;Triangle(vh,p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,c);}}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c){var v=b-a;var n=new Vector2(-v.y,v.x).normalized*width*.5f;Triangle(vh,a-n,a+n,b+n,c);Triangle(vh,a-n,b+n,b-n,c);}
    }
    public static class AtlasPoseMath
    {
        public static bool Finite(Quaternion q)=>float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w)&&Mathf.Abs(q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w-1)<.02f;
        public static bool TryMapFrame(Pose arCamera,Pose mapCamera,out Pose frame)
        {
            frame=Pose.identity;if(!AtlasMap.Finite(arCamera.position)||!AtlasMap.Finite(mapCamera.position)||!Finite(arCamera.rotation)||!Finite(mapCamera.rotation))return false;
            Quaternion q=arCamera.rotation*Quaternion.Inverse(mapCamera.rotation);var flat=Vector3.ProjectOnPlane(q*Vector3.forward,Vector3.up);
            if(flat.sqrMagnitude<.5f)return false;var yaw=Quaternion.LookRotation(flat.normalized,Vector3.up);
            if(Quaternion.Angle(yaw,q)>12)return false;frame=new Pose(arCamera.position-yaw*mapCamera.position,yaw);return true;
        }
        public static Vector3 ToMap(Pose frame,Vector3 world)=>Quaternion.Inverse(frame.rotation)*(world-frame.position);
    }
}
