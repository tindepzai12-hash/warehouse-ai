using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Lab
{
    public enum LabCueIntent { Hold, Travel, LookToRoute }
    public enum LabTurnPhase { Cruise, Approach, AtTurn, Exiting }

    // Semantic state is immutable. Camera projection never writes route progress.
    public sealed class LabNavigationDecision
    {
        public readonly long id;
        public readonly int routeRevision,referenceEpoch;
        public readonly double time;
        public readonly bool usable,arrived;
        public readonly string target,title,reason,maneuverId;
        public readonly LabPoint aim;
        public readonly float remaining,turnDistance,glyphBearing;
        public readonly LabTurnPhase phase;
        public readonly LabRouteVisualContext visualRoute;
        public readonly float along,stopAt;
        public bool VisualMatches=>visualRoute!=null&&visualRoute.epoch==referenceEpoch&&visualRoute.revision==routeRevision&&visualRoute.target==target;
        public readonly LabPoint turnPoint;
        public LabNavigationDecision(long id,int revision,int epoch,double time,bool usable,bool arrived,string target,string title,string reason,
            LabPoint aim,float remaining,float turnDistance,float glyphBearing,LabTurnPhase phase,string maneuverId,
            LabRouteVisualContext visualRoute=null,float along=0,float stopAt=float.PositiveInfinity,LabPoint turnPoint=null)
        {this.id=id;routeRevision=revision;referenceEpoch=epoch;this.time=time;this.usable=usable;this.arrived=arrived;this.target=target;this.title=title;this.reason=reason;this.aim=aim;
            this.remaining=remaining;this.turnDistance=turnDistance;this.glyphBearing=glyphBearing;this.phase=phase;this.maneuverId=maneuverId;
            this.visualRoute=visualRoute;this.along=along;this.stopAt=visualRoute==null?0:Math.Min(visualRoute.Length,stopAt);
            this.turnPoint=turnPoint==null?null:new LabPoint(turnPoint.x,turnPoint.y,turnPoint.z);}
    }
    public sealed class LabPresentationSample
    {
        public long decisionId,cameraSampleId;
        public LabCueIntent intent;
        public string title,subtitle,reason;
        public float bearing,glyphBearing;
        public bool hud;public int cameraSide=1;
        public LabCueMode mode;public int visibleTokens;
    }
    public sealed class LabCameraCoach
    {
        bool looking;int side=1;
        public void Reset(){looking=false;side=1;}
        public LabCueIntent Update(bool usable,bool headingValid,float bearing)
        {
            if(!usable){looking=false;return LabCueIntent.Hold;}
            if(!headingValid){looking=true;return LabCueIntent.LookToRoute;}
            float a=Math.Abs(bearing);
            if(looking?a<42:a>65)looking=!looking;
            if(a<165&&a>8)side=bearing>=0?1:-1;
            return looking?LabCueIntent.LookToRoute:LabCueIntent.Travel;
        }
        public string Title(bool headingValid)=>!headingValid?"NHÌN VỀ LỐI ĐI":side>0?"XOAY MÁY SANG PHẢI":"XOAY MÁY SANG TRÁI";
        public int Side=>side;
    }
    // Completion requires entering the outgoing leg, not turning the camera or crossing an along threshold alone.
    public sealed class LabTurnTracker
    {
        int completed;string id;double exitingSince=-1;
        public LabTurnPhase Phase {get;private set;}
        public void Reset(){completed=0;id=null;exitingSince=-1;Phase=LabTurnPhase.Cruise;}
        public LabTurn Update(IList<LabTurn> turns,IList<LabPoint> path,LabPoint position,float along,double now)
        {
            if(turns==null||path==null||!LabPaths.Valid(position)){Phase=LabTurnPhase.Cruise;return null;}
            while(completed<turns.Count)
            {
                var turn=turns[completed];
                if(id!=turn.id){id=turn.id;exitingSince=-1;}
                float legEnd=LabPaths.Length(path);
                if(completed+1<turns.Count)legEnd=Math.Min(legEnd,turns[completed+1].along);
                float available=Math.Max(.01f,legEnd-turn.along);
                float required=Math.Min(.5f,available*.45f);
                var outgoing=LabPaths.Sample(path,turn.along+Math.Min(.3f,available));
                float dx=outgoing.x-turn.point.x,dz=outgoing.z-turn.point.z,n=(float)Math.Sqrt(dx*dx+dz*dz);
                float forward=n>.001f?((position.x-turn.point.x)*dx+(position.z-turn.point.z)*dz)/n:0;
                float lateral=n>.001f?Math.Abs((position.x-turn.point.x)*dz-(position.z-turn.point.z)*dx)/n:float.PositiveInfinity;
                bool exiting=along>turn.along+required&&forward>=required&&lateral<.4f;
                if(exiting){if(exitingSince<0)exitingSince=now;Phase=LabTurnPhase.Exiting;if(now-exitingSince>=.4){completed++;id=null;continue;}}
                else {exitingSince=-1;Phase=turn.along-along>3?LabTurnPhase.Cruise:turn.along-along>1?LabTurnPhase.Approach:LabTurnPhase.AtTurn;}
                return turn;
            }
            Phase=LabTurnPhase.Cruise;return null;
        }
    }
    public sealed class LabMovementEstimate
    {
        LabPoint origin;double at,lastStep=-1;float priorBearing;int consistent;
        public float Bearing {get;private set;}
        public float Confidence {get;private set;}
        public void Reset(){origin=null;lastStep=-1;consistent=0;Confidence=0;}
        public void Update(LabPoint p,double now)
        {
            if(!LabPaths.Valid(p)){Reset();return;}
            if(origin==null||now<at||now-at>2){origin=p;at=now;consistent=0;Confidence=0;return;}
            float distance=LabPaths.Distance(origin,p);
            if(distance<.28f){if(lastStep<0||now-lastStep>1.3)Confidence=0;return;}
            float bearing=(float)((Math.Atan2(p.x-origin.x,p.z-origin.z)*180/Math.PI+360)%360);
            float difference=Math.Abs((bearing-priorBearing+540)%360-180);
            consistent=lastStep>=0&&now-lastStep<1.5&&difference<35?consistent+1:1;
            Bearing=priorBearing=bearing;Confidence=consistent>=2?.8f:0;lastStep=now;origin=p;at=now;
        }
    }
}
