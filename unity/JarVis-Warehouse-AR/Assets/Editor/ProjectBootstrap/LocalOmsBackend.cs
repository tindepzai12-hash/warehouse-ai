using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
namespace ProjectBootstrap
{
    // Editor-only convenience for the owner's local live backend; never runs in an APK or batch QA.
    [InitializeOnLoad] public static class LocalOmsBackend
    {
        [Serializable] sealed class Endpoint { public string baseUrl; }
        static LocalOmsBackend(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode&&!Application.isBatchMode)Ensure();};}
        static void Ensure()
        {
            try
            {
                var project=Directory.GetParent(Application.dataPath).FullName;
                var file=Path.Combine(project,"Library","JarvisBackend.local.json");
                if(!File.Exists(file))return;
                var config=JsonUtility.FromJson<Endpoint>(File.ReadAllText(file));
                if(config?.baseUrl!="http://127.0.0.1:8000")return;
                using(var client=new TcpClient())
                {
                    try{var connect=client.ConnectAsync("127.0.0.1",8000);if(connect.Wait(250)&&client.Connected)return;}catch{ }
                }
                var backend=Path.GetFullPath(Path.Combine(project,"..","..","warehouse-ai"));
                var database=Path.Combine(backend,"data","warehouse-live.sqlite3");
                if(!File.Exists(database)||!File.Exists(Path.Combine(backend,"server.js")))return;
                var node=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"nodejs","node.exe");
                if(!File.Exists(node))return;
                var start=new ProcessStartInfo(node,"\""+Path.Combine(backend,"server.js")+"\""){WorkingDirectory=backend,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                start.EnvironmentVariables["PORT"]="8000";start.EnvironmentVariables["DATABASE_PATH"]=database;
                start.EnvironmentVariables["OMS_SEED_DEMO"]="false";start.EnvironmentVariables["NODE_ENV"]="development";
                Process.Start(start);
                UnityEngine.Debug.Log("[JarVis] Started the existing local live OMS backend on port 8000.");
            }
            catch{UnityEngine.Debug.LogWarning("[JarVis] Local backend could not start. Start E:/warehouse-ai with npm start.");}
        }
    }
}
