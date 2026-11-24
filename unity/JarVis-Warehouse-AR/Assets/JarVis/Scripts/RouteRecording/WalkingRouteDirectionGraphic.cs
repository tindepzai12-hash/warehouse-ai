using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteDirectionGraphic : MaskableGraphic
    {
        public float Bearing;
        public static Vector2 TipDirection(float bearing)
        {
            return new Vector2(Mathf.Sin(bearing * Mathf.Deg2Rad), Mathf.Cos(bearing * Mathf.Deg2Rad));
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;
            Quaternion rotation = Quaternion.Euler(0, 0, -Bearing);
            Vector2[] shape =
            {
                new Vector2(0, height * 0.44f),
                new Vector2(-width * 0.4f, -height * 0.04f),
                new Vector2(-width * 0.14f, -height * 0.04f),
                new Vector2(width * 0.14f, -height * 0.04f),
                new Vector2(width * 0.4f, -height * 0.04f),
                new Vector2(-width * 0.14f, -height * 0.4f),
                new Vector2(width * 0.14f, -height * 0.4f)
            };
            foreach (Vector2 point in shape) vertices.AddVert(rotation * point, color, Vector2.zero);
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(0, 2, 3);
            vertices.AddTriangle(0, 3, 4);
            vertices.AddTriangle(2, 5, 6);
            vertices.AddTriangle(2, 6, 3);
        }
    }
}
