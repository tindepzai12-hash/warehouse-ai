using System;
namespace AtlasAR
{
    [Serializable] public sealed class DiagnosticFrame
    {
        public string utc;
        public string source="UNITY_CPU_RAW_LUMA_DOWNSAMPLED";
        public bool poseUsable=false;
        public double imageSeconds;
        public long frameNanoseconds;
        public int width,height,rawWidth,rawHeight,epoch;
        public UnityEngine.Vector4 intrinsics;
        public UnityEngine.Vector3 sessionCameraPosition;
        public UnityEngine.Quaternion sessionCameraRotation;
    }
}
