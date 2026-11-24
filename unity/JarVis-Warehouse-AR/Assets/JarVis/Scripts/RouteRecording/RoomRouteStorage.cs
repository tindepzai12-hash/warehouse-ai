using System;
using System.IO;
using UnityEngine;

namespace JarVis.Warehouse.RouteRecording
{
    public static class RoomRouteStorage
    {
        public static bool Valid(WalkingRouteData data)
        {
            if (data == null || data.state != "complete" || data.roomAnchorId != RoomRouteAlignment.AnchorId || data.startQr != RoomRouteAlignment.StartQr || data.endQr != RoomRouteAlignment.EndQr || data.points == null || data.points.Count < 2 || data.points.Count > WalkingRoute.MaximumPoints || !Finite(data.originInMarker) || !Finite(data.headingInMarker) || !Finite(data.destinationInRoute)) return false;
            float length = 0;
            for (int index = 0; index < data.points.Count; index++)
            {
                RoutePoint point = data.points[index];
                if (point == null || !Finite(new Vector3(point.x,point.y,point.z))) return false;
                if (index > 0)
                {
                    RoutePoint previous = data.points[index - 1];
                    length += Vector2.Distance(new Vector2(point.x,point.z),new Vector2(previous.x,previous.z));
                }
            }
            return length >= 1 && length < 2000 && data.originInMarker.magnitude < 5;
        }

        public static WalkingRouteData Load(string root)
        {
            foreach (string name in new[] { "current.json","previous.json" })
            {
                string path = Path.Combine(root,"room-map",name);
                try
                {
                    if (!File.Exists(path) || new FileInfo(path).Length > 12000000) continue;
                    WalkingRouteData data = JsonUtility.FromJson<WalkingRouteData>(File.ReadAllText(path));
                    if (Valid(data)) return data;
                }
                catch (Exception exception) when (exception is IOException || exception is ArgumentException) { }
            }
            return null;
        }

        public static void Commit(string root,WalkingRouteData data)
        {
            if (!Valid(data)) throw new InvalidDataException("Tuyến A → B chưa hoàn chỉnh hoặc thiếu mốc định vị.");
            string directory = Path.Combine(root,"room-map");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory,"current.json");
            string pending = Path.Combine(directory,"pending.json");
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
            using (FileStream stream = new FileStream(pending,FileMode.Create,FileAccess.Write))
            {
                stream.Write(bytes,0,bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(target)) File.Replace(pending,target,Path.Combine(directory,"previous.json"));
            else File.Move(pending,target);
        }

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
