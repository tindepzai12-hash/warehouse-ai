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
        TMP_Text status;
        TMP_InputField manual;
        UnityEngine.UI.Button lookupButton;
        Task<Result> decode;
        Color32[] pixels;
        bool configured, querying, decodeWarningShown;
        float nextDecode, lastAcceptedAt;
        string lastAccepted;
        int session;

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
        void OnEnable(){if(configured)StartCoroutine(StartCamera());}
        IEnumerator StartCamera()
        {
#if UNITY_EDITOR
            if(Application.isBatchMode){status.text="Kiểm tra bố cục: camera không được mở. Nhập mã vẫn dùng luồng tra cứu hiện tại.";yield break;}
#endif
            status.text="Đang mở camera…";
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                bool answered=false;
                var callbacks=new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _=>answered=true;
                callbacks.PermissionDenied += _=>answered=true;
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera,callbacks);
                float deadline=Time.realtimeSinceStartup+20;
                while(!answered && !UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && Time.realtimeSinceStartup<deadline)yield return null;
            }
            bool permission=UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera);
#else
            if(!Application.HasUserAuthorization(UserAuthorization.WebCam))yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            bool permission=Application.HasUserAuthorization(UserAuthorization.WebCam);
#endif
            if(!permission){status.text="Chưa có quyền camera. Bạn vẫn có thể nhập mã bên dưới.";yield break;}
            var devices=WebCamTexture.devices;
            if(devices.Length==0){status.text="Không tìm thấy camera. Nhập mã bên dưới để tra cứu.";yield break;}
            var device=devices.FirstOrDefault(d=>!d.isFrontFacing); if(string.IsNullOrEmpty(device.name))device=devices[0];
            cameraTexture=new WebCamTexture(device.name,1280,720,30);
            preview.texture=cameraTexture; preview.color=Color.white; cameraTexture.Play();
            float timeout=Time.realtimeSinceStartup+10;
            while(cameraTexture!=null && cameraTexture.width<=16 && Time.realtimeSinceStartup<timeout)yield return null;
            if(cameraTexture==null)yield break;
            if(cameraTexture.width<=16){StopCamera();status.text="Camera không khởi động. Thử mở lại màn hình hoặc nhập mã.";yield break;}
            status.text="Đưa mã QR / barcode vào camera để quét.";
        }
        void Update()
        {
            if(!configured || cameraTexture==null || !cameraTexture.isPlaying || cameraTexture.width<=16)return;
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
                    _=decode.Exception; // Observe the worker error without leaking frame contents.
                    if(!decodeWarningShown)Debug.LogWarning("[JarVis] Camera decode failed; next frame will retry.");
                    decodeWarningShown=true;decode=null;return;
                }
                var result=decode.Result;decode=null;
                if(result!=null && !querying && (result.Text!=lastAccepted || Time.unscaledTime-lastAcceptedAt>4))
                {lastAccepted=result.Text;lastAcceptedAt=Time.unscaledTime;manual.text=result.Text;Submit(result.Text);}
            }
            if(querying || Time.unscaledTime<nextDecode || !cameraTexture.didUpdateThisFrame)return;
            nextDecode=Time.unscaledTime+.3f;
            if(pixels!=null && pixels.Length!=cameraTexture.width*cameraTexture.height)pixels=null;
            pixels=cameraTexture.GetPixels32(pixels);
            int width=cameraTexture.width,height=cameraTexture.height;
            // This array is not reused until the task completes. Unity camera APIs stay on the main thread.
            var frame=pixels;
            decode=Task.Run(()=>Decode(frame,width,height));
        }
        public static Result Decode(Color32[] frame,int width,int height)
        {
            var bytes=new byte[width*height*4];
            for(int i=0;i<frame.Length;i++){int j=i*4;bytes[j]=frame[i].r;bytes[j+1]=frame[i].g;bytes[j+2]=frame[i].b;bytes[j+3]=frame[i].a;}
            var reader=new BarcodeReaderGeneric{AutoRotate=true};
            reader.Options.TryHarder=true;
            return reader.Decode(new RGBLuminanceSource(bytes,width,height,RGBLuminanceSource.BitmapFormat.RGBA32));
        }
        public void Submit(string code)
        {
            if(querying)return;
            code=code?.Trim();
            if(string.IsNullOrEmpty(code)){status.text="Vui lòng nhập mã trước khi tra cứu.";return;}
            if(code.Length>160){status.text="Mã quá dài. Chỉ dùng mã hàng hoặc mã vị trí, tối đa 160 ký tự.";return;}
            if(code.StartsWith("JWM:",StringComparison.Ordinal))
            {
                var navigation=FindFirstObjectByType<ImportedFigmaNavigation>();
                if(navigation!=null&&navigation.OpenMapFromMarker(code))return;
                status.text="Mã mốc kho không hợp lệ. Cần mã JWM:MAIN:<ID> đã khai báo trong bản đồ.";
                return;
            }
            if(!OmsMobileSession.IsReady){status.text=ImportedAppLocalization.Text("Đã đọc mã: ")+code+ImportedAppLocalization.Text("\nCần đăng nhập backend để tra hàng thật. Chưa ghi giao dịch kho.");return;}
            StartCoroutine(Lookup(code,session));
        }
        IEnumerator Lookup(string code,int requestSession)
        {
            querying=true;lookupButton.interactable=false;status.text="Đang tra mã trong kho…";
            OmsMobileApi.InventoryList result=null;string error=null;
            yield return OmsMobileApi.SearchInventory(code,value=>result=value,value=>error=value);
            if(requestSession!=session)yield break;
            querying=false;lookupButton.interactable=true;
            if(result==null){status.text=ImportedAppLocalization.Text("Không thể tra cứu. Bấm Tra mã để thử lại.")+"\n"+error;yield break;}
            var rows=(result.items??Array.Empty<OmsMobileApi.InventoryRow>()).Where(r=>r.barcode==code || r.sku==code || r.lotCode==code).ToArray();
            if(rows.Length==0){status.text=ImportedAppLocalization.Text("Không tìm thấy mã này trong dữ liệu kho: ")+code;yield break;}
            status.text=(result.synthetic?ImportedAppLocalization.Text("Dữ liệu thử nghiệm backend\n"):"")+rows[0].name+" · "+rows[0].sku+"\n"+string.Join("\n",rows.Take(2).Select(r=>r.locationCode+": "+r.available+" "+r.unit));
        }
        void StopCamera()
        {if(cameraTexture==null)return;cameraTexture.Stop();Destroy(cameraTexture);cameraTexture=null;if(preview!=null){preview.texture=null;preview.color=Color.black;}}
        void OnDisable(){session++;StopAllCoroutines();StopCamera();decode=null;pixels=null;querying=false;if(lookupButton!=null)lookupButton.interactable=true;}
        void OnDestroy(){StopCamera();}
    }
}

