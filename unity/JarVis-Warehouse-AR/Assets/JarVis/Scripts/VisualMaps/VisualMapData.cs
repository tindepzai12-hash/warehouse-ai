using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using UnityEngine;

namespace JarVis.Warehouse.VisualMaps
{
    [Serializable]
    public sealed class VisualMapFrame
    {
        public string name;
        public int width,height;
        public float fx,fy,cx,cy;
        public double time,cameraTime;
        public float[] position,rotation;
        public bool onRoute,start,end;
        public VisualMapDepth depth;
    }

    [Serializable]
    public sealed class VisualMapDepth
    {
        public string file,confidence,format = "float32-metres";
        public int width,height;
        public float fx,fy,cx,cy;
        public double time;
    }

    [Serializable]
    public sealed class VisualMapCapture
    {
        public int schema = 2;
        public string captureId,name,purpose = "environment",parentId = "";
        public bool interrupted,floorKnown;
        public string captureState = "recording";
        public string coordinateSystem = "Unity metres, Y up";
        public string device;
        public float floorY;
        public List<VisualMapFrame> frames = new List<VisualMapFrame>();
    }

    [Serializable]
    public sealed class VisualMapPoint
    {
        public float x,y,z;
        public Vector3 Position => new Vector3(x,y,z);
    }

    [Serializable]
    public sealed class VisualMapModel
    {
        public int schema,registered,total;
        public string id,state,name,geometryState,objectState,parentId;
        public int revision,objectCount,routePoints;
        public bool navigationAllowed;
        public float floorY,rmse;
        public VisualMapPoint[] points;
        public bool Valid => (schema == 1 || schema == 2) && state == "ready" && !string.IsNullOrEmpty(id) && points != null && points.Length <= 600 && Finite(floorY) && Finite(rmse) && rmse <= 0.3f;
        public bool CanNavigate => Valid && navigationAllowed && points.Length >= 3;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    public sealed class VisualMapList { public VisualMapModel[] maps; }

    [Serializable]
    public sealed class VisualMapGeometry
    {
        public string state,reason;
        public int width,height,depthFrames;
        public float resolution;
        public float[] origin;
        public int[] cells;
        public bool Valid
        {
            get
            {
                if (width < 1 || height < 1 || width > 512 || height > 512 || cells == null || cells.Length != width * height || origin == null || origin.Length != 2 || !VisualMapModel.Finite(origin[0]) || !VisualMapModel.Finite(origin[1]) || !VisualMapModel.Finite(resolution) || resolution < .05f || resolution > .5f) return false;
                foreach (int cell in cells) if (cell < 0 || cell > 2) return false;
                return true;
            }
        }
    }

    [Serializable]
    public sealed class VisualObjectDetection { public string label; public float confidence; public float[] box; }

    [Serializable]
    public sealed class VisualObjectResult { public string requestId,model; public VisualObjectDetection[] detections; }

    [Serializable]
    public sealed class VisualMapResponse
    {
        public bool ok,hasMap;
        public string mapId,requestId,message,error,state,stage,id;
        public int inliers,step,registered,total;
        public float medianError,rmse;
        public float[] position,rotation;
    }

    [Serializable]
    public sealed class VisualMapQuery
    {
        public string mapId,requestId,image,device;
        public float fx,fy,cx,cy;
    }

    public static class VisualMapData
    {
        public const string Version = "0.2.1";

        public static bool Connection(string text,out string url,out string token)
        {
            url = token = null;
            if (string.IsNullOrEmpty(text) || text.Length > 300) return false;
            string[] parts = text.Split('|');
            if (parts.Length != 3 || parts[0] != "JVM1" || parts[2].Length != 64 || !Uri.TryCreate(parts[1],UriKind.Absolute,out Uri address)) return false;
            if (address.Scheme != "http" || address.AbsolutePath != "/" || address.Query.Length > 0 || address.Fragment.Length > 0 || address.UserInfo.Length > 0 || !IPAddress.TryParse(address.Host,out IPAddress host)) return false;
            byte[] bytes = host.GetAddressBytes();
            if (bytes.Length != 4 || !(bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)) return false;
            foreach (char character in parts[2]) if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f')) return false;
            url = address.GetLeftPart(UriPartial.Authority);
            token = parts[2];
            return true;
        }

        public static bool Pose(VisualMapResponse response,out Pose pose)
        {
            pose = default;
            if (response == null || !response.ok || response.inliers < 25 || !VisualMapModel.Finite(response.medianError) || response.medianError > 2.5f || response.position == null || response.position.Length != 3 || response.rotation == null || response.rotation.Length != 9) return false;
            foreach (float value in response.position) if (!VisualMapModel.Finite(value)) return false;
            foreach (float value in response.rotation) if (!VisualMapModel.Finite(value)) return false;
            Vector3 right = new Vector3(response.rotation[0],response.rotation[3],response.rotation[6]);
            Vector3 up = new Vector3(response.rotation[1],response.rotation[4],response.rotation[7]);
            Vector3 forward = new Vector3(response.rotation[2],response.rotation[5],response.rotation[8]);
            if (Mathf.Abs(right.magnitude - 1) > 0.02f || Mathf.Abs(up.magnitude - 1) > 0.02f || Mathf.Abs(forward.magnitude - 1) > 0.02f || Mathf.Abs(Vector3.Dot(up,forward)) > 0.02f || Vector3.Dot(Vector3.Cross(right,up),forward) < 0.98f) return false;
            pose = new Pose(new Vector3(response.position[0],response.position[1],response.position[2]),Quaternion.LookRotation(forward,up));
            return true;
        }

        public static bool Consistent(Pose first,Pose second) => Vector3.Distance(first.position,second.position) <= 0.25f && Quaternion.Angle(first.rotation,second.rotation) <= 8;

        public static bool Alignment(Pose observedCamera,Pose mapCamera,out Pose alignment)
        {
            alignment = default;
            if (!FinitePose(observedCamera) || !FinitePose(mapCamera)) return false;
            Quaternion rotation = observedCamera.rotation * Quaternion.Inverse(mapCamera.rotation);
            if (Vector3.Angle(rotation * Vector3.up,Vector3.up) > 3) return false;
            Vector3 forward = Vector3.ProjectOnPlane(rotation * Vector3.forward,Vector3.up);
            if (forward.sqrMagnitude < .0001f) return false;
            Quaternion yaw = Quaternion.LookRotation(forward,Vector3.up);
            alignment = new Pose(observedCamera.position - yaw * mapCamera.position,yaw);
            return true;
        }

        public static bool DifferentView(Pose first,Pose second) => FinitePose(first) && FinitePose(second) && (Vector3.Distance(first.position,second.position) >= .15f || Quaternion.Angle(first.rotation,second.rotation) >= 8);

        private static bool FinitePose(Pose pose)
        {
            Vector3 position = pose.position;
            Quaternion rotation = pose.rotation;
            if (!VisualMapModel.Finite(position.x) || !VisualMapModel.Finite(position.y) || !VisualMapModel.Finite(position.z) || !VisualMapModel.Finite(rotation.x) || !VisualMapModel.Finite(rotation.y) || !VisualMapModel.Finite(rotation.z) || !VisualMapModel.Finite(rotation.w)) return false;
            return Mathf.Abs(Quaternion.Dot(rotation,rotation) - 1) < .02f;
        }

        public static string Bundle(string folder)
        {
            string target = folder + ".zip";
            string pending = target + ".pending";
            using (FileStream stream = File.Create(pending))
            using (ZipArchive archive = new ZipArchive(stream,ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(Path.Combine(folder,"manifest.json"),"manifest.json",System.IO.Compression.CompressionLevel.Fastest);
                VisualMapCapture capture = JsonUtility.FromJson<VisualMapCapture>(File.ReadAllText(Path.Combine(folder,"manifest.json")));
                foreach (VisualMapFrame frame in capture.frames)
                {
                    if (string.IsNullOrEmpty(frame.name) || Path.GetFileName(frame.name) != frame.name) throw new InvalidDataException("Invalid image name");
                    archive.CreateEntryFromFile(Path.Combine(folder,"images",frame.name),"images/" + frame.name,System.IO.Compression.CompressionLevel.NoCompression);
                    if (frame.depth == null || string.IsNullOrEmpty(frame.depth.file)) continue;
                    if (frame.depth.file != frame.name + ".f32" || frame.depth.confidence != frame.name + ".u8") throw new InvalidDataException("Invalid depth name");
                    foreach (string name in new[] { frame.depth.file,frame.depth.confidence }) archive.CreateEntryFromFile(Path.Combine(folder,"depth",name),"depth/" + name,System.IO.Compression.CompressionLevel.Fastest);
                }
            }
            if (File.Exists(target)) File.Delete(target);
            File.Move(pending,target);
            return target;
        }

        public static string Export(string source)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (AndroidJavaClass downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
            using (AndroidJavaObject values = new AndroidJavaObject("android.content.ContentValues"))
            using (AndroidJavaObject pending = new AndroidJavaObject("java.lang.Integer",1))
            {
                values.Call("put","_display_name",Path.GetFileName(source));
                values.Call("put","mime_type","application/zip");
                values.Call("put","relative_path","Download/JarVisMaps");
                values.Call("put","is_pending",pending);
                using (AndroidJavaObject uri = resolver.Call<AndroidJavaObject>("insert",downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"),values))
                {
                    if (uri == null) throw new IOException("Không tạo được ZIP trong Download.");
                    using (AndroidJavaObject output = resolver.Call<AndroidJavaObject>("openOutputStream",uri))
                    using (FileStream input = File.OpenRead(source))
                    {
                        byte[] buffer = new byte[65536];
                        int count;
                        while ((count = input.Read(buffer,0,buffer.Length)) > 0) output.Call("write",buffer,0,count);
                        output.Call("flush");
                        output.Call("close");
                    }
                    values.Call("clear");
                    using (AndroidJavaObject complete = new AndroidJavaObject("java.lang.Integer",0)) values.Call("put","is_pending",complete);
                    resolver.Call<int>("update",uri,values,null,null);
                }
            }
            return "Download/JarVisMaps/" + Path.GetFileName(source);
#else
            return source;
#endif
        }
    }
}
