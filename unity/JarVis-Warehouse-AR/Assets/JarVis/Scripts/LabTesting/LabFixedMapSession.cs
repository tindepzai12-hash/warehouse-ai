using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabFixedRepository fixedRepository;
        LabFixedMap fixedActive,fixedDraft,fixedTrial;
        LabFixedMap FixedNavigationMap=>fixedActive??fixedTrial;
        public bool TrialNavigation=>fixedActive==null&&fixedTrial!=null;
        public bool FixedPublishedReady=>fixedActive!=null;
        bool fixedSurvey,fixingDraftReference;
        double fixedLatchUntil,lastSurveyAccept=-10;
        string fixedLatchText,lastSurveyMarker;
        int fixedVerifiedCount;
        double scanAttemptAt=-1;
        public bool ScanNeedsHelp=>scanAttemptAt>=0&&Time.realtimeSinceStartupAsDouble-scanAttemptAt>=8&&NeedsCalibration&&ScanProgress<1;
        public void RetryFixedScan()
        {
            if(addingAnchor)return;
            epoch++;decodedMarker=null;decodedMarkerCount=0;visible.Clear();calibration.Reset();lastGateUpdate=-1;setupGateMarker=null;
            scanAttemptAt=Time.realtimeSinceStartupAsDouble;ScanProgress=0;fixedLatchUntil=0;
            Message="Đang quét lại. Giữ một thẻ giấy trong khung; mốc đã lưu và đích được giữ.";
        }
        public bool FixedSurvey=>fixedSurvey;
        public bool FixedReady=>FixedNavigationMap!=null;
        public bool FixedDraftAvailable=>fixedDraft!=null;
        public int FixedMarkerCount=>(fixedSurvey?fixedDraft:FixedNavigationMap)?.markers.Count??0;
        public bool FixedScanning=>fixedSurvey||!FixedReady&&(Locating||Exercise!=null);
        public bool CanChooseFixed(string id)=>LabPrintedCard.Id(id)&&FixedNavigationMap?.markers.Any(m=>m.id==id)==true;
        public string FixedSummary=>fixedSurvey?"Đã lưu nháp "+FixedMarkerCount+"/5 mốc · "+(fixedDraft.loopChecked?"đã kiểm tra quay lại":"cần quay lại một mốc để kiểm tra"):
            TrialNavigation?"Chỉ đường thử A–E · vị trí xấp xỉ theo map bạn đã chọn. Chọn đích, quét một mốc gần bạn.":FixedReady?"Đã lưu "+FixedMarkerCount+"/5 mốc · chọn đích hoặc xem vị trí":FixedDraftNeedsRepair?"Đã lưu nháp "+fixedDraft.markers.Count+"/5 · có mốc cần đối chiếu · có thể tiếp tục ghi":fixedDraft!=null?"Đã lưu nháp "+fixedDraft.markers.Count+"/5 · "+(fixedDraft.loopChecked?"cần lưu vị trí trên map":"cần kiểm tra quay lại")+" · chỉ đường chưa bật":"Chưa có bộ mốc đã căn map · thiết lập A–E một lần";
        void InitializeFixedMaps()
        {
            fixedRepository=new LabFixedRepository(Path.Combine(Application.persistentDataPath,"navigation","maps",Floor.id),Floor,
                v=>JsonUtility.ToJson(v,true),v=>JsonUtility.FromJson<LabFixedMap>(v),v=>JsonUtility.ToJson(v,true),v=>JsonUtility.FromJson<LabFixedPointer>(v));
            fixedActive=fixedRepository.LoadActive(out string reason);fixedDraft=fixedRepository.LoadDraft();
            LoadFixedTrial();
            if(FixedReady)ApplyFixedMap();
            else {profile=null;profileReady=false;Message=reason=="NO_ACTIVE_MAP"?FixedSummary:"Bộ mốc chưa đọc được. Dữ liệu cũ vẫn được giữ; có thể tiếp tục bản nháp.";}
        }
        void ApplyFixedMap()
        {
            var source=FixedNavigationMap;
            Map=JsonUtility.FromJson<LabSurvey>(JsonUtility.ToJson(source.survey));
            profile=new LabCalibrationProfile{schema=2,mapId=Floor.id,revision=Floor.revision,geometrySha256=Floor.geometrySha256,loopChecked=source.loopChecked,loopPositionError=source.loopError,loopAngleError=source.loopAngle};
            foreach(var m in source.markers)profile.markers.Add(new LabCalibratedMarker{id=m.id,width=m.imageWidth,rotationInMap=(float[])m.rotation.Clone()});
            profileReady=TrialNavigation?LabTrialNavigation.Validate(source,Floor)==null:profile.Matches(Floor);fitCache=null;
            Folder=Path.Combine(Root,(TrialNavigation?"trial-":"fixed-")+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));
        }
        public void BeginFixedSurvey(bool fresh=false)
        {
            if(!ready||addingAnchor)return;
            markerSave.Clear();
            StopExercise();RemoveAnchor();fixedSurvey=true;fixingDraftReference=false;lastSurveyMarker=null;fixedLatchUntil=0;lastSurveyAccept=-10;
            ResetSurveyContinuity();fixedLatchProgress=1;fixedScanStage="SEARCHING";fixedRejectId=fixedRejectReason="";fixedRejectedPosition=null;
            if(fresh||fixedDraft==null)
            {
                var survey=new LabSurvey{id="fixed-draft-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss"),utc=DateTime.UtcNow.ToString("O"),device=SystemInfo.deviceModel,automaticMarkerSize=true,markerWidthMetres=LabPrintedCard.ImageWidthMetres,coordinateSystem="metres; gravity aligned; draft reference; full marker quaternions",purpose="Fixed A-E draft; no navigation until registered and loop checked"};
                survey.placement=ConfirmedPlacement();
                fixedDraft=new LabFixedMap{mapId=Floor.id,floorRevision=Floor.revision,floorHash=Floor.geometrySha256,device=SystemInfo.deviceModel,survey=survey};
                if(!TrySaveFixedDraft()){fixedSurvey=false;return;}
            }
            Map=fixedDraft.survey;fitCache=null;profileReady=false;profile=null;
            Folder=Path.Combine(Root,Map.id);Directory.CreateDirectory(Folder);
            Message=fixedDraft.markers.Count==0?"Quét một thẻ A–E. Khi nhận đúng mã và đủ mẫu ổn định, bấm Lưu mốc.":"Quét một mốc đã lưu để nối tọa độ, rồi quét thẻ mới và bấm Lưu mốc.";
            NavigationState="Ghi mốc cố định · "+FixedMarkerCount+"/5";
        }
        public void FinishFixedSurvey()
        {
            if(!fixedSurvey||addingAnchor)return;
            markerSave.Clear();
            if(!TrySaveFixedDraft())return;fixedSurvey=false;RemoveAnchor();navigationTrusted=false;SetMarkerScanning(false);
            Message="Đã giữ bản nháp. Chạm vị trí ước lượng của từng thẻ rồi Lưu nháp vị trí. Chưa cần quay vòng để xuất dữ liệu.";
            ui.OpenMap(true);
        }
        public void LeaveFixedSurvey()
        {
            if(addingAnchor)return;
            markerSave.Clear();
            if(!fixedSurvey)return;if(!TrySaveFixedDraft())return;fixedSurvey=false;RemoveAnchor();navigationTrusted=false;SetMarkerScanning(false);
            if(FixedReady)ApplyFixedMap();Message="Đã giữ bản nháp để tiếp tục sau. "+FixedSummary;
        }
        void UpdateFixedSurvey(double now)
        {
            markerSave.Clear();
            SetMarkerScanning(true);HideGuidance();
            if(!CheckFixedContinuity(now))return;
            if(now<fixedLatchUntil){ScanProgress=fixedLatchProgress;ScanLabel=fixedLatchText;return;}
            NavigationState="Ghi mốc cố định · "+FixedMarkerCount+"/5";
            if(!Tracking){calibration.Reset();ScanProgress=0;fixedScanStage="WAITING_TRACKING";ScanLabel=TrackingCoach();return;}
            if(anchor!=null&&anchor.trackingState!=TrackingState.Tracking)
            {
                if(lostSince<0)lostSince=now;
                ScanProgress=0;ScanLabel="Đang giữ bản nháp · nhìn lại một mốc đã ghi để nối tọa độ";
                if(now-lostSince>2){RemoveAnchor();epoch++;lastSurveyMarker=null;navigationTrusted=false;calibration.Reset();}
                return;
            }
            lostSince=-1;
            UpdateMarkerDecode(now);
            string id=decodedMarker;
            if(!LabPrintedCard.Id(id)){ScanProgress=0;fixedScanStage="SEARCHING";ScanLabel=decodedMarkerCount>1&&now-decodedMarkerAt<=.85?"Đọc được nhiều mã A–E · đưa một thẻ cần ghi vào camera":quality.Available&&quality.Mean<40?"Thiếu ánh sáng · bật thêm đèn để thấy khung thẻ":"Đang tìm thẻ A–E · đưa trọn QR và khung hoa văn vào camera";if(ScanNeedsHelp)ScanLabel+="\nChưa nhận được · thử lùi máy để thấy đủ khung";return;}
            fixedScanStage="CHECKING_POSE";
            if(!LabPrintedCard.Id(id)||!StableMarker(id,now,out var image))return;
            if(now-lastSurveyAccept<1.5)return;
            if(id==lastSurveyMarker&&anchor!=null)
            {fixedScanStage="ALREADY_SAVED";ScanProgress=1;ScanLabel="Mốc "+id+" đã lưu · "+FixedMarkerCount+"/5\nĐi tới thẻ khác; quay lại mốc này sau để kiểm tra.";return;}
            if(anchor==null)
            {
                var known=fixedDraft.markers.FirstOrDefault(m=>m.id==id);
                if(fixedDraft.markers.Count>0&&known==null){fixedScanStage="NEED_SAVED_MARKER";ScanProgress=0;ScanLabel="Đã đọc "+id+" · chưa lưu vị trí\nCần nối lại bằng "+string.Join(" / ",fixedDraft.markers.Select(m=>m.id))+" đã lưu gần bạn.";return;}
                if(known==null){OfferMarkerSave(id,image.pose,now);return;}
                CaptureFixedReference(id,image.pose,known);return;
            }
            if(fixedDraft.markers.Any(m=>m.id==id))AcceptFixedObservation(id,image.pose);
            else OfferMarkerSave(id,image.pose,now);
        }
        bool TrySaveFixedDraft()
        {
            try{fixedRepository.SaveDraft(fixedDraft);return true;}
            catch(Exception e){Message="Chưa lưu được bản nháp: "+e.GetType().Name+". Kiểm tra dung lượng; bản trước vẫn còn.";return false;}
        }
        async void CaptureFixedReference(string id,Pose observed,LabFixedMarker known)
        {
            if(addingAnchor)return;addingAnchor=true;int request=epoch;
            try
            {
                Quaternion rotation=Quaternion.identity;Vector3 origin;
                if(known==null)
                {
                    // Horizontal draft axes use camera heading, never the normal of a flat card.
                    var f=Vector3.ProjectOnPlane(CameraOwner.arCamera.transform.forward,Vector3.up);
                    rotation=Quaternion.LookRotation(f.sqrMagnitude>.01f?f.normalized:Vector3.forward,Vector3.up);
                    origin=observed.position;origin.y=CameraOwner.arCamera.transform.position.y-1.35f;
                }
                else
                {
                    if(!LabFixedPose.TryWorldFromMap(observed,known.centre,known.rotation,out var worldFromMap))
                    {Message="Hướng thẻ chưa khớp trọng lực. Nhìn trọn khung rồi quét lại.";return;}
                    rotation=worldFromMap.rotation;origin=worldFromMap.position;
                }
                var result=await CameraOwner.anchors.TryAddAnchorAsync(new Pose(origin,rotation));
                if(!result.status.IsSuccess()){Message="Chưa giữ được mốc AR. Bản nháp vẫn còn.";return;}
                if(this==null||request!=epoch||paused||!Tracking){CameraOwner.anchors.TryRemoveAnchor(result.value);return;}
                AdoptNavigationAnchor(result.value);fixingDraftReference=false;navigationTrusted=true;recovering=false;
                ResetSurveyContinuity();Map.interrupted=false;routeError="";
                if(known==null)AcceptFixedObservation(id,observed);else
                {lastSurveyMarker=id;lastSurveyAccept=Time.realtimeSinceStartupAsDouble;fixedLatchUntil=lastSurveyAccept+1.2;fixedLatchProgress=0;fixedScanStage="REFERENCE_RESTORED";fixedLatchText="Đã nối lại bằng "+id+" · giữ "+FixedMarkerCount+" mốc\nĐi tới mốc khác rồi quay lại để kiểm tra.";calibration.Reset();TrySaveFixedDraft();}
            }
            catch(Exception e){Message="Chưa nối được mốc: "+e.GetType().Name+". Bản nháp vẫn còn.";}
            finally{addingAnchor=false;}
        }
        void AcceptFixedObservation(string id,Pose observed)
        {
            var previous=JsonUtility.FromJson<LabFixedMap>(JsonUtility.ToJson(fixedDraft));
            try{AcceptFixedObservationCore(id,observed);}
            catch(Exception e)
            {fixedDraft=previous;Map=previous.survey;fixedRejectId=id;fixedRejectReason="WRITE_"+e.GetType().Name;fixedScanStage="WRITE_FAILED";ScanProgress=0;fixedLatchProgress=0;calibration.Reset();fixedLatchUntil=Time.realtimeSinceStartupAsDouble+2;Message="Chưa lưu được mốc: "+e.GetType().Name+". Kiểm tra dung lượng rồi quét lại; bản trước vẫn còn.";fixedLatchText=ScanLabel=Message;}
        }
        void AcceptFixedObservationCore(string id,Pose observed)
        {
            var centre=ToPoint(MapFrame.InverseTransformPoint(observed.position));
            var rotation=Quaternion.Inverse(MapFrame.rotation)*observed.rotation;
            var known=fixedDraft.markers.FirstOrDefault(m=>m.id==id);
            double now=Time.realtimeSinceStartupAsDouble;
            if(known!=null)
            {
                float error=Vector3.Distance(ToVector(centre),ToVector(known.centre)),angle=Quaternion.Angle(rotation,Q(known.rotation));
                Map.observations.Add(new LabObservation{marker=id,utc=DateTime.UtcNow.ToString("O"),positionError=error,yawError=angle});
                // Re-localizing from this same observation is not independent verification.
                if(fixingDraftReference){fixingDraftReference=false;lastSurveyMarker=id;return;}
                if(fixedDraft.markers.Count<2||id==lastSurveyMarker)return;
                if(error>.25f||angle>5){RejectFixedMarker(id,"LOOP_MISMATCH",centre,now);fixedLatchText=ScanLabel=Message="Chưa kiểm tra được "+id+" · lệch "+error.ToString("F2")+" m / "+angle.ToString("F1")+"°\nGiữ thẻ cố định rồi thử lại; bản nháp được giữ.";fixedDraft.loopChecked=false;fixedRepository.SaveDraft(fixedDraft);return;}
                known.verified=true;fixedDraft.loopChecked=true;Map.loopChecked=true;fixedDraft.loopError=error;fixedDraft.loopAngle=angle;fixedVerifiedCount++;
                fixedLatchText="Đã kiểm tra lại "+id+" · sai khác "+error.ToString("F2")+" m";
            }
            else
            {
                var access=ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position));access.y=0;
                known=new LabFixedMarker{id=id,utc=DateTime.UtcNow.ToString("O"),centre=centre,access=access,rotation=Q(rotation),samples=calibration.Samples};
                fixedDraft.markers.Add(known);Map.markers.Add(new LabMarker{id=id,centre=centre,access=access,yaw=rotation.eulerAngles.y});
                string integrity=LabSurveyIntegrity.Check(fixedDraft);
                known.captureIssue=integrity;
                fixedDraft.loopChecked=false;Map.loopChecked=false;fixingDraftReference=false;
                fixedLatchText="Đã lưu "+(integrity!=null?"nháp ":"mốc ")+id+" · "+FixedMarkerCount+"/5"+(integrity!=null?"\nVị trí cần đối chiếu map · có thể ghi tiếp thẻ khác.":"");
            }
            fixedRepository.SaveDraft(fixedDraft);lastSurveyMarker=id;lastSurveyAccept=now;fixedLatchUntil=now+1.3;fixedLatchProgress=1;fixedScanStage="SAVED";fixedRejectId=fixedRejectReason="";scanAttemptAt=now;
            if(!string.IsNullOrEmpty(known.captureIssue)){fixedScanStage="SAVED_NEEDS_REVIEW";fixedRejectId=id;fixedRejectReason=known.captureIssue;fixedRejectedPosition=centre;}
            SuspendMarkerPose();
            calibration.Reset();setupGateMarker=null;decodedMarker=null;ScanProgress=1;ScanLabel=fixedLatchText;Message=fixedLatchText+". Đi theo lối thật tới thẻ tiếp theo.";
        }
        public bool PublishFixedPlacement(LabMapPlacement placement)
        {
            if(fixedDraft==null){Message="Ghi mốc bằng camera trước.";return false;}
            var previousPlacement=fixedDraft.survey.placement;fixedDraft.survey.placement=placement;
            if(!TrySaveFixedDraft()){fixedDraft.survey.placement=previousPlacement;return false;}
            var fit=LabRegistration.Solve(fixedDraft.survey,Floor,placement);
            if(!fit.valid){Message=fit.reason;return false;}
            if(Math.Abs(fit.scaleRatio-1)>.03f||fit.maxError>.225f){Message="Khoảng cách mốc chưa khớp map. Kiểm tra chỗ chạm và tỷ lệ thẻ; không kéo giãn map để làm khớp.";return false;}
            foreach(var p in placement.pins.Where(p=>p.approximate))
            {
                var measured=fixedDraft.markers.FirstOrDefault(m=>m.id==p.id);
                if(measured!=null&&LabPaths.Distance(fit.Transform(measured.centre),p.position)>.75f)
                {Message="Mốc "+p.id+" đo mới chưa khớp chỗ bạn đánh dấu trên map. Kiểm tra điểm đặt thẻ hoặc chỉnh vị trí "+p.id+" trên map; chưa bật chỉ đường.";return false;}
            }
            if(!fixedDraft.loopChecked){Message="Đã lưu các điểm trên map. Tiếp tục ghi, quay lại một mốc để đối chiếu trước khi dùng chỉ đường.";return false;}
            var candidate=JsonUtility.FromJson<LabFixedMap>(JsonUtility.ToJson(fixedDraft));
            candidate.survey.coordinateSystem=Floor.coordinateSystem;candidate.survey.purpose="Fixed A-E on authored floor; physical acceptance pending";
            var yaw=Quaternion.Euler(0,fit.yaw,0);candidate.survey.placement=new LabMapPlacement{mapId=Floor.id,revision=Floor.revision,geometrySha256=Floor.geometrySha256};
            var layoutText=Resources.Load<TextAsset>("LabTest/LabConfirmedDestinations");
            var layout=layoutText!=null?JsonUtility.FromJson<LabApproachLayout>(layoutText.text):null;
            foreach(var m in candidate.markers)
            {
                m.centre=fit.Transform(m.centre);m.access=fit.Transform(m.access);m.access.y=0;m.rotation=Q(yaw*Q(m.rotation));
                m.observedAccess=new LabPoint(m.access.x,0,m.access.z);
                if(!LabFixedApproach.TryChoose(Floor,m.centre,m.access,layout,m.id,out var approach,out var source))
                {Message="Mốc "+m.id+" chưa có chỗ đứng trên lối đi. Kiểm tra điểm trên map; dữ liệu quét vẫn được giữ.";return false;}
                m.access=approach;m.accessSource=source;
                var s=candidate.survey.markers.First(v=>v.id==m.id);s.centre=m.centre;s.access=m.access;s.yaw=Q(m.rotation).eulerAngles.y;
                candidate.survey.placement.pins.Add(new LabMapPin{id=m.id,position=new LabPoint(m.centre.x,0,m.centre.z)});
            }
            string error=candidate.Validate(Floor,true);
            if(error!=null){Message=error=="ACCESS_NOT_WALKABLE"?"Chỗ đứng khi quét chưa khớp lối đi. Kiểm tra vị trí thẻ trên map; chưa bật chỉ đường.":"Chưa đủ dữ liệu để dùng: "+error;return false;}
            try
            {
                fixedRepository.Commit(candidate,fixedActive?.generation??0);fixedActive=candidate;fixedSurvey=false;StopExercise();ApplyFixedMap();
                Message="Đã lưu bộ mốc "+FixedMarkerCount+"/5. Có thể bắt đầu ở mốc đã lưu bất kỳ; cần đi thử để xác nhận sai số thực tế.";return true;
            }
            catch(Exception e){Message="Chưa công bố được: "+e.GetType().Name+". Bản đang dùng và bản nháp vẫn còn.";return false;}
        }
        public void StartFixedDestination(string target)
        {
            if(!ready||!CanChooseFixed(target)){Message="Mốc "+target+" chưa được ghi và căn trên map. Mở Thiết lập mốc để bổ sung.";return;}
            if(fixedSurvey)LeaveFixedSurvey();
            if(NavigationPoseValid){ChangeDestination(target);return;}
            StopExercise();ApplyFixedMap();Exercise=new LabExercise(target);Locating=false;sessionMode=TrialNavigation?"TRIAL_APPROXIMATE_OWNER_LAYOUT":"FIXED_MARKERS_SCHEMA2";floorVisual.Begin(true);BeginNavigationJournal();
            Message=(TrialNavigation?"Chỉ đường thử · vị trí xấp xỉ. ":"")+"Tới "+target+": quét một thẻ đã lưu gần bạn (A–E). Không cần quay về A.";
        }
        public void LocateFixed()
        {
            if(!FixedReady){Message="Ghi và căn các mốc trước khi xem vị trí.";return;}
            if(NavigationPoseValid){Locating=true;return;}StopExercise();ApplyFixedMap();Locating=true;
            Message="Quét một thẻ đã lưu gần bạn. App sẽ giữ tọa độ khi bạn di chuyển.";
        }
        public void BlockCurrentAisle()
        {
            if(!NavigationPoseValid||activeRoute==null)return;
            string edge=null;float best=.8f;
            foreach(var candidate in corridorGraph.edges.Where(e=>activeRoute.edges.Contains(e.id)))for(int i=1;i<candidate.points.Length;i++)
            {
                var a=candidate.points[i-1];var b=candidate.points[i];var p=Position;float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz,t=n>1e-8f?Mathf.Clamp01(((p.x-a.x)*dx+(p.z-a.z)*dz)/n):0;
                float error=LabPaths.Distance(p,new LabPoint(a.x+t*dx,0,a.z+t*dz));if(error<best){best=error;edge=candidate.id;}
            }
            if(edge==null){Message="Chưa xác định được đoạn lối hiện tại. Chọn tuyến khác trên map.";return;}
            manualBlocks.Add(edge);ApplyAisleBlocks();needsRoutePlan=true;nextPlan=0;
            Message="Đã tránh đoạn lối vừa chọn trong lượt đi này. Đang tìm đường khác.";
        }
        public void ClearBlockedAisles(){manualBlocks.Clear();ApplyAisleBlocks();needsRoutePlan=true;nextPlan=0;Message="Đã bỏ lối bạn đánh dấu. Camera vẫn tránh vật cản còn được quan sát.";}
        void ExportFixedMaps()
        {
            ExportSavedMarkerCoordinates();
            ExportPositionEntry();
            if(TrialNavigation)File.WriteAllText(Path.Combine(Folder,"trial-map.json"),JsonUtility.ToJson(fixedTrial,true));
            if(fixedActive!=null)File.WriteAllText(Path.Combine(Folder,"fixed-map-active.json"),JsonUtility.ToJson(fixedActive,true));
            if(fixedDraft!=null)File.WriteAllText(Path.Combine(Folder,"fixed-map-draft.json"),JsonUtility.ToJson(fixedDraft,true));
            var measured=fixedSurvey?fixedDraft:fixedActive??fixedDraft;
            if(measured!=null)File.WriteAllText(Path.Combine(Folder,"distance-review.json"),JsonUtility.ToJson(LabSurveyDistances.Create(measured,Floor),true));
        }
    }
}
