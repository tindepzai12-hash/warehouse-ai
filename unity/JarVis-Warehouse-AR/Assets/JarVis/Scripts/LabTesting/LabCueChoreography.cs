using System;

namespace JarVis.Warehouse.Lab
{
    // No Unity clock or navigation side effects: deterministic under replay.
    public sealed class LabCueChoreography
    {
        public const double Stagger=.160,Fade=.220,Period=1.4;
        readonly int[] cells={-1,-1,-1};
        readonly double[] starts=new double[3];
        readonly bool[] retained=new bool[3];
        int epoch=-1,revision=-1;string maneuver;LabCueMode mode;
        bool valid;double lastTime;
        public void Invalidate(){valid=false;for(int i=0;i<3;i++)cells[i]=-1;}
        public void Context(int nextEpoch,int nextRevision,string nextManeuver,LabCueMode nextMode,double now)
        {
            if(!valid||epoch!=nextEpoch||revision!=nextRevision||maneuver!=nextManeuver||mode!=nextMode||now<lastTime)
            {for(int i=0;i<3;i++)cells[i]=-1;epoch=nextEpoch;revision=nextRevision;maneuver=nextManeuver;mode=nextMode;valid=true;}
            lastTime=now;
        }
        public void Sync(LabCueToken[] tokens,int count,double now)
        {
            count=Math.Max(0,Math.Min(3,count));
            bool hadAny=false;
            for(int i=0;i<3;i++)
            {
                retained[i]=false;
                for(int j=0;j<count;j++)if(cells[i]==tokens[j].cell){retained[i]=true;hadAny=true;break;}
            }
            for(int i=0;i<3;i++)if(!retained[i])cells[i]=-1;
            for(int j=0;j<count;j++)
            {
                bool exists=false;for(int i=0;i<3;i++)if(cells[i]==tokens[j].cell){exists=true;break;}
                if(exists)continue;
                for(int i=0;i<3;i++)if(cells[i]<0){cells[i]=tokens[j].cell;starts[i]=now+(hadAny?0:j*Stagger);break;}
            }
        }
        public float Alpha(int cell,double now,bool reduced)
        {
            if(!valid)return 0;
            for(int i=0;i<3;i++)if(cells[i]==cell)
            {
                if(reduced)return 1;
                double t=Math.Max(0,Math.Min(1,(now-starts[i])/Fade));
                return (float)(1-(1-t)*(1-t)*(1-t));
            }
            return 0;
        }
        public static float Light(double now,int rank,bool reduced)
        {
            if(reduced)return 1;
            double phase=(now/Period-rank/3.0)%1;if(phase<0)phase+=1;
            return .94f+.06f*(float)Math.Cos(phase*Math.PI*2);
        }
    }

    public enum LabFloorVisualSource { Unavailable, EstimatedPhoneHeight, UserAdjusted }
    public sealed class LabFloorVisualReference
    {
        public float Offset {get;private set;}
        public LabFloorVisualSource Source {get;private set;}=LabFloorVisualSource.Unavailable;
        public void Begin(bool estimated){Offset=0;Source=estimated?LabFloorVisualSource.EstimatedPhoneHeight:LabFloorVisualSource.Unavailable;}
        public void Adjust(float metres)
        {
            if(float.IsNaN(metres)||float.IsInfinity(metres))return;
            Offset=Math.Max(-.75f,Math.Min(.75f,Offset+metres));Source=LabFloorVisualSource.UserAdjusted;
        }
        public void Restore(){Offset=0;Source=LabFloorVisualSource.EstimatedPhoneHeight;}
    }
}

