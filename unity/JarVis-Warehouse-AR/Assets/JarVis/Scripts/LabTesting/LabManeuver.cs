using System;
using System.Collections.Generic;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabTurn
    {
        public string id,type;
        public LabPoint point;
        public float along,angle;
    }
    public static class LabManeuver
    {
        public static List<LabTurn> Build(IList<LabPoint> path)
        {
            var turns=new List<LabTurn>();if(path==null)return turns;float along=0;
            for(int i=1;i<path.Count-1;i++)
            {
                along+=LabPaths.Distance(path[i-1],path[i]);
                // Short access spurs at a standing point are not a physical aisle turn.
                if(along<.35f||LabPaths.Length(path)-along<.35f)continue;
                var a=LabPaths.Sample(path,Math.Max(0,along-.65f));
                var c=LabPaths.Sample(path,Math.Min(LabPaths.Length(path),along+.65f));
                float angle=LabNavigationPolicy.TurnAngle(a,path[i],c);
                if(Math.Abs(angle)<35)continue;
                string type=Math.Abs(angle)>150?"UTURN":angle>0?"RIGHT":"LEFT";
                if(turns.Count>0&&along-turns[turns.Count-1].along<.8f&&turns[turns.Count-1].type==type)continue;
                turns.Add(new LabTurn{id="turn-"+i,type=type,point=path[i],along=along,angle=angle});
            }
            return turns;
        }
        public static LabTurn Next(IList<LabTurn> turns,float along)
        {foreach(var turn in turns)if(along<turn.along+.7f)return turn;return null;}
        public static string Title(LabTurn turn,float along,bool arrived=false)
        {
            if(arrived)return "ĐÃ GẦN ĐÍCH";
            if(turn==null||turn.along-along>3)return "ĐI THẲNG";
            string word=turn.type=="RIGHT"?"RẼ PHẢI":turn.type=="LEFT"?"RẼ TRÁI":"QUAY ĐẦU";
            return turn.along-along>1?"SẮP "+word:word;
        }
    }
    public sealed class LabInstructionLatch
    {
        string current,candidate;double since,candidateSince;
        public void Reset(){current=candidate=null;since=candidateSince=0;}
        public string Update(string wanted,double now,bool urgent=false)
        {
            if(current==null||urgent){current=wanted;since=now;candidate=null;return current;}
            if(current==wanted){candidate=null;return current;}
            if(candidate!=wanted){candidate=wanted;candidateSince=now;}
            if(now-since>=.5&&now-candidateSince>=.2){current=wanted;since=now;candidate=null;}
            return current;
        }
    }
    public sealed class LabRouteProgress
    {
        LabPoint previousPosition;
        public float Along {get;private set;}
        public float CrossTrack {get;private set;}
        public int Segment {get;private set;}
        public float Total {get;private set;}
        public LabPoint Projection {get;private set;}
        public void Reset(IList<LabPoint> path){previousPosition=null;Along=0;Segment=0;CrossTrack=0;Total=LabPaths.Length(path);Projection=path.Count>0?path[0]:null;}
        public void Update(IList<LabPoint> path,LabPoint position)
        {
            if(path==null||path.Count<2||!LabPaths.Valid(position))return;
            float budget=previousPosition==null?float.PositiveInfinity:.20f+1.6f*LabPaths.Distance(previousPosition,position);
            previousPosition=new LabPoint(position.x,0,position.z);
            float prefix=0,best=float.PositiveInfinity,bestAlong=Along;int bestSegment=Segment;
            LabPoint projection=Projection;
            for(int i=0;i<path.Count-1;i++)
            {
                var a=path[i];var b=path[i+1];float dx=b.x-a.x,dz=b.z-a.z,n=dx*dx+dz*dz,length=(float)Math.Sqrt(n);
                if(prefix<=Along+budget&&prefix+length>=Along-budget)
                {
                    float t=n>1e-8?Math.Max(0,Math.Min(1,((position.x-a.x)*dx+(position.z-a.z)*dz)/n)):0;
                    if(length>1e-6f)
                    {float limited=Math.Max(Along-budget,Math.Min(Along+budget,prefix+t*length));t=Math.Max(0,Math.Min(1,(limited-prefix)/length));}
                    var p=new LabPoint(a.x+dx*t,0,a.z+dz*t);float d=LabPaths.Distance(position,p);
                    // Prefer continuity when parallel sections are equally near.
                    float score=d+.03f*Math.Abs(prefix+t*length-Along);
                    if(score<best){best=score;bestAlong=prefix+t*length;bestSegment=i;projection=p;}
                }
                prefix+=length;
            }
            Along=bestAlong;Segment=bestSegment;Projection=projection;CrossTrack=LabPaths.Distance(position,projection);
        }
        public List<LabPoint> RemainingPath(IList<LabPoint> full,LabPoint position,LabFloorRouter router)
        {
            var result=new List<LabPoint>();if(full==null||full.Count<2||Projection==null)return result;
            result.Add(position);
            // Projection is useful for progress; never prepend an unchecked shortcut.
            if(LabPaths.Distance(position,Projection)>.04f){if(!router.Segment(position,Projection))return new List<LabPoint>();result.Add(Projection);}
            for(int i=Segment+1;i<full.Count;i++)if(LabPaths.Distance(result[result.Count-1],full[i])>.01f)result.Add(full[i]);
            return result;
        }
    }
}
