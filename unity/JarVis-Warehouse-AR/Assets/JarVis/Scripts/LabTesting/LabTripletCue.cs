using UnityEngine;
namespace JarVis.Warehouse.Lab
{
    public static class LabChevronShape
    {
        public static readonly Vector2[] Points={new Vector2(-.46f,-.10f),new Vector2(-.035f,.285f),new Vector2(.035f,.285f),new Vector2(.46f,-.10f),new Vector2(.46f,-.15f),new Vector2(.33f,-.29f),new Vector2(.285f,-.29f),new Vector2(0,-.035f),new Vector2(-.285f,-.29f),new Vector2(-.33f,-.29f),new Vector2(-.46f,-.15f)};
        // Ear clipping at initialization, shared by world and HUD geometry.
        public static readonly int[] Triangles=Triangulate();
        public static readonly Vector2[] Outline=OffsetOutline(.035f);
        static Vector2[] OffsetOutline(float distance)
        {
            var result=new Vector2[Points.Length];
            for(int i=0;i<Points.Length;i++)
            {
                var previous=(Points[i]-Points[(i+Points.Length-1)%Points.Length]).normalized;
                var next=(Points[(i+1)%Points.Length]-Points[i]).normalized;
                var a=new Vector2(-previous.y,previous.x);var b=new Vector2(-next.y,next.x);
                var m=(a+b).normalized;float denominator=Vector2.Dot(m,b);
                result[i]=Points[i]+m*(distance/Mathf.Max(.25f,denominator));
            }
            return result;
        }
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        static int[] Triangulate()
        {
            var ids=new System.Collections.Generic.List<int>();for(int i=Points.Length-1;i>=0;i--)ids.Add(i);
            var tris=new System.Collections.Generic.List<int>();int guard=0;
            while(ids.Count>2&&guard++<128)
            {
                bool removed=false;
                for(int k=0;k<ids.Count;k++)
                {
                    int a=ids[(k+ids.Count-1)%ids.Count],b=ids[k],c=ids[(k+1)%ids.Count];
                    if(Cross(Points[a],Points[b],Points[c])<=.000001f)continue;
                    bool inside=false;
                    foreach(int j in ids)if(j!=a&&j!=b&&j!=c&&Cross(Points[a],Points[b],Points[j])>=0&&Cross(Points[b],Points[c],Points[j])>=0&&Cross(Points[c],Points[a],Points[j])>=0){inside=true;break;}
                    if(inside)continue;tris.Add(a);tris.Add(b);tris.Add(c);ids.RemoveAt(k);removed=true;break;
                }
                if(!removed)throw new System.InvalidOperationException("Invalid chevron contour");
            }
            return tris.ToArray();
        }
    }
    public sealed class LabTripletCue:MonoBehaviour
    {
        readonly Transform[] parts=new Transform[3];readonly MeshRenderer[] faces=new MeshRenderer[3];
        Material white,blue;Mesh mesh;MaterialPropertyBlock pulse;
        public void Initialize(Material source)
        {
            white=new Material(source);white.SetColor("_BaseColor",Color.white);blue=new Material(source);blue.SetColor("_BaseColor",new Color(.065f,.22f,.68f));pulse=new MaterialPropertyBlock();
            mesh=new Mesh{name="JarVis bevel chevron"};var vertices=new Vector3[LabChevronShape.Points.Length];for(int i=0;i<vertices.Length;i++)vertices[i]=LabChevronShape.Points[i];mesh.vertices=vertices;
            var t=LabChevronShape.Triangles;var both=new int[t.Length*2];for(int i=0;i<t.Length;i+=3){both[i]=t[i];both[i+1]=t[i+1];both[i+2]=t[i+2];both[t.Length+i]=t[i+2];both[t.Length+i+1]=t[i+1];both[t.Length+i+2]=t[i];}mesh.triangles=both;mesh.RecalculateBounds();mesh.RecalculateNormals();
            for(int i=0;i<3;i++){parts[i]=new GameObject("Chevron "+i).transform;parts[i].SetParent(transform,false);Make(parts[i],"Blue edge",blue,1.10f,.018f);faces[i]=Make(parts[i],"White face",white,1,0);}
        }
        MeshRenderer Make(Transform parent,string name,Material material,float scale,float z)
        {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localScale=Vector3.one*scale;go.transform.localPosition=Vector3.forward*z;go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;return r;}
        public void Show(Camera camera,Vector3 position,float bearing,bool turning)
        {
            transform.position=position;transform.rotation=camera.transform.rotation*Quaternion.Euler(0,0,-bearing);
            float depth=Vector3.Dot(position-camera.transform.position,camera.transform.forward);
            float worldHeight=2*Mathf.Max(.05f,depth)*Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad)*(128f/412)*camera.aspect;
            transform.localScale=Vector3.one*(worldHeight/1.4f);
            for(int i=0;i<3;i++)
            {parts[i].localPosition=new Vector3(0,(i-1)*.39f,0);float light=.94f+.06f*Mathf.Sin(Time.unscaledTime*5.2f-i*1.4f);pulse.SetColor("_BaseColor",new Color(light,light,1));faces[i].SetPropertyBlock(pulse);}
        }
        void OnDestroy(){if(Application.isPlaying){Destroy(white);Destroy(blue);Destroy(mesh);}else{DestroyImmediate(white);DestroyImmediate(blue);DestroyImmediate(mesh);}}
    }
}
