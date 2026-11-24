using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public enum RoomScanState { Hidden,WaitingCamera,WaitingTracking,Searching,WrongCode,Locating,Locked,WarmingUp }

    public sealed class RoomScanFeedback
    {
        public RoomScanState State { get; private set; }
        public string Expected { get; private set; }
        public float Progress { get; private set; }
        public double SuccessUntil { get; private set; }
        private double frameAt = double.NegativeInfinity,decodedAt = double.NegativeInfinity,wrongAt = double.NegativeInfinity;
        public bool Animating => State == RoomScanState.Searching || State == RoomScanState.Locating;
        public string MarkerName => Expected == RoomRouteAlignment.EndQr ? "B" : "A";
        public string Label => State == RoomScanState.WarmingUp ? "Dịch máy nhẹ · quan sát xung quanh" : State == RoomScanState.Locked ? "Đã khóa " + MarkerName : State == RoomScanState.Locating ? "Đã đọc " + MarkerName + " · đang khóa vị trí" : State == RoomScanState.WrongCode ? "Sai mã · cần thẻ " + MarkerName : State == RoomScanState.WaitingTracking ? "Giữ máy yên · đang ổn định AR" : State == RoomScanState.WaitingCamera ? "Đang chờ ảnh camera…" : "Đang quét thẻ " + MarkerName;
        public void Begin(string expected)
        {
            Expected = expected;
            frameAt = decodedAt = wrongAt = double.NegativeInfinity;
            SuccessUntil = 0;
            Progress = 0;
            State = RoomScanState.WaitingCamera;
        }
        public void Frame(double now) => frameAt = now;
        public void Warmup(float progress) { State = RoomScanState.WarmingUp; Progress = Mathf.Clamp01(progress); SuccessUntil = 0; }
        public void Decoded(string value,double now)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value == Expected) { decodedAt = now; wrongAt = double.NegativeInfinity; }
            else wrongAt = now;
        }
        public void Tick(double now,bool tracking,float markerProgress)
        {
            if (!tracking) { State = RoomScanState.WaitingTracking; Progress = 0; SuccessUntil = 0; return; }
            if (now < SuccessUntil) { State = RoomScanState.Locked; return; }
            Progress = 0;
            if (now - frameAt > 1.2) State = RoomScanState.WaitingCamera;
            else if (now - wrongAt < 1.5) State = RoomScanState.WrongCode;
            else if (now - decodedAt <= 1.5) { State = RoomScanState.Locating; Progress = Mathf.Clamp01(markerProgress); }
            else State = RoomScanState.Searching;
        }
        public void Lock(double now) { State = RoomScanState.Locked; Progress = 1; SuccessUntil = now + 1; }
        public void Hide() { State = RoomScanState.Hidden; SuccessUntil = 0; Expected = null; }
    }
}
