using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    // A fresh, stable camera observation is a candidate until the user confirms it.
    public sealed class LabMarkerSaveGate
    {
        public string Id {get;private set;}
        public Pose Pose {get;private set;}
        int epoch;double observedAt;
        public void Clear(){Id=null;}
        public void Offer(string id,Pose pose,int frameEpoch,double now)
        {
            if(!LabPrintedCard.Id(id)||double.IsNaN(now)||double.IsInfinity(now)){Clear();return;}
            Id=id;Pose=pose;epoch=frameEpoch;observedAt=now;
        }
        public bool Ready(bool tracking,bool continuity,string currentId,int currentEpoch,double now)
            =>Id!=null&&tracking&&continuity&&currentId==Id&&currentEpoch==epoch&&
              !double.IsNaN(now)&&now>=observedAt&&now-observedAt<=.5;
        public bool Take(bool tracking,bool continuity,string currentId,int currentEpoch,double now,out string id,out Pose pose)
        {
            id=null;pose=default;
            if(!Ready(tracking,continuity,currentId,currentEpoch,now))return false;
            id=Id;pose=Pose;Clear();return true;
        }
    }
}
