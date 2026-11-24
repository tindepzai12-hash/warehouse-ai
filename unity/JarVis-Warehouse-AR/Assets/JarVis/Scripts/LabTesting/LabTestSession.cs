using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using JarVis.Warehouse.RouteRecording;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    // Dedicated OFFLINE test app. Never writes to OMS or claims that its fixtures are real tasks.
    public sealed partial class LabTestSession : MonoBehaviour
    {
        public const string Version="0.9.8";
        public VisualMapSession CameraOwner;
        public LabSurvey Map { get; private set; }
        public LabExercise Exercise { get; private set; }
        public bool Recording { get; private set; }
        public bool Locating { get; private set; }
        public LabFloorPlan Floor { get; private set; }
        public LabMapFit Registration=>CachedRegistration();
        public LabPoint FloorPosition=>Located&&Registration.valid?Registration.Transform(Position):null;
        public bool Ready=>ready;
        public bool NewScan=>Floor?.id=="LAB-GLB-20261028";
        public bool AwaitingDirection { get; private set; }
        public bool NeedsCalibration=>NewScan?(fixedSurvey||MarkerScanRequested||Located&&Time.realtimeSinceStartupAsDouble<fixedLatchUntil):(SettingUp||Locating||Exercise!=null&&!Exercise.Complete)&&!Located;
        LabFloorRouter floorRouter;
        readonly LabScanCalibration calibration=new LabScanCalibration();
        Pose capturedA;Vector3 captureCamera;
        double capturedAt,lastGateUpdate=-1,nextPlan;
        float capturedWidth;
        int captureEpoch;
        string routeError;
        LabPoint plannedFrom;
        string plannedTarget;
        double lastPoseTime,nextPositionLog;
        LabPoint lastPose;
        [Serializable] sealed class PositionSample {public double seconds;public LabPoint position;public float heading,estimatedMarkerWidth;public string target,routeError;public bool manualHeading=true;}
        public string Message { get; private set; }="Đang mở camera AR…";
        public string ScanLabel { get; private set; }="Chưa nhận thẻ";
        public float ScanProgress { get; private set; }
        public List<LabPoint> Route { get; private set; }=new List<LabPoint>();
        public bool Tracking {
            get {
#if UNITY_EDITOR
                if(reviewLocated||reviewCameraTracking)return true;
#endif
                return NewScan?LabTrackingRecovery.FrameUsable(CameraOwner!=null&&CameraOwner.Ready,ARSession.state==ARSessionState.SessionTracking,cameraFrameAt>=0,CameraFrameAge,paused)&&ProviderFrameFresh:CameraOwner!=null&&CameraOwner.Tracking;
            }
        }
        public bool Located {get {
#if UNITY_EDITOR
            if(reviewLocated)return true;
#endif
            return Tracking&&anchor!=null&&anchor.trackingState==TrackingState.Tracking&&!addingAnchor&&(!NewScan||navigationTrusted&&!recovering);
        }}
        public LabPoint RawPosition {get {
#if UNITY_EDITOR
            if(reviewLocated)return reviewPosition;
#endif
            return Located?ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position)):null;
        }}
        public LabPoint Position=>NewScan&&Located&&poseEstimate.Current.valid?poseEstimate.Current.accepted:RawPosition;
        public LabPoint DisplayPosition=>Located&&poseEstimate.Current.valid?poseEstimate.Current.display:LastKnownPosition;
        public bool NavigationPoseValid=>Located&&!AwaitingDirection&&poseEstimate.Current.valid;
        public string PoseReason=>poseEstimate.Current.reason;
        public float Heading {get {
#if UNITY_EDITOR
            if(reviewLocated)return 0;
#endif
            if(!Located)return 0;var f=MapFrame.InverseTransformDirection(CameraOwner.arCamera.transform.forward);return LabNavigationEstimate.HorizontalHeading(f.x,f.z,LastKnownHeading,out _);}}
        public bool AtTarget=>Located&&(!NewScan||NavigationPoseValid)&&Exercise?.Target!=null&&Map.markers.Any(m=>m.id==Exercise.Target&&LabPaths.Distance(Position,m.access)<=(NewScan?.65f:Exercise.DestinationOnly?.25f:.8f))&&(NewScan?floorRouter!=null&&floorRouter.Segment(Position,Map.markers.First(m=>m.id==Exercise.Target).access):true);
        public string Folder { get; private set; }
        public bool HasData=>Map!=null&&Map.markers.Count>0;
        public bool ScanReady=>!NewScan&&Exercise!=null&&!Exercise.Complete&&(Exercise.Target==null||AtTarget);
        public float Remaining=>LabPaths.Length(Route);
        public bool RemainingKnown=>NavigationPoseValid&&Route!=null&&Route.Count>=2&&string.IsNullOrEmpty(routeError);
        public string Direction { get; private set; }="";
        public string ExportedPath { get; private set; }
        ARTrackedImageManager imageManager;
        ARAnchor anchor;
        bool ready,addingAnchor,paused,legValid=true;
        int epoch,decodeEpoch,decodePhase;
        double sampled,nextDecode,nextPhoto,nextQuality,lostSince=-1,decodedAt,feedbackUntil;
        double lastFixTime;
        LabPoint lastPhotoPosition;
        string lastMarker;
        List<LabPoint> trail=new List<LabPoint>();
        Task<string> decoding;
        VisualMapImageQuality quality;
        readonly Dictionary<string,Observation> visible=new Dictionary<string,Observation>();
        LabTestUi ui;LabCameraAi cameraAi;
        VisualMapCue cue;
        Transform pin;
        TextMesh pinLabel;
        Material pinMaterial;
        class Observation { public Pose pose;public double updated,stableSince;public bool tracking;public float width,height;public long frame; }
        static LabPoint ToPoint(Vector3 p)=>new LabPoint(p.x,p.y,p.z);
        static Vector3 ToVector(LabPoint p)=>new Vector3(p.x,p.y,p.z);
        static float Yaw(Pose p)=>RoomRouteAlignment.Heading(p);
        string Root=>Path.Combine(Application.persistentDataPath,"lab-tests");

#if UNITY_EDITOR
        bool reviewLocated,reviewCameraTracking,reviewRuntime;LabPoint reviewPosition;
        public void EditorReviewRecovery(bool cameraReady)
        {
            EditorReviewNavigation("ĐI THẲNG",true,false,false);
            reviewLocated=false;reviewCameraTracking=cameraReady;PreviewMode=true;journeyStarted=true;recovering=!cameraReady;RecoveryNeedsMarker=cameraReady;
            LastKnownPosition=reviewPosition;NavigationState=cameraReady?"Cần căn lại vị trí":"Khôi phục · 68% giữ máy ổn định";
            Message=cameraReady?"Mở map, chọn điểm A/B/C đúng nơi đang đứng để căn nhanh lại. Đích cũ vẫn được giữ.":"Đứng yên một lát, nhìn lại vùng vừa đi. Đích và tuyến vẫn được giữ.";
        }
        public void EditorReviewNavigation(string title,bool valid,bool arrived,bool edge)
        {
            reviewLocated=true;floorRouter=new LabFloorRouter(Floor);InitializePlanner();
            Exercise=new LabExercise("C");reviewPosition=Map.markers.First(m=>m.id==(arrived?"C":"A")).access;
            poseEstimate.Update(floorRouter,reviewPosition,0,1,1,true);
            if(!valid)poseEstimate.Current.valid=false;
            PlanRoutes(Map.markers.First(m=>m.id=="A").access,Map.markers.First(m=>m.id=="C").access,1);
            ManeuverTitle=arrived?"ĐÃ GẦN ĐÍCH":title;SetReviewInstruction(edge);NavigationState=valid?"Đang theo vị trí":"Vị trí chưa khớp lối đi";
        }
        void SetReviewInstruction(bool edge){guideEdge=edge;GuideBearing=65;Direction=ManeuverTitle;}
#endif
        IEnumerator Start()
        {
            InitializeStability();
            CameraOwner=GetComponent<VisualMapSession>();
            var floorResource=Resources.Load<TextAsset>("LabTest/LabFloorPlanNew")??Resources.Load<TextAsset>("LabTest/LabFloorPlan");
            if(floorResource!=null)Floor=JsonUtility.FromJson<LabFloorPlan>(floorResource.text);
            ui=gameObject.AddComponent<LabTestUi>();ui.Owner=this;InitializeNavigationJournal();InitializeTestSuite();
            if(NewScan){floorRouter=new LabFloorRouter(Floor);InitializePlanner();LoadNewPreset();InitializeFixedMaps();}else LoadLast();
            // CameraOwner performs the existing permission and XR bootstrap. Its capture UI is not used here.
            yield return null;
            var oldHud=transform.Find("Visual Maps HUD");if(oldHud!=null)oldHud.gameObject.SetActive(false);
            var oldUi=GetComponent<VisualMapUi>();if(oldUi!=null)oldUi.enabled=false;
            while(!CameraOwner.Ready&&!Application.isEditor){Message=CameraOwner.Message;yield return null;}
            CameraOwner.enabled=false;ConfigureTrackingRuntime();
            var depth=CameraOwner.arCamera.GetComponent<AROcclusionManager>();if(depth!=null)depth.enabled=false;
            imageManager=CameraOwner.arCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>().gameObject.AddComponent<ARTrackedImageManager>();
            imageManager.enabled=false;
            var library=Resources.Load<XRReferenceImageLibrary>(NewScan?"LabTest/LabFixedMarkers":"LabTest/LabMarkers");
            if(library==null){Message="Thiếu ảnh thẻ A. Chưa thể căn vị trí.";yield break;}
            imageManager.referenceLibrary=library;imageManager.requestedMaxNumberOfMovingImages=0;
            imageManager.trackablesChanged.AddListener(Changed);imageManager.enabled=false;
            ready=true;
            cameraAi=GetComponent<LabCameraAi>();if(cameraAi==null)cameraAi=gameObject.AddComponent<LabCameraAi>();
            Message=Application.isEditor?"Xem UI trong Editor · chưa kiểm tra camera thật.":FixedSummary;
        }
        void LoadLast()
        {
            try
            {
                string selected=PlayerPrefs.GetString("JarVisLab.last","");
                if(string.IsNullOrEmpty(selected)||Path.GetFileName(selected)!=selected)return;
                Folder=Path.Combine(Root,selected);
                string file=Path.Combine(Folder,"lab-map.json");if(!File.Exists(file))return;
                var data=JsonUtility.FromJson<LabSurvey>(File.ReadAllText(file));if(LabPaths.ValidMap(data))Map=data;
            }
            catch(Exception){Message="Chưa đọc được lượt trước. Tạo lượt mới; dữ liệu cũ vẫn được giữ.";}
        }
        public void RecordNew()
        {
            if(!ready)return;StopExercise();RemoveAnchor();epoch++;
            string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6);
            Map=new LabSurvey{id=id,utc=DateTime.UtcNow.ToString("O"),device=SystemInfo.deviceModel};
            Folder=Path.Combine(Root,id);
            try{Directory.CreateDirectory(Path.Combine(Folder,"images"));}catch(Exception){Message="Không tạo được lượt ghi. Kiểm tra dung lượng điện thoại.";return;}
            Recording=true;lastMarker=null;trail.Clear();visible.Clear();legValid=true;ExportedPath=null;
            if(!Save())return;Message="Bắt đầu bằng thẻ A cố định, rộng đúng 20 cm. Hướng trọn thẻ vào khung và giữ yên.";
        }
        public void RecordMore()
        {
            if(!ready||!HasData)return;StopExercise();Recording=true;trail.Clear();lastMarker=null;legValid=true;
            Message="Nhìn một thẻ đã ghi để căn vị trí, rồi đi thêm lối khác tới mốc tiếp theo.";
        }
        public void StopRecording()
        {
            Recording=false;trail.Clear();lastMarker=null;if(!Save())return;
            Message="Đã lưu "+(Map?.markers.Count??0)+"/5 mốc và "+(Map?.legs.Count(e=>e.valid)??0)+" đoạn đi. "+(Map?.loopChecked==true?"Có đối chiếu khi quay về A.":"Cần ghi đường quay về A để kiểm tra độ lệch.");
        }
        public void BeginDestination(string target)
        {
            if(!ready)return;
            if(NewScan)
            {
                StartFixedDestination(target);return;
            }
            var resource=Resources.Load<TextAsset>("LabTest/LabNavigationPreset");
            if(resource==null){Message="Thiếu dữ liệu thử đã ghi.";return;}
            var preset=JsonUtility.FromJson<LabSurvey>(resource.text);
            if(!LabPaths.ValidMap(preset)||!LabRegistration.Solve(preset,Floor).valid){Message="Dữ liệu thử chưa khớp; chưa bật dẫn đường.";return;}
            if(Recording)StopRecording();
            StopExercise();RemoveAnchor();visible.Clear();Map=preset;
            Folder=Path.Combine(Root,"navigation-test-20261028");
            try{Directory.CreateDirectory(Folder);}catch(Exception){Message="Không lưu được lượt thử.";return;}
            if(!Save())return;
            Exercise=new LabExercise(target);epoch++;BeginNavigationJournal();
            Message="Căn tạm · chưa kiểm tra vòng về A. Quét mốc cố định đã ghi để tới "+target+". C gần B theo số đo.";
        }
        public void BeginExercise(bool transfer)
        {
            if(!ready||!HasData)return;
            if(!Registration.valid){Message=Registration.reason;ui.OpenMap(true);return;}
            if(!Map.loopChecked||!Map.markers.Any(m=>m.id=="B")||transfer&&!Map.markers.Any(m=>m.id=="C"))
            {Message="Chưa đủ dữ liệu: ghi B"+(transfer?", C":"")+" và đi về A để đối chiếu trước.";return;}
            Recording=false;Locating=false;trail.Clear();epoch++;Exercise=new LabExercise(transfer);Route.Clear();BeginNavigationJournal();
            Message=transfer?"Bài lấy/chuyển · tới B từ vị trí hiện tại. Không cần quét mã bắt đầu.":"Bài nhập kho · đưa QR hàng LAB-PACKAGE-001 vào camera.";
        }
        public void StopExercise(){EndNavigationJournal("STOP_NAVIGATION");PreviewMode=false;journeyStarted=false;Exercise=null;Locating=false;setupStage=0;Route.Clear();epoch++;HideGuidance();if(NewScan)ResetCalibration();}
        public void BeginLocate()
        {
            if(NewScan){LocateFixed();return;}
            if(!ready||!HasData){Message="Ghi mốc A và B bằng camera trước, rồi đặt mốc lên bản đồ.";return;}
            if(!Registration.valid){Message=Registration.reason;ui.OpenMap(true);return;}
            StopExercise();Locating=true;Message="Nhìn một thẻ đã ghi, ví dụ A. Mốc và vị trí điện thoại sẽ hiện trên bản đồ lab.";
        }
        public bool SetPlacement(LabMapPlacement placement)
        {
            if(NewScan)return PublishFixedPlacement(placement);
            if(!HasData||Floor==null||placement==null)return false;
            var previous=Map.placement;Map.placement=placement;
            if(!Save()){Map.placement=previous;return false;}
            Message="Đã lưu vị trí mốc trên bản đồ. "+Registration.reason;return true;
        }
        public void Back()
        {
            if(fixedSurvey){LeaveFixedSurvey();return;}
            if(Recording)StopRecording();else {StopExercise();Message="Chọn ghi lối đi hoặc một bài thử ngoại tuyến.";}
        }
        bool Save()
        {
            if(Map==null||string.IsNullOrEmpty(Folder))return false;
            try
            {
                Directory.CreateDirectory(Folder);
                string file=Path.Combine(Folder,"lab-map.json"),tmp=file+".tmp";
                File.WriteAllText(tmp,JsonUtility.ToJson(Map,true));
                if(File.Exists(file))File.Replace(tmp,file,file+".previous");else File.Move(tmp,file);
                PlayerPrefs.SetString("JarVisLab.last",Path.GetFileName(Folder));PlayerPrefs.Save();return true;
            }
            catch(Exception){Recording=false;Message="Không lưu được dữ liệu. Dừng ghi và kiểm tra dung lượng điện thoại.";return false;}
        }
        public void Export()
        {
            FlushNavigationTrace();
            if(!HasData)return;
            try
            {
                if(!Save())return;
                if(Floor!=null)File.WriteAllText(Path.Combine(Folder,"floor-plan.json"),JsonUtility.ToJson(Floor));
                ExportNavigationState();ExportFixedMaps();ExportStabilityInto(Folder);
                string file=Path.Combine(Root,"JarVis-Lab-"+Map.id+"-"+DateTime.UtcNow.ToString("HHmmss")+".zip");
                ZipFile.CreateFromDirectory(Folder,file,System.IO.Compression.CompressionLevel.Fastest,false);
                ExportedPath=VisualMapData.Export(file);Message="Đã xuất "+ExportedPath;
            }
            catch(Exception){Message="Chưa xuất được ZIP. Lượt ghi nội bộ vẫn còn; thử xuất lại.";}
        }
        void Changed(ARTrackablesChangedEventArgs<ARTrackedImage> changes)
        {
            foreach(var image in changes.added)Observe(image);
            foreach(var image in changes.updated)Observe(image);
            foreach(var removed in changes.removed)if(removed.Value!=null)visible.Remove(removed.Value.referenceImage.name);
        }
        void Observe(ARTrackedImage image)
        {
            string id=image.referenceImage.name;
            if(id.Length!=1||id[0]<'A'||id[0]>'E')return;
            if(NewScan&&id!=markerPoseId)return;
            double now=Time.realtimeSinceStartupAsDouble;Pose pose=new Pose(image.transform.position,image.transform.rotation);
            bool tracking=image.trackingState==TrackingState.Tracking&&(NewScan||Mathf.Abs(Vector3.Dot(pose.rotation*Vector3.up,Vector3.up))<.45f);
            if(!visible.TryGetValue(id,out var sample))visible[id]=sample=new Observation{stableSince=now};
            if(!tracking||!sample.tracking||now-sample.updated>.5||Vector3.Distance(pose.position,sample.pose.position)>.035f||Quaternion.Angle(pose.rotation,sample.pose.rotation)>3.5f)sample.stableSince=now;
            sample.pose=pose;sample.updated=now;sample.tracking=tracking;sample.width=image.size.x;sample.height=image.size.y;sample.frame=cameraSampleId;
        }
        async void Align(string id,Pose observed)
        {
            if(addingAnchor)return;addingAnchor=true;int request=epoch;
            try
            {
                var known=Map.markers.FirstOrDefault(m=>m.id==id);
                Quaternion rotation=Quaternion.Euler(0,Yaw(observed)-(known?.yaw??0),0);
                Vector3 position=observed.position-rotation*(known==null?Vector3.zero:ToVector(known.centre));
                var result=await CameraOwner.anchors.TryAddAnchorAsync(new Pose(position,rotation));
                if(!result.status.IsSuccess()){Message="Chưa giữ được mốc AR. Giữ máy ổn định rồi thử lại.";return;}
                if(this==null||request!=epoch||!Tracking||paused){CameraOwner.anchors.TryRemoveAnchor(result.value);return;}
                RemoveAnchor();anchor=result.value;lastMarker=null;trail.Clear();lastFixTime=Time.realtimeSinceStartupAsDouble;
                Message="Đã định vị bằng mốc "+id+". "+(Recording?"Đi chậm theo lối trống tới mốc tiếp theo.":"Vị trí của bạn đã hiện trên sơ đồ.");
                if(Recording)AcceptMarker(id,observed);
            }
            catch(Exception){Message="Chưa tạo được mốc AR. Chưa bật dẫn đường; giữ máy yên và thử lại.";}
            finally{addingAnchor=false;}
        }
        void AcceptMarker(string id,Pose observed)
        {
            if(anchor==null)return;
            Vector3 position=MapFrame.InverseTransformPoint(observed.position);
            var existing=Map.markers.FirstOrDefault(m=>m.id==id);
            float markerYaw=Mathf.DeltaAngle(MapFrame.eulerAngles.y,Yaw(observed));
            if(existing!=null)
            {
                float error=Vector3.Distance(ToVector(existing.centre),position),angle=Mathf.Abs(Mathf.DeltaAngle(existing.yaw,markerYaw));
                Map.observations.Add(new LabObservation{marker=id,utc=DateTime.UtcNow.ToString("O"),positionError=error,yawError=angle});
                if(error>.25f||angle>6)
                {
                    legValid=false;Map.interrupted=true;trail.Clear();lastMarker=null;RemoveAnchor();visible[id].stableSince=Time.realtimeSinceStartupAsDouble;
                    Save();Message="Mốc "+id+" lệch "+error.ToString("F2")+" m / "+angle.ToString("F0")+"°. Đã dừng ghép lối; giữ thẻ trong khung để căn lại.";return;
                }
                lastFixTime=Time.realtimeSinceStartupAsDouble;
                if(id=="A"&&Map.markers.Count>=2)Map.loopChecked=true;
            }
            else
            {
                if(Map.markers.Count>=5)return;
                existing=new LabMarker{id=id,centre=ToPoint(position),access=ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position)),yaw=markerYaw};
                Map.markers.Add(existing);existing.photo=Photo(id);
            }
            var here=ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position));
            if(lastMarker!=null&&lastMarker!=id&&trail.Count>1)
            {
                trail.Add(here);
                bool valid=legValid&&LabPaths.Distance(here,existing.access)<=.6f;
                if(valid&&Map.legs.Count<100)Map.legs.Add(new LabLeg{from=lastMarker,to=id,valid=true,points=trail.ToArray()});
                else Message="Chưa nối lối tới "+id+": cần đứng gần chỗ đã ghi hoặc tracking đã gián đoạn.";
            }
            lastMarker=id;trail.Clear();trail.Add(here);legValid=true;sampled=Time.realtimeSinceStartupAsDouble;
            Save();feedbackUntil=Time.realtimeSinceStartupAsDouble+2;
            Message="Đã ghi mốc "+id+" · "+Map.markers.Count+"/5. "+(Map.markers.Count==5&&!Map.loopChecked?"Đi về A để đối chiếu độ lệch.":"Đi tới mốc khác qua lối cần lưu.");
        }
        string Photo(string id)
        {
            try
            {
                if(Map.photos.Count>=600||quality.Unusable)return null;
                if(!CameraOwner.cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))return null;
                var frame=new VisualMapFrame{width=640,height=Mathf.RoundToInt(640f*image.height/image.width),cameraTime=image.timestamp};image.Dispose();
                byte[] data=VisualMapSensor.ColorJpeg(CameraOwner.cameraManager,frame);if(data==null)return null;
                string relative="images/"+id+"-"+DateTime.UtcNow.ToString("HHmmssfff")+".jpg";File.WriteAllBytes(Path.Combine(Folder,relative),data);
                var rotation=Quaternion.Inverse(MapFrame.rotation)*CameraOwner.arCamera.transform.rotation;
                var photo=new LabPhoto{file=relative,marker=id,utc=DateTime.UtcNow.ToString("O"),cameraPosition=ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position)),cameraRotation=new[]{rotation.x,rotation.y,rotation.z,rotation.w},width=frame.width,height=frame.height,cameraTime=frame.cameraTime};
                if(CameraOwner.cameraManager.TryGetIntrinsics(out var intrinsics))
                {
                    float sx=frame.width/(float)intrinsics.resolution.x,sy=frame.height/(float)intrinsics.resolution.y;
                    photo.fx=intrinsics.focalLength.x*sx;photo.fy=intrinsics.focalLength.y*sy;photo.cx=intrinsics.principalPoint.x*sx;photo.cy=frame.height-1-intrinsics.principalPoint.y*sy;
                }
                Map.photos.Add(photo);lastPhotoPosition=photo.cameraPosition;return relative;
            }
            catch(Exception){return null;}
        }
        void Update()
        {
            if(!ready||paused)return;double now=Time.realtimeSinceStartupAsDouble;
            if(now<stabilityBackoff)return;
            try{TickStability(now);UpdateSession(now);}
            catch(Exception e)
            {
                ReportStability("SESSION_UPDATE",e);stabilityBackoff=now+.5;HideGuidance();
                poseEstimate.Current.valid=false;
                Message="Camera vừa gián đoạn. Đứng yên một lát để app thử lại; mốc và đích vẫn được giữ.";
            }
        }
        void UpdateSession(double now)
        {
            if(now>nextQuality)
            {
                nextQuality=now+1;
                try{quality=VisualMapSensor.Quality(CameraOwner.cameraManager);}
                catch(Exception e){ReportStability("IMAGE_QUALITY",e);}
            }
            TickTestSuite(now);if(NewScan){if(fixedSurvey){UpdateFixedSurvey(now);RecordNavigationTrace(now);}else UpdateNavigation(now);return;}
            if(!Tracking)
            {
                if(lostSince<0)lostSince=now;
                ScanProgress=0;ScanLabel=VisualMapCoach.TrackingHint(ARSession.notTrackingReason);
                if(now-lostSince>.8&&anchor!=null){Map.interrupted=true;trail.Clear();lastMarker=null;RemoveAnchor();Save();}
                HideGuidance();return;
            }
            lostSince=-1;
            if(Located&&Recording&&lastMarker!=null&&now-sampled>=.2)
            {
                sampled=now;var point=Position;
                if(trail.Count==0||LabPaths.Distance(point,trail[trail.Count-1])>=.08f)
                {
                    if(trail.Count>0&&LabPaths.Distance(point,trail[trail.Count-1])>.65f){legValid=false;Message="Vị trí vừa nhảy. Đoạn này sẽ không được dùng chỉ đường; nhìn lại một mốc đã ghi.";}
                    if(trail.Count<10000)trail.Add(point);else {legValid=false;Message="Đoạn ghi đã dài. Nhìn mốc tiếp theo để chia đoạn.";}
                }
                if(now>=nextPhoto&&trail.Count>1&&(lastPhotoPosition==null||LabPaths.Distance(Position,lastPhotoPosition)>.25f)){nextPhoto=now+3;Photo("walk");}
            }
            ScanProgress=0;ScanLabel=quality.Available&&quality.Mean<40?"Khu vực thiếu sáng · bật thêm đèn hoặc hướng camera về vùng có chi tiết.":"Đưa trọn thẻ A–E vào khung; giữ yên để nhận mốc.";
            foreach(var pair in visible)
            {
                var sample=pair.Value;if(!sample.tracking||now-sample.updated>.35)continue;
                if(!Recording&&(Map==null||!Map.markers.Any(m=>m.id==pair.Key)))continue;
                float progress=Mathf.Clamp01((float)((now-sample.stableSince)/.9));
                if(progress>=ScanProgress){ScanProgress=progress;ScanLabel="Nhận mốc "+pair.Key+" · "+Mathf.FloorToInt(progress*100)+"% ổn định";}
                if(progress<1||addingAnchor)continue;
                if(!Located)
                {
                    if(Map==null||Map.markers.Count==0){if(!Recording||pair.Key!="A")continue;}
                    else if(!Map.markers.Any(m=>m.id==pair.Key))continue;
                    Align(pair.Key,sample.pose);break;
                }
                var known=Map.markers.FirstOrDefault(m=>m.id==pair.Key);
                if(known!=null)
                {
                    float error=Vector3.Distance(MapFrame.InverseTransformPoint(sample.pose.position),ToVector(known.centre));
                    float angle=Mathf.Abs(Mathf.DeltaAngle(known.yaw,Mathf.DeltaAngle(MapFrame.eulerAngles.y,Yaw(sample.pose))));
                    if(!Recording&&(error>.25f||angle>6))
                    {
                        RemoveAnchor();sample.stableSince=now;Route.Clear();Message="Mốc không khớp vị trí đang ước lượng. Đã ẩn mũi tên; giữ thẻ yên để định vị lại.";break;
                    }
                    if(error<=.25f&&angle<=6)lastFixTime=now;
                }
                if(Recording&&pair.Key!=lastMarker&&now>feedbackUntil){AcceptMarker(pair.Key,sample.pose);break;}
            }
            if(Exercise!=null&&!Exercise.Complete)
            {
                UpdateRoute();
                if(ScanReady&&now>=nextDecode&&decoding==null){nextDecode=now+.4;StartDecode(now);}
                CompleteDecode(now);
            }
            else HideGuidance();
        }
        bool LoadNewPreset()
        {
            var text=Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew");
            if(text==null){Message="Thiếu bản đồ scan mới.";return false;}
            var preset=JsonUtility.FromJson<LabSurvey>(text.text);
            if(!LabPaths.ValidMap(preset)||!LabRegistration.Solve(preset,Floor).valid){Message="Dữ liệu map mới chưa khớp.";return false;}
            preset.placement=ConfirmedPlacement();
            FlushNavigationTrace();Map=preset;Folder=Path.Combine(Root,preset.id+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));return true;
        }
        public void RetryCalibration()
        {
            if(addingAnchor)return;
            ResetCalibration();
            if(NewScan&&!PreviewMode){Message="Quét một mốc đã lưu gần bạn (A–E). Đích vẫn được giữ.";return;}
            Message=PreviewMode?"Quét A rồi đứng trước bàn A, nhìn về B để căn nhanh.":ProfileReady?"Quét A; khi tiếp tục lượt cũ có thể dùng B đã thiết lập.":"Thiết lập A–B trước khi dùng dẫn đường.";
        }
        void ResetCalibration()
        {
            epoch++;ResetTrialHeading();ResetCameraEvidence();AwaitingDirection=false;capturedAt=0;calibration.Reset();lastGateUpdate=-1;visible.Clear();RemoveAnchor();Route.Clear();
            trackingRecovery.Reset();previewOriginId="A";ResetRouteState();poseEstimate.Reset();previewHeading.Reset();plannedFrom=null;plannedTarget=null;ScanProgress=0;routeError=null;lastPose=null;lastPoseTime=0;fitCache=null;
            navigationTrusted=false;recovering=false;RecoveryNeedsMarker=false;anchorStableSince=-1;lostSince=-1;setupNeedsRestart=false;guideEdge=false;decodedMarker=null;setupGateMarker=null;previewQrSince=-1;
        }
        // Compatibility entry only. Manual camera bearing is no longer used by this version.
        public void ConfirmDirection(){if(trialHeadingPending)ConfirmTrialHeading();else if(PreviewMode)ConfirmPreviewDirection();else Message="Dùng Thiết lập A–B để lưu hướng thẻ."; }
        public void ConfirmArrival()
        {
            if(!NewScan||Exercise==null||!AtTarget)return;
            Exercise.ConfirmArrival(true);Route.Clear();HideGuidance();Message="Bạn đã xác nhận tới "+Exercise.Destination+". Chưa xác thực bằng QR đích.";SaveExercise();
            if(journal?.Active==true){traceAt=0;RecordNavigationTrace(Time.realtimeSinceStartupAsDouble);EndNavigationJournal("ARRIVAL_CONFIRMED");}
        }
        void StartDecode(double now)
        {
            if(!CameraOwner.cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))return;
            try
            {
                var plane=image.GetPlane(0);int stride=Math.Max(1,image.width/900),w=image.width/stride,h=image.height/stride;
                byte[] pixels=new byte[w*h];for(int row=0;row<h;row++)for(int col=0;col<w;col++)pixels[row*w+col]=plane.data[row*stride*plane.rowStride+col*stride*plane.pixelStride];
                decodedAt=now;decodeEpoch=epoch;decodePhase=Exercise.Phase;
                decoding=Task.Run(()=>WalkingRouteRecorder.Decode(pixels,w,h));
            }
            finally{image.Dispose();}
        }
        void CompleteDecode(double now)
        {
            if(decoding==null||!decoding.IsCompleted)return;
            var task=decoding;decoding=null;
            if(task.IsFaulted){_=task.Exception;return;}
            if(task.Result==null||decodeEpoch!=epoch||Exercise==null||decodePhase!=Exercise.Phase||now-decodedAt>.8||!ScanReady)return;
            string code=task.Result;
            // Do not report stationary location markers as wrong products over and over.
            if(Exercise.Phase==0&&code.StartsWith("JWM:MAIN:",StringComparison.Ordinal))return;
            string error=Exercise.Scan(code,AtTarget);
            if(error!=null){Message=error=="WRONG_PRODUCT"?"Sai hàng. Cần QR LAB-PACKAGE-001.":error=="WRONG_DESTINATION"?"Sai mốc đích. Cần thẻ "+Exercise.Target+".":"Chưa tới gần đích. Đi theo lối đã ghi.";nextDecode=now+1.5;return;}
            epoch++;Route.Clear();feedbackUntil=now+2;
            Message=Exercise.Complete?"Đã tới đúng mốc và xác nhận mã đích. Bài thử ngoại tuyến hoàn tất.":"Đúng hàng · tiếp theo tới "+Exercise.Target+" và quét thẻ đích để hoàn tất.";
            SaveExercise();
        }
        void SaveExercise()
        {
            try{File.AppendAllText(Path.Combine(Folder,"exercise-events.jsonl"),"{\"utc\":\""+DateTime.UtcNow.ToString("O")+"\",\"exercise\":\""+(Exercise.DestinationOnly?"NAVIGATION_TEST":Exercise.Transfer?"TRANSFER":"PUTAWAY")+"\",\"phase\":"+Exercise.Phase+"}\n");}catch(Exception){Message+=" Chưa lưu được nhật ký bài thử.";}
        }
        void UpdateRoute()
        {
            Route.Clear();Direction="";
            if(!Registration.valid){HideGuidance();Direction=Registration.reason;return;}
            if(Exercise.Target==null){HideGuidance();return;}
            if(!Located){HideGuidance();Direction="Chưa định vị · nhìn một mốc A–E đã ghi";return;}
            if(AtTarget){HideGuidance();ShowPin();Direction=Exercise.Phase==0?"Đã gần B · quét QR hàng thử":"Đã gần "+Exercise.Target+" · quét thẻ "+Exercise.Target;return;}
            string error=LabPaths.Find(Map,Position,Exercise.Target,out var route);
            if(error!=null){HideGuidance();Direction=error=="OFF_WALKED_PATH"?"Bạn đang ngoài lối đã ghi. Quay về lối đã ghi hoặc ghi bổ sung.":"Chưa có lối nối tới "+Exercise.Target+". Ghi thêm đoạn đi trước.";return;}
            Route=route;var aim=LabPaths.Sample(Route,Mathf.Min(1.8f,Remaining));
            Vector3 world=MapFrame.TransformPoint(ToVector(aim));
            Vector3 forward=Vector3.ProjectOnPlane(CameraOwner.arCamera.transform.forward,Vector3.up),along=Vector3.ProjectOnPlane(world-CameraOwner.arCamera.transform.position,Vector3.up);
            float angle=Vector3.SignedAngle(forward,along,Vector3.up);
            Direction=Mathf.Abs(angle)>125?"Quay lại theo lối đã ghi":angle>35?"Theo lối sang phải":angle< -35?"Theo lối sang trái":"Đi thẳng theo lối đã ghi";
            if(cue==null)
            {
                var source=Resources.Load<Material>("Routes/LiveViewUnlit");if(source==null)return;
                cue=new GameObject("Lab purpose driven chevrons").AddComponent<VisualMapCue>();cue.Initialize(source);
            }
            cue.gameObject.SetActive(true);
            // Floating ribbon follows recorded camera height; no invented floor detection or room mesh.
            world.y-=.65f;cue.Show(CameraOwner.arCamera,world,angle,Mathf.Abs(angle)>35);
            ShowPin();
        }
        void ShowPin()
        {
            if(!Located||Exercise?.Target==null)return;
            if(NewScan){ShowDestination();return;}
            if(pin==null)
            {
                var source=Resources.Load<Material>("Routes/LiveViewUnlit");if(source==null)return;
                pinMaterial=new Material(source);pinMaterial.SetColor("_BaseColor",new Color(.04f,.27f,.95f));
                var beacon=GameObject.CreatePrimitive(PrimitiveType.Sphere);Destroy(beacon.GetComponent<Collider>());pin=beacon.transform;pin.localScale=Vector3.one*.19f;beacon.GetComponent<MeshRenderer>().sharedMaterial=pinMaterial;
                pinLabel=new GameObject("Destination label",typeof(TextMesh)).GetComponent<TextMesh>();pinLabel.fontSize=50;pinLabel.characterSize=.014f;pinLabel.anchor=TextAnchor.LowerCenter;pinLabel.color=Color.white;
            }
            var target=Map.markers.First(m=>m.id==Exercise.Target);
            pin.gameObject.SetActive(true);pin.position=MapFrame.TransformPoint(ToVector(target.centre));
            pinLabel.gameObject.SetActive(true);pinLabel.text="ĐÍCH "+Exercise.Target;pinLabel.transform.position=pin.position+Vector3.up*.22f;
            pinLabel.transform.rotation=Quaternion.LookRotation(pinLabel.transform.position-CameraOwner.arCamera.transform.position,Vector3.up);
        }
        void HideGuidance(){HideNavigationVisuals();if(cue!=null)cue.gameObject.SetActive(false);if(pin!=null)pin.gameObject.SetActive(false);if(pinLabel!=null)pinLabel.gameObject.SetActive(false);}
        void RemoveAnchor(){if(mapFrame!=null){Destroy(mapFrame.gameObject);mapFrame=null;}HideGuidance();if(anchor!=null){if(CameraOwner.anchors!=null)CameraOwner.anchors.TryRemoveAnchor(anchor);anchor=null;}}
        void OnApplicationPause(bool value)
        {
            markerSave.Clear();
            stability?.Write(value?"PAUSE":"RESUME",Version);
            PauseTestSuite(value);PauseNavigationJournal(value);
            if(NewScan){PauseNavigation(value);return;}
            paused=value;if(!value)return;epoch++;visible.Clear();trail.Clear();lastMarker=null;RemoveAnchor();
            if(NewScan)ResetCalibration();
            if(Map!=null){Map.interrupted|=Recording;Save();}Message="Đã tạm dừng. Nhìn một mốc đã ghi để định vị lại; dữ liệu đã lưu vẫn còn.";
        }
        void OnApplicationQuit(){CloseStability();CloseTestSuite();EndNavigationJournal("APP_QUIT");}
        void OnDestroy()
        {
            CloseStability();
            CloseTestSuite();EndNavigationJournal("SESSION_DESTROYED");
            FlushNavigationTrace();if(navigationVisual!=null){if(Application.isPlaying)Destroy(navigationVisual.gameObject);else DestroyImmediate(navigationVisual.gameObject);}
            epoch++;if(CameraOwner?.cameraManager!=null)CameraOwner.cameraManager.frameReceived-=CameraFrameReceived;if(imageManager!=null)imageManager.trackablesChanged.RemoveListener(Changed);RemoveAnchor();
            if(decoding!=null)decoding.ContinueWith(t=>{if(t.IsFaulted)_=t.Exception;});
            if(markerDecode!=null)markerDecode.ContinueWith(t=>{if(t.IsFaulted)_=t.Exception;});
            ReleaseMarkerCameraReadback();
            if(cue!=null)Destroy(cue.gameObject);if(pin!=null)Destroy(pin.gameObject);if(pinLabel!=null)Destroy(pinLabel.gameObject);if(pinMaterial!=null)Destroy(pinMaterial);
        }
    }
}
