using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        sealed class MarkerPoseJob { public string reference;public Observation sample;public float[] quad; }
        Observation decodedObservation;
        bool markerScanWanted;
        string markerPoseId;
        double markerIdentityAt=-1;
        void SuspendMarkerPose()
        {
            if(imageManager!=null)imageManager.enabled=false;
            markerPoseId=null;markerIdentityAt=-1;visible.Clear();decodedObservation=null;
        }
        bool SelectMarkerPose(string id,double now)
        {
            if(!markerScanWanted||imageManager==null||!LabPrintedCard.Id(id))return false;
            markerIdentityAt=now;
            if(markerPoseId==id&&imageManager.enabled)return false;
            SuspendMarkerPose();
            var library=Resources.Load<XRReferenceImageLibrary>("LabTest/LabFixedMarker-"+id);
            if(library==null||library.count!=1||library[0].name!=id)
            {markerPoseReason="MISSING_SINGLE_CARD_LIBRARY";Message="Thiếu ảnh nhận diện riêng của mốc "+id+". Chưa ghi vị trí.";return true;}
            // Read the QR identity first. Only that reference may influence image tracking.
            imageManager.referenceLibrary=library;markerPoseId=id;markerIdentityAt=now;imageManager.enabled=true;
            calibration.Reset();lastGateUpdate=-1;setupGateMarker=null;ScanProgress=0;return true;
        }
        bool TryMarkerViewport(double now,out int width,out int height,out MarkerPoseJob[] jobs)
        {
            width=height=0;jobs=Array.Empty<MarkerPoseJob>();
            var camera=CameraOwner.arCamera;
            if(paused||!Tracking||!CameraDisplayTransformReady||CameraFrameAge>.3)return false;
            float aspect=camera.aspect;if(aspect<=0)return false;
            height=aspect<=1?960:Mathf.RoundToInt(960/aspect);width=Mathf.RoundToInt(height*aspect);
            if(width<1||height<1)return false;
            var candidates=new List<MarkerPoseJob>();
            foreach(var entry in visible)
            {
                var seen=entry.Value;if(!seen.tracking||now-seen.updated>.5||seen.width<=0)continue;
                // Five similar templates can all describe the same physical paper. Warp it once.
                if(candidates.Any(j=>Vector3.Distance(j.sample.pose.position,seen.pose.position)<.05f&&Quaternion.Angle(j.sample.pose.rotation,seen.pose.rotation)<8))continue;
                float halfWidth=seen.width*.5f,halfHeight=(seen.height>0?seen.height:seen.width*1.2f)*.5f;
                var corners=new[]{new Vector3(-halfWidth,0,halfHeight),new Vector3(halfWidth,0,halfHeight),new Vector3(halfWidth,0,-halfHeight),new Vector3(-halfWidth,0,-halfHeight)};
                var quad=new float[8];bool ahead=true;
                for(int i=0;i<4;i++){var p=camera.WorldToViewportPoint(seen.pose.position+seen.pose.rotation*corners[i]);if(p.z<=.05f)ahead=false;quad[i*2]=p.x*width;quad[i*2+1]=p.y*height;}
                if(!ahead)continue;
                candidates.Add(new MarkerPoseJob{reference=entry.Key,quad=quad,sample=new Observation{pose=seen.pose,width=seen.width,height=seen.height,tracking=true,updated=now,stableSince=seen.stableSince,frame=cameraSampleId}});
                if(candidates.Count>=5)break;
            }
            if(!CameraOwner.cameraManager.TryAcquireLatestCpuImage(out var image))return false;
            try
            {
                if(CameraProviderSeconds>=0&&Math.Abs(image.timestamp-CameraProviderSeconds)>.12)return false;
                if(markerLuminance==null||markerLuminance.Length!=width*height)markerLuminance=new byte[width*height];
                var plane=image.GetPlane(0);
                if(!LabCameraLuminance.Copy(plane.data,image.width,image.height,plane.rowStride,plane.pixelStride,CameraDisplayTransform,markerLuminance,width,height))return false;
                jobs=candidates.ToArray();return true;
            }
            catch(Exception error)
            {
                markerPoseReason="CAMERA_CPU_IMAGE_UNAVAILABLE";
                ReportStability("QR_CPU_IMAGE",error);
                return false;
            }
            finally{image.Dispose();}
        }
        static MarkerQrFrame DecodeBoundMarkerFrame(byte[] pixels,int width,int height,MarkerPoseJob[] jobs)
        {
            var bound=new List<MarkerQrFrame>();
            foreach(var job in jobs)
            {
                var crop=LabScanWarp.QrCrop(pixels,width,height,job.quad);if(crop==null)continue;
                var decoded=DecodeMarkerFrame(crop,320,320);
                if(decoded.count!=1)continue;
                if(decoded.id!=job.reference)continue;
                decoded.observation=job.sample;decoded.reference=job.reference;
                bound.Add(decoded);
            }
            var ids=bound.Select(v=>v.id).Distinct().ToArray();
            if(ids.Length>1)return new MarkerQrFrame{count=ids.Length};
            if(ids.Length==1)
            {
                var first=bound[0];
                // Same ID seen at conflicting physical poses must not silently choose one.
                if(bound.Any(v=>Vector3.Distance(v.observation.pose.position,first.observation.pose.position)>.08f||Quaternion.Angle(v.observation.pose.rotation,first.observation.pose.rotation)>12))
                    return new MarkerQrFrame{count=1,id=first.id,reason="CARD_POSE_CONFLICT"};
                return first;
            }
            // Reading a QR alone provides a useful hint, never a location without a bound pose.
            return DecodeMarkerFrame(pixels,width,height);
        }
        void ReleaseMarkerCameraReadback(){markerLuminance=null;}
    }
}
