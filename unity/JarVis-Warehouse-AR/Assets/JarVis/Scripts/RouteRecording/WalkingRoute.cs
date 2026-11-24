using System;
using System.Collections.Generic;
using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    [Serializable]
    public sealed class RoutePoint
    {
        public double seconds;
        public float x, y, z, yawDegrees;
    }

    [Serializable]
    public sealed class WalkingRouteData
    {
        public int schemaVersion = 1;
        public string id = Guid.NewGuid().ToString("N");
        public string createdUtc = DateTime.UtcNow.ToString("O");
        public string finishedUtc;
        public string state = "recording";
        public string startQr;
        public string endQr;
        public string interruption;
        public string device;
        public string operatingSystem;
        public string appVersion;
        public string coordinateSystem = "metres; gravity aligned; origin at START camera; +Z initial horizontal camera forward; +X right; Y relative camera height, NOT floor; QR identifiers are NOT measured marker poses";
        public string accuracy = "Uncorrected ARCore visual-inertial estimate. Drift is possible. Not a persistent spatial map or a surveyed route. Do not use for safety-critical navigation.";
        public float horizontalMetres;
        public double durationSeconds;
        public List<RoutePoint> points = new List<RoutePoint>();
        public string roomAnchorId;
        public Vector3 originInMarker;
        public float headingInMarker;
        public Vector3 destinationInRoute;
    }

    public static class RoomRouteAlignment
    {
        public const string AnchorId = "JarVisRoom18-v1";
        public const string StartQr = "JWM:MAIN:A";
        public const string EndQr = "JWM:MAIN:B";

        public static float Heading(Pose marker)
        {
            Vector3 normal = marker.rotation * Vector3.up;
            return Mathf.Atan2(normal.x,normal.z) * Mathf.Rad2Deg;
        }

        public static void Capture(WalkingRouteData data,Pose marker,Vector3 camera,float yaw)
        {
            data.schemaVersion = 2;
            data.coordinateSystem = "metres; gravity aligned; origin at START camera; +Z initial horizontal camera forward; +X right; Y relative camera height; restored from the measured vertical 18cm Room-A image pose";
            data.roomAnchorId = AnchorId;
            data.originInMarker = Quaternion.Inverse(Quaternion.Euler(0,Heading(marker),0)) * (camera - marker.position);
            data.headingInMarker = Mathf.DeltaAngle(Heading(marker),yaw);
        }

        public static Pose Restore(WalkingRouteData data,Pose marker)
        {
            Quaternion heading = Quaternion.Euler(0,Heading(marker),0);
            return new Pose(marker.position + heading * data.originInMarker,Quaternion.Euler(0,Heading(marker) + data.headingInMarker,0));
        }
    }

    public sealed class WalkingRoute
    {
        public const double MaximumSeconds = 1800;
        public const int MaximumPoints = 18000;
        public WalkingRouteData Data { get; private set; }
        public bool Active => Data != null && Data.state == "recording";
        public bool AwaitingEnd { get; private set; }
        private Vector3 startPosition;
        private Quaternion inverseHeading;
        private Vector3 lastObserved;
        private double began, lastObservedTime;

        public static bool TryQr(string text, out string warehouse)
        {
            warehouse = null;
            if (string.IsNullOrEmpty(text) || text.Length > 140) return false;
            string[] parts = text.Split(':');
            if (parts.Length != 3 || parts[0] != "JWM" || parts[1].Length == 0 || parts[1].Length > 32 || parts[2].Length == 0 || parts[2].Length > 64) return false;
            foreach (string part in new[] { parts[1], parts[2] })
                foreach (char character in part)
                    if (!((character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9') || character == '_' || character == '-' || character == '.')) return false;
            warehouse = parts[1];
            return true;
        }

        public bool Begin(string qr, Vector3 position, float heading, double time, bool tracking)
        {
            if (Active || !tracking || !TryQr(qr, out _) || !Finite(position) || !Finite(heading) || double.IsNaN(time) || double.IsInfinity(time) || time < 0) return false;
            Data = new WalkingRouteData { startQr = qr };
            startPosition = position;
            inverseHeading = Quaternion.Inverse(Quaternion.Euler(0, heading, 0));
            lastObserved = position;
            began = lastObservedTime = time;
            AwaitingEnd = false;
            Data.points.Add(new RoutePoint());
            return true;
        }

        public bool Observe(Vector3 position, float heading, double time, bool tracking, bool force = false)
        {
            if (!Active) return false;
            if (!tracking) return Interrupt("AR mất theo dõi. Không nối đoạn đường chưa biết; hãy ghi lại từ QR đầu.");
            if (!Finite(position) || !Finite(heading) || double.IsNaN(time) || double.IsInfinity(time)) return Interrupt("Dữ liệu vị trí không hợp lệ.");
            double delta = time - lastObservedTime;
            if (delta < 0 || delta > 1) return Interrupt("Thời gian ghi bị gián đoạn hơn 1 giây. Hãy ghi lại tuyến.");
            if (time - began > MaximumSeconds || Data.points.Count >= MaximumPoints) return Interrupt("Đã đạt giới hạn an toàn 30 phút / 18.000 điểm. Hãy chia thành tuyến ngắn.");
            Vector3 step = position - lastObserved;
            if (step.magnitude > Math.Max(0.45, delta * 3.5)) return Interrupt("Vị trí AR nhảy bất thường. Dừng ghi để không lưu đường sai.");
            lastObserved = position;
            lastObservedTime = time;
            Data.durationSeconds = time - began;
            Vector3 relative = inverseHeading * (position - startPosition);
            float relativeYaw = Mathf.DeltaAngle(0, heading - Quaternion.Inverse(inverseHeading).eulerAngles.y);
            RoutePoint previous = Data.points[Data.points.Count - 1];
            float horizontal = Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(relative.x, relative.z));
            if (force || (Data.durationSeconds - previous.seconds >= 0.2 && (horizontal >= 0.08f || Mathf.Abs(Mathf.DeltaAngle(previous.yawDegrees, relativeYaw)) >= 12)))
            {
                Data.horizontalMetres += horizontal;
                Data.points.Add(new RoutePoint { seconds = Data.durationSeconds, x = relative.x, y = relative.y, z = relative.z, yawDegrees = relativeYaw });
            }
            return true;
        }

        public void ArmEnd(bool enabled)
        {
            AwaitingEnd = Active && enabled;
        }

        public bool Finish(string qr, Vector3 position, float heading, double time, bool tracking)
        {
            if (!Active || !AwaitingEnd || qr == Data.startQr || !TryQr(qr, out string warehouse) || !TryQr(Data.startQr, out string startWarehouse) || warehouse != startWarehouse) return false;
            if (!Observe(position, heading, time, tracking, true)) return false;
            if (Data.horizontalMetres < 1 || Data.durationSeconds < 2) return false;
            Data.endQr = qr;
            Data.finishedUtc = DateTime.UtcNow.ToString("O");
            Data.state = "complete";
            AwaitingEnd = false;
            return true;
        }

        public bool Interrupt(string reason)
        {
            if (Active)
            {
                Data.state = "interrupted";
                Data.interruption = reason;
                Data.finishedUtc = DateTime.UtcNow.ToString("O");
                AwaitingEnd = false;
            }
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
