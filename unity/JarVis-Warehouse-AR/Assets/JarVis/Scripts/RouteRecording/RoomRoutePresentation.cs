using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public static class RoomRoutePresentation
    {
        public const float Spacing = 2.2f;
        public static int Budget(float remaining) => !Finite(remaining) || remaining <= 2 ? 0 : remaining <= 8 ? 1 : 2;
        public static float First(float travelled) => 1.5f + Mathf.Max(0,Mathf.Ceil((travelled + 1.2f - 1.5f) / Spacing)) * Spacing;
        public static float MarkerScale(float depth,float fieldOfView,float baseHeight,float fraction) => !Finite(depth) || depth <= 0 ? 0 : Mathf.Clamp(depth * 2 * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f) * fraction / baseHeight,0.001f,3);
        public static float ArrowScale(float depth,float fieldOfView,float aspect) => Mathf.Min(0.38f,MarkerScale(depth,fieldOfView,0.68f,0.14f * aspect));
        public static bool ProjectArrow(Camera camera,Vector3 position,Quaternion rotation,float scale,out Rect rect)
        {
            Vector2 minimum = Vector2.one * float.PositiveInfinity,maximum = Vector2.one * float.NegativeInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = new Vector3((corner & 1) == 0 ? -0.36f : 0.36f,(corner & 2) == 0 ? -0.02f : 0.02f,(corner & 4) == 0 ? -0.24f : 0.38f);
                Vector3 view = camera.WorldToViewportPoint(position + rotation * (local * scale + Vector3.forward * ((corner & 4) == 0 ? -0.035f : 0.035f)));
                if (!Finite(view.x) || !Finite(view.y) || !Finite(view.z) || view.z <= camera.nearClipPlane) { rect = default; return false; }
                minimum = Vector2.Min(minimum,new Vector2(view.x,view.y));
                maximum = Vector2.Max(maximum,new Vector2(view.x,view.y));
            }
            rect = Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y);
            return true;
        }
        public static float FitArrow(Camera camera,Vector3 position,Quaternion rotation,float scale,out Rect rect)
        {
            rect = default;
            if (scale <= 0 || !ProjectArrow(camera,position,rotation,scale,out rect)) return 0;
            if (rect.width <= 0.14f && rect.height <= 0.12f) return scale;
            float lower = 0,upper = scale;
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float candidate = (lower + upper) * 0.5f;
                if (ProjectArrow(camera,position,rotation,candidate,out Rect projected) && projected.width <= 0.14f && projected.height <= 0.12f) lower = candidate;
                else upper = candidate;
            }
            return lower >= 0.001f && ProjectArrow(camera,position,rotation,lower,out rect) ? lower : 0;
        }
        public static bool InGuidanceWindow(Rect rect) => rect.xMin >= 0.06f && rect.xMax <= 0.94f && rect.yMin >= 0.34f && rect.yMax <= 0.84f && rect.width <= 0.1401f && rect.height <= 0.1201f;
        public static bool Separated(Rect first,Rect second) => !new Rect(first.xMin - 0.03f,first.yMin - 0.035f,first.width + 0.06f,first.height + 0.07f).Overlaps(second);
        public static bool NextTurn(WalkingRouteFollower follower,out float distance,out float angle)
        {
            distance = angle = 0;
            if (follower.Data == null || follower.Index < 1) return false;
            for (int index = 1; index < follower.Data.points.Count; index++)
            {
                Vector3 current = follower.WorldPoint(index,0) - follower.WorldPoint(index - 1,0);
                distance += current.magnitude;
                if (index < follower.Index || index >= follower.Data.points.Count - 1) continue;
                Vector3 next = follower.WorldPoint(index + 1,0) - follower.WorldPoint(index,0);
                angle = Vector3.SignedAngle(current,next,Vector3.up);
                if (Mathf.Abs(angle) >= 35) return true;
            }
            return false;
        }
        public static float CueAngle(WalkingRouteFollower follower) => NextTurn(follower,out float corner,out float angle) && corner - follower.TravelledMetres <= 1.2f ? angle : Mathf.Abs(follower.Angle) >= 45 ? follower.Angle : 0;
        public static string CameraHint(WalkingRouteFollower follower) => NextTurn(follower,out float corner,out float angle) && corner - follower.TravelledMetres <= 1.2f && Mathf.Abs(Mathf.DeltaAngle(0,follower.Angle + angle)) < 45 ? "" : follower.CameraHint;
        public static string Instruction(WalkingRouteFollower follower)
        {
            if (!NextTurn(follower,out float corner,out float angle) || corner - follower.TravelledMetres > 1.2f) return follower.Direction;
            float remaining = Mathf.Max(0,corner - follower.TravelledMetres);
            return "Rẽ " + (angle > 0 ? "phải" : "trái") + (remaining > 0.4f ? " sau ~" + remaining.ToString("F1") + " m" : " tại góc đã ghi");
        }
        public static bool Arrow(WalkingRouteFollower follower,int ordinal,float floor,out Vector3 position,out Vector3 forward,out float distance)
        {
            position = forward = Vector3.zero;
            distance = First(follower.TravelledMetres) + ordinal * Spacing;
            float limit = Mathf.Min(follower.TotalLength - 0.8f,Mathf.Min(follower.TravelledMetres + 6,VisibleUntil(follower)));
            bool turning = NextTurn(follower,out float corner,out _) && corner - follower.TravelledMetres <= 1.2f;
            if (ordinal == 0) distance = turning ? corner : Mathf.Min(distance,limit);
            if (distance < follower.TravelledMetres + 0.25f || distance > follower.TotalLength - 0.45f || (!turning || ordinal != 0) && distance > limit) return false;
            if (!Sample(follower,distance,floor,out position,out forward,out _)) return false;
            if (turning && ordinal == 0 && Sample(follower,distance + 0.01f,floor,out _,out Vector3 next,out _)) forward = next;
            return true;
        }
        public static bool Sample(WalkingRouteFollower follower,float distance,float floor,out Vector3 position,out Vector3 forward,out int segment)
        {
            position = forward = Vector3.zero;
            segment = 1;
            float covered = 0;
            for (int index = 1; index < follower.Data.points.Count; index++)
            {
                Vector3 start = follower.WorldPoint(index - 1,floor),end = follower.WorldPoint(index,floor);
                float length = Vector3.Distance(start,end);
                if (length <= 0.001f) continue;
                if (distance <= covered + length)
                {
                    segment = index;
                    position = Vector3.Lerp(start,end,(distance - covered) / length);
                    forward = (end - start).normalized;
                    return true;
                }
                covered += length;
            }
            return false;
        }
        public static float VisibleUntil(WalkingRouteFollower follower)
        {
            return NextTurn(follower,out float corner,out _) ? Mathf.Max(0,corner - 0.18f) : follower.TotalLength;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
