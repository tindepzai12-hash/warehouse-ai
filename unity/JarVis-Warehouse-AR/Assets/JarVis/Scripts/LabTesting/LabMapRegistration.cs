using System;
using System.Collections.Generic;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabMapPin
    {
        public string id;
        public LabPoint position;
        public bool approximate;
        // Optional user entry. Never overwrites the camera observation or certifies accuracy.
        public string entrySource;
        public float expectedErrorMetres;
        public bool heightEntered;
    }
    [Serializable] public sealed class LabMapPlacement
    {
        public string mapId,geometrySha256;
        public int revision;
        public string entryPurpose,sourceScanFile;
        public List<LabMapPin> pins=new List<LabMapPin>();
    }
    [Serializable] public sealed class LabFloorLabel { public string id;public float x,z; }
    [Serializable] public sealed class LabFloorPlan
    {
        public string id,geometrySha256,source,coordinateSystem,sourceGeometrySha256;
        public int revision,width,height;
        public bool displayMirrorX;
        public float resolution,originX,originZ,displayYaw,requiredClearance;
        public int[] cells;
        public float[] clearance;
        public LabFloorLabel[] labels;
        public bool Contains(LabPoint p)=>LabPaths.Valid(p)&&p.x>=originX&&p.z>=originZ&&p.x<=originX+width*resolution&&p.z<=originZ+height*resolution;
        public bool Valid=>width>0&&height>0&&resolution>0&&cells!=null&&cells.Length==width*height;
    }
    public sealed class LabMapFit
    {
        public bool valid;
        public string reason;
        public int count;
        public float yaw,tx,tz,maxError,scaleRatio;
        // A gravity-aligned, metric rigid transform. Never silently stretch a measured room.
        public LabPoint Transform(LabPoint p)
        {
            double a=yaw*Math.PI/180,c=Math.Cos(a),s=Math.Sin(a);
            return new LabPoint((float)(c*p.x+s*p.z)+tx,p.y,(float)(-s*p.x+c*p.z)+tz);
        }
    }
    public static class LabRegistration
    {
        public static LabMapFit Solve(LabSurvey survey,LabFloorPlan floor,LabMapPlacement placement=null)
        {
            var fit=new LabMapFit{reason="Chưa gắn mốc vào bản đồ."};
            placement=placement??survey?.placement;
            if(survey==null||floor==null||!floor.Valid||placement==null)return fit;
            if(!LabPaths.ValidMap(survey)){fit.reason="Dữ liệu mốc đã ghi không hợp lệ.";return fit;}
            if(placement.mapId!=floor.id||placement.revision!=floor.revision||placement.geometrySha256!=floor.geometrySha256)
            {fit.reason="Mốc thuộc phiên bản bản đồ khác. Cần gắn lại.";return fit;}
            if(placement.pins==null||placement.pins.Count>5||placement.pins.Any(p=>p==null||p.id==null||p.id.Length!=1||p.id[0]<'A'||p.id[0]>'E'||!floor.Contains(p.position))||placement.pins.Select(p=>p.id).Distinct().Count()!=placement.pins.Count)
            {fit.reason="Vị trí mốc không hợp lệ.";return fit;}
            var pairs=placement.pins.Where(p=>!p.approximate).Select(p=>new {pin=p,marker=survey.markers.FirstOrDefault(m=>m.id==p.id)}).Where(p=>p.marker!=null).ToArray();
            fit.count=pairs.Length;
            if(pairs.Length<2){fit.reason="Cần ít nhất 2 mốc đã ghi bằng camera và đã đặt trên bản đồ.";return fit;}
            float baseline=0;
            foreach(var a in pairs)foreach(var b in pairs)baseline=Math.Max(baseline,LabPaths.Distance(a.marker.centre,b.marker.centre));
            if(baseline<2){fit.reason="Hai mốc quá gần. Chọn thêm mốc cách nhau ít nhất 2 m.";return fit;}
            double sx=pairs.Average(p=>(double)p.marker.centre.x),sz=pairs.Average(p=>(double)p.marker.centre.z);
            double dx=pairs.Average(p=>(double)p.pin.position.x),dz=pairs.Average(p=>(double)p.pin.position.z);
            double dot=0,cross=0,sourcePower=0,destPower=0;
            foreach(var pair in pairs)
            {
                double x=pair.marker.centre.x-sx,z=pair.marker.centre.z-sz,u=pair.pin.position.x-dx,v=pair.pin.position.z-dz;
                dot+=x*u+z*v;cross+=z*u-x*v;sourcePower+=x*x+z*z;destPower+=u*u+v*v;
            }
            fit.scaleRatio=(float)Math.Sqrt(destPower/Math.Max(1e-12,sourcePower));
            if(Math.Abs(fit.scaleRatio-1)>.15){fit.reason="Khoảng cách mốc không khớp bản đồ (>15%). Kiểm tra chỗ đặt mốc; QR vuông 10 cm, không đo cả tờ A4.";return fit;}
            fit.yaw=(float)(Math.Atan2(cross,dot)*180/Math.PI);
            var centre=fit.Transform(new LabPoint((float)sx,0,(float)sz));fit.tx=(float)dx-centre.x;fit.tz=(float)dz-centre.z;
            foreach(var pair in pairs)fit.maxError=Math.Max(fit.maxError,LabPaths.Distance(fit.Transform(pair.marker.centre),pair.pin.position));
            if(fit.maxError>.45f){fit.reason="Các mốc chưa khớp: lệch lớn nhất "+fit.maxError.ToString("F2")+" m. Kiểm tra mốc đã chạm trên bản đồ.";return fit;}
            fit.valid=true;fit.reason="Đã căn "+fit.count+" mốc · sai khác lớn nhất "+fit.maxError.ToString("F2")+" m.";return fit;
        }
    }
}
