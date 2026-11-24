using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using UnityEngine.XR.Management;

namespace AtlasAR
{
    [Serializable] public class VisualResult
    {
        public bool valid;public string reason;public int features,matches,inliers;public float median,milliseconds;public float[] position,forward,up;
    }
    [Serializable] public class AtlasLog
    {
        public string utc,type,mapId,detail,version,tracking,reason,selected,imageFile,imageSave;public bool positionValid,recording;
        public string navigationTarget,navigationInstruction;public float remainingRouteMetres;public Vector3[] route;
        public AtlasNativeCamera.CaptureCheck cameraCheck;
        public Vector3 exposurePosition;public Quaternion exposureRotation;public long exposureTimestampNs;
        public string queryGate,loader,navigationCameraCue;public bool sessionRunning,cameraRunning,frameReady;public float frameAgeSeconds,managedMemoryMb;public long receivedFrames;
        public float seconds;public Vector3 mapPosition,worldPosition;public Quaternion worldRotation;public int features,matches,inliers,epoch,width,height;public float reprojection,processingMs;public double imageTimestamp;public Vector4 intrinsics;
    }
    public sealed class AtlasSession:MonoBehaviour
    {
        public ARSession arSession;public ARCameraManager cameraManager;public Camera arCamera;public ARAnchorManager anchorManager;
        public AtlasImmersalBridge immersalNavigation;
        public string Status{get;private set;}="Đang mở map A–E";
        public string Hint{get;private set;}="Bắt đầu tại bàn máy tính: quét thẻ A cũ, thấy trọn khung hoa văn.";
        string noticeStatus,noticeHint;float noticeUntil;bool cameraStarting;float pauseAt,firstCameraStart,lastFrameAt,nextStartupLog;long receivedFrames;int startupAttempts;bool hadFrame;
        public string DisplayStatus=>Time.realtimeSinceStartup<noticeUntil?noticeStatus:Status;
        public string DisplayHint=>Time.realtimeSinceStartup<noticeUntil?noticeHint:Hint;
        public bool HasNotice=>Time.realtimeSinceStartup<noticeUntil;
        public string Selected="A";
        public bool Recording=>diagnostics!=null&&diagnostics.Recording;
        public int CapturedImages=>diagnostics?.CapturedImages??0;
        public int Confirmation=>Mathf.Min(consensus.Count,3);
        AtlasDiagnostics diagnostics;
        readonly AtlasPoseConsensus consensus=new AtlasPoseConsensus();
        readonly AtlasTrackingRecovery recovery=new AtlasTrackingRecovery();
        public bool PositionValid{get;private set;}
        public Vector3 MapPosition{get;private set;}public Vector3 MapForward{get;private set;}=Vector3.forward;
        public AtlasNavigation Navigation {get;private set;}
        AtlasMap map;AtlasUi ui;ARAnchor anchor;Transform mapRoot;
        readonly List<Transform> labels=new List<Transform>();readonly List<Renderer> dots=new List<Renderer>();MaterialPropertyBlock pinProperties;
        Task<string> initializeTask,poseTask,diagnosticTask;bool engineReady,engineFailed,paused,anchorBusy,destroyed,wasTracking,pendingExport;int epoch,taskEpoch;
        string diagnosticImage;long cameraAttempts,cameraAccepted,cameraRejected,taskFrameTimestamp;byte[] nativeLuma;
        Pose taskCamera;float nextQuery,nextLog,lastGoodVisual;bool cameraReady;
        long timestamp;float taskAt;double taskImageTime,lastImageTime=-1;byte[] pixels;int taskWidth,taskHeight;Vector4 taskK;string taskImageFile,taskImageSave;float workerSeconds,nextCameraError;ulong nativeSession;
        Transform sessionSpace;
        Vector3 lastArPosition;float lastArAt;
        public AtlasMap SavedMap=>map;

        void Start()
        {
            pinProperties=new MaterialPropertyBlock();
            Application.targetFrameRate=30;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            try{map=AtlasMap.Load();Navigation=new AtlasNavigation(map);ui=gameObject.AddComponent<AtlasUi>();ui.Create(map,this);if(immersalNavigation!=null){immersalNavigation.Initialize();ui.AttachImmersalControls(immersalNavigation.gameObject);}}
            catch(Exception error){Debug.LogException(error);enabled=false;return;}
            diagnostics=new AtlasDiagnostics(map);Log("SESSION_START","Atlas AR "+Application.version+"; map revision="+map.revision+"; coordinates fixed; initialize from existing printed A only; QR10cm/card20cm");
            sessionSpace=anchorManager.GetComponent<Unity.XR.CoreUtils.XROrigin>().TrackablesParent;
            cameraManager.frameReceived+=Frame;
            ARSession.stateChanged+=SessionStateChanged;Application.lowMemory+=LowMemory;
            StartCoroutine(EnsureCamera());
        }
        IEnumerator EnsureCamera(){if(cameraStarting||destroyed)yield break;cameraStarting=true;yield return StartCamera();cameraStarting=false;}
        IEnumerator StartCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Status="Cho phép camera để định vị AR";Hint="Bạn vẫn xem được map A–E khi chưa cấp quyền.";
                var callbacks=new UnityEngine.Android.PermissionCallbacks();bool answered=false;float requested=Time.realtimeSinceStartup;
                callbacks.PermissionGranted+=_=>answered=true;callbacks.PermissionDenied+=_=>answered=true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera,callbacks);
                while(!answered&&Time.realtimeSinceStartup-requested<45)yield return null;
                if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {Status="Map đã sẵn sàng · Camera chưa được phép";Hint="Cấp quyền camera trong Cài đặt để dùng AR.";yield break;}
            }
            if(ARSession.state==ARSessionState.None||ARSession.state==ARSessionState.CheckingAvailability)yield return ARSession.CheckAvailability();
            if(ARSession.state==ARSessionState.NeedsInstall)yield return ARSession.Install();
            if(ARSession.state==ARSessionState.Unsupported||ARSession.state==ARSessionState.NeedsInstall){Status="ARCore chưa sẵn sàng";Hint="Map A–E vẫn dùng được; kiểm tra Google Play Services for AR.";yield break;}
            if(paused||destroyed)yield break;
            arSession.enabled=true;cameraManager.enabled=true;cameraManager.autoFocusRequested=true;
            if(firstCameraStart<=0)firstCameraStart=Time.realtimeSinceStartup;Log("CAMERA_START_REQUESTED","ARSession + ARCameraManager enabled; no forced origin assignment");
            if(initializeTask==null&&!engineReady)initializeTask=Task.Run(()=>Jni(()=>{using(var owner=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=owner.GetStatic<AndroidJavaObject>("currentActivity"))using(var localizer=new AndroidJavaClass("com.teep.atlasar.MarkerLocalizer"))return localizer.CallStatic<string>("initialize",activity);}));
#else
            Status="Map A–E đã sẵn sàng";Hint="Cần Samsung để quét thẻ A cũ và kiểm tra vị trí AR.";yield break;
#endif
        }
        static string Jni(Func<string> call)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJNI.AttachCurrentThread();try{return call();}catch(Exception e){return "ENGINE_ERROR:"+e.GetType().Name+":"+e.Message;}finally{AndroidJNI.DetachCurrentThread();}
#else
            return "EDITOR_NO_CAMERA";
#endif
        }
        void Frame(ARCameraFrameEventArgs frame)
        {
            timestamp=frame.timestampNs??0;cameraReady=timestamp>0;lastFrameAt=Time.realtimeSinceStartup;receivedFrames++;
            if(!hadFrame){hadFrame=true;Log("FIRST_CAMERA_FRAME","timestampNs="+timestamp);}
            if(map!=null&&!destroyed&&engineReady&&!PositionValid&&Tracking&&cameraReady&&!recovery.Active&&poseTask==null&&!anchorBusy&&!pendingExport&&Time.realtimeSinceStartup>=nextQuery)
            {nextQuery=Time.realtimeSinceStartup+1;Query();}
        }
        bool Tracking=>!paused&&ARSession.state==ARSessionState.SessionTracking;
        void Update()
        {
            if(map==null||destroyed)return;float now=Time.realtimeSinceStartup;
            bool tracking=Tracking;
            if(!paused&&now>=nextStartupLog){nextStartupLog=now+5;Log("PIPELINE_STATE",QueryGate());}
            if(!paused&&!engineFailed&&!PositionValid&&firstCameraStart>0&&now-firstCameraStart>20&&!hadFrame&&startupAttempts==0&&!cameraStarting)
            {startupAttempts++;Log("STARTUP_RETRY","No camera frame in 20 seconds; one subsystem restart, no localized map exists");StartCoroutine(RestartUnlocatedCamera());}
            if(!paused&&!PositionValid&&!tracking&&firstCameraStart>0&&now-firstCameraStart>25)
            {Status="Camera chưa định vị được";Hint=hadFrame?TrackingHint()+" Có thể bấm Thử lại camera.":"Chưa có khung hình camera AR · Bấm Thử lại camera hoặc xuất ZIP.";}
            if(diagnosticTask!=null&&diagnosticTask.IsCompleted)
            {
                string saved=diagnosticTask.IsFaulted?"ERROR:DIAGNOSTIC_WORKER":diagnosticTask.Result;
                diagnosticTask=null;diagnostics?.CompleteImage(saved);Log("DIAGNOSTIC_IMAGE_SAVE",diagnosticImage+";"+saved);
            }
            if(wasTracking&&!tracking)
            {
                epoch++;consensus.Reset();PositionValid=false;cameraReady=false;lastArAt=0;
                if(anchor!=null)recovery.Begin(now);Log("TRACKING_LOST",ARSession.notTrackingReason.ToString());
            }
            wasTracking=tracking;
            if(initializeTask!=null&&initializeTask.IsCompleted)
            {
                string result=initializeTask.IsFaulted?"ERROR:INIT":initializeTask.Result;initializeTask=null;engineReady=result.StartsWith("READY:");engineFailed=!engineReady;Log("ENGINE_INIT",result);
                if(engineFailed){Status="Không mở được dữ liệu thẻ A";Hint="Map A–E vẫn hiển thị. Xuất log để kiểm tra APK.";}
            }
            if(tracking)
            {
                if(lastArAt>0&&now-lastArAt<.25f&&Vector3.Distance(lastArPosition,arCamera.transform.position)>1.2f)Invalidate("AR_POSE_JUMP");
                lastArPosition=arCamera.transform.position;lastArAt=now;
            }
            if(anchor!=null&&anchor.trackingState==TrackingState.None)Invalidate("ANCHOR_STOPPED");
            if(anchor!=null&&(!tracking||anchor.trackingState!=TrackingState.Tracking)&&!recovery.Active)
            {epoch++;consensus.Reset();recovery.Begin(now);Log("ANCHOR_PAUSED","Waiting for same-session anchor recovery");}
            if(recovery.Active)
            {
                var action=recovery.Observe(now,tracking,anchor!=null&&anchor.trackingState==TrackingState.Tracking);
                if(action==AtlasRecoveryAction.Expired)Invalidate("RECOVERY_EXPIRED");
                else if(action==AtlasRecoveryAction.Resume){Log("TRACKING_RECOVERED","Anchor tracked continuously for 0.7 seconds");nextQuery=now;}
            }
            PositionValid=tracking&&!recovery.Active&&anchor!=null&&mapRoot!=null&&anchor.trackingState==TrackingState.Tracking;
            if(poseTask!=null&&poseTask.IsCompleted)
            {
                string raw=poseTask.IsFaulted?"{}":poseTask.Result;poseTask=null;workerSeconds=now-taskAt;
                diagnostics?.CompleteImage(taskImageSave);
                if(taskImageFile!=null)Log("IMAGE_SAVE",taskImageSave??"ERROR:WORKER_DID_NOT_SAVE");
                if(taskEpoch==epoch&&tracking&&!recovery.Active&&!paused&&now-taskAt<=2.5f)Process(raw,now);
                else{consensus.Reset();Log("VISUAL_DISCARDED","Old/interrupted session or result older than 2.5 seconds");}
                nextQuery=now+(PositionValid?Mathf.Max(2.5f,workerSeconds):Mathf.Max(.5f,workerSeconds*.5f));
            }
            if(!PositionValid)Navigation.Tick(false,MapPosition,MapForward,arCamera,ui.cameraMode,now);
            if(mapRoot!=null)mapRoot.gameObject.SetActive(PositionValid);
            if(PositionValid)
            {
                MapPosition=mapRoot.InverseTransformPoint(arCamera.transform.position);
                if(!AtlasMap.Finite(MapPosition)||!map.InRoom(MapPosition)||MapPosition.y<.3f||MapPosition.y>2.9f){Invalidate("TRACKED_POSITION_OUTSIDE_ROOM");Status="Đang kiểm tra lại vị trí";Hint="Vị trí chưa khớp phòng · Quay lại thẻ A để căn lại.";return;}
                var mapForward=mapRoot.InverseTransformDirection(arCamera.transform.forward);mapForward.y=0;if(mapForward.sqrMagnitude>.01f)MapForward=mapForward.normalized;
                Navigation.Tick(true,MapPosition,MapForward,arCamera,ui.cameraMode,now);
                Status=Navigation.Active?Navigation.Instruction:"Đã căn từ A · Chọn điểm muốn tới";
                Hint=Navigation.Active?Navigation.CameraCue:"Đang theo dõi bằng AR · Chọn A–E, không cần quét lại giữa các chuyến";
                for(int i=0;i<labels.Count;i++)
                {
                    var label=labels[i];label.rotation=arCamera.transform.rotation;float distance=Vector3.Distance(label.position,arCamera.transform.position);label.localScale=Vector3.one*Mathf.Clamp(distance*.0007f,.0025f,.012f);
                    pinProperties.SetColor("_Color",map.markers[i].id==Selected?new Color32(22,91,232,255):new Color32(56,135,182,255));dots[i].SetPropertyBlock(pinProperties);
                }
            }
            else if(!engineFailed&&engineReady&&!tracking&&(firstCameraStart<=0||now-firstCameraStart<25))
            {
                Status=recovery.Active?"Tạm mất tracking · Đang khôi phục":"Đang khởi động theo dõi AR";Hint=TrackingHint();
            }
            if(recovery.Active&&tracking){Status="Đang giữ lại mốc AR · Chờ camera ổn định";Hint=TrackingHint();}
            // Present only the final navigation state, after this frame's validity and replanning checks.
            immersalNavigation?.Tick(mapRoot,PositionValid,ui.cameraMode);
            if(pendingExport&&poseTask==null&&initializeTask==null&&diagnosticTask==null){pendingExport=false;ExportNow();}
            if(now>=nextLog){nextLog=now+.5f;Log("SAMPLE",PositionValid?"LOCATED":"UNLOCATED");}
            diagnostics?.Tick();
        }
        string QueryGate()
        {
            if(paused)return "APP_PAUSED";if(pendingExport)return "EXPORT_SNAPSHOT";if(!engineReady)return engineFailed?"ENGINE_FAILED":"ENGINE_INITIALIZING";
            if(!Tracking)return "WAIT_AR_TRACKING";if(!cameraReady||Time.realtimeSinceStartup-lastFrameAt>1)return "WAIT_FRESH_FRAME";
            if(PositionValid)return "TRACKED_USING_FIXED_A";if(recovery.Active)return "ANCHOR_RECOVERY";if(anchorBusy)return "ANCHOR_PENDING";
            return poseTask!=null?"LOCALIZER_WORKER_BUSY":"READY_TO_SCAN_A";
        }
        void SessionStateChanged(ARSessionStateChangedEventArgs change){if(map!=null)Log("AR_STATE_CHANGED",change.state.ToString());}
        void LowMemory(){diagnostics?.Stop();Log("LOW_MEMORY","Stopped optional image recording; map retained");diagnostics?.Flush();}
        IEnumerator RestartUnlocatedCamera()
        {
            if(PositionValid||cameraStarting||paused||anchorBusy)yield break;
            cameraStarting=true;epoch++;consensus.Reset();cameraReady=false;lastImageTime=-1;
            cameraManager.enabled=false;arSession.enabled=false;yield return null;yield return null;
            if(!paused&&!destroyed){yield return StartCamera();}cameraStarting=false;
        }
        public void RetryCamera(){if(PositionValid){ShowNotice("Vị trí đang được giữ","Để căn lại vị trí, dùng Quét lại A.");return;}StartCoroutine(RestartUnlocatedCamera());}
        string TrackingHint()
        {
            if(ARSession.notTrackingReason==NotTrackingReason.InsufficientLight)return "Khu vực thiếu sáng · Bật thêm đèn hoặc hướng về vùng sáng.";
            if(ARSession.notTrackingReason==NotTrackingReason.ExcessiveMotion)return "Máy di chuyển nhanh · Giữ máy và xoay chậm lại.";
            if(ARSession.notTrackingReason==NotTrackingReason.InsufficientFeatures)return "Cảnh ít chi tiết · Nhìn về bàn, TV hoặc thiết bị thay vì tường trơn.";
            return "Giữ camera ổn định, nhìn quanh chậm. Map và mốc A–E không bị xóa.";
        }
        string DistanceToSelected(){var m=map.Marker(Selected);if(m==null)return "";var p=m.position;p.y=MapPosition.y;return " · cách ngang "+Vector3.Distance(MapPosition,p).ToString("F1")+" m";}
        public string TargetHint
        {
            get
            {
                if(!PositionValid||mapRoot==null)return "A–E đã lưu · Quét thẻ A ở bàn máy tính để bắt đầu";
                if(Navigation.Active)return "Tuyến tới "+Navigation.Target+" · Đường xanh trên map; quan sát lối đi thực tế";
                var m=map.Marker(Selected);if(m==null)return "";var p=arCamera.WorldToViewportPoint(mapRoot.TransformPoint(m.position));
                return Selected+DistanceToSelected()+" · "+(p.z<=0?"Mốc ở phía sau camera":p.x<.08f?"Xoay camera sang trái để thấy mốc":p.x>.92f?"Xoay camera sang phải để thấy mốc":p.y<.08f?"Hạ camera để thấy mốc":p.y>.88f?"Nâng camera để thấy mốc":"Mốc trong khung nhìn");
            }
        }
        void Query()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            cameraAttempts++;
            try
            {
                if(!AtlasNativeCamera.TryCapturePixels(arSession,cameraManager,timestamp,ref nativeLuma,out var calibrated,out var reason))
                {
                    // Save independent, explicitly uncalibrated evidence if the native path fails.
                    if(Recording&&diagnosticTask==null&&cameraManager.TryAcquireLatestCpuImage(out var image))
                    {
                        try{if(AtlasImageSampling.Copy(image,ref pixels,out int dw,out int dh))
                            SaveDiagnostic(pixels,new DiagnosticFrame{utc=DateTime.UtcNow.ToString("O"),imageSeconds=image.timestamp,
                                width=dw,height=dh,rawWidth=image.width,rawHeight=image.height,epoch=epoch,frameNanoseconds=timestamp});}
                        finally{image.Dispose();}
                    }
                    CameraFailure(reason);return;
                }
                cameraAccepted++;
                if(nativeSession!=0&&nativeSession!=calibrated.SessionToken)Invalidate("NATIVE_SESSION_CHANGED");
                nativeSession=calibrated.SessionToken;
                double nativeImageTime=calibrated.ImageTimestampNs*1e-9;
                if(nativeImageTime<=lastImageTime)return;
                if(!calibrated.TryGetWorldCameraPose(sessionSpace,out var exposure)){CameraFailure("NON_RIGID_SESSION_ORIGIN");return;}
                if(!AtlasImageSampling.Copy(nativeLuma,calibrated.Width,calibrated.Height,ref pixels,out int w,out int h)){CameraFailure("CPU_LUMA_COPY_FAILED");return;}
                var k=AtlasImageSampling.ScaleIntrinsics(calibrated.Intrinsics,calibrated.Width,calibrated.Height,w,h);
                if(!float.IsFinite(k.x)||!float.IsFinite(k.y)||k.x<10||k.y<10){CameraFailure("INVALID_SCALED_INTRINSICS");return;}
                SaveDiagnostic(pixels,new DiagnosticFrame{utc=DateTime.UtcNow.ToString("O"),source="ARCORE_SINGLE_OWNED_IMAGE",poseUsable=true,
                    imageSeconds=nativeImageTime,frameNanoseconds=calibrated.FrameTimestampNs,width=w,height=h,
                    rawWidth=calibrated.Width,rawHeight=calibrated.Height,epoch=epoch,intrinsics=k,
                    sessionCameraPosition=calibrated.SessionCameraPose.position,sessionCameraRotation=calibrated.SessionCameraPose.rotation});
                taskCamera=exposure;taskEpoch=epoch;taskAt=Time.realtimeSinceStartup;taskImageTime=nativeImageTime;lastImageTime=taskImageTime;
                taskFrameTimestamp=calibrated.FrameTimestampNs;
                taskWidth=w;taskHeight=h;taskK=k;taskImageFile=null;taskImageSave=null;
                var bytes=pixels;var folder=diagnostics?.Folder;var filename=taskImageFile;
                Log("QUERY","Existing QR A calibration; one owned ARCore image + frame camera pose/intrinsics");
                poseTask=Task.Run(()=>Jni(()=>
                {
                    using(var engine=new AndroidJavaClass("com.teep.atlasar.MarkerLocalizer"))
                    {
                        string result=engine.CallStatic<string>("locate",bytes,w,h,(double)k.x,(double)k.y,(double)k.z,(double)k.w);
                        return result;
                    }
                }));
            }
            catch(Exception error){Log("CAMERA_ERROR",error.GetType().Name);}
#endif
        }
        void SaveDiagnostic(byte[] frame,DiagnosticFrame description)
        {
            if(!Recording||diagnosticTask!=null)return;
            string name=diagnostics.ReserveImage(Time.realtimeSinceStartup);if(name==null)return;
            var copy=(byte[])frame.Clone();var folder=diagnostics.Folder;diagnosticImage=name;
            string metadata=JsonUtility.ToJson(description);int w=description.width,h=description.height;
            diagnosticTask=Task.Run(()=>Jni(()=>{using(var engine=new AndroidJavaClass("com.teep.atlasar.SceneLocalizer"))
            {string result=engine.CallStatic<string>("saveFrame",folder,name,copy,w,h);
                if(result.StartsWith("OK:"))File.WriteAllText(Path.Combine(folder,name+".json"),metadata);return result;}}));
        }
        void CameraFailure(string reason)
        {
            cameraRejected++;
            if(Time.realtimeSinceStartup>=nextCameraError){nextCameraError=Time.realtimeSinceStartup+5;Log("CAMERA_CALIBRATION_REJECTED",reason);}
            if(!PositionValid){Status="Đang chờ khung hình AR hợp lệ";Hint="Giữ máy ổn định. Nếu kéo dài, bấm Xuất ZIP để kiểm tra.";}
        }
        void Process(string raw,float now)
        {
            VisualResult result;try{result=JsonUtility.FromJson<VisualResult>(raw);}catch{result=null;}
            if(result==null){consensus.Reset();Log("VISUAL_ERROR",raw);return;}
            Log("VISUAL_RESULT",raw,result);
            if(!result.valid||result.position==null||result.position.Length!=3||result.forward?.Length!=3||result.up?.Length!=3)
            {
                consensus.Reset();if(!PositionValid){Status="Hướng camera vào thẻ A cũ";Hint=MarkerHint(result.reason);}return;
            }
            var p=V(result.position);if(!AtlasMap.Finite(p)||!map.InRoom(p)){consensus.Reset();Hint="Chưa khớp phạm vi phòng · Giữ trọn thẻ A trong camera.";return;}
            var f=V(result.forward);var u=V(result.up);if(!AtlasMap.Finite(f)||!AtlasMap.Finite(u)||Mathf.Abs(f.sqrMagnitude-1)>.05f||Mathf.Abs(u.sqrMagnitude-1)>.05f||Mathf.Abs(Vector3.Dot(f,u))>.1f){consensus.Reset();return;}
            if(!AtlasPoseMath.TryMapFrame(taskCamera,new Pose(p,Quaternion.LookRotation(f,u)),out var frame))
            {consensus.Reset();if(!PositionValid)Hint="Hướng camera chưa khớp · Giữ máy và nhìn quanh chậm.";return;}
            if(anchor!=null&&PositionValid)
            {
                float shift=Vector3.Distance(frame.position,anchor.transform.position),angle=Quaternion.Angle(frame.rotation,anchor.transform.rotation);
                if(shift<.35f&&angle<5){lastGoodVisual=now;consensus.Reset();return;}
                Log("VISUAL_ANCHOR_CONFLICT",$"shift={shift:F3}; angle={angle:F2}");
                if(consensus.Offer(frame,taskImageTime,taskAt,now))
                {Invalidate("REPEATED_VISUAL_CONFLICT");Status="Đang đối chiếu lại vị trí";Hint="Cảnh camera khác mốc AR đang giữ · Giữ máy ổn định để xác nhận lại.";}
                return;
            }
            bool confirmed=consensus.Offer(frame,taskImageTime,taskAt,now);
            Status=$"Đang xác nhận vị trí · {Confirmation}/3";Hint="Đã thấy A · Giữ nguyên thẻ, giữ máy ổn định để xác nhận.";
            if(confirmed){var accepted=consensus.Pose;consensus.Reset();Accept(accepted,epoch);}
        }
        static string MarkerHint(string reason)
        {
            if(reason!=null&&(reason.Contains("OTHER")||reason.Contains("WRONG")))return "Đây chưa phải thẻ A · Bắt đầu tại bàn máy tính có thẻ A.";
            if(reason=="LOW_LIGHT")return "Thẻ thiếu sáng · Bật đèn hoặc tránh để bóng tay che thẻ A.";
            if(reason!=null&&reason.Contains("SMALL"))return "Đưa máy gần hơn, khoảng 40–80 cm; thấy trọn khung thẻ A.";
            if(reason!=null&&reason.Contains("AMBIG"))return "Nghiêng máy nhẹ để xác nhận hướng thẻ; không di chuyển tờ giấy.";
            return "Quét A cũ tại bàn máy tính · Thấy trọn khung hoa văn, đủ sáng; giấy nằm ngang vẫn quét được.";
        }
        public void StartNavigation()
        {
            if(!PositionValid){ShowNotice("Cần căn vị trí bằng A trước","Giữ đích đã chọn · Hướng camera vào thẻ A cũ trên bàn máy tính.");ui.SetCamera(true);return;}
            bool ok=Navigation.Begin(MapPosition,Selected,Time.realtimeSinceStartup);Log(ok?"ROUTE_STARTED":"ROUTE_UNAVAILABLE",Selected+"; "+Navigation.Instruction);
            if(ok)ui.SetCamera(true);else ShowNotice("Chưa có tuyến phù hợp",Navigation.Instruction);
        }
        public void SelectTarget(string id){ui.Select(id);}
        public void ChooseAlternative()
        {
            if(!PositionValid||!Navigation.Active){ShowNotice("Chọn đích và bắt đầu chỉ đường trước","Các lối khác được tính từ vị trí hiện tại sau khi căn A.");return;}
            bool changed=Navigation.ChooseAlternative(MapPosition,Selected,Time.realtimeSinceStartup);
            Log(changed?"ROUTE_ALTERNATIVE":"ROUTE_NO_ALTERNATIVE",Navigation.RouteChoiceInfo);
            if(!changed)ShowNotice("Giữ tuyến hiện tại",Navigation.RouteChoiceInfo);
        }
        public void StopNavigation(){Navigation.Stop();Log("ROUTE_STOPPED","Owner stopped route");}
        static string HintFor(string reason,int features)
        {
            if(reason=="LOW_LIGHT")return "Khu vực thiếu sáng · Bật thêm đèn hoặc hướng về vùng sáng.";
            if(reason!=null&&reason.StartsWith("ENGINE_ERROR"))return "Bộ nhận cảnh gặp lỗi · Xuất log để kiểm tra.";
            if(reason=="LOW_TEXTURE"||features<40)return "Cảnh ít chi tiết · Hướng camera về bàn, màn hình hoặc thiết bị.";
            return "Quay camera chậm về cụm bàn và thiết bị; tránh chỉ nhìn tường trơn.";
        }
        async void Accept(Pose pose,int generation)
        {
            if(anchorBusy||destroyed||!Tracking)return;anchorBusy=true;
            try
            {
                var added=await anchorManager.TryAddAnchorAsync(pose);
                if(!added.status.IsSuccess()||added.value==null){if(!destroyed&&generation==epoch){Status="Chưa giữ được mốc AR";Hint="Giữ máy ổn định để app thử lại.";}return;}
                if(destroyed||generation!=epoch||!Tracking){Destroy(added.value.gameObject);return;}
                if(anchor!=null)Destroy(anchor.gameObject);anchor=added.value;
                mapRoot=new GameObject("XR Space — approved map aligned by QR A",typeof(Immersal.XR.XRSpace)).transform;mapRoot.SetParent(anchor.transform,false);Navigation.Bind(mapRoot);immersalNavigation?.Bind(mapRoot);lastGoodVisual=Time.realtimeSinceStartup;recovery.Reset();
                Log("LOCALIZED","Three consistent existing printed A poses accepted; approved A-E map anchored without moving marker coordinates");
            }
            catch(Exception error){Log("ANCHOR_ERROR",error.GetType().Name);}
            finally{anchorBusy=false;}
        }
        void CreatePins()
        {
            labels.Clear();dots.Clear();var material=Resources.Load<Material>("Atlas/PinMaterial");var font=Resources.Load<Font>("Atlas/Bold");
            foreach(var m in map.markers)
            {
                var node=new GameObject("Navigation Target "+m.id,typeof(AtlasNavigationTarget)).transform;node.SetParent(mapRoot,false);node.localPosition=m.position;
                var target=node.GetComponent<AtlasNavigationTarget>();target.markerId=m.id;target.savedMapPosition=m.position;
                target.arrivalMapPosition=Navigation.Arrival(m.id);var arrival=new GameObject("Arrival floor "+m.id).transform;arrival.SetParent(node,false);arrival.localPosition=target.arrivalMapPosition-m.position;target.arrivalPose=arrival;
                var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="Exact marker centre";sphere.transform.SetParent(node,false);sphere.transform.localScale=Vector3.one*.09f;Destroy(sphere.GetComponent<Collider>());var renderer=sphere.GetComponent<Renderer>();renderer.sharedMaterial=material;dots.Add(renderer);
                var label=new GameObject("Label "+m.id,typeof(RectTransform),typeof(Canvas));label.transform.SetParent(node,false);label.transform.localPosition=Vector3.up*.18f;label.transform.localScale=Vector3.one*.005f;
                var canvas=label.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=arCamera;var rect=label.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(110,64);
                var bg=new GameObject("Label background",typeof(RectTransform),typeof(Image));bg.transform.SetParent(label.transform,false);var br=bg.GetComponent<RectTransform>();br.anchorMin=Vector2.zero;br.anchorMax=Vector2.one;br.offsetMin=br.offsetMax=Vector2.zero;bg.GetComponent<Image>().color=new Color32(22,91,232,235);bg.GetComponent<Image>().raycastTarget=false;
                var text=new GameObject("ID",typeof(RectTransform),typeof(Text));text.transform.SetParent(bg.transform,false);var tr=text.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;var t=text.GetComponent<Text>();t.font=font;t.fontSize=40;t.text=m.id;t.color=Color.white;t.alignment=TextAnchor.MiddleCenter;t.raycastTarget=false;labels.Add(label.transform);
            }
        }
        public void Relocate(){Invalidate("USER_RELOCATE_A");ui.SetCamera(true);Status="Căn lại tại bàn máy tính A";Hint="Quét thẻ A cũ · Tọa độ và đích đã chọn được giữ lại.";if((!cameraManager.enabled||engineFailed)&&initializeTask==null){engineFailed=false;StartCoroutine(EnsureCamera());}}
        void Invalidate(string reason)
        {
            epoch++;consensus.Reset();recovery.Reset();PositionValid=false;lastArAt=0;cameraReady=false;lastImageTime=-1;
            Navigation?.Bind(null);immersalNavigation?.Bind(null);Status="Cần căn vị trí bằng thẻ A";Hint="Quét thẻ A cũ tại bàn máy tính; không cần in mã mới.";
            Navigation?.Tick(false,MapPosition,MapForward,arCamera,ui.cameraMode,Time.realtimeSinceStartup);
            if(mapRoot!=null)mapRoot.gameObject.SetActive(false);if(anchor!=null)Destroy(anchor.gameObject);anchor=null;mapRoot=null;labels.Clear();dots.Clear();Log("INVALIDATED",reason);
        }
        void OnApplicationPause(bool value)
        {
            if(paused==value)return;paused=value;if(map==null)return;
            if(value)
            {
                pauseAt=Time.realtimeSinceStartup;epoch++;consensus.Reset();PositionValid=false;cameraReady=false;lastArAt=0;
                if(anchor!=null)recovery.Begin(pauseAt);if(mapRoot!=null)mapRoot.gameObject.SetActive(false);
                Navigation?.Tick(false,MapPosition,MapForward,arCamera,ui.cameraMode,pauseAt);Log("APP_PAUSED","Hide guidance; keep same-session anchor pending recovery");diagnostics?.Flush();
            }
            else
            {
                lastArAt=0;wasTracking=false;lastImageTime=-1;
                if(Time.realtimeSinceStartup-pauseAt>=8)Invalidate("LONG_APP_PAUSE_NEW_FIX_REQUIRED");
                Log("APP_RESUMED","Await live tracking + same anchor before restoring guidance");StartCoroutine(EnsureCamera());
            }
        }
        void OnDestroy(){destroyed=true;epoch++;ARSession.stateChanged-=SessionStateChanged;Application.lowMemory-=LowMemory;Navigation?.Dispose();if(cameraManager!=null)cameraManager.frameReceived-=Frame;diagnostics?.Flush();}
        void Log(string type,string detail,VisualResult r=null)
        {
            if(diagnostics==null||arCamera==null)return;
            bool queryMetadata=type=="QUERY"||type=="VISUAL_RESULT"||type=="VISUAL_DISCARDED"||type=="IMAGE_SAVE";
            diagnostics.Append(new AtlasLog{utc=DateTime.UtcNow.ToString("O"),type=type,mapId=map.id,detail=detail,version=Application.version,seconds=Time.realtimeSinceStartup,
                cameraCheck=type=="QUERY"||type=="CAMERA_CALIBRATION_REJECTED"?AtlasNativeCamera.LastCheck:null,
                positionValid=PositionValid,recording=Recording,tracking=ARSession.state.ToString(),reason=ARSession.notTrackingReason.ToString(),selected=Selected,epoch=epoch,
                queryGate=QueryGate(),loader=XRGeneralSettings.Instance?.Manager?.activeLoader?.name,sessionRunning=arSession.subsystem!=null&&arSession.subsystem.running,cameraRunning=cameraManager.subsystem!=null&&cameraManager.subsystem.running,frameReady=cameraReady,frameAgeSeconds=hadFrame?Time.realtimeSinceStartup-lastFrameAt:-1,receivedFrames=receivedFrames,managedMemoryMb=GC.GetTotalMemory(false)/(1024f*1024f),navigationCameraCue=Navigation?.CameraCue,
                navigationTarget=Navigation?.Target,navigationInstruction=Navigation?.Instruction,remainingRouteMetres=Navigation?.Remaining??0,route=type=="ROUTE_STARTED"?Navigation?.Points:null,
                mapPosition=PositionValid?MapPosition:Vector3.zero,worldPosition=arCamera.transform.position,worldRotation=arCamera.transform.rotation,
                features=r?.features??0,matches=r?.matches??0,inliers=r?.inliers??0,reprojection=r?.median??0,processingMs=r?.milliseconds??0,
                imageTimestamp=queryMetadata?taskImageTime:0,width=queryMetadata?taskWidth:0,height=queryMetadata?taskHeight:0,intrinsics=queryMetadata?taskK:Vector4.zero,imageFile=queryMetadata?taskImageFile:null,imageSave=type=="VISUAL_RESULT"?taskImageSave:null,
                exposurePosition=queryMetadata?taskCamera.position:Vector3.zero,exposureRotation=queryMetadata?taskCamera.rotation:Quaternion.identity,exposureTimestampNs=queryMetadata?taskFrameTimestamp:0});
        }
        public void ToggleRecording()
        {
            if(diagnostics==null||!diagnostics.Available){ShowNotice("Không ghi được lượt thử","Kiểm tra dung lượng còn trống rồi mở lại app.");return;}
            diagnostics.Toggle();Log("RECORDING",Recording?"Owner enabled up to 12 local grayscale query images":"Owner stopped image recording");
            ShowNotice(Recording?"Đang ghi lượt thử tại máy":"Đã dừng ghi lượt thử",Recording?"Lưu tối đa 12 ảnh camera cục bộ để chẩn đoán. Không tải lên mạng.":"Bấm Xuất ZIP để gửi dữ liệu kiểm tra.");
        }
        void ShowNotice(string status,string hint){noticeStatus=status;noticeHint=hint;noticeUntil=Time.realtimeSinceStartup+4;}
        public void Export()
        {
            if(diagnostics==null||!diagnostics.Available){ShowNotice("Chưa ghi được dữ liệu kiểm tra","Kiểm tra dung lượng còn trống rồi mở lại app.");return;}
            diagnostics.Stop();pendingExport=true;ShowNotice("Đang chuẩn bị ZIP","Chờ khung hình hiện tại xử lý xong; không cần bấm lại.");
        }
        void ExportNow()
        {
            Log("EXPORT",$"attempts={cameraAttempts}; calibrated={cameraAccepted}; rejected={cameraRejected}; saved={CapturedImages}");diagnostics.Flush();
#if UNITY_ANDROID && !UNITY_EDITOR
            try{using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))using(var exporter=new AndroidJavaClass("com.teep.atlasar.AtlasExport"))
                {string result=exporter.CallStatic<string>("save",activity,diagnostics.Folder);ShowNotice(result.StartsWith("OK:")?"Đã lưu ZIP vào Download/AtlasAR":"Chưa lưu được ZIP",result.StartsWith("OK:")?"My Files → Downloads → AtlasAR để gửi file.":"Thử lại và kiểm tra dung lượng còn trống.");}}
            catch(Exception e){Log("EXPORT_FAILED",e.GetType().Name);ShowNotice("Xuất ZIP gặp lỗi","Dữ liệu trong app vẫn được giữ; thử lại.");}
#else
            Debug.Log("Atlas session data: "+diagnostics.Folder);
#endif
        }
        static Vector3 V(float[] p)=>new Vector3(p[0],p[1],p[2]);
    }
}
