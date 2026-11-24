using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class RoomRouteReadinessReview
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool pass,string message)
        {
            if (!pass) throw new InvalidOperationException(message);
            Checks.Add("PASS " + message);
        }
        public static void Prepared(RoomEnvironmentReadiness environment)
        {
            environment.Begin();
            for (int step = 0; step <= 32; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Features(30,4,now);
                environment.Observe(new Vector3(step * 0.003f,1.3f,0),Quaternion.Euler(0,step * 0.6f,0),now,true);
            }
        }
        private static WalkingRouteFollower Aligned(params Vector2[] points)
        {
            WalkingRouteData data = RoomRouteReview.Fixture();
            data.points.Clear();
            foreach (Vector2 point in points) data.points.Add(new RoutePoint { x = point.x,z = point.y });
            WalkingRouteFollower follower = new WalkingRouteFollower();
            follower.Prepare(data);
            Check(follower.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true),"Test alignment succeeds");
            return follower;
        }
        public static void Run()
        {
            Checks.Clear();
            RoomEnvironmentReadiness environment = new RoomEnvironmentReadiness();
            environment.Begin();
            environment.Observe(Vector3.zero,Quaternion.identity,10,true);
            Check(!environment.Ready && environment.Message.Contains("camera"),"No image cannot pass preparation from tracking/time alone");
            for (int step = 0; step <= 40; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Observe(Vector3.zero,Quaternion.identity,now,true);
            }
            Check(!environment.Ready && environment.Progress < 1,"Still phone never auto-passes preparation");
            environment = new RoomEnvironmentReadiness();
            for (int step = 0; step <= 40; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Observe(Vector3.zero,Quaternion.Euler(0,step,0),now,true);
            }
            Check(!environment.Ready && environment.Message.Contains("không chỉ xoay"),"Rotation alone asks for gentle translation");
            environment = new RoomEnvironmentReadiness();
            for (int step = 0; step <= 40; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,step * 50000000L);
                environment.Observe(new Vector3(step * 0.003f,0,0),Quaternion.identity,now,true);
            }
            Check(!environment.Ready && environment.Message.Contains("Lia máy"),"Translation alone asks for additional view change");
            environment = new RoomEnvironmentReadiness();
            for (int step = 0; step <= 40; step++)
            {
                double now = step * 0.05;
                environment.Frame(now,42);
                environment.Observe(new Vector3(step * 0.003f,0,0),Quaternion.Euler(0,step,0),now,true);
            }
            Check(!environment.Ready && !environment.Fresh(2),"Repeated frame timestamp is not fresh video");
            environment = new RoomEnvironmentReadiness(); Prepared(environment);
            Check(environment.Ready && Mathf.Approximately(environment.Progress,1),"Real-frame continuous stable translation plus view change unlocks A");
            environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.Euler(0,19.2f,0),1.7,false);
            Check(!environment.Ready,"Tracking loss revokes preparation before A lock");
            environment = new RoomEnvironmentReadiness(); Prepared(environment);
            environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.identity,2.2,true);
            Check(!environment.Ready && !environment.Fresh(2.2),"Stalled camera revokes preparation");
            environment = new RoomEnvironmentReadiness(); Prepared(environment);
            environment.Frame(1.65,1650000000);
            environment.Observe(new Vector3(2,1.3f,0),Quaternion.Euler(0,20,0),1.65,true);
            Check(!environment.Ready,"Pose jump restarts preparation rather than completing motion criteria");
            environment = new RoomEnvironmentReadiness(); Prepared(environment);
            environment.Frame(1.65,1650000000);
            environment.Observe(new Vector3(0.096f,1.3f,0),Quaternion.Euler(0,150,0),1.65,true);
            Check(!environment.Ready,"Sudden orientation jump cannot masquerade as environment observation");
            environment = new RoomEnvironmentReadiness(); Prepared(environment); environment.Begin();
            Check(!environment.Ready && environment.Progress == 0,"Each recording/navigation attempt starts a fresh preparation");
            environment.Pause();
            Check(!environment.Fresh(1.65),"Pause invalidates stale camera freshness");
            environment.Frame(2,1);
            Check(environment.Fresh(2),"New camera session may restart its timestamp after pause");
            environment.Observe(new Vector3(float.NaN,0,0),Quaternion.identity,2,true);
            Check(!environment.Ready,"Non-finite pose never passes preparation");

            RoomRouteDeviation deviation = new RoomRouteDeviation();
            deviation.Observe(0.91f,0);
            Check(deviation.Caution && !deviation.Confirmed,"First out-of-corridor frame is caution, not confirmed off-route");
            for (int step = 1; step <= 20; step++) deviation.Observe(step % 2 == 0 ? 0.89f : 0.91f,step * 0.1);
            Check(deviation.Caution && !deviation.Confirmed,"Threshold oscillation does not flash between normal and off-route");
            deviation.Observe(0.54f,2.1); deviation.Observe(0.54f,2.3);
            Check(deviation.Caution,"One near-route frame cannot flicker arrows back on");
            deviation.Observe(0.54f,2.5);
            Check(!deviation.Caution,"Continuous return inside 0.55m clears caution after dwell");
            deviation.Observe(1,3); deviation.Observe(1,3.5);
            Check(!deviation.Confirmed,"Outside less than 0.6s is not confirmed");
            deviation.Observe(1,3.7);
            Check(deviation.Confirmed,"Sustained outside corridor confirms off-route");
            deviation.Observe(0.7f,3.8);
            Check(deviation.Confirmed,"Different exit threshold prevents off-route blinking");
            deviation.Observe(0.4f,4); deviation.Observe(0.4f,4.4);
            Check(!deviation.Caution && !deviation.Confirmed,"Stable return clears confirmed off-route");
            deviation.Observe(1.8f,5);
            Check(deviation.Confirmed,"Large deviation still warns immediately");
            deviation.Reset();
            Check(!deviation.Caution,"Rescan resets deviation history");

            WalkingRouteFollower caution = Aligned(Vector2.zero,new Vector2(0,6));
            for (int step = 1; step <= 10; step++) caution.Observe(new Vector3(step * 0.1f,1.3f,step * 0.05f),0,step * 0.1,true);
            Check(caution.RouteChecking && !caution.OffRoute && !caution.ShowArrow,"Caution suppresses misleading navigation arrows immediately");
            float travelled = caution.TravelledMetres;
            for (int step = 11; step <= 18; step++) caution.Observe(new Vector3(1,1.3f,step * 0.05f),0,step * 0.1,true);
            Check(caution.OffRoute && caution.TravelledMetres == travelled && !caution.Arrived,"Deviation freezes progress and cannot fake arrival");
            caution.RequireScan("Test");
            Check(!caution.RouteCaution && caution.WaitingQr && !caution.ShowArrow,"Invalidated alignment hides guidance and clears old caution");

            foreach (int side in new[] { -1,1 })
            {
                WalkingRouteFollower corner = Aligned(Vector2.zero,new Vector2(0,2),new Vector2(side * 2,2));
                for (int step = 1; step <= 17; step++) corner.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
                for (int step = 1; step <= 10; step++) corner.Observe(new Vector3(side * step * 0.1f,1.3f,1.7f),0,1.7 + step * 0.1,true);
                Check(corner.Index == 2 && !corner.RouteCaution && corner.TravelledMetres > 2.8f,"Small ordinary corner cut progresses to adjacent segment " + side);
            }
            WalkingRouteFollower lead = Aligned(Vector2.zero,new Vector2(0.2f,0.2f),new Vector2(0,4));
            for (int step = 1; step <= 15; step++) lead.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            Check(lead.Index == 2 && !lead.RouteCaution,"Small initial hand offset cannot trap follower on first short segment");
            WalkingRouteFollower hairpin = Aligned(Vector2.zero,new Vector2(0,2),new Vector2(0.2f,0));
            for (int step = 1; step <= 15; step++) hairpin.Observe(new Vector3(0.2f,1.3f,step * 0.1f),0,step * 0.1,true);
            Check(hairpin.Index == 1,"Nearby return leg of U-turn is not selected before endpoint");
            WalkingRouteFollower crossing = Aligned(Vector2.zero,new Vector2(0,4),new Vector2(2,4),new Vector2(2,2),new Vector2(-2,2));
            for (int step = 1; step <= 20; step++) crossing.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            Check(crossing.Index == 1 && crossing.Remaining > 8,"Crossing a future leg never skips to it");

            GameObject root = new GameObject("Preparation controller QA");
            try
            {
                WalkingRouteRecorder recorder = root.AddComponent<WalkingRouteRecorder>();
                typeof(WalkingRouteRecorder).GetProperty("Ready").SetValue(recorder,true);
                recorder.BeginRecording();
                typeof(WalkingRouteRecorder).GetMethod("HandleQr",BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recorder,new object[] { RoomRouteAlignment.StartQr,0d });
                Check(recorder.PreparingStart && recorder.ScanningStart && !recorder.Route.Active,"Correct QR cannot bypass preparation controller gate");
                recorder.Cancel();
                Check(recorder.Home && !recorder.PreparingStart,"Cancel preparation returns to existing home without extra screen");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            Directory.CreateDirectory("Logs/RoomRoutes");
            File.WriteAllLines("Logs/RoomRoutes/readiness-033.txt",Checks);
        }
    }
}
