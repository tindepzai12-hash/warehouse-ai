using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        readonly LabMarkerSaveGate markerSave=new LabMarkerSaveGate();
        bool SaveContinuity=>fixedSurvey&&!paused&&!addingAnchor&&!surveyRecovery.Holding&&
            (anchor==null||anchor.trackingState==TrackingState.Tracking&&MapFrame!=null);
        public bool CanSaveMarker=>fixedDraft!=null&&!fixedDraft.markers.Any(m=>m.id==markerSave.Id)&&markerSave.Ready(Tracking,SaveContinuity,decodedMarker,epoch,Time.realtimeSinceStartupAsDouble);
        public string SaveMarkerButton=>addingAnchor?"Đang lưu mốc…":markerSave.Id!=null?"Lưu mốc "+markerSave.Id:"Lưu mốc · chờ nhận thẻ";
        public int SavedMarkerCount=>(fixedDraft?.markers.Count>0?fixedDraft:fixedActive)?.markers.Count??0;
        public List<LabSavedMarkerRow> SavedMarkerRows=>LabSavedMarkerRow.Create(fixedDraft?.markers.Count>0?fixedDraft:fixedActive);
        void OfferMarkerSave(string id,Pose pose,double now)
        {
            markerSave.Offer(id,pose,epoch,now);fixedScanStage="READY_TO_SAVE";
            ScanProgress=1;ScanLabel="Đã nhận mốc "+id+" · sẵn sàng lưu\nBấm Lưu mốc "+id+" để giữ tọa độ trên máy.";
            Message="Mốc "+id+" chưa lưu. Giữ thẻ trong camera rồi bấm Lưu mốc "+id+".";
        }
        public void ConfirmSaveMarker()
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(!markerSave.Take(Tracking,SaveContinuity,decodedMarker,epoch,now,out string id,out Pose pose))
            {Message="Chưa có mốc ổn định để lưu. Giữ trọn thẻ trong camera rồi bấm Lưu mốc.";return;}
            if(fixedDraft?.markers.Any(m=>m.id==id)==true){Message="Mốc "+id+" đã lưu. Tọa độ cũ được giữ.";return;}
            if(anchor==null&&fixedDraft.markers.Count>0)
            {Message="Quét lại một mốc đã lưu để nối tọa độ trước.";return;}
            if(anchor==null)CaptureFixedReference(id,pose,null);
            else AcceptFixedObservation(id,pose);
        }
        public void OpenSavedMarkers()
        {
            if(ui==null)return;
            ui.OpenSavedMarkers();
        }
        void ExportSavedMarkerCoordinates()
        {
            var csv=new StringBuilder("marker_id,coordinate_frame,x_m,y_m,z_m,height_source,tolerance_m,physical_accuracy_verified\n");
            foreach(var row in SavedMarkerRows.Where(r=>r.captured))
            {
                string R(float v)=>v.ToString("R",CultureInfo.InvariantCulture);
                csv.Append(row.id).Append(",AR_DRAFT,").Append(R(row.observed.x)).Append(',').Append(R(row.observed.y)).Append(',').Append(R(row.observed.z)).Append(",ESTIMATED_REFERENCE_HEIGHT,,false\n");
                if(row.mapEntered)
                {
                    var source=fixedDraft?.markers.Count>0?fixedDraft:fixedActive;
                    var pin=source.survey.placement.pins.First(p=>p.id==row.id);
                    csv.Append(row.id).Append(',').Append(LabPositionEntry.Source).Append(',').Append(R(row.map.x)).Append(',').Append(pin.heightEntered?R(row.map.y):"").Append(',').Append(R(row.map.z)).Append(',').Append(pin.heightEntered?"USER_ENTERED":"UNKNOWN").Append(',').Append(R(pin.expectedErrorMetres)).Append(",false\n");
                }
            }
            File.WriteAllText(Path.Combine(Folder,"saved-marker-coordinates.csv"),csv.ToString());
        }
    }
}
