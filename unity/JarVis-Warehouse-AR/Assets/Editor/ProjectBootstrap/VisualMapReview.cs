using System;
using System.IO;
using System.IO.Compression;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class VisualMapReview
    {
        public static void Run()
        {
            int count = 0;
            void Check(bool value,string label) { if (!value) throw new InvalidOperationException(label); count++; }
            string token = new string('a',64);
            foreach (string host in new[] { "192.168.1.8","10.0.0.2","172.16.3.4","127.0.0.1" }) Check(VisualMapData.Connection("JVM1|http://" + host + ":8765|" + token,out _,out _),"Private address accepted");
            foreach (string host in new[] { "8.8.8.8","example.com","192.168.1.8/secret","192.168.1.8/?token=x","user:pass@192.168.1.8","[::1]" }) Check(!VisualMapData.Connection("JVM1|http://" + host + "|" + token,out _,out _),"Untrusted address rejected");
            Check(!VisualMapData.Connection("JVM1|http://192.168.1.2|short",out _,out _),"Token validated");
            Check(VisualMapData.Consistent(new Pose(Vector3.zero,Quaternion.identity),new Pose(new Vector3(.1f,0,0),Quaternion.Euler(0,4,0))),"Close observations agree");
            Check(!VisualMapData.Consistent(new Pose(Vector3.zero,Quaternion.identity),new Pose(new Vector3(.5f,0,0),Quaternion.identity)),"Translation jump rejected");
            Check(!VisualMapData.Consistent(new Pose(Vector3.zero,Quaternion.identity),new Pose(Vector3.zero,Quaternion.Euler(0,20,0))),"Yaw jump rejected");
            VisualMapResponse response = new VisualMapResponse { ok = true,inliers = 30,medianError = 1,position = new[] { 1f,1.3f,2f },rotation = new[] { 1f,0,0,0,1f,0,0,0,1f } };
            Check(VisualMapData.Pose(response,out Pose pose) && Vector3.Distance(pose.position,new Vector3(1,1.3f,2)) < .0001f,"Pose decoded");
            response.position[0] = float.NaN; Check(!VisualMapData.Pose(response,out _),"NaN position rejected"); response.position[0] = 1;
            response.inliers = 24; Check(!VisualMapData.Pose(response,out _),"Weak pose rejected"); response.inliers = 30;
            response.rotation[0] = -1; Check(!VisualMapData.Pose(response,out _),"Reflection rejected"); response.rotation[0] = 1;
            Pose mapCamera = new Pose(new Vector3(7,1.3f,12),Quaternion.Euler(12,48,-3));
            Pose observedCamera = new Pose(new Vector3(-2,1.4f,3),Quaternion.Euler(2,37,0) * mapCamera.rotation);
            Matrix4x4 original = Matrix4x4.TRS(observedCamera.position,observedCamera.rotation,Vector3.one) * Matrix4x4.TRS(mapCamera.position,mapCamera.rotation,Vector3.one).inverse;
            Quaternion originalYaw = Quaternion.Euler(0,original.rotation.eulerAngles.y,0);
            Vector3 originalPosition = (Vector3)original.GetColumn(3) + originalYaw * mapCamera.position;
            Check(Vector3.Distance(originalPosition,observedCamera.position) > .1f,"Regression fixture reproduces old initial displacement");
            Check(VisualMapData.Alignment(observedCamera,mapCamera,out Pose alignment),"Small gravity residual accepted");
            Check(Vector3.Distance(alignment.position + alignment.rotation * mapCamera.position,observedCamera.position) < .0001f,"Yaw projection preserves camera correspondence");
            Check(Vector3.Angle(alignment.rotation * Vector3.up,Vector3.up) < .001f,"Aligned floor stays upright");
            Vector3 mapShift = new Vector3(12,0,18);
            Pose shiftedMapCamera = new Pose(mapCamera.position + mapShift,mapCamera.rotation);
            Check(VisualMapData.Alignment(observedCamera,shiftedMapCamera,out Pose shiftedAlignment),"Shifted map origin accepted");
            Vector3 testPoint = new Vector3(8,0,14);
            Check(Vector3.Distance(alignment.position + alignment.rotation * testPoint,shiftedAlignment.position + shiftedAlignment.rotation * (testPoint + mapShift)) < .0001f,"Changing map origin cannot shift the world route");
            Pose tilted = new Pose(observedCamera.position,Quaternion.Euler(7,37,0) * mapCamera.rotation);
            Check(!VisualMapData.Alignment(tilted,mapCamera,out _),"Large gravity mismatch rejected");
            Check(!VisualMapData.DifferentView(observedCamera,observedCamera),"Repeated viewpoint cannot confirm localization");
            Check(VisualMapData.DifferentView(observedCamera,new Pose(observedCamera.position,Quaternion.AngleAxis(12,Vector3.up) * observedCamera.rotation)),"A new room view can confirm localization");
            Check(VisualMapData.DifferentView(observedCamera,new Pose(observedCamera.position + Vector3.right * .2f,observedCamera.rotation)),"A translated view can confirm localization");
            Check(!VisualMapData.Alignment(new Pose(new Vector3(float.NaN,0,0),Quaternion.identity),mapCamera,out _),"Non-finite alignment rejected");
            byte[] rows = new byte[32 * 32];
            for (int row = 0; row < 32; row++) for (int column = 0; column < 32; column++) rows[row * 32 + column] = row < 16 ? (byte)30 : (byte)220;
            Texture2D decoded = new Texture2D(2,2);
            decoded.LoadImage(VisualMapSession.Encode(rows,32,32));
            Check(decoded.GetPixel(16,30).r < .3f && decoded.GetPixel(16,1).r > .7f,"JPEG keeps CPU image top row and intrinsics orientation");
            UnityEngine.Object.DestroyImmediate(decoded);
            byte[] dark = new byte[64 * 48],lit = new byte[64 * 48];
            for (int index = 0; index < lit.Length; index++) lit[index] = index % 2 == 0 ? (byte)80 : (byte)180;
            VisualMapImageQuality darkQuality = VisualMapCoach.Measure(dark,64,48),litQuality = VisualMapCoach.Measure(lit,64,48);
            Check(darkQuality.Unusable && !litQuality.Unusable,"Only unusable frames rejected; ordinary room lighting accepted");
            Check(VisualMapCoach.Hint(true,darkQuality,0,0,default,null,0).Contains("không đủ ánh sáng"),"Measured dark scene gives actionable lighting hint");
            Check(VisualMapCoach.Hint(true,litQuality,0,80,default,null,0).Contains("nhanh"),"Measured angular speed gives slow-down hint");
            Check(VisualMapCoach.Hint(false,darkQuality,0,0,default,null,0).Contains("ảnh đã lưu vẫn còn"),"Tracking recovery preserves saved work");
            Check(!VisualMapCoach.Measure(new byte[2],64,48).Available,"Malformed image cannot produce a fake quality score");
            Check(VisualMapCoach.TrackingHint(UnityEngine.XR.ARSubsystems.NotTrackingReason.InsufficientLight).Contains("không đủ ánh sáng"),"ARCore light failure gives lighting hint even before a usable frame");
            Check(VisualMapCoach.TrackingHint(UnityEngine.XR.ARSubsystems.NotTrackingReason.ExcessiveMotion).Contains("quá nhanh"),"ARCore excessive motion gives actionable recovery");
            VisualMapCapture capture = new VisualMapCapture { name = "Phòng thử",captureId = Guid.NewGuid().ToString("N"),captureState = "complete",floorKnown = false };
            for (int index = 0; index < 3; index++)
            {
                Quaternion rotation = Quaternion.Euler(0,20,0);
                capture.frames.Add(new VisualMapFrame { name = index.ToString("D5") + ".jpg",position = new[] { index * .2f,1.3f,0 },rotation = new[] { rotation.x,rotation.y,rotation.z,rotation.w } });
            }
            Pose atOrigin = new Pose(new Vector3(0,1.3f,0),Quaternion.identity);
            Check(VisualMapCoach.Hint(true,litQuality,0,0,atOrigin,capture.frames,2).Contains("sang trái"),"Missing left viewpoint produces left hint");
            foreach (VisualMapFrame frame in capture.frames) { Quaternion rotation = Quaternion.Euler(0,-20,0); frame.rotation = new[] { rotation.x,rotation.y,rotation.z,rotation.w }; }
            Check(VisualMapCoach.Hint(true,litQuality,0,0,atOrigin,capture.frames,2).Contains("sang phải"),"Missing right viewpoint produces right hint");
            Check(!VisualMapCoach.Hint(true,litQuality,0,0,atOrigin,capture.frames,0).Contains("sang phải"),"No needless directional instruction while user already moves");
            VisualMapModel preview = new VisualMapModel { schema = 2,state = "ready",id = "room_a",points = Array.Empty<VisualMapPoint>() };
            Check(preview.Valid && !preview.CanNavigate,"Room map without route remains viewable, never navigation-ready");
            VisualMapPoint[] dense = new VisualMapPoint[101];
            for (int index = 0; index < dense.Length; index++) dense[index] = new VisualMapPoint { z = index * .05f };
            VisualMapRouteGuide guide = new VisualMapRouteGuide(dense);
            guide.Project(Vector3.zero,0);
            Check(Mathf.Abs(guide.Aim(out float straightTurn).z - 1.6f) < .001f && straightTurn == 0,"Dense straight route keeps 1.6m lookahead and never invents a turn");
            Check(guide.Project(new Vector3(0,0,1),1) < .001f && Mathf.Abs(guide.Progress - 1) < .001f,"Follower passes many dense samples in one movement");
            Check(guide.Project(new Vector3(.8f,0,1),.8f) > .79f,"Actual off-route position is not snapped to route");
            VisualMapRouteGuide corner = new VisualMapRouteGuide(new[] { new VisualMapPoint(),new VisualMapPoint { z = 3 },new VisualMapPoint { x = 3,z = 3 } });
            corner.Project(new Vector3(0,0,2.4f),2.4f); Vector3 cornerAim = corner.Aim(out float cornerTurn);
            Check(cornerTurn > 40 && cornerAim.x < .01f && cornerAim.z <= 3,"Right corner cue cannot cut across diagonal");
            VisualMapRouteGuide hairpin = new VisualMapRouteGuide(new[] { new VisualMapPoint(),new VisualMapPoint { z = 5 },new VisualMapPoint { x = .4f,z = 5 },new VisualMapPoint { x = .4f } });
            hairpin.Project(new Vector3(.4f,0,.1f),.1f);
            Check(hairpin.Progress < 1,"Hairpin close return leg cannot steal progress");
            VisualMapGeometry grid = new VisualMapGeometry { width = 2,height = 2,resolution = .1f,origin = new[] { 0f,0f },cells = new[] { 0,1,2,1 } };
            Check(grid.Valid,"Geometry contract accepted"); grid.cells[1] = 3; Check(!grid.Valid,"Unknown cell encoding rejected");
            grid.cells[1] = 1; grid.origin[0] = float.NaN; Check(!grid.Valid,"Non-finite map origin rejected");
            Vector2 projected = VisualMapFloorGraphic.Project(new Vector3(2,0,4),Vector2.zero,new Vector2(4,8),new Rect(-100,-100,200,200));
            Check(projected.magnitude < .001f,"Minimap uses map X/Z and preserves aspect");
            Vector3 cameraInMap = Quaternion.Inverse(alignment.rotation) * (observedCamera.position - alignment.position);
            Check(Vector3.Distance(cameraInMap,mapCamera.position) < .001f,"Live minimap inverts the exact AR map alignment");
            string root = Path.GetFullPath(Path.Combine("Logs/VisualMaps/checkpoints",Guid.NewGuid().ToString("N"))),folder = Path.Combine(root,"capture");
            Directory.CreateDirectory(Path.Combine(folder,"images"));
            foreach (VisualMapFrame frame in capture.frames) File.WriteAllBytes(Path.Combine(folder,"images",frame.name),new byte[] { 1,2,3 });
            File.WriteAllBytes(Path.Combine(folder,"images","orphan.jpg"),new byte[] { 1 });
            VisualMapLibrary.Checkpoint(folder,capture);
            Check(VisualMapLibrary.Scan(root).Count == 1 && !VisualMapLibrary.Scan(root)[0].Interrupted,"Completed offline capture can be reopened without floor");
            capture.captureState = "recording"; VisualMapLibrary.Checkpoint(folder,capture);
            Check(VisualMapLibrary.Scan(root)[0].Interrupted,"Interrupted app session is recoverable, not silently completed");
            using (ZipArchive archive = ZipFile.OpenRead(VisualMapData.Bundle(folder))) Check(archive.Entries.Count == 4,"Export includes only declared images, not orphan files");
            Directory.CreateDirectory("Logs/VisualMaps");
            File.WriteAllText("Logs/VisualMaps/review.txt","PASS " + count + " checks; not physical Android acceptance.");
            Debug.Log("Visual Maps review PASS " + count);
        }
    }
}
