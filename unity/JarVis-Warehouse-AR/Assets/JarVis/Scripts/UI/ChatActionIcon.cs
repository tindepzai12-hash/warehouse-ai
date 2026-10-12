using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Font-independent vector strokes: no missing glyphs and no low-resolution icon textures.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChatActionIcon : MaskableGraphic
    {
        protected override void Awake(){base.Awake();useLegacyMeshGeneration=false;}
        public enum Symbol { Photo, Microphone, Speaker, Chevron, Stop, Close, Plus, SpeakerOff, Play, Pause, Send, Transcript, Retry, Trash }
        public Symbol symbol;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var bounds=rectTransform.rect;
            Vector2 Point(float x,float y)=>bounds.center+new Vector2(x,y)*Mathf.Min(bounds.width,bounds.height)/24f;
            void Line(float x1,float y1,float x2,float y2)
            {
                Vector2 a=Point(x1,y1),b=Point(x2,y2),n=new Vector2(-(b-a).y,(b-a).x).normalized*Mathf.Min(bounds.width,bounds.height)/24f;
                int index=vh.currentVertCount;
                vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);
            }
            void Arc(float x,float y,float radius,float from,float to)
            {
                for(int i=0;i<24;i++)
                {
                    float a=Mathf.Lerp(from,to,i/24f)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1)/24f)*Mathf.Deg2Rad;
                    Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);
                }
            }
            switch(symbol)
            {
                case Symbol.Send:Line(-7,0,8,0);Line(1,7,8,0);Line(8,0,1,-7);break;
                case Symbol.Transcript:Line(-7,8,7,8);Line(7,8,7,-8);Line(7,-8,-7,-8);Line(-7,-8,-7,8);Line(-3,4,3,4);Line(-3,0,3,0);Line(-3,-4,1,-4);break;
                case Symbol.Retry:Arc(0,0,7,45,330);Line(6,6,6,0);Line(6,6,0,6);break;
                case Symbol.Trash:Line(-7,5,7,5);Line(-3,8,3,8);Line(-3,8,-3,5);Line(3,8,3,5);Line(-5,5,-4,-8);Line(-4,-8,4,-8);Line(4,-8,5,5);Line(-2,2,-2,-5);Line(2,2,2,-5);break;
                case Symbol.Play:Line(-5,-8,7,0);Line(7,0,-5,8);Line(-5,8,-5,-8);break;
                case Symbol.Pause:Line(-4,-8,-4,8);Line(4,-8,4,8);break;
                case Symbol.Plus:Line(-8,0,8,0);Line(0,-8,0,8);break;
                case Symbol.Photo:
                    Line(-9,-8,9,-8);Line(9,-8,9,8);Line(9,8,-9,8);Line(-9,8,-9,-8);
                    Arc(-4,3,1.6f,0,360);Line(-8,-6,-2,0);Line(-2,0,2,-4);Line(2,-4,5,-1);Line(5,-1,9,-5);break;
                case Symbol.Microphone:
                    Line(-3,1,-3,6);Line(3,1,3,6);Arc(0,6,3,0,180);Arc(0,1,3,180,360);
                    Line(-7,2,-7,0);Line(7,2,7,0);Arc(0,0,7,180,360);Line(0,-7,0,-10);Line(-4,-10,4,-10);break;
                case Symbol.Speaker:
                case Symbol.SpeakerOff:
                    Line(-9,-3,-6,-3);Line(-6,-3,-1,-7);Line(-1,-7,-1,7);Line(-1,7,-6,3);Line(-6,3,-9,3);Line(-9,3,-9,-3);
                    if(symbol==Symbol.SpeakerOff){Line(3,-4,10,4);Line(3,4,10,-4);}else{Arc(-1,0,6,-55,55);Arc(-1,0,10,-55,55);}break;
                case Symbol.Chevron:Line(-3,7,4,0);Line(4,0,-3,-7);break;
                case Symbol.Stop:Line(-6,-6,6,-6);Line(6,-6,6,6);Line(6,6,-6,6);Line(-6,6,-6,-6);break;
                case Symbol.Close:Line(-6,-6,6,6);Line(-6,6,6,-6);break;
            }
        }
        public void Set(Symbol value){if(symbol==value)return;symbol=value;SetVerticesDirty();}
    }
}
