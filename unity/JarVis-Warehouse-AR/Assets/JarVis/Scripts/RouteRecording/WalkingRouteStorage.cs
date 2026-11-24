using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public static class WalkingRouteStorage
    {
        public static string Save(string root, WalkingRouteData data)
        {
            if (data == null || !Guid.TryParseExact(data.id, "N", out _)) throw new InvalidDataException("ID tuyến không hợp lệ.");
            string folder = Path.Combine(root, "routes", data.id);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "route.json");
            string pending = path + ".pending";
            string backup = path + ".previous";
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
            using (FileStream stream = new FileStream(pending, FileMode.Create, FileAccess.Write))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path))
            {
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(path, backup);
            }
            File.Move(pending, path);
            return path;
        }

        public static WalkingRouteData LoadLast(string root)
        {
            string routes = Path.Combine(root, "routes");
            if (!Directory.Exists(routes)) return null;
            string[] folders = Directory.GetDirectories(routes);
            Array.Sort(folders, (first, second) => Directory.GetLastWriteTimeUtc(second).CompareTo(Directory.GetLastWriteTimeUtc(first)));
            foreach (string folder in folders)
                foreach (string name in new[] { "route.json", "route.json.pending", "route.json.previous" })
                {
                    string path = Path.Combine(folder, name);
                    if (!File.Exists(path)) continue;
                    try
                    {
                        FileInfo file = new FileInfo(path);
                        if (file.Length > 12000000) continue;
                        WalkingRouteData data = JsonUtility.FromJson<WalkingRouteData>(File.ReadAllText(path));
                        if (data == null || !Guid.TryParseExact(data.id, "N", out _) || !WalkingRoute.TryQr(data.startQr, out _) || data.points == null || data.points.Count < 1) continue;
                        if (data.state == "recording")
                        {
                            data.state = "interrupted";
                            data.interruption = "Phiên trước chưa kết thúc. Không thể nối lại sau khi khởi động ứng dụng.";
                        }
                        return data;
                    }
                    catch (Exception exception) when (exception is IOException || exception is ArgumentException) { }
                }
            return null;
        }

        public static string Export(string root, WalkingRouteData data)
        {
            Save(root, data);
            string name = "JarVis-route-" + data.createdUtc.Substring(0, 10) + "-" + data.id.Substring(0, 8) + "-" + DateTime.UtcNow.ToString("HHmmssfff") + ".zip";
            string path = Path.Combine(root, name);
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "route.json", JsonUtility.ToJson(data, true));
                StringBuilder csv = new StringBuilder("seconds,x_metres,y_relative_camera_metres,z_metres,yaw_degrees\n");
                foreach (RoutePoint point in data.points)
                    csv.AppendFormat(CultureInfo.InvariantCulture, "{0:F3},{1:F4},{2:F4},{3:F4},{4:F2}\n", point.seconds, point.x, point.y, point.z, point.yawDegrees);
                WriteEntry(archive, "trajectory.csv", csv.ToString());
                WriteEntry(archive, "README.txt", "JarVis Route Recorder\nTrang thai: " + data.state + "\n" + data.coordinateSystem + "\n" + data.accuracy + "\nQR start/end identify checkpoints, not their measured 3D poses. No photos, GPS or cloud map are included. A separate alignment/calibration step is needed to replay this in another AR session.\nInterrupted routes MUST NOT be used for navigation.\n");
            }
            return path;
        }

        private static void WriteEntry(ZipArchive archive, string name, string text)
        {
            using (StreamWriter writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false))) writer.Write(text);
        }

        public static string Publish(string path)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
            using (AndroidJavaClass downloads = new AndroidJavaClass("android.provider.MediaStore$Downloads"))
            using (AndroidJavaObject collection = downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"))
            using (AndroidJavaObject values = new AndroidJavaObject("android.content.ContentValues"))
            {
                values.Call("put", "_display_name", Path.GetFileName(path));
                values.Call("put", "mime_type", "application/zip");
                values.Call("put", "relative_path", "Download/JarVisRoutes");
                values.Call("put", "is_pending", new AndroidJavaObject("java.lang.Integer", 1));
                using (AndroidJavaObject uri = resolver.Call<AndroidJavaObject>("insert", collection, values))
                {
                    if (uri == null) throw new IOException("Không tạo được file Downloads.");
                    try
                    {
                        using (AndroidJavaObject output = resolver.Call<AndroidJavaObject>("openOutputStream", uri))
                        {
                            if (output == null) throw new IOException("Không mở được file Downloads.");
                            output.Call("write", File.ReadAllBytes(path));
                            output.Call("flush");
                            output.Call("close");
                        }
                        values.Call("clear");
                        values.Call("put", "is_pending", new AndroidJavaObject("java.lang.Integer", 0));
                        if (resolver.Call<int>("update", uri, values, null, null) < 1) throw new IOException("Android chưa xác nhận lưu file.");
                    }
                    catch
                    {
                        resolver.Call<int>("delete", uri, null, null);
                        throw;
                    }
                }
            }
            return "Download/JarVisRoutes/" + Path.GetFileName(path);
#else
            return path;
#endif
        }
    }
}
