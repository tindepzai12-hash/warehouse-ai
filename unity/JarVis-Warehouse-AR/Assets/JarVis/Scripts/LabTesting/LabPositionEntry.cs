using System;
using System.Globalization;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    // Owner-supplied approximate coordinates are an input for later review, not localization evidence.
    public static class LabPositionEntry
    {
        public const string Source="USER_ESTIMATED_MAP_ENTRY";
        public const string Purpose="POSITION_INPUT_FOR_REVIEW_NOT_REGISTERED";
        public const string ScanFile="Scaniverse 2026-10-28 121814.glb";
        public static bool Number(string text,out float value)
        {
            value=0;
            return text!=null&&float.TryParse(text.Trim().Replace(',','.').Replace('−','-'),NumberStyles.Float,CultureInfo.InvariantCulture,out value)&&!float.IsNaN(value)&&!float.IsInfinity(value);
        }
        public static bool TrySet(LabMapPlacement placement,LabFloorPlan floor,string id,string x,string z,string heightCm,string errorCm,out string reason)
        {
            reason=null;
            if(placement==null||floor?.Valid!=true||!LabPrintedCard.Id(id)||placement.pins==null||placement.mapId!=floor.id||placement.revision!=floor.revision||placement.geometrySha256!=floor.geometrySha256)
            {reason="Vị trí không thuộc bản đồ đang mở.";return false;}
            if(!Number(x,out float px)||!Number(z,out float pz)){reason="Nhập X và Z bằng mét; chấp nhận dấu phẩy hoặc dấu chấm.";return false;}
            if(!floor.Contains(new LabPoint(px,0,pz))){reason="Điểm nằm ngoài map. Chạm lại đúng khu vực trên bản đồ.";return false;}
            if(!Number(errorCm,out float tolerance)||tolerance<5||tolerance>200){reason="Sai số dự kiến từ 5 đến 200 cm; có thể dùng 50 cm để thử.";return false;}
            bool entered=!string.IsNullOrWhiteSpace(heightCm);float h=0;
            if(entered&&(!Number(heightCm,out h)||h<0||h>300)){reason="Chiều cao tâm toàn thẻ: 0–300 cm từ sàn; chưa biết thì để trống.";return false;}
            var pin=placement.pins.FirstOrDefault(p=>p.id==id);
            if(pin==null){pin=new LabMapPin{id=id};placement.pins.Add(pin);}
            pin.position=new LabPoint(px,entered?h*.01f:(pin.position?.y??0),pz);
            pin.approximate=true;pin.entrySource=Source;pin.expectedErrorMetres=tolerance*.01f;pin.heightEntered=entered;
            placement.entryPurpose=Purpose;placement.sourceScanFile=ScanFile;
            return true;
        }
        public static string Validate(LabMapPlacement placement,LabFloorPlan floor)
        {
            if(placement==null||floor?.Valid!=true||placement.mapId!=floor.id||placement.revision!=floor.revision||placement.geometrySha256!=floor.geometrySha256)return "MAP_REVISION_MISMATCH";
            if(placement.pins==null||placement.pins.Count>5||placement.pins.Any(p=>p==null||!LabPrintedCard.Id(p.id)||!floor.Contains(p.position))||placement.pins.Select(p=>p.id).Distinct().Count()!=placement.pins.Count)return "INVALID_PINS";
            foreach(var pin in placement.pins.Where(p=>p.entrySource==Source))
                if(!pin.approximate||!Number(pin.expectedErrorMetres.ToString("R",CultureInfo.InvariantCulture),out float e)||e<.04999f||e>2.00001f||pin.heightEntered&&(pin.position.y<0||pin.position.y>3.00001f))return "INVALID_ENTRY";
            return null;
        }
    }
}
