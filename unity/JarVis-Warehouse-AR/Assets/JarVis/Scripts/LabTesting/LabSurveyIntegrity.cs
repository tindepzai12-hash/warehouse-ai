using System;
using System.Linq;

namespace JarVis.Warehouse.Lab
{
    public static class LabSurveyIntegrity
    {
        public static string Check(LabFixedMap map)
        {
            if(map?.survey==null||map.markers==null)return "MISSING_SURVEY";
            for(int i=0;i<map.markers.Count;i++)for(int j=i+1;j<map.markers.Count;j++)
            {
                var a=map.markers[i];var b=map.markers[j];
                if(!LabPaths.Valid(a?.centre)||!LabPaths.Valid(b?.centre))return "INVALID_MARKER";
                double dx=a.centre.x-b.centre.x,dy=a.centre.y-b.centre.y,dz=a.centre.z-b.centre.z;
                if(dx*dx+dy*dy+dz*dz<.15*.15)return "COINCIDENT_"+a.id+"_"+b.id;
                var pa=map.survey.placement?.pins?.FirstOrDefault(p=>p.id==a.id);
                var pb=map.survey.placement?.pins?.FirstOrDefault(p=>p.id==b.id);
                if(pa!=null&&pb!=null&&!pa.approximate&&!pb.approximate&&LabPaths.Valid(pa.position)&&LabPaths.Valid(pb.position))
                {
                    float expected=LabPaths.Distance(pa.position,pb.position),measured=LabPaths.Distance(a.centre,b.centre);
                    if(expected>=2&&Math.Abs(measured-expected)>.35f+expected*.08f)return "CONTROL_DISTANCE_"+a.id+"_"+b.id;
                }
            }
            return null;
        }
    }
    // Continuity is checked in the draft map frame, so provider corrections moving
    // camera and anchor together do not trigger a false jump. A real jump is not repaired by offsetting it.
    public sealed class LabSurveyContinuity
    {
        LabPoint previous;double at;long frame=-1;
        public string Reason {get;private set;}="NO_BASELINE";
        public void Reset(){previous=null;frame=-1;Reason="NO_BASELINE";}
        public string Observe(double now,long sourceFrame,LabPoint point,bool healthy)
        {
            if(!healthy||!LabPaths.Valid(point)){bool had=previous!=null;Reset();Reason=had?"SURVEY_TRACKING_LOST":"NO_BASELINE";return had?Reason:null;}
            if(sourceFrame==frame)return null;
            string error=null;
            if(previous!=null)
            {
                double dt=now-at;
                if(dt<=0||sourceFrame<frame)error="SURVEY_CLOCK_CHANGED";
                else if(dt>.8)error="SURVEY_FRAME_GAP";
                else if(LabPaths.Distance(previous,point)>.30+3*dt||Math.Abs(previous.y-point.y)>.30+3*dt)error="SURVEY_POSE_JUMP";
            }
            previous=new LabPoint(point.x,point.y,point.z);at=now;frame=sourceFrame;Reason=error??"CONTINUOUS";
            return error;
        }
    }
    // A short outage freezes capture. Recovery must keep the same anchor, have
    // plausible motion, and supply several fresh healthy frames before capture resumes.
    public sealed class LabSurveyRecovery
    {
        readonly LabSurveyContinuity continuity=new LabSurveyContinuity();
        LabPoint lastPoint;double lastAt,lossAt=-1,stableAt=-1;long lastFrame=-1;string anchor;
        int stableFrames;
        public bool Holding {get;private set;}
        public string Reason {get;private set;}="NO_BASELINE";
        public void Reset(){continuity.Reset();lastPoint=null;lossAt=stableAt=-1;lastFrame=-1;stableFrames=0;anchor=null;Holding=false;Reason="NO_BASELINE";}
        public string Observe(double now,long frame,LabPoint point,bool healthy,string key)
        {
            if(lastPoint==null)
            {
                Holding=!healthy;
                if(!healthy||!LabPaths.Valid(point)){Reason="WAITING_TRACKING";return null;}
                anchor=key;lastPoint=point;lastAt=now;lastFrame=frame;Reason="CONTINUOUS";
                return continuity.Observe(now,frame,point,true);
            }
            if(now<lastAt||frame<lastFrame)return Reason="SURVEY_CLOCK_CHANGED";
            if(key!=anchor)return Reason="SURVEY_ANCHOR_CHANGED";
            if(!healthy||!LabPaths.Valid(point))
            {
                if(lossAt<0)lossAt=now;stableAt=-1;stableFrames=0;Holding=true;Reason="WAITING_TRACKING";
                return now-lossAt>=2?Reason="SURVEY_TRACKING_LOST":null;
            }
            if(lossAt>=0)
            {
                Holding=true;
                if(now-lossAt>=2)return Reason="SURVEY_RECOVERY_TIMEOUT";
                if(stableAt<0)
                {
                    double gap=now-lastAt;
                    if(LabPaths.Distance(lastPoint,point)>.35+1.8*gap||Math.Abs(lastPoint.y-point.y)>.35+.8*gap)return Reason="SURVEY_POSE_JUMP";
                    stableAt=now;continuity.Reset();stableFrames=0;
                }
                string fault=continuity.Observe(now,frame,point,true);if(fault!=null)return Reason=fault;
                if(frame!=lastFrame)stableFrames++;
                lastPoint=point;lastAt=now;lastFrame=frame;Reason="CHECKING_RECOVERY";
                if(now-stableAt<.6||stableFrames<3)return null;
                lossAt=stableAt=-1;Holding=false;Reason="RECOVERED";return null;
            }
            string error=continuity.Observe(now,frame,point,true);
            if(error!=null)return Reason=error;
            lastPoint=point;lastAt=now;lastFrame=frame;Holding=false;Reason="CONTINUOUS";return null;
        }
    }
    [Serializable] public sealed class LabApproachDestination {public string id;public LabPoint position,access;}
    [Serializable] public sealed class LabDistancePair
    {
        public string pair;public bool measured,referenceAvailable,referenceApproximate;
        public float horizontalMetres,spatialMetres,referenceHorizontalMetres,differenceMetres,differencePercent;
    }
    [Serializable] public sealed class LabDistanceReview
    {
        public string mapId,floorHash,sourceGeometrySha256;
        public string note="Straight-line horizontal and spatial distances, not walking-route length. QR scale assumes inner square 10 cm. Unmeasured pairs are explicitly marked measured=false. Floor reference combines GLB and owner-arranged 2D sketch; not an independent tape measurement.";
        public LabDistancePair[] pairs;
    }
    public static class LabSurveyDistances
    {
        public static LabDistanceReview Create(LabFixedMap map,LabFloorPlan floor)
        {
            var result=new LabDistanceReview{mapId=map.mapId,floorHash=map.floorHash,sourceGeometrySha256=floor.sourceGeometrySha256};
            var pairs=new System.Collections.Generic.List<LabDistancePair>();
            for(char a='A';a<='E';a++)for(char b=(char)(a+1);b<='E';b++)
            {
                var pair=new LabDistancePair{pair=""+a+b};
                var ma=map.markers.FirstOrDefault(m=>m.id==a.ToString());var mb=map.markers.FirstOrDefault(m=>m.id==b.ToString());
                var pa=map.survey.placement?.pins?.FirstOrDefault(p=>p.id==a.ToString());var pb=map.survey.placement?.pins?.FirstOrDefault(p=>p.id==b.ToString());
                pair.referenceAvailable=pa!=null&&pb!=null&&LabPaths.Valid(pa.position)&&LabPaths.Valid(pb.position);
                if(pair.referenceAvailable){pair.referenceHorizontalMetres=LabPaths.Distance(pa.position,pb.position);pair.referenceApproximate=pa.approximate||pb.approximate;}
                pair.measured=ma!=null&&mb!=null&&LabPaths.Valid(ma.centre)&&LabPaths.Valid(mb.centre);
                if(pair.measured)
                {
                    pair.horizontalMetres=LabPaths.Distance(ma.centre,mb.centre);double dy=ma.centre.y-mb.centre.y;
                    pair.spatialMetres=(float)Math.Sqrt(pair.horizontalMetres*pair.horizontalMetres+dy*dy);
                    if(pair.referenceAvailable){pair.differenceMetres=pair.horizontalMetres-pair.referenceHorizontalMetres;pair.differencePercent=pair.referenceHorizontalMetres>.0001f?100*pair.differenceMetres/pair.referenceHorizontalMetres:0;}
                }
                pairs.Add(pair);
            }
            result.pairs=pairs.ToArray();return result;
        }
    }
    [Serializable] public sealed class LabApproachLayout
    {public string mapId,geometrySha256;public int revision;public LabApproachDestination[] destinations;}
    public static class LabFixedApproach
    {
        public static bool TryChoose(LabFloorPlan floor,LabPoint centre,LabPoint observed,LabApproachLayout layout,string id,out LabPoint access,out string source)
        {
            access=null;source="ACCESS_NOT_WALKABLE";var router=new LabFloorRouter(floor);
            if(!LabPaths.Valid(centre)||!LabPaths.Valid(observed))return false;
            if(router.Walkable(observed)&&LabPaths.Distance(centre,observed)<=1.6f){access=observed;source="OBSERVED_CAMERA_PROJECTION";return true;}
            if(layout?.mapId!=floor.id||layout.geometrySha256!=floor.geometrySha256||layout.revision!=floor.revision)return false;
            var authored=layout.destinations?.FirstOrDefault(p=>p.id==id);
            if(authored==null||!LabPaths.Valid(authored.position)||!LabPaths.Valid(authored.access)||LabPaths.Distance(centre,authored.position)>.75f||LabPaths.Distance(centre,authored.access)>1.6f||!router.Walkable(authored.access))return false;
            // Explicit owner-layout approach, never an arbitrary nearest point through a wall.
            access=new LabPoint(authored.access.x,0,authored.access.z);source="CONFIRMED_LAYOUT_APPROACH";return true;
        }
    }
}
