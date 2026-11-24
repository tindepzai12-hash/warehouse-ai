using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using ZXing;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteRecorder : MonoBehaviour
    {
        public ARSession session;
        public ARCameraManager cameraManager;
        public Camera arCamera;
        public WalkingRoute Route { get; private set; } = new WalkingRoute();
        public WalkingRouteData DisplayData => NavigationMode ? Follower.Data : Route.Data ?? recovered;
        public WalkingRouteFollower Follower { get; } = new WalkingRouteFollower();
        public RoomScanFeedback Scan { get; } = new RoomScanFeedback();
        public RoomEnvironmentReadiness Environment { get; } = new RoomEnvironmentReadiness();
        public bool PreparingStart => (ScanningStart || (NavigationMode && Follower.WaitingQr)) && !Environment.Ready;
        public bool AwaitingDeparture { get; private set; }
        public bool GuidanceStarted { get; private set; }
        public bool PreparingEnvironment => ScanningStart || (NavigationMode && Follower.WaitingQr) || AwaitingDeparture;
        public float PreparationProgress => AwaitingDeparture ? Environment.Ready ? 1 : 0.2f + Environment.Progress * 0.8f : Scan.State == RoomScanState.Locked ? 1 : Mathf.Min(0.99f,Environment.Progress * 0.8f + (Environment.Ready ? Scan.Progress * 0.2f : 0));
        public WalkingRouteLiveView LiveView { get; private set; }
        public bool NavigationMode { get; private set; }
        public bool UsingSavedRoute { get; private set; }
        public string RouteSource => "Tuyến phòng · A → B";
        public string DisplayMessage => PreparingStart ? Environment.Message : NavigationMode && !Follower.WaitingQr ? Follower.Message : Message;
        public string Message { get; private set; } = "Đang chuẩn bị camera AR…";
        public bool ScanningStart { get; private set; }
        public bool Ready { get; private set; }
        public bool Tracking => Ready && ARSession.state == ARSessionState.SessionTracking && Environment.Fresh(Time.realtimeSinceStartupAsDouble);
        public bool Stable => Tracking && Time.realtimeSinceStartupAsDouble - trackingSince >= 1;
        public string TrackingLabel => Tracking ? "AR đang theo dõi" : "AR: " + ARSession.notTrackingReason;
        private WalkingRouteData recovered;
        private Task<string> decoding;
        private double decodedAt, nextDecode, nextObserve, nextSave, trackingSince;
        private bool wasTracking, initializing, enabledBeforePause;
        private int scanEpoch, decodeEpoch, originalSleep;
        private bool storageFailed;
        public RoomMarkerTracker Markers { get; private set; }
        private ARTrackedImageManager imageManager;
        private ARPointCloudManager pointCloudManager;
        public bool HasSavedRoute => RoomRouteStorage.Valid(recovered);
        public bool ReplacementPending { get; private set; }
        public bool SaveFailed => storageFailed;
        public WalkingRouteData SavedRoute => recovered;
        public bool Home => !NavigationMode && !ScanningStart && !Route.Active;

        private void Awake()
        {
            originalSleep = Screen.sleepTimeout;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 30;
            session.enabled = false;
            cameraManager.enabled = false;
            cameraManager.frameReceived += CameraFrame;
            imageManager = arCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>().gameObject.AddComponent<ARTrackedImageManager>();
            imageManager.enabled = false;
            Markers = gameObject.AddComponent<RoomMarkerTracker>();
            Markers.Configure(imageManager);
            GameObject origin = imageManager.gameObject;
            pointCloudManager = origin.GetComponent<ARPointCloudManager>() ?? origin.AddComponent<ARPointCloudManager>();
            pointCloudManager.enabled = false;
            gameObject.AddComponent<RoomFeatureSampler>().Configure(pointCloudManager,this);
            gameObject.AddComponent<WalkingRouteRecorderUi>().Recorder = this;
            LiveView = gameObject.AddComponent<WalkingRouteLiveView>();
            LiveView.Recorder = this;
            try { recovered = RoomRouteStorage.Load(Application.persistentDataPath); }
            catch (Exception) { Message = "Không đọc được bản ghi cũ. Có thể bắt đầu bản ghi mới."; }
        }

        private IEnumerator Start()
        {
            yield return Initialize();
        }

        public void Primary()
        {
            if (AwaitingDeparture)
            {
                if (Tracking && Stable && Environment.Ready && !Follower.Suspended && !Follower.RouteCaution && Follower.Active)
                { GuidanceStarted = true; AwaitingDeparture = false; Scan.Hide(); Message = "Đã bắt đầu đi theo tuyến A → B đã lưu."; }
                return;
            }
            if (NavigationMode && Follower.OffRoute && !Follower.Suspended) { RestartAlignment(); return; }
            if (NavigationMode || Route.Active || ScanningStart) { Cancel(); return; }
            if (!Ready)
            {
                if (!initializing) StartCoroutine(Initialize());
                return;
            }
            if (storageFailed) { Persist(); return; }
            if (HasSavedRoute && !ReplacementPending) { ReplacementPending = true; return; }
            BeginRecording();
        }

        public void BeginRecording()
        {
            if (!Ready || Route.Active || NavigationMode) return;
            ReplacementPending = false;
            Route = new WalkingRoute();
            storageFailed = false;
            ScanningStart = true;
            Scan.Begin(RoomRouteAlignment.StartQr);
            Environment.Begin();
            Markers?.ResetSamples();
            scanEpoch++;
            Message = "Quét toàn bộ thẻ A mới, dán thẳng đứng và in đúng 18 × 18 cm.";
        }

        public void Cancel()
        {
            ReplacementPending = false;
            NavigationMode = false;
            GuidanceStarted = AwaitingDeparture = false;
            Follower.Stop("Đã dừng chỉ đường.");
            Route.Interrupt("Đã hủy lượt ghi; tuyến hoàn chỉnh trước vẫn được giữ.");
            Route = new WalkingRoute();
            ScanningStart = false;
            storageFailed = false;
            scanEpoch++;
            Scan.Hide();
            Message = HasSavedRoute ? "Tuyến A → B đã lưu vẫn được giữ." : "Chọn Ghi tuyến để tạo đường A → B.";
        }

        public void Navigate() => NavigateSaved();

        public void NavigateSaved()
        {
            if (!Ready || Route.Active) return;
            if (!HasSavedRoute) { Message = "Chưa có tuyến hoàn chỉnh. Ghi từ A đến B trước."; return; }
            Markers?.ResetSamples();
            StartNavigation(recovered, true);
        }

        public void NavigateExample()
        {
            if (!Ready || Route.Active) return;
            Message = "Bản thử phòng chỉ sử dụng tuyến A → B vừa ghi.";
        }

        private void StartNavigation(WalkingRouteData data, bool saved)
        {
            Follower.Prepare(data);
            UsingSavedRoute = saved;
            NavigationMode = Follower.WaitingQr;
            GuidanceStarted = AwaitingDeparture = false;
            if (!NavigationMode) { Message = "Tuyến chưa đủ dữ liệu hợp lệ (ít nhất 1 m). Hãy ghi lại A → B."; return; }
            ScanningStart = false;
            Scan.Begin(RoomRouteAlignment.StartQr);
            Environment.Begin();
            Message = "Quét toàn bộ thẻ A. Đường sẽ xuất hiện khi khóa được vị trí.";
            scanEpoch++;
        }

        private IEnumerator Initialize()
        {
            initializing = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                bool answered = false;
                UnityEngine.Android.PermissionCallbacks callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += permission => answered = true;
                callbacks.PermissionDenied += permission => answered = true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera, callbacks);
                double requested = Time.realtimeSinceStartupAsDouble;
                while (!answered && Time.realtimeSinceStartupAsDouble - requested < 60) yield return null;
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                {
                    Message = "Cần quyền Camera. Vào Cài đặt điện thoại → Ứng dụng → JarVis Routes → Quyền → Camera, rồi bấm Thử lại.";
                    initializing = false;
                    yield break;
                }
            }
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null)
            {
                Message = "Thiếu cấu hình XR. Không thể ghi hành trình.";
                initializing = false;
                yield break;
            }
            if (!manager.isInitializationComplete) yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            {
                Message = "Không khởi tạo được ARCore. Kiểm tra thiết bị và Google Play Services for AR.";
                initializing = false;
                yield break;
            }
            manager.StartSubsystems();
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
            {
                Message = "Thiết bị chưa hỗ trợ / chưa cài ARCore. Cài Google Play Services for AR rồi thử lại.";
                initializing = false;
                yield break;
            }
            session.enabled = true;
            cameraManager.enabled = true;
            if (Markers.Error != null) { Message = Markers.Error; initializing = false; yield break; }
            imageManager.enabled = true;
            pointCloudManager.enabled = true;
            yield return null;
            if (imageManager.subsystem == null || !imageManager.subsystem.running)
            {
                Message = "Chưa khởi tạo được định vị thẻ A/B. Thử lại camera AR.";
                imageManager.enabled = false;
                initializing = false;
                yield break;
            }
            Ready = true;
            Message = HasSavedRoute ? "Đã có một tuyến A → B. Chọn Chỉ đường hoặc ghi lại." : "Chưa có tuyến. Chọn Ghi tuyến để bắt đầu.";
            ScanningStart = false;
#else
            Message = "Bản xem giao diện Editor — camera AR/QR chỉ được xác nhận trên điện thoại thật.";
#endif
            initializing = false;
            yield break;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool tracking = Tracking;
            if (pointCloudManager != null && pointCloudManager.enabled != (Ready && PreparingEnvironment)) pointCloudManager.enabled = Ready && PreparingEnvironment;
            bool awaitingStart = ScanningStart || (NavigationMode && Follower.WaitingQr) || AwaitingDeparture;
            if (awaitingStart) Environment.Observe(arCamera.transform.position,arCamera.transform.rotation,now,tracking);
            if (tracking && !wasTracking) trackingSince = now;
            if (!tracking && Route.Active)
            {
                Route.Interrupt("AR mất theo dõi (" + ARSession.notTrackingReason + "). Tuyến bị ngắt, cần ghi lại từ QR đầu.");
                Message = Route.Data.interruption;
                Persist();
            }
            wasTracking = tracking;
            if (NavigationMode && Follower.Active)
            {
                Follower.Observe(arCamera.transform.position, arCamera.transform.eulerAngles.y, now, tracking,GuidanceStarted);
                if (Follower.WaitingQr) { GuidanceStarted = AwaitingDeparture = false; Message = Follower.Message; Scan.Begin(RoomRouteAlignment.StartQr); Environment.Begin(); Markers?.ResetSamples(); scanEpoch++; }
            }
            if (Route.Active && now >= nextObserve)
            {
                nextObserve = now + 0.1;
                if (!Route.Observe(arCamera.transform.position, arCamera.transform.eulerAngles.y, now, tracking))
                {
                    Message = Route.Data.interruption;
                    Persist();
                }
                else if (now >= nextSave)
                {
                    nextSave = now + 2;
                    Persist();
                }
            }
            if (decoding != null && decoding.IsCompleted)
            {
                if (decoding.IsCompletedSuccessfully && decodeEpoch == scanEpoch && now - decodedAt < 1 && Stable) HandleQr(decoding.Result, now);
                decoding = null;
            }
            bool scanning = ScanningStart || Route.AwaitingEnd || (NavigationMode && Follower.WaitingQr);
            if (scanning || now < Scan.SuccessUntil)
            {
                string expected = NavigationMode || ScanningStart ? RoomRouteAlignment.StartQr : RoomRouteAlignment.EndQr;
                if (now >= Scan.SuccessUntil && Scan.Expected != expected) Scan.Begin(expected);
                if (PreparingStart) Scan.Warmup(Environment.Progress);
                else Scan.Tick(now,tracking,Markers == null ? 0 : Markers.Progress(expected));
            }
            else if (AwaitingDeparture && !Environment.Ready) Scan.Warmup(Environment.Progress);
            else Scan.Hide();
            if (Stable && scanning && now >= Scan.SuccessUntil && decoding == null && now >= nextDecode)
            {
                nextDecode = now + 0.35;
                CaptureForQr(now);
            }
        }

        private void CaptureForQr(double now)
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) return;
            try
            {
                Scan.Frame(now);
                XRCpuImage.Plane plane = image.GetPlane(0);
                int stride = Math.Max(1, image.width / 1000);
                int width = image.width / stride;
                int height = image.height / stride;
                byte[] luminance = new byte[width * height];
                for (int row = 0; row < height; row++)
                    for (int column = 0; column < width; column++)
                        luminance[row * width + column] = plane.data[row * stride * plane.rowStride + column * stride * plane.pixelStride];
                decodedAt = now;
                decodeEpoch = scanEpoch;
                decoding = Task.Run(() => Decode(luminance, width, height));
            }
            catch (Exception) { Message = "Chưa đọc được ảnh camera. Giữ máy ổn định và thử lại."; }
            finally { image.Dispose(); }
        }

        public static string Decode(byte[] luminance, int width, int height)
        {
            if (luminance == null || width <= 0 || height <= 0 || (long)width * height != luminance.Length) return null;
            BarcodeReaderGeneric reader = new BarcodeReaderGeneric { AutoRotate = true };
            reader.Options.PossibleFormats = new[] { BarcodeFormat.QR_CODE };
            reader.Options.TryHarder = true;
            Result result = reader.Decode(new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8));
            return result?.Text;
        }

        private void HandleQr(string text, double now)
        {
            if (string.IsNullOrEmpty(text)) return;
            string expected = NavigationMode || ScanningStart ? RoomRouteAlignment.StartQr : RoomRouteAlignment.EndQr;
            if (text != expected)
            {
                if (Route.Active && text == RoomRouteAlignment.StartQr) return;
                Scan.Decoded(text,now);
                Message = "Sai mốc. Cần quét thẻ " + (expected == RoomRouteAlignment.StartQr ? "A." : "B để kết thúc.");
                return;
            }
            Scan.Decoded(text,now);
            if (PreparingStart) return;
            if (Markers == null || !Markers.TryGet(text,out Pose marker))
            {
                Message = "Đã đọc QR. Giữ toàn bộ thẻ trong camera để khóa vị trí; thẻ phải dán thẳng đứng.";
                return;
            }
            if (NavigationMode)
            {
                if (Follower.AlignAtMarker(marker,arCamera.transform.position,arCamera.transform.eulerAngles.y,now,Stable)) { GuidanceStarted = false; AwaitingDeparture = Follower.Active; scanEpoch++; Scan.Lock(now); }
                else Message = "Chưa căn được vị trí. Đứng gần A và giữ máy ổn định.";
                return;
            }
            if (!WalkingRoute.TryQr(text, out string warehouse))
            {
                Message = "QR không đúng định dạng mốc kho. Dùng hai mã mẫu A/B được gửi kèm APK.";
                return;
            }
            if (ScanningStart)
            {
                if (Route.Begin(text, arCamera.transform.position, arCamera.transform.eulerAngles.y, now, Tracking))
                {
                    RoomRouteAlignment.Capture(Route.Data,marker,arCamera.transform.position,arCamera.transform.eulerAngles.y);
                    Route.ArmEnd(true);
                    Route.Data.device = SystemInfo.deviceModel;
                    Route.Data.operatingSystem = SystemInfo.operatingSystem;
                    Route.Data.appVersion = Application.version;
                    ScanningStart = false;
                    Scan.Lock(now);
                    scanEpoch++;
                    nextObserve = now + 0.1;
                    nextSave = now + 2;
                    Message = "Đã khóa A. Đi chậm tới B; đưa toàn bộ thẻ B vào camera để kết thúc.";
                }
                return;
            }
            if (!Route.AwaitingEnd) return;
            if (text == Route.Data.startQr) { Message = "Đây là QR đầu. QR cuối phải là mốc khác (ví dụ B)."; return; }
            WalkingRoute.TryQr(Route.Data.startQr, out string startWarehouse);
            if (warehouse != startWarehouse) { Message = "QR cuối thuộc kho khác. Hãy dùng cùng mã kho với QR đầu."; return; }
            if (Route.Finish(text, arCamera.transform.position, arCamera.transform.eulerAngles.y, now, Tracking))
            {
                RoutePoint last = Route.Data.points[Route.Data.points.Count - 1];
                Vector3 cameraRelative = new Vector3(last.x,last.y,last.z);
                float initialYaw = arCamera.transform.eulerAngles.y - last.yawDegrees;
                Route.Data.destinationInRoute = cameraRelative + Quaternion.Inverse(Quaternion.Euler(0,initialYaw,0)) * (marker.position - arCamera.transform.position);
                scanEpoch++;
                Scan.Lock(now);
                Persist();
            }
            else Message = Route.Active ? "Tuyến cần dài ít nhất 1 m và 2 giây. Tiếp tục đi rồi quét QR cuối." : Route.Data.interruption;
        }

        private void Persist()
        {
            if (Route.Data == null || Route.Data.state != "complete") return;
            try
            {
                RoomRouteStorage.Commit(Application.persistentDataPath, Route.Data);
                recovered = Route.Data;
                storageFailed = false;
                Message = "Đã lưu tuyến A → B. Quay lại A rồi chọn Chỉ đường.";
            }
            catch (Exception)
            {
                storageFailed = true;
                Route.Interrupt("Không lưu được dữ liệu. Kiểm tra dung lượng điện thoại rồi ghi lại.");
                Message = "Chưa lưu được tuyến mới. Bản hoàn chỉnh trước vẫn được giữ. Kiểm tra dung lượng rồi thử lưu lại.";
            }
        }

        public void Export()
        {
            if (DisplayData == null || Route.Active) return;
            try
            {
                string path = WalkingRouteStorage.Export(Application.persistentDataPath, DisplayData);
                string published = WalkingRouteStorage.Publish(path);
                storageFailed = false;
                Message = "Đã xuất: " + published;
            }
            catch (Exception) { Message = "Chưa xuất được Downloads. Bản riêng trong ứng dụng được giữ nếu đã lưu thành công; kiểm tra dung lượng rồi bấm Xuất ZIP lại."; }
        }

        public void Secondary()
        {
            Cancel();
        }

        public string StateLabel => NavigationMode ? Follower.WaitingQr ? "Dẫn thử · quét lại A" : Follower.Confirming ? "Cần căn thủ công tại A" : Follower.Active ? Follower.Direction : Follower.Arrived ? "Tới điểm cuối đã ghi" : "Đã dừng dẫn thử" : storageFailed ? "Lỗi lưu dữ liệu" : Route.Active ? (Route.AwaitingEnd ? "Quét mốc cuối" : "Đang ghi hành trình") : DisplayData == null ? "Quét mốc bắt đầu" : DisplayData.state == "complete" ? "Đã ghi xong tuyến" : "Bản ghi bị gián đoạn";

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Environment.Pause();
                GuidanceStarted = AwaitingDeparture = false;
                if (NavigationMode) { Follower.RequireScan("App rời màn hình. Quay về A và quét lại trước khi dẫn."); Message = Follower.Message; Scan.Begin(RoomRouteAlignment.StartQr); }
                enabledBeforePause = Ready;
                if (Route.Active)
                {
                    Route.Interrupt("Ứng dụng rời màn hình / khóa máy. Không nối tuyến sau khi quay lại; hãy ghi từ QR đầu.");
                    Message = Route.Data.interruption;
                    Persist();
                }
                scanEpoch++;
                ScanningStart = false;
                Ready = false;
            }
            else if (enabledBeforePause)
            {
                Ready = true;
                wasTracking = false;
                trackingSince = Time.realtimeSinceStartupAsDouble;
                Message = NavigationMode ? Follower.Message : "Đã quay lại ứng dụng. Bấm ghi tuyến mới; tuyến cũ không được nối tiếp.";
            }
        }

        private void OnDestroy()
        {
            if (cameraManager != null) cameraManager.frameReceived -= CameraFrame;
            Route.Interrupt("Phiên ghi đã đóng trước khi quét QR cuối.");
            Screen.sleepTimeout = originalSleep;
        }

        private void CameraFrame(ARCameraFrameEventArgs frame) => Environment.Frame(Time.realtimeSinceStartupAsDouble,frame.timestampNs);

        private void RestartAlignment()
        {
            GuidanceStarted = AwaitingDeparture = false;
            Follower.RequireScan("Quay về thẻ A để căn vị trí lại.");
            Message = "Quay về A, chuẩn bị AR rồi quét toàn bộ thẻ để căn lại.";
            Environment.Begin();
            Markers?.ResetSamples();
            Scan.Begin(RoomRouteAlignment.StartQr);
            scanEpoch++;
        }
    }
}
