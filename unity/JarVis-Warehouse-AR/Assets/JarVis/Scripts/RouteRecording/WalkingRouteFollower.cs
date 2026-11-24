using System;
using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteFollower
    {
        public WalkingRouteData Data { get; private set; }
        public bool WaitingQr { get; private set; }
        public bool Confirming { get; private set; }
        public bool Active { get; private set; }
        public bool Arrived { get; private set; }
        public int Index { get; private set; }
        public Vector3 LocalPosition { get; private set; }
        public float Angle { get; private set; }
        public float Distance { get; private set; }
        public float Remaining { get; private set; }
        public float TotalLength { get; private set; }
        public float Progress => Arrived ? 1 : Active && TotalLength > 0 ? Mathf.Clamp01(1 - Remaining / TotalLength) : 0;
        public bool OffRoute => Active && (MarkerAligned ? deviation.Confirmed : Distance > 2);
        public bool RouteChecking => Active && MarkerAligned && deviation.Caution && !deviation.Confirmed;
        public bool RouteCaution => OffRoute || RouteChecking;
        public bool Suspended { get; private set; }
        public bool ShowArrow => Active && !Suspended && !RouteCaution;
        public float TravelledMetres => MarkerAligned ? travelled : TotalLength - Remaining;
        public string CameraHint => !MarkerAligned || !Active || Mathf.Abs(Angle) < 45 ? "" : Mathf.Abs(Angle) > 120 ? "Quay máy về phía tuyến để thấy mũi tên" : "Xoay máy về hướng mũi tên";
        public bool HasEndQr => Data != null && Data.state == "complete" && WalkingRoute.TryQr(Data.endQr, out _);
        public string Destination => HasEndQr ? Data.endQr.Substring(Data.endQr.LastIndexOf(':') + 1) : "Điểm cuối đã ghi";
        public int AlignmentSerial { get; private set; }
        public float CalibrationCameraY => origin.y;
        public float LocalYaw { get; private set; }
        public bool MarkerAligned { get; private set; }
        public Vector3 DestinationWorld(float floorY)
        {
            Vector3 result = origin + Quaternion.Inverse(inverse) * Data.destinationInRoute;
            result.y = floorY;
            return result;
        }
        public Vector3 WorldPoint(int index, float floorY)
        {
            RoutePoint point = Data.points[index];
            Vector3 result = origin + Quaternion.Inverse(inverse) * new Vector3(point.x, 0, point.z);
            result.y = floorY;
            return result;
        }
        public string Message { get; private set; } = "";
        private Vector3 origin, previous;
        private Quaternion inverse;
        private double previousTime;
        private double suspendedSince,stableSince;
        private float travelled,turnAngle,turnDistance;
        private readonly System.Collections.Generic.List<float> cumulative = new System.Collections.Generic.List<float>();
        private readonly RoomRouteDeviation deviation = new RoomRouteDeviation();

        public void Prepare(WalkingRouteData data)
        {
            Stop("Không có tuyến hợp lệ.");
            Data = null;
            TotalLength = 0;
            MarkerAligned = false;
            if (data == null || data.points == null || data.points.Count < 2 || data.points.Count > WalkingRoute.MaximumPoints || !WalkingRoute.TryQr(data.startQr, out _)) return;
            foreach (RoutePoint point in data.points)
                if (point == null || !Finite(new Vector3(point.x, point.y, point.z))) return;
            Data = data.roomAnchorId == RoomRouteAlignment.AnchorId && data.state == "complete" ? RoomRouteGeometry.GuidanceCopy(data) : data;
            travelled = turnAngle = turnDistance = 0;
            cumulative.Clear(); cumulative.Add(0);
            for (int index = 1; index < Data.points.Count; index++)
            {
                TotalLength += Vector2.Distance(Point(index - 1), Point(index));
                cumulative.Add(TotalLength);
            }
            if (TotalLength < 1) { Data = null; return; }
            WaitingQr = true;
            Message = HasEndQr ? "Tuyến đã ghi tới " + Data.endQr + ". Quét lại " + data.startQr + " và căn vị trí / hướng cũ." : "Tuyến chưa có QR cuối. Chỉ thử tới điểm cuối đã ghi; quét lại " + data.startQr + ".";
        }

        public bool AcceptQr(string text)
        {
            if (!WaitingQr || Data == null) return false;
            if (text != Data.startQr) { Message = "Sai mốc. Cần " + Data.startQr; return false; }
            WaitingQr = false;
            Confirming = true;
            Message = "Đứng đúng vị trí, hướng máy và khoảng cách tới mốc như lúc ghi. QR chỉ nhận tên, chưa tự căn 3D. Xác nhận khi AR ổn định.";
            return true;
        }

        public bool Align(Vector3 position, float yaw, double now, bool stable)
        {
            if (!Confirming || !stable || !Finite(position) || !Finite(yaw) || !Finite(now)) return false;
            origin = previous = position;
            AlignmentSerial++;
            inverse = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0));
            previousTime = now;
            Index = 1;
            LocalPosition = Vector3.zero;
            LocalYaw = 0;
            Confirming = false;
            Active = true;
            Suspended = false;
            Arrived = false;
            Distance = Point(Index).magnitude;
            Remaining = TotalLength;
            travelled = turnAngle = turnDistance = 0;
            deviation.Reset();
            Angle = Bearing(Point(Index), 0);
            Message = "Căn thủ công: có thể lệch. Quan sát lối đi thật; không đi xuyên vật cản.";
            return true;
        }

        public void Observe(Vector3 position, float yaw, double now, bool stable,bool allowProgress = true)
        {
            if (!Active) return;
            double delta = now - previousTime;
            if (MarkerAligned)
            {
                if (!Finite(position) || !Finite(yaw) || !Finite(now) || delta < 0 || delta > 1)
                { RequireScan("Phiên vị trí bị gián đoạn. Quay về A và quét lại; không nối đoạn chưa biết."); return; }
                if (!stable)
                {
                    if (!Suspended) { Suspended = true; suspendedSince = now; stableSince = -1; }
                    stableSince = -1;
                    if (now - previousTime > 0.75 || now - suspendedSince > 0.75) RequireScan("AR mất vị trí quá lâu. Quay về A để quét lại.");
                    else Message = "Đang tìm lại vị trí. Đứng yên; tạm ẩn mũi tên.";
                    return;
                }
                if (Suspended)
                {
                    if (delta > 0.75 || Vector3.Distance(position,previous) > 0.25f)
                    { RequireScan("Vị trí đổi sau khi mất tracking. Quay về A và quét lại."); return; }
                    if (stableSince < 0) stableSince = now;
                    if (now - stableSince < 0.2) return;
                    Suspended = false;
                }
                if (Vector3.Distance(position,previous) > Math.Max(0.45,delta * 3.5))
                { RequireScan("AR nhảy vị trí " + Vector3.Distance(position,previous).ToString("F1") + " m. Quay về A và quét lại."); return; }
            }
            else if (!stable || !Finite(position) || !Finite(yaw) || !Finite(now) || delta < 0 || delta > 1 || Vector3.Distance(position, previous) > Math.Max(0.45, delta * 3.5))
            {
                Stop("Dừng dẫn: tracking mất/nhảy hoặc app gián đoạn. Quay về A, quét và căn lại; không nối phiên AR."); return;
            }
            previous = position;
            previousTime = now;
            LocalPosition = inverse * (position - origin);
            Vector2 current = new Vector2(LocalPosition.x, LocalPosition.z);
            if (!allowProgress) return;
            if (MarkerAligned) { ObserveRoom(current,yaw,now); return; }
            Vector2 target = Point(Index);
            Distance = Vector2.Distance(current, target);
            while (Distance <= 0.45f)
            {
                if (Index == Data.points.Count - 1)
                {
                    Active = false;
                    Arrived = true;
                    Remaining = 0;
                    Message = "Đã tới gần " + Destination + " (ước lượng trong 0,45 m). " + (HasEndQr ? "Kiểm tra QR đích và vị trí thật; chưa xác minh lại QR trong phiên dẫn." : "Chưa có QR cuối xác minh; kiểm tra vị trí thật.");
                    return;
                }
                Index++;
                target = Point(Index);
                Distance = Vector2.Distance(current, target);
            }
            Vector2 direction = target - current;
            float localYaw = Mathf.DeltaAngle(0, yaw - Quaternion.Inverse(inverse).eulerAngles.y);
            LocalYaw = localYaw;
            Angle = Bearing(direction, localYaw);
            Remaining = Distance;
            for (int index = Index; index < Data.points.Count - 1; index++) Remaining += Vector2.Distance(Point(index), Point(index + 1));
            Message = Distance > 2 ? "Bạn đang xa điểm kế tiếp hơn 2 m. Dừng đi, kiểm tra căn A; đừng đi tắt qua vật cản." : "Theo lần lượt các điểm đã ghi; khoảng cách là ước lượng. Không có phát hiện vật cản.";
        }

        private void ObserveRoom(Vector2 current,float yaw,double now)
        {
            Vector2 start = Point(Index - 1),end = Point(Index);
            Vector2 closest = RoomRouteGeometry.Closest(current,start,end);
            while (Index < Data.points.Count - 1 && CanAdvance(current,start,end,closest))
            {
                Index++;
                start = Point(Index - 1); end = Point(Index);
                closest = RoomRouteGeometry.Closest(current,start,end);
            }
            Distance = Vector2.Distance(current,closest);
            deviation.Observe(Distance,now);
            if (!RouteCaution) travelled = Mathf.Max(travelled,cumulative[Index - 1] + Vector2.Distance(start,closest));
            Remaining = Mathf.Max(0,TotalLength - travelled);
            LocalYaw = Mathf.DeltaAngle(0,yaw - Quaternion.Inverse(inverse).eulerAngles.y);
            Vector2 tangent = (end - start).normalized;
            Vector2 aim = closest + tangent * Mathf.Min(0.75f,Vector2.Distance(closest,end));
            Angle = Bearing((aim - current).sqrMagnitude > 0.001f ? aim - current : tangent,LocalYaw);
            turnDistance = Vector2.Distance(closest,end);
            turnAngle = Index < Data.points.Count - 1 ? Vector2.SignedAngle((Point(Index + 1) - end).normalized,tangent) : 0;
            if (!RouteCaution && Index == Data.points.Count - 1 && Vector2.Distance(current,end) <= 0.45f && Remaining <= 0.45f)
            {
                Active = false; Arrived = true; Remaining = 0;
                Message = "Đã tới gần B theo hành trình đã ghi; kiểm tra điểm đích thật. Chưa quét xác minh lại QR B.";
            }
            else Message = OffRoute ? "Đang lệch tuyến. Dừng lại, không đi xuyên vật cản." : RouteChecking ? "Đang đối chiếu tuyến. Dừng một chút; mũi tên tạm ẩn." : "Theo tuyến đã ghi; không phát hiện vật cản.";
        }

        private bool CanAdvance(Vector2 current,Vector2 start,Vector2 end,Vector2 closest)
        {
            float toEnd = Vector2.Distance(current,end);
            if (toEnd < 0.15f) return true;
            Vector2 next = Point(Index + 1),direction = (next - end).normalized;
            if (Vector2.Angle(end - start,direction) > 130) return false;
            if (Vector2.Dot(current - end,end - start) >= 0 && toEnd < 0.65f) return true;
            Vector2 onNext = RoomRouteGeometry.Closest(current,end,next);
            float nextDistance = Vector2.Distance(current,onNext);
            return Vector2.Distance(closest,end) <= 0.6f && toEnd <= 1 && Vector2.Dot(current - end,direction) > 0.1f && nextDistance <= 0.55f && nextDistance + 0.08f < Vector2.Distance(current,closest);
        }

        public void RequireScan(string reason)
        {
            Stop(reason);
            WaitingQr = Data != null;
        }

        public bool AlignAtMarker(Pose marker,Vector3 camera,float yaw,double now,bool stable)
        {
            if (!WaitingQr || !RoomRouteStorage.Valid(Data) || !stable) return false;
            Pose restored = RoomRouteAlignment.Restore(Data,marker);
            if (Vector3.Distance(camera,restored.position) > 2.5f) return false;
            if (!AcceptQr(Data.startQr) || !Align(restored.position,restored.rotation.eulerAngles.y,now,stable)) return false;
            previous = camera;
            MarkerAligned = true;
            Observe(camera,yaw,now,stable);
            return Active || Arrived;
        }

        public string Direction => !Active ? "" : Suspended ? "ĐỨNG YÊN · ĐANG TÌM LẠI VỊ TRÍ" : MarkerAligned ? RouteCaution ? "DỪNG · ĐỐI CHIẾU TUYẾN" : Mathf.Abs(turnAngle) >= 35 && turnDistance <= 1.2f ? (turnDistance > 0.4f ? "SẮP RẼ " : "RẼ ") + (turnAngle > 0 ? "PHẢI" : "TRÁI") : "ĐI THẲNG" : Mathf.Abs(Angle) > 140 ? "QUAY LẠI" : Mathf.Abs(Angle) < 20 ? "ĐI THẲNG" : Angle < 0 ? "SANG TRÁI " + Mathf.Abs(Angle).ToString("F0") + "°" : "SANG PHẢI " + Angle.ToString("F0") + "°";
        public static float Bearing(Vector2 direction, float cameraYaw) => Mathf.DeltaAngle(cameraYaw, Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);
        public void Stop(string reason)
        {
            WaitingQr = Confirming = Active = Arrived = false;
            Suspended = false;
            deviation.Reset();
            Message = reason;
        }
        private Vector2 Point(int index) => new Vector2(Data.points[index].x, Data.points[index].z);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
