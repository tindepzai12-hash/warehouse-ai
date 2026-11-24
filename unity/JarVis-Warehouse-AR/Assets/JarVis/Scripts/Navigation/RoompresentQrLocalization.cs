using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.UI;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;

namespace JarVis.Warehouse.Navigation
{
    [Serializable]
    public sealed class RoompresentQrRegistration
    {
        public int schema=1;
        public string qr;
        public RoompresentMapRegistration fit;
        public static string FilePath=>Path.Combine(Application.persistentDataPath,"roompresent-fixed-a-v1.json");
        public bool Validate(RoompresentVpsLocalization vps,Vector3[] source,out Vector3 t,out float yaw)
        {
            t=default;yaw=0;
            return schema==1&&RoompresentQrLocalization.IsStartQr(qr)&&fit!=null&&fit.Validate(vps.mapFingerprint,vps.scanFingerprint,source,out t,out yaw);
        }
    }

    // Metric pose comes from three QR finder centres on one measured AR plane.
    // Decoding a payload alone, or assuming that the camera started at A, never aligns the map.
    public sealed class RoompresentQrLocalization : MonoBehaviour
    {
        SmallRoomAR guide;
        ARCameraManager cameraManager;
        Camera cameraOwner;
        ARPlaneManager planes;
        ARRaycastManager rays;
        ARAnchorManager anchors;
        ARAnchor anchor;
        Transform referenceFrame;
        readonly List<ARRaycastHit> hits=new List<ARRaycastHit>();
        Matrix4x4 display;
        float frameAt=-100,nextScan,stableAt,qrReadAt=-100,nextHint,nextRecovery,nextCaptureLog;
        bool haveDisplay,paused,creating;
        int epoch,jobEpoch,count,width,height;
        Pose captured,stable;
        string candidateQr;
        string measuredPoint;
        Vector3 measuredPosition;
        int measuredRevision;
        Task<QrFrame> decode;
        RoompresentQrRegistration record;
        public string Message {get;private set;}
        public string Qr {get;private set;}
        public string SetupPoint {get;private set;}
        public string LastReadPoint {get;private set;}
        public int StableSamples=>Mathf.Min(count,4);
        public bool SawQr=>Time.unscaledTime-qrReadAt<3;
        public int Revision {get;private set;}
        public Transform Frame=>referenceFrame!=null?referenceFrame:anchor==null?null:anchor.transform;
        public bool CameraReady=>!paused&&cameraManager!=null&&ARSession.state==ARSessionState.SessionTracking&&ARSession.notTrackingReason==NotTrackingReason.None&&Time.unscaledTime-frameAt<.6f;
        public bool HasAnchor=>CameraReady&&anchor!=null&&anchor.trackingState==TrackingState.Tracking;
        public bool Calibrated {get;private set;}
        public bool ReacquiringA {get;private set;}
        public bool Trusted=>HasAnchor&&Calibrated&&!ReacquiringA;
        public bool HasReference=>anchor!=null&&Calibrated;
        public void RequestReacquireA()
        {
            // Keep the existing native reference until a new measured A replaces it.
            if(guide!=null&&guide.localization!=null)guide.localization.SetAutoFocus(true);ReacquiringA=true;SetupPoint=null;epoch++;count=0;candidateQr=null;nextScan=0;Message=null;
            if(guide?.localization!=null)guide.localization.SetAutoFocus(true);
            Debug.Log("[RoompresentQR] Explicit A rescan; same session and saved delivery progress retained.");
        }
        public static bool IsStartQr(string value)=>value=="JWM:ROOMPRESENT:153613:A"||value=="JWM:MAIN:E"||value=="JWM:MAIN:A";
        public static bool MatchesPoint(string value,string point)=>point=="A"?IsStartQr(value):(point=="B"||point=="C")&&RoompresentDeliveryRecord.Destination(value)==point;
        public static Result DecodeQrFrame(byte[] gray,int width,int height)
        {
            // The printed cards have a decorative square border. Read the central QR
            // first, then expand to the whole frame; return ORIGINAL image coordinates.
            var reader=new BarcodeReaderGeneric{AutoRotate=false};reader.Options.TryHarder=true;reader.Options.TryInverted=true;reader.Options.PossibleFormats=new[]{BarcodeFormat.QR_CODE};
            foreach(float fraction in new[]{.65f,.9f,0f})
            {
                int size=Mathf.RoundToInt(Math.Min(width,height)*fraction),w=fraction==0?width:size,h=fraction==0?height:size;
                int left=(width-w)/2,top=(height-h)/2;byte[] pixels=gray;
                if(fraction!=0){pixels=new byte[w*h];for(int y=0;y<h;y++)Array.Copy(gray,(top+y)*width+left,pixels,y*w,w);}
                var source=new RGBLuminanceSource(pixels,w,h,RGBLuminanceSource.BitmapFormat.Gray8);
                var result=reader.Decode(source);
                // Version-one printed cards have no fourth alignment pattern. Small
                // perspective distortion can defeat the default affine estimate.
                // Recover identity from checksum-validated image samples only; the
                // measured AR plane is still required for every spatial pose update.
                if(result==null&&(fraction==.65f||fraction==0))result=DecodePerspectiveCard(source);
                if(result==null)continue;
                if(fraction==0||result.ResultPoints==null)return result;
                var points=result.ResultPoints.Select(p=>new ResultPoint(p.X+left,p.Y+top)).ToArray();
                return new Result(result.Text,result.RawBytes,points,result.BarcodeFormat);
            }
            return null;
        }
        static Result DecodePerspectiveCard(RGBLuminanceSource source)
        {
            var binary=new BinaryBitmap(new ZXing.Common.HybridBinarizer(source)).BlackMatrix;
            if(binary==null)return null;
            var hints=new Dictionary<DecodeHintType,object>{{DecodeHintType.TRY_HARDER,true}};
            var detected=new ZXing.QrCode.Internal.Detector(binary).detect(hints);
            if(detected==null||detected.Points==null||detected.Points.Length<3||detected.Bits.Width!=21)return null;
            var bl=detected.Points[0];var tl=detected.Points[1];var tr=detected.Points[2];
            var decoder=new ZXing.QrCode.Internal.Decoder();
            float[] offsets={0,.05f,-.05f,.1f,-.1f,.15f,-.15f,.2f,-.2f};
            foreach(float u in offsets)foreach(float v in offsets)
            {
                if(u==0&&v==0)continue;
                float x=bl.X+tr.X-tl.X+u*(tr.X-tl.X)+v*(bl.X-tl.X);
                float y=bl.Y+tr.Y-tl.Y+u*(tr.Y-tl.Y)+v*(bl.Y-tl.Y);
                var transform=ZXing.Common.PerspectiveTransform.quadrilateralToQuadrilateral(
                    3.5f,3.5f,17.5f,3.5f,17.5f,17.5f,3.5f,17.5f,
                    tl.X,tl.Y,tr.X,tr.Y,x,y,bl.X,bl.Y);
                var bits=ZXing.Common.GridSampler.Instance.sampleGrid(binary,21,21,transform);
                if(bits==null)continue;
                var result=decoder.decode(bits,null);
                if(result!=null)return new Result(result.Text,result.RawBytes,detected.Points,BarcodeFormat.QR_CODE);
            }
            return null;
        }
        public sealed class QrFrame
        {
            public Result result;
            public ResultPoint[] points;
            public int dimension;
            public ZXing.Common.BitMatrix binary;
        }
        public static QrFrame InspectFrame(byte[] gray,int width,int height)
        {
            var frame=new QrFrame{result=DecodeQrFrame(gray,width,height)};
            if(frame.result!=null)return frame;
            // Version-one cards have no fourth alignment pattern. A three-finder
            // image estimate can decode badly under perspective; retain geometry
            // for rectification against the MEASURED AR plane, without accepting an ID.
            frame.binary=new BinaryBitmap(new ZXing.Common.HybridBinarizer(new RGBLuminanceSource(gray,width,height,RGBLuminanceSource.BitmapFormat.Gray8))).BlackMatrix;
            var hints=new Dictionary<DecodeHintType,object>{{DecodeHintType.TRY_HARDER,true}};
            var detected=new ZXing.QrCode.Internal.Detector(frame.binary).detect(hints);
            if(detected?.Points?.Length>=3){frame.points=detected.Points;frame.dimension=detected.Bits.Width;}
            return frame;
        }
        public static Result DecodeProjected(QrFrame frame,Func<float,float,Vector2> project)
        {
            if(frame?.binary==null||frame.points==null||frame.dimension<21||frame.dimension>177||(frame.dimension-21)%4!=0)return null;
            var bits=new ZXing.Common.BitMatrix(frame.dimension);
            for(int y=0;y<frame.dimension;y++)for(int x=0;x<frame.dimension;x++)
            {
                int dark=0;
                foreach(var offset in new[]{Vector2.zero,new Vector2(.18f,0),new Vector2(-.18f,0),new Vector2(0,.18f),new Vector2(0,-.18f)})
                {
                    var p=project(x+.5f+offset.x,y+.5f+offset.y);int px=Mathf.RoundToInt(p.x),py=Mathf.RoundToInt(p.y);
                    if(px<0||py<0||px>=frame.binary.Width||py>=frame.binary.Height)return null;
                    if(frame.binary[px,py])dark++;
                }
                bits[x,y]=dark>=3;
            }
            var decoded=new ZXing.QrCode.Internal.Decoder().decode(bits,null);
            return decoded==null?null:new Result(decoded.Text,decoded.RawBytes,frame.points,BarcodeFormat.QR_CODE);
        }
        public bool TryMeasureQr(Result result,Pose capture,int imageWidth,int imageHeight,out Pose pose)
        {
            pose=default;
            if(!CameraReady||result?.ResultPoints==null||result.ResultPoints.Length<3||Vector3.Distance(capture.position,cameraOwner.transform.position)>=.02f||Quaternion.Angle(capture.rotation,cameraOwner.transform.rotation)>=2)return false;
            var p=result.ResultPoints;
            return TryPoint(p[0],out var bl,out var id,imageWidth,imageHeight)&&TryPoint(p[1],out var tl,out var id2,imageWidth,imageHeight)&&TryPoint(p[2],out var tr,out var id3,imageWidth,imageHeight)&&id==id2&&id==id3&&TryPose(bl,tl,tr,out pose);
        }
        public static bool TryDestinationOrigin(Pose previous,Vector3 observed,Vector3 measured,out Pose origin)
        {
            origin=default;
            if(!RoompresentPoseGate.Finite(previous.position)||!RoompresentPoseGate.Finite(new Vector3(previous.rotation.x,previous.rotation.y,previous.rotation.z))||!float.IsFinite(previous.rotation.w)||!RoompresentPoseGate.Finite(observed)||!RoompresentPoseGate.Finite(measured))return false;
            var position=measured-previous.rotation*observed;
            if(Vector3.Distance(position,previous.position)>.75f)return false;
            origin=new Pose(position,previous.rotation);return true;
        }
        // A destination QR measures position locally. Keep the previously trusted yaw:
        // one unregistered QR cannot recover absolute room heading by itself.
        public async Task<bool> AttachDestinationReference(string stop,Pose measured)
        {
            if(!Trusted||record==null||(stop!="B"&&stop!="C"))return false;
            if(!record.Validate(guide.localization,guide.registrationLandmarks,out var t,out var yaw))return false;
            var rotation=Frame.rotation;var observed=record.fit.observed[stop=="B"?1:2];
            if(!TryDestinationOrigin(new Pose(Frame.position,rotation),observed,measured.position,out var correction))return false;
            var origin=correction.position;
            int generation=epoch;
            try
            {
                var added=await anchors.TryAddAnchorAsync(measured);
                if(!added.status.IsSuccess()||added.value==null)return false;
                if(this==null||generation!=epoch||!Trusted){if(anchors!=null)anchors.TryRemoveAnchor(added.value);return false;}
                var previous=anchor;
                var reference=new GameObject("Fixed A frame from measured "+stop).transform;
                reference.SetParent(added.value.transform,false);reference.SetPositionAndRotation(origin,rotation);
                anchor=added.value;referenceFrame=reference;
                ApplyPreview(t,yaw);
                if(previous!=null)anchors.TryRemoveAnchor(previous);
                Debug.Log("[RoompresentQR] Destination "+stop+" measured; native reference transferred; saved B/C and room yaw retained. anchor="+anchor.trackableId);
                return true;
            }
            catch(Exception e){Debug.LogWarning("[RoompresentQR] Destination reference retry: "+e.GetType().Name);return false;}
        }
        public Result RecoverOnPlane(QrFrame frame,Pose capture,int imageWidth,int imageHeight)
        {
            if(!CameraReady||frame.points==null||Vector3.Distance(capture.position,cameraOwner.transform.position)>=.02f||Quaternion.Angle(capture.rotation,cameraOwner.transform.rotation)>=2)return null;
            var p=frame.points;
            if(!TryPoint(p[0],out var bl,out var id,imageWidth,imageHeight)||!TryPoint(p[1],out var tl,out var id2,imageWidth,imageHeight)||!TryPoint(p[2],out var tr,out var id3,imageWidth,imageHeight)||id!=id2||id!=id3||!TryPose(bl,tl,tr,out _)){Message="SHOW_SURFACE";return null;}
            var right=(tr-tl)/(frame.dimension-7);var down=(bl-tl)/(frame.dimension-7);
            return DecodeProjected(frame,(x,y)=>{var viewport=cameraOwner.WorldToViewportPoint(tl+(x-3.5f)*right+(y-3.5f)*down);return new Vector2(viewport.x*imageWidth,(1-viewport.y)*imageHeight);});
        }
        public void RequestSetupPoint(string point)
        {
            if(point!=null&&point!="B"&&point!="C")throw new ArgumentException("Expected B or C",nameof(point));
            if(SetupPoint==point)return;
            SetupPoint=point;epoch++;count=0;Message=null;LastReadPoint=null;
            if(point!=null)guide.localization.SetAutoFocus(true);
        }
        public bool TryConsumeSetupPoint(string point,out Vector3 position)
        {
            position=default;
            if(!HasAnchor||measuredPoint!=point||measuredRevision!=Revision)return false;
            position=measuredPosition;measuredPoint=null;return true;
        }
        public static RoompresentQrLocalization Attach(SmallRoomAR guide)
        {
            var value=guide.GetComponent<RoompresentQrLocalization>();
            if(value!=null)return value;
            value=guide.gameObject.AddComponent<RoompresentQrLocalization>();value.Configure(guide);return value;
        }
        void Configure(SmallRoomAR value)
        {
            guide=value;cameraManager=value.localization.cameraManager;cameraOwner=cameraManager.GetComponent<Camera>();
            var origin=cameraManager.GetComponentInParent<XROrigin>();
            planes=origin.GetComponent<ARPlaneManager>()??origin.gameObject.AddComponent<ARPlaneManager>();planes.requestedDetectionMode=PlaneDetectionMode.Horizontal;
            rays=origin.GetComponent<ARRaycastManager>()??origin.gameObject.AddComponent<ARRaycastManager>();
            anchors=origin.GetComponent<ARAnchorManager>()??origin.gameObject.AddComponent<ARAnchorManager>();
            planes.enabled=rays.enabled=anchors.enabled=true;
            cameraManager.frameReceived+=FrameReceived;
            guide.localization.SetAutoFocus(true);
            try
            {
                // Local installer changes take priority. A malformed local record must not
                // silently fall back to different shelf coordinates.
                string json=File.Exists(RoompresentQrRegistration.FilePath)?File.ReadAllText(RoompresentQrRegistration.FilePath):Resources.Load<TextAsset>("RoompresentCheckedRegistration")?.text;
                if(!string.IsNullOrEmpty(json))
                {
                    var saved=JsonUtility.FromJson<RoompresentQrRegistration>(json);
                    if(saved!=null&&saved.Validate(value.localization,value.registrationLandmarks,out _,out _))record=saved;
                }
            }
            catch(Exception){Message="Saved A registration unavailable";}
        }
        void FrameReceived(ARCameraFrameEventArgs args){frameAt=Time.unscaledTime;if(args.displayMatrix.HasValue){display=args.displayMatrix.Value;haveDisplay=true;}}
        bool TryPoint(ResultPoint point,out Vector3 position,out TrackableId planeId,int imageWidth=0,int imageHeight=0)
        {
            position=default;planeId=default;
            var screen=new Vector2(point.X/(imageWidth==0?width:imageWidth)*Screen.width,(1-point.Y/(imageHeight==0?height:imageHeight))*Screen.height);
            if(!rays.Raycast(screen,hits,TrackableType.PlaneWithinPolygon))return false;
            foreach(var hit in hits)
            {
                var plane=planes.GetPlane(hit.trackableId);
                if(hit.distance<.15f||hit.distance>2.5f||plane==null||plane.trackingState!=TrackingState.Tracking||plane.alignment!=PlaneAlignment.HorizontalUp)continue;
                position=hit.pose.position;planeId=hit.trackableId;return true;
            }
            return false;
        }
        public static bool TryPose(Vector3 bottomLeft,Vector3 topLeft,Vector3 topRight,out Pose pose)
        {
            pose=default;
            if(!RoompresentPoseGate.Finite(bottomLeft)||!RoompresentPoseGate.Finite(topLeft)||!RoompresentPoseGate.Finite(topRight))return false;
            var up=topLeft-bottomLeft;var right=topRight-topLeft;
            if(up.magnitude<.025f||up.magnitude>.8f||right.magnitude<.025f||right.magnitude>.8f||Mathf.Abs(Vector3.Dot(up.normalized,right.normalized))>.3f||Mathf.Abs(up.magnitude/right.magnitude-1)>.25f||Mathf.Abs(up.y)>.015f||Mathf.Abs(right.y)>.015f)return false;
            // Proper corner ordering is important: reject a mirrored observation rather than flipping yaw.
            if(Vector3.Dot(Vector3.Cross(up.normalized,right.normalized),Vector3.up)<.9f)return false;
            pose=new Pose((bottomLeft+topRight)*.5f,Quaternion.LookRotation(Vector3.ProjectOnPlane(up,Vector3.up).normalized,Vector3.up));return true;
        }
        void Update()
        {
            if(guide==null)return;
            if(!paused&&Time.unscaledTime>=nextRecovery)
            {
                nextRecovery=Time.unscaledTime+2;
                var session=FindFirstObjectByType<ARSession>();
                if(session!=null&&!session.enabled)session.enabled=true;
                if(!cameraManager.enabled&&!guide.localization.FocusRefreshing)cameraManager.enabled=true;
                if(!anchors.enabled)anchors.enabled=true;
            }
            if(decode!=null&&decode.IsCompleted)
            {
                var task=decode;decode=null;
                if(task.IsFaulted){Debug.LogWarning("[RoompresentQR] Decoder job failed: "+task.Exception.GetBaseException().GetType().Name);count=0;Message="DECODE_RETRY";}
                else if(jobEpoch==epoch&&CameraReady&&(!HasAnchor||SetupPoint!=null||ReacquiringA))
                {
                    var frame=task.Result;var result=frame.result??RecoverOnPlane(frame,captured,width,height);
                    string expected=HasAnchor&&SetupPoint!=null?SetupPoint:"A";
                    bool matches=result!=null&&MatchesPoint(result.Text,expected)&&(expected!="A"||record==null||record.qr==result.Text);
                    if(result!=null)
                    {
                        LastReadPoint=IsStartQr(result.Text)?"A":RoompresentDeliveryRecord.Destination(result.Text);
                        if(expected=="A"&&matches)qrReadAt=Time.unscaledTime;
                        if(matches&&Time.unscaledTime>=nextHint){nextHint=Time.unscaledTime+8;Debug.Log("[RoompresentQR] QR "+expected+" decoded; checking measured plane and stable pose.");}
                        if(!matches&&LastReadPoint!=null)Message="WRONG_POINT";
                    }
                    if(matches&&result.ResultPoints?.Length>=3&&Vector3.Distance(captured.position,cameraOwner.transform.position)<.02f&&Quaternion.Angle(captured.rotation,cameraOwner.transform.rotation)<2)
                    {
                        var points=result.ResultPoints;
                        if(TryPoint(points[0],out var bl,out var id)&&TryPoint(points[1],out var tl,out var id2)&&TryPoint(points[2],out var tr,out var id3)&&id==id2&&id==id3&&TryPose(bl,tl,tr,out var pose))
                        {
                            if(candidateQr!=result.Text||count==0||Vector3.Distance(stable.position,pose.position)>.025f||Quaternion.Angle(stable.rotation,pose.rotation)>3){candidateQr=result.Text;stable=pose;count=1;stableAt=Time.unscaledTime;}
                            else count++;
                            Message="STABILIZING";
                            if(count>=4&&Time.unscaledTime-stableAt>=.8f&&!creating)
                            {
                                if(expected=="A")CreateAnchor(pose,result.Text);
                                else if(HasAnchor)
                                {
                                    measuredPoint=expected;measuredPosition=Frame.InverseTransformPoint(pose.position);measuredRevision=Revision;
                                    RequestSetupPoint(null);
                                    Debug.Log("[RoompresentQR] Measured setup QR "+expected+" in fixed A frame; position="+measuredPosition.ToString("F3")+" revision="+Revision);
#if UNITY_ANDROID && !UNITY_EDITOR
                                    Handheld.Vibrate();
#endif
                                }
                            }
                        }
                        else{count=0;Message="SHOW_SURFACE";}
                    }
                    else {count=0;if(matches)Message="HOLD_STILL";}
                }
            }
            if((HasAnchor&&SetupPoint==null&&!ReacquiringA)||creating||!CameraReady||!haveDisplay||decode!=null||Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.25f;
            if(!cameraManager.TryAcquireLatestCpuImage(out var image))return;
            using(image)
            {
                float aspect=cameraOwner.aspect;height=aspect<=1?1440:Mathf.RoundToInt(1440/aspect);width=Mathf.RoundToInt(height*aspect);
                var gray=new byte[width*height];var plane=image.GetPlane(0);
                if(!LabCameraLuminance.Copy(plane.data,image.width,image.height,plane.rowStride,plane.pixelStride,display,gray,width,height))
                {
                    Message="CAMERA_IMAGE";
                    if(Time.unscaledTime>=nextCaptureLog){nextCaptureLog=Time.unscaledTime+8;Debug.LogWarning("[RoompresentQR] CPU image rejected: source="+image.width+"x"+image.height+" target="+width+"x"+height+" stride="+plane.rowStride+" pixelStride="+plane.pixelStride);}
                    return;
                }
                if(Time.unscaledTime>=nextCaptureLog){nextCaptureLog=Time.unscaledTime+8;Debug.Log("[RoompresentQR] CPU image ready for QR decoder: "+width+"x"+height);}
                // ZXing uses top-left rows. The shared camera helper outputs bottom-left rows.
                for(int y=0;y<height/2;y++)for(int x=0;x<width;x++){int a=y*width+x,b=(height-y-1)*width+x;byte temp=gray[a];gray[a]=gray[b];gray[b]=temp;}
                captured=new Pose(cameraOwner.transform.position,cameraOwner.transform.rotation);jobEpoch=epoch;int w=width,h=height;
                decode=Task.Run(()=>InspectFrame(gray,w,h));
            }
        }
        async void CreateAnchor(Pose pose,string qr)
        {
            creating=true;int generation=epoch;
            try
            {
                var added=await anchors.TryAddAnchorAsync(pose);
                if(!added.status.IsSuccess()||added.value==null){count=0;return;}
                if(this==null||generation!=epoch||!CameraReady){if(anchors!=null)anchors.TryRemoveAnchor(added.value);return;}
                if(anchor!=null){if(guide.mapRoot.IsChildOf(anchor.transform))guide.mapRoot.SetParent(guide.transform,true);anchors.TryRemoveAnchor(anchor);}
                anchor=added.value;referenceFrame=null;ReacquiringA=false;Qr=qr;Revision++;Calibrated=false;
                guide.localization.MarkerOwnsFrame=true;guide.localization.sdk.Session?.PauseSession();
                if(record!=null&&record.Validate(guide.localization,guide.registrationLandmarks,out var t,out var yaw)){ApplyPreview(t,yaw);Calibrated=true;guide.localization.SetAutoFocus(true);}
                Debug.Log("[RoompresentQR] Measured fixed A on AR plane; anchor="+anchor.trackableId+" calibrated="+Calibrated+" qr="+qr);
#if UNITY_ANDROID && !UNITY_EDITOR
                Handheld.Vibrate();
#endif
            }
            catch(Exception e){Message=e.GetType().Name;count=0;}
            finally{creating=false;}
        }
        public void ApplyPreview(Vector3 translation,float yaw)
        {
            guide.mapRoot.SetParent(Frame,false);guide.mapRoot.localPosition=translation;guide.mapRoot.localRotation=Quaternion.Euler(0,yaw,0);guide.mapRoot.localScale=Vector3.one;
            guide.surface.RemoveData();guide.surface.AddData();
        }
        public bool SaveChecked(Vector3[] observed)
        {
            if(!HasAnchor)return false;
            var next=new RoompresentQrRegistration{qr=Qr,fit=new RoompresentMapRegistration{source=guide.registrationLandmarks.ToArray(),observed=observed.ToArray(),mapFingerprint=guide.localization.mapFingerprint,scanFingerprint=guide.localization.scanFingerprint,operatorChecked=true,checkedUtc=DateTime.UtcNow.ToString("O")}};
            if(!next.Validate(guide.localization,guide.registrationLandmarks,out var t,out var yaw))return false;
            string temp=RoompresentQrRegistration.FilePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(next,true));if(File.Exists(RoompresentQrRegistration.FilePath))File.Replace(temp,RoompresentQrRegistration.FilePath,null);else File.Move(temp,RoompresentQrRegistration.FilePath);
            record=next;ApplyPreview(t,yaw);Calibrated=true;RequestSetupPoint(null);guide.localization.SetAutoFocus(true);Debug.Log("[RoompresentQR] Operator checked and saved B/C relative to fixed A. No camera start pose saved.");return true;
        }
        void OnApplicationPause(bool value){paused=value;epoch++;count=0;candidateQr=null;nextScan=0;nextRecovery=0;haveDisplay=false;frameAt=-100;}
        void OnDestroy()
        {
            epoch++;if(cameraManager!=null)cameraManager.frameReceived-=FrameReceived;
            if(decode!=null)_=decode.ContinueWith(t=>{if(t.IsFaulted)_=t.Exception;});
        }
    }
}
