using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using JarVis.Warehouse.RouteRecording;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

namespace JarVis.Warehouse.VisualMaps
{
    public enum VisualMapMode { Home,Connect,Capture,StartQr,Route,Review,Upload,Build,NavigateQr,Locate,Navigate,Library,MapDetail,Inspect,MapLive }

    public sealed class VisualMapSession : MonoBehaviour
    {
        public ARSession session;
        public ARCameraManager cameraManager;
        public Camera arCamera;
        public ARRaycastManager raycasts;
        public ARPlaneManager planes;
        public ARAnchorManager anchors;
        public VisualMapMode Mode { get; private set; }
        public string Message { get; private set; } = "Đang mở camera AR…";
        public bool Ready { get; private set; }
        public bool Tracking => Ready && ARSession.state == ARSessionState.SessionTracking && Time.realtimeSinceStartupAsDouble - lastFrame < 0.4;
        public bool Configured => !string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(token);
        public bool Busy { get; private set; }
        public bool FloorSet { get; private set; }
        public int PhotoCount => capture?.frames.Count ?? 0;
        public int DepthFrames { get; private set; }
        public string DepthStatus { get; private set; } = "Depth chưa có dữ liệu";
        public List<VisualMapLocalEntry> LocalCaptures { get; private set; } = new List<VisualMapLocalEntry>();
        public VisualMapModel[] RemoteMaps { get; private set; } = Array.Empty<VisualMapModel>();
        public VisualMapModel SelectedMap { get; private set; }
        public VisualMapGeometry Geometry { get; private set; }
        public string GeometryMessage { get; private set; } = "Chọn map để tải sơ đồ 2D";
        public bool MapPoseAvailable => hasAlignment && Tracking && (Mode == VisualMapMode.Navigate || Mode == VisualMapMode.MapLive) && visualRoot != null && anchor != null && anchor.trackingState == TrackingState.Tracking && Time.realtimeSinceStartupAsDouble - lastLocalized <= 20;
        public Vector3 MapPosition => visualRoot != null ? visualRoot.InverseTransformPoint(arCamera.transform.position) : Vector3.zero;
        public Vector3 MapForward => visualRoot != null ? Vector3.ProjectOnPlane(visualRoot.InverseTransformDirection(arCamera.transform.forward),Vector3.up).normalized : Vector3.forward;
        public Texture2D ObjectImage { get; private set; }
        public VisualObjectDetection[] ObjectDetections { get; private set; } = Array.Empty<VisualObjectDetection>();
        public int LibraryVersion { get; private set; }
        public string CaptureName => capture?.name ?? "";
        public string CaptureHint => Time.realtimeSinceStartupAsDouble < captureNoticeUntil ? Message : captureHint;
        private string captureHint = "Đang đọc camera…";
        private double nextCoach,captureNoticeUntil;
        private float captureSpeed,captureTurnSpeed;
        private AROcclusionManager occlusion;
        private string selectedMapId,pendingParent;
        public int RoutePhotos { get; private set; }
        public float UploadProgress { get; private set; }
        public float Remaining { get; private set; }
        public string Direction { get; private set; } = "";
        public bool Arrived { get; private set; }
        public int LocalizationMatches => candidateCount;
        public string PcAddress => Configured ? new Uri(url).Host : "Chưa kết nối PC";
        public string LastBundle => PlayerPrefs.GetString("VisualMaps.lastBundle","");
        private VisualMapCapture capture;
        private VisualMapModel map;
        private string url,token,folder,bundle;
        private double lastFrame,nextPhoto,nextQr,nextQuery,lastLocalized,stillSince,lostSince,cameraTimestamp = -1,savedTimestamp = -1,lastQueryFrameTime = -1;
        private Pose lastCamera,lastPhoto,candidate,candidateCamera,targetAlignment;
        private bool observed,hasCandidate,hasAlignment,addingAnchor,interrupted,wasTracking;
        private int generation,candidateCount,originalSleep,poseEpoch,pollGeneration = -1;
        private Transform visualRoot,beacon;
        private TextMesh destinationLabel;
        private Material blue;
        private VisualMapCue routeCue;
        private VisualMapRouteGuide routeGuide;
        private Vector3 previousMapCamera;
        private bool navigationRequested;
        private ARAnchor anchor;
        private UnityWebRequest request;
        private Task<string> decoding;
        private int decodeGeneration,decodePose;
        private double decodedAt;
        private VisualMapMode decodeMode;
        private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

        private void Awake()
        {
            originalSleep = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 30;
            url = PlayerPrefs.GetString("VisualMaps.url","");
            token = PlayerPrefs.GetString("VisualMaps.token","");
            if (!VisualMapData.Connection("JVM1|" + url + "|" + token,out string checkedUrl,out string checkedToken)) url = token = null;
            cameraManager.frameReceived += Frame;
            occlusion = arCamera.GetComponent<AROcclusionManager>() ?? arCamera.gameObject.AddComponent<AROcclusionManager>();
            occlusion.enabled = false;
            selectedMapId = PlayerPrefs.GetString("VisualMaps.selectedMap","");
            session.enabled = cameraManager.enabled = false;
            planes.enabled = raycasts.enabled = anchors.enabled = false;
            gameObject.AddComponent<VisualMapUi>().Owner = this;
        }

        private IEnumerator Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                double began = Time.realtimeSinceStartupAsDouble;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && Time.realtimeSinceStartupAsDouble - began < 20) yield return null;
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera)) { Message = "Cần cấp quyền Camera trong Cài đặt → JarVis Maps. Đóng rồi mở lại app sau khi cấp quyền."; yield break; }
            }
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null) { Message = "Thiếu XR Manager. Không thể mở AR."; yield break; }
            if (!manager.isInitializationComplete) yield return manager.InitializeLoader();
            if (manager.activeLoader == null) { Message = "Chưa mở được ARCore. Kiểm tra Google Play Services for AR rồi mở lại app."; yield break; }
            manager.StartSubsystems();
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall) { Message = "Thiết bị chưa sẵn sàng cho ARCore."; yield break; }
            session.enabled = cameraManager.enabled = true;
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            planes.enabled = raycasts.enabled = anchors.enabled = true;
            occlusion.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
            occlusion.environmentDepthTemporalSmoothingRequested = false;
            occlusion.requestedOcclusionPreferenceMode = OcclusionPreferenceMode.NoOcclusion;
            occlusion.enabled = true;
            Ready = true;
            Message = "Quét phòng để tự chụp và lưu ảnh. Không cần PC, Wi-Fi hoặc QR để bắt đầu.";
#else
            Message = "Xem giao diện Editor · camera, mặt sàn và đi thật chưa được kiểm chứng.";
            yield break;
#endif
        }

        private void Frame(ARCameraFrameEventArgs frame)
        {
            lastFrame = Time.realtimeSinceStartupAsDouble;
            cameraTimestamp = frame.timestampNs.HasValue ? frame.timestampNs.Value * 1e-9 : -1;
        }

        public void Connect()
        {
            Cancel(); Mode = VisualMapMode.Connect;
            Message = "Mở START-PC.cmd trên máy tính, dùng chung Wi-Fi và quét QR kết nối trên PC.";
        }

        public void BeginCapture()
        {
            if (!Ready || Busy) return;
            Cancel();
            capture = new VisualMapCapture { device = SystemInfo.deviceModel,captureId = Guid.NewGuid().ToString("N"),name = "Phòng " + DateTime.Now.ToString("dd-MM HH:mm"),parentId = pendingParent ?? "" };
            pendingParent = null;
            savedTimestamp = -1;
            folder = Path.Combine(Application.persistentDataPath,"visual-captures",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0,6));
            try { Directory.CreateDirectory(Path.Combine(folder,"images")); }
            catch (Exception) { Message = "Không tạo được thư mục ảnh. Kiểm tra dung lượng điện thoại."; return; }
            FloorSet = interrupted = false;
            RoutePhotos = 0;
            DepthFrames = 0;
            DepthStatus = "Depth chưa có dữ liệu";
            bundle = null;
            nextCoach = nextPhoto = 0;
            captureNoticeUntil = Time.realtimeSinceStartupAsDouble + 3;
            captureHint = "Đang đọc camera…";
            captureSpeed = captureTurnSpeed = 0;
            Mode = VisualMapMode.Capture;
            Message = "Bắt đầu quét: đi chậm, hướng camera quanh vùng cần lưu. App tự chụp và nhắc khi cần bổ sung.";
        }

        public void Primary()
        {
            if (Busy) return;
            if (Mode == VisualMapMode.Home) { BeginCapture(); return; }
            if (Mode == VisualMapMode.Capture)
            {
                if (PhotoCount == 0) { Message = "Chưa có ảnh để lưu. Dịch chuyển chậm, nhìn sàn và đồ vật."; return; }
                capture.captureState = "complete";
                Mode = VisualMapMode.Review;
                SaveCheckpoint();
                Message = "Đã lưu lượt quét phòng · có thể xuất ZIP không cần QR hoặc Wi-Fi. Cần ít nhất 20 ảnh để dựng trên PC.";
            }
            else if (Mode == VisualMapMode.Review) StartCoroutine(Upload());
            else if (Mode == VisualMapMode.Locate) { nextQuery = 0; Message = "Đứng yên một chút, nhìn vùng đã chụp để đối chiếu lại."; }
            else if (Mode == VisualMapMode.Build) StartCoroutine(Poll());
            else if (Mode == VisualMapMode.MapDetail) Navigate();
            else if (Mode == VisualMapMode.Inspect) StartCoroutine(InspectObjects());
            else if (Mode == VisualMapMode.Library) OpenLibrary();
        }

        public void StartRoute()
        {
            if (Busy || Mode != VisualMapMode.Capture || !FloorSet || PhotoCount < 20) { Message = "Thu tối thiểu 20 ảnh phòng và căn sàn trước khi ghi tuyến ứng viên."; return; }
            capture.purpose = "route";
            Mode = VisualMapMode.StartQr;
            Message = "Quét A rồi đi tới B. Đây là tuyến ứng viên; chưa phải đường đã kiểm tra vật cản.";
        }

        public void CaptureExtra()
        {
            if (Mode != VisualMapMode.Capture || Busy) return;
            if (!FloorSet) SetFloor(); else StartRoute();
        }

        public void RenameCapture(string name)
        {
            if (capture == null || Busy) return;
            string value = (name ?? "").Trim();
            if (value.Length < 1 || value.Length > 64) { Message = "Tên cần 1–64 ký tự."; return; }
            capture.name = value;
            bundle = null;
            SaveCheckpoint();
        }

        private void SaveCheckpoint()
        {
            if (capture == null || string.IsNullOrEmpty(folder)) return;
            try { VisualMapLibrary.Checkpoint(folder,capture); }
            catch (Exception) { interrupted = capture.interrupted = true; capture.captureState = "interrupted"; Mode = VisualMapMode.Review; Message = "Chưa lưu được checkpoint. Dừng quét và kiểm tra dung lượng; ảnh đã ghi vẫn còn."; }
        }

        public void OpenLibrary()
        {
            if (Busy) return;
            Cancel(); Mode = VisualMapMode.Library;
            try { LocalCaptures = VisualMapLibrary.Scan(Path.Combine(Application.persistentDataPath,"visual-captures")); }
            catch (Exception) { Message = "Chưa đọc được thư viện cục bộ. Không xóa dữ liệu."; }
            LibraryVersion++;
            Message = "Chọn lượt đã lưu để xuất/gửi lại, hoặc chọn map trên PC. Các phòng không ghi đè nhau.";
            if (Configured) StartCoroutine(RefreshMaps());
        }

        private IEnumerator RefreshMaps()
        {
            int epoch = generation;
            string body = null;
            yield return Send(Get("/maps"),value => body = value);
            if (epoch != generation || body == null) yield break;
            VisualMapList list = null;
            try { list = JsonUtility.FromJson<VisualMapList>(body); } catch (Exception) { }
            RemoteMaps = list?.maps ?? Array.Empty<VisualMapModel>();
            LibraryVersion++;
            Message = LocalCaptures.Count + " lượt trên điện thoại · " + RemoteMaps.Length + " map trên PC.";
        }

        public void SelectLocal(VisualMapLocalEntry entry)
        {
            if (Busy) return;
            try
            {
                capture = JsonUtility.FromJson<VisualMapCapture>(File.ReadAllText(Path.Combine(entry.Folder,"manifest.json")));
                folder = entry.Folder; bundle = null; interrupted = capture.interrupted || capture.captureState == "recording";
                capture.interrupted |= interrupted;
                FloorSet = capture.floorKnown; DepthFrames = entry.DepthFrames; RoutePhotos = 0;
                foreach (VisualMapFrame frame in capture.frames) if (frame.onRoute) RoutePhotos++;
                Mode = VisualMapMode.Review;
                Message = entry.Photos + " ảnh đã lưu · " + entry.DepthFrames + " ảnh có Depth. Không nối pose lượt cũ vào phiên AR mới.";
            }
            catch (Exception) { Message = "Không đọc được lượt này; dữ liệu gốc không bị xóa."; }
        }

        public void SelectRemote(VisualMapModel item)
        {
            if (Busy || item == null || string.IsNullOrEmpty(item.id)) return;
            generation++;
            SelectedMap = item; selectedMapId = item.id;
            Geometry = null;
            GeometryMessage = "Đang tải sơ đồ 2D…";
            PlayerPrefs.SetString("VisualMaps.selectedMap",selectedMapId); PlayerPrefs.Save();
            Mode = VisualMapMode.MapDetail;
            Message = item.registered + "/" + item.total + " ảnh ghép · hình học: " + (item.geometryState == "surface-preview" ? "bản nháp bề mặt" : "chưa đủ") + ". " + item.objectCount + " lượt nhận diện trên ảnh, không phải số đồ vật duy nhất. Chưa xác nhận tránh vật cản.";
            StartCoroutine(FetchMapDetail());
        }

        private IEnumerator FetchMapDetail()
        {
            int epoch = generation;
            string identifier = selectedMapId,body = null;
            yield return Send(Get("/maps/" + UnityWebRequest.EscapeURL(identifier)),value => body = value);
            if (epoch != generation || identifier != selectedMapId || body == null) yield break;
            VisualMapModel result = null;
            try { result = JsonUtility.FromJson<VisualMapModel>(body); } catch (Exception) { }
            if (result == null || !result.Valid || result.id != identifier) { Message = "Không đọc được dữ liệu map đã chọn."; yield break; }
            SelectedMap = result;
            body = null;
            yield return Send(Get("/maps/" + UnityWebRequest.EscapeURL(identifier) + "/geometry"),value => body = value);
            if (epoch != generation || identifier != selectedMapId) yield break;
            VisualMapGeometry geometry = null;
            try { if (body != null) geometry = JsonUtility.FromJson<VisualMapGeometry>(body); } catch (Exception) { }
            Geometry = geometry != null && geometry.Valid ? geometry : null;
            GeometryMessage = Geometry != null ? "Sơ đồ: sàn sáng · vật cản đỏ · phần tối chưa rõ" : "Chưa có sơ đồ phòng đủ dữ liệu; chỉ hiển thị tuyến nếu có";
        }

        public void BeginRevision()
        {
            if (SelectedMap == null || Busy) return;
            pendingParent = SelectedMap.id;
            BeginCapture();
        }

        public void OpenObjects()
        {
            if (Busy) return;
            if (!Configured) { Connect(); return; }
            Cancel(); Mode = VisualMapMode.Inspect;
            if (ObjectImage != null) Destroy(ObjectImage);
            ObjectImage = null; ObjectDetections = Array.Empty<VisualObjectDetection>();
            Message = "Chụp một ảnh gửi PC để gợi ý loại đồ vật. Không nhận SKU, không kiểm tra lối đi bằng nhãn ảnh.";
        }

        private IEnumerator InspectObjects()
        {
            if (!ReadImage(out byte[] gray,out VisualMapFrame frame)) { Message = "Camera chưa theo dõi ổn định. Lia chậm rồi chụp lại."; yield break; }
            byte[] jpeg = VisualMapSensor.ColorJpeg(cameraManager,frame) ?? Encode(gray,frame.width,frame.height);
            if (ObjectImage != null) Destroy(ObjectImage);
            ObjectImage = new Texture2D(2,2,TextureFormat.RGB24,false);
            ObjectImage.LoadImage(jpeg); ObjectDetections = Array.Empty<VisualObjectDetection>();
            string identifier = Guid.NewGuid().ToString("N");
            VisualMapQuery query = new VisualMapQuery { requestId = identifier,image = Convert.ToBase64String(jpeg) };
            UnityWebRequest outgoing = new UnityWebRequest(url + "/objects","POST");
            outgoing.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(query)));
            outgoing.downloadHandler = new DownloadHandlerBuffer(); outgoing.SetRequestHeader("Content-Type","application/json");
            int epoch = generation; string body = null;
            Message = "PC đang nhận diện ảnh vừa chụp…";
            yield return Send(outgoing,value => body = value,45);
            if (epoch != generation || body == null) yield break;
            VisualObjectResult result = JsonUtility.FromJson<VisualObjectResult>(body);
            if (result == null || result.requestId != identifier || result.detections == null) { Message = "Kết quả không khớp ảnh vừa chụp. Thử lại."; yield break; }
            ObjectDetections = result.detections;
            StringBuilder summary = new StringBuilder();
            int count = Mathf.Min(4,result.detections.Length);
            for (int index = 0; index < count; index++) summary.Append(result.detections[index].label).Append(" · ").Append(Mathf.RoundToInt(result.detections[index].confidence * 100)).Append("%\n");
            Message = count == 0 ? "Chưa nhận ra đồ vật thuộc nhóm model hỗ trợ. Thử ảnh gần hơn, đủ sáng; không có nhãn không có nghĩa lối trống." : "Gợi ý từ ảnh vừa chụp:\n" + summary.ToString().TrimEnd();
        }

        private void SetFloor()
        {
            captureNoticeUntil = Time.realtimeSinceStartupAsDouble + 4;
            if (!Tracking || !raycasts.Raycast(new Vector2(Screen.width * .5f,Screen.height * .5f),hits,TrackableType.PlaneWithinPolygon)) { Message = "Chưa thấy sàn ở tâm camera. Lia chậm xuống dưới; ảnh phòng vẫn đang được lưu."; return; }
            foreach (ARRaycastHit hit in hits)
            {
                ARPlane plane = planes.GetPlane(hit.trackableId);
                float height = arCamera.transform.position.y - hit.pose.position.y;
                if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp || height < .65f || height > 2.3f) continue;
                capture.floorY = hit.pose.position.y;
                capture.floorKnown = true;
                FloorSet = true;
                SaveCheckpoint();
                captureNoticeUntil = Time.realtimeSinceStartupAsDouble + 4;
                Message = "Đã đặt mặt sàn. Đi một vòng chậm, chụp đồ vật/góc phòng từ các vị trí khác nhau; đừng chỉ xoay tại chỗ.";
                return;
            }
            Message = "Mặt vừa thấy có thể là bàn. Hướng xuống sàn dưới chân, rồi căn lại.";
            captureNoticeUntil = Time.realtimeSinceStartupAsDouble + 4;
        }

        public void Navigate()
        {
            if (!Ready || Busy) return;
            if (!Configured) { Connect(); return; }
            if (string.IsNullOrEmpty(selectedMapId)) { OpenLibrary(); Message = "Chọn map cụ thể trước khi chỉ đường."; return; }
            navigationRequested = SelectedMap != null && SelectedMap.CanNavigate;
            Cancel();
            StartCoroutine(LoadMap());
        }

        private IEnumerator LoadMap()
        {
            int epoch = generation;
            string body = null;
            yield return Send(Get("/maps/" + UnityWebRequest.EscapeURL(selectedMapId)),value => body = value);
            if (epoch != generation || body == null) yield break;
            VisualMapModel result = null;
            try { result = JsonUtility.FromJson<VisualMapModel>(body); }
            catch (Exception) { }
            if (result == null || !result.Valid) { Message = "PC chưa có bản đồ hợp lệ. Thu ảnh → ghi A–B → gửi dựng trước."; yield break; }
            if (result.id != selectedMapId || navigationRequested && !result.CanNavigate) { Mode = VisualMapMode.MapDetail; Message = "Tuyến trên map đã đổi; mở lại map để kiểm tra."; yield break; }
            foreach (VisualMapPoint point in result.points)
                if (point == null || !VisualMapModel.Finite(point.x) || !VisualMapModel.Finite(point.y) || !VisualMapModel.Finite(point.z)) { Message = "Tọa độ bản đồ không hợp lệ; không chỉ đường."; yield break; }
            map = result;
            SelectedMap = result;
            Arrived = hasAlignment = hasCandidate = false;
            candidateCount = 0;
            lastQueryFrameTime = -1;
            Mode = navigationRequested ? VisualMapMode.NavigateQr : VisualMapMode.Locate;
            nextQuery = 0;
            Message = navigationRequested ? "Quét QR A, sau đó hướng máy ra phòng để đối chiếu bản đồ." : "Nhìn vùng đã quét, giữ máy rồi đổi góc nhẹ. Đối chiếu hai góc để hiện vị trí trên map 2D.";
        }

        public void Cancel()
        {
            if (Mode == VisualMapMode.Capture || Mode == VisualMapMode.StartQr || Mode == VisualMapMode.Route)
            { if (capture != null) { capture.interrupted = true; capture.captureState = "interrupted"; } SaveCheckpoint(); }
            generation++;
            request?.Abort();
            Mode = VisualMapMode.Home;
            hasCandidate = hasAlignment = false;
            candidateCount = 0;
            Arrived = false;
            HideVisuals();
            if (visualRoot != null) Destroy(visualRoot.gameObject);
            visualRoot = null;
            if (anchor != null) anchors.TryRemoveAnchor(anchor);
            anchor = null;
            Message = "Ảnh và ZIP đã tạo vẫn được giữ. Bản đồ trên PC không bị xóa.";
        }

        public void ResumeUpload()
        {
            if (Busy || !File.Exists(LastBundle)) return;
            bundle = LastBundle;
            Mode = VisualMapMode.Review;
            Message = "Sẵn sàng gửi lại gói ảnh gần nhất. Không cần chụp lại nếu gói ảnh đủ chất lượng.";
        }

        public void Export()
        {
            if (Busy) return;
            StartCoroutine(CreateBundle(true));
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            Pose camera = new Pose(arCamera.transform.position,arCamera.transform.rotation);
            if (!observed) { lastCamera = camera; observed = true; stillSince = now; }
            float distance = Vector3.Distance(camera.position,lastCamera.position);
            float angle = Quaternion.Angle(camera.rotation,lastCamera.rotation);
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 5);
            captureSpeed = Mathf.Lerp(captureSpeed,distance / Mathf.Max(.01f,Time.unscaledDeltaTime),blend);
            captureTurnSpeed = Mathf.Lerp(captureTurnSpeed,angle / Mathf.Max(.01f,Time.unscaledDeltaTime),blend);
            if (distance > .35f * Mathf.Max(Time.unscaledDeltaTime,.01f) || angle > 30 * Mathf.Max(Time.unscaledDeltaTime,.01f)) stillSince = now;
            bool recording = Mode == VisualMapMode.Capture || Mode == VisualMapMode.StartQr || Mode == VisualMapMode.Route;
            bool navigating = Mode == VisualMapMode.Navigate || Mode == VisualMapMode.Locate || Mode == VisualMapMode.MapLive;
            if (!Tracking && wasTracking)
            {
                poseEpoch++;
                hasCandidate = false; candidateCount = 0;
                if (navigating) LoseLocation("AR mất theo dõi. Dừng lại, chờ đối chiếu ảnh mới; bỏ kết quả ảnh cũ đang xử lý.");
            }
            wasTracking = Tracking;
            if ((recording || navigating) && (distance > Mathf.Max(.45f,3 * Time.unscaledDeltaTime) || angle > 70))
            {
                poseEpoch++;
                if (recording && PhotoCount > 0) InterruptCapture("Vị trí AR vừa nhảy. Lượt này chưa thể dùng; giữ ảnh và bắt đầu lượt mới.");
                else if (navigating) LoseLocation("Vị trí AR vừa nhảy. Dừng lại để đối chiếu bản đồ, không đi theo mũi tên cũ.");
            }
            lastCamera = camera;
            if (!Tracking)
            {
                if (lostSince == 0) lostSince = now;
                if (recording && PhotoCount > 0 && now - lostSince > 8) InterruptCapture("AR chưa tìm lại vị trí sau 8 giây. Giữ lượt ảnh này để kiểm tra, không tự nối sai tọa độ.");
                if (navigating) { HideVisuals(); hasCandidate = false; candidateCount = 0; }
            }
            else lostSince = 0;
            if (Mode == VisualMapMode.Capture && now >= nextCoach)
            {
                nextCoach = now + .5;
                VisualMapImageQuality quality = default;
                try { if (Tracking) quality = VisualMapSensor.Quality(cameraManager); } catch (Exception) { }
                captureHint = Tracking ? VisualMapCoach.Hint(true,quality,captureSpeed,captureTurnSpeed,camera,capture?.frames,now - stillSince) : VisualMapCoach.TrackingHint(ARSession.notTrackingReason);
            }
            if (recording && Tracking && now >= nextPhoto && !interrupted)
            {
                nextPhoto = now + 1;
                if (PhotoCount == 0 || Vector3.Distance(camera.position,lastPhoto.position) > .14f || Quaternion.Angle(camera.rotation,lastPhoto.rotation) > 7) SavePhoto(false,false);
            }
            bool scanning = Mode == VisualMapMode.Connect || Mode == VisualMapMode.StartQr || Mode == VisualMapMode.Route || Mode == VisualMapMode.NavigateQr;
            if (decoding != null && decoding.IsCompleted)
            {
                Task<string> finished = decoding;
                decoding = null;
                if (finished.IsCompletedSuccessfully && decodeGeneration == generation && decodePose == poseEpoch && decodeMode == Mode && Tracking && now - decodedAt < 1.5) HandleQr(finished.Result);
            }
            if (scanning && decoding == null && Tracking && now >= nextQr)
            {
                nextQr = now + .4;
                ScanQr();
            }
            if ((Mode == VisualMapMode.Locate || Mode == VisualMapMode.Navigate || Mode == VisualMapMode.MapLive) && Tracking && !Busy && !addingAnchor && now >= nextQuery && now - stillSince >= .35)
            {
                nextQuery = now + 2.5;
                StartCoroutine(Localize());
            }
            if (Mode == VisualMapMode.Navigate || Mode == VisualMapMode.MapLive)
            {
                if (!Tracking || now - lastLocalized > 20 || anchor == null || anchor.trackingState != TrackingState.Tracking)
                { LoseLocation("Dừng lại · cần đối chiếu ảnh / tìm lại anchor. Nhìn vùng đã chụp và giữ máy ổn định."); return; }
                UpdateGuidance();
            }
        }

        private void InterruptCapture(string reason)
        {
            interrupted = true;
            if (capture != null) { capture.interrupted = true; capture.captureState = "interrupted"; }
            SaveCheckpoint();
            Mode = PhotoCount > 0 ? VisualMapMode.Review : VisualMapMode.Home;
            Message = reason + " Ảnh đã lưu còn nguyên và có thể xuất ZIP.";
        }

        private bool ReadImage(out byte[] gray,out VisualMapFrame metadata)
        {
            gray = null; metadata = null;
            if (!Tracking || !cameraManager.TryGetIntrinsics(out XRCameraIntrinsics intrinsics) || !cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return false;
            try
            {
                if (cameraTimestamp < 0 || Math.Abs(image.timestamp - cameraTimestamp) > .15 || intrinsics.resolution.x <= 0 || intrinsics.resolution.y <= 0) return false;
                int stride = Mathf.Max(1,Mathf.CeilToInt(image.width / 1280f));
                int width = image.width / stride,height = image.height / stride;
                gray = new byte[width * height];
                XRCpuImage.Plane plane = image.GetPlane(0);
                for (int row = 0; row < height; row++)
                    for (int column = 0; column < width; column++) gray[row * width + column] = plane.data[row * stride * plane.rowStride + column * stride * plane.pixelStride];
                Vector3 position = arCamera.transform.position;
                Quaternion rotation = arCamera.transform.rotation;
                float sx = width / (float)intrinsics.resolution.x,sy = height / (float)intrinsics.resolution.y;
                metadata = new VisualMapFrame { width = width,height = height,fx = intrinsics.focalLength.x * sx,fy = intrinsics.focalLength.y * sy,cx = intrinsics.principalPoint.x * sx,cy = intrinsics.principalPoint.y * sy,time = Time.realtimeSinceStartupAsDouble,position = new[] { position.x,position.y,position.z },rotation = new[] { rotation.x,rotation.y,rotation.z,rotation.w } };
                metadata.cameraTime = image.timestamp;
                return true;
            }
            finally { image.Dispose(); }
        }

        public static byte[] Encode(byte[] gray,int width,int height)
        {
            Color32[] pixels = new Color32[gray.Length];
            for (int row = 0; row < height; row++)
                for (int column = 0; column < width; column++)
                {
                    byte value = gray[row * width + column];
                    pixels[(height - row - 1) * width + column] = new Color32(value,value,value,255);
                }
            Texture2D texture = new Texture2D(width,height,TextureFormat.RGB24,false);
            try { texture.SetPixels32(pixels); texture.Apply(false); return texture.EncodeToJPG(90); }
            finally { Destroy(texture); }
        }

        private bool SavePhoto(bool start,bool end)
        {
            if (capture == null || capture.frames.Count >= 600) { Message = "Đã đủ giới hạn an toàn 600 ảnh cho một lượt. Kết thúc tại B hoặc bắt đầu lượt khác."; return false; }
            if (!ReadImage(out byte[] gray,out VisualMapFrame frame)) return false;
            if (frame.cameraTime <= savedTimestamp && capture.frames.Count > 0)
            {
                if (!start && !end) return false;
                VisualMapFrame existing = capture.frames[capture.frames.Count - 1];
                if (!existing.onRoute) { existing.onRoute = true; RoutePhotos++; }
                existing.start |= start; existing.end |= end;
                SaveCheckpoint();
                return true;
            }
            VisualMapImageQuality quality = VisualMapCoach.Measure(gray,frame.width,frame.height);
            if (quality.Unusable) { Message = "Chưa lưu khung ảnh tối/trắng hoặc gần như trống. Hướng camera vào vùng có chi tiết; không cần quét lại từ đầu."; return false; }
            frame.name = capture.frames.Count.ToString("D5") + ".jpg";
            frame.onRoute = Mode == VisualMapMode.Route;
            frame.start = start; frame.end = end;
            try
            {
                File.WriteAllBytes(Path.Combine(folder,"images",frame.name),VisualMapSensor.ColorJpeg(cameraManager,frame) ?? Encode(gray,frame.width,frame.height));
            }
            catch (Exception) { InterruptCapture("Không lưu được ảnh; kiểm tra dung lượng rồi bắt đầu lại."); return false; }
            try
            {
                if (VisualMapSensor.Depth(occlusion,frame,folder)) { DepthFrames++; DepthStatus = "Đã có Depth + confidence đồng bộ"; }
                else DepthStatus = "Chưa có Depth đủ tin cậy · chỉ lưu ảnh";
            }
            catch (Exception) { frame.depth = null; DepthStatus = "Depth tạm chưa dùng được · ảnh vẫn lưu"; }
            capture.frames.Add(frame);
            SaveCheckpoint();
            savedTimestamp = frame.cameraTime;
            if (frame.onRoute) RoutePhotos++;
            lastPhoto = new Pose(arCamera.transform.position,arCamera.transform.rotation);
            return true;
        }

        private void ScanQr()
        {
            if (!ReadImage(out byte[] gray,out VisualMapFrame frame)) return;
            decodeGeneration = generation; decodePose = poseEpoch; decodeMode = Mode;
            decodedAt = Time.realtimeSinceStartupAsDouble;
            decoding = Task.Run(() => WalkingRouteRecorder.Decode(gray,frame.width,frame.height));
        }

        private void HandleQr(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (Mode == VisualMapMode.Connect)
            {
                if (!VisualMapData.Connection(text,out string address,out string secret)) { Message = "Đây không phải QR kết nối PC JarVis Maps trên mạng riêng."; return; }
                url = address; token = secret;
                PlayerPrefs.SetString("VisualMaps.url",url); PlayerPrefs.SetString("VisualMaps.token",token); PlayerPrefs.Save();
                Mode = VisualMapMode.Home;
                Message = "Đã đọc QR kết nối. Đang kiểm tra PC…";
                StartCoroutine(Poll());
                return;
            }
            string expected = Mode == VisualMapMode.Route ? RoomRouteAlignment.EndQr : RoomRouteAlignment.StartQr;
            if (text != expected) { Message = "Sai mã. Cần QR " + (Mode == VisualMapMode.Route ? "B." : "A."); return; }
            if (Mode == VisualMapMode.NavigateQr) { Mode = VisualMapMode.Locate; nextQuery = 0; Message = "Đã nhận A · hướng camera ra phòng để đối chiếu ảnh. Chưa căn vị trí chỉ bằng QR."; return; }
            if (Mode == VisualMapMode.StartQr)
            {
                Mode = VisualMapMode.Route;
                if (!SavePhoto(true,false)) { if (!interrupted) Mode = VisualMapMode.StartQr; return; }
                Message = "Đã ghi A. Đi chậm đúng lối muốn lưu tới B. Đích B là vị trí đứng khi quét kết thúc.";
            }
            else if (Mode == VisualMapMode.Route)
            {
                if (RoutePhotos < 4) { Message = "Cần ít nhất 4 ảnh trên tuyến và đường dài trên 1 m; tiếp tục đi tới B."; return; }
                if (!SavePhoto(false,true)) return;
                capture.captureState = "complete";
                Mode = VisualMapMode.Review;
                SaveCheckpoint();
                Message = "Đã nhận B · " + PhotoCount + " ảnh. Gửi sang PC để dựng bản đồ; ảnh gốc vẫn được giữ.";
            }
        }

        private IEnumerator CreateBundle(bool export)
        {
            if (bundle == null)
            {
                if (capture == null || PhotoCount == 0) { Message = "Chưa có ảnh để xuất."; yield break; }
                Busy = true;
                Message = "Đang đóng gói ảnh; không xóa ảnh gốc…";
                try { File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonUtility.ToJson(capture),new UTF8Encoding(false)); }
                catch (Exception) { Busy = false; Message = "Không lưu được thông tin lượt chụp. Kiểm tra dung lượng; ảnh gốc vẫn còn."; yield break; }
                string capturedFolder = folder;
                Task<string> task = Task.Run(() => VisualMapData.Bundle(capturedFolder));
                while (!task.IsCompleted) yield return null;
                Busy = false;
                if (task.IsFaulted) { Message = "Không đóng gói được. Kiểm tra dung lượng, ảnh gốc còn nguyên."; yield break; }
                bundle = task.Result;
                PlayerPrefs.SetString("VisualMaps.lastBundle",bundle); PlayerPrefs.Save();
            }
            if (export)
            {
                try { Message = "Đã xuất: " + VisualMapData.Export(bundle); }
                catch (Exception) { Message = "Chưa xuất được sang Download. ZIP nội bộ vẫn còn, có thể Gửi sang PC lại."; }
            }
        }

        private IEnumerator Upload()
        {
            if (!Configured) { Message = "Cần Kết nối PC bằng QR trước; có thể Xuất ZIP để giữ ảnh."; yield break; }
            if (capture != null && (capture.interrupted || interrupted || PhotoCount < 20)) { Message = "Cần tối thiểu 20 ảnh cùng phiên, không bị mất tracking để dựng. Vẫn có thể xuất ZIP cứu dữ liệu."; yield break; }
            int epoch = generation;
            yield return CreateBundle(false);
            if (epoch != generation || bundle == null) yield break;
            Mode = VisualMapMode.Upload;
            UnityWebRequest upload = new UnityWebRequest(url + "/capture","POST");
            upload.uploadHandler = new UploadHandlerFile(bundle);
            upload.downloadHandler = new DownloadHandlerBuffer();
            upload.SetRequestHeader("Content-Type","application/zip");
            string body = null;
            yield return Send(upload,value => body = value,180);
            if (epoch != generation) yield break;
            if (body == null) { Mode = VisualMapMode.Review; yield break; }
            Mode = VisualMapMode.Build;
            Message = "PC đã nhận ảnh. Đang dựng bản đồ thật; chưa có % phủ phòng để báo.";
            yield return Poll();
        }

        private IEnumerator Poll()
        {
            int epoch = generation;
            if (pollGeneration == epoch) yield break;
            pollGeneration = epoch;
            try
            {
              do
              {
                string body = null;
                yield return Send(Get("/status"),value => body = value);
                if (epoch != generation || body == null) yield break;
                VisualMapResponse result = JsonUtility.FromJson<VisualMapResponse>(body);
                Message = result.stage + (result.total > 0 ? " · " + result.registered + "/" + result.total + " ảnh" : "");
                if (result.state == "failed") { Mode = File.Exists(LastBundle) ? VisualMapMode.Review : VisualMapMode.Home; yield break; }
                if (result.state == "ready") { Mode = VisualMapMode.Home; Message += " · Mở Thư viện để xem map; chưa xác nhận tránh vật cản."; yield break; }
                if (Mode != VisualMapMode.Build) yield break;
                yield return new WaitForSecondsRealtime(3);
              } while (epoch == generation);
            }
            finally { if (pollGeneration == epoch) pollGeneration = -1; }
        }

        private UnityWebRequest Get(string path) => UnityWebRequest.Get(url + path);

        private IEnumerator Send(UnityWebRequest outgoing,Action<string> success,int timeout = 25)
        {
            Busy = true;
            request = outgoing;
            outgoing.timeout = timeout;
            outgoing.redirectLimit = 0;
            outgoing.SetRequestHeader("Authorization","Bearer " + token);
            using (outgoing)
            {
                UnityWebRequestAsyncOperation operation = outgoing.SendWebRequest();
                while (!operation.isDone) { UploadProgress = Mathf.Clamp01(outgoing.uploadProgress); yield return null; }
                if (outgoing.result == UnityWebRequest.Result.Success && outgoing.downloadedBytes <= 2 * 1024 * 1024) success(outgoing.downloadHandler.text);
                else
                {
                    Message = outgoing.responseCode == 401 ? "QR kết nối đã cũ. Kết nối PC lại." : outgoing.responseCode == 404 ? "PC chưa có bản đồ. Thu ảnh và gửi dựng trước." : outgoing.responseCode == 409 ? "PC đang bận hoặc bản đồ đã đổi. Dừng và chọn Chỉ đường lại." : "Chưa liên lạc được PC. Kiểm tra cùng Wi-Fi, server đang mở và quyền mạng riêng của Windows; không cần cài lại app.";
                    if (outgoing.responseCode >= 400 && outgoing.responseCode < 500 && outgoing.downloadedBytes < 4096)
                    {
                        try
                        {
                            VisualMapResponse error = JsonUtility.FromJson<VisualMapResponse>(outgoing.downloadHandler.text);
                            if (!string.IsNullOrEmpty(error.error) && error.error.Length < 450) Message = error.error;
                        }
                        catch (Exception) { }
                    }
                }
            }
            if (request == outgoing) request = null;
            Busy = false;
        }

        private IEnumerator Localize()
        {
            if (map == null || !ReadImage(out byte[] gray,out VisualMapFrame frame)) yield break;
            if (frame.cameraTime <= lastQueryFrameTime) yield break;
            lastQueryFrameTime = frame.cameraTime;
            int epoch = generation;
            int poseVersion = poseEpoch;
            string id = Guid.NewGuid().ToString("N");
            Pose observedCamera = new Pose(new Vector3(frame.position[0],frame.position[1],frame.position[2]),new Quaternion(frame.rotation[0],frame.rotation[1],frame.rotation[2],frame.rotation[3]));
            double began = Time.realtimeSinceStartupAsDouble;
            VisualMapQuery query = new VisualMapQuery { mapId = map.id,requestId = id,device = SystemInfo.deviceModel,fx = frame.fx,fy = frame.fy,cx = frame.cx,cy = frame.cy,image = Convert.ToBase64String(Encode(gray,frame.width,frame.height)) };
            UnityWebRequest outgoing = new UnityWebRequest(url + "/localize","POST");
            outgoing.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(query)));
            outgoing.downloadHandler = new DownloadHandlerBuffer();
            outgoing.SetRequestHeader("Content-Type","application/json");
            string body = null;
            Message = "Đang đối chiếu ảnh với bản đồ phòng…";
            yield return Send(outgoing,value => body = value);
            if (epoch != generation || poseVersion != poseEpoch || body == null || !Tracking || Time.realtimeSinceStartupAsDouble - began > 12) yield break;
            VisualMapResponse result = JsonUtility.FromJson<VisualMapResponse>(body);
            if (result.mapId != map.id || result.requestId != id || !VisualMapData.Pose(result,out Pose mapCamera))
            { Message = string.IsNullOrEmpty(result.message) ? "Ảnh chưa đủ khớp. Nhìn vùng đã chụp, giữ máy một chút để thử lại." : result.message; RecordLocalizationFailure(); yield break; }
            if (navigationRequested && !hasAlignment && Vector2.Distance(new Vector2(mapCamera.position.x,mapCamera.position.z),new Vector2(map.points[0].x,map.points[0].z)) > 1.2f)
            { Message = "Đã khớp bản đồ nhưng chưa gần A. Quay về chỗ bắt đầu rồi quét lại."; Mode = VisualMapMode.NavigateQr; yield break; }
            if (!VisualMapData.Alignment(observedCamera,mapCamera,out Pose alignment)) { LoseLocation("Hướng trọng lực chưa khớp bản đồ. Đã ẩn đường; nhìn góc phòng khác đã chụp rồi giữ máy để căn lại."); yield break; }
            if ((Mode == VisualMapMode.Navigate || Mode == VisualMapMode.MapLive) && hasAlignment)
            {
                Pose current = new Pose(visualRoot.position,visualRoot.rotation);
                if (!VisualMapData.Consistent(current,alignment)) { LoseLocation("Hai lần định vị chưa đồng nhất. Đã ẩn mũi tên; dừng để đối chiếu lại."); yield break; }
                targetAlignment = new Pose(anchor.transform.InverseTransformPoint(alignment.position),Quaternion.Inverse(anchor.transform.rotation) * alignment.rotation);
                lastLocalized = Time.realtimeSinceStartupAsDouble;
                Message = "Khớp " + result.inliers + " điểm · sai số ảnh " + result.medianError.ToString("F1") + " px. Quan sát lối đi thật.";
                yield break;
            }
            if (hasCandidate && VisualMapData.Consistent(candidate,alignment))
            {
                if (!VisualMapData.DifferentView(candidateCamera,observedCamera))
                { Message = "Đã khớp 1/2 lượt. Nhìn sang một góc khác trong vùng đã chụp rồi giữ máy; không dùng lại cùng một góc để xác nhận."; yield break; }
                candidateCount++;
            }
            else candidateCount = 1;
            candidate = alignment; candidateCamera = observedCamera; hasCandidate = true;
            Message = "Đã khớp " + candidateCount + "/2 lượt · " + result.inliers + " điểm. Nhìn một góc khác đã chụp rồi giữ máy.";
            if (candidateCount < 2) yield break;
            AddAnchor(alignment,epoch,poseVersion);
        }

        private void RecordLocalizationFailure()
        {
            hasCandidate = false; candidateCount = 0;
            if ((Mode == VisualMapMode.Navigate || Mode == VisualMapMode.MapLive) && Time.realtimeSinceStartupAsDouble - lastLocalized > 12) LoseLocation(Message);
        }

        private async void AddAnchor(Pose pose,int epoch,int poseVersion)
        {
            addingAnchor = true;
            try
            {
                if (visualRoot != null) Destroy(visualRoot.gameObject);
                visualRoot = null;
                if (anchor != null) anchors.TryRemoveAnchor(anchor);
                anchor = null;
                var result = await anchors.TryAddAnchorAsync(pose);
                if (!result.status.IsSuccess()) { Message = "Chưa tạo được anchor AR. Giữ máy ổn định để thử lại."; return; }
                if (this == null || epoch != generation || poseVersion != poseEpoch || !Tracking || Mode != VisualMapMode.Locate) { if (anchors != null) anchors.TryRemoveAnchor(result.value); return; }
                anchor = result.value;
                targetAlignment = new Pose(Vector3.zero,Quaternion.identity);
                BuildVisuals();
                hasAlignment = true;
                Mode = navigationRequested ? VisualMapMode.Navigate : VisualMapMode.MapLive;
                lastLocalized = Time.realtimeSinceStartupAsDouble;
                Message = navigationRequested ? "Đã định vị · theo hướng mũi tên và quan sát lối đi thật." : "Chấm xanh là vị trí của bạn; đầu nhọn là hướng camera. Sơ đồ lấy từ dữ liệu đã quét.";
            }
            catch (Exception) { Message = "Không tạo được anchor; chưa bật chỉ đường. Thử định vị lại."; }
            finally { addingAnchor = false; }
        }

        private void LoseLocation(string reason)
        {
            Mode = VisualMapMode.Locate;
            hasCandidate = false; candidateCount = 0;
            Message = reason;
            HideVisuals();
        }

        private void BuildVisuals()
        {
            Material source = Resources.Load<Material>("Routes/LiveViewUnlit");
            if (source == null) throw new InvalidOperationException("Thiếu vật liệu AR.");
            if (blue == null) { blue = new Material(source); blue.SetColor("_BaseColor",new Color(.04f,.25f,.94f)); }
            visualRoot = new GameObject("COLMAP map frame").transform;
            visualRoot.SetParent(anchor.transform,false);
            if (!navigationRequested) return;
            routeGuide = new VisualMapRouteGuide(map.points);
            previousMapCamera = MapPosition;
            Vector3 initial = previousMapCamera; initial.y = map.floorY;
            routeGuide.Project(initial,.5f);
            GameObject cue = new GameObject("Nearby directional chevrons");
            cue.transform.SetParent(visualRoot,false);
            routeCue = cue.AddComponent<VisualMapCue>(); routeCue.Initialize(source);
            GameObject pin = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(pin.GetComponent<Collider>());
            pin.GetComponent<MeshRenderer>().sharedMaterial = blue;
            beacon = pin.transform; beacon.SetParent(visualRoot,false);
            beacon.localScale = new Vector3(.23f,.23f,.23f);
            beacon.localPosition = map.points[map.points.Length - 1].Position + Vector3.up * .55f;
            GameObject label = new GameObject("Destination B",typeof(TextMesh));
            label.transform.SetParent(beacon,false);
            destinationLabel = label.GetComponent<TextMesh>();
            destinationLabel.font = Resources.Load<Font>("BeVietnamPro-Bold");
            destinationLabel.text = "B";
            destinationLabel.anchor = TextAnchor.MiddleCenter;
            destinationLabel.alignment = TextAlignment.Center;
            destinationLabel.fontSize = 64;
            destinationLabel.characterSize = .045f;
            destinationLabel.color = Color.white;
            label.GetComponent<MeshRenderer>().sharedMaterial = destinationLabel.font.material;
        }

        private void UpdateGuidance()
        {
            if (visualRoot == null || map == null) return;
            visualRoot.gameObject.SetActive(true);
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition,targetAlignment.position,1 - Mathf.Exp(-Time.unscaledDeltaTime * 2));
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation,targetAlignment.rotation,1 - Mathf.Exp(-Time.unscaledDeltaTime * 2));
            if (!navigationRequested || routeGuide == null) return;
            Vector3 camera = MapPosition;
            camera.y = map.floorY;
            Vector3 previous = previousMapCamera; previous.y = map.floorY;
            float offRoute = routeGuide.Project(camera,Vector3.Distance(camera,previous)); previousMapCamera = camera;
            if (offRoute > 1.0f) { HideVisuals(); Direction = "DỪNG · đang lệch lối đã ghi"; Message = "Quay lại lối đã ghi; map 2D vẫn hiển thị vị trí thật của bạn."; return; }
            Remaining = routeGuide.Remaining;
            Vector3 destination = map.points[map.points.Length - 1].Position;
            Arrived = Remaining < .8f && Vector3.Distance(camera,destination) < .7f;
            Direction = Arrived ? "Đã tới gần B · đối chiếu vị trí thật" : "Đi theo lối đã ghi";
            Vector3 aim = routeGuide.Aim(out float turn);
            bool turning = Mathf.Abs(turn) >= 40;
            if (turning) Direction = "Sắp rẽ " + (turn > 0 ? "phải" : "trái");
            Vector3 targetDirection = aim - camera;
            if (targetDirection.sqrMagnitude < .04f) targetDirection = routeGuide.Sample(routeGuide.Progress + .5f) - routeGuide.Sample(routeGuide.Progress);
            float bearing = Vector3.SignedAngle(MapForward,targetDirection,Vector3.up);
            routeCue.gameObject.SetActive(!Arrived && Remaining > .7f && Vector3.Distance(camera,aim) >= .5f);
            routeCue.Show(arCamera,visualRoot.TransformPoint(aim + Vector3.up * 1.0f),turning ? Mathf.Clamp(bearing + turn,-100,100) : bearing,turning);
            Vector3 view = arCamera.WorldToViewportPoint(routeCue.transform.position);
            if (!Arrived && (view.z < 0 || view.x < .05f || view.x > .95f || view.y < .15f || view.y > .85f))
            {
                Direction += Mathf.Abs(bearing) > 20 ? (bearing > 0 ? " · nhìn sang phải →" : " · ← nhìn sang trái") : " · hướng camera về phía trước";
            }
            destinationLabel.transform.rotation = Quaternion.LookRotation(destinationLabel.transform.position - arCamera.transform.position,Vector3.up);
        }

        private void HideVisuals() { if (visualRoot != null) visualRoot.gameObject.SetActive(false); }

        private void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            bool recording = Mode == VisualMapMode.Capture || Mode == VisualMapMode.StartQr || Mode == VisualMapMode.Route;
            Cancel();
            if (recording) interrupted = true;
            Message = recording ? "Lượt chụp đã ngắt khi rời app. Bắt đầu lượt mới để tránh nối sai hệ tọa độ." : "Phiên đã tạm dừng. Chọn Chỉ đường và định vị lại khi quay lại.";
        }

        private void OnDestroy()
        {
            generation++;
            request?.Abort();
            if (cameraManager != null) cameraManager.frameReceived -= Frame;
            Screen.sleepTimeout = originalSleep;
            if (blue != null) Destroy(blue);
            if (ObjectImage != null) Destroy(ObjectImage);
        }
    }
}
