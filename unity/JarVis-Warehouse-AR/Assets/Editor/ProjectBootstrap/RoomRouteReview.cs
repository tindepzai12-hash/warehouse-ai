using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using JarVis.Warehouse.RouteRecording;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using ZXing;

namespace ProjectBootstrap
{
    public static class RoomRouteReview
    {
        private static readonly List<string> Checks = new List<string>();
        private static void Check(bool condition,string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            Checks.Add("PASS " + name);
        }
        public static WalkingRouteData Fixture()
        {
            WalkingRoute route = new WalkingRoute();
            route.Begin(RoomRouteAlignment.StartQr,new Vector3(0,1.3f,0),0,0,true);
            RoomRouteAlignment.Capture(route.Data,new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0);
            for (int step = 1; step <= 40; step++) route.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            route.ArmEnd(true);
            route.Finish(RoomRouteAlignment.EndQr,new Vector3(0,1.3f,4),0,4.1,true);
            route.Data.destinationInRoute = new Vector3(0,0,4.5f);
            return route.Data;
        }
        public static void Run()
        {
            Checks.Clear();
            Directory.CreateDirectory("Logs/RoomRoutes");
            WalkingRouteData data = Fixture();
            Check(RoomRouteStorage.Valid(data),"Complete A/B room map accepted");
            foreach (float angle in new[] { 0f,90f,-90f,179f })
            {
                Pose marker = new Pose(new Vector3(12,2,-7),Quaternion.Euler(0,angle,0) * Quaternion.Euler(90,0,0));
                Pose restored = RoomRouteAlignment.Restore(data,marker);
                Quaternion rotation = Quaternion.Euler(0,angle,0);
                Vector3 expected = marker.position + rotation * new Vector3(0,0.1f,0.6f);
                Check(Vector3.Distance(restored.position,expected) < 0.001f,"New-session origin from marker yaw=" + angle);
                Check(Mathf.Abs(Mathf.DeltaAngle(restored.rotation.eulerAngles.y,angle)) < 0.001f,"New-session heading yaw=" + angle);
                WalkingRouteFollower follower = new WalkingRouteFollower();
                follower.Prepare(data);
                Check(follower.AlignAtMarker(marker,restored.position,angle,0,true),"Scan automatically starts guidance, no manual confirmation");
                Check(follower.MarkerAligned && !follower.WaitingQr && !follower.Confirming,"Measured marker alignment selected");
                for (int step = 1; step <= 40; step++) follower.Observe(restored.position + rotation * new Vector3(0,0,step * 0.1f),angle,step * 0.1,true);
                Check(follower.Arrived,"Dense samples do not leave progress stuck");
                Check(Vector3.Distance(follower.DestinationWorld(0),new Vector3(expected.x,0,expected.z) + rotation * new Vector3(0,0,4.5f)) < 0.001f,"Destination uses recorded B marker, not camera center");
            }
            string root = Path.GetFullPath("Logs/RoomRoutes/Storage-" + Guid.NewGuid().ToString("N"));
            RoomRouteStorage.Commit(root,data);
            Check(RoomRouteStorage.Load(root).id == data.id,"One current map survives serialization/reload");
            WalkingRouteData invalid = Fixture();
            invalid.state = "interrupted";
            bool rejected = false;
            try { RoomRouteStorage.Commit(root,invalid); } catch (InvalidDataException) { rejected = true; }
            Check(rejected && RoomRouteStorage.Load(root).id == data.id,"Interrupted replacement cannot destroy previous map");
            WalkingRouteData next = Fixture();
            RoomRouteStorage.Commit(root,next);
            Check(RoomRouteStorage.Load(root).id == next.id && Directory.GetFiles(Path.Combine(root,"room-map"),"current.json").Length == 1,"Replacement leaves exactly one selectable current map");
            File.WriteAllText(Path.Combine(root,"room-map","current.json"),"invalid json");
            Check(RoomRouteStorage.Load(root).id == data.id,"Corrupt current file recovers previous good map");
            invalid = Fixture(); invalid.endQr = RoomRouteAlignment.StartQr;
            Check(!RoomRouteStorage.Valid(invalid),"A cannot be the destination");
            invalid = Fixture(); invalid.originInMarker = new Vector3(float.NaN,0,0);
            Check(!RoomRouteStorage.Valid(invalid),"Invalid marker transform rejected");
            invalid = Fixture(); invalid.roomAnchorId = null;
            Check(!RoomRouteStorage.Valid(invalid),"Old camera-only route cannot masquerade as room map");
            WalkingRoute corner = new WalkingRoute();
            corner.Begin(RoomRouteAlignment.StartQr,new Vector3(0,1.3f,0),0,0,true);
            RoomRouteAlignment.Capture(corner.Data,new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0);
            for (int step = 1; step <= 20; step++) corner.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            for (int step = 1; step <= 20; step++) corner.Observe(new Vector3(step * 0.1f,1.3f,2),90,2 + step * 0.1,true);
            corner.ArmEnd(true);
            corner.Finish(RoomRouteAlignment.EndQr,new Vector3(2,1.3f,2),90,4.1,true);
            WalkingRouteFollower bent = new WalkingRouteFollower();
            bent.Prepare(corner.Data);
            bent.AlignAtMarker(new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0)),new Vector3(0,1.3f,0),0,0,true);
            Check(Mathf.Abs(bent.Angle) < 1,"L-route starts along first segment, not through wall to B");
            for (int step = 1; step <= 18; step++) bent.Observe(new Vector3(0,1.3f,step * 0.1f),0,step * 0.1,true);
            Check(bent.Active && bent.Direction.Contains("PHẢI"),"L-route announces actual right turn at corner independently of camera steering");
            for (int step = 1; step <= 20; step++) bent.Observe(new Vector3(step * 0.1f,1.3f,2),90,1.8 + step * 0.1,true);
            Check(bent.Arrived,"L-route reaches recorded endpoint sequentially");
            WalkingRoute shortRoute = new WalkingRoute();
            Check(!shortRoute.Finish(RoomRouteAlignment.EndQr,Vector3.zero,0,0,true),"B before A cannot complete recording");
            shortRoute.Begin(RoomRouteAlignment.StartQr,Vector3.zero,0,0,true);
            shortRoute.ArmEnd(true);
            Check(!shortRoute.Finish(RoomRouteAlignment.StartQr,Vector3.zero,0,0.1,true),"A cannot finish recording");
            for (int step = 1; step <= 25; step++) shortRoute.Observe(Vector3.zero,0,step * 0.1,true);
            Check(!shortRoute.Finish(RoomRouteAlignment.EndQr,Vector3.zero,0,2.6,true),"Stationary route under 1m rejected");
            WalkingRouteFollower wrong = new WalkingRouteFollower();
            wrong.Prepare(data);
            Check(!wrong.AcceptQr(RoomRouteAlignment.EndQr) && wrong.WaitingQr,"Navigation rejects B instead of A");
            invalid = Fixture(); invalid.state = "recording";
            wrong.Prepare(invalid);
            Check(!wrong.AlignAtMarker(new Pose(),Vector3.zero,0,0,true),"Draft cannot start marker-aligned navigation");
            WalkingRouteFollower lost = new WalkingRouteFollower();
            lost.Prepare(data);
            Pose anchor = new Pose(new Vector3(0,1.2f,-0.6f),Quaternion.Euler(90,0,0));
            Check(!lost.AlignAtMarker(anchor,new Vector3(0,1.3f,0),0,0,false),"Unstable AR cannot auto-start");
            lost.AlignAtMarker(anchor,new Vector3(0,1.3f,0),0,0,true);
            lost.Observe(new Vector3(0,1.3f,0),0,0.1,false);
            Check(lost.Suspended && !lost.ShowArrow,"Brief tracking loss freezes guidance and hides arrows");
            lost.Observe(new Vector3(0,1.3f,0),0,0.9,false);
            Check(!lost.Active && lost.WaitingQr,"Long tracking loss returns directly to A scanner");
            XRReferenceImageLibrary library = Resources.Load<XRReferenceImageLibrary>("Routes/RoomMarkers");
            Check(library != null && library.count == 2 && Mathf.Abs(library[0].size.x - 0.18f) < 0.0001f,"Two measured 18cm reference cards included");
            foreach (string name in new[] { "Room-A","Room-B" })
            {
                Texture2D texture = Resources.Load<Texture2D>("Routes/" + name);
                Color32[] pixels = texture.GetPixels32();
                byte[] rgba = new byte[pixels.Length * 4];
                for (int index = 0; index < pixels.Length; index++)
                {
                    rgba[index * 4] = pixels[index].r;
                    rgba[index * 4 + 1] = pixels[index].g;
                    rgba[index * 4 + 2] = pixels[index].b;
                    rgba[index * 4 + 3] = pixels[index].a;
                }
                Result decoded = new BarcodeReaderGeneric { AutoRotate = true }.Decode(new RGBLuminanceSource(rgba,texture.width,texture.height,RGBLuminanceSource.BitmapFormat.RGBA32));
                Check(decoded != null && decoded.Text == (name == "Room-A" ? RoomRouteAlignment.StartQr : RoomRouteAlignment.EndQr),"QR decodes inside textured " + name);
            }
            GameObject markerRoot = new GameObject("Marker freshness QA");
            try
            {
                RoomMarkerTracker tracker = markerRoot.AddComponent<RoomMarkerTracker>();
                Check(!tracker.TryGet(RoomRouteAlignment.StartQr,out _),"No measured image means no fabricated pose");
                Type sampleType = typeof(RoomMarkerTracker).GetNestedType("Sample",BindingFlags.NonPublic);
                object sample = Activator.CreateInstance(sampleType,true);
                IDictionary samples = (IDictionary)typeof(RoomMarkerTracker).GetField("samples",BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tracker);
                sampleType.GetField("pose").SetValue(sample,anchor);
                sampleType.GetField("tracking").SetValue(sample,true);
                sampleType.GetField("updated").SetValue(sample,Time.realtimeSinceStartupAsDouble);
                sampleType.GetField("stableSince").SetValue(sample,Time.realtimeSinceStartupAsDouble - 1);
                samples.Add("Room-A",sample);
                Check(tracker.TryGet(RoomRouteAlignment.StartQr,out _),"Fresh stable marker accepted");
                sampleType.GetField("tracking").SetValue(sample,false);
                Check(!tracker.TryGet(RoomRouteAlignment.StartQr,out _),"Limited/lost image rejected");
                sampleType.GetField("tracking").SetValue(sample,true);
                sampleType.GetField("updated").SetValue(sample,Time.realtimeSinceStartupAsDouble - 1);
                Check(!tracker.TryGet(RoomRouteAlignment.StartQr,out _),"Stale marker pose rejected");
            }
            finally { UnityEngine.Object.DestroyImmediate(markerRoot); }
            File.WriteAllLines("Logs/RoomRoutes/logic.txt",Checks);
        }
    }
}
