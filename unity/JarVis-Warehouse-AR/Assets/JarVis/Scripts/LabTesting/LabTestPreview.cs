using System;
using System.Linq;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        public bool PreviewMode {get;private set;}
        double previewQrSince=-1;
        string previewOriginId="A";
        string PreviewLookId=>previewOriginId=="A"?"B":"A";
        public string PreviewOriginId=>previewOriginId;
        readonly LabHeadingConfirmation previewHeading=new LabHeadingConfirmation();
        public float HeadingProgress=>previewHeading.Progress;
        public void BeginPreviewDestination(string target)
        {
            BeginDestination(target);
            if(Exercise==null)return;
            previewOriginId="A";PreviewMode=true;
            floorVisual.Begin(true);
            sessionMode="PREVIEW_ESTIMATED";
            Message="Thử mũi tên · quét A, rồi đứng ở lối trước bàn A và hướng camera về bàn B. Không cần đo A–B–A.";
        }
        public void BeginPreviewRecoveryAt(string id)
        {
            if(!CanPreviewRecover||!Tracking||addingAnchor||!Map.markers.Any(m=>m.id==id))return;
            ResetCalibration();previewOriginId=id;AwaitingDirection=true;
            sessionMode="PREVIEW_MANUAL_RECOVERY_"+id;
            Message="Đứng đúng điểm "+id+", nhìn về "+PreviewLookId+" rồi giữ yên để căn lại. Đây là vị trí bạn xác nhận, chưa được camera tự chứng minh.";
        }
        void UpdatePreviewCalibration(double now)
        {
            HideGuidance();
            NavigationState="Thử mũi tên · chưa căn hướng";
            if(AwaitingDirection)
            {
                var cam=CameraOwner.arCamera.transform;var f=Vector3.ProjectOnPlane(cam.forward,Vector3.up);
                previewHeading.Observe(now,ToPoint(cam.position),f.x,f.z,Tracking);
                ScanProgress=previewHeading.Progress;
                Message="Đứng ở điểm "+previewOriginId+", nhìn về "+PreviewLookId+". Giữ yên hướng · "+Mathf.RoundToInt(HeadingProgress*100)+"%. Căn nhanh chỉ là ước lượng.";
                return;
            }
            if(decodedMarker=="A"&&now-decodedMarkerAt<.85)
            {
                if(previewQrSince<0)previewQrSince=now;
                ScanProgress=Mathf.Clamp01((float)((now-previewQrSince)/.7));
                ScanLabel="Đã nhận A · "+Mathf.FloorToInt(ScanProgress*100)+"% giữ QR trong khung";
                if(ScanProgress>=1){AwaitingDirection=true;previewQrSince=-1;}
            }
            else
            {
                previewQrSince=-1;ScanProgress=0;
                ScanLabel="Quét QR A · giữ máy và thẻ yên một lát";
            }
        }
        async void ConfirmPreviewDirection()
        {
            if(!PreviewMode||!AwaitingDirection||addingAnchor)return;
            if(!Tracking){Message="Đợi tracking phục hồi rồi xác nhận; chưa cần quét lại A.";return;}
            if(HeadingProgress<1){Message="Nhìn về "+PreviewLookId+" và giữ yên một lát trước khi xác nhận.";return;}
            var camera=CameraOwner.arCamera.transform;
            var forward=Vector3.ProjectOnPlane(camera.forward,Vector3.up);
            if(forward.magnitude<.35f){Message="Nâng camera nhìn về "+PreviewLookId+", đừng chĩa xuống thẻ.";return;}
            var a=Map.markers.First(m=>m.id==previewOriginId).access;
            var b=Map.markers.First(m=>m.id==PreviewLookId).access;
            var known=ToVector(b)-ToVector(a);known.y=0;
            if(known.sqrMagnitude<1)return;
            float yaw=Vector3.SignedAngle(known,forward,Vector3.up);
            var rotation=Quaternion.Euler(0,yaw,0);
            // Explicit preview assumption: phone held 1.35 m above the floor at A's standing point.
            var cameraPosition=camera.position;
            var origin=cameraPosition-rotation*new Vector3(a.x,1.35f,a.z);
            int request=epoch;addingAnchor=true;
            try
            {
                var result=await CameraOwner.anchors.TryAddAnchorAsync(new Pose(origin,rotation));
                if(!result.status.IsSuccess()){Message="Chưa giữ được mốc AR; giữ máy rồi bấm xác nhận lại.";return;}
                if(this==null||request!=epoch||paused||!Tracking){CameraOwner.anchors.TryRemoveAnchor(result.value);return;}
                AdoptNavigationAnchor(result.value);
                navigationTrusted=true;recovering=false;RecoveryNeedsMarker=false;journeyStarted=true;
                poseEstimate.Reset();ResetRouteState();AwaitingDirection=false;lostSince=-1;lastPose=null;plannedFrom=null;nextPlan=0;Route.Clear();
                Message="Đang thử mũi tên · vị trí ước lượng. Quan sát lối đi thật; chưa dùng để đánh giá sai số map.";
                NavigationState="Thử mũi tên · vị trí ước lượng";
            }
            catch(Exception){Message="Chưa căn nhanh được; giữ máy rồi bấm xác nhận lại.";}
            finally{addingAnchor=false;}
        }
    }
}
