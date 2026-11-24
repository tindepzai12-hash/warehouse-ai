using System.Collections.Generic;
using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public static class RoomRouteGeometry
    {
        public static WalkingRouteData GuidanceCopy(WalkingRouteData source)
        {
            WalkingRouteData result = JsonUtility.FromJson<WalkingRouteData>(JsonUtility.ToJson(source));
            if (source.points.Count <= 2) return result;
            bool[] keep = new bool[source.points.Count];
            keep[0] = keep[keep.Length - 1] = true;
            Stack<Vector2Int> ranges = new Stack<Vector2Int>();
            ranges.Push(new Vector2Int(0,keep.Length - 1));
            while (ranges.Count > 0)
            {
                Vector2Int range = ranges.Pop();
                float maximum = 0.12f;
                int selected = -1;
                for (int index = range.x + 1; index < range.y; index++)
                {
                    float distance = Distance(Point(source.points[index]),Point(source.points[range.x]),Point(source.points[range.y]));
                    if (distance > maximum) { maximum = distance; selected = index; }
                }
                if (selected < 0) continue;
                keep[selected] = true;
                ranges.Push(new Vector2Int(range.x,selected));
                ranges.Push(new Vector2Int(selected,range.y));
            }
            List<RoutePoint> cloned = result.points;
            result.points = new List<RoutePoint>();
            for (int index = 0; index < keep.Length; index++) if (keep[index]) result.points.Add(cloned[index]);
            return result;
        }
        public static Vector2 Point(RoutePoint point) => new Vector2(point.x,point.z);
        public static float Distance(Vector2 point,Vector2 start,Vector2 end) => Vector2.Distance(point,Closest(point,start,end));
        public static Vector2 Closest(Vector2 point,Vector2 start,Vector2 end)
        {
            Vector2 delta = end - start;
            return start + delta * (delta.sqrMagnitude < 0.000001f ? 0 : Mathf.Clamp01(Vector2.Dot(point - start,delta) / delta.sqrMagnitude));
        }
    }
}
