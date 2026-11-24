using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Cdm.Figma.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZXing;

namespace JarVis.Warehouse.UI
{
    // Camera decoding never mutates stock: all warehouse information comes from OMS.
    public sealed class ImportedCameraScanner : MonoBehaviour
    {
        WebCamTexture cameraTexture;
        RawImage preview;
        GameObject normalTarget;
        RoompresentCodeFrame productFrame;
        TMP_Text status;
        TMP_InputField manual;
        UnityEngine.UI.Button lookupButton;
        UnityEngine.UI.Button againButton, settingsButton;
        Task<Result> decode;
        Color32[] pixels;
        bool configured, querying, appPaused, scanLocked, lastEnglish;
        float autoNextScanAt;
        float lastCameraFrameAt, nextCameraRecovery;
        float nextDecode, guidanceAt, candidateAt;string candidateCode;int candidateHits;
        int session, decodeSession;
        string messageVi, messageEn, identityEndpoint, identityToken, identityEmployee;
        readonly ScannerCameraLease cameraLease = new ScannerCameraLease();
        readonly FrameDecoder decoder = new FrameDecoder();
        public ScannerState State { get; private set; }
        public BarcodeScanResult LastScan { get; private set; }
        public bool ScanLocked => scanLocked;

        public void Configure(GameObject root)
        {
            if (configured) return;
            var nodes = root.GetComponentsInChildren<FigmaNode>(true);
            var box = nodes.FirstOrDefault(n => n.nodeName == "viewfinder-box");
            var panel = nodes.FirstOrDefault(n => n.nodeName == "bottom-scans-panel");
            var font = root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault()?.font;
            if (box == null || panel == null || font == null)
            { Debug.LogError("[JarVis] Scanner frame is missing required imported nodes."); return; }
            preview = new GameObject("Live camera", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            preview.transform.SetParent(box.transform, false);
            preview.transform.SetAsFirstSibling(); preview.raycastTarget = false;
            preview.color = Color.black;
            if (box.GetComponent<RectMask2D>() == null) box.gameObject.AddComponent<RectMask2D>();
            var targetFrame=nodes.FirstOrDefault(n=>n.nodeName=="scan-target-frame");
            normalTarget=targetFrame==null?null:targetFrame.gameObject;
            var missionFrame=new GameObject("Product barcode frame",typeof(RectTransform),typeof(CanvasRenderer),typeof(RoompresentCodeFrame));missionFrame.transform.SetParent(box.transform,false);
            var frameRect=(RectTransform)missionFrame.transform;frameRect.anchorMin=frameRect.anchorMax=frameRect.pivot=new Vector2(.5f,.5f);
            productFrame=missionFrame.GetComponent<RoompresentCodeFrame>();productFrame.SetBarcode(true);missionFrame.SetActive(false);
            var tag=MakeText(missionFrame.transform,font,"Barcode instruction",new Vector2(0,30),new Vector2(300,26));tag.fontSize=13;tag.alignment=TextAlignmentOptions.Center;tag.color=Color.white;tag.text="BARCODE · A1 / A2";
            if(targetFrame!=null){if(targetFrame.GetComponent<RectMask2D>()==null)targetFrame.gameObject.AddComponent<RectMask2D>();targetFrame.gameObject.AddComponent<ScannerSweep>();}
            foreach (var node in nodes)
            {
                if (node.nodeName == "animated-scan-line") node.gameObject.SetActive(false);
                if (node.nodeName == "scan-results") node.gameObject.SetActive(false);
                // Unity's webcam API does not expose a torch control: do not show a fake button.
                if (node.nodeName == "flash-icon-wrap") node.gameObject.SetActive(false);
                if (node.nodeId == "5:353")
                {
                    var heading=node.GetComponent<TMP_Text>();
                    if(heading!=null)heading.text="Quét & tra cứu";
                }
            }
            status = MakeText(panel.transform, font, "Camera status", new Vector2(20,-116), new Vector2(372,110));
            status.fontSize = 13;
            var entry = new GameObject("Manual code", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TMP_InputField));
            entry.transform.SetParent(panel.transform,false);
            Place(entry.GetComponent<RectTransform>(),new Vector2(20,-70),new Vector2(258,40));
            entry.GetComponent<UnityEngine.UI.Image>().color = new Color(.94f,.96f,1);
            var label = MakeText(entry.transform,font,"Code",new Vector2(8,-4),new Vector2(242,32));
            label.fontSize = 14;
            manual = entry.GetComponent<TMP_InputField>();
            manual.textViewport = entry.GetComponent<RectTransform>(); manual.textComponent = label;
            manual.characterLimit = 160; manual.lineType = TMP_InputField.LineType.SingleLine;
            var placeholder = MakeText(entry.transform,font,"Placeholder",new Vector2(8,-4),new Vector2(242,32));
            placeholder.text = "Nhập mã QR / barcode"; placeholder.color = Color.gray; placeholder.fontSize=13;
            manual.placeholder = placeholder;
            var action = new GameObject("Lookup code",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));
            action.transform.SetParent(panel.transform,false);
            Place(action.GetComponent<RectTransform>(),new Vector2(286,-70),new Vector2(106,40));
            var actionImage = action.GetComponent<UnityEngine.UI.Image>(); actionImage.color=new Color(.06f,.32f,.82f);
            lookupButton=action.GetComponent<UnityEngine.UI.Button>(); lookupButton.targetGraphic=actionImage;
            var actionLabel=MakeText(action.transform,font,"Label",new Vector2(4,-4),new Vector2(98,32));
            actionLabel.text="Tra mã"; actionLabel.fontSize=14; actionLabel.color=Color.white; actionLabel.alignment=TextAlignmentOptions.Center;
            lookupButton.onClick.AddListener(() => Submit(manual.text));
            manual.onSubmit.AddListener(Submit);
            againButton = MakeAction(panel.transform, font, "Scan again", RestartScan);
            settingsButton = MakeAction(panel.transform, font, "Camera settings", OpenCameraSettings);
            settingsButton.gameObject.SetActive(false);
            var manualLink=nodes.FirstOrDefault(n=>n.nodeId=="5:354");
            if(manualLink!=null)
            {
                var hit=manualLink.GetComponent<TMP_Text>(); if(hit!=null)hit.raycastTarget=true;
                var button=manualLink.GetComponent<UnityEngine.UI.Button>() ?? manualLink.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=hit;
                button.onClick.AddListener(()=>manual.ActivateInputField());
            }
            configured=true;
            if(isActiveAndEnabled)StartCoroutine(StartCamera());
        }
        static void Place(RectTransform rect, Vector2 position, Vector2 size)
        { rect.anchorMin=rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size; }
        static TMP_Text MakeText(Transform parent,TMP_FontAsset font,string name,Vector2 position,Vector2 size)
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(parent,false); Place(text.rectTransform,position,size); text.font=font;
            text.color=new Color(.12f,.18f,.28f); text.raycastTarget=false; text.richText=false;
            text.textWrappingMode=TextWrappingModes.Normal; text.overflowMode=TextOverflowModes.Ellipsis; return text;
        }
        static UnityEngine.UI.Button MakeAction(Transform parent, TMP_FontAsset font, string name, UnityEngine.Events.UnityAction callback)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); Place(rect, Vector2.zero, new Vector2(366, 48));
            var button = rect.GetComponent<UnityEngine.UI.Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(callback);
            var text = MakeText(rect, font, "Label", Vector2.zero, new Vector2(366, 48)); text.alignment = TextAlignmentOptions.Center; text.fontSize = 14;
            return button;
        }
        void Message(ScannerState state, string vi, string en)
        {
            State = state; messageVi = vi; messageEn = en; RefreshText();
            if (againButton != null) againButton.interactable = !querying && state != ScannerState.Starting && state != ScannerState.RequestingPermission;
        }
        void RefreshText()
        {
            lastEnglish = ImportedAppLocalization.IsEnglish;
            if (status != null) status.text = lastEnglish ? messageEn : messageVi;
            if (againButton != null) againButton.GetComponentInChildren<TMP_Text>(true).text = lastEnglish ? "Scan next / retry camera" : "Quét tiếp / mở lại camera";
            if (settingsButton != null) settingsButton.GetComponentInChildren<TMP_Text>(true).text = lastEnglish ? "Camera settings" : "Cài đặt camera";
        }
        void OnEnable(){if(configured)RestartScan();}
        public void RestartScan()
        {
            if (!configured || !isActiveAndEnabled || querying || appPaused || RoompresentDeliveryMission.AwaitingAcknowledgement) return;
            session++; StopAllCoroutines(); StopCamera(); nextDecode=0;lastCameraFrameAt=Time.unscaledTime;candidateCode=null;candidateHits=0;scanLocked = false; LastScan = null; manual.text = "";
            identityEndpoint = OmsMobileSession.BaseUrl; identityToken = OmsMobileSession.AccessToken; identityEmployee = OmsMobileSession.EmployeeCode;
            settingsButton.gameObject.SetActive(false); StartCoroutine(StartCamera());
        }
        IEnumerator StartCamera()
        {
            int openingSession = session;
#if UNITY_EDITOR
            if (Application.isEditor) { Message(ScannerState.Idle, "Kiểm tra Editor: camera không được mở. Có thể nhập mã.", "Editor review: camera disabled. Manual entry is available."); yield break; }
#else
            Message(ScannerState.RequestingPermission, "Cho phép camera để quét mã. Ảnh không được tải lên máy chủ.", "Allow camera access to scan codes. Camera frames are not uploaded.");
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                bool answered=false;
                var callbacks=new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _=>answered=true;
                callbacks.PermissionDenied += _=>answered=true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera,callbacks);
                float deadline=Time.realtimeSinceStartup+20;
                while(openingSession == session && !answered && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && Time.realtimeSinceStartup<deadline)yield return null;
            }
            bool permission=UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#else
            if(!Application.HasUserAuthorization(UserAuthorization.WebCam))yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            bool permission=Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
            if (openingSession != session || appPaused) yield break;
            if(!permission){settingsButton.gameObject.SetActive(true);Message(ScannerState.Error,"Chưa có quyền camera. Cấp quyền trong Cài đặt hoặc nhập mã bên dưới.","Camera permission is unavailable. Allow it in Settings or enter the code below.");yield break;}
            Message(ScannerState.Starting, "Đang mở camera sau…", "Starting rear camera…");
            if (!cameraLease.Acquire(this)) { Message(ScannerState.Error, "Camera đang được dùng. Đóng màn camera khác rồi thử lại.", "Camera is in use. Close the other camera screen and retry."); yield break; }
            yield return null;
            float releaseDeadline = Time.realtimeSinceStartup + 3;
            while (openingSession == session && !cameraLease.CameraStopped && Time.realtimeSinceStartup < releaseDeadline) yield return null;
            if (openingSession != session) yield break;
            if (!cameraLease.CameraStopped) { StopCamera(); Message(ScannerState.Error, "Camera AR chưa dừng. Quay lại rồi mở scanner lại.", "AR camera has not stopped. Go back and reopen the scanner."); yield break; }
            var devices=WebCamTexture.devices;
            var device=devices.FirstOrDefault(d=>!d.isFrontFacing);
            if(string.IsNullOrEmpty(device.name)){StopCamera();Message(ScannerState.Error,"Không tìm thấy camera sau. Nhập mã bên dưới để tra cứu.","No rear camera found. Enter the code below to look it up.");yield break;}
            bool started = false;
            try { cameraTexture=new WebCamTexture(device.name,1280,720,30); cameraTexture.autoFocusPoint = new Vector2(.5f,.5f); preview.texture=cameraTexture; preview.color=Color.white; cameraTexture.Play(); started = true; }
            catch (Exception) { StopCamera(); Message(ScannerState.Error,"Camera không mở được. Đóng ứng dụng camera khác rồi thử lại.","Camera could not start. Close other camera apps and retry."); }
            if (!started) yield break;
            float timeout=Time.realtimeSinceStartup+10;
            while(openingSession == session && cameraTexture!=null && (!cameraTexture.didUpdateThisFrame || cameraTexture.width<=16) && Time.realtimeSinceStartup<timeout)yield return null;
            if(cameraTexture==null || openingSession != session)yield break;
            if(cameraTexture.width<=16 || !cameraTexture.isPlaying || !cameraTexture.didUpdateThisFrame){StopCamera();Message(ScannerState.Error,"Camera không khởi động. Bấm mở lại camera hoặc nhập mã.","Camera did not start. Retry the camera or enter a code.");yield break;}
            lastCameraFrameAt=Time.unscaledTime;
            guidanceAt = Time.unscaledTime + 10;
            Message(ScannerState.Scanning,RoompresentDeliveryMission.Armed?"Đưa barcode A1 hoặc A2 vào khung ngang.":"Đưa một mã QR / barcode vào camera.",RoompresentDeliveryMission.Armed?"Place the A1 or A2 barcode inside the wide frame.":"Show one QR code or barcode to the camera.");
        }
        void Update()
        {
            bool productTask=RoompresentDeliveryMission.Armed;
            if(productFrame!=null)productFrame.gameObject.SetActive(productTask);
            if(normalTarget!=null)normalTarget.SetActive(!productTask);
            if (!configured) return;
            if (autoNextScanAt > 0 && Time.unscaledTime >= autoNextScanAt && (SmallRoomDeliveryMission.Armed || RoompresentDeliveryMission.Armed))
            { autoNextScanAt=0; scanLocked=false; LastScan=null; candidateCode=null; candidateHits=0; manual.text=""; Message(ScannerState.Scanning,"Quét barcode quyển sách còn lại.","Scan the other book barcode."); }
            if (lastEnglish != ImportedAppLocalization.IsEnglish) RefreshText();
            if ((querying || LastScan != null) && (identityEndpoint != OmsMobileSession.BaseUrl || identityToken != OmsMobileSession.AccessToken || identityEmployee != OmsMobileSession.EmployeeCode || identityToken != null && !OmsMobileSession.IsReady))
            {
                session++; StopAllCoroutines(); querying = false; scanLocked = true; LastScan = null; manual.text = ""; lookupButton.interactable = true;
                StopCamera(); Message(ScannerState.Result,"Phiên đăng nhập đã thay đổi. Đăng nhập và quét lại.","Your session changed. Sign in and scan again.");
            }
            if(appPaused)return;
            if(cameraTexture!=null&&cameraTexture.didUpdateThisFrame)lastCameraFrameAt=Time.unscaledTime;
            // Recover a stopped/stalled feed, not an unreadable code. Never dismiss a success popup.
            if(State==ScannerState.Scanning&&!querying&&!scanLocked&&!RoompresentDeliveryMission.AwaitingAcknowledgement&&Time.unscaledTime>=nextCameraRecovery&&
                (cameraTexture==null||!cameraTexture.isPlaying||Time.unscaledTime-lastCameraFrameAt>5))
            {
                nextCameraRecovery=Time.unscaledTime+12;
                Debug.Log("[JarVisScanner] Reopening stale rear-camera feed; task data retained.");
                RestartScan();return;
            }
            if(cameraTexture==null || !cameraTexture.isPlaying || cameraTexture.width<=16)return;
            var rect=preview.rectTransform; var parent=(RectTransform)rect.parent;
            int rotation=cameraTexture.videoRotationAngle;
            bool swapped=rotation%180!=0;
            float orientedW=swapped?cameraTexture.height:cameraTexture.width;
            float orientedH=swapped?cameraTexture.width:cameraTexture.height;
            float scale=Mathf.Max(parent.rect.width/orientedW,parent.rect.height/orientedH);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
            rect.sizeDelta=new Vector2(cameraTexture.width*scale,cameraTexture.height*scale);
            rect.localEulerAngles=new Vector3(0,0,-rotation);
            preview.uvRect=cameraTexture.videoVerticallyMirrored?new Rect(0,1,1,-1):new Rect(0,0,1,1);
            if(decode!=null)
            {
                if(!decode.IsCompleted)return;
                if(decode.IsFaulted)
                {
                    _=decode.Exception;
                    decode=null;if(decodeSession==session&&!scanLocked)Message(ScannerState.Scanning,"Chưa đọc được mã. Giữ máy yên và thử lại.","Could not read the code. Hold the phone steady and try again.");return;
                }
                var result=decode.Result;decode=null;
                if(result!=null && decodeSession == session && !querying && !scanLocked)
                {
                    if(JarvisLabMission.Armed&&!SmallRoomDeliveryMission.Armed&&!RoompresentDeliveryMission.Armed&&!JarvisTestProduct.Matches(result.Text))
                    { candidateCode=null;candidateHits=0;Message(ScannerState.Scanning,"Chưa đúng hộp sữa. Quét barcode 4710043004342, không quét QR quảng cáo.","Wrong item. Scan barcode 4710043004342, not the marketing QR."); }
                    else
                    {
                        if(candidateCode==result.Text&&Time.unscaledTime-candidateAt<4f)candidateHits++;else{candidateCode=result.Text;candidateHits=1;}
                        candidateAt=Time.unscaledTime;
                        if(candidateHits>=2){manual.SetTextWithoutNotify(result.Text);Accept(result.Text,result.BarcodeFormat.ToString(),"RearCamera");}
                        else Message(ScannerState.Scanning,"Đã thấy mã · giữ yên để xác nhận…","Code found · hold still to confirm…");
                    }
                }
            }
            if(querying || scanLocked || Time.unscaledTime<nextDecode || !cameraTexture.didUpdateThisFrame)return;
            if (Time.unscaledTime > guidanceAt) { guidanceAt = Time.unscaledTime + 15; Message(ScannerState.Scanning,"Chưa thấy mã: tăng ánh sáng, giữ máy yên, đưa mã gần hơn. Có thể nhập thủ công.","No code yet: improve lighting, hold steady and move closer. You can also enter it manually."); }
            nextDecode=Time.unscaledTime+.3f;
            if(pixels!=null && pixels.Length!=cameraTexture.width*cameraTexture.height)pixels=null;
            try { pixels=cameraTexture.GetPixels32(pixels); }
            catch (Exception) { StopCamera(); Message(ScannerState.Error,"Không đọc được ảnh camera. Bấm mở lại camera.","Camera frames are unavailable. Retry the camera."); return; }
            int width=cameraTexture.width,height=cameraTexture.height;
            // The worker owns this frame. A camera restart may resize/reuse the main buffer
            // before the previous job finishes; never let it read pixels being overwritten.
            var frame=(Color32[])pixels.Clone();
            bool booksOnly = SmallRoomDeliveryMission.Armed || RoompresentDeliveryMission.Armed;
            decodeSession = session;
            decode=Task.Run(()=>decoder.Decode(frame,width,height,booksOnly));
        }
        public static Result Decode(Color32[] frame,int width,int height,bool booksOnly = false)
        {
            return new FrameDecoder().Decode(frame, width, height, booksOnly);
        }
        sealed class FrameDecoder
        {
            readonly BarcodeReaderGeneric reader = new BarcodeReaderGeneric { AutoRotate = true };
            byte[] luminance;
            public FrameDecoder() { reader.Options.TryHarder = true; reader.Options.TryInverted = true; }
            public Result Decode(Color32[] frame, int width, int height, bool booksOnly = false)
            {
                // Restrict mission scans to EAN-13; avoid searching unrelated formats on every frame.
                reader.Options.PossibleFormats = booksOnly ? new[] { BarcodeFormat.EAN_13 } : null;
                if (frame == null || width <= 0 || height <= 0 || (long)width * height > 4194304 || frame.Length != (long)width * height) return null;
                if (luminance == null || luminance.Length != frame.Length) luminance = new byte[frame.Length];
                for (int index = 0; index < frame.Length; index++) luminance[index] = (byte)((frame[index].r * 306 + frame[index].g * 601 + frame[index].b * 117) >> 10);
                var result = reader.Decode(new RGBLuminanceSource(luminance, width, height, RGBLuminanceSource.BitmapFormat.Gray8));
                if (result != null) return result;
                // Average speckle along 1D bars, retaining bar widths and checksum validation.
                var filtered = new byte[luminance.Length];
                foreach (bool vertical in new[] { true, false })
                {
                    int lines = vertical ? width : height, length = vertical ? height : width;
                    for (int line = 0; line < lines; line++)
                    {
                        int sum = 0, count = 0;
                        for (int p = 0; p < length; p++)
                        {
                            if (p == 0) for (int k = 0; k <= Math.Min(4, length - 1); k++) { sum += luminance[vertical ? k * width + line : line * width + k]; count++; }
                            else
                            {
                                int enter = p + 4, leave = p - 5;
                                if (enter < length) { sum += luminance[vertical ? enter * width + line : line * width + enter]; count++; }
                                if (leave >= 0) { sum -= luminance[vertical ? leave * width + line : line * width + leave]; count--; }
                            }
                            filtered[vertical ? p * width + line : line * width + p] = (byte)(sum / count);
                        }
                    }
                    result = reader.Decode(new RGBLuminanceSource(filtered, width, height, RGBLuminanceSource.BitmapFormat.Gray8));
                    if (result != null) return result;
                }
                return null;
            }
        }
        void ContinueMissionScan()
        {
            if(!isActiveAndEnabled)return;
            session++;scanLocked=false;LastScan=null;candidateCode=null;candidateHits=0;
            manual.SetTextWithoutNotify("");autoNextScanAt=0;
            // Pause closes the webcam while the success popup remains visible. OK must
            // reacquire the feed as well as unlock decoding when returning later.
            if(cameraTexture==null||!cameraTexture.isPlaying){RestartScan();return;}
            Message(ScannerState.Scanning,"Quét barcode quyển còn lại.","Scan the other book barcode.");
        }
        public void Submit(string code)
        {
            if(querying||RoompresentDeliveryMission.AwaitingAcknowledgement)return;
            Accept(code, "Manual", "ManualEntry");
        }
        void Accept(string code, string format, string source)
        {
            if (querying || source == "RearCamera" && scanLocked) return;
            scanLocked = true;
            LastScan = ScannerValidation.Validate(code, format, source);
            identityEndpoint = OmsMobileSession.BaseUrl; identityToken = OmsMobileSession.AccessToken; identityEmployee = OmsMobileSession.EmployeeCode;
            if (LastScan.ValidationStatus != "Valid")
            { Message(ScannerState.Result,"Mã không được hỗ trợ. Dùng SKU / barcode tối đa 160 ký tự, không có khoảng trắng đầu/cuối, URL hoặc JSON. Bấm Quét tiếp để thử mã khác.","Unsupported code. Use a SKU / barcode up to 160 characters, without outer spaces, URLs or JSON. Tap Scan next to try another code."); return; }
            if (SmallRoomDeliveryMission.HandleScan(code, source, GetComponentInParent<ImportedFigmaNavigation>(), out var smallVi, out var smallEn))
            { Message(ScannerState.Result,smallVi,smallEn); if(SmallRoomDeliveryMission.Armed)autoNextScanAt=Time.unscaledTime+.9f; return; }
            if (RoompresentDeliveryMission.HandleScan(code, source, GetComponentInParent<ImportedFigmaNavigation>(), out var trialVi, out var trialEn, ContinueMissionScan))
            { Message(ScannerState.Result, trialVi, trialEn); return; }
            if(LastScan.Context == ScannerContext.WarehouseCheckpoint)
            {
                var navigation=FindAnyObjectByType<ImportedFigmaNavigation>();
                if(navigation!=null&&navigation.OpenMapFromMarker(code))return;
                LastScan.ErrorCode = "UNKNOWN_CHECKPOINT";
                Message(ScannerState.Result,"Mốc chưa được khai báo trong map kho. Bấm Quét tiếp để thử lại.","Checkpoint is not configured in the warehouse map. Tap Scan next to retry.");
                return;
            }
            if(JarvisTestProduct.Matches(LastScan.NormalizedIdentifier))
            {
                LastScan.ErrorCode="OWNER_TEST_RECORD";
                Message(ScannerState.Result,"Đã nhận hộp sữa · xem thông tin trong cửa sổ.","Milk recognised · product details opened.");
                if(JarvisLabMission.AcceptProduct(LastScan.NormalizedIdentifier)){StopCamera();JarvisLabLauncher.Open();}
                else JarvisLabMission.ShowProduct(GetComponentInParent<ImportedFigmaNavigation>());
                return;
            }
            var catalogue=JarvisLocalCatalogue.Find(LastScan.NormalizedIdentifier);
            if(catalogue!=null){LastScan.ErrorCode="OWNER_CATALOGUE";Message(ScannerState.Result,"Đã quét "+catalogue.name,"Scanned "+catalogue.nameEn);JarvisLocalCatalogue.Show(GetComponentInParent<ImportedFigmaNavigation>(),catalogue);return;}
            if(!OmsMobileSession.IsReady){LastScan.ErrorCode = "SIGN_IN_REQUIRED";Message(ScannerState.Result,"Đã đọc: "+code+"\nCần đăng nhập để tra hàng thật. Chưa ghi giao dịch kho.","Read: "+code+"\nSign in to look up real stock. No stock transaction was recorded.");return;}
            StartCoroutine(Lookup(LastScan,session));
        }
        IEnumerator Lookup(BarcodeScanResult scan,int requestSession)
        {
            querying=true;lookupButton.interactable=false;Message(ScannerState.LookingUp,"Đang tìm hàng trong kho…","Looking up warehouse stock…");
            OmsMobileApi.InventoryList result=null;
            yield return OmsMobileApi.SearchInventory(scan.NormalizedIdentifier,value=>result=value,_=>{});
            if(requestSession!=session || !ScannerValidation.SameIdentity(identityEndpoint,identityToken,identityEmployee))yield break;
            querying=false;lookupButton.interactable=true;
            if(result==null||result.items==null){scan.ErrorCode="LOOKUP_FAILED";Message(ScannerState.Result,"Đã đọc: "+scan.RawValue+"\nChưa tra được hàng. Kiểm tra mạng và đăng nhập, rồi bấm Tra mã để thử lại.","Read: "+scan.RawValue+"\nLookup unavailable. Check connection and sign-in, then tap Look up to retry.");yield break;}
            PresentReply(scan, result);
        }
        public void PresentReply(BarcodeScanResult scan, OmsMobileApi.InventoryList result)
        {
            if (scan == null) return;
            var rows = ScannerValidation.ExactMatches(result, scan.NormalizedIdentifier, out bool lotOnly);
            if(rows.Length==0){scan.ErrorCode="NOT_FOUND";Message(ScannerState.Result,"Không tìm thấy mã trong kho: "+scan.RawValue+"\nKhông tự tạo sản phẩm. Kiểm tra mã hoặc bấm Quét tiếp.","No warehouse match: "+scan.RawValue+"\nNo product was created. Check the code or tap Scan next.");return;}
            var products = rows.Select(row=>row.productId ?? row.sku).Distinct().ToArray();
            if (products.Length != 1 || string.IsNullOrWhiteSpace(products[0])) { scan.ErrorCode="AMBIGUOUS_MATCH"; Message(ScannerState.Result,"Mã khớp nhiều sản phẩm. Cần quản lý kiểm tra; chưa chọn hàng hoặc ghi giao dịch.","Code matches multiple products. Ask a manager to resolve it; no item selected or stock transaction recorded.");return; }
            scan.MatchedProductId=rows[0].productId; scan.ValidationStatus="Matched"; scan.ErrorCode=null;
            string prefixVi=result.synthetic?"DỮ LIỆU THỬ NGHIỆM\n":"", prefixEn=result.synthetic?"TEST BACKEND DATA\n":"";
            string title=rows[0].name+"\nSKU: "+rows[0].sku+" · "+scan.BarcodeFormat+"\n";
            string detailsVi=string.Join("\n",rows.Take(3).Select(row=>(row.locationCode??"—")+" · "+row.available+" "+row.unit+" · Lô "+(row.lotCode??"—")+" · HSD "+(string.IsNullOrEmpty(row.expiryDate)?"—":row.expiryDate)));
            string detailsEn=string.Join("\n",rows.Take(3).Select(row=>(row.locationCode??"—")+" · "+row.available+" "+row.unit+" · Lot "+(row.lotCode??"—")+" · Exp "+(string.IsNullOrEmpty(row.expiryDate)?"—":row.expiryDate)));
            Message(ScannerState.Result,prefixVi+(lotOnly?"Đang tra mã lô, không phải barcode sản phẩm.\n":"")+title+detailsVi+(rows.Length>3?"\nCòn "+(rows.Length-3)+" dòng: xem Tra tồn kho.":"")+"\nChỉ tra cứu — chưa thay đổi tồn kho.",prefixEn+(lotOnly?"Lot lookup, not a product barcode.\n":"")+title+detailsEn+(rows.Length>3?"\n"+(rows.Length-3)+" more rows: see Inventory.":"")+"\nRead-only — stock unchanged.");
        }
        void StopCamera()
        {
            if(cameraTexture!=null){cameraTexture.Stop();Destroy(cameraTexture);cameraTexture=null;}
            if(preview!=null){preview.texture=null;preview.color=Color.black;}
            cameraLease.Release();
        }
        void OnApplicationPause(bool paused)
        {
            appPaused=paused;
            if (!configured || !isActiveAndEnabled || State == ScannerState.RequestingPermission) return;
            if (paused) { session++;candidateCode=null;candidateHits=0;StopAllCoroutines();bool interrupted=querying;querying=false;lookupButton.interactable=true;StopCamera();if(interrupted)Message(ScannerState.Paused,"Tra cứu đã tạm dừng. Bấm Tra mã để thử lại.","Lookup paused. Tap Look up to retry.");else if(!scanLocked)Message(ScannerState.Paused,"Camera đã tạm dừng.","Camera paused."); }
            else if (scanLocked) Message(ScannerState.Result,messageVi,messageEn);
            else RestartScan();
        }
        void OpenCameraSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using(var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var uriClass = new AndroidJavaClass("android.net.Uri"))
                using(var uri = uriClass.CallStatic<AndroidJavaObject>("parse","package:"+Application.identifier))
                using(var intent = new AndroidJavaObject("android.content.Intent","android.settings.APPLICATION_DETAILS_SETTINGS",uri)) activity.Call("startActivity",intent);
            }
            catch (Exception) { Message(ScannerState.Error,"Mở Cài đặt Android → Ứng dụng → JarVis → Quyền → Camera.","Open Android Settings → Apps → JarVis → Permissions → Camera."); }
#else
            Message(ScannerState.Error,"Cấp quyền camera trong cài đặt hệ thống.","Allow camera access in system settings.");
#endif
        }
        void OnDisable(){autoNextScanAt=0;SmallRoomDeliveryMission.Disarm();RoompresentDeliveryMission.Disarm();session++;StopAllCoroutines();StopCamera();pixels=null;querying=false;scanLocked=false;LastScan=null;identityToken=null;if(manual!=null)manual.text="";if(lookupButton!=null)lookupButton.interactable=true;State=ScannerState.Released;}
        void OnDestroy(){StopCamera();if(decode!=null)decode.ContinueWith(task=>{if(task.IsFaulted)_=task.Exception;}, TaskScheduler.Default);}
    }
}

