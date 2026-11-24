using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JarVis.Warehouse.RouteRecording;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabCalibrationProfile profile,pendingProfile;
        LabMapFit fitCache;LabSurvey fitSurvey;LabFloorPlan fitFloor;LabMapPlacement fitPlacement;int fitMarkerCount;
        bool navigationTrusted,recovering,journeyStarted,setupNeedsRestart,guideEdge,profileReady;
        double anchorStableSince=-1,traceAt,flushAt;
        int setupStage;string rebaseTrace="BASELINE";
        Pose setupA;
        LabMapFit setupFit;
        LabGuideVisual navigationVisual;
        readonly StringBuilder traceBuffer=new StringBuilder();
        Task<MarkerQrFrame> markerDecode;
        sealed class MarkerQrFrame { public string id,reference,reason;public int count;public Observation observation; }
        byte[] markerLuminance;
        int decodedMarkerCount;
        string markerPoseReason="SEARCHING_QR";
        string decodedMarker,traceState,setupGateMarker;
        double decodedMarkerAt,markerDecodeAt,nextMarkerDecode;
        int markerDecodeEpoch;
        public bool SettingUp=>setupStage>0;
        public int SetupStage=>setupStage;
        public bool ProfileReady=>profileReady;
        public bool GuideEdge=>guideEdge&&Located&&Route.Count>0;
        public float GuideBearing {get;private set;}
        public string NavigationState {get;private set;}="Chọn đích để bắt đầu";
        public bool RecoveryNeedsMarker {get;private set;}
        public LabPoint LastKnownPosition {get;private set;}
        public float LastKnownHeading {get;private set;}
        public bool SetupNeedsRestart=>setupNeedsRestart;
        public string SetupStep=>setupStage==1?"1/3 · Quét A":setupStage==2?"2/3 · Đi tới và quét B":"3/3 · Quay lại quét A";
        string ProfilePath=>Path.Combine(Root,"calibration-"+Floor.id+"-r"+Floor.revision+".json");
        [Serializable] sealed class NavigationTrace
        {
            public string phase,alignmentMethod;public float alignmentMarkerResidual;public double depthCameraDelta;public int projectedDepthPoints;
            public bool navigationPoseValid,remainingKnown,cameraGuidanceOccluded;public string ai,destinationChanged,presentationReason;public LabPoint[] routePoints;
            public string scanQrId,scanPoseReason,scanLibraryId,surveyContinuityReason;public int scanQrIds,scanReferenceHypotheses;
            public string surveyStage,surveyRejectedId,surveyRejectReason;public int surveySavedCount;public LabPoint surveyRejectedPosition;
            public string utc,state,trackingReason,target,routeError,cueReason,mapHash,poseReason,routeId,graphHash,maneuverId,maneuverType;
            public float[] cameraRotation,anchorRotation,mapFrameRotation;
            public string anchorId,recoveryReason,recoveryStage,cueIntent,primaryAction,rebaseEvent,poseTimeSource;public long decisionId,cameraSampleId,providerTimestampNs,presentationDecisionId;public double recoveryDeadline,lastKnownAge;public double cameraFrameAge;public float recoveryProgress,anchorDistance,movementBearing,movementConfidence;public bool markerScannerRunning;public LabPoint mapOriginWorld;
            public double seconds;
            public int epoch,revision,setup,routeRevision,visibleTokens;public string visualMode,visualFloorSource;public float visualHeightOffset;
            public bool tracking,anchorTracking,located,recovering,needsMarker,cueVisible,preview;
            public LabPoint position,rawCameraWorld,anchorWorld,rawPosition,acceptedPosition,displayPosition;
            public float correction,along,crossTrack,turnDistance,remaining;
            public float heading,progress;
            public string routeSource,localPlannerStatus;public float frontFloorCoverage;public string mapKnowledgeId,alignmentMode,depthStatus,obstacleStatus;public string[] cameraBlockedEdges,manualBlockedEdges;public int obstacleCells,groundFrames;
        }
        LabMapFit CachedRegistration()
        {
            if(fitCache==null||fitSurvey!=Map||fitFloor!=Floor||fitPlacement!=Map?.placement||fitMarkerCount!=(Map?.markers.Count??0))
            {fitSurvey=Map;fitFloor=Floor;fitPlacement=Map?.placement;fitMarkerCount=Map?.markers.Count??0;fitCache=LabRegistration.Solve(Map,Floor);}
            return fitCache;
        }
        void LoadCalibrationProfile()
        {
            try
            {
                profile=File.Exists(ProfilePath)?JsonUtility.FromJson<LabCalibrationProfile>(File.ReadAllText(ProfilePath)):null;
                profileReady=profile?.Matches(Floor)==true;if(!profileReady)profile=null;
            }
            catch(Exception){profileReady=false;profile=null;Message="Chưa đọc được thiết lập mốc. Dữ liệu cũ vẫn được giữ.";}
        }
        public void BeginSetup()
        {
            if(!ready||addingAnchor)return;
            StopExercise();if(!LoadNewPreset())return;ResetCalibration();setupStage=1;setupNeedsRestart=false;
            pendingProfile=new LabCalibrationProfile{mapId=Floor.id,revision=Floor.revision,geometrySha256=Floor.geometrySha256};
            NavigationState="Thiết lập mốc cố định";
            Message="Giữ A và B cố định ở hai bàn. Quét A → đi tới quét B → quay lại A kiểm tra. Bước này chưa bật mũi tên.";
        }
        bool VisibleMarker(string id,double now,out Observation image)
        {
            // Similar reference templates are hypotheses, not distinct physical sheets.
            // Only the QR identity decoded from the current camera image selects a pose.
            image=null;
            if(!LabScanIdentity.Accepts(id,decodedMarker,decodedMarkerCount,now,decodedMarkerAt)){markerPoseReason=decodedMarkerCount>1?"MULTIPLE_QR":"QR_NOT_CURRENT";return false;}
            image=decodedObservation;
            if(image==null||!image.tracking||now-image.updated>.85){markerPoseReason="WAITING_CARD_POSE";return false;}
            var vp=CameraOwner.arCamera.WorldToViewportPoint(image.pose.position);
            bool valid=vp.z>.05f&&vp.x>.01f&&vp.x<.99f&&vp.y>.01f&&vp.y<.99f;
            markerPoseReason=valid?"MATCHED_QR_AND_REFERENCE":"CARD_OUTSIDE_CAMERA";return valid;
        }
        static MarkerQrFrame DecodeMarkerFrame(byte[] pixels,int width,int height)
        {
            var reader=new BarcodeReaderGeneric{AutoRotate=true};
            reader.Options.PossibleFormats=new[]{BarcodeFormat.QR_CODE};reader.Options.TryHarder=true;
            var source=new RGBLuminanceSource(pixels,width,height,RGBLuminanceSource.BitmapFormat.Gray8);
            var results=reader.DecodeMultiple(source);
            if(results==null||results.Length==0){var single=reader.Decode(source);results=single==null?Array.Empty<Result>():new[]{single};}
            var ids=results.Select(r=>LabScanIdentity.PayloadId(r.Text)).Where(v=>v!=null).Distinct().ToArray();
            return new MarkerQrFrame{count=ids.Length,id=ids.Length==1?ids[0]:null};
        }
        void UpdateMarkerDecode(double now)
        {
            if(markerDecode!=null&&markerDecode.IsCompleted)
            {
                var task=markerDecode;markerDecode=null;
                if(task.IsFaulted){_=task.Exception;}
                else if(markerDecodeEpoch==epoch&&now-markerDecodeAt<1.2)
                {
                    var result=task.Result;
                    decodedMarkerCount=result.count;decodedMarker=result.id;decodedMarkerAt=markerDecodeAt;
                    bool switched=result.count==1&&NewScan&&SelectMarkerPose(result.id,now);
                    decodedObservation=switched?null:result.observation;
                    markerPoseReason=result.reason??(result.observation!=null?"MATCHED_QR_AND_REFERENCE":"WAITING_CARD_POSE");
                    if(result.count!=1){calibration.Reset();ScanProgress=0;setupGateMarker=null;}
                }
            }
            // Keep the provider/library alive through a brief occlusion. Identity freshness
            // still independently gates acceptance; this does not accept a stale QR pose.
            if(NewScan&&markerPoseId!=null&&now-markerIdentityAt>3)SuspendMarkerPose();
            if(markerDecode!=null||now<nextMarkerDecode)return;
            nextMarkerDecode=now+.35;
            if(TryMarkerViewport(now,out int viewportWidth,out int viewportHeight,out var jobs))
            {
                var pixels=markerLuminance;markerDecodeAt=now;markerDecodeEpoch=epoch;
                markerDecode=Task.Run(()=>DecodeBoundMarkerFrame(pixels,viewportWidth,viewportHeight,jobs));return;
            }
            if(!CameraOwner.cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))return;
            try
            {
                var plane=image.GetPlane(0);int stride=Math.Max(1,(Math.Max(image.width,image.height)+639)/640),w=image.width/stride,h=image.height/stride;
                if(markerLuminance==null||markerLuminance.Length!=w*h)markerLuminance=new byte[w*h];
                var pixels=markerLuminance;
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=plane.data[y*stride*plane.rowStride+x*stride*plane.pixelStride];
                markerDecodeAt=now;markerDecodeEpoch=epoch;markerDecode=Task.Run(()=>DecodeMarkerFrame(pixels,w,h));
            }
            finally{image.Dispose();}
        }
        bool StableMarker(string id,double now,out Observation image)
        {
            if(setupGateMarker!=id){setupGateMarker=id;calibration.Reset();lastGateUpdate=-1;}
            if(!VisibleMarker(id,now,out image))
            {
                if(now-lastGateUpdate>.7){calibration.Reset();ScanProgress=0;}
                ScanLabel=quality.Available&&quality.Mean<40?"Thiếu sáng · bật thêm đèn hoặc hướng máy về vùng sáng hơn":
                    decodedMarker!=null?"Đã nhận "+decodedMarker+" · giữ trọn khung hoa văn trong camera":"Đưa trọn thẻ giấy A–E vào khung · đang tìm mốc";
                if(decodedMarkerCount>1&&now-decodedMarkerAt<=.85)ScanLabel="Đọc được nhiều mã A–E · đưa một thẻ cần ghi vào camera";
                else if(decodedMarker==id&&now-decodedMarkerAt<=.85)ScanLabel="Đã đọc QR "+id+" · đang xác định vị trí thẻ\nThẻ nằm trên bàn hoặc dán đứng đều được";
                return false;
            }
            if(image.updated!=lastGateUpdate){lastGateUpdate=image.updated;calibration.ObserveFrame(image.frame,image.updated,true,image.width,ToPoint(image.pose.position),ToPoint(CameraOwner.arCamera.transform.position));}
            ScanProgress=Math.Min(.99f,calibration.Progress);
            ScanLabel="Đã nhận "+id+" · "+Mathf.FloorToInt(ScanProgress*100)+"% kiểm tra vị trí\nGiữ trọn khung thẻ · "+calibration.Samples+"/8 mẫu ổn định";
            if(ScanNeedsHelp)ScanLabel+="\nNếu % đứng yên: giữ thẻ phẳng, đưa máy chậm hơn";
            return calibration.Ready&&now-image.stableSince>=.65;
        }
        void UpdateNavigation(double now)
        {
            bool active=SettingUp||Locating||Exercise!=null&&!Exercise.Complete;
            if(!active){SetMarkerScanning(false);HideGuidance();NavigationState=ProfileReady?"Sẵn sàng · chọn đích A–E":"Ghi và căn các mốc trước";return;}
            SetMarkerScanning(MarkerScanRequested);
            CheckRecoveryDeadline(now);
            if(!Tracking)
            {
                previewHeading.Reset();
                if(trialHeadingPending){ResetTrialHeading();AwaitingDirection=false;RequireTrackingReference("HEADING_TRACKING_INTERRUPTED");}
                BeginTrackingInterruption(now);
                if(SettingUp&&setupStage>1&&now-lostSince>8)setupNeedsRestart=true;
                HideGuidance();NavigationState=journeyStarted?"Tracking tạm gián đoạn · đang giữ đích":"Đang chuẩn bị AR";ScanProgress=0;
                ScanLabel=TrackingCoach();if(!RecoveryNeedsMarker)Message=ScanLabel;
                RecordNavigationTrace(now);return;
            }
            if(MarkerScanRequested)UpdateMarkerDecode(now);
            if(trialHeadingPending){UpdateTrialHeading(now);RecordNavigationTrace(now);return;}
            if(SettingUp)
            {
                if(setupNeedsRestart){NavigationState="Cần đo lại A–B";ScanLabel="Tracking đã gián đoạn trong lượt đo";RecordNavigationTrace(now);return;}
                UpdateSetup(now);RecordNavigationTrace(now);return;
            }
            if(PreviewMode&&!Located&&(!journeyStarted||AwaitingDirection||!RecoveryNeedsMarker&&!navigationTrusted))
            {
                UpdatePreviewCalibration(now);RecordNavigationTrace(now);return;
            }
            if(!PreviewMode&&!ProfileReady)
            {
                NavigationState="Chưa thiết lập hướng map";ScanProgress=0;ScanLabel="Thiết lập A–B trước khi dẫn đường";
                HideGuidance();RecordNavigationTrace(now);return;
            }
            if(journeyStarted&&navigationTrusted&&(anchor==null||anchor.trackingState!=TrackingState.Tracking))BeginTrackingInterruption(now);
            if(recovering&&navigationTrusted&&!RecoveryNeedsMarker)TryRecoverTracking(now);
            if(journeyStarted&&navigationTrusted&&anchor==null)RequireTrackingReference("ANCHOR_REMOVED");
            if(!Located)
            {
                HideGuidance();
                if(!recovering&&!RecoveryNeedsMarker){NavigationState="Đang nhận mốc";ScanLabel="Quét một mốc đã lưu gần bạn (A–E)";}
                if(RecoveryNeedsMarker){NavigationState="Cần căn lại vị trí";Message=PreviewMode?"Mở map, chọn điểm A/B/C đúng nơi đang đứng để căn nhanh lại. Đích cũ vẫn được giữ.":"Quét một mốc A–E đã lưu gần bạn để tiếp tục; đích cũ vẫn được giữ.";}
                if(!PreviewMode&&MarkerScanRequested&&!addingAnchor)
                {
                    var marker=profile.markers.FirstOrDefault(m=>m.id==decodedMarker);
                    if(marker!=null&&StableMarker(marker.id,now,out var image))AlignCalibrated(marker,image.pose,image.width);
                    else if(marker==null){calibration.Reset();ScanProgress=0;}
                }
                RecordNavigationTrace(now);return;
            }
            var raw=RawPosition;var forwardLocal=MapFrame.InverseTransformDirection(CameraOwner.arCamera.transform.forward);
            var current=raw;
            if(lastPose!=null&&now-lastPoseTime>0&&now-lastPoseTime<.5&&LabPaths.Distance(current,lastPose)>.5f+3*(now-lastPoseTime))
            {
                RequireTrackingReference("POSE_JUMP");
                Message=PreviewMode?"Vị trí vừa nhảy. Mở map để căn lại tại A/B/C; đích vẫn được giữ.":"Vị trí vừa nhảy. Quét mốc đã thiết lập gần bạn để kiểm tra; đích vẫn được giữ.";
                NavigationState="Cần mốc kiểm tra";RecordNavigationTrace(now);return;
            }
            poseEstimate.Update(floorRouter,raw,forwardLocal.x,forwardLocal.z,now,PreviewMode);
            lastPose=current;lastPoseTime=now;
            if(NavigationPoseValid){LastKnownPosition=poseEstimate.Current.accepted;LastKnownHeading=Heading;movement.Update(Position,now);}else movement.Reset();
            ScanProgress=1;ScanLabel="Đã căn map · đang theo dõi chuyển động";NavigationState="Đang theo vị trí";
            if(now<fixedLatchUntil)ScanLabel=fixedLatchText;
            if(Exercise!=null&&!Exercise.Complete){if(now>=nextDecision||!NavigationPoseValid||needsRoutePlan||plannedTarget!=Exercise.Target){nextDecision=now+.1;UpdateFloorRoute(now);}}else HideGuidance();
            MaybeRebaseAnchor(now);
            RecordNavigationTrace(now);
        }
        void UpdateSetup(double now)
        {
            NavigationState=SetupStep;
            if(setupStage>1&&anchor!=null&&anchor.trackingState!=TrackingState.Tracking)
            {
                if(lostSince<0)lostSince=now;
                ScanLabel="Đợi mốc AR phục hồi, chưa cần đo lại";
                if(now-lostSince>8){setupNeedsRestart=true;Message="Mốc đo chưa phục hồi. Bắt đầu lại lượt đo khi tracking ổn định.";}
                return;
            }
            lostSince=-1;recovering=false;
            string wanted=setupStage==2?"B":"A";ScanLabel="Quét toàn thẻ "+wanted+" cố định · "+SetupStep;
            if(addingAnchor||!StableMarker(wanted,now,out var image))return;
            if(setupStage==1){CaptureSetupA(image.pose,image.width);return;}
            if(anchor==null||anchor.trackingState!=TrackingState.Tracking){setupNeedsRestart=true;Message="Mốc đo không còn tracking. Bắt đầu lại thiết lập.";return;}
            var local=new Pose(MapFrame.InverseTransformPoint(image.pose.position),Quaternion.Inverse(MapFrame.rotation)*image.pose.rotation);
            if(setupStage==2)
            {
                var a=Map.markers.First(m=>m.id=="A");var b=Map.markers.First(m=>m.id=="B");
                setupFit=LabNavigationPolicy.AlignPair(a.centre,b.centre,ToPoint(setupA.position),ToPoint(local.position));
                if(!setupFit.valid){Message="Khoảng cách A–B chưa khớp map. Kiểm tra đúng thẻ, đúng bàn; xuất ZIP để đối chiếu.";setupNeedsRestart=true;return;}
                pendingProfile.baselineError=setupFit.maxError;
                var mapRotation=Quaternion.Euler(0,setupFit.yaw,0);
                pendingProfile.markers[0].rotationInMap=Q(Quaternion.Inverse(mapRotation)*setupA.rotation);
                pendingProfile.markers.Add(new LabCalibratedMarker{id="B",width=image.width,rotationInMap=Q(Quaternion.Inverse(mapRotation)*local.rotation)});
                setupStage=3;calibration.Reset();setupGateMarker=null;decodedMarker=null;
                Message="Đã đo A–B. Đi về đúng thẻ A cố định để kiểm tra độ lệch trước khi lưu.";return;
            }
            float positionError=Vector3.Distance(local.position,setupA.position),angleError=Quaternion.Angle(local.rotation,setupA.rotation);
            pendingProfile.loopPositionError=positionError;pendingProfile.loopAngleError=angleError;
            if(positionError>.25f||angleError>3||Mathf.Abs(image.width-pendingProfile.markers[0].width)>pendingProfile.markers[0].width*.08f)
            {setupNeedsRestart=true;Message="Quay về A lệch "+positionError.ToString("F2")+" m / "+angleError.ToString("F1")+"°. Chưa lưu. Kiểm tra thẻ đứng yên và thử đo lại.";return;}
            pendingProfile.loopChecked=true;pendingProfile.utc=DateTime.UtcNow.ToString("O");
            if(!pendingProfile.Matches(Floor)){setupNeedsRestart=true;Message="Thiết lập chưa qua kiểm tra dữ liệu. Xuất ZIP để kiểm tra.";return;}
            try
            {
                Directory.CreateDirectory(Root);string tmp=ProfilePath+".tmp";File.WriteAllText(tmp,JsonUtility.ToJson(pendingProfile,true));
                if(File.Exists(ProfilePath))File.Replace(tmp,ProfilePath,ProfilePath+".previous");else File.Move(tmp,ProfilePath);
                profile=pendingProfile;profileReady=true;setupStage=0;RemoveAnchor();navigationTrusted=false;recovering=false;
                Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"calibration-profile.json"),JsonUtility.ToJson(profile,true));
                Message="Đã lưu hướng A/B, vòng A–B–A đạt kiểm tra. Chọn B hoặc C để thử; C chưa được đối chiếu độc lập ngoài lab.";
                NavigationState="Thiết lập đã lưu";
            }
            catch(Exception){setupNeedsRestart=true;Message="Chưa lưu được thiết lập. Dữ liệu cũ vẫn được giữ.";}
        }
        async void CaptureSetupA(Pose observed,float width)
        {
            if(addingAnchor)return;addingAnchor=true;int request=epoch;
            try
            {
                var result=await CameraOwner.anchors.TryAddAnchorAsync(new Pose(observed.position,Quaternion.identity));
                if(!result.status.IsSuccess()){Message="Chưa giữ được A. Giữ máy ổn định rồi quét lại.";return;}
                if(this==null||request!=epoch||paused||!Tracking){CameraOwner.anchors.TryRemoveAnchor(result.value);return;}
                RemoveAnchor();anchor=result.value;
                setupA=new Pose(MapFrame.InverseTransformPoint(observed.position),Quaternion.Inverse(MapFrame.rotation)*observed.rotation);
                pendingProfile.markers.Clear();pendingProfile.markers.Add(new LabCalibratedMarker{id="A",width=width});
                setupStage=2;calibration.Reset();setupGateMarker=null;decodedMarker=null;
                Message="Đã giữ A. Để nguyên thẻ A, đi theo lối thật tới B và quét thẻ B. Chưa bật chỉ đường trong lượt đo.";
            }
            catch(Exception){Message="Chưa tạo được mốc đo A. Thử quét lại.";}
            finally{addingAnchor=false;}
        }
        static float[] Q(Quaternion q)=>new[]{q.x,q.y,q.z,q.w};
        static Quaternion Q(float[] q)=>new Quaternion(q[0],q[1],q[2],q[3]);
        async void AlignCalibrated(LabCalibratedMarker marker,Pose observed,float width)
        {
            if(addingAnchor||!ProfileReady)return;
            if(Mathf.Abs(width-marker.width)>marker.width*.12f){Message="Khổ ảnh "+marker.id+" khác lúc thiết lập. Giữ nguyên mức phóng thẻ hoặc thiết lập lại.";return;}
            var known=Map.markers.First(m=>m.id==marker.id);
            Pose worldFromMap;
            if(TrialNavigation)
            {
                var forward=Vector3.ProjectOnPlane(CameraOwner.arCamera.transform.forward,Vector3.up);
                var provisional=forward.sqrMagnitude>.1f?Quaternion.LookRotation(forward,Vector3.up):Quaternion.identity;
                worldFromMap=new Pose(observed.position-provisional*ToVector(known.centre),provisional);
            }
            else if(!LabFixedPose.TryWorldFromMap(observed,known.centre,marker.rotationInMap,out worldFromMap))
            {Message="Hướng thẻ chưa khớp trọng lực. Giữ toàn khung thẻ trong camera rồi thử lại.";return;}
            var position=worldFromMap.position;var rotation=worldFromMap.rotation;
            int request=epoch;addingAnchor=true;
            try
            {
                var result=await CameraOwner.anchors.TryAddAnchorAsync(new Pose(position,rotation));
                if(!result.status.IsSuccess()){Message="Chưa giữ được mốc. Giữ thẻ trong camera rồi thử lại.";return;}
                if(this==null||request!=epoch||paused||!Tracking){CameraOwner.anchors.TryRemoveAnchor(result.value);return;}
                AdoptNavigationAnchor(result.value);navigationTrusted=true;recovering=false;RecoveryNeedsMarker=false;journeyStarted=true;lostSince=-1;
                fixedLatchUntil=Time.realtimeSinceStartupAsDouble+1.2;fixedLatchText="Đã căn vị trí bằng mốc "+marker.id;ScanProgress=1;ScanLabel=fixedLatchText;
                poseEstimate.Reset();ResetRouteState();Route.Clear();plannedFrom=null;nextPlan=0;lastPose=null;lastFixTime=Time.realtimeSinceStartupAsDouble;
                Message="Đã căn bằng "+marker.id+". Theo chấm vị trí và chỉ dẫn; quan sát lối đi thực.";
                visible.Clear();calibration.Reset();Map.markerWidthMetres=marker.width;
                Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"calibration-profile.json"),JsonUtility.ToJson(profile,true));Save();
                if(TrialNavigation)RequestTrialHeading(marker.id);
            }
            catch(Exception){navigationTrusted=false;Message="Chưa hoàn tất căn mốc. Quét lại mốc để tiếp tục.";}
            finally{addingAnchor=false;}
        }
        void UpdateFloorRoute(double now)
        {
            UpdateCameraBlocks(now);
            if(!NavigationPoseValid)
            {
                HideGuidance();ShowPin();routeError=PoseReason;NavigationState="Vị trí chưa khớp lối đi";
                ManeuverTitle=LabPoseFeedback.Title(PoseReason);Direction=PoseFeedback;PublishDecision(now,reason:PoseReason);return;
            }
            var target=Map.markers.First(m=>m.id==Exercise.Target);var position=Position;
            if(activeRoute==null||plannedTarget!=Exercise.Target||needsRoutePlan)
            {if(plannedTarget!=Exercise.Target||now>=nextPlan){PlanRoutes(position,target.access,now);needsRoutePlan=false;}}
            if(activeRoute==null){HideGuidance();ShowPin();ManeuverTitle=routeError=="CAMERA_OBSTACLE_NO_ROUTE"?"LỐI PHÍA TRƯỚC BỊ CHẮN":"CHƯA CÓ TUYẾN";Direction=routeError=="CAMERA_OBSTACLE_NO_ROUTE"?"Camera thấy vật cản. Dừng lại, quan sát và chọn lối đã biết khác.":routeError=="NO_ROUTE_BLOCKED"?"Không còn lối đã xác nhận tới đích. Kiểm tra các lối tạm chặn trên map.":"Mở map kiểm tra vị trí và lối đi.";PublishDecision(now,reason:routeError??"NO_ROUTE");return;}
            routeProgress.Update(activeRoute.points,position);
            Route=routeProgress.RemainingPath(activeRoute.points,position,floorRouter);
            bool deviation=routeProgress.CrossTrack>.60f||Route.Count==0;
            if(deviation)
            {
                if(deviationSince<0)deviationSince=now;
                if(now-deviationSince>=.8&&now>=nextPlan)
                {
                    PlanRoutes(position,target.access,now);
                    if(activeRoute==null)
                    {
                        HideGuidance();ShowPin();ManeuverTitle="CHƯA CÓ TUYẾN";
                        Direction="Chưa có lối đã xác nhận từ vị trí hiện tại. Mở map để kiểm tra.";
                        PublishDecision(now,reason:routeError??"NO_ROUTE");return;
                    }
                    routeProgress.Update(activeRoute.points,position);
                    Route=routeProgress.RemainingPath(activeRoute.points,position,floorRouter);
                    deviation=routeProgress.CrossTrack>.60f||Route.Count==0;
                    deviationSince=deviation?now:-1;
                }
            }else deviationSince=-1;
            if(deviation&&deviationSince>=0){HideGuidance();ManeuverTitle="ĐANG CẬP NHẬT TUYẾN";Direction="Chọn đường từ vị trí hiện tại.";PublishDecision(now,reason:"OFF_ROUTE");return;}
            if(Route.Count==0){HideGuidance();ShowPin();ManeuverTitle="ĐANG TÍNH LẠI";Direction="Đứng ở lối đi; đang chọn tuyến từ vị trí hiện tại.";PublishDecision(now,reason:"REPLANNING");return;}
            if(AtTarget){HideGuidance();ShowPin();NavigationState="Đã gần đích";ManeuverTitle=LabManeuver.Title(null,0,true);Direction="Đối chiếu "+Exercise.Target+" ngoài thực tế rồi xác nhận.";PublishDecision(now,arrived:true);return;}
            if(CameraMapReady&&obstacles!=null&&obstacles.Intersects(Route,now))
            {needsRoutePlan=true;HideGuidance();ManeuverTitle="ĐANG TÌM LỐI VÒNG";Direction="Camera thấy vật cản trên tuyến; dừng để tính lại.";PublishDecision(now,reason:"LIVE_PATH_BLOCKED");return;}
            if(RouteSource=="CAMERA_GRID")
            {
                FrontFloorCoverage=floorEvidence?.FrontCoverage(Route,now)??0;
                if(FrontFloorCoverage<.7f)
                {HideGuidance();ManeuverTitle="QUÉT LỐI VÒNG";Direction="Đã quan sát "+Mathf.RoundToInt(FrontFloorCoverage*100)+"% hành lang phía trước (tối đa 1,8 m). Nhìn chậm xuống sàn và hai bên lối vòng.";PublishDecision(now,reason:"LOCAL_PATH_NEEDS_DEPTH");return;}
            }
            routeError=null;currentTurn=turnTracker.Update(routeTurns,activeRoute.points,position,routeProgress.Along,now);
            ManeuverTitle=instructionLatch.Update(LabManeuver.Title(currentTurn,routeProgress.Along),now);Direction=ManeuverTitle;
            NavigationState=poseEstimate.Current.correction>.01f?"Ước lượng · đã hiệu chỉnh mép lối":"Đang theo vị trí";
            float look=Mathf.Min(1.8f,Remaining);if(currentTurn!=null&&TurnDistance>.15f)look=Mathf.Min(look,TurnDistance);
            var aim=LabPaths.Sample(Route,Mathf.Max(.15f,look));
            PublishDecision(now,aim);
        }
        LabGuideVisual EnsureNavigationVisual()
        {
            if(navigationVisual==null)
            {
                var material=Resources.Load<Material>("Routes/LiveViewUnlit");
                if(material!=null){navigationVisual=new GameObject("Lab guide presentation").AddComponent<LabGuideVisual>();navigationVisual.Initialize(material);}
            }
            return navigationVisual;
        }
        void ShowDestination()
        {
            if(!Located||Exercise?.Target==null)return;
            var target=Map.markers.First(m=>m.id==Exercise.Target);
            // The floor model cannot provide live depth occlusion. Do not point through an authored obstacle.
            if(floorRouter!=null&&Position!=null&&!floorRouter.KnownFloorSegment(Position,target.access)){navigationVisual?.HideDestination();return;}
            EnsureNavigationVisual()?.ShowDestination(CameraOwner.arCamera,MapFrame.TransformPoint(ToVector(target.access)),target.id,TargetDescription,ui?.GuideBoundsAllowed);
        }
        void HideNavigationVisuals(){guideEdge=false;if(navigationVisual!=null)navigationVisual.Hide();}
        void PauseNavigation(bool value)
        {
            depthFrames.Clear();
            if(value&&trialHeadingPending){ResetTrialHeading();AwaitingDirection=false;RequireTrackingReference("HEADING_APP_PAUSED");}
            paused=value;trackingRecovery.BreakStability();recoverySampleId=-1;
            if(value)
            {
                BeginTrackingInterruption(Time.realtimeSinceStartupAsDouble);
                if(fixedSurvey)InterruptFixedSurvey("SURVEY_APP_PAUSED");
                epoch++;decodedMarker=null;HideGuidance();
                if(SettingUp&&setupStage>1)setupNeedsRestart=true;
                FlushNavigationTrace();
            }
            else Message="Đang khôi phục theo mốc cũ. Giữ yên máy, nhìn lại vùng vừa đi; chưa cần quét lại A.";
        }
        void RecordNavigationTrace(double now)
        {
            LiveNavigationLog(now);
            string state=NavigationState+"|"+routeError+"|"+setupStage+"|"+Located;
            if(now<traceAt&&state==traceState)return;traceAt=now+.2;traceState=state;
            var trace=new NavigationTrace{routeRevision=routeRevision,visibleTokens=presentation.visibleTokens,visualMode=presentation.mode.ToString(),visualFloorSource=VisualFloorSource,visualHeightOffset=VisualFloorOffset,presentationDecisionId=presentation.decisionId,movementBearing=movement.Bearing,movementConfidence=movement.Confidence,decisionId=decision?.id??0,cameraSampleId=cameraSampleId,providerTimestampNs=lastProviderNs,poseTimeSource=providerTimestampAvailable?"AR_CAMERA_TIMESTAMP_PROXY_NOT_NATIVE_POSE":"CALLBACK_TIME_NATIVE_POSE_TIMESTAMP_UNAVAILABLE",recoveryStage=trackingRecovery.Stage,recoveryDeadline=trackingRecovery.Deadline,lastKnownAge=LastKnownAge,cueIntent=presentation.intent.ToString(),primaryAction=presentation.title,rebaseEvent=rebaseTrace,anchorId=AnchorKey,recoveryReason=trackingRecovery.Reason,cameraFrameAge=CameraFrameAge,recoveryProgress=RecoveryProgress,markerScannerRunning=imageManager!=null&&imageManager.enabled,mapOriginWorld=MapFrame!=null?ToPoint(MapFrame.position):null,mapFrameRotation=MapFrame!=null?Q(MapFrame.rotation):null,anchorDistance=anchor!=null&&CameraOwner?.arCamera!=null?Vector3.Distance(anchor.transform.position,CameraOwner.arCamera.transform.position):0,rawPosition=RawPosition,acceptedPosition=poseEstimate.Current.accepted,displayPosition=poseEstimate.Current.display,poseReason=PoseReason,correction=poseEstimate.Current.correction,routeId=activeRoute?.id,graphHash=corridorGraph?.graphHash,along=routeProgress.Along,crossTrack=routeProgress.CrossTrack,remaining=Remaining,maneuverId=currentTurn?.id,maneuverType=currentTurn?.type,turnDistance=TurnDistance,preview=PreviewMode,mapHash=Floor?.geometrySha256,cameraRotation=CameraOwner?.arCamera!=null?Q(CameraOwner.arCamera.transform.rotation):null,anchorRotation=anchor!=null?Q(anchor.transform.rotation):null,utc=DateTime.UtcNow.ToString("O"),seconds=now,state=NavigationState,epoch=epoch,revision=Floor?.revision??0,setup=setupStage,tracking=Tracking,trackingReason=ARSession.notTrackingReason.ToString(),anchorTracking=anchor!=null&&anchor.trackingState==TrackingState.Tracking,located=Located,recovering=recovering,needsMarker=RecoveryNeedsMarker,position=Located?Position:null,rawCameraWorld=CameraOwner?.arCamera!=null?ToPoint(CameraOwner.arCamera.transform.position):null,anchorWorld=anchor!=null?ToPoint(anchor.transform.position):null,heading=Located?Heading:0,progress=ScanProgress,target=Exercise?.Target,routeError=routeError,cueVisible=navigationVisual!=null&&navigationVisual.CueVisible,cueReason=navigationVisual?.Reason};
            trace.scanQrId=decodedMarker;trace.scanQrIds=decodedMarkerCount;trace.scanPoseReason=markerPoseReason;
            trace.scanLibraryId=markerPoseId;trace.surveyContinuityReason=surveyRecovery.Reason;
            trace.surveyStage=fixedScanStage;trace.surveyRejectedId=fixedRejectId;trace.surveyRejectReason=fixedRejectReason;trace.surveySavedCount=fixedDraft?.markers.Count??0;trace.surveyRejectedPosition=fixedRejectedPosition;
            trace.scanReferenceHypotheses=visible.Values.Count(v=>v.tracking&&now-v.updated<=.5);
            EnrichTrace(trace);RecordSuiteTrace(trace);
            if(journal?.Active==true){RecordJournal(trace,now);return;}
            traceBuffer.AppendLine(JsonUtility.ToJson(trace));
            if(now>=flushAt||traceBuffer.Length>24000){flushAt=now+3;FlushNavigationTrace();}
        }
        void FlushNavigationTrace()
        {
            if(traceBuffer.Length==0||string.IsNullOrEmpty(Folder))return;
            try
            {
                Directory.CreateDirectory(Folder);string path=Path.Combine(Folder,"navigation-trace.jsonl");
                if(File.Exists(path)&&new FileInfo(path).Length>4*1024*1024){string previous=path+".previous";if(File.Exists(previous))File.Delete(previous);File.Move(path,previous);}
                File.AppendAllText(path,traceBuffer.ToString());traceBuffer.Clear();
            }
            catch(Exception){traceBuffer.Clear();Message="Chưa lưu được nhật ký. Kiểm tra dung lượng rồi xuất ZIP lại.";}
        }
    }
}
