namespace JarVis.Warehouse.Lab
{
    public static class LabPoseFeedback
    {
        public static string Title(string reason)
        {
            switch(reason)
            {
                case "CLEARANCE_MARGIN":return "VỊ TRÍ SÁT MÉP LỐI";
                case "OBSTACLE":return "VỊ TRÍ TRÙNG VẬT CẢN";
                case "OUTSIDE_MAP":return "VỊ TRÍ NGOÀI MAP";
                case "UNKNOWN_FLOOR":return "VÙNG MAP CHƯA XÁC NHẬN";
                default:return "KIỂM TRA VỊ TRÍ";
            }
        }
        public static string Hint(string reason)
        {
            switch(reason)
            {
                case "CLEARANCE_MARGIN":return "Chấm đang sát mép lối trên map. Nếu bạn đứng giữa lối thật, quét mốc gần nhất để căn lại; giữ nguyên đích.";
                case "OBSTACLE":return "Map đang đặt bạn vào vùng bàn/vật cản. Đứng yên, kiểm tra map rồi quét mốc gần nhất; không đi theo mũi tên cũ.";
                case "OUTSIDE_MAP":return "Chấm đã ra ngoài map. Đứng yên, quét một mốc A–E gần bạn để căn lại; không cần về A.";
                case "UNKNOWN_FLOOR":return "Vị trí rơi vào vùng map chưa rõ. Mở map đối chiếu hoặc quét mốc gần nhất; đích vẫn được giữ.";
                default:return "Chưa có vị trí đáng tin để chỉ đường. Giữ máy nhìn lại vùng có chi tiết; nếu chưa phục hồi, quét mốc gần nhất.";
            }
        }
    }
    public sealed partial class LabTestSession
    {
        public string PoseFeedback=>LabPoseFeedback.Hint(PoseReason);
        public string EvidencePhase=>FixedSurvey?"MARKER_SURVEY":SettingUp?"SETUP":AwaitingDirection?"HEADING":
            CameraGuidanceOccluded?"PANEL":RecoveringTracking||RecoveryNeedsMarker?"RECOVERY":!Tracking?"TRACKING_UNAVAILABLE":
            !Located?"SCANNING":!NavigationPoseValid?"POSE_REJECTED":AtTarget?"ARRIVED":NavigationInProgress?"GUIDING":"READY";
    }
}
