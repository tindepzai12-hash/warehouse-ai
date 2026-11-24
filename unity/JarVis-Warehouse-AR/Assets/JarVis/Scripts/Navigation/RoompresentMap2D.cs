using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.AI;

namespace JarVis.Warehouse.Navigation
{
    // Rectangular reference diagram; real landmark coordinates and NavMesh routes, not a wall survey or live user location.
    public static class RoompresentMap2D
    {
        const int Width=384,Height=512;
        public static string Normalize(string value)
        {
            var text=new StringBuilder();
            foreach(char c in (value??"").ToLowerInvariant().Normalize(NormalizationForm.FormD))
                if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)text.Append(c=='đ'?'d':c);
            return text.ToString();
        }
        public static bool IsRequest(string question,bool previouslySent=false)
        {
            string q=Normalize(question);
            return Regex.IsMatch(q,@"\b2\s*[- ]?\s*d\b|\bmap\b")||q.Contains("ban do")||q.Contains("so do")||
                previouslySent&&(q.Contains("gui lai anh")||q.Contains("gui lai hinh")||q.Contains("resend the image")||q.Contains("send it again")||q.Contains("show it again"));
        }
        // Packaged, reviewed routes stay viewable without tracking, NavMesh or a provider response.
        public static byte[] ReferenceBytes()=>Resources.Load<TextAsset>("RoompresentRoute2D")?.bytes;
        public static Texture2D LoadReference()
        {
            var bytes=ReferenceBytes();if(bytes==null)return null;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false){name="Saved Roompresent schematic",filterMode=FilterMode.Bilinear};
            if(ImageConversion.LoadImage(texture,bytes))return texture;
            Object.Destroy(texture);return null;
        }
        public static Texture2D Create(SmallRoomAR guide)
        {
            var mesh=NavMesh.CalculateTriangulation();
            if(guide==null||guide.mapRoot==null||mesh.vertices.Length==0)return null;
            var vertices=new Vector2[mesh.vertices.Length];
            Vector2 min=new Vector2(float.MaxValue,float.MaxValue),max=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<vertices.Length;i++)
            {
                var p=guide.mapRoot.InverseTransformPoint(mesh.vertices[i]);vertices[i]=new Vector2(p.x,p.z);
                min=Vector2.Min(min,vertices[i]);max=Vector2.Max(max,vertices[i]);
            }
            float scale=Mathf.Min(304/Mathf.Max(.1f,max.x-min.x),400/Mathf.Max(.1f,max.y-min.y));var center=(min+max)*.5f;
            Vector2 Project(Vector2 p)=>(p-center)*scale+new Vector2(Width*.5f,Height*.5f);
            Vector2 Point(Vector3 p)=>Project(new Vector2(p.x,p.z));
            var pixels=new Color32[Width*Height];var bg=new Color32(246,249,253,255);
            for(int i=0;i<pixels.Length;i++)pixels[i]=bg;
            void Put(int x,int y,Color32 c){if(x>=0&&x<Width&&y>=0&&y<Height)pixels[y*Width+x]=c;}
            void Disc(Vector2 p,int r,Color32 c)
            {for(int y=-r;y<=r;y++)for(int x=-r;x<=r;x++)if(x*x+y*y<=r*r)Put(Mathf.RoundToInt(p.x)+x,Mathf.RoundToInt(p.y)+y,c);}
            void Line(Vector2 a,Vector2 b,int r,Color32 c)
            {int n=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)));for(int i=0;i<=n;i++)Disc(Vector2.Lerp(a,b,i/(float)n),r,c);}
            // A clean schematic rectangle replaces the noisy scan footprint. It is not a physical wall outline.
            for(int y=16;y<Height-16;y++)for(int x=16;x<Width-16;x++)
                Put(x,y,x<18||x>=Width-18||y<18||y>=Height-18?new Color32(153,175,203,255):new Color32(229,237,247,255));
            float step=scale*.5f;var grid=new Color32(211,224,240,255);
            for(float x=(Width*.5f-center.x*scale)%step+step;x<Width-28;x+=step)if(x>28)Line(new Vector2(x,28),new Vector2(x,Height-28),0,grid);
            for(float y=(Height*.5f-center.y*scale)%step+step;y<Height-28;y+=step)if(y>28)Line(new Vector2(28,y),new Vector2(Width-28,y),0,grid);
            bool Path(Vector3 a,Vector3 b,Color32 color)
            {
                if(!NavMesh.SamplePosition(guide.mapRoot.TransformPoint(a),out var from,.85f,NavMesh.AllAreas)||!NavMesh.SamplePosition(guide.mapRoot.TransformPoint(b),out var to,.85f,NavMesh.AllAreas))return false;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
                for(int i=1;i<path.corners.Length;i++)
                {
                    var p=Point(guide.mapRoot.InverseTransformPoint(path.corners[i-1]));var q=Point(guide.mapRoot.InverseTransformPoint(path.corners[i]));
                    Line(p,q,5,new Color32(255,255,255,255));Line(p,q,2,color);
                    float length=Vector2.Distance(p,q);var d=(q-p).normalized;var side=new Vector2(-d.y,d.x);
                    for(float distance=45;distance<length-28;distance+=54)
                    {var tip=p+d*distance;Line(tip-d*7+side*5,tip,1,color);Line(tip-d*7-side*5,tip,1,color);}
                }
                return true;
            }
            // Show both possible first stops, not a fabricated straight line through furniture.
            Path(guide.basinAccess,guide.bagAccess,new Color32(146,165,187,255));
            Path(guide.startLocal,guide.basinAccess,new Color32(40,98,222,255));
            Path(guide.startLocal,guide.bagAccess,new Color32(8,146,136,255));
            void Label(Vector3 p,char letter,Color32 color)
            {
                Vector2 pos=Point(p);
                void Box(Vector2 center,int half,int radius,Color32 fill)
                {
                    for(int y=-half;y<=half;y++)for(int x=-half;x<=half;x++)
                    {float dx=Mathf.Max(Mathf.Abs(x)-(half-radius),0),dy=Mathf.Max(Mathf.Abs(y)-(half-radius),0);if(dx*dx+dy*dy<=radius*radius)Put((int)center.x+x,(int)center.y+y,fill);}
                }
                Box(pos+new Vector2(0,-3),24,7,new Color32(195,209,226,255));Box(pos,23,7,new Color32(255,255,255,255));Box(pos,19,5,color);
                var ink=letter=='A'?new Color32(87,58,13,255):new Color32(255,255,255,255);
                Vector2 P(float x,float y)=>pos+new Vector2(x,y);
                void Stroke(float x,float y,float u,float v)=>Line(P(x,y),P(u,v),2,ink);
                void Arc(float cx,float cy,float radius,float a,float b)
                {for(int i=0;i<20;i++){float u=Mathf.Lerp(a,b,i/20f)*Mathf.Deg2Rad,v=Mathf.Lerp(a,b,(i+1)/20f)*Mathf.Deg2Rad;Line(P(cx+Mathf.Cos(u)*radius,cy+Mathf.Sin(u)*radius),P(cx+Mathf.Cos(v)*radius,cy+Mathf.Sin(v)*radius),2,ink);}}
                if(letter=='A'){Stroke(-8,-10,0,11);Stroke(0,11,8,-10);Stroke(-5,-3,5,-3);}
                else if(letter=='B'){Stroke(-7,-10,-7,10);Stroke(-7,10,0,10);Stroke(-7,0,0,0);Stroke(-7,-10,0,-10);Arc(0,5,5,-90,90);Arc(0,-5,5,-90,90);}
                else Arc(1,0,10,50,310);
            }
            Label(guide.startLocal,'A',new Color32(255,203,100,255));
            Label(guide.basinAccess,'B',new Color32(40,98,222,255));
            Label(guide.bagAccess,'C',new Color32(8,146,136,255));
            var texture=new Texture2D(Width,Height,TextureFormat.RGBA32,false){name="Roompresent 2D schematic",filterMode=FilterMode.Bilinear};
            texture.SetPixels32(pixels);texture.Apply();return texture;
        }
    }
}
