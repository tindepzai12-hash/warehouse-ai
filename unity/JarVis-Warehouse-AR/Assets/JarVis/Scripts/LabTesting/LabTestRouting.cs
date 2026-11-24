using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        readonly LabNavigationEstimate poseEstimate=new LabNavigationEstimate();
        readonly LabRouteProgress routeProgress=new LabRouteProgress();
        readonly LabInstructionLatch instructionLatch=new LabInstructionLatch();
        string sessionMode="NOT_STARTED";
        LabRoutePlanner routePlanner;LabCorridorGraph corridorGraph;
        LabRouteChoice activeRoute;
        LabRouteVisualContext visualRoute;
        List<LabRouteChoice> routeChoices=new List<LabRouteChoice>();
        List<LabTurn> routeTurns=new List<LabTurn>();LabTurn currentTurn;
        bool needsRoutePlan;double deviationSince=-1;
        public string ManeuverTitle {get;private set;}="ĐI THẲNG";
        public float TurnDistance=>currentTurn==null?float.PositiveInfinity:Mathf.Max(0,currentTurn.along-routeProgress.Along);
        public string RouteId=>activeRoute?.id;
        public IReadOnlyList<LabRouteChoice> RouteChoices=>routeChoices;
        public void ChangeDestination(string target)
        {
            if(!NavigationPoseValid||!CanChooseFixed(target))return;
            ResetRouteState();Exercise=new LabExercise(target);plannedTarget=null;nextPlan=0;
            if(journal?.Active!=true)BeginNavigationJournal();
            Message="Đã đổi đích tới "+target+"; giữ nguyên mốc căn hiện tại.";
        }
        void InitializePlanner()
        {
            var resource=Resources.Load<TextAsset>("LabTest/LabCorridorsNew");
            corridorGraph=resource!=null?JsonUtility.FromJson<LabCorridorGraph>(resource.text):null;
            routePlanner=new LabRoutePlanner(Floor,corridorGraph);
            mapKnowledge=LabMapKnowledge.Load();manualBlocks.Clear();cameraBlocks=Array.Empty<string>();
            obstacles=CameraMapReady?new LabObstacleMemory(mapKnowledge.depth,floorRouter??new LabFloorRouter(Floor)):null;obstacleContext="";
            floorEvidence=new LabFloorEvidence(floorRouter??new LabFloorRouter(Floor));localPathfinder=new LabLocalPathfinder(Floor);
        }
        void ResetRouteState()
        {activeRoute=null;visualRoute=null;routeCueSampler.Reset();routeChoices.Clear();routeTurns.Clear();currentTurn=null;deviationSince=-1;needsRoutePlan=false;instructionLatch.Reset();ManeuverTitle="ĐI THẲNG";decision=null;turnTracker.Reset();cameraCoach.Reset();movement.Reset();renderAim=null;nextDecision=0;}
        void PlanRoutes(LabPoint from,LabPoint to,double now)
        {
            if(routePlanner==null)InitializePlanner();
            bool sameTarget=plannedTarget==Exercise.Target;var previous=activeRoute;
            nextPlan=now+1;plannedFrom=from;plannedTarget=Exercise.Target;
            routeError=routePlanner.Find(from,to,out routeChoices);
            if(CameraMapReady&&obstacles!=null)
            {
                routeChoices=routeChoices.Where(r=>!obstacles.Intersects(r.points,now)).ToList();
                if(routeChoices.Count==0&&routeError==null)routeError="CAMERA_OBSTACLE_NO_ROUTE";
            }
            LocalPlannerStatus="STATIC_MAP";
            // Manual exclusions remain authoritative. Never bypass them with an unconstrained grid connector.
            if(CameraMapReady&&obstacles!=null&&manualBlocks.Count==0&&ObstacleCount>0)
            {
                var points=obstacles.ConfirmedPoints(now);
                string error=localPathfinder.Find(from,to,points,out var bypass);
                LocalPlannerStatus=error??"LOCAL_PATH_COMPUTED";
                if(error==null&&bypass.Count>=2)
                {
                    float length=LabPaths.Length(bypass);
                    var local=new LabRouteChoice{id="CAMERA_GRID_"+(routeRevision+1),edges=Array.Empty<string>(),points=bypass.ToArray(),length=length,cost=length,turns=LabManeuver.Build(bypass).Count};
                    routeChoices.Add(local);routeChoices=routeChoices.OrderBy(r=>r.cost).Take(3).ToList();routeError=null;
                }
            }
            if(previous!=null&&sameTarget&&!previous.edges.Any(e=>manualBlocks.Contains(e)||cameraBlocks.Contains(e)))
            {
                routeProgress.Update(previous.points,from);
                var remaining=routeProgress.RemainingPath(previous.points,from,floorRouter);
                if(routeProgress.CrossTrack<=.60f&&remaining.Count>=2&&obstacles?.Intersects(remaining,now)!=true)
                {
                    // Keep a still safe route instead of replacing its revision for cosmetic improvements.
                    activeRoute=previous;routeError=null;
                    if(!routeChoices.Any(r=>r.id==previous.id))routeChoices.Insert(0,previous);
                    Route=remaining;deviationSince=-1;return;
                }
            }
            activeRoute=routeChoices.FirstOrDefault();
            if(activeRoute!=null)ActivateRoute(activeRoute,"AUTO_OR_DEVIATION");else Route.Clear();
        }
        void ActivateRoute(LabRouteChoice route,string reason)
        {
            activeRoute=route;routeRevision++;instructionLatch.Reset();turnTracker.Reset();decision=null;routeProgress.Reset(route.points);routeTurns=LabManeuver.Build(route.points);
            visualRoute=new LabRouteVisualContext(route.points,epoch,routeRevision,Exercise.Target);routeCueSampler.Reset();
            Route=route.points.ToList();deviationSince=-1;
            if(!string.IsNullOrEmpty(Folder))
            {
                try {Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"route-events.jsonl"),JsonUtility.ToJson(new RouteEvent{utc=DateTime.UtcNow.ToString("O"),reason=reason,selected=route.id,candidates=routeChoices.ToArray()})+"\n");}
                catch(Exception){Message="Chưa lưu được lịch sử tuyến; xuất ZIP sau khi kiểm tra dung lượng.";}
            }
        }
        public void ChooseRoute(int index)
        {
            if(!NavigationPoseValid||index<0||index>=routeChoices.Count)return;
            var route=routeChoices[index];
            // A stale alternative must never prepend a shortcut back through furniture.
            var progress=new LabRouteProgress();progress.Reset(route.points);progress.Update(route.points,Position);
            var remaining=progress.RemainingPath(route.points,Position,floorRouter);
            if(remaining.Count<2||progress.CrossTrack>.60f||route.edges.Any(e=>manualBlocks.Contains(e)||cameraBlocks.Contains(e))||obstacles?.Intersects(remaining,Time.realtimeSinceStartupAsDouble)==true)
            {needsRoutePlan=true;Message="Tuyến đã đổi hoặc bị chắn. Đang cập nhật từ vị trí hiện tại; mở lại map để chọn.";return;}
            ActivateRoute(route,"USER_SELECTED");
        }
        [Serializable] sealed class RouteEvent {public string utc,reason,selected;public LabRouteChoice[] candidates;}
        [Serializable] sealed class NavigationExport {public string appVersion,mapHash,graphHash,coordinateSystem,mode,selected;public int revision;public LabRouteChoice[] candidates;public LabTurn[] turns;public LabPoseEstimate estimate;public bool physicalAccuracyVerified=false;public LabCalibrationProfile calibration;}
        void ExportNavigationState()
        {
            if(corridorGraph!=null)File.WriteAllText(Path.Combine(Folder,"corridors.json"),JsonUtility.ToJson(corridorGraph,true));
            File.WriteAllText(Path.Combine(Folder,"navigation-state.json"),JsonUtility.ToJson(new NavigationExport{appVersion=Version,mapHash=Floor.geometrySha256,graphHash=corridorGraph?.graphHash,coordinateSystem=Floor.coordinateSystem,revision=Floor.revision,mode=sessionMode,selected=activeRoute?.id,candidates=routeChoices.ToArray(),turns=routeTurns.ToArray(),estimate=poseEstimate.Current,calibration=profile},true));
        }
    }
}
