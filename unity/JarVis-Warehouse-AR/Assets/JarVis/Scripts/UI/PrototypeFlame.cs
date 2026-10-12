using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PrototypeFlame:MaskableGraphic
    {
        protected override void Awake(){base.Awake();useLegacyMeshGeneration=false;}
        public float PreviewTime=-1;
        float last;
        void Update(){if(Time.unscaledTime-last<1f/30)return;last=Time.unscaledTime;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();float time=PrototypeUi.ReducedMotion?0:PreviewTime>=0?PreviewTime:Time.unscaledTime;
            float phase=(Mathf.Sin(time*7)+1)*.5f;var rect=rectTransform.rect;
            Vector2 Point(Vector2 p)=>new Vector2(rect.xMin+p.x/32*rect.width,rect.yMax-p.y/40*rect.height);
            var outline=new List<Vector2>();Vector2 current=Vector2.Lerp(new Vector2(17,1),new Vector2(20,3),phase);outline.Add(current);
            void Curve(Vector2 a,Vector2 b,Vector2 end){var start=current;for(int i=1;i<=8;i++){float t=i/8f,n=1-t;outline.Add(n*n*n*start+3*n*n*t*a+3*n*t*t*b+t*t*t*end);}current=end;}
            Curve(new Vector2(20-phase*5,12),new Vector2(27+phase*3,12+phase*5),new Vector2(27-phase,23+phase*3));
            Curve(new Vector2(27,32),new Vector2(22,38),new Vector2(16,38));Curve(new Vector2(7,38),new Vector2(3,32),new Vector2(3+phase,25-phase));
            Curve(new Vector2(3+phase*2,17+phase),new Vector2(9-phase*2,13+phase*5),new Vector2(10-phase*2,8+phase*3));
            Curve(new Vector2(13+phase,13+phase*3),new Vector2(13,16+phase*5),new Vector2(13,18+phase*4));
            Curve(new Vector2(18+phase*4,13+phase*4),new Vector2(17+phase*5,7+phase),outline[0]);
            void Fan(List<Vector2> points,Vector2 center,Color bottom,Color top){int root=mesh.currentVertCount;mesh.AddVert(Point(center),bottom,Vector2.zero);foreach(var p in points)mesh.AddVert(Point(p),Color.Lerp(top,bottom,p.y/40),Vector2.zero);for(int i=1;i<points.Count;i++)mesh.AddTriangle(root,root+i,root+i+1);mesh.AddTriangle(root,root+points.Count,root+1);}
            Fan(outline,new Vector2(16,27),PrototypeUi.Hex("ffb000"),PrototypeUi.Hex("f54d12"));
            outline=new List<Vector2>();current=new Vector2(17+phase,19+phase*2);outline.Add(current);
            Curve(new Vector2(17,25),new Vector2(23,26+phase),new Vector2(22,32));Curve(new Vector2(21,37),new Vector2(16,39),new Vector2(12,35));
            Curve(new Vector2(9,30),new Vector2(13,27),new Vector2(14,25));Curve(new Vector2(15,25),new Vector2(16,22),outline[0]);
            Fan(outline,new Vector2(16,31),PrototypeUi.Hex("fff0b1"),PrototypeUi.Hex("ffd75b"));
            if(!PrototypeUi.ReducedMotion)for(int e=0;e<2;e++){float t=Mathf.Repeat(time/1.45f+e*.5f,1);Color ember=PrototypeUi.Hex("ffb02a");ember.a=Mathf.Sin(t*Mathf.PI)*.75f;var p=Point(new Vector2(9+e*14+t*3,14-t*18));float radius=rect.width/32*(1-t);int n=mesh.currentVertCount;mesh.AddVert(p+new Vector2(-radius,-radius),ember,Vector2.zero);mesh.AddVert(p+new Vector2(-radius,radius),ember,Vector2.zero);mesh.AddVert(p+new Vector2(radius,radius),ember,Vector2.zero);mesh.AddVert(p+new Vector2(radius,-radius),ember,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3);}
        }
    }
}
