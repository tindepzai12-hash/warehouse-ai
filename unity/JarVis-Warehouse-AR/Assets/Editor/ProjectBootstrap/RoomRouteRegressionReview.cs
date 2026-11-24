using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class RoomRouteRegressionReview
    {
        private static readonly List<string> Checks = new List<string>();
        private static readonly Pose Anchor = new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0));
        private static void Check(bool pass,string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        private static WalkingRouteFollower Aligned(WalkingRouteData data)
        {
            WalkingRouteFollower follower = new WalkingRouteFollower();
            follower.Prepare(data);
            Check(follower.AlignAtMarker(Anchor,new Vector3(0,1.3f,0),0,0,true),"Measured A alignment succeeds");
            return follower;
        }
        public static void Run()
        {
            Checks.Clear();
            WalkingRouteData data = RoomRouteReview.Fixture();
            data.points.Clear();
            for (int step = 0; step <= 60; step++) data.points.Add(new RoutePoint { x = step == 0 || step == 60 ? 0 : Mathf.Sin(step * 1.7f) * 0.035f,z = step * 0.1f,seconds = step * 0.1 });
            string raw = JsonUtility.ToJson(data);
            WalkingRouteFollower straight = Aligned(data);
            Check(straight.Data.points.Count == 2,"3.5cm lateral scan/walking jitter does not create false corners");
            Check(JsonUtility.ToJson(data) == raw,"Raw saved route remains byte-equivalent after guidance preparation");
            foreach (float yaw in new[] { 0f,90f,-90f,179f })
            {
                straight.Observe(new Vector3(0,1.3f,0),yaw,0.1,true);
                Check(straight.Direction == "ĐI THẲNG","Camera yaw " + yaw + " does not fabricate right/left road turn");
            }
            Check(!straight.OffRoute,"Long straight segment is not off-route merely because waypoint is far");
            for (int step = 1; step <= 60; step++) straight.Observe(new Vector3(0,1.3f,step * 0.1f),0,0.1 + step * 0.1,true);
            Check(straight.Arrived,"Straight route with simplified long segment arrives");
            WalkingRouteData bent = RoomRouteReview.Fixture();
            bent.points = new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 2 },new RoutePoint { x = 2,z = 2 } };
            WalkingRouteFollower corner = Aligned(bent);
            for (int step = 1; step <= 18; step++) corner.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            Check(corner.Direction.Contains("PHẢI"),"Real right-angle corner is preserved");
            for (int step = 1; step <= 20; step++) corner.Observe(new Vector3(step * 0.1f,1.3f,2),90,1.8 + step * 0.1,true);
            Check(corner.Arrived,"Preserved corner progresses sequentially");
            bent.points[2].x = -2;
            WalkingRouteFollower left = Aligned(bent);
            for (int step = 1; step <= 18; step++) left.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            Check(left.Direction.Contains("TRÁI"),"Real left-angle corner is not forced into the reference right turn");
            WalkingRouteData loop = RoomRouteReview.Fixture();
            loop.points = new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 2 },new RoutePoint { x = 2,z = 2 },new RoutePoint { x = 2 },new RoutePoint() };
            WalkingRouteFollower loopFollower = Aligned(loop);
            Check(loopFollower.Active && !loopFollower.Arrived && loopFollower.Index == 1 && loopFollower.TravelledMetres == 0,"U/loop returning to A cannot skip directly to a later nearby endpoint");
            WalkingRouteFollower transient = Aligned(data);
            transient.Observe(new Vector3(0,1.3f,0),0,0.05,false);
            Check(transient.Suspended && !transient.ShowArrow && transient.TravelledMetres == 0,"Short loss freezes progress and hides guidance");
            transient.Observe(new Vector3(0,1.3f,0),0,0.1,true);
            Check(transient.Suspended,"One good frame does not instantly resume guidance");
            transient.Observe(new Vector3(0,1.3f,0),0,0.35,true);
            Check(transient.Active && !transient.Suspended && transient.ShowArrow,"Continuous near-identical pose and stable tracking can resume after short loss");
            transient.Observe(new Vector3(0,1.3f,0),0,0.4,false);
            transient.Observe(new Vector3(0,1.3f,0),0,1.11,false);
            Check(!transient.Active && transient.WaitingQr,"Tracking timeout opens A rescan within navigation, not a dead state");
            Check(transient.AlignAtMarker(Anchor,new Vector3(0,1.3f,0),0,2,true),"Recovery accepts A again without leaving the navigation screen");
            WalkingRouteFollower intermittent = Aligned(data);
            intermittent.Observe(new Vector3(0,1.3f,0),0,0.05,false);
            intermittent.Observe(new Vector3(0,1.3f,0),0,0.1,true);
            intermittent.Observe(new Vector3(0,1.3f,0),0,0.2,false);
            intermittent.Observe(new Vector3(0,1.3f,0),0,0.35,true);
            Check(intermittent.Suspended,"Intermittent good frames cannot count as continuous stable recovery");
            intermittent.Observe(new Vector3(0,1.3f,0),0,0.6,true);
            Check(!intermittent.Suspended && intermittent.Active,"Stable recovery restarts its timer after another lost frame");
            WalkingRouteFollower rescanned = Aligned(data);
            for (int step = 1; step <= 20; step++) rescanned.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            rescanned.RequireScan("Regression rescan");
            Check(rescanned.AlignAtMarker(Anchor,new Vector3(0,1.3f,0),0,4,true) && rescanned.TravelledMetres < 0.01f && rescanned.Remaining > 5.9f,"Rescan at A resets travelled progress instead of hiding first arrows");
            WalkingRouteFollower jumped = Aligned(data);
            jumped.Observe(new Vector3(1,1.3f,0),0,0.1,true);
            Check(!jumped.ShowArrow && jumped.WaitingQr && jumped.Message.Contains("nhảy"),"Real pose jump still invalidates alignment and names the cause");
            WalkingRouteFollower movedDuringLoss = Aligned(data);
            movedDuringLoss.Observe(new Vector3(0,1.3f,0),0,0.05,false);
            movedDuringLoss.Observe(new Vector3(0.3f,1.3f,0),0,0.1,true);
            Check(movedDuringLoss.WaitingQr && !movedDuringLoss.Active,"Movement during tracking loss is not silently bridged");
            RoomScanFeedback scan = new RoomScanFeedback();
            scan.Begin(RoomRouteAlignment.StartQr); scan.Tick(0,true,1);
            Check(scan.State == RoomScanState.WaitingCamera && !scan.Animating,"No camera frame cannot pretend scanner is running");
            scan.Frame(0); scan.Tick(0,true,0);
            Check(scan.State == RoomScanState.Searching && scan.Animating,"Fresh camera frames drive scanning animation");
            scan.Decoded(RoomRouteAlignment.EndQr,0.1); scan.Tick(0.1,true,1);
            Check(scan.State == RoomScanState.WrongCode,"Wrong QR is visually distinct even with a marker pose");
            scan.Decoded(RoomRouteAlignment.StartQr,0.2); scan.Tick(0.2,true,0.6f);
            Check(scan.State == RoomScanState.Locating && scan.Progress == 0.6f,"Read QR is distinct from measured pose lock");
            scan.Tick(0.3,true,1);
            Check(scan.State != RoomScanState.Locked,"Pose progress alone cannot fake controller success");
            scan.Lock(0.3); scan.Tick(0.5,true,1);
            Check(scan.State == RoomScanState.Locked,"Successful controller transition gets green acknowledgement");
            scan.Tick(0.6,false,1);
            Check(scan.State == RoomScanState.WaitingTracking && !scan.Animating,"Tracking loss cancels stale green acknowledgement");
            scan.Frame(1); scan.Decoded(RoomRouteAlignment.StartQr,1); scan.Tick(3,true,1);
            Check(scan.State == RoomScanState.WaitingCamera,"Camera stall stops scan animation");
            scan.Frame(3); scan.Tick(3,true,1);
            Check(scan.State == RoomScanState.Searching,"Old decoded payload cannot remain recognized forever");
            GameObject root = new GameObject("Pause recovery regression");
            try
            {
                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                typeof(WalkingRouteRecorder).GetProperty("Ready").SetValue(recorder,true);
                typeof(WalkingRouteRecorder).GetField("recovered",BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recorder,data);
                recorder.NavigateSaved(); recorder.Follower.AlignAtMarker(Anchor,new Vector3(0,1.3f,0),0,0,true);
                typeof(WalkingRouteRecorder).GetMethod("OnApplicationPause",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recorder,new object[] { true });
                Check(recorder.Follower.WaitingQr && !recorder.Follower.Active && recorder.HasSavedRoute,"Pause requests rescan and preserves stored route");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Directory.CreateDirectory("Logs/RoomRoutes");
            File.WriteAllLines("Logs/RoomRoutes/regression-033.txt",Checks);
        }
    }
}
