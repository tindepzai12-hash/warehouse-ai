using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Demo2026
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JarVisMapGraphic : MaskableGraphic
    {
        // Read-only copy of the owner's checked map-space points. No registration is overwritten.
        static readonly Vector2[] Checked = { new Vector2(1.6321806f,-3.2412338f), new Vector2(-2.0507436f,1.8392259f), new Vector2(-.7009451f,2.8860228f) };
        public static Vector2 Point(int index, float w, float h)
        {
            var v = Checked[index]; return new Vector2(Mathf.Lerp(34,w-34,(v.x+2.0507436f)/3.6829242f),Mathf.Lerp(h-64,66,(v.y+3.2412338f)/6.1272566f));
        }
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            Vector2 P(Vector2 p) => new Vector2(r.xMin+p.x,r.yMax-p.y);
            void Line(Vector2 a,Vector2 b,float t,Color c)
            {
                a=P(a);b=P(b);var n=new Vector2(-(b-a).y,(b-a).x).normalized*t*.5f;int i=vh.currentVertCount;
                vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
            float w=r.width,h=r.height;var grid=new Color(.83f,.87f,.92f,.6f);
            for(float x=18;x<w;x+=28)Line(new Vector2(x,48),new Vector2(x,h-38),.6f,grid);
            for(float y=48;y<h-38;y+=28)Line(new Vector2(18,y),new Vector2(w-18,y),.6f,grid);
            Line(new Vector2(18,48),new Vector2(w-18,48),2,grid);Line(new Vector2(w-18,48),new Vector2(w-18,h-38),2,grid);Line(new Vector2(w-18,h-38),new Vector2(18,h-38),2,grid);Line(new Vector2(18,h-38),new Vector2(18,48),2,grid);
            var a=Point(0,w,h);var b=Point(1,w,h);var c=Point(2,w,h);
            Line(a,b,4,JarVisVisuals.Hex("4a7bf2"));Line(a,c,4,JarVisVisuals.Hex("00a78c"));Line(b,c,3,JarVisVisuals.Hex("9cadc8"));
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JarVisPinGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;Vector2 center=new Vector2(r.center.x,r.yMax-r.width*.5f);float outer=r.width*.47f,inner=outer*.36f;
            void Triangle(Vector2 a,Vector2 b,Vector2 c,Color col){int i=vh.currentVertCount;vh.AddVert(a,col,Vector2.zero);vh.AddVert(b,col,Vector2.zero);vh.AddVert(c,col,Vector2.zero);vh.AddTriangle(i,i+1,i+2);}
            for(int i=0;i<64;i++){float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;Vector2 va=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),vb=new Vector2(Mathf.Cos(b),Mathf.Sin(b));Triangle(center+va*inner,center+va*outer,center+vb*outer,color);Triangle(center+va*inner,center+vb*outer,center+vb*inner,color);}
            Triangle(center+new Vector2(-outer*.76f,-outer*.5f),new Vector2(r.center.x,r.yMin+1),center+new Vector2(outer*.76f,-outer*.5f),color);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JarVisRouteGraphic : MaskableGraphic
    {
        public bool Returning,ToC;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;Vector2 P(float x,float y)=>new Vector2(r.xMin+x*r.width,r.yMin+y*r.height);
            void L(Vector2 a,Vector2 b,float t){var n=new Vector2(-(b-a).y,(b-a).x).normalized*t*.5f;int i=vh.currentVertCount;vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);}
            float top=Returning?.15f:ToC?.8f:.34f;
            Vector2 left=P(.25f,0),right=P(.75f,0),tl=P(top-.035f,1),tr=P(top+.035f,1);
            L(left,tl,1.2f);L(right,tr,1.2f);
            for(int i=0;i<8;i++){float t=(i+.5f)/8;Vector2 c=Vector2.Lerp(P(.5f,0),P(top,1),t);float w=Mathf.Lerp(24,4,t),h=w*.7f;L(c+new Vector2(-w,-h),c,Mathf.Lerp(5,2,t));L(c,c+new Vector2(w,-h),Mathf.Lerp(5,2,t));}
        }
    }
}
