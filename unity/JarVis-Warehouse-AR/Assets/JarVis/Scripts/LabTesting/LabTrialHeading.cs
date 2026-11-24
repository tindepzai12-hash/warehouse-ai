using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    // The accepted trial stores approximate positions, not a verified orientation of every printed sheet.
    public sealed partial class LabTestSession
    {
        bool trialHeadingPending;string trialOrigin,trialLook,trialAnchorKey;int trialEpoch;
        Vector3 trialMarkerInAnchor;
        public string AlignmentMethod {get;private set;}="NOT_CONFIRMED";
        public float AlignmentMarkerResidual {get;private set;}=-1;
        public string TrialAlignmentStatus {get;private set;}="NOT_CONFIRMED";
        public string DirectionReferenceId=>trialHeadingPending?trialLook:PreviewLookId;
        public string DirectionOriginId=>trialHeadingPending?trialOrigin:previewOriginId;
        void RequestTrialHeading(string id)
        {
            trialOrigin=id;trialLook=Map.markers.Where(m=>m.id!=id).OrderByDescending(m=>LabPaths.Distance(m.access,Map.markers.First(a=>a.id==id).access)).First().id;
            trialAnchorKey=AnchorKey;trialEpoch=epoch;
            trialMarkerInAnchor=anchor.transform.InverseTransformPoint(MapFrame.TransformPoint(ToVector(Map.markers.First(m=>m.id==id).centre)));
            trialHeadingPending=true;AwaitingDirection=true;previewHeading.Reset();TrialAlignmentStatus="MANUAL_HEADING_REQUIRED";
            ResetCameraEvidence();NavigationState="Căn hướng tại "+id;HideGuidance();
        }
        void UpdateTrialHeading(double now)
        {
            if(!Located||trialEpoch!=epoch||trialAnchorKey!=AnchorKey)
            {previewHeading.Reset();ScanProgress=0;Message="Mốc AR chưa ổn định. Giữ máy, nhìn lại vùng vừa quét; nếu cần hãy quét mốc gần bạn. Đích vẫn được giữ.";return;}
            HideGuidance();var cam=CameraOwner.arCamera.transform;var f=Vector3.ProjectOnPlane(cam.forward,Vector3.up);
            previewHeading.Observe(now,ToPoint(cam.position),f.x,f.z,Tracking);ScanProgress=HeadingProgress;
            NavigationState="Đã nhận "+trialOrigin+" · cần xác nhận hướng";
            Message="Đứng gần thẻ "+trialOrigin+", ngắm tâm thẻ "+trialLook+". Giữ yên "+Mathf.FloorToInt(HeadingProgress*100)+"%, rồi xác nhận. % là độ ổn định, không kiểm chứng bạn ngắm đúng thẻ.";
        }
        public void ChangeDirectionReference()
        {
            if(!trialHeadingPending)return;
            var choices=Map.markers.Where(m=>m.id!=trialOrigin&&LabPaths.Distance(m.access,Map.markers.First(a=>a.id==trialOrigin).access)>=2).Select(m=>m.id).ToArray();
            int index=Array.IndexOf(choices,trialLook);trialLook=choices[(index+1)%choices.Length];previewHeading.Reset();
        }
        void ConfirmTrialHeading()
        {
            if(!trialHeadingPending||!AwaitingDirection||!Located||!Tracking||HeadingProgress<1||trialEpoch!=epoch||trialAnchorKey!=AnchorKey)return;
            var camera=CameraOwner.arCamera.transform;var f=Vector3.ProjectOnPlane(camera.forward,Vector3.up);
            var a=Map.markers.First(m=>m.id==trialOrigin).centre;var b=Map.markers.First(m=>m.id==trialLook).centre;
            var observed=anchor.transform.TransformPoint(trialMarkerInAnchor);
            if(!LabMarkerHeadingAlignment.TrySolve(a,b,ToPoint(observed),ToPoint(camera.position),f.x,f.z,
                out var yaw,out var translation,out var reason))
            {Message="Chưa căn được hướng: đứng gần thẻ vừa quét, ngắm thẻ tham chiếu ở xa và thử lại. Đích vẫn được giữ.";AlignmentMethod=reason;previewHeading.Reset();return;}
            MapFrame.SetPositionAndRotation(ToVector(translation),Quaternion.Euler(0,yaw,0));
            AlignmentMarkerResidual=Vector3.Distance(MapFrame.TransformPoint(ToVector(a)),observed);
            AlignmentMethod=reason;
            trialHeadingPending=false;AwaitingDirection=false;TrialAlignmentStatus="MANUAL_APPROXIMATE_"+trialOrigin+"_LOOK_"+trialLook;
            poseEstimate.Reset();ResetRouteState();ResetCameraEvidence();lastPose=null;plannedFrom=null;plannedTarget=null;nextPlan=0;
            sessionMode="TRIAL_QR_CENTRE_AND_MANUAL_BEARING";Message="Đã giữ tâm QR "+trialOrigin+" và căn hướng thử. Đối chiếu chấm vị trí với lối thật; đích vẫn là "+Exercise?.Target+".";
        }
        void ResetTrialHeading(){trialHeadingPending=false;TrialAlignmentStatus="NOT_CONFIRMED";AlignmentMethod="NOT_CONFIRMED";AlignmentMarkerResidual=-1;previewHeading.Reset();}
    }
}
