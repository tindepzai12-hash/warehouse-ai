using System;
using System.IO;
using JarVis.Warehouse.RouteRecording;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class WalkingRouteFollowerReview
    {
        private static int assertions;
        public static void Run()
        {
            assertions = 0;
            TextAsset asset = Resources.Load<TextAsset>("Routes/RecordedPrefix");
            Check(asset != null, "bundled real phone route");
            WalkingRouteData data = JsonUtility.FromJson<WalkingRouteData>(asset.text);
            Check(data.id == "f0b545f31bbe4a1f99d3eacf85d0ccb2" && data.points.Count == 71, "exact phone route ID / point count");
            Check(data.state == "interrupted" && string.IsNullOrEmpty(data.endQr), "do not fabricate QR B / complete state");
            Check(Mathf.Abs(WalkingRouteFollower.Bearing(Vector2.up, 0)) < 0.01f, "north maps to forward");
            Check(Mathf.Abs(WalkingRouteFollower.Bearing(Vector2.right, 0) - 90) < 0.01f, "east maps to right");
            Check(Mathf.Abs(WalkingRouteFollower.Bearing(Vector2.left, 0) + 90) < 0.01f, "west maps to left");
            Check(Mathf.Abs(Mathf.Abs(WalkingRouteFollower.Bearing(Vector2.down, 0)) - 180) < 0.01f, "south maps to turnaround");
            Check(Mathf.Abs(WalkingRouteFollower.Bearing(Vector2.right, 90)) < 0.01f, "bearing follows camera yaw");
            Check(Mathf.Abs(WalkingRouteFollower.Bearing(Vector2.up, 90) + 90) < 0.01f, "turning camera right moves target left");
            Check(Vector2.Distance(WalkingRouteDirectionGraphic.TipDirection(0), Vector2.up) < 0.001f, "arrow zero points up");
            Check(Vector2.Distance(WalkingRouteDirectionGraphic.TipDirection(90), Vector2.right) < 0.001f, "arrow positive points right");
            Check(Vector2.Distance(WalkingRouteDirectionGraphic.TipDirection(-90), Vector2.left) < 0.001f, "arrow negative points left");
            Check(Vector2.Distance(WalkingRouteDirectionGraphic.TipDirection(180), Vector2.down) < 0.001f, "arrow turnaround points down");
            foreach (float heading in new[] { 0f, 90f, -135f })
            {
                WalkingRouteFollower follower = new WalkingRouteFollower();
                follower.Prepare(data);
                Check(follower.WaitingQr, "requires start QR");
                Check(!follower.ShowArrow && follower.Progress == 0, "hide arrow and reset progress before calibration");
                Check(!follower.Align(Vector3.zero, 0, 0, true), "cannot align before QR");
                Check(!follower.AcceptQr("JWM:MAIN:B"), "reject B as start");
                Check(follower.AcceptQr("JWM:MAIN:A"), "accept A");
                Check(!follower.Align(Vector3.zero, 0, 0, false), "reject unstable calibration");
                Vector3 origin = new Vector3(7, 1.4f, -3);
                Quaternion rotation = Quaternion.Euler(0, heading, 0);
                double time = 0;
                Check(follower.Align(origin, heading, time, true), "manual confirmation");
                Check(follower.Progress == 0 && follower.Remaining > 13 && follower.Remaining < 14, "start distance matches recorded route, not straight shortcut");
                for (int index = 1; index < data.points.Count; index++)
                {
                    RoutePoint before = data.points[index - 1], after = data.points[index];
                    Vector3 from = new Vector3(before.x, before.y, before.z);
                    Vector3 to = new Vector3(after.x, after.y, after.z);
                    for (int sample = 1; sample <= 40; sample++)
                    {
                        time += 0.02;
                        follower.Observe(origin + rotation * Vector3.Lerp(from, to, sample / 40f), heading + after.yawDegrees, time, true);
                    }
                }
                Check(follower.Arrived && !follower.Active, "ordered replay arrives at last known point after rotation");
                Check(follower.Index == data.points.Count - 1, "no early endpoint shortcut");
                Check(follower.Progress == 1 && !follower.ShowArrow, "arrival completes progress and hides arrow");
            }
            WalkingRouteFollower interrupted = Start(data);
            interrupted.Observe(Vector3.zero, 0, 0.1, false);
            Check(!interrupted.Active && !interrupted.Arrived, "tracking loss stops navigation");
            Check(!interrupted.ShowArrow, "tracking loss hides arrow");
            WalkingRouteFollower off = Start(data);
            for (int sample = 1; sample <= 20; sample++) off.Observe(new Vector3(sample * 0.15f,0,0), 0, sample * 0.1, true);
            Check(off.Active && off.OffRoute && !off.ShowArrow && !off.Arrived, "walk off route: hide arrow without fake arrival");
            off.Stop("pause fixture");
            Check(!off.ShowArrow && off.Progress == 0, "stopped UI has no stale progress / direction");
            WalkingRouteFollower jump = Start(data);
            jump.Observe(new Vector3(20,0,0), 0, 0.1, true);
            Check(!jump.Active && !jump.Arrived, "pose jump stops navigation");
            WalkingRouteFollower gap = Start(data);
            gap.Observe(Vector3.zero, 0, 2, true);
            Check(!gap.Active, "frame gap stops navigation");
            WalkingRouteFollower invalid = new WalkingRouteFollower();
            invalid.Prepare(new WalkingRouteData());
            Check(!invalid.WaitingQr, "empty route rejected");
            Directory.CreateDirectory("Logs/WalkingRoutes");
            File.WriteAllText("Logs/WalkingRoutes/follower-result.txt", "PASS " + assertions + " assertions\nReal phone route replay in synthetic poses only; manual alignment and field navigation NOT verified.\nUTC=" + DateTime.UtcNow.ToString("O"));
        }
        private static WalkingRouteFollower Start(WalkingRouteData data)
        {
            WalkingRouteFollower result = new WalkingRouteFollower();
            result.Prepare(data);
            result.AcceptQr(data.startQr);
            result.Align(Vector3.zero, 0, 0, true);
            return result;
        }
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Follower review: " + name);
            assertions++;
        }
    }
}
