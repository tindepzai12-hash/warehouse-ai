using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Small interface symbols remain sharp at every Canvas scale; brand art stays imported.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ImportedUtilityIcon : MaskableGraphic
    {
        public string symbol;
        public float strokeWidth = 1.7f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float scale=Mathf.Min(r.width,r.height)/24f;
            Vector2 P(float x,float y)=>r.center+new Vector2(x-12,12-y)*scale;
            void L(float x,float y,float a,float b)
            {
                var p=P(x,y);var q=P(a,b);var n=new Vector2(-(q-p).y,(q-p).x).normalized*scale*Mathf.Clamp(strokeWidth,1,3)/2;
                int i=vh.currentVertCount;vh.AddVert(p-n,color,Vector2.zero);vh.AddVert(p+n,color,Vector2.zero);vh.AddVert(q+n,color,Vector2.zero);vh.AddVert(q-n,color,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
            void C(float x,float y,float radius){for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;L(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);}}
            void R(float x,float y,float w,float h){L(x,y,x+w,y);L(x+w,y,x+w,y+h);L(x+w,y+h,x,y+h);L(x,y+h,x,y);}
            void A(float x,float y,float radius,float start,float end){for(int i=0;i<16;i++){float a=Mathf.Lerp(start,end,i/16f)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(i+1)/16f)*Mathf.Deg2Rad;L(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius);}}
            switch(symbol)
            {
                case "qr-code": L(3,8,3,3);L(3,3,8,3);L(16,3,21,3);L(21,3,21,8);L(21,16,21,21);L(21,21,16,21);L(8,21,3,21);L(3,21,3,16);R(7,7,3,3);R(14,7,3,3);R(7,14,3,3);L(14,14,17,14);L(17,14,17,17);L(14,17,14,18);break;
                case "bell": A(12,9,6,180,360);L(6,9,6,14);L(6,14,4,17);A(5,17,1,90,180);L(5,18,19,18);A(19,17,1,0,90);L(20,17,18,14);L(18,14,18,9);A(12,20,2,0,180);L(12,1,12,3);break;
                case "power": A(12,13,8,220,500);L(12,2,12,12);break;
                case "clipboard-check": R(5,5,14,17);R(9,2,6,5);L(8,13,11,16);L(11,16,16,10);break;
                case "file-text": L(5,2,15,2);L(15,2,20,7);L(20,7,20,22);L(20,22,5,22);L(5,22,5,2);L(15,2,15,7);L(15,7,20,7);L(8,12,17,12);L(8,16,17,16);break;
                case "flame": L(12,2,8,9);L(8,9,6,7);L(6,7,3,14);A(12,14,9,0,180);L(21,14,19,8);L(19,8,16,11);L(16,11,12,2);L(12,12,9,17);A(12,17,3,0,180);L(15,17,12,12);break;
                case "home": L(3,10,12,3);L(12,3,21,10);L(5,9,5,21);L(5,21,19,21);L(19,21,19,9);R(9,14,6,7);break;
                case "tasks": case "list-check": L(4,5,16,5);L(4,11,16,11);L(4,17,11,17);L(15,17,18,20);L(18,20,22,15);break;
                case "map": L(3,5,9,3);L(9,3,15,6);L(15,6,21,3);L(21,3,21,19);L(21,19,15,22);L(15,22,9,19);L(9,19,3,21);L(3,21,3,5);L(9,3,9,19);L(15,6,15,22);break;
                case "jarvis": case "message-square": R(3,4,18,14);L(3,18,3,22);L(3,22,8,18);break;
                case "search": C(10,10,6);L(15,15,21,21);break;
                case "filter": L(3,5,21,5);L(21,5,14,12);L(14,12,14,20);L(14,20,10,18);L(10,18,10,12);L(10,12,3,5);break;
                case "package": L(3,7,12,2);L(12,2,21,7);L(21,7,12,12);L(12,12,3,7);L(3,7,3,17);L(3,17,12,22);L(12,22,21,17);L(21,17,21,7);L(12,12,12,22);L(7,5,16,10);break;
                case "alert-circle": C(12,12,9);L(12,6,12,13);L(12,17,12,18);break;
                case "alert-triangle": L(12,3,2,21);L(2,21,22,21);L(22,21,12,3);L(12,9,12,14);L(12,17,12,18);break;
                case "user": C(12,7,4);L(4,21,4,17);L(4,17,8,13);L(8,13,16,13);L(16,13,20,17);L(20,17,20,21);break;
                case "lock": L(7,10,17,10);A(17,12,2,270,360);L(19,12,19,19);A(17,19,2,0,90);L(17,21,7,21);A(7,19,2,90,180);L(5,19,5,12);A(7,12,2,180,270);L(8,10,8,7);A(12,7,4,180,360);L(16,7,16,10);C(12,15,1);L(12,16,12,18);break;
                case "chevron-right": L(9,5,16,12);L(16,12,9,19);break;
                case "chevron-down": L(5,9,12,16);L(12,16,19,9);break;
                case "arrow-left": L(20,12,4,12);L(4,12,11,5);L(4,12,11,19);break;
                case "arrow-right": L(4,12,20,12);L(20,12,13,5);L(20,12,13,19);break;
                case "menu": L(3,5,21,5);L(3,12,21,12);L(3,19,21,19);break;
                case "plus": L(4,12,20,12);L(12,4,12,20);break;
                case "minus": L(4,12,20,12);break;
                case "x": L(5,5,19,19);L(5,19,19,5);break;
                case "calendar": R(3,5,18,16);L(3,10,21,10);L(7,2,7,8);L(17,2,17,8);break;
                case "scan-barcode": L(3,8,3,3);L(3,3,8,3);L(16,3,21,3);L(21,3,21,8);L(21,16,21,21);L(21,21,16,21);L(8,21,3,21);L(3,21,3,16);L(8,7,8,17);L(12,6,12,18);L(16,7,16,17);break;
                case "camera": R(2,6,20,15);C(12,13,4);L(6,6,8,3);L(8,3,16,3);L(16,3,18,6);break;
                case "check": L(4,12,9,17);L(9,17,20,6);break;
                case "eye": C(12,12,3);L(2,12,6,7);L(6,7,12,5);L(12,5,18,7);L(18,7,22,12);L(22,12,18,17);L(18,17,12,19);L(12,19,6,17);L(6,17,2,12);break;
            }
        }
    }
}

