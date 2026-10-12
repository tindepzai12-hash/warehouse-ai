using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Navigation
{
    public sealed class WarehouseRouteGraphic : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float lineWidth = 10f;
        private Route route;

        public void SetRoute(Route value)
        {
            route = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (route == null || !route.IsValid)
                return;

            Rect area = rectTransform.rect;
            GetBounds(route, out float minX, out float maxX, out float minZ, out float maxZ);
            Vector2 previous = Map(route.waypoints[0], area, minX, maxX, minZ, maxZ);
            for (int index = 1; index < route.waypoints.Count; index++)
            {
                Vector2 next = Map(route.waypoints[index], area, minX, maxX, minZ, maxZ);
                AddLine(vertexHelper, previous, next, lineWidth, color);
                previous = next;
            }
        }

        private static void GetBounds(
            Route value,
            out float minX,
            out float maxX,
            out float minZ,
            out float maxZ
        )
        {
            minX = maxX = value.waypoints[0].x;
            minZ = maxZ = value.waypoints[0].z;
            foreach (Waypoint waypoint in value.waypoints)
            {
                minX = Mathf.Min(minX, waypoint.x);
                maxX = Mathf.Max(maxX, waypoint.x);
                minZ = Mathf.Min(minZ, waypoint.z);
                maxZ = Mathf.Max(maxZ, waypoint.z);
            }
        }

        private static Vector2 Map(
            Waypoint waypoint,
            Rect area,
            float minX,
            float maxX,
            float minZ,
            float maxZ
        )
        {
            float normalizedX = Mathf.InverseLerp(minX - 1f, maxX + 1f, waypoint.x);
            float normalizedY = Mathf.InverseLerp(minZ - 1f, maxZ + 1f, waypoint.z);
            return new Vector2(
                Mathf.Lerp(area.xMin + 20f, area.xMax - 20f, normalizedX),
                Mathf.Lerp(area.yMin + 20f, area.yMax - 20f, normalizedY)
            );
        }

        private static void AddLine(
            VertexHelper vertexHelper,
            Vector2 start,
            Vector2 end,
            float width,
            Color lineColor
        )
        {
            Vector2 direction = (end - start).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
            int startIndex = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = lineColor;

            vertex.position = start - normal;
            vertexHelper.AddVert(vertex);
            vertex.position = start + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end + normal;
            vertexHelper.AddVert(vertex);
            vertex.position = end - normal;
            vertexHelper.AddVert(vertex);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }
    }
}
