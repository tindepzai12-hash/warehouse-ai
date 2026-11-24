using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabStabilityJournal stability;
        readonly Dictionary<string,double> stabilityErrors=new Dictionary<string,double>();
        double nextStability,stabilityBackoff;
        bool stabilitySubscribed,stabilityExportBusy;
        string exitInfo="{\"supported\":false,\"source\":\"editor\"}";
        public bool StabilityExportBusy=>stabilityExportBusy;
        void InitializeStability()
        {
            if(stabilitySubscribed)return;
            stability=new LabStabilityJournal(Path.Combine(Root,"stability"));
            stability.Write("SESSION_START",Version+"; "+SystemInfo.deviceModel);
            Application.lowMemory+=LowMemory;
            Application.logMessageReceivedThreaded+=StabilityLog;
            stabilitySubscribed=true;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var bridge=new AndroidJavaClass("com.jarvis.warehouse.LabRuntimeDiagnostics"))
                    exitInfo=bridge.CallStatic<string>("exits",activity);
            }
            catch(Exception e){exitInfo="{\"supported\":false,\"errorType\":\""+e.GetType().Name+"\"}";}
#endif
        }
        void StabilityLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Exception&&type!=LogType.Assert)return;
            // Condition text can contain arbitrary payloads; store only stack/method names.
            stability?.Write("UNITY_"+type,stack);
        }
        public void ReportStability(string code,Exception error)
        {
            double now=Time.realtimeSinceStartupAsDouble;
            if(stabilityErrors.TryGetValue(code,out var previous)&&now-previous<2)return;
            if(stabilityErrors.Count<64||stabilityErrors.ContainsKey(code))stabilityErrors[code]=now;
            stability?.Write(code,error?.GetType().Name??"NO_EXCEPTION_TYPE");
        }
        void TickStability(double now)
        {
            if(stability==null||now<nextStability)return;nextStability=now+2;
            stability.Write("STATE","mode="+sessionMode+"; epoch="+epoch+"; paused="+paused+"; scan="+fixedSurvey+
                "; target="+Exercise?.Target+"; tracking="+Tracking+"; pose="+NavigationPoseValid+
                "; allocatedMiB="+(Profiler.GetTotalAllocatedMemoryLong()/1048576)+
                "; ai="+cameraAi?.Diagnostic);
        }
        void LowMemory()
        {
            stability?.Write("LOW_MEMORY","AI/depth suspended for 60s; map and target retained");
            cameraAi?.ReduceMemoryLoad();ReleaseMarkerCameraReadback();
        }
        void CloseStability()
        {
            if(!stabilitySubscribed)return;
            stability?.Write("SESSION_END",Version);
            Application.lowMemory-=LowMemory;Application.logMessageReceivedThreaded-=StabilityLog;stabilitySubscribed=false;
        }
        LabNavigationJournal.Snapshot StabilitySnapshot()
        {
            if(stability==null)InitializeStability();
            var snapshot=new LabNavigationJournal.Snapshot{id="Stability-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")};
            foreach(var item in stability.Snapshot())snapshot.text[item.Key]=item.Value;
            snapshot.text["android-exits.json"]=exitInfo;
            snapshot.text["README.txt"]="Local diagnostics for JarVis "+Version+".\n"+
                "runtime*.tsv: UTC, lifecycle/state, Unity exceptions and memory-pressure events. Max 2 x 256 KiB.\n"+
                "android-exits.json: own-package exit history (Android 11+). Reason 3=low memory, 4=Java crash, 5=native crash, 6=ANR. "+
                "Reason 2=signal; it does not prove low memory. User stop/update exits are not proof of an app fault.\n"+
                "Historical exits may belong to previous versions. Correlate timestamp and SESSION_START version.\n"+
                "No camera images or account data. An empty exit list does not prove stability.\n";
            if(stability.Error!=null)snapshot.text["write-error.txt"]=stability.Error;
            return snapshot;
        }
        void ExportStabilityInto(string folder)
        {
            if(stability==null)return;
            string path=Path.Combine(folder,"stability");Directory.CreateDirectory(path);
            foreach(var item in StabilitySnapshot().text)File.WriteAllText(Path.Combine(path,item.Key),item.Value);
        }
        public async void ExportStability()
        {
            if(stabilityExportBusy)return;stabilityExportBusy=true;NavigationExportStatus="Đang xuất nhật ký lỗi…";
            try
            {
                var snapshot=StabilitySnapshot();string output=Path.Combine(Root,"diagnostic-exports");
                string zip=await Task.Run(()=>LabNavigationJournal.WriteZip(snapshot,output));
                if(this==null)return;
                string destination=LabNavigationDownloads.Publish(zip);
                NavigationExportStatus="Đã xuất nhật ký lỗi: "+destination;
            }
            catch(Exception e){if(this!=null)NavigationExportStatus="Chưa xuất được nhật ký: "+e.GetType().Name+". Thử lại khi có dung lượng trống.";}
            finally{stabilityExportBusy=false;}
        }
    }
}
