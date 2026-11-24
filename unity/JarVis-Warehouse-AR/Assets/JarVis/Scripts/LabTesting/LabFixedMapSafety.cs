using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        readonly LabSurveyRecovery surveyRecovery=new LabSurveyRecovery();
        float fixedLatchProgress=1;
        string fixedScanStage="SEARCHING",fixedRejectId="",fixedRejectReason="";
        LabPoint fixedRejectedPosition;
        void ResetSurveyContinuity(){surveyRecovery.Reset();}
        void RejectFixedMarker(string id,string reason,LabPoint candidate,double now)
        {
            fixedRejectId=id;fixedRejectReason=reason;fixedRejectedPosition=candidate;
            fixedScanStage="REJECTED";calibration.Reset();ScanProgress=0;fixedLatchProgress=0;
            string why=reason.StartsWith("CONTROL_DISTANCE",StringComparison.Ordinal)?"khoảng cách tới mốc đã lưu chưa khớp map":reason.StartsWith("COINCIDENT",StringComparison.Ordinal)?"vị trí bị trùng mốc khác":"tọa độ chưa đạt kiểm tra";
            fixedLatchText="Chưa lưu "+id+" · "+why+"\nGiữ yên để thử lại; nếu còn lệch, quét mốc đã lưu gần bạn.";
            ScanLabel=Message=fixedLatchText;lastSurveyAccept=now;fixedLatchUntil=now+2;
            // Rejected IDs must not enter lastSurveyMarker, which tracks successful captures.
        }
        public bool FixedDraftNeedsRepair=>fixedDraft!=null&&LabSurveyIntegrity.Check(fixedDraft)!=null;
        public string FixedDraftIssue=>LabSurveyIntegrity.Check(fixedDraft);
        public void RestartFixedSurvey()
        {
            if(!ready||addingAnchor)return;
            try
            {
                if(fixedDraft!=null)
                {
                    string archive=Path.Combine(fixedRepository.DirectoryPath,"draft-history");Directory.CreateDirectory(archive);
                    string name=DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N")+".json";
                    File.WriteAllText(Path.Combine(archive,name),JsonUtility.ToJson(fixedDraft,true));
                }
                BeginFixedSurvey(true);
            }
            catch(Exception){Message="Chưa sao lưu được bản nháp cũ. Chưa thay dữ liệu; kiểm tra dung lượng rồi thử lại.";}
        }
        LabMapPlacement ConfirmedPlacement()
        {
            var supplied=Resources.Load<TextAsset>("LabTest/LabConfirmedDestinations");
            var suppliedPlacement=supplied!=null?JsonUtility.FromJson<LabMapPlacement>(supplied.text):null;
            if(suppliedPlacement?.mapId==Floor.id&&suppliedPlacement.geometrySha256==Floor.geometrySha256&&suppliedPlacement.revision==Floor.revision&&suppliedPlacement.pins?.Count==5)
                return suppliedPlacement;
            var text=Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew");
            var preset=text!=null?JsonUtility.FromJson<LabSurvey>(text.text):null;
            if(preset?.placement==null||preset.placement.mapId!=Floor.id||preset.placement.geometrySha256!=Floor.geometrySha256||preset.placement.revision!=Floor.revision)
                return new LabMapPlacement{mapId=Floor.id,revision=Floor.revision,geometrySha256=Floor.geometrySha256};
            // Owner reconfirmed A/B/C unchanged on 02 Nov. These are control pins,
            // not fabricated measurements or automatically verified AR rotations.
            var placement=JsonUtility.FromJson<LabMapPlacement>(JsonUtility.ToJson(preset.placement));
            placement.pins=placement.pins.Where(p=>p.id=="A"||p.id=="B"||p.id=="C").ToList();return placement;
        }
        bool CheckFixedContinuity(double now)
        {
            if(anchor==null||MapFrame==null){ResetSurveyContinuity();return true;}
            bool healthy=Tracking&&anchor.trackingState==TrackingState.Tracking;
            var local=ToPoint(MapFrame.InverseTransformPoint(CameraOwner.arCamera.transform.position));
            string error=surveyRecovery.Observe(now,cameraSampleId,local,healthy,AnchorKey);
            if(error==null)
            {
                if(!surveyRecovery.Holding)return true;
                calibration.Reset();ScanProgress=0;fixedScanStage="RECOVERING";
                ScanLabel=Message="Tracking tạm gián đoạn · đang giữ các mốc\nDừng một lát, nhìn vùng vừa đi. Chưa lưu thêm mốc.";
                return false;
            }
            InterruptFixedSurvey(error);return false;
        }
        void InterruptFixedSurvey(string reason)
        {
            if(!fixedSurvey||fixedDraft==null)return;
            fixedDraft.loopChecked=false;Map.loopChecked=false;Map.interrupted=true;
            epoch++;calibration.Reset();ScanProgress=0;decodedMarker=null;decodedObservation=null;decodedMarkerCount=0;visible.Clear();lastSurveyMarker=null;fixedLatchUntil=0;
            RemoveAnchor();navigationTrusted=false;ResetSurveyContinuity();SuspendMarkerPose();fixedScanStage="NEED_SAVED_MARKER";
            fixedLatchText="";Message="Tọa độ vừa gián đoạn. Mốc cũ được giữ. Quét một mốc đã ghi gần bạn để nối lại; chưa lưu thêm mốc.";
            ScanLabel=Message;routeError=reason;TrySaveFixedDraft();
        }
    }
}
