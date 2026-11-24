using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class RoomRouteDirectionGlyph : MaskableGraphic
    {
        public float Degrees;

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width,rect.height) * 0.45f;
            Quaternion rotation = Quaternion.Euler(0,0,-Degrees);
            Vector2[] points = { new Vector2(0,1),new Vector2(-0.8f,0),new Vector2(-0.27f,0.08f),new Vector2(-0.27f,-0.85f),new Vector2(0.27f,-0.85f),new Vector2(0.27f,0.08f),new Vector2(0.8f,0) };
            foreach (Vector2 point in points)
            {
                Vector3 vertex = rotation * (Vector3)(point * scale);
                helper.AddVert((Vector3)rect.center + vertex,color,Vector2.zero);
            }
            helper.AddTriangle(0,1,2);
            helper.AddTriangle(0,2,5);
            helper.AddTriangle(0,5,6);
            helper.AddTriangle(2,3,4);
            helper.AddTriangle(2,4,5);
        }
    }
}
