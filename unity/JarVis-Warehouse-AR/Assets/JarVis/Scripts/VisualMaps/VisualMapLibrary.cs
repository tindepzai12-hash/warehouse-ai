using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace JarVis.Warehouse.VisualMaps
{
    public sealed class VisualMapLocalEntry
    {
        public string Folder,Name,Id;
        public int Photos,DepthFrames;
        public bool Interrupted;
    }

    public static class VisualMapLibrary
    {
        public static void Checkpoint(string folder,VisualMapCapture capture)
        {
            string path = Path.Combine(folder,"manifest.json"),pending = path + ".pending";
            File.WriteAllText(pending,JsonUtility.ToJson(capture),new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(pending,path,null);
            else File.Move(pending,path);
        }

        public static List<VisualMapLocalEntry> Scan(string root)
        {
            List<VisualMapLocalEntry> result = new List<VisualMapLocalEntry>();
            if (!Directory.Exists(root)) return result;
            foreach (string folder in Directory.GetDirectories(root))
            {
                try
                {
                    string path = Path.Combine(folder,"manifest.json");
                    if (!File.Exists(path) || new FileInfo(path).Length > 2 * 1024 * 1024) continue;
                    VisualMapCapture capture = JsonUtility.FromJson<VisualMapCapture>(File.ReadAllText(path));
                    if (capture == null || capture.frames == null || capture.frames.Count == 0) continue;
                    int depth = 0;
                    foreach (VisualMapFrame frame in capture.frames) if (frame.depth != null && !string.IsNullOrEmpty(frame.depth.file)) depth++;
                    result.Add(new VisualMapLocalEntry { Folder = folder,Id = capture.captureId,Name = string.IsNullOrEmpty(capture.name) ? Path.GetFileName(folder) : capture.name,Photos = capture.frames.Count,DepthFrames = depth,Interrupted = capture.interrupted || capture.captureState == "recording" });
                }
                catch (Exception) { }
            }
            result.Sort((first,second) => string.CompareOrdinal(second.Folder,first.Folder));
            return result;
        }
    }
}
