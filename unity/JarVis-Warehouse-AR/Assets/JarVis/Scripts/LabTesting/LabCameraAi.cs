using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Lab
{
    // Semantics annotate observations. Metric depth may temporarily close known edges; never moves localization or static floor.
    public sealed class LabCameraAi : MonoBehaviour
    {
        [Serializable] sealed class Item {public string label;public float score;}
        [Serializable] sealed class Result {public long frame;public float ms;public Item[] items;}
        LabTestSession owner;ARCameraManager cameraManager;AROcclusionManager depth;
        AndroidJavaObject detector;Text label;RectTransform panel;
        bool converting,paused,disposed,wasActive;float inferenceInterval=.35f;long sequence,generation;double next,submittedAt,lastResultAt=-1,lastDepthAt=-1;
        XRCpuImage.AsyncConversion conversion;
        Vector2Int conversionSize;byte[] rgbaBuffer;
        double conversionAt,cooldownUntil,nextPoll;long conversionGeneration;
        string objects="",backend="STARTING";float near=float.NaN;int nearHits;
        readonly List<LabPoint> depthPoints=new List<LabPoint>(432);
        readonly LabPoint[] pointPool=CreatePoints();readonly LabDepthSample[] depthSamples=new LabDepthSample[432];
        static LabPoint[] CreatePoints(){var p=new LabPoint[432];for(int i=0;i<p.Length;i++)p[i]=new LabPoint(0,0,0);return p;}
        public string DepthStatus {get;private set;}="WAITING_DEPTH";
        public double DepthCameraDelta {get;private set;}=-1;
        public int ProjectedDepthPoints {get;private set;}
        readonly Vector3[] panelCorners=new Vector3[4];
        public Rect OccupiedViewport
        {
            get
            {
                if(panel==null||!panel.gameObject.activeInHierarchy||owner==null||owner.CameraGuidanceOccluded)return Rect.zero;
                panel.GetWorldCorners(panelCorners);var lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var hi=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                for(int i=0;i<4;i++){var point=RectTransformUtility.WorldToScreenPoint(null,panelCorners[i]);var v=new Vector2(point.x/Screen.width,point.y/Screen.height);lo=Vector2.Min(lo,v);hi=Vector2.Max(hi,v);}
                return Rect.MinMaxRect(lo.x-.012f,lo.y-.012f,hi.x+.012f,hi.y+.012f);
            }
        }
        public string Diagnostic=>backend+"; resultAge="+(lastResultAt<0?-1:Time.realtimeSinceStartupAsDouble-lastResultAt).ToString("F2")+"; depthAge="+(lastDepthAt<0?-1:Time.realtimeSinceStartupAsDouble-lastDepthAt).ToString("F2");
        IEnumerator Start()
        {
            owner=GetComponent<LabTestSession>();
            while(owner!=null&&!owner.Ready)yield return null;
            if(owner==null||owner.CameraOwner==null)yield break;
            cameraManager=owner.CameraOwner.cameraManager;
            depth=owner.CameraOwner.arCamera.GetComponent<AROcclusionManager>();
            if(depth==null)depth=owner.CameraOwner.arCamera.gameObject.AddComponent<AROcclusionManager>();
            depth.requestedEnvironmentDepthMode=EnvironmentDepthMode.Disabled;depth.enabled=false;
            var canvas=new GameObject("Camera AI observations",typeof(Canvas),typeof(CanvasScaler));canvas.transform.SetParent(transform,false);
            var c=canvas.GetComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=110;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(412,915);
            var plate=new GameObject("AI status",typeof(RectTransform),typeof(Image));plate.transform.SetParent(canvas.transform,false);
            panel=plate.GetComponent<RectTransform>();panel.anchorMin=new Vector2(.06f,.60f);panel.anchorMax=new Vector2(.94f,.60f);panel.sizeDelta=new Vector2(0,42);plate.GetComponent<Image>().color=new Color(.03f,.08f,.17f,.9f);plate.GetComponent<Image>().raycastTarget=false;
            var text=new GameObject("Observation",typeof(RectTransform),typeof(Text));text.transform.SetParent(plate.transform,false);var rect=text.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(10,4);rect.offsetMax=new Vector2(-10,-4);
            label=text.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=12;label.color=Color.white;label.alignment=TextAnchor.MiddleLeft;label.raycastTarget=false;
        }
        void EnsureDetector()
        {
            if(detector!=null||backend.StartsWith("ERROR"))return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try{using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))detector=new AndroidJavaObject("com.jarvis.warehouse.LabObjectDetector",activity);}
            catch(Exception error){backend="ERROR_"+error.GetType().Name;}
#else
            backend="EDITOR_NO_DEVICE";
#endif
        }
        void Update()
        {
            try{UpdateCamera();}
            catch(Exception e)
            {
                owner?.ReportStability("AI_UPDATE",e);
                ReleaseProcessing();backend="ERROR_"+e.GetType().Name;cooldownUntil=Time.realtimeSinceStartupAsDouble+60;
            }
        }
        void UpdateCamera()
        {
            if(label==null||paused||disposed)return;
            double now=Time.realtimeSinceStartupAsDouble;
            bool scanning=owner.NeedsCalibration||owner.FixedSurvey||owner.SettingUp||owner.CameraGuidanceOccluded;
            bool eligible=owner.Ready&&!scanning&&owner.Tracking&&owner.CameraMapPoseReady&&(owner.NavigationInProgress||owner.Locating);
            bool active=eligible&&now>=cooldownUntil;
            panel.gameObject.SetActive(eligible);
            if(depth!=null&&active!=wasActive){depth.requestedEnvironmentDepthMode=active?EnvironmentDepthMode.Fastest:EnvironmentDepthMode.Disabled;depth.enabled=active;}
            if(!active){if(wasActive){generation++;sequence++;objects="";nearHits=0;lastResultAt=lastDepthAt=-1;}CancelConversion();wasActive=false;DepthStatus="IDLE_OR_ALIGNING";ProjectedDepthPoints=0;DepthCameraDelta=-1;if(eligible)label.text="Đã giảm tải AI tạm thời do bộ nhớ thấp.\nAR và dữ liệu mốc vẫn được giữ.";return;}
            wasActive=true;EnsureDetector();
            PollCapture(now);
            if(detector!=null)
            {
                try
                {
                    string json=null;if(now>=nextPoll){nextPoll=now+.1;backend=detector.Call<string>("status");json=detector.Call<string>("poll");}
                    if(!string.IsNullOrEmpty(json))
                    {
                        var result=JsonUtility.FromJson<Result>(json);
                        if(result!=null&&result.frame==sequence&&now-submittedAt<.8)
                        {owner.RecordSuiteSemantic(json,now);inferenceInterval=Mathf.Clamp(result.ms*.002f,.35f,.85f);lastResultAt=now;objects="";foreach(var item in result.items??Array.Empty<Item>()){string name=Name(item.label);if(name!=null&&!objects.Contains(name))objects+=(objects.Length>0?", ":"")+name;}}
                    }
                    if(backend=="ERROR_MEMORY"){owner.ReportStability("AI_MEMORY",null);ReduceMemoryLoad();return;}
                    if(backend.StartsWith("ERROR")){CancelConversion();CloseDetector();}
                    if(detector!=null&&!scanning&&owner.Tracking&&now>=next&&!converting&&!detector.Call<bool>("isBusy"))
                    {next=now+inferenceInterval;BeginCapture(now);}
                }catch(Exception error){backend="ERROR_"+error.GetType().Name;owner.ReportStability("AI_BRIDGE",error);CancelConversion();CloseDetector();}
            }
            if(!scanning&&owner.Tracking&&now>=nextDepth){nextDepth=now+.2;ReadDepth(now);}
            if(!owner.Tracking||now-lastDepthAt>.5){nearHits=0;near=float.NaN;}
            bool close=nearHits>=2&&!float.IsNaN(near)&&near<1;
            label.color=close?new Color(1,.85f,.4f):Color.white;
            label.text=close?"Bề mặt gần camera · ~"+near.ToString("F1")+" m\nDừng lại, quan sát lối đi thực tế.":backend.StartsWith("ERROR")?"AI chưa sẵn sàng · dẫn đường theo map\nKhông có cảnh báo vật thể từ AI.":backend=="STARTING"?"AI · Đang nạp model offline…":lastResultAt<0||now-lastResultAt>.8?"AI · Đang chờ ảnh camera rõ\nQuan sát lối đi thực tế.":objects.Length>0?"AI thấy: "+objects+"\n"+(now-lastDepthAt>.5?"Chưa có độ sâu · chưa đo khoảng cách.":"Camera: "+DepthHint()):"Camera: "+DepthHint()+"\nNhãn đồ vật không xác nhận lối đi trống.";
        }
        double nextDepth;
        void BeginCapture(double now)
        {
            try
            {
                if(converting||cameraManager==null||!cameraManager.TryAcquireLatestCpuImage(out var image))return;
                try
                {
                    float scale=256f/Mathf.Max(image.width,image.height);conversionSize=new Vector2Int(Mathf.Max(1,Mathf.RoundToInt(image.width*scale)),Mathf.Max(1,Mathf.RoundToInt(image.height*scale)));
                    var parameters=new XRCpuImage.ConversionParams{inputRect=new RectInt(0,0,image.width,image.height),outputDimensions=conversionSize,outputFormat=TextureFormat.RGBA32,transformation=XRCpuImage.Transformation.None};
                    conversion=image.ConvertAsync(parameters);converting=true;conversionAt=now;conversionGeneration=generation;
                }
                finally{image.Dispose();}
            }
            catch(Exception e){CancelConversion();next=now+1;owner.ReportStability("AI_CONVERSION_START",e);}
        }
        void PollCapture(double now)
        {
            if(!converting)return;
            try
            {
                if(paused||disposed||conversionGeneration!=generation||now-conversionAt>2){CancelConversion();next=now+1;return;}
                var status=conversion.status;if(!status.IsDone())return;
                if(status==XRCpuImage.AsyncConversionStatus.Ready&&wasActive&&detector!=null)
                {
                    var data=conversion.GetData<byte>();int size=conversionSize.x*conversionSize.y*4;
                    if(data.Length!=size)throw new InvalidOperationException("AI_FRAME_SIZE");
                    if(rgbaBuffer==null||rgbaBuffer.Length!=size)rgbaBuffer=new byte[size];data.CopyTo(rgbaBuffer);
                    submittedAt=now;detector.Call("submit",rgbaBuffer,conversionSize.x,conversionSize.y,++sequence);
                }
                CancelConversion();
            }
            catch(Exception e){CancelConversion();CloseDetector();backend="ERROR_"+e.GetType().Name;owner.ReportStability("AI_CONVERSION_RESULT",e);}
        }
        void CancelConversion()
        {
            if(!converting)return;converting=false;
            try{conversion.Dispose();}catch(Exception e){owner?.ReportStability("AI_CONVERSION_DISPOSE",e);}
        }
        void CloseDetector()
        {
            var previous=detector;detector=null;if(previous==null)return;
            try{previous.Call("close");}catch(Exception e){owner?.ReportStability("AI_CLOSE",e);}
            finally{try{previous.Dispose();}catch(Exception e){owner?.ReportStability("AI_PROXY_DISPOSE",e);}}
        }
        void ReleaseProcessing()
        {
            generation++;sequence++;CancelConversion();CloseDetector();rgbaBuffer=null;wasActive=false;
            if(depth!=null)try{depth.requestedEnvironmentDepthMode=EnvironmentDepthMode.Disabled;depth.enabled=false;}catch(Exception e){owner?.ReportStability("DEPTH_RELEASE",e);}
            lastDepthTimestamp=-1;lastResultAt=lastDepthAt=-1;objects="";nearHits=0;
        }
        public void ReduceMemoryLoad()
        {
            cooldownUntil=Time.realtimeSinceStartupAsDouble+60;ReleaseProcessing();backend="STARTING";DepthStatus="LOW_MEMORY_COOLDOWN";
        }
        void ReadDepth(double now)
        {
            ProjectedDepthPoints=0;DepthCameraDelta=-1;
            if(depth==null||!depth.enabled||!depth.TryAcquireEnvironmentDepthCpuImage(out var image)){DepthStatus="NO_DEPTH_IMAGE";return;}
            try
            {
                // Near-camera warning is independent from floor/route evidence.
                bool uint16=image.format==XRCpuImage.Format.DepthUint16;
                if(!uint16&&image.format!=XRCpuImage.Format.DepthFloat32){DepthStatus="UNSUPPORTED_DEPTH_FORMAT";return;}
                if(Math.Abs(image.timestamp-lastDepthTimestamp)<.00001)return;lastDepthTimestamp=image.timestamp;
                var plane=image.GetPlane(0);var bytes=plane.data;float sum=0;int count=0,valid=0;
                for(int y=image.height*4/10;y<image.height*6/10;y+=2)for(int x=image.width*4/10;x<image.width*6/10;x+=2)
                {int offset=y*plane.rowStride+x*plane.pixelStride;if(offset<0||offset+(uint16?2:4)>bytes.Length)continue;float z=uint16?(bytes[offset]|bytes[offset+1]<<8)*.001f:bytes.Reinterpret<float>(1)[offset/4];if(float.IsNaN(z)||z<.25f||z>5)continue;valid++;if(z<1){sum+=z;count++;}}
                lastDepthAt=now;
                ObserveMappedDepth(image,now,uint16);
                if(count>=8&&count>=valid*.5f){near=sum/count;nearHits++;}else{nearHits=0;near=float.NaN;}
            }catch(Exception e){DepthStatus="DEPTH_UNAVAILABLE";nextDepth=now+1;owner.ReportStability("DEPTH_READ",e);}finally{image.Dispose();}
        }
        string DepthHint()
        {
            if(!owner.CameraMapReady)return "dữ liệu map chưa khớp";
            if(!owner.CameraMapPoseReady)return "cần căn đúng vị trí";
            if(owner.RouteSource=="CAMERA_GRID")return "lối vòng · đã quan sát "+Mathf.RoundToInt(owner.FrontFloorCoverage*100)+"% hành lang phía trước";
            if(owner.CameraBlockedEdges.Length>0)return "đang tìm lối tránh "+owner.CameraBlockedEdges.Length+" đoạn bị chắn";
            if(DepthStatus!="PROJECTED")return "chưa đủ độ sâu · nhìn chậm vào lối";
            return owner.GroundObservationFrames<3?"cần nhìn thấy sàn phía trước":"đang đo vật cản trong lối đã biết";
        }
        // ARCore background uses row-vector screen UV * display transform. CPU depth is sampled in native image UV.
        public static bool DepthUv(Matrix4x4 display,float u,float v,out Vector2 uv)
        {
            var q=display.transpose*new Vector4(u,v,1,0);uv=new Vector2(q.x,q.y);
            return !float.IsNaN(uv.x)&&!float.IsNaN(uv.y)&&uv.x>=0&&uv.x<1&&uv.y>=0&&uv.y<1;
        }
        void ObserveMappedDepth(XRCpuImage image,double now,bool uint16)
        {
            if(!owner.CameraMapPoseReady){DepthStatus="ALIGNMENT_REQUIRED";return;}
            ProjectedDepthPoints=0;DepthCameraDelta=-1;
            if(!owner.TryDepthCameraFrame(image.timestamp,now,out var sampleFrame,out var frameReason))
            {DepthStatus=frameReason;return;}
            DepthCameraDelta=Math.Abs(sampleFrame.timestamp-image.timestamp);
            var plane=image.GetPlane(0);var bytes=plane.data;var camera=owner.CameraOwner.arCamera;depthPoints.Clear();
            for(int row=0;row<18;row++)for(int col=0;col<24;col++)
            {
                float u=.06f+.88f*(col+.5f)/24,v=.08f+.78f*(row+.5f)/18;
                if(!DepthUv(sampleFrame.display,u,v,out var uv))continue;
                int x=(int)(uv.x*image.width),y=(int)(uv.y*image.height),offset=y*plane.rowStride+x*plane.pixelStride;
                if(offset<0||offset+(uint16?2:4)>bytes.Length)continue;
                float z=uint16?(bytes[offset]|bytes[offset+1]<<8)*.001f:bytes.Reinterpret<float>(1)[offset/4];
                if(float.IsNaN(z)||z<.35f||z>3)continue;
                if(!sampleFrame.Project(u,v,z,out var world))continue;
                var map=owner.WorldToMapPoint(world);
                int index=depthPoints.Count;var point=pointPool[index];point.x=map.x;point.y=map.y;point.z=map.z;depthPoints.Add(point);
                depthSamples[index]=new LabDepthSample{u=u,v=v,depth=z,x=map.x,y=map.y,z=map.z};
            }
            DepthStatus=depthPoints.Count>=12?"PROJECTED":"INSUFFICIENT_VALID_DEPTH";
            ProjectedDepthPoints=depthPoints.Count;
            if(depthPoints.Count>=12){owner.ObserveDepth(image.timestamp,now,depthPoints);owner.RecordSuiteDepth(now,image.timestamp,image.width,image.height,depthSamples,depthPoints.Count,camera,cameraManager,sampleFrame);}
        }
        double lastDepthTimestamp=-1;
        static string Name(string value){switch(value){case "person":return "người";case "chair":return "ghế";case "dining table":return "bàn";case "couch":return "sofa";case "bed":return "giường";case "tv":return "màn hình";case "backpack":return "ba lô";case "suitcase":return "va li";default:return null;}}
        void OnApplicationPause(bool value){paused=value;DepthStatus="PAUSED_OR_RESUMING";if(value)ReleaseProcessing();else{backend="STARTING";next=Time.realtimeSinceStartupAsDouble+.75;}}
        void OnDisable(){ReleaseProcessing();backend="STARTING";}
        void OnDestroy(){disposed=true;ReleaseProcessing();}
    }
}
