using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Navigation;
namespace JarVis.Warehouse.Preview
{
    // Projects the saved world-space A target; never a hard-coded screen position.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArSavedStartPin : MaskableGraphic
    {
        public SmallRoomAR Guide;
        public Camera SourceCamera;
        public Transform SavedTarget;
        public bool Visible {get;private set;}
        public void RefreshProjection(bool editorTrackingFixture=false)
        {
            bool fixture=Application.isEditor&&!Application.isPlaying&&editorTrackingFixture;
            bool valid=Guide!=null&&Guide.ReturningToStart&&(Guide.DeliveryTracking||fixture)&&SourceCamera!=null&&SavedTarget!=null;
            Vector3 v=valid?SourceCamera.WorldToViewportPoint(SavedTarget.position):Vector3.zero;
            Visible=valid&&v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1;
            canvasRenderer.SetAlpha(Visible?1:0);raycastTarget=false;
            if(!Visible)return;
            var parent=(RectTransform)rectTransform.parent;
            rectTransform.anchorMin=rectTransform.anchorMax=Vector2.zero;rectTransform.pivot=new Vector2(.5f,0);
            rectTransform.anchoredPosition=new Vector2(v.x*parent.rect.width,v.y*parent.rect.height);
        }
        void LateUpdate(){RefreshProjection();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var red=new Color32(255,51,65,255);const int count=48;
            for(int n=0;n<count;n++)
            {
                float a=n*Mathf.PI*2/count,b=(n+1)*Mathf.PI*2/count;int k=vh.currentVertCount;
                foreach(var p in new[]{new Vector2(Mathf.Cos(a)*13,29+Mathf.Sin(a)*13),new Vector2(Mathf.Cos(b)*13,29+Mathf.Sin(b)*13),new Vector2(Mathf.Cos(b)*5,29+Mathf.Sin(b)*5),new Vector2(Mathf.Cos(a)*5,29+Mathf.Sin(a)*5)})vh.AddVert(p,red,Vector2.zero);
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
            int t=vh.currentVertCount;vh.AddVert(new Vector2(-11,22),red,Vector2.zero);vh.AddVert(new Vector2(11,22),red,Vector2.zero);vh.AddVert(Vector2.zero,red,Vector2.zero);vh.AddTriangle(t,t+1,t+2);
        }
    }
}
