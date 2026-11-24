using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    public sealed class LabSavedMarkerRow
    {
        public string id,text;
        public bool captured,mapEntered;
        public LabPoint observed,map;
        static string N(float value)=>value.ToString("F2",CultureInfo.InvariantCulture);
        public static List<LabSavedMarkerRow> Create(LabFixedMap source)
        {
            var rows=new List<LabSavedMarkerRow>();
            foreach(string id in new[]{"A","B","C","D","E"})
            {
                var row=new LabSavedMarkerRow{id=id};rows.Add(row);
                var marker=source?.markers?.FirstOrDefault(m=>m.id==id);
                if(marker==null){row.text=id+" · Chưa lưu mốc camera";continue;}
                row.captured=true;row.observed=new LabPoint(marker.centre.x,marker.centre.y,marker.centre.z);
                row.text=id+" · Đã lưu mốc camera\nAR tạm: X "+N(marker.centre.x)+" · Z "+N(marker.centre.z)+" m";
                var pin=source.survey?.placement?.pins?.FirstOrDefault(p=>p.id==id);
                if(pin?.position!=null&&pin.entrySource==LabPositionEntry.Source)
                {
                    row.mapEntered=true;row.map=new LabPoint(pin.position.x,pin.position.y,pin.position.z);
                    row.text+="\nMap đã nhập: X "+N(pin.position.x)+" · Z "+N(pin.position.z)+" m";
                    row.text+="\nSai số nhập ±"+N(pin.expectedErrorMetres*100)+" cm · "+
                        (pin.heightEntered?"cao tâm thẻ "+N(pin.position.y*100)+" cm":"chưa nhập cao tâm thẻ");
                }
                else row.text+="\nChưa nhập vị trí trên map; pin tham chiếu không phải số đo mới.";
                if(!string.IsNullOrEmpty(marker.captureIssue))row.text+="\nCần đối chiếu vị trí · vẫn giữ bản nháp.";
            }
            return rows;
        }
    }
}
