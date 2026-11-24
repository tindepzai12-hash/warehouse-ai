using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRoutePreview : MaskableGraphic
    {
        public WalkingRouteData Data;
        public WalkingRouteFollower Follower;
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (Data == null || Data.points == null || Data.points.Count < 2) return;
            Vector2 minimum = Vector2.zero, maximum = Vector2.zero;
            foreach (RoutePoint point in Data.points)
            {
                minimum = Vector2.Min(minimum,new Vector2(point.x,point.z));
                maximum = Vector2.Max(maximum,new Vector2(point.x,point.z));
            }
            float scale = Mathf.Min((rectTransform.rect.width - 24) / Mathf.Max(1,maximum.x - minimum.x),(rectTransform.rect.height - 24) / Mathf.Max(1,maximum.y - minimum.y));
            Vector2 center = (minimum + maximum) * 0.5f;
            int skip = Mathf.Max(1,Data.points.Count / 400);
            for (int index = 0; index < Data.points.Count - 1; index += skip)
            {
                RoutePoint from = Data.points[index], to = Data.points[Mathf.Min(index + skip,Data.points.Count - 1)];
                Vector2 first = (new Vector2(from.x,from.z) - center) * scale;
                Vector2 second = (new Vector2(to.x,to.z) - center) * scale;
                Vector2 normal = new Vector2(-(second - first).y,(second - first).x).normalized * 1.4f;
                int start = vertices.currentVertCount;
                Color tint = new Color(0.04f,0.31f,0.94f);
                vertices.AddVert(first - normal,tint,Vector2.zero);
                vertices.AddVert(first + normal,tint,Vector2.zero);
                vertices.AddVert(second + normal,tint,Vector2.zero);
                vertices.AddVert(second - normal,tint,Vector2.zero);
                vertices.AddTriangle(start,start + 1,start + 2);
                vertices.AddTriangle(start,start + 2,start + 3);
            }
            Marker(vertices,-center * scale,new Color(0.02f,0.3f,0.95f),3);
            RoutePoint end = Data.points[Data.points.Count - 1];
            Marker(vertices,(new Vector2(end.x,end.z) - center) * scale,new Color(1,0.35f,0.02f),4);
            if (Follower != null && (Follower.Active || Follower.Arrived))
            {
                Vector2 position = (new Vector2(Follower.LocalPosition.x,Follower.LocalPosition.z) - center) * scale;
                Marker(vertices,position,new Color(0,0.65f,0.36f),4);
                Vector2 forward = WalkingRouteDirectionGraphic.TipDirection(Follower.LocalYaw);
                Vector2 side = new Vector2(-forward.y,forward.x);
                int start = vertices.currentVertCount;
                Color tint = new Color(0,0.45f,0.24f);
                vertices.AddVert(position + forward * 10,tint,Vector2.zero);
                vertices.AddVert(position - forward * 2 + side * 5,tint,Vector2.zero);
                vertices.AddVert(position - forward * 2 - side * 5,tint,Vector2.zero);
                vertices.AddTriangle(start,start + 1,start + 2);
            }
        }
        private static void Marker(VertexHelper vertices,Vector2 point,Color tint,float radius)
        {
            int start = vertices.currentVertCount;
            vertices.AddVert(point + new Vector2(0,radius),tint,Vector2.zero);
            vertices.AddVert(point + new Vector2(-radius,0),tint,Vector2.zero);
            vertices.AddVert(point + new Vector2(0,-radius),tint,Vector2.zero);
            vertices.AddVert(point + new Vector2(radius,0),tint,Vector2.zero);
            vertices.AddTriangle(start,start + 1,start + 2);
            vertices.AddTriangle(start,start + 2,start + 3);
        }
    }
}
