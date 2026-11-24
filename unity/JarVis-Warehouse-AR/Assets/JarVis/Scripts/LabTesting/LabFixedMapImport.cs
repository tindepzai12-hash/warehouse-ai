using System;
using System.IO;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabFixedMap importCandidate;bool pickingMap;
        public bool ImportReady=>importCandidate!=null;
        public string ImportSummary=>importCandidate==null?"Chọn gói map đã xuất từ JarVis Lab.":"Gói có "+importCandidate.markers.Count+" mốc, cùng phiên bản mặt bằng. Xác nhận để tạo phiên bản mới; bản trước vẫn được giữ.";
        public void ExportFixedPackage()
        {
            if(fixedActive==null){Message="Chưa có bộ mốc đã căn map. Xuất ZIP kiểm tra để lấy bản nháp.";return;}
            try
            {
                string text=JsonUtility.ToJson(fixedActive,true),manifest=JsonUtility.ToJson(new LabFixedPointer{generation=fixedActive.generation,snapshotId=fixedActive.snapshotId,sha256=LabFixedRepository.Hash(text)},true);
                Directory.CreateDirectory(Root);string path=Path.Combine(Root,"JarVis-FixedMap-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".zip");
                LabFixedArchive.Write(path,text,manifest);ExportedPath=VisualMapData.Export(path);Message="Đã xuất gói map: "+ExportedPath;
            }
            catch(Exception e){Message="Chưa xuất được gói map: "+e.GetType().Name+". Bản đang dùng vẫn còn.";}
        }
        public void PickFixedPackage()
        {
            if(pickingMap)return;importCandidate=null;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                pickingMap=true;
                using(var picker=new AndroidJavaClass("com.jarvis.warehouse.LabMapPicker"))picker.CallStatic("pick",gameObject.name,"OnFixedPackagePicked",Application.temporaryCachePath);
                Message="Chọn ZIP gói map. App sẽ kiểm tra trước khi thay bản đang dùng.";
            }
            catch(Exception){pickingMap=false;Message="Chưa mở được trình chọn file. Thử lại.";}
#else
            Message="Nhập gói map dùng trình chọn file trên Android. Kiểm tra định dạng đã có trong bộ thử máy tính.";
#endif
        }
        public void OnFixedPackagePicked(string path)
        {
            if(!pickingMap)return;pickingMap=false;
            if(string.IsNullOrEmpty(path)){Message="Đã hủy chọn file; map hiện tại giữ nguyên.";return;}
            try
            {
                string full=Path.GetFullPath(path),cache=Path.GetFullPath(Application.temporaryCachePath)+Path.DirectorySeparatorChar;
                if(!full.StartsWith(cache,StringComparison.Ordinal)||Path.GetFileName(full).StartsWith("lab-map-",StringComparison.Ordinal)!=true)throw new InvalidDataException("UNEXPECTED_FILE");
                string text,manifest;using(var file=File.OpenRead(full))LabFixedArchive.Read(file,out text,out manifest);
                var pointer=JsonUtility.FromJson<LabFixedPointer>(manifest);var candidate=JsonUtility.FromJson<LabFixedMap>(text);
                if(pointer==null||pointer.schema!=2||pointer.sha256!=LabFixedRepository.Hash(text)||candidate==null||candidate.Validate(Floor,true)!=null||pointer.snapshotId!=candidate.snapshotId||pointer.generation!=candidate.generation)throw new InvalidDataException("INVALID_MAP_PACKAGE");
                importCandidate=candidate;Message=ImportSummary;File.Delete(full);
            }
            catch(Exception e){importCandidate=null;Message="Gói map chưa hợp lệ: "+e.GetType().Name+". Bản đang dùng vẫn còn.";}
        }
        public void ConfirmFixedImport()
        {
            if(importCandidate==null||fixedSurvey)return;
            try
            {
                fixedRepository.Commit(importCandidate,fixedActive?.generation??0);StopExercise();fixedActive=importCandidate;importCandidate=null;ApplyFixedMap();
                Message="Đã nhập bộ mốc. Quét một mốc đã lưu để xác định vị trí trong phiên mới.";
            }
            catch(Exception e){Message="Chưa nhập được: "+e.GetType().Name+". Bản đang dùng vẫn còn.";}
        }
        public void CancelFixedImport(){importCandidate=null;Message="Đã hủy nhập; map hiện tại giữ nguyên.";}
    }
}
