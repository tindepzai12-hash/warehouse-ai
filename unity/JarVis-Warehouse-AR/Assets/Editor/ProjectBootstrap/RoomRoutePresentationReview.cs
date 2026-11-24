using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class RoomRoutePresentationReview
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool pass,string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        public static void Run()
        {
            Checks.Clear();
            foreach (float remaining in new[] { 0f,1,2,2.01f,5,5.5f,8,8.01f,10,1000 })
            {
                int expected = remaining <= 2 ? 0 : remaining <= 8 ? 1 : 2;
                Check(RoomRoutePresentation.Budget(remaining) == expected,"Arrow budget boundary " + remaining);
            }
            Check(RoomRoutePresentation.Budget(float.NaN) == 0,"Invalid distance never spawns arrows");
            for (int step = 0; step < 120; step++)
            {
                float travelled = step * 0.1f,first = RoomRoutePresentation.First(travelled);
                Check(first >= travelled + 1.199f && first - travelled < 3.401f,"World-grid glyph stays ahead, not attached to camera " + step);
            }
            foreach (float depth in new[] { 0.2f,0.5f,1,2,5,10f })
            {
                float scale = RoomRoutePresentation.MarkerScale(depth,60,0.73f,0.06f);
                float fraction = scale * 0.73f / (depth * 2 * Mathf.Tan(30 * Mathf.Deg2Rad));
                Check(fraction <= 0.0601f,"B projected height capped near and far " + depth);
                float arrow = RoomRoutePresentation.ArrowScale(depth,60,412f / 915);
                float width = arrow * 0.68f / (depth * 2 * Mathf.Tan(30 * Mathf.Deg2Rad) * 412 / 915);
                Check(width <= 0.1401f && arrow <= 0.38f,"Arrow viewport width cap " + depth);
            }
            Check(RoomRoutePresentation.MarkerScale(-1,60,0.73f,0.06f) == 0,"Behind-camera marker cannot grow backwards");
            Check(!RoomFeatureSampler.Visible(new Vector3(float.NaN,0.5f,2)) && !RoomFeatureSampler.Visible(new Vector3(0.5f,0.5f,-1)) && !RoomFeatureSampler.Visible(new Vector3(1.1f,0.5f,2)),"Offscreen/invalid/behind-camera features excluded");
            Check(RoomFeatureSampler.Visible(new Vector3(0.5f,0.5f,2)),"Visible real-world feature accepted");
            WalkingRouteData cornerData = RoomRouteReview.Fixture();
            cornerData.roomAnchorId = null;
            cornerData.points = new List<RoutePoint> { new RoutePoint(),new RoutePoint { z = 1 },new RoutePoint { z = 3 },new RoutePoint { x = 3,z = 3 },new RoutePoint { x = 3 },new RoutePoint() };
            WalkingRouteFollower corner = new WalkingRouteFollower();
            corner.Prepare(cornerData); corner.AcceptQr(cornerData.startQr); corner.Align(Vector3.zero,0,0,true);
            Check(Mathf.Abs(RoomRoutePresentation.VisibleUntil(corner) - 2.82f) < 0.001f,"Lookahead stops BEFORE first actual corner, even beyond collinear samples and future loop branches");
            Check(RoomRoutePresentation.Sample(corner,2,0,out Vector3 straight,out Vector3 ahead,out _) && Vector3.Distance(straight,new Vector3(0,0,2)) < 0.001f && ahead == Vector3.forward,"Recorded straight segment keeps its own direction, not direct bearing to B");
            Check(RoomRoutePresentation.Sample(corner,3.5f,0,out Vector3 turned,out Vector3 right,out _) && Vector3.Distance(turned,new Vector3(0.5f,0,3)) < 0.001f && right == Vector3.right,"Only the recorded corner changes arrow direction");
            RoomEnvironmentReadiness environment = new RoomEnvironmentReadiness();
            for (int step = 0; step <= 40; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Observe(new Vector3(step * 0.003f,1.3f,0),Quaternion.Euler(0,step,0),now,true);
            }
            Check(!environment.Ready && environment.Progress < 1,"Movement and elapsed time without real feature samples never reach 100 percent");
            environment = new RoomEnvironmentReadiness();
            for (int step = 0; step <= 40; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Features(100,1,now);
                environment.Observe(new Vector3(step * 0.003f,1.3f,0),Quaternion.Euler(0,step,0),now,true);
            }
            Check(!environment.Ready,"Many points in only one viewing cell do not pass environmental observation");
            environment = new RoomEnvironmentReadiness(); RoomRouteReadinessReview.Prepared(environment);
            Check(environment.Ready,"Sustained real samples across cells can finish environment stage");
            for (int step = 33; step <= 130; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.Euler(0,19.2f,0),now,true);
            }
            Check(environment.Ready && environment.Progress == 1,"Completed observation survives low-feature QR view while camera and pose remain continuous");

            WalkingRouteFollower follower = new WalkingRouteFollower();
            follower.Prepare(RoomRouteReview.Fixture());
            follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true);
            follower.Observe(new Vector3(0,1.3f,0.3f),0,0.1,true,false);
            Check(follower.TravelledMetres == 0 && follower.Remaining == 4,"Pre-departure observation keeps tracking guard but freezes route progress");
            follower.Observe(new Vector3(0,1.3f,0.3f),0,0.2,false,false);
            Check(follower.Suspended,"Waiting for Start never disables tracking-loss protection");
            GameObject root = new GameObject("Departure controller QA");
            try
            {
                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                typeof(WalkingRouteRecorder).GetProperty("Ready").SetValue(recorder,true);
                typeof(WalkingRouteRecorder).GetField("recovered",BindingFlags.NonPublic | BindingFlags.Instance).SetValue(recorder,RoomRouteReview.Fixture());
                recorder.NavigateSaved(); RoomRouteReadinessReview.Prepared(recorder.Environment);
                Check(recorder.PreparationProgress <= 0.8f && !recorder.GuidanceStarted,"Environment ready is only 80 percent before A pose is locked");
                recorder.Follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true);
                typeof(WalkingRouteRecorder).GetProperty("AwaitingDeparture").SetValue(recorder,true);
                Check(recorder.PreparationProgress == 1 && !recorder.GuidanceStarted,"Locked A completes preparation but waits for explicit Start");
                recorder.Environment.Pause(); recorder.Primary();
                Check(!recorder.GuidanceStarted && recorder.AwaitingDeparture,"Start cannot bypass stale/unprepared camera");
                recorder.Cancel();
                Check(recorder.HasSavedRoute && !recorder.AwaitingDeparture && !recorder.GuidanceStarted,"Cancel pre-departure preserves map and clears session gate");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Directory.CreateDirectory("Logs/RoomRoutes");
            File.WriteAllLines("Logs/RoomRoutes/presentation-034.txt",Checks);
        }
    }
}
