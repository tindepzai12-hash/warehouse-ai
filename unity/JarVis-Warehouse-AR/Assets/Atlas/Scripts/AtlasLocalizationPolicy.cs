using UnityEngine;

namespace AtlasAR
{
    // These gates never create a position: they accept or reject measured poses.
    public sealed class AtlasPoseConsensus
    {
        public int Count { get; private set; }
        public Pose Pose { get; private set; }
        double lastImage=-1;
        float firstAt,lastAt;
        public void Reset(){Count=0;lastImage=-1;firstAt=lastAt=0;}
        public bool Offer(Pose pose,double imageTime,float capturedAt,float now)
        {
            if(!AtlasMap.Finite(pose.position)||!AtlasPoseMath.Finite(pose.rotation)||
               !double.IsFinite(imageTime)||imageTime<=lastImage||now<capturedAt||now-capturedAt>2.5f)return false;
            lastImage=imageTime;
            if(Count==0||now-lastAt>4||Vector3.Distance(Pose.position,pose.position)>.4f||Quaternion.Angle(Pose.rotation,pose.rotation)>8)
            {Count=1;Pose=pose;firstAt=now;}
            else{Count++;Pose=new Pose(Vector3.Lerp(Pose.position,pose.position,.35f),Quaternion.Slerp(Pose.rotation,pose.rotation,.35f));}
            lastAt=now;return Count>=3&&now-firstAt>=.6f;
        }
    }
    public enum AtlasRecoveryAction { Waiting,Resume,Expired }
    public sealed class AtlasTrackingRecovery
    {
        public bool Active {get;private set;}
        float began,stableSince=-1;
        public void Begin(float now){if(Active)return;Active=true;began=now;stableSince=-1;}
        public void Reset(){Active=false;stableSince=-1;}
        public AtlasRecoveryAction Observe(float now,bool tracking,bool anchorTracked)
        {
            if(!Active)return AtlasRecoveryAction.Resume;
            if(now-began>=8)return AtlasRecoveryAction.Expired;
            if(!tracking||!anchorTracked){stableSince=-1;return AtlasRecoveryAction.Waiting;}
            if(stableSince<0)stableSince=now;
            if(now-stableSince<.7f)return AtlasRecoveryAction.Waiting;
            Reset();return AtlasRecoveryAction.Resume;
        }
    }
}
