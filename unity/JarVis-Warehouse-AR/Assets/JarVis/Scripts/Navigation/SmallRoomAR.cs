using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Immersal.Samples.Navigation;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.UI;
using TMPro;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ZXing;

namespace JarVis.Warehouse.Navigation
{
    // User-established start: standing at door A and facing basin B. No cloud map is substituted.
    public sealed class SmallRoomAR : MonoBehaviour
    {
        public Transform mapRoot, pins;
        public bool presentationRoom;
        public RoompresentVpsLocalization localization;
        public RoompresentQrLocalization qrLocalization;
        public Vector3[] registrationLandmarks;
        bool MissionLoaded => presentationRoom ? RoompresentDeliveryMission.Current.Loaded : SmallRoomDeliveryMission.Current.Loaded;
        bool MissionAccepted => presentationRoom ? RoompresentDeliveryMission.Current.accepted : SmallRoomDeliveryMission.Current.accepted;
        bool MissionComplete => presentationRoom ? RoompresentDeliveryMission.Current.Complete : SmallRoomDeliveryMission.Current.Complete;
        bool DeliveredB => presentationRoom ? RoompresentDeliveryMission.Current.deliveredB : SmallRoomDeliveryMission.Current.deliveredB;
        bool DeliveredC => presentationRoom ? RoompresentDeliveryMission.Current.deliveredC : SmallRoomDeliveryMission.Current.deliveredC;
        string LogPrefix => presentationRoom ? "[RoompresentQuick] " : "[SmallRoom] ";
        void CloseVenue() { if(presentationRoom)RoompresentQuickMapLauncher.Close();else SmallRoomMapLauncher.Close(); }
        public Vector3 startLocal, basinLocal, bagLocal, basinAccess, bagAccess;
        public NavMeshSurface surface;
        public NavigationPath path;
        ARCameraManager cameraManager;
        ARPlaneManager planes;
        ARAnchorManager anchors;
        ARSession session;
        ARAnchor anchor;
        TMP_Text heading, status, detail, focusLabel;
        bool configured, aligned, addingAnchor, paused, wasTracked;
        bool lostAnchor;
        float frameAt = -100, sampleAt, stableSince, nextRoute, nextQr, resumeAt, nextRecovery;
        Vector3 stableFoot, lastSurfacePosition, nextDirectionPoint;
        Quaternion stableRotation, lastSurfaceRotation;
        int stableCount, epoch, decodeEpoch, qrHits;
        string activeStop, qrCandidate, notice;
        float qrAt, remaining = float.PositiveInfinity;
        Task<RoompresentQrLocalization.QrFrame> decode;
        Matrix4x4 cameraDisplay;
        bool haveCameraDisplay;
        Pose qrCapture;
        int qrWidth,qrHeight;
        readonly RoompresentDeliveryVerification verification=new RoompresentDeliveryVerification();
        public bool HasStartFix=>qrLocalization!=null&&qrLocalization.Trusted;
        public bool DeliveryTracking=>Trusted;
        public string SelectedStop=>activeStop;
        public RoompresentDeliveryVerification Verification=>verification;
        public string DeliveryFeedback {get;private set;}
        public bool StartAligned {get;private set;}
        public string PendingQrSuccess {get;private set;}
        int acknowledgedStartRevision,qrMetricCount;
        Pose destinationPose;
        float destinationSince;
        bool destinationAnchoring;
        float destinationReadAt=-1,nextDestinationLog;
        public bool DestinationPositionUpdated {get;private set;}
        public void RescanStart()
        {
            CancelDelivery();
            if(qrLocalization!=null){acknowledgedStartRevision=qrLocalization.Revision;qrLocalization.RequestReacquireA();}
            pins.gameObject.SetActive(false);path.ClearMesh();
        }
        public void ExitGuidance(){CloseVenue();}
        bool routeMissing;
        float nextTrackingLog;
        public bool RouteMissing=>routeMissing;
        public bool ReturningToStart=>presentationRoom&&activeStop=="A";
        public float ReturnDistance=>ReturningToStart?remaining:float.PositiveInfinity;
        public bool AtStart=>ReturningToStart&&Trusted&&float.IsFinite(remaining)&&remaining<=.65f;
        public void UpdateStartPin()
        {
            if(pins==null)return;
            foreach(var pin in pins.GetComponentsInChildren<IsNavigationTarget>(true))
                if(pin.targetName=="A")pin.gameObject.SetActive(ReturningToStart);
        }
        public void ReturnToStart()
        {
            if(!presentationRoom||!MissionLoaded||!MissionAccepted)return;
            CancelDelivery();activeStop="A";remaining=float.PositiveInfinity;nextRoute=0;UpdateStartPin();
            // Navigation choice only: retain the pending delivery and all saved spatial references.
            Debug.Log(LogPrefix+"Return to saved A selected; delivery progress retained.");
        }
        public float ScanProgress=>verification.Active ? (verification.WaitingForProduct?.5f:RoompresentDeliveryRecord.Destination(qrCandidate)==verification.Stop?Mathf.Min(qrHits/2f*.5f+qrMetricCount/4f*.4f,.9f):0) : qrLocalization!=null&&qrLocalization.SawQr?Mathf.Min(qrLocalization.StableSamples/4f,.9f):0;
        public void AcknowledgeQrSuccess(){PendingQrSuccess=null;epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;nextQr=0;}
        public void RefreshDeliveryCamera(){if(localization!=null)localization.RefreshCameraFocus();}
        public void CompleteDeliveryHandoff()
        {
            // Completing one book does not change the spatial reference, calibration or camera.
            var record=RoompresentDeliveryMission.Current;
            activeStop=record.Complete?null:record.deliveredB?"C":"B";UpdateStartPin();
            record.preferredStop=activeStop;RoompresentDeliveryMission.Save();nextRoute=0;
            epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;
            Debug.Log(LogPrefix+"Delivery handoff next="+activeStop+" anchorRevision="+(qrLocalization==null?0:qrLocalization.Revision)+" calibrated="+(qrLocalization!=null&&qrLocalization.Calibrated));
        }
        public void SelectDelivery(string stop)
        {
            if(!presentationRoom||(stop!="B"&&stop!="C")||stop=="B"&&DeliveredB||stop=="C"&&DeliveredC)return;
            CancelDelivery();activeStop=stop;nextRoute=0;UpdateStartPin();
            RoompresentDeliveryMission.Current.preferredStop=stop;RoompresentDeliveryMission.Save();
        }
        public void BeginDelivery(string stop)
        {
            if(!Trusted)return;
            SelectDelivery(stop);
            if(!verification.Begin(RoompresentDeliveryMission.Current,stop))return;
            DeliveryFeedback=null;DestinationPositionUpdated=false;destinationReadAt=-1;epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;nextQr=0;
            localization.SetAutoFocus(true);
        }
        public void CancelDelivery()
        {
            verification.Cancel();PendingQrSuccess=null;DeliveryFeedback=null;epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;
            if(localization!=null)localization.SetAutoFocus(true);
        }
        bool English => ImportedAppLocalization.IsEnglish;
        string T(string vi, string en) => English ? en : vi;
        bool CameraTracking => !paused && ARSession.state == ARSessionState.SessionTracking && cameraManager != null && cameraManager.enabled && Time.unscaledTime - frameAt < .8f;
        bool QrFrame => qrLocalization!=null&&qrLocalization.Frame!=null;
        bool Trusted => presentationRoom&&localization!=null&&!HasStartFix ? false : QrFrame ? qrLocalization.Trusted&&CameraTracking&&Time.unscaledTime>=resumeAt : localization != null ? aligned && localization.Localized && CameraTracking && Time.unscaledTime >= resumeAt : aligned && !lostAnchor && anchor != null && anchor.trackingState == TrackingState.Tracking && CameraTracking && Time.unscaledTime >= resumeAt;
        Vector3 Access(string id) => mapRoot.TransformPoint(id == "A" ? startLocal : id == "B" ? basinAccess : bagAccess);
        Vector3 ObjectPoint(string id) => mapRoot.TransformPoint(id == "A" ? startLocal : id == "B" ? basinLocal : bagLocal);

        public void ResumeGuidance(TMP_FontAsset font)
        {
            if(!configured)Configure(font);else MakeUi(font);
            enabled=true;
        }

        public void Configure(TMP_FontAsset font)
        {
            if (configured) return;
            configured = true; Application.targetFrameRate = 30;
            session = FindFirstObjectByType<ARSession>(); cameraManager = FindFirstObjectByType<ARCameraManager>();
            var origin = cameraManager.GetComponentInParent<XROrigin>();
            if(localization==null)
            {
            planes = origin.GetComponent<ARPlaneManager>() ?? origin.gameObject.AddComponent<ARPlaneManager>();
            planes.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            anchors = origin.GetComponent<ARAnchorManager>() ?? origin.gameObject.AddComponent<ARAnchorManager>();
            }
            if(presentationRoom && localization==null && gameObject.scene.name=="Roompresent_VPS")throw new InvalidOperationException("Roompresent VPS scene missing localization owner");
            cameraManager.frameReceived += Frame;
            if(localization!=null){qrLocalization=RoompresentQrLocalization.Attach(this);localization.SetAutoFocus(true);}
            pins.gameObject.SetActive(false); path.ClearMesh();
            if(localization!=null && !qrLocalization.Calibrated && RoompresentMapRegistration.TryRead(localization,registrationLandmarks,out var translation,out float yaw))
            {mapRoot.localPosition=translation;mapRoot.localRotation=Quaternion.Euler(0,yaw,0);aligned=true;RefreshSurface();}
            UpdateStartPin();
            MakeUi(font);
        }
        void Frame(ARCameraFrameEventArgs frame) { frameAt = Time.unscaledTime;if(frame.displayMatrix.HasValue){cameraDisplay=frame.displayMatrix.Value;haveCameraDisplay=true;} }
        void MakeUi(TMP_FontAsset font)
        {
            var canvasObject = new GameObject("Small room guidance", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 310;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(412, 915); scaler.matchWidthOrHeight = .5f;
            var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>(); safe.SetParent(canvasObject.transform, false);
            var area = Screen.safeArea; safe.anchorMin = new Vector2(area.xMin/Screen.width, area.yMin/Screen.height); safe.anchorMax = new Vector2(area.xMax/Screen.width, area.yMax/Screen.height); safe.offsetMin = safe.offsetMax = Vector2.zero;
            if(presentationRoom && localization!=null){MakeRoomUi(safe,font);return;}
            var dock = new GameObject("Guidance dock", typeof(RectTransform)).GetComponent<RectTransform>(); dock.SetParent(safe, false);
            dock.anchorMin = dock.anchorMax = new Vector2(.5f, 0); dock.pivot = new Vector2(.5f, 0); dock.anchoredPosition = new Vector2(0, 12); dock.sizeDelta = new Vector2(378, 198);
            var card = PrototypeUi.Panel(dock, "Directions", 0, 0, 378, 198, true, 20);
            heading = PrototypeUi.Text(card, font, "", 16, 12, 346, 28, 20, true, true);
            status = PrototypeUi.Text(card, font, "", 16, 45, 346, 25, 13, true, true);
            detail = PrototypeUi.Text(card, font, "", 16, 75, 346, 66, 13, true); detail.overflowMode = TextOverflowModes.Truncate;
            PrototypeUi.Action(card, font, T("Về JarVis", "Back to JarVis"), 196, 150, 166, 34, true, CloseVenue);
            if(localization!=null)focusLabel=PrototypeUi.Action(card,font,T("Nét: Toàn phòng","Focus: Room"),16,150,166,38,true,localization.ToggleFocus).GetComponentInChildren<TMP_Text>();
        }
        void MakeRoomUi(RectTransform safe,TMP_FontAsset font)
        {
            // Roompresent guidance is camera + spatial pins + active floor arrows only.
            // Keep the existing controller's state fields without a visible overlay.
            var stateRoot=new GameObject("Guidance state",typeof(RectTransform)).GetComponent<RectTransform>();
            stateRoot.SetParent(safe,false);
            heading=PrototypeUi.Text(stateRoot,font,"",0,0,346,28,18,true);
            status=PrototypeUi.Text(stateRoot,font,"",0,0,346,24,13,true);
            detail=PrototypeUi.Text(stateRoot,font,"",0,0,346,24,12,true);
            stateRoot.gameObject.SetActive(false);
            safe.gameObject.AddComponent<RoompresentDeliveryHud>().Configure(this,font);
            safe.gameObject.AddComponent<RoompresentArAssistant>().Configure(this,font);
        }
        void Update()
        {
            if (!configured) return;
            if(presentationRoom&&localization!=null)
            {
                if(HasStartFix)
                {
                    StartAligned=true;
                    if(qrLocalization.Revision>acknowledgedStartRevision)
                    {acknowledgedStartRevision=qrLocalization.Revision;PendingQrSuccess="A";}
                }
                if(verification.Active&&!Trusted){verification.Cancel();PendingQrSuccess=null;DeliveryFeedback=T("Tracking gián đoạn. Bấm xác nhận để quét lại QR đích.","Tracking interrupted. Confirm again and rescan the destination QR.");epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;}
                if(!HasStartFix)
                {
                    if(Time.unscaledTime>=nextTrackingLog)
                    {
                        nextTrackingLog=Time.unscaledTime+4;
                        Debug.Log(LogPrefix+"Recovering same anchor; next="+activeStop+" state="+ARSession.state+" reason="+ARSession.notTrackingReason+" frameAge="+(Time.unscaledTime-frameAt).ToString("F2")+" revision="+qrLocalization.Revision);
                    }
                    wasTracked=false;pins.gameObject.SetActive(false);path.ClearMesh();return;
                }
            }
            if(focusLabel!=null)focusLabel.text=localization.AutoFocus?T("Nét: Tự động","Focus: Auto"):T("Nét: Toàn phòng","Focus: Room");
            if(QrFrame&&!qrLocalization.Trusted)
            {
                if(wasTracked){wasTracked=false;epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;}
                pins.gameObject.SetActive(false);path.ClearMesh();
                WaitingForRoom();return;
            }
            if(localization!=null&&!QrFrame&&!localization.Localized&&wasTracked){wasTracked=false;epoch++;qrHits=0;qrMetricCount=0;qrCandidate=null;}
            if(localization!=null&&!QrFrame&&(!aligned||!localization.Localized))
            {
                pins.gameObject.SetActive(false);path.ClearMesh();WaitingForRoom();return;
            }
            if (localization==null && !paused && Time.unscaledTime >= nextRecovery)
            {
                nextRecovery = Time.unscaledTime + 3;
                // Resume the same AR session. Resetting it would erase the room anchor.
                if (session != null && !session.enabled) session.enabled = true;
                if (cameraManager != null && !cameraManager.enabled) cameraManager.enabled = true;
            }
            if (!aligned&&!QrFrame)
            {
                heading.text = T("Đang căn tại A", "Aligning at A");
                status.text = presentationRoom ? T("Cạnh bàn A · hướng máy về xe B", "Beside table A · face cart B") : T("Trước cửa · hướng máy về chậu B", "At the door · face basin B");
                detail.text = CameraTracking ? T("Giữ máy yên một lát để nhận mặt sàn. Dẫn đường sẽ tự hiện.", "Hold still briefly to detect the floor. Guidance starts automatically.") : T("Đang khởi động tracking · giữ khung cảnh phòng trong camera.", "Starting tracking · keep the room in view.");
                AutoAlign(); return;
            }
            if (localization==null && anchor == null) lostAnchor = true;
            bool tracked = QrFrame ? qrLocalization.Trusted&&CameraTracking : localization!=null ? localization.Localized&&CameraTracking : CameraTracking && anchor != null && anchor.trackingState == TrackingState.Tracking && !lostAnchor;
            if (tracked != wasTracked)
            {
                wasTracked = tracked;
                if (tracked) { resumeAt = Time.unscaledTime + .7f; nextRoute = 0; }
                else { epoch++; qrHits = 0; qrCandidate = null; }
                Debug.Log(LogPrefix + (tracked ? "Tracking recovered; retaining anchor and task." : "Tracking interrupted; same session keeps recovering; task retained."));
            }
            if (!Trusted)
            {
                pins.gameObject.SetActive(false); path.ClearMesh();
                status.color = new Color(1f,.78f,.35f);
                heading.text = T("Dẫn đường tạm chờ", "Directions on hold");
                status.text = T("Tiến độ giao sách vẫn được giữ", "Delivery progress is retained");
                detail.text = lostAnchor ? T("Trở lại điểm xuất phát để tự căn lại.", "Return to the start to realign.") : T("Giữ máy chắc; app đang tự nối lại.", "Hold steady; reconnecting automatically.");
                return;
            }
            pins.gameObject.SetActive(true);
            if ((surface.transform.position-lastSurfacePosition).sqrMagnitude > .0001f || Quaternion.Angle(surface.transform.rotation,lastSurfaceRotation) > .4f) RefreshSurface();
            if (!MissionLoaded || !MissionAccepted) { heading.text = T("Cần quét đủ hai sách", "Scan both books first"); path.ClearMesh(); return; }
            if (MissionComplete && !ReturningToStart)
            {
                path.ClearMesh(); heading.text = T("Đã giao đủ hai sách", "Both books delivered"); status.text = "A1 → B ✓   A2 → C ✓";
                detail.text = T("Nhiệm vụ thử đã lưu trên máy. Bạn có thể về JarVis.", "Local test progress saved. You can return to JarVis."); return;
            }
            if (Time.unscaledTime >= nextRoute) { nextRoute = Time.unscaledTime + .35f; Guide(); }
            heading.text = activeStop == null ? T("Đang tìm đường", "Finding a route") : T("Tới ", "Go to ") + activeStop + (activeStop == "A" ? "" : activeStop == "B" ? " · A1" : " · A2");
            status.text = activeStop == null || !float.IsFinite(remaining) ? T("Chờ đường đi trên sàn", "Waiting for a floor route") : TurnHint(nextDirectionPoint) + " · " + remaining.ToString("F1") + " m";
            status.color = new Color(.35f, 1f, .76f);
            if(ReturningToStart)
            {
                heading.text=AtStart?T("Đã về bàn A","Back at table A"):T("Về bàn A","Return to table A");
                detail.text=AtStart?T("Đã tới điểm xuất phát. Tiến độ giao sách được giữ.","At the start. Delivery progress is retained."):T("Đi theo mũi tên về bàn A. Có thể chọn B hoặc C để tiếp tục.","Follow the arrows back to table A. Select B or C to resume.");
                return;
            }
            bool near = activeStop != null && ObjectDistance(activeStop) <= 1.2f && remaining <= 1.25f;
            detail.text = near ? T("Đặt sách tại đích rồi đưa QR điểm giao vào camera. App tự quét.", "Place the book and show the destination QR. Scanning is automatic.") : presentationRoom ? T("Đi theo mũi tên. B: xe sách trái · C: xe sách phải.", "Follow the arrows. B: left book cart · C: right book cart.") : T("Đi theo mũi tên trên sàn. B: chậu đỏ/xanh · C: túi đen.", "Follow the floor arrows. B: basin · C: black bag.");
            if(presentationRoom)
            {
                string next=activeStop=="B"&&!DeliveredC?"C · A2":activeStop=="C"&&!DeliveredB?"B · A1":null;
                detail.text=near?T("Đặt sách rồi quét QR đích để xác nhận.","Place book; scan destination QR to confirm."):next!=null?T("Tiếp theo: ","Next: ")+next:T("Điểm giao cuối · ","Final delivery · ")+(DeliveredB?"B ✓":"C ✓");
                if(notice!=null)detail.text=notice;
            }
            else if (notice != null) detail.text += "\n" + notice;
            if (presentationRoom ? verification.Active&&PendingQrSuccess==null : near) ReadDestinationQr();
        }
        void WaitingForRoom()
        {
            status.color=new Color(1f,.78f,.35f);
            heading.text=T("Chuẩn bị dẫn đường","Preparing directions");
            status.text=qrLocalization.HasAnchor&&!qrLocalization.Calibrated?T("Chưa có bản lưu vị trí hợp lệ","No valid saved room registration"):T("Giữ máy chắc một chút","Hold the phone steady briefly");
            detail.text=qrLocalization.HasAnchor&&!qrLocalization.Calibrated?T("Mở Bản đồ để kiểm tra cấu hình phòng.","Open Map to check room configuration."):T("Để mặt bàn tại điểm xuất phát trong hình.","Keep the start table in camera view.");
        }
        void LateUpdate(){if(configured&&Trusted)FacePins(pins,cameraManager.transform);}
        public static void FacePins(Transform root,Transform camera)
        {
            foreach(Transform pin in root){var direction=Vector3.ProjectOnPlane(camera.position-pin.position,Vector3.up);if(direction.sqrMagnitude>.001f)pin.rotation=Quaternion.LookRotation(direction,Vector3.up);}
        }
        void AutoAlign()
        {
            if (addingAnchor || paused || Time.unscaledTime < sampleAt) return;
            sampleAt = Time.unscaledTime + .12f;
            if (!CameraTracking || !TryFloor(out var foot) || !TryStartPose(startLocal, basinLocal, foot, cameraManager.transform.forward, out var mapPose)) { stableCount = 0; return; }
            if (stableCount == 0 || Vector3.Distance(foot,stableFoot) > .045f || Quaternion.Angle(mapPose.rotation,stableRotation) > 4)
            { stableCount = 1; stableFoot = foot; stableRotation = mapPose.rotation; stableSince = Time.unscaledTime; return; }
            stableCount++;
            if (stableCount >= 8 && Time.unscaledTime-stableSince >= .8f) CreateAnchor(stableFoot,stableRotation);
        }
        bool TryFloor(out Vector3 foot)
        {
            foot = default; bool found = false; float lowest = float.PositiveInfinity;
            var camera = cameraManager.transform.position;
            foreach (var plane in planes.trackables)
            {
                if (plane.trackingState != TrackingState.Tracking || plane.alignment != PlaneAlignment.HorizontalUp || plane.size.x*plane.size.y < .15f) continue;
                var local = plane.transform.InverseTransformPoint(camera);
                float y = plane.transform.position.y, height = camera.y-y;
                if (height < .75f || height > 2.2f || y >= lowest || !InsideBoundary(plane, new Vector2(local.x,local.z))) continue;
                lowest = y; found = true; foot = new Vector3(camera.x,y,camera.z);
            }
            return found;
        }
        static bool InsideBoundary(ARPlane plane, Vector2 p)
        {
            var boundary = plane.boundary; bool inside = false;
            for (int i=0,j=boundary.Length-1;i<boundary.Length;j=i++)
            { var a=boundary[i]; var b=boundary[j]; if ((a.y>p.y)!=(b.y>p.y) && p.x<(b.x-a.x)*(p.y-a.y)/(b.y-a.y)+a.x) inside=!inside; }
            return inside;
        }
        public static bool TryStartPose(Vector3 start, Vector3 targetB, Vector3 observedFoot, Vector3 cameraForward, out Pose result)
        {
            result = default; var source = Vector3.ProjectOnPlane(targetB-start,Vector3.up); var observed = Vector3.ProjectOnPlane(cameraForward,Vector3.up);
            if (source.sqrMagnitude < .25f || observed.sqrMagnitude < .4f || !Finite(start) || !Finite(targetB) || !Finite(observedFoot) || !Finite(cameraForward)) return false;
            var rotation = Quaternion.LookRotation(observed.normalized,Vector3.up)*Quaternion.Inverse(Quaternion.LookRotation(source.normalized,Vector3.up));
            result = new Pose(observedFoot-rotation*start,rotation); return true;
        }
        static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        async void CreateAnchor(Vector3 foot, Quaternion rotation)
        {
            addingAnchor = true; int request = epoch;
            try
            {
                var added = await anchors.TryAddAnchorAsync(new Pose(foot,rotation));
                if (!added.status.IsSuccess() || added.value == null) { stableCount=0; return; }
                if (this == null || request != epoch || paused || !CameraTracking) { if (anchors != null) anchors.TryRemoveAnchor(added.value); return; }
                anchor = added.value; mapRoot.SetParent(anchor.transform,false); mapRoot.localPosition=-startLocal; mapRoot.localRotation=Quaternion.identity; mapRoot.localScale=Vector3.one;
                aligned=true; RefreshSurface(); nextRoute=0; resumeAt=Time.unscaledTime+.7f;
                Debug.Log(LogPrefix+"Automatic A/facing-B start convention; detected floor + native AR anchor. No absolute localization claimed.");
            }
            catch (Exception e) { Debug.LogWarning(LogPrefix+"Anchor retry: "+e.GetType().Name); stableCount=0; }
            finally { addingAnchor=false; }
        }
        void RefreshSurface() { surface.RemoveData(); surface.AddData(); lastSurfacePosition=surface.transform.position; lastSurfaceRotation=surface.transform.rotation; }
        NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID=surface.agentTypeID, areaMask=NavMesh.AllAreas };
        Vector3 OnFloor(Vector3 p) { p.y=mapRoot.TransformPoint(new Vector3(0,startLocal.y,0)).y; return p; }
        bool Route(Vector3 from, Vector3 to, out NavMeshPath route, out float length)
        {
            route=new NavMeshPath(); length=float.PositiveInfinity;
            // The handset can be held over a cart while scanning its book. Project back
            // to nearby walkable floor (bounded 0.85 m), never draw through cart geometry.
            if (!NavMesh.SamplePosition(OnFloor(from),out var a,presentationRoom?.85f:.4f,Filter) || !NavMesh.SamplePosition(OnFloor(to),out var b,.35f,Filter) ||
                !NavMesh.CalculatePath(a.position,b.position,Filter,route) || route.status!=NavMeshPathStatus.PathComplete) return false;
            length=0; for(int i=1;i<route.corners.Length;i++) length+=Vector3.Distance(route.corners[i-1],route.corners[i]); return true;
        }
        void Guide()
        {
            
            if (activeStop==null || activeStop=="B" && DeliveredB || activeStop=="C" && DeliveredC)
            {
                if(presentationRoom && (RoompresentDeliveryMission.Current.preferredStop=="B"&&!DeliveredB||RoompresentDeliveryMission.Current.preferredStop=="C"&&!DeliveredC))activeStop=RoompresentDeliveryMission.Current.preferredStop;
                else if(DeliveredB) activeStop="C";
                else if(DeliveredC) activeStop="B";
                else
                {
                    var start=cameraManager.transform.position;
                    Route(start,Access("B"),out _,out float ab); Route(start,Access("C"),out _,out float ac);
                    Route(Access("B"),Access("C"),out _,out float bc); Route(Access("C"),Access("B"),out _,out float cb);
                    if(float.IsPositiveInfinity(ab+bc) && float.IsPositiveInfinity(ac+cb)) { path.ClearMesh(); remaining=float.PositiveInfinity; return; }
                    activeStop=ab+bc<=ac+cb?"B":"C";
                }
                Debug.Log(LogPrefix+"Optimal next stop "+activeStop);
            }
            if (Route(cameraManager.transform.position,Access(activeStop),out var route,out remaining))
            {
                if(routeMissing)Debug.Log(LogPrefix+"Floor route recovered to "+activeStop);
                routeMissing=false;
                if(ReturningToStart&&remaining<=.65f)path.ClearMesh();
                else path.GenerateValidatedPolyline(route.corners.ToList(),Vector3.up);
                notice=null;
                nextDirectionPoint=Access(activeStop);
                foreach(var corner in route.corners)if(Vector3.ProjectOnPlane(corner-cameraManager.transform.position,Vector3.up).sqrMagnitude>.16f){nextDirectionPoint=corner;break;}
            }
            else { if(!routeMissing)Debug.Log(LogPrefix+"No floor route to "+activeStop+"; tracking still="+Trusted);routeMissing=true;path.ClearMesh(); notice=T("Đang nối lại đường đi; ở trong lối sàn trống.","Reconnecting the route; stay in the clear floor aisle."); }
        }
        string TurnHint(Vector3 target)
        {
            var forward=Vector3.ProjectOnPlane(cameraManager.transform.forward,Vector3.up); var direction=Vector3.ProjectOnPlane(target-cameraManager.transform.position,Vector3.up);
            float angle=Vector3.SignedAngle(forward,direction,Vector3.up);
            if(Mathf.Abs(angle)>120) return T("Quay người lại","Turn around");
            if(angle>35) return T("Hướng sang phải","Turn right");
            if(angle< -35) return T("Hướng sang trái","Turn left");
            return T("Đi theo mũi tên","Follow the arrows");
        }
        float ObjectDistance(string stop) => Vector3.ProjectOnPlane(cameraManager.transform.position-ObjectPoint(stop),Vector3.up).magnitude;
        async void ReadDestinationQr()
        {
            if(destinationAnchoring)return;
            if(decode!=null && decode.IsCompleted)
            {
                var done=decode;decode=null;
                if(done.IsFaulted){_=done.Exception;return;}
                var detection=done.Result;
                var result=detection.result;
                if(result==null&&presentationRoom&&decodeEpoch==epoch&&Trusted)result=qrLocalization.RecoverOnPlane(detection,qrCapture,qrWidth,qrHeight);
                if(decodeEpoch==epoch && Trusted && result!=null)
                {
                    if(qrCandidate==result.Text && Time.unscaledTime-qrAt<4)qrHits++;else{qrCandidate=result.Text;qrHits=1;destinationReadAt=-1;qrMetricCount=0;}
                    qrAt=Time.unscaledTime;
                    if(qrHits>=2)
                    {
                        bool completed=false;
                        string completedStop=activeStop;
                        if(presentationRoom&&!verification.WaitingForProduct&&RoompresentDeliveryRecord.Destination(result.Text)==verification.Stop)
                        {
                            if(destinationReadAt<0)destinationReadAt=Time.unscaledTime;
                            bool measuredOk=qrLocalization.TryMeasureQr(result,qrCapture,qrWidth,qrHeight,out var measured);
                            if(measuredOk)
                            {
                                if(qrMetricCount==0||Vector3.Distance(measured.position,destinationPose.position)>.05f||Quaternion.Angle(measured.rotation,destinationPose.rotation)>4)
                                {qrMetricCount=1;destinationPose=measured;destinationSince=Time.unscaledTime;}
                                else qrMetricCount++;
                            }
                            else qrMetricCount=0;
                            bool metricReady=qrMetricCount>=4&&Time.unscaledTime-destinationSince>=.8f;
                            if(Time.unscaledTime>=nextDestinationLog)
                            {
                                nextDestinationLog=Time.unscaledTime+3;
                                Debug.Log(LogPrefix+"Read QR "+verification.Stop+"; distance="+ObjectDistance(verification.Stop).ToString("F2")+" tracking="+Trusted+" plane="+measuredOk+" stable="+qrMetricCount);
                            }
                            if(!metricReady&&Time.unscaledTime-destinationReadAt<1.5f)
                            {DeliveryFeedback=T("Đã đọc QR · giữ mã và mặt kệ trong hình","QR read · keep the code and shelf surface in view");return;}
                            if(metricReady)
                            {
                                destinationAnchoring=true;int generation=epoch;string stop=verification.Stop;
                                try{DestinationPositionUpdated=await qrLocalization.AttachDestinationReference(stop,measured);}finally{destinationAnchoring=false;}
                                if(this==null||generation!=epoch||!verification.Active)return;
                            }
                            // Correct QR + trusted nearby pose can verify delivery even if a
                            // textureless shelf has no measured plane. Never invent a new pose.
                            if(!DestinationPositionUpdated)Debug.Log(LogPrefix+"QR identity verified; retaining existing trusted room reference (no metric refinement).");
                        }
                        if(presentationRoom)
                        {
                            var outcome=verification.Observe(RoompresentDeliveryMission.Current,result.Text,"RearCamera",Trusted,ObjectDistance(verification.Stop));
                            DeliveryFeedback=outcome==DeliveryScanOutcome.WrongDestination?T("Sai điểm giao. Cần QR ","Wrong destination. Scan QR ")+verification.Stop:
                                outcome==DeliveryScanOutcome.WrongProduct?T("Sai sản phẩm. Cần ","Wrong product. Scan ")+verification.ProductName:
                                outcome==DeliveryScanOutcome.Unavailable?T("Đã đọc mã · vị trí đang lệch (","Code read · position mismatch (")+ObjectDistance(verification.Stop).ToString("F1")+T(" m). Nếu đang đúng kệ, bấm Quét A để căn lại."," m). If you are at the right cart, use Scan A to realign."):null;
                            completed=outcome==DeliveryScanOutcome.Completed;
                            if(outcome==DeliveryScanOutcome.DestinationVerified){PendingQrSuccess=verification.Stop;epoch++;qrCandidate=null;Debug.Log(LogPrefix+"Destination verified; next scan book "+verification.ProductName);}
                        }
                        else completed=SmallRoomDeliveryMission.Current.Confirm(result.Text,activeStop,Trusted,ObjectDistance(activeStop));
                        if(completed)
                        {
                            if(presentationRoom){CompleteDeliveryHandoff();}else SmallRoomDeliveryMission.Save();Debug.Log(LogPrefix+"Destination QR confirmed at "+completedStop);
                            notice=T("Đã xác nhận giao sách","Book delivery confirmed");
                            if(!presentationRoom){activeStop=null;nextRoute=0;epoch++;}
#if UNITY_ANDROID && !UNITY_EDITOR
                            Handheld.Vibrate();
#endif
                        }
                        else if(!presentationRoom)notice=T("Chưa đúng QR của đích đang đi.","Use the QR for the active destination.");
                        qrHits=0;
                    }
                }
            }
            if(!Trusted || !haveCameraDisplay || decode!=null || Time.unscaledTime<nextQr || !cameraManager.TryAcquireLatestCpuImage(out var image))return;
            nextQr=Time.unscaledTime+.25f;
            using(image)
            {
                float aspect=cameraManager.GetComponent<Camera>().aspect;int h=aspect<=1?1440:Mathf.RoundToInt(1440/aspect),w=Mathf.RoundToInt(h*aspect);var gray=new byte[w*h];var plane=image.GetPlane(0);
                if(!LabCameraLuminance.Copy(plane.data,image.width,image.height,plane.rowStride,plane.pixelStride,cameraDisplay,gray,w,h))return;
                for(int y=0;y<h/2;y++)for(int x=0;x<w;x++){int a=y*w+x,b=(h-y-1)*w+x;byte temp=gray[a];gray[a]=gray[b];gray[b]=temp;}
                qrCapture=new Pose(cameraManager.transform.position,cameraManager.transform.rotation);qrWidth=w;qrHeight=h;
                decodeEpoch=epoch;
                bool productStep=presentationRoom&&verification.WaitingForProduct;
                decode=Task.Run(()=>productStep?new RoompresentQrLocalization.QrFrame{result=RoompresentDeliveryVerification.DecodeProduct(gray,w,h)}:RoompresentQrLocalization.InspectFrame(gray,w,h));
            }
        }
        void OnApplicationPause(bool value) { paused=value;verification.Cancel();epoch++;stableCount=0;qrHits=0;qrMetricCount=0;qrCandidate=null;wasTracked=false;resumeAt=Time.unscaledTime+.7f; }
        void OnDestroy()
        {
            epoch++;if(cameraManager!=null)cameraManager.frameReceived-=Frame;
            if(decode!=null)_=decode.ContinueWith(t=>{if(t.IsFaulted)_=t.Exception;});
        }
    }
}
