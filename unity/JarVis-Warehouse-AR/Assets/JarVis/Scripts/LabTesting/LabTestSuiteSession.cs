using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        LabTestSuite testSuite;double nextSuiteHeartbeat,nextSuiteDepth,nextSuiteSemantic;
        string suiteError,suiteExportPath;
        public bool SuiteRunning=>testSuite?.Active!=null;
        public int SuiteCaseNumber=>(testSuite?.Manifest.selected??0)+1;
        public bool SuiteExportBusy {get;private set;}
        public string SuiteUiKey=>(SuiteRunning?"RUN":"READY")+"|"+(testSuite?.Manifest.selected??0)+"|"+SuiteExportBusy+"|"+testSuite?.Manifest.attempts.Count;
        public string SuiteBadge=>testSuite==null?"Chưa mở được bộ test":"Bài "+(testSuite.Manifest.selected+1)+"/"+LabTestSuite.Missions.Length+" · "+testSuite.Selected.title;
        public string SuiteInstructions
        {
            get
            {
                if(testSuite==null)return suiteError??"Chưa mở được dữ liệu. Kiểm tra dung lượng.";
                var m=testSuite.Selected;
                return m.start+" → "+m.target+" · khoảng "+m.seconds+" giây\n"+m.instructions+"\n"+
                    (SuiteRunning?"Đang ghi. Kết thúc rồi tự đánh giá bên dưới.":"Bắt đầu mới ghi. Làm lại sẽ giữ bản trước.")+"\n"+(suiteError??testSuite.Error??suiteExportPath??"");
            }
        }
        void InitializeTestSuite()
        {
            if(testSuite!=null)return;
            try{testSuite=new LabTestSuite(Path.Combine(Root,"guided-tests"),Version,SystemInfo.deviceModel);}
            catch(Exception e){suiteError="Chưa mở được bộ test: "+e.GetType().Name;}
        }
        public bool BeginSuiteCase()
        {
            InitializeTestSuite();if(testSuite==null||SuiteRunning||!FixedReady)return false;
            try
            {
                if(!testSuite.Begin(Time.realtimeSinceStartupAsDouble,NavigationContext())){suiteError="Bộ test đã đạt giới hạn 50 MB. Xuất ZIP trước.";return false;}
                suiteError=suiteExportPath=null;nextSuiteHeartbeat=nextSuiteDepth=nextSuiteSemantic=0;
                StartFixedDestination(testSuite.Selected.target);return true;
            }
            catch(Exception e){suiteError="Chưa bắt đầu được: "+e.GetType().Name;return false;}
        }
        public void FinishSuiteCase(string verdict)
        {
            try{testSuite?.Finish(verdict,Time.realtimeSinceStartupAsDouble);suiteError=null;}
            catch(Exception e){suiteError="Chưa đóng được bài: "+e.GetType().Name;}
        }
        public void SuspendSuiteCase(){if(SuiteRunning)FinishSuiteCase("STOPPED");}
        public void NextSuiteCase(){if(SuiteRunning)return;try{testSuite?.SkipSelected(Time.realtimeSinceStartupAsDouble);}catch(Exception e){suiteError=e.GetType().Name;}}
        public void PreviousSuiteCase(){if(SuiteRunning)return;try{testSuite?.Select(-1);}catch(Exception e){suiteError=e.GetType().Name;}}
        void TickTestSuite(double now)
        {
            if(!SuiteRunning||now<nextSuiteHeartbeat)return;nextSuiteHeartbeat=now+.2;
            var active=testSuite.Active;active.heartbeats++;if(NavigationPoseValid)active.validPoses++;if(!Tracking)active.trackingLost++;
            testSuite.Append("heartbeat",JsonUtility.ToJson(new SuiteHeartbeat{phase=EvidencePhase,utc=DateTime.UtcNow.ToString("O"),seconds=now,target=Exercise?.Target,tracking=Tracking,located=Located,poseValid=NavigationPoseValid,poseReason=PoseReason,position=Position,raw=RawPosition,heading=Heading,epoch=epoch,alignment=TrialAlignmentStatus,depth=cameraAi?.DepthStatus,obstacles=ObstacleCount,frontCoverage=FrontFloorCoverage,route=RouteId,routeSource=RouteSource,presentation=presentation.reason,planner=LocalPlannerStatus,frameSeconds=Time.unscaledDeltaTime}),now);
        }
        [Serializable] sealed class SuiteHeartbeat
        {public string phase,utc,target,poseReason,alignment,depth,route,routeSource,presentation,planner;public double seconds;public bool tracking,located,poseValid;public int epoch,obstacles;public LabPoint position,raw;public float heading,frontCoverage,frameSeconds;}
        public void RecordSuiteSemantic(string json,double now)
        {
            if(!SuiteRunning||now<nextSuiteSemantic)return;nextSuiteSemantic=now+1;
            testSuite.Append("objects","{\"utc\":\""+DateTime.UtcNow.ToString("O")+"\",\"seconds\":"+now.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"result\":"+json+"}",now);
        }
        void RecordSuiteTrace(NavigationTrace trace)
        {if(!SuiteRunning)return;testSuite.Active.traceFrames++;testSuite.Append("navigation",JsonUtility.ToJson(trace),trace.seconds);}
        public void RecordSuiteDepth(double now,double timestamp,int width,int height,LabDepthSample[] samples,int count,Camera camera,ARCameraManager manager,LabDepthCameraFrame frame)
        {
            if(!SuiteRunning||now<nextSuiteDepth)return;nextSuiteDepth=now+1;
            int step=Math.Max(1,(int)Math.Ceiling(count/96.0));var selected=Enumerable.Range(0,count).Where(i=>i%step==0).Select(i=>samples[i]).ToArray();
            float[] intrinsics=null;
            if(manager.TryGetIntrinsics(out var intrinsic))intrinsics=new[]{intrinsic.focalLength.x,intrinsic.focalLength.y,intrinsic.principalPoint.x,intrinsic.principalPoint.y,(float)intrinsic.resolution.x,(float)intrinsic.resolution.y};
            var projection=frame.projection;var display=frame.display;
            testSuite.Active.depthFrames++;
            testSuite.Append("depth",JsonUtility.ToJson(new SuiteDepth{utc=DateTime.UtcNow.ToString("O"),seconds=now,imageTimestamp=timestamp,cameraTimestamp=frame.timestamp,cameraFrameReceived=frame.received,poseSource="CAMERA_CALLBACK_ESTIMATE_WITH_MOTION_GUARD",width=width,height=height,epoch=epoch,points=selected,intrinsics=intrinsics,projection=MatrixValues(projection),displayTransform=MatrixValues(display),cameraWorld=ToPoint(frame.position),cameraRotation=Q(frame.rotation),mapOriginWorld=ToPoint(MapFrame.position),mapRotation=Q(MapFrame.rotation),status=cameraAi?.DepthStatus,knowledge=MapKnowledgeId}),now);
        }
        static float[] MatrixValues(Matrix4x4 m){var values=new float[16];for(int i=0;i<16;i++)values[i]=m[i];return values;}
        [Serializable] sealed class SuiteDepth
        {public string utc,status,knowledge,poseSource;public double seconds,imageTimestamp,cameraTimestamp,cameraFrameReceived;public int width,height,epoch;public LabDepthSample[] points;public float[] intrinsics,projection,displayTransform,cameraRotation,mapRotation;public LabPoint cameraWorld,mapOriginWorld;}
        void PauseTestSuite(bool pausedValue)
        {
            if(!SuiteRunning)return;
            try{testSuite.Append("events",JsonUtility.ToJson(new SuiteEvent{utc=DateTime.UtcNow.ToString("O"),seconds=Time.realtimeSinceStartupAsDouble,eventName=pausedValue?"APP_PAUSE":"APP_RESUME"}),Time.realtimeSinceStartupAsDouble);testSuite.Flush();}
            catch(Exception e){suiteError="Lưu tạm lỗi: "+e.GetType().Name;}
        }
        [Serializable] sealed class SuiteEvent {public string utc,eventName;public double seconds;}
        void CloseTestSuite(){try{testSuite?.Dispose();}catch(Exception){}testSuite=null;}
        public async void ExportTestSuite()
        {
            if(SuiteExportBusy||testSuite==null)return;SuiteExportBusy=true;suiteError="Đang tạo một ZIP toàn bộ bài test…";
            try
            {
                var snapshot=testSuite.Capture();var output=Path.Combine(Root,"guided-test-exports");
                string file=await Task.Run(()=>LabNavigationJournal.WriteZip(snapshot,output));if(this==null)return;
                suiteExportPath=file;
                try{suiteExportPath=LabNavigationDownloads.Publish(file);suiteError="Đã xuất ZIP. Mở Tệp của bạn → Tải về → JarVisMaps để gửi.";}
                catch(Exception e){suiteError="Đã giữ ZIP nội bộ. Chép ra Tải về lỗi "+e.GetType().Name+"; bấm Xuất lại, không cần đi lại.";}
            }
            catch(Exception e){suiteError="Xuất lỗi "+e.GetType().Name+". Dữ liệu bài test vẫn được giữ.";}
            finally{if(this!=null)SuiteExportBusy=false;}
        }
    }
}
