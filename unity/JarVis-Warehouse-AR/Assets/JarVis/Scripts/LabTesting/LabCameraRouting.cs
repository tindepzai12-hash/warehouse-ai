using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabMapKnowledge mapKnowledge;LabObstacleMemory obstacles;
        LabFloorEvidence floorEvidence;LabLocalPathfinder localPathfinder;
        public float FrontFloorCoverage {get;private set;}
        public string LocalPlannerStatus {get;private set;}="STATIC_MAP";
        public string RouteSource=>activeRoute?.id?.StartsWith("CAMERA_GRID_")==true?"CAMERA_GRID":"AUTHORED_GRAPH";
        readonly HashSet<string> manualBlocks=new HashSet<string>();
        string[] cameraBlocks=Array.Empty<string>();string obstacleContext="";double nextLiveLog;
        public string MapKnowledgeId=>mapKnowledge?.id??"MISSING";
        public bool CameraMapReady=>mapKnowledge?.Matches(Floor,corridorGraph)==true;
        public string ObstacleStatus=>CameraMapReady?obstacles?.Status??"WAITING_DEPTH":"MAP_KNOWLEDGE_MISMATCH";
        public int ObstacleCount=>obstacles?.ConfirmedCount??0;
        public int GroundObservationFrames=>obstacles?.GroundFrames??0;
        public string[] CameraBlockedEdges=>cameraBlocks;
        public string[] ManualBlockedEdges=>manualBlocks.OrderBy(s=>s).ToArray();
        public int ReferenceEpoch=>epoch;
        public bool CameraMapPoseReady=>NavigationPoseValid&&!AwaitingDirection;
        public Vector3 WorldToMapPoint(Vector3 value)=>MapFrame.InverseTransformPoint(value);
        public void ObserveDepth(double imageTimestamp,double now,IReadOnlyList<LabPoint> points)
        {
            if(!CameraMapReady||!CameraMapPoseReady)return;
            floorEvidence?.Observe(epoch,imageTimestamp,now,points);
            obstacles.Observe(epoch,imageTimestamp,now,points);
        }
        void UpdateCameraBlocks(double now)
        {
            string context=epoch+"|"+AnchorKey+"|"+MapKnowledgeId;
            if(context!=obstacleContext){obstacleContext=context;obstacles?.Reset(epoch);floorEvidence?.Reset(epoch);FrontFloorCoverage=0;cameraBlocks=Array.Empty<string>();ApplyAisleBlocks();}
            var next=CameraMapReady&&NavigationPoseValid&&!AwaitingDirection?obstacles.BlockedEdges(corridorGraph,now):Array.Empty<string>();
            bool changed=!next.SequenceEqual(cameraBlocks);
            if(changed){cameraBlocks=next;ApplyAisleBlocks();}
            // Changes off the remaining route must not restart progress and the cue animation.
            bool blocked=activeRoute!=null&&NavigationPoseValid&&obstacles?.Intersects(Route,now)==true;
            if(LabRouteStability.NeedsReplan(activeRoute!=null,changed,blocked))needsRoutePlan=true;
        }
        void ApplyAisleBlocks(){routePlanner?.SetBlocked(manualBlocks.Concat(cameraBlocks));}
        void ResetCameraEvidence(){depthFrames.Clear();obstacles?.Reset(epoch);floorEvidence?.Reset(epoch);FrontFloorCoverage=0;LocalPlannerStatus="STATIC_MAP";obstacleContext="";cameraBlocks=Array.Empty<string>();ApplyAisleBlocks();}
        void LiveNavigationLog(double now)
        {
            if(now<nextLiveLog)return;nextLiveLog=now+1;
            Debug.Log("JARVIS_NAV "+JsonUtility.ToJson(new LiveState{version=Version,knowledge=MapKnowledgeId,epoch=epoch,target=Exercise?.Target,
                tracking=Tracking,located=Located,valid=NavigationPoseValid,pose=PoseReason,alignment=TrialAlignmentStatus,
                obstacle=ObstacleStatus,obstacleCells=ObstacleCount,cameraBlocks=cameraBlocks,route=RouteId,presentation=presentation.reason}));
        }
        [Serializable] sealed class LiveState
        {public string version,knowledge,target,pose,alignment,obstacle,route,presentation;public int epoch,obstacleCells;public bool tracking,located,valid;public string[] cameraBlocks;}
    }
}
