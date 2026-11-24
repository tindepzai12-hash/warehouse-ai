using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoompresentCodeFrame : MaskableGraphic
    {
        public bool Barcode {get;private set;}
        float phase=.5f;
        bool sweeping=true;
        void Update()
        {
            var scanner=GetComponentInParent<ImportedCameraScanner>();
            sweeping=scanner==null||scanner.State==ScannerState.Scanning||scanner.State==ScannerState.Decoding;
            phase=PrototypeUi.ReducedMotion?.5f:.5f-.5f*Mathf.Cos(Time.unscaledTime*Mathf.PI/.95f);SetVerticesDirty();
        }
        public void SetBarcode(bool value)
        {
            var size=value?new Vector2(300,132):new Vector2(236,236);
            if(Barcode==value&&rectTransform.sizeDelta==size)return;
            Barcode=value;rectTransform.sizeDelta=size;
            color=PrototypeUi.Hex(value?"68e6bb":"80c5ff");raycastTarget=false;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;const float t=3,l=25;
            void Box(float x,float y,float w,float h)
            {
                int n=vh.currentVertCount;
                vh.AddVert(new Vector3(x,y),color,Vector2.zero);vh.AddVert(new Vector3(x+w,y),color,Vector2.zero);
                vh.AddVert(new Vector3(x+w,y+h),color,Vector2.zero);vh.AddVert(new Vector3(x,y+h),color,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            foreach(bool right in new[]{false,true})foreach(bool top in new[]{false,true})
            {
                float x=right?r.xMax-l:r.xMin,y=top?r.yMax-t:r.yMin;
                Box(x,y,l,t);Box(right?r.xMax-t:r.xMin,top?r.yMax-l:r.yMin,t,l);
            }
            if(sweeping)DrawSweep(vh,r,color,phase,PrototypeUi.ReducedMotion);
        }
        public static void DrawSweep(VertexHelper vh,Rect r,Color tint,float position,bool still)
        {
            float inset=10,x=r.xMin+inset,w=Mathf.Max(0,r.width-2*inset),y=Mathf.Lerp(r.yMax-inset-10,r.yMin+inset+10,position);
            void Band(float at,float height,float alpha)
            {
                int n=vh.currentVertCount;var c=tint;c.a=alpha;
                vh.AddVert(new Vector3(x,at-height/2),c,Vector2.zero);vh.AddVert(new Vector3(x+w,at-height/2),c,Vector2.zero);vh.AddVert(new Vector3(x+w,at+height/2),c,Vector2.zero);vh.AddVert(new Vector3(x,at+height/2),c,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
            if(!still){Band(y,18,.025f);Band(y,10,.055f);Band(y,5,.14f);}
            Band(y,2,still?.4f:.92f);
            if(!still)for(int i=0;i<5;i++)Band(Mathf.Clamp(y+4+i*3,r.yMin+inset,r.yMax-inset),3,.075f*(1-i/5f));
        }
    }
}
