using System;
using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class RoomEnvironmentReadiness
    {
        public bool Ready { get; private set; }
        public float Progress { get; private set; }
        public int FeatureCount { get; private set; }
        public int FeatureCells { get; private set; }
        private double featureAt = double.NegativeInfinity,featureSince = -1,featureGoodAt = double.NegativeInfinity;
        private int featureFrame = -1;
        public string Message { get; private set; } = "Đang chờ camera AR. Chưa quét QR.";
        private double frameAt = double.NegativeInfinity,started = -1,observedAt;
        private long? timestamp;
        private int frames,firstFrame;
        private Vector3 firstPosition,previous;
        private Quaternion firstRotation,previousRotation;
        private float translation,rotation;
        private double requestedAt = -1;
        public bool WaitingTooLong(double now) => !Ready && requestedAt >= 0 && Finite(now) && now - requestedAt >= 8;

        public bool Fresh(double now) => Finite(now) && now >= frameAt && now - frameAt <= 0.5;

        public void Frame(double now,long? timestampNs)
        {
            if (!Finite(now) || now < frameAt || (timestampNs.HasValue && timestamp.HasValue && timestampNs.Value <= timestamp.Value)) return;
            timestamp = timestampNs;
            frameAt = now;
            frames++;
        }

        public void Begin()
        {
            ResetObservations();
            requestedAt = -1;
            featureAt = featureGoodAt = double.NegativeInfinity;
            featureSince = -1;
            featureFrame = -1;
            FeatureCount = FeatureCells = 0;
            Message = "Di chuyển máy nhẹ, nhìn sàn và đồ vật có chi tiết.";
        }

        public void Pause()
        {
            Begin();
            frameAt = double.NegativeInfinity;
            timestamp = null;
        }

        public void Observe(Vector3 position,Quaternion orientation,double now,bool tracking)
        {
            if (requestedAt < 0 && Finite(now)) requestedAt = now;
            if (!Fresh(now) || !tracking || !Finite(position.x) || !Finite(position.y) || !Finite(position.z) || !Finite(orientation.x) || !Finite(orientation.y) || !Finite(orientation.z) || !Finite(orientation.w) || Quaternion.Dot(orientation,orientation) < 0.5f)
            {
                ResetObservations();
                featureGoodAt = double.NegativeInfinity;
                featureSince = -1;
                Message = !Fresh(now) ? "Đang chờ camera AR. Chưa quét QR." : "AR chưa bắt vị trí. Lia máy chậm ở nơi đủ sáng.";
                return;
            }
            double delta = now - observedAt;
            if (started >= 0 && (delta < 0 || delta > 0.5 || Vector3.Distance(position,previous) > Math.Max(0.45,delta * 3.5) || Quaternion.Angle(previousRotation,orientation) > Math.Max(30,delta * 180)))
                ResetObservations();
            if (started < 0)
            {
                started = now;
                firstPosition = position;
                firstRotation = orientation;
                firstFrame = frames;
            }
            previous = position;
            previousRotation = orientation;
            observedAt = now;
            translation = Mathf.Max(translation,Vector3.Distance(firstPosition,position));
            rotation = Mathf.Max(rotation,Quaternion.Angle(firstRotation,orientation));
            bool features = Ready || (now >= featureGoodAt && now - featureGoodAt <= 4);
            float featureProgress = features ? 1 : now - featureAt <= 0.6 ? Mathf.Min(Mathf.Clamp01(FeatureCount / 20f),Mathf.Clamp01(FeatureCells / 3f)) * 0.5f : 0;
            Progress = (Mathf.Clamp01((float)(now - started) / 1.3f) + Mathf.Clamp01((frames - firstFrame) / 12f) + Mathf.Clamp01(translation / 0.08f) + Mathf.Clamp01(rotation / 15) + featureProgress) * 0.2f;
            Ready = now - started >= 1.3 && frames - firstFrame >= 12 && translation >= 0.08f && rotation >= 15 && features;
            Message = Ready ? "Đủ điều kiện môi trường. Quét cả thẻ A để khóa vị trí." : translation < 0.08f ? "Dịch máy sang hai bên: " + Mathf.Min(8,Mathf.FloorToInt(translation * 100)) + "/8 cm; không chỉ xoay tại chỗ." : rotation < 15 ? "Lia máy chậm: " + Mathf.Min(15,Mathf.FloorToInt(rotation)) + "/15°. Nhìn thêm cạnh đồ vật / sàn có chi tiết." : !features ? now - featureAt > 0.6 ? "Chưa có chi tiết mới. Hướng vào sách/kệ hoặc sàn có hoa văn, tăng sáng; không cần tải lại map." : "Chi tiết: " + FeatureCount + "/20 điểm · " + FeatureCells + "/3 vùng. Giữ máy chậm ở nơi đủ sáng." : "Giữ chuyển động chậm, đang ổn định AR…";
        }

        public void Features(int count,int cells,double now)
        {
            if (!Fresh(now) || frames == featureFrame || count < 0 || cells < 0 || cells > 6) return;
            featureFrame = frames;
            FeatureCount = count;
            FeatureCells = cells;
            if (count < 20 || cells < 3) featureSince = -1;
            else
            {
                if (featureSince < 0 || now - featureAt > 0.6) featureSince = now;
                if (now - featureSince >= 0.5) featureGoodAt = now;
            }
            featureAt = now;
        }

        private void ResetObservations()
        {
            Ready = false;
            Progress = translation = rotation = 0;
            started = -1;
            featureGoodAt = double.NegativeInfinity;
            featureSince = -1;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
