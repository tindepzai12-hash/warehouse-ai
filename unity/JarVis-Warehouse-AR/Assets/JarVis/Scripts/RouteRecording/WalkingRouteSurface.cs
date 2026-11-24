using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteSurface : MaskableGraphic
    {
        public float Radius = 16;
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(Radius,Mathf.Min(rect.width,rect.height) * 0.5f);
            vertices.AddVert(rect.center,color,Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= 8; step++)
                {
                    float angle = (90 - corner * 90 - step * 90f / 8) * Mathf.Deg2Rad;
                    vertices.AddVert(center + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * radius,color,Vector2.zero);
                }
            }
            for (int index = 1; index <= 36; index++) vertices.AddTriangle(0,index,index == 36 ? 1 : index + 1);
        }
    }
}
