using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JarVis.Warehouse.VisualMaps;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabNavigationJournal journal;
        bool journalAvailable,journalWriteFailed;
        double nextJournalRetry;
        int journalRouteRevision=-1;
        string unpublishedNavigationZip,unpublishedNavigationId;int unpublishedSampleCount;
        public bool NavigationExportBusy {get;private set;}
        public string NavigationExportStatus {get;private set;}
        public bool HasNavigationLog=>journalAvailable;
        public bool NavigationRecordingFailed=>journalWriteFailed;
        public string NavigationLogSummary
        {
            get
            {
                if(journalWriteFailed)return "Nhật ký chưa ghi được. Kiểm tra dung lượng rồi thử xuất lại; dữ liệu đã lưu vẫn còn.";
                if(journal?.Current==null)return HasNavigationLog?"Có lượt chỉ đường đã lưu từ lần trước. Bạn có thể xuất ngay.":"Chọn đích để bắt đầu. App tự lưu lượt chỉ đường, kể cả lúc mất tracking.";
                var m=journal.Current;int seconds=(int)m.durationSeconds;
                return (journal.Active?"Đang lưu lượt":"Đã lưu lượt")+" · "+(seconds/60)+":"+(seconds%60).ToString("D2")+" · "+m.samples+" mẫu\nChỉ lưu tọa độ/trạng thái, không quay camera.";
            }
        }
        void InitializeNavigationJournal()
        {
            if(journal!=null)return;
            journal=new LabNavigationJournal(Path.Combine(Root,"navigation-runs"));
            try{journalAvailable=journal.LatestFolder()!=null;}catch(Exception e){NavigationExportStatus="Chưa đọc được nhật ký: "+e.GetType().Name;}
        }
        Dictionary<string,string> NavigationContext()
        {
            var data=new Dictionary<string,string>();
            if(stability!=null)foreach(var item in StabilitySnapshot().text)data["stability-"+item.Key]=item.Value;
            if(Map!=null)data["map.json"]=JsonUtility.ToJson(Map,true);
            if(Floor!=null)data["floor-plan.json"]=JsonUtility.ToJson(Floor,true);
            if(profile!=null)data["calibration-profile.json"]=JsonUtility.ToJson(profile,true);
            if(corridorGraph!=null)data["corridors.json"]=JsonUtility.ToJson(corridorGraph,true);
            var knowledgeResource=Resources.Load<TextAsset>("LabTest/LabMapKnowledge");if(knowledgeResource!=null)data["map-knowledge.json"]=knowledgeResource.text;
            data["navigation-state.json"]=JsonUtility.ToJson(new JournalContext{target=Exercise?.Target,mapHash=Floor?.geometrySha256,graphHash=corridorGraph?.graphHash,route=Route?.ToArray(),routeId=activeRoute?.id,trial=TrialNavigation,mode=sessionMode,epoch=epoch,routeRevision=routeRevision},true);
            return data;
        }
        [Serializable] sealed class JournalContext
        {
            public string target,mapHash,graphHash,routeId,mode;public LabPoint[] route;
            public bool trial,physicalAccuracyVerified=false;public int epoch,routeRevision;
        }
        void BeginNavigationJournal()
        {
            InitializeNavigationJournal();
            try{journal.Begin(Version,Time.realtimeSinceStartupAsDouble,NavigationContext());journalRouteRevision=-1;journalAvailable=true;journalWriteFailed=false;NavigationExportStatus=null;}
            catch(Exception e){JournalFailure(e);}
        }
        void EndNavigationJournal(string reason)
        {
            if(journal==null)return;
            try{journal.End(reason,Time.realtimeSinceStartupAsDouble);journalWriteFailed=false;}
            catch(Exception e){JournalFailure(e);}
        }
        void PauseNavigationJournal(bool value)
        {
            if(journal==null)return;
            try{journal.Pause(value,Time.realtimeSinceStartupAsDouble);journalWriteFailed=false;}
            catch(Exception e){JournalFailure(e);}
        }
        void JournalFailure(Exception e)
        {
            journalWriteFailed=true;nextJournalRetry=Time.realtimeSinceStartupAsDouble+2;
            NavigationExportStatus="Chưa lưu/xuất được: "+e.GetType().Name+". Kiểm tra dung lượng điện thoại rồi thử lại.";
        }
        void EnrichTrace(NavigationTrace trace)
        {
            trace.phase=EvidencePhase;trace.alignmentMethod=AlignmentMethod;trace.alignmentMarkerResidual=AlignmentMarkerResidual;
            trace.depthCameraDelta=cameraAi?.DepthCameraDelta??-1;trace.projectedDepthPoints=cameraAi?.ProjectedDepthPoints??0;
            if(float.IsNaN(trace.turnDistance)||float.IsInfinity(trace.turnDistance))trace.turnDistance=-1;
            if(double.IsNaN(trace.lastKnownAge)||double.IsInfinity(trace.lastKnownAge))trace.lastKnownAge=-1;
            if(double.IsNaN(trace.cameraFrameAge)||double.IsInfinity(trace.cameraFrameAge))trace.cameraFrameAge=-1;
            if(double.IsNaN(trace.recoveryDeadline)||double.IsInfinity(trace.recoveryDeadline))trace.recoveryDeadline=-1;
            trace.navigationPoseValid=NavigationPoseValid;trace.remainingKnown=RemainingKnown;
            trace.mapKnowledgeId=MapKnowledgeId;trace.alignmentMode=TrialAlignmentStatus;trace.depthStatus=cameraAi?.DepthStatus;trace.obstacleStatus=ObstacleStatus;trace.obstacleCells=ObstacleCount;trace.groundFrames=GroundObservationFrames;trace.cameraBlockedEdges=cameraBlocks;trace.manualBlockedEdges=ManualBlockedEdges;
            trace.ai=cameraAi?.Diagnostic;trace.presentationReason=presentation.reason;trace.cameraGuidanceOccluded=CameraGuidanceOccluded;
            trace.routeSource=RouteSource;trace.localPlannerStatus=LocalPlannerStatus;trace.frontFloorCoverage=FrontFloorCoverage;
            trace.routePoints=activeRoute?.points; // Test bundle includes the active route even if the journal just started.
        }
        void RecordJournal(NavigationTrace trace,double now)
        {
            if(journal?.Active!=true)return;
            if(journalWriteFailed)
            {
                if(now<nextJournalRetry)return;
                try{journal.Flush();journalWriteFailed=false;}catch(Exception e){JournalFailure(e);return;}
            }
            trace.navigationPoseValid=NavigationPoseValid;trace.remainingKnown=RemainingKnown;
            trace.mapKnowledgeId=MapKnowledgeId;trace.alignmentMode=TrialAlignmentStatus;trace.depthStatus=cameraAi?.DepthStatus;trace.obstacleStatus=ObstacleStatus;trace.obstacleCells=ObstacleCount;trace.groundFrames=GroundObservationFrames;trace.cameraBlockedEdges=cameraBlocks;trace.manualBlockedEdges=ManualBlockedEdges;
            trace.ai=cameraAi?.Diagnostic;trace.presentationReason=presentation.reason;trace.cameraGuidanceOccluded=CameraGuidanceOccluded;
            trace.destinationChanged=Exercise?.Target;
            if(journalRouteRevision!=routeRevision){trace.routePoints=activeRoute?.points?.ToArray();journalRouteRevision=routeRevision;}
            if(float.IsInfinity(trace.turnDistance)||float.IsNaN(trace.turnDistance))trace.turnDistance=-1;
            if(double.IsInfinity(trace.lastKnownAge)||double.IsNaN(trace.lastKnownAge))trace.lastKnownAge=-1;
            if(double.IsInfinity(trace.cameraFrameAge)||double.IsNaN(trace.cameraFrameAge))trace.cameraFrameAge=-1;
            string row=string.Join(",",trace.utc,N(now),trace.target,trace.tracking?"1":"0",trace.located?"1":"0",trace.navigationPoseValid?"1":"0",N(trace.rawPosition?.x),N(trace.rawPosition?.z),N(trace.navigationPoseValid?trace.acceptedPosition?.x:null),N(trace.navigationPoseValid?trace.acceptedPosition?.z:null),N(trace.displayPosition?.x),N(trace.displayPosition?.z),trace.located?N(trace.heading):"",trace.remainingKnown?N(trace.remaining):"",trace.routeId,Csv(trace.poseReason))+"\n";
            try{journal.Append(JsonUtility.ToJson(trace),row,now,trace.navigationPoseValid,trace.tracking);}
            catch(Exception e){JournalFailure(e);}
        }
        static string N(double? value)=>value.HasValue&&!double.IsNaN(value.Value)&&!double.IsInfinity(value.Value)?value.Value.ToString("0.######",CultureInfo.InvariantCulture):"";
        static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
        public async void ExportNavigationRun()
        {
            if(NavigationExportBusy)return;
            InitializeNavigationJournal();NavigationExportBusy=true;NavigationExportStatus="Đang đóng gói lượt chỉ đường…";
            try
            {
                string run=Path.GetFileName(journal.LatestFolder());
                bool retryCopy=!journal.Active&&string.IsNullOrEmpty(journal.Error)&&run==unpublishedNavigationId&&(journal.Current==null||journal.Current.samples==unpublishedSampleCount)&&File.Exists(unpublishedNavigationZip);
                string file=unpublishedNavigationZip;
                if(!retryCopy)
                {
                    var snapshot=journal.Capture(journal.Active?NavigationContext():new Dictionary<string,string>());
                    int sampleCount=journal.Current?.samples??0;
                    journalWriteFailed=false;
                    string output=Path.Combine(Root,"navigation-exports");
                    file=await Task.Run(()=>LabNavigationJournal.WriteZip(snapshot,output));
                    if(this==null)return;
                    unpublishedNavigationZip=file;unpublishedNavigationId=snapshot.id;unpublishedSampleCount=sampleCount;
                }
                if(this==null)return;
                // A completed internal ZIP is retained if Android refuses the public copy.
                ExportedPath=file;
                try
                {
                    ExportedPath=LabNavigationDownloads.Publish(file);unpublishedNavigationZip=null;unpublishedNavigationId=null;
                    NavigationExportStatus="Đã xuất ZIP.\n"+ExportedPath+"\nMở Tệp của bạn → Tải về → JarVisMaps để gửi file.";
                }
                catch(Exception e){NavigationExportStatus="ZIP đã lưu nội bộ, nhưng chưa chép được vào Tải về ("+e.GetType().Name+"). Bấm xuất lại để thử; không cần đi lại tuyến.";}
            }
            catch(Exception e)
            {
                if(!string.IsNullOrEmpty(journal.Error))JournalFailure(e);
                else NavigationExportStatus="Chưa xuất được ZIP ("+e.GetType().Name+"). Nhật ký đã lưu vẫn còn; thử xuất lại, không cần đi lại tuyến.";
            }
            finally{if(this!=null)NavigationExportBusy=false;}
        }
    }
    public static class LabNavigationDownloads
    {
        public static string Publish(string source)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var resolver=activity.Call<AndroidJavaObject>("getContentResolver"))
            using(var downloads=new AndroidJavaClass("android.provider.MediaStore$Downloads"))
            using(var values=new AndroidJavaObject("android.content.ContentValues"))
            {
                values.Call("put","_display_name",Path.GetFileName(source));values.Call("put","mime_type","application/zip");values.Call("put","relative_path","Download/JarVisMaps");
                using(var pending=new AndroidJavaObject("java.lang.Integer",1))values.Call("put","is_pending",pending);
                using(var uri=resolver.Call<AndroidJavaObject>("insert",downloads.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI"),values))
                {
                    if(uri==null)throw new IOException("Không tạo được file Tải về.");
                    try
                    {
                        using(var output=resolver.Call<AndroidJavaObject>("openOutputStream",uri))
                        using(var input=File.OpenRead(source))
                        {
                            if(output==null)throw new IOException("Không mở được file Tải về.");
                            byte[] buffer=new byte[65536];int count;while((count=input.Read(buffer,0,buffer.Length))>0)output.Call("write",buffer,0,count);
                            output.Call("flush");output.Call("close");
                        }
                        using(var descriptor=resolver.Call<AndroidJavaObject>("openFileDescriptor",uri,"r"))
                        {
                            if(descriptor==null||descriptor.Call<long>("getStatSize")!=new FileInfo(source).Length)throw new IOException("File Tải về chưa đủ dung lượng.");
                            descriptor.Call("close");
                        }
                        values.Call("clear");using(var done=new AndroidJavaObject("java.lang.Integer",0))values.Call("put","is_pending",done);
                        if(resolver.Call<int>("update",uri,values,null,null)!=1)throw new IOException("Chưa công bố file Tải về.");
                    }
                    catch{resolver.Call<int>("delete",uri,null,null);throw;}
                }
            }
            return "Download/JarVisMaps/"+Path.GetFileName(source);
#else
            return source;
#endif
        }
    }
}
