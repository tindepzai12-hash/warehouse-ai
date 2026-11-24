using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using JarVis.Warehouse.VisualMaps;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        readonly LabTrackingRecovery trackingRecovery=new LabTrackingRecovery();
        readonly LabDepthFrames depthFrames=new LabDepthFrames();
        public bool TryDepthCameraFrame(double timestamp,double now,out LabDepthCameraFrame frame,out string reason)
        {
            var camera=CameraOwner.arCamera.transform;
            return depthFrames.Select(timestamp,now,epoch,camera.position,camera.rotation,out frame,out reason);
        }
        double cameraFrameAt=-1,nextAnchorRebase;
        bool rebaseBusy;
        long cameraSampleId,lastProviderNs=-1,recoverySampleId=-1;double providerAdvancedAt=-1;bool providerTimestampAvailable;
        public bool ProviderFrameFresh=>!providerTimestampAvailable||providerAdvancedAt>=0&&Time.realtimeSinceStartupAsDouble-providerAdvancedAt<=1.5;
        public Matrix4x4 CameraDisplayTransform {get;private set;}
        public bool CameraDisplayTransformReady {get;private set;}
        public double CameraProviderSeconds=>lastProviderNs<0?-1:lastProviderNs*1e-9;
        Transform mapFrame;
        Transform MapFrame=>mapFrame!=null?mapFrame:anchor!=null?anchor.transform:null;
        public double CameraFrameAge=>cameraFrameAt<0?-1:Time.realtimeSinceStartupAsDouble-cameraFrameAt;
        public bool RecoveringTracking=>recovering&&!RecoveryNeedsMarker;
        public float RecoveryProgress=>trackingRecovery.Progress;
        public bool CanPreviewRecover=>PreviewMode&&journeyStarted&&Exercise!=null&&!Exercise.Complete;
        public void OpenPreviewRecovery()
        {
            if(!CanPreviewRecover)return;
            RequireTrackingReference("USER_REQUESTED_ALIGNMENT");ui.OpenMap(false);
        }
        public bool MarkerScanRequested=>fixedSurvey||SettingUp||((Locating||Exercise!=null&&!Exercise.Complete)&&!Located&&!AwaitingDirection&&
            (PreviewMode?!journeyStarted:!recovering||RecoveryNeedsMarker));
        string AnchorKey=>anchor!=null?anchor.trackableId.ToString():"";
        void CameraFrameReceived(ARCameraFrameEventArgs frame)
        {
            cameraFrameAt=Time.realtimeSinceStartupAsDouble;
            if(frame.displayMatrix.HasValue){CameraDisplayTransform=frame.displayMatrix.Value;CameraDisplayTransformReady=true;}
            if(frame.timestampNs.HasValue)
            {
                providerTimestampAvailable=true;long ns=frame.timestampNs.Value;
                if(lastProviderNs>=0&&ns<lastProviderNs){depthFrames.Clear();if(navigationTrusted&&journeyStarted)RequireTrackingReference("PROVIDER_CLOCK_CHANGED");}
                if(ns!=lastProviderNs){lastProviderNs=ns;providerAdvancedAt=cameraFrameAt;cameraSampleId++;}
                if(frame.displayMatrix.HasValue&&CameraOwner?.arCamera!=null)
                {
                    var camera=CameraOwner.arCamera;
                    depthFrames.Record(new LabDepthCameraFrame{timestamp=ns*1e-9,received=cameraFrameAt,epoch=epoch,
                        projection=frame.projectionMatrix??camera.projectionMatrix,cameraToWorld=camera.cameraToWorldMatrix,
                        display=frame.displayMatrix.Value,position=camera.transform.position,rotation=camera.transform.rotation});
                }
            }
            else {providerTimestampAvailable=false;cameraSampleId++;}
        }
        void ConfigureTrackingRuntime()
        {
            CameraOwner.cameraManager.frameReceived+=CameraFrameReceived;
            CameraOwner.cameraManager.autoFocusRequested=true;
            // VIO remains enabled; this app does not need to detect floor planes for the cue.
            if(NewScan){if(CameraOwner.planes!=null)CameraOwner.planes.enabled=false;if(CameraOwner.raycasts!=null)CameraOwner.raycasts.enabled=false;}
        }
        void SetMarkerScanning(bool enabled)
        {
            if(imageManager==null||Application.isEditor)return;
            if(NewScan)
            {
                if(markerScanWanted==enabled)return;markerScanWanted=enabled;
                scanAttemptAt=enabled?Time.realtimeSinceStartupAsDouble:-1;
                SuspendMarkerPose();decodedMarker=null;decodedMarkerCount=0;setupGateMarker=null;calibration.Reset();return;
            }
            if(imageManager.enabled==enabled)return;
            scanAttemptAt=enabled?Time.realtimeSinceStartupAsDouble:-1;
            imageManager.enabled=enabled;visible.Clear();decodedMarker=null;decodedMarkerCount=0;setupGateMarker=null;calibration.Reset();
        }
        void CheckRecoveryDeadline(double now)
        {
            if(recovering&&!RecoveryNeedsMarker&&trackingRecovery.Tick(now)=="NEED_REFERENCE")RequireTrackingReference(trackingRecovery.Reason);
        }
        void BeginTrackingInterruption(double now)
        {
            if(lostSince<0)lostSince=now;
            anchorStableSince=-1;
            if(anchor!=null&&navigationTrusted&&!trackingRecovery.Active)
                trackingRecovery.Begin(now,AnchorKey,poseEstimate.Current.accepted??LastKnownPosition,PreviewMode);
            recovering=anchor!=null&&navigationTrusted;
            trackingRecovery.BreakStability();recoverySampleId=-1;
        }
        void RequireTrackingReference(string reason)
        {
            recovering=false;navigationTrusted=false;RecoveryNeedsMarker=true;HideGuidance();
            NavigationState="Cần căn lại vị trí";
            Message=PreviewMode?"Giữ đích cũ. Mở map, chọn A/B/C đúng nơi đang đứng để căn nhanh lại.":"Giữ đích cũ. Quét một mốc A–E đã lưu gần bạn để tiếp tục.";
            routeError=reason;
        }
        void TryRecoverTracking(double now)
        {
            if(trackingRecovery.Tick(now)=="NEED_REFERENCE"){RequireTrackingReference(trackingRecovery.Reason);return;}
            bool healthy=Tracking&&anchor!=null&&anchor.trackingState==TrackingState.Tracking;
            if(!trackingRecovery.Active){BeginTrackingInterruption(now);if(!trackingRecovery.Active){RequireTrackingReference("ANCHOR_MISSING");return;}}
            if(recoverySampleId==cameraSampleId)return;recoverySampleId=cameraSampleId;
            var candidate=anchor!=null?ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position)):null;
            LabPoint accepted=null;string floorReason="MISSING";
            bool onFloor=candidate!=null&&floorRouter.TryMarginProjection(candidate,trackingRecovery.Origin,PreviewMode?.30f:.15f,out accepted,out floorReason);
            var localForward=anchor!=null?MapFrame.InverseTransformDirection(CameraOwner.arCamera.transform.forward):Vector3.forward;
            float heading=LabNavigationEstimate.HorizontalHeading(localForward.x,localForward.z,LastKnownHeading,out _);
            string result=trackingRecovery.Observe(now,healthy,AnchorKey,candidate,heading,onFloor);
            NavigationState=trackingRecovery.Stage=="MAP_VALIDATING"?"Đang đối chiếu vị trí với map":"Khôi phục · "+Mathf.FloorToInt(RecoveryProgress*100)+"% ổn định";
            Message=healthy?"Đứng yên một lát, nhìn lại vùng vừa đi. Đích và tuyến vẫn được giữ.":TrackingCoach();
            if(result=="NEED_REFERENCE"){RequireTrackingReference(trackingRecovery.Reason);return;}
            if(result!="READY")return;
            string error=floorRouter.Find(trackingRecovery.Origin,accepted,out var connection);
            if(error!=null||LabPaths.Length(connection)>trackingRecovery.Budget+.30f){RequireTrackingReference("RECOVERY_PATH_IMPLAUSIBLE");return;}
            recovering=false;lostSince=-1;lastPose=null;nextPlan=0;needsRoutePlan=true;poseEstimate.Reset();
            trackingRecovery.Reset();RecoveryNeedsMarker=false;routeError=null;
            Message="Đã phục hồi theo mốc AR cũ; vị trí vẫn cần đối chiếu với lối thực.";
        }
        string TrackingCoach()
        {
            if(CameraFrameAge>1.5||!ProviderFrameFresh)return "Chưa có khung camera mới. Giữ app ở trước; đích vẫn được giữ.";
            if(ARSession.notTrackingReason==NotTrackingReason.InsufficientLight)return "Khu vực thiếu ánh sáng. Hướng máy sang vùng sáng hơn, giữ chậm; đích vẫn được giữ.";
            if(ARSession.notTrackingReason==NotTrackingReason.InsufficientFeatures)return "Camera thiếu điểm bám. Nhìn vào bàn, góc tường hoặc đồ vật có chi tiết; tránh chỉ nhìn tường trống.";
            return VisualMapCoach.TrackingHint(ARSession.notTrackingReason)+" Đích vẫn được giữ.";
        }
        void AdoptNavigationAnchor(ARAnchor value)
        {
            RemoveAnchor();anchor=value;mapFrame=new GameObject("Lab map coordinate frame").transform;mapFrame.SetParent(anchor.transform,false);
            trackingRecovery.Reset();nextAnchorRebase=Time.realtimeSinceStartupAsDouble+3;
        }
        async void MaybeRebaseAnchor(double now)
        {
            if(rebaseBusy||now<nextAnchorRebase||!NavigationPoseValid||recovering||SettingUp||mapFrame==null)return;
            if(Vector3.Distance(CameraOwner.arCamera.transform.position,anchor.transform.position)<6.5f)return;
            rebaseBusy=true;nextAnchorRebase=now+10;int request=epoch;var old=anchor;ARAnchor replacement=null;
            try
            {
                var here=Position;var ground=MapFrame.TransformPoint(new Vector3(here.x,0,here.z));
                var result=await CameraOwner.anchors.TryAddAnchorAsync(new Pose(ground,Quaternion.identity));
                if(!result.status.IsSuccess())return;replacement=result.value;
                if(this==null||request!=epoch||paused||old!=anchor||!Located||replacement.trackingState!=TrackingState.Tracking)return;
                var before=MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position);var beforeRotation=mapFrame.rotation;rebaseTrace="REPARENT";
                mapFrame.SetParent(replacement.transform,true);
                var after=MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position);
                if(Vector3.Distance(before,after)>.02f||Quaternion.Angle(beforeRotation,mapFrame.rotation)>.1f){mapFrame.SetParent(old.transform,true);rebaseTrace="ROLLBACK";return;}
                rebaseTrace="COMMIT";anchor=replacement;replacement=null;CameraOwner.anchors.TryRemoveAnchor(old);
                RecordNavigationTrace(Time.realtimeSinceStartupAsDouble);
            }
            catch(Exception){nextAnchorRebase=Time.realtimeSinceStartupAsDouble+15;}
            finally{if(replacement!=null&&CameraOwner!=null)CameraOwner.anchors.TryRemoveAnchor(replacement);rebaseBusy=false;}
        }
    }
}
