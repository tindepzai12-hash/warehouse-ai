using System;
using System.IO;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        public bool PositionDraftAvailable=>fixedDraft?.markers.Count>0;
        public void OpenPositionEntry()
        {
            if(!PositionDraftAvailable){Message="Quét và ghi ít nhất một mốc A–E trước.";return;}
            if(addingAnchor)return;
            StopExercise();fixedSurvey=false;RemoveAnchor();navigationTrusted=false;SetMarkerScanning(false);
            Map=fixedDraft.survey;fitCache=null;profileReady=false;
            ui.OpenMap(true);
        }
        public bool SavePositionEntryDraft(LabMapPlacement placement)
        {
            if(fixedDraft==null){Message="Quét và ghi ít nhất một mốc trước; vị trí nhập không thay cho quan sát camera.";return false;}
            string error=LabPositionEntry.Validate(placement,Floor);
            if(error!=null){Message="Chưa lưu vị trí nhập: "+error;return false;}
            var previous=fixedDraft.survey.placement;
            try
            {
                // Serialize a private copy so subsequent UI edits cannot mutate a saved draft.
                var copy=JsonUtility.FromJson<LabMapPlacement>(JsonUtility.ToJson(placement));
                copy.entryPurpose=LabPositionEntry.Purpose;copy.sourceScanFile=LabPositionEntry.ScanFile;
                fixedDraft.survey.placement=copy;fixedRepository.SaveDraft(fixedDraft);
                Message="Đã lưu vị trí nháp · "+fixedDraft.markers.Count+" mốc camera. Vào Map và dữ liệu → Xuất ZIP kiểm tra / bản nháp. Bộ chỉ đường đang dùng được giữ riêng.";
                return true;
            }
            catch(Exception e)
            {fixedDraft.survey.placement=previous;Message="Chưa lưu được vị trí: "+e.GetType().Name+". Bản trước vẫn còn.";return false;}
        }
        void ExportPositionEntry()
        {
            if(fixedDraft?.survey?.placement==null)return;
            File.WriteAllText(Path.Combine(Folder,"position-entry.json"),JsonUtility.ToJson(fixedDraft.survey.placement,true));
            File.WriteAllText(Path.Combine(Folder,"POSITION-ENTRY-README.txt"),
                "JarVis "+Version+" — position input for review\n"+
                "Map: "+Floor.id+" revision "+Floor.revision+" hash "+Floor.geometrySha256+"\n"+
                "Geometry source: "+Floor.sourceGeometrySha256+"\n"+
                "Scaniverse 2026-10-28 121814.glb + owner-confirmed desk arrangement.\n"+
                "position-entry.json contains entered map coordinates and optional height; fixed-map-draft.json retains original camera observations.\n"+
                "expectedErrorMetres is the owner's requested tolerance, not measured accuracy. Unedited reference pins are not new measurements.\n"+
                "Coordinate X/Z is the existing map frame; heightEntered Y is full reference-image centre above floor.\n"+
                "QR edge 0.100 m; reference image width "+LabPrintedCard.ImageWidthMetres.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" m. A4 sheet width is not the reference width.\n"+
                "No new position input is automatically published as a verified navigation map.\n");
        }
    }
}
