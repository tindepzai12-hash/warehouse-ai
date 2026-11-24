using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class RoomScanReticle : MaskableGraphic
    {
        public RoomScanState State;
        public float Progress,Phase;
        public bool ShowProgress;
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = rectTransform.rect;
            float left = rect.xMin,right = rect.xMax,bottom = rect.yMin,top = rect.yMax;
            if (State == RoomScanState.WarmingUp)
            {
                float shift = Mathf.Sin(Phase * 1.8f) * 24;
                float center = rect.center.x + shift,mid = rect.center.y;
                Quad(helper,new Rect(center - 26,mid - 45,3,90),color);
                Quad(helper,new Rect(center + 23,mid - 45,3,90),color);
                Quad(helper,new Rect(center - 26,mid - 45,52,3),color);
                Quad(helper,new Rect(center - 26,mid + 42,52,3),color);
                Quad(helper,new Rect(center - 7,mid - 35,14,2),color);
                Quad(helper,new Rect(rect.center.x - 88,mid - 1,24,2),color);
                Quad(helper,new Rect(rect.center.x + 64,mid - 1,24,2),color);
                Quad(helper,new Rect(left + 42,bottom + 26,rect.width - 84,2),new Color(color.r,color.g,color.b,0.25f));
                Quad(helper,new Rect(left + 42,bottom + 26,(rect.width - 84) * Mathf.Clamp01(Progress),2),color);
                return;
            }
            float length = 28,weight = 3;
            foreach (int horizontal in new[] { -1,1 })
            foreach (int vertical in new[] { -1,1 })
            {
                float x = horizontal < 0 ? left : right - length;
                float y = vertical < 0 ? bottom : top - weight;
                Quad(helper,new Rect(x,y,length,weight),color);
                x = horizontal < 0 ? left : right - weight;
                y = vertical < 0 ? bottom : top - length;
                Quad(helper,new Rect(x,y,weight,length),color);
            }
            if (State == RoomScanState.Searching || State == RoomScanState.Locating)
            {
                float y = Mathf.Lerp(bottom + 12,top - 12,(Mathf.Sin(Phase * 2.4f) + 1) * 0.5f);
                Quad(helper,new Rect(left + 10,y,rect.width - 20,1.5f),new Color(color.r,color.g,color.b,0.75f));
            }
            if (ShowProgress || State == RoomScanState.Locating || State == RoomScanState.Locked)
                Quad(helper,new Rect(left,bottom - 9,rect.width * Mathf.Clamp01(Progress),3),color);
        }
        private static void Quad(VertexHelper helper,Rect rect,Color tint)
        {
            int first = helper.currentVertCount;
            helper.AddVert(new Vector3(rect.xMin,rect.yMin),tint,Vector2.zero);
            helper.AddVert(new Vector3(rect.xMin,rect.yMax),tint,Vector2.zero);
            helper.AddVert(new Vector3(rect.xMax,rect.yMax),tint,Vector2.zero);
            helper.AddVert(new Vector3(rect.xMax,rect.yMin),tint,Vector2.zero);
            helper.AddTriangle(first,first + 1,first + 2); helper.AddTriangle(first,first + 2,first + 3);
        }
    }
}
