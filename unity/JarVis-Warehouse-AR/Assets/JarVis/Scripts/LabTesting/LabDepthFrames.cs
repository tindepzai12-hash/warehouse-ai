using System;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public struct LabDepthCameraFrame
    {
        public double timestamp,received;
        public int epoch;
        public Matrix4x4 projection,cameraToWorld,display;
        public Vector3 position;
        public Quaternion rotation;
        Matrix4x4 inverseProjection;bool prepared;
        public bool Project(float u,float v,float depth,out Vector3 world)
        {
            world=default;
            if(float.IsNaN(depth)||float.IsInfinity(depth)||depth<=0)return false;
            if(!prepared){if(Mathf.Abs(projection.determinant)<1e-8f)return false;inverseProjection=projection.inverse;prepared=true;}
            var p=inverseProjection*new Vector4(2*u-1,2*v-1,1,1);
            if(Mathf.Abs(p.w)<1e-8f)return false;p/=p.w;
            if(p.z>=-1e-6f)return false;
            world=cameraToWorld.MultiplyPoint3x4(new Vector3(p.x,p.y,p.z)*(-depth/p.z));
            return LabPaths.Valid(new LabPoint(world.x,world.y,world.z));
        }
    }
    // Bounded frame history. Callback transforms are estimates, so a motion guard also applies.
    public sealed class LabDepthFrames
    {
        readonly LabDepthCameraFrame[] frames=new LabDepthCameraFrame[12];int count,next;
        public void Clear(){count=next=0;}
        public void Record(LabDepthCameraFrame frame)
        {
            if(double.IsNaN(frame.timestamp)||double.IsInfinity(frame.timestamp)||frame.timestamp<0)return;
            frames[next]=frame;next=(next+1)%frames.Length;count=Math.Min(count+1,frames.Length);
        }
        public bool Select(double timestamp,double now,int epoch,Vector3 livePosition,Quaternion liveRotation,
            out LabDepthCameraFrame selected,out string reason)
        {
            selected=default;reason="NO_MATCHING_CAMERA_FRAME";double delta=double.PositiveInfinity;bool found=false;
            if(double.IsNaN(timestamp)||double.IsInfinity(timestamp)||timestamp<0)return false;
            for(int i=0;i<count;i++)
            {
                var f=frames[i];double age=now-f.received,d=Math.Abs(f.timestamp-timestamp);
                if(f.epoch!=epoch||age<0||age>.25||d>=delta)continue;
                delta=d;selected=f;found=true;
            }
            if(!found)return false;
            if(delta>.06){reason="DEPTH_CAMERA_TIME_GAP";return false;}
            if(Vector3.Distance(livePosition,selected.position)>.08f||Quaternion.Angle(liveRotation,selected.rotation)>5)
            {reason="DEPTH_CAMERA_MOTION_GAP";return false;}
            reason="MATCHED_CAMERA_FRAME";return true;
        }
    }
}
