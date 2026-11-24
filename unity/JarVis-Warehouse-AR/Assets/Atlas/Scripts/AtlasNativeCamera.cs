using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace AtlasAR
{
    /// <summary>Raw, unrotated CPU image calibration and exposure pose from one ARCore frame.</summary>
    public static class AtlasNativeCamera
    {
        const string Library = "atlas_native_camera";
        static int mainThread;
        [Serializable] public sealed class CaptureCheck
        {
            public string source;
            public int unityWidth,unityHeight,nativeWidth,nativeHeight;
            public double unityImageSeconds,deltaNanoseconds;
            public long expectedFrameNanoseconds,nativeFrameNanoseconds,nativeImageNanoseconds;
        }
        public static CaptureCheck LastCheck {get;private set;}

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeMainThread() { mainThread = Thread.CurrentThread.ManagedThreadId; }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct NativeSample
        {
            public uint version, size;
            public long frameTimestampNs, imageTimestampNs;
            public ulong sessionToken;
            public int width, height, trackingState, reserved;
            public float fx, fy, cx, cy;
            public float qx, qy, qz, qw, px, py, pz, reservedFloat;
        }

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        static extern int atlas_camera_capture(IntPtr sessionWrapper, IntPtr frameWrapper,
            long expectedTimestampNs, out NativeSample sample, uint outputSize);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        static extern int atlas_camera_capture_luma(IntPtr sessionWrapper, IntPtr frameWrapper,
            long expectedTimestampNs, out NativeSample sample, uint outputSize,
            [Out] byte[] luma, uint capacity);

        public readonly struct Sample
        {
            public readonly long FrameTimestampNs, ImageTimestampNs;
            // Identity token only. Never persist or dereference it; parent must also enforce its session epoch.
            public readonly ulong SessionToken;
            public readonly int Width, Height;
            public readonly float Fx, Fy, Cx, Cy;
            // Unity LH session space, raw optical camera basis: +X right, +Y up, +Z looking forward.
            public readonly Pose SessionCameraPose;
            public Pose UnityPose => SessionCameraPose;
            public Vector4 Intrinsics => new Vector4(Fx, Fy, Cx, Cy);

            internal Sample(NativeSample raw)
            {
                FrameTimestampNs=raw.frameTimestampNs; ImageTimestampNs=raw.imageTimestampNs;
                SessionToken=raw.sessionToken; Width=raw.width; Height=raw.height;
                Fx=raw.fx; Fy=raw.fy; Cx=raw.cx; Cy=raw.cy;
                // S=diag(1,1,-1): pUnity=S*pGL; RUnity=S*RGL*S.
                var rotation=new Quaternion(-raw.qx,-raw.qy,raw.qz,raw.qw).normalized;
                SessionCameraPose=new Pose(new Vector3(raw.px,raw.py,-raw.pz),rotation);
            }

            /// <summary>Apply the explicit session-space parent, e.g. XROrigin.TrackablesParent.</summary>
            public bool TryGetWorldCameraPose(Transform sessionSpace, out Pose pose)
            {
                pose=Pose.identity;
                if(SessionToken==0 || !Rigid(sessionSpace))return false;
                pose=new Pose(sessionSpace.TransformPoint(SessionCameraPose.position),
                    sessionSpace.rotation*SessionCameraPose.rotation);
                return true;
            }

            /// <summary>mapCamera must use the same RAW image optical basis (SceneLocalizer's result).</summary>
            public bool TryMapToSession(Pose mapCamera, out Pose mapToSession)
            {
                mapToSession=Pose.identity;
                if(SessionToken==0 || !Finite(mapCamera.position) || !Finite(mapCamera.rotation))return false;
                float norm=Quaternion.Dot(mapCamera.rotation,mapCamera.rotation);
                if(Mathf.Abs(norm-1)>0.01f)return false;
                var mapRotation=mapCamera.rotation.normalized;
                var inverse=new Quaternion(-mapRotation.x,-mapRotation.y,-mapRotation.z,mapRotation.w);
                var rotation=SessionCameraPose.rotation*inverse;
                mapToSession=new Pose(SessionCameraPose.position-rotation*mapCamera.position,rotation);
                // No yaw flattening or gravity-policy decision here; the parent owns acceptance.
                return true;
            }

            public bool TryMapToWorld(Pose mapCamera, Transform sessionSpace, out Pose mapToWorld)
            {
                mapToWorld=Pose.identity;
                if(!Rigid(sessionSpace) || !TryMapToSession(mapCamera,out var local))return false;
                mapToWorld=new Pose(sessionSpace.TransformPoint(local.position),sessionSpace.rotation*local.rotation);
                return true;
            }
        }

        /// <summary>One owned ARCore image supplies luma, intrinsics and frame pose. Call in frameReceived.</summary>
        public static bool TryCapturePixels(ARSession session, ARCameraManager manager,
            long expectedFrameTimestampNs, ref byte[] luma, out Sample sample, out string reason)
        {
            sample=default; reason=null;LastCheck=null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(mainThread==0 || Thread.CurrentThread.ManagedThreadId!=mainThread){reason="MAIN_THREAD_REQUIRED";return false;}
            if(session==null || manager==null || session.subsystem==null || manager.subsystem==null ||
               !session.subsystem.running || !manager.subsystem.running){reason="SUBSYSTEM_NOT_RUNNING";return false;}
            if(!(session.subsystem is UnityEngine.XR.ARCore.ARCoreSessionSubsystem) ||
               !(manager.subsystem is UnityEngine.XR.ARCore.ARCoreCameraSubsystem)){reason="ARCORE_REQUIRED";return false;}
            if(expectedFrameTimestampNs<=0){reason="FRAME_TIMESTAMP_REQUIRED";return false;}
            var camera=manager.GetComponent<Camera>();
            if(camera==null){reason="CAMERA_REQUIRED";return false;}
            var parameters=new XRCameraParams{zNear=camera.nearClipPlane,zFar=camera.farClipPlane,
                screenWidth=Screen.width,screenHeight=Screen.height,screenOrientation=Screen.orientation};
            if(!manager.subsystem.TryGetLatestFrame(parameters,out var frame) ||
               !frame.TryGetTimestamp(out long timestamp) || timestamp!=expectedFrameTimestampNs)
            {reason="FRAME_TIMESTAMP_MISMATCH";return false;}
            if(frame.nativePtr==IntPtr.Zero || session.subsystem.nativePtr==IntPtr.Zero)
            {reason="NATIVE_POINTER_UNAVAILABLE";return false;}
            try
            {
                int size=Marshal.SizeOf<NativeSample>();
                if(size!=96){reason="MANAGED_ABI_MISMATCH";return false;}
                // Bounded reusable buffer; never allocate based on an unchecked native dimension.
                if(luma==null||luma.Length!=4*1024*1024)luma=new byte[4*1024*1024];
                int code=atlas_camera_capture_luma(session.subsystem.nativePtr,frame.nativePtr,timestamp,out var raw,(uint)size,luma,(uint)luma.Length);
                if(code!=0){reason=NativeFailure(code);return false;}
                if(raw.version!=1 || raw.size!=96 || raw.frameTimestampNs!=timestamp || raw.trackingState!=0 ||
                   raw.sessionToken==0){reason="NATIVE_SAMPLE_INVALID";return false;}
                if(raw.width<1||raw.height<1||(long)raw.width*raw.height>luma.Length)
                {reason="NATIVE_LUMA_SIZE_INVALID";return false;}
                LastCheck=new CaptureCheck{source="ARCORE_SINGLE_OWNED_IMAGE",nativeWidth=raw.width,nativeHeight=raw.height,
                    expectedFrameNanoseconds=timestamp,nativeFrameNanoseconds=raw.frameTimestampNs,
                    nativeImageNanoseconds=raw.imageTimestampNs,deltaNanoseconds=raw.imageTimestampNs-raw.frameTimestampNs};
                sample=new Sample(raw);return true;
            }
            catch(DllNotFoundException){reason="NATIVE_LIBRARY_MISSING";return false;}
            catch(EntryPointNotFoundException){reason="NATIVE_ENTRYPOINT_MISSING";return false;}
            catch(BadImageFormatException){reason="NATIVE_LIBRARY_ABI_MISMATCH";return false;}
#else
            reason="ANDROID_DEVICE_REQUIRED";return false;
#endif
        }

        internal static bool TimestampMatches(double seconds,long nanoseconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0||nanoseconds<=0)return false;
            double nativeSeconds=nanoseconds*1e-9;
            double next=BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(nativeSeconds)+1);
            // Only double conversion roundoff (two ULPs + one ns); no motion/skew allowance.
            double tolerance=2*(next-nativeSeconds)+1e-9;
            return Math.Abs(seconds-nativeSeconds)<=tolerance;
        }

        static string NativeFailure(int code)
        {
            switch(code)
            {
                case 1:return "NATIVE_ARGUMENT_INVALID";
                case 2:return "UNITY_NATIVE_WRAPPER_VERSION_UNSUPPORTED";
                case 3:return "ARCORE_SYMBOLS_UNAVAILABLE";
                case 4:return "FRAME_TIMESTAMP_MISMATCH";
                case 5:return "CAMERA_NOT_TRACKING";
                case 6:return "ARCORE_ALLOCATION_FAILED";
                case 7:return "CURRENT_CPU_IMAGE_UNAVAILABLE";
                case 8:return "CPU_INTRINSICS_INVALID";
                case 9:return "NATIVE_CPU_IMAGE_INVALID";
                case 10:return "RAW_CAMERA_POSE_INVALID";
                case 11:return "NATIVE_LUMA_STRIDE_OR_CAPACITY_INVALID";
                default:return "NATIVE_ERROR_"+code;
            }
        }

        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        static bool Finite(Quaternion q)=>float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w);
        static bool Rigid(Transform t)
        {
            if(t==null)return false;
            var m=t.localToWorldMatrix;
            var x=(Vector3)m.GetColumn(0);var y=(Vector3)m.GetColumn(1);var z=(Vector3)m.GetColumn(2);
            return Finite(t.position)&&Finite(t.rotation)&&Finite(x)&&Finite(y)&&Finite(z)&&
                Mathf.Abs(x.sqrMagnitude-1)<0.001f&&Mathf.Abs(y.sqrMagnitude-1)<0.001f&&
                Mathf.Abs(z.sqrMagnitude-1)<0.001f&&Mathf.Abs(Vector3.Dot(x,y))<0.001f&&
                Mathf.Abs(Vector3.Dot(x,z))<0.001f&&Mathf.Abs(Vector3.Dot(y,z))<0.001f&&
                Vector3.Dot(Vector3.Cross(x,y),z)>0.999f;
        }
    }
}
