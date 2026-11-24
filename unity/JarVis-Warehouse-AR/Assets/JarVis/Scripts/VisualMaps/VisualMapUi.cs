using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JarVis.Warehouse.VisualMaps
{
    public sealed class VisualMapUi : MonoBehaviour
    {
        public VisualMapSession Owner;
        private RectTransform safe,card,reticle,fill,library,libraryContent;
        private InputField nameInput;
        private int libraryVersion = -1;
        private VisualMapMode previousMode;
        private Text title,status,heading,detail,note,primaryText,secondaryText,connectText;
        private Button primary,secondary,connect,back,retry;
        private Font regular,bold;
        private RectTransform mapPanel;
        private Text mapCaption;
        private RawImage objectPreview;
        private static readonly Color Blue = new Color(.04f,.27f,.95f);
        private static readonly Color Ink = new Color(.035f,.08f,.19f);

        private void Start()
        {
            regular = Resources.Load<Font>("BeVietnamPro-Regular");
            bold = Resources.Load<Font>("BeVietnamPro-Bold");
            GameObject canvas = new GameObject("Visual Maps HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(412,915);
            scaler.matchWidthOrHeight = 0;
            safe = Rect(canvas.transform,"Safe area");
            RectTransform header = Panel(safe,"Header");
            header.anchorMin = new Vector2(0,1); header.anchorMax = Vector2.one; header.pivot = new Vector2(.5f,1);
            header.offsetMin = new Vector2(16,-94); header.offsetMax = new Vector2(-16,-12);
            back = MakeButton(header,"‹",false,Owner.Cancel,out Text backLabel);
            backLabel.fontSize = 32;
            Place(back.GetComponent<RectTransform>(),new Vector2(0,.5f),new Vector2(31,0),new Vector2(44,52));
            title = Label(header,"JarVis Maps",20,Ink,true);
            Place(title.rectTransform,new Vector2(.5f,.5f),new Vector2(20,14),new Vector2(280,28));
            status = Label(header,"COLMAP + hloc · " + VisualMapData.Version,11,new Color(.3f,.37f,.48f),false);
            Place(status.rectTransform,new Vector2(.5f,.5f),new Vector2(20,-17),new Vector2(280,22));
            card = Panel(safe,"Action card");
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.right; card.pivot = new Vector2(.5f,0);
            card.anchoredPosition = new Vector2(0,14); card.sizeDelta = new Vector2(-32,0);
            VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18,18,17,17); layout.spacing = 11;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            heading = Label(card,"Bản đồ phòng của bạn",21,Ink,true); Height(heading.gameObject,31);
            detail = Label(card,"",13,Ink,false); detail.alignment = TextAnchor.UpperLeft;
            LayoutElement detailLayout = detail.gameObject.AddComponent<LayoutElement>(); detailLayout.preferredHeight = 76; detailLayout.minHeight = 76;
            primary = MakeButton(card,"Ghi bản đồ",true,Owner.Primary,out primaryText); Height(primary.gameObject,50);
            secondary = MakeButton(card,"Chỉ đường A → B",false,() => Secondary(),out secondaryText); Height(secondary.gameObject,48);
            connect = MakeButton(card,"Kết nối PC",false,() => Connect(),out connectText); Height(connect.gameObject,44);
            retry = MakeButton(card,"Nhận diện đồ vật",false,Owner.OpenObjects,out Text retryLabel); Height(retry.gameObject,44);
            RectTransform nameRect = Surface(card,"Capture name",new Color(.9f,.94f,1)); Height(nameRect.gameObject,44);
            Text nameText = Label(nameRect,"",13,Ink,false);
            nameText.rectTransform.anchorMin = Vector2.zero; nameText.rectTransform.anchorMax = Vector2.one;
            nameText.rectTransform.offsetMin = new Vector2(12,4); nameText.rectTransform.offsetMax = new Vector2(-12,-4);
            nameInput = nameRect.gameObject.AddComponent<InputField>(); nameInput.textComponent = nameText;
            nameInput.characterLimit = 64; nameInput.onEndEdit.AddListener(Owner.RenameCapture);
            nameRect.GetComponent<Image>().raycastTarget = true;
            nameInput.transform.SetSiblingIndex(1);
            note = Label(card,"Không GPS · không cloud trả phí",10,new Color(.31f,.38f,.49f),false); note.alignment = TextAnchor.MiddleCenter; Height(note.gameObject,28);
            reticle = Rect(safe,"QR floor guide");
            Place(reticle,new Vector2(.5f,.57f),Vector2.zero,new Vector2(196,196));
            foreach (Vector2 corner in new[] { Vector2.zero,Vector2.up,Vector2.right,Vector2.one })
            {
                RectTransform horizontal = Surface(reticle,"Corner horizontal",Color.white);
                Place(horizontal,corner,new Vector2(corner.x == 0 ? 13 : -13,0),new Vector2(28,3));
                RectTransform vertical = Surface(reticle,"Corner vertical",Color.white);
                Place(vertical,corner,new Vector2(0,corner.y == 0 ? 13 : -13),new Vector2(3,28));
            }
            fill = Surface(reticle,"Active scanning line",new Color(.1f,.75f,1,.8f));
            Place(fill,new Vector2(.5f,.5f),Vector2.zero,new Vector2(172,2));
            library = Rect(safe,"Map library");
            library.anchorMin = Vector2.zero; library.anchorMax = Vector2.one;
            RectTransform viewport = Surface(library,"Viewport",new Color(.97f,.985f,1,.95f));
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().raycastTarget = true; viewport.gameObject.AddComponent<RectMask2D>();
            libraryContent = Rect(viewport,"Content"); libraryContent.anchorMin = Vector2.up; libraryContent.anchorMax = Vector2.one; libraryContent.pivot = Vector2.up;
            libraryContent.offsetMin = libraryContent.offsetMax = Vector2.zero;
            VerticalLayoutGroup rows = libraryContent.gameObject.AddComponent<VerticalLayoutGroup>();
            rows.padding = new RectOffset(10,10,10,10); rows.spacing = 10;
            rows.childControlWidth = rows.childControlHeight = rows.childForceExpandWidth = true; rows.childForceExpandHeight = false;
            libraryContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scrolling = library.gameObject.AddComponent<ScrollRect>(); scrolling.viewport = viewport; scrolling.content = libraryContent;
            scrolling.horizontal = false; scrolling.movementType = ScrollRect.MovementType.Clamped;
            mapPanel = Surface(safe,"2D map",new Color(.075f,.12f,.19f,.96f));
            mapPanel.gameObject.AddComponent<RectMask2D>();
            RectTransform mapContent = Rect(mapPanel,"Floor and live position");
            mapContent.anchorMin = Vector2.zero; mapContent.anchorMax = Vector2.one; mapContent.offsetMin = new Vector2(0,34); mapContent.offsetMax = Vector2.zero;
            VisualMapFloorGraphic graphic = mapContent.gameObject.AddComponent<VisualMapFloorGraphic>(); graphic.Owner = Owner; graphic.raycastTarget = false;
            mapCaption = Label(mapPanel,"Map 2D · đang đối chiếu",10,Color.white,false);
            mapCaption.rectTransform.anchorMin = Vector2.zero; mapCaption.rectTransform.anchorMax = Vector2.right;
            mapCaption.rectTransform.offsetMin = new Vector2(8,3); mapCaption.rectTransform.offsetMax = new Vector2(-8,32);
            RectTransform photo = Rect(safe,"Object snapshot"); objectPreview = photo.gameObject.AddComponent<RawImage>(); objectPreview.raycastTarget = false;
            VisualMapObjectGraphic boxes = Rect(photo,"Detection boxes").gameObject.AddComponent<VisualMapObjectGraphic>(); boxes.Owner = Owner; boxes.raycastTarget = false;
            boxes.rectTransform.anchorMin = Vector2.zero; boxes.rectTransform.anchorMax = Vector2.one; boxes.rectTransform.offsetMin = boxes.rectTransform.offsetMax = Vector2.zero;
            if (EventSystem.current == null) new GameObject("Visual Maps events",typeof(EventSystem),typeof(InputSystemUIInputModule));
        }

        private void Update()
        {
            if (safe == null || Owner == null) return;
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width,area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width,area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            VisualMapMode mode = Owner.Mode;
            bool home = mode == VisualMapMode.Home;
            bool recording = mode == VisualMapMode.Capture || mode == VisualMapMode.StartQr || mode == VisualMapMode.Route;
            bool locating = mode == VisualMapMode.Locate;
            title.text = mode == VisualMapMode.Navigate ? "Chỉ đường · A → B" : mode == VisualMapMode.Library ? "Thư viện map" : mode == VisualMapMode.Inspect ? "Nhận diện đồ vật" : recording ? "Quét phòng" : "JarVis Maps";
            status.text = (mode == VisualMapMode.Capture ? Owner.PhotoCount + " ảnh đã lưu · thu ảnh" : Owner.Tracking ? "AR đang theo dõi" : "AR chưa theo dõi ổn định") + " · " + VisualMapData.Version;
            heading.text = home ? "Map và đồ vật của bạn" : mode == VisualMapMode.MapLive ? "Vị trí của bạn trên map" : mode == VisualMapMode.Library ? "Lượt chụp và map trên PC" : mode == VisualMapMode.MapDetail ? Owner.SelectedMap?.name ?? "Map đã chọn" : mode == VisualMapMode.Inspect ? "Gợi ý từ một ảnh chụp" : mode == VisualMapMode.Connect ? "Quét QR kết nối PC" : mode == VisualMapMode.Capture ? "Đang quét · " + Owner.PhotoCount + " ảnh" : mode == VisualMapMode.StartQr ? "Quét A để ghi tuyến" : mode == VisualMapMode.Route ? "Đi tới B · " + Owner.RoutePhotos + " ảnh tuyến" : mode == VisualMapMode.Review ? "Lượt chụp đã lưu" : mode == VisualMapMode.Upload ? "Đang gửi · " + Mathf.FloorToInt(Owner.UploadProgress * 100) + "%" : mode == VisualMapMode.Build ? "PC đang phân tích" : mode == VisualMapMode.NavigateQr ? "Quét A để chỉ đường" : locating ? "Đối chiếu vị trí · " + Mathf.Min(2,Owner.LocalizationMatches) + "/2" : Owner.Arrived ? "Đã tới gần B" : "B · còn ~" + Owner.Remaining.ToString("F1") + " m";
            heading.fontSize = heading.text.Length > 27 ? 18 : 21;
            if (heading.text.Length > 32) heading.text = heading.text.Substring(0,29) + "…";
            detail.text = mode == VisualMapMode.Capture ? Owner.CaptureHint : mode == VisualMapMode.Navigate ? Owner.Direction + "\n" + Owner.Message : Owner.Message;
            primaryText.text = home ? "Quét phòng mới" : mode == VisualMapMode.Library ? "Làm mới thư viện" : mode == VisualMapMode.MapDetail ? Owner.SelectedMap != null && Owner.SelectedMap.CanNavigate ? "Chỉ đường trên map này" : "Định vị trên map 2D" : mode == VisualMapMode.Inspect ? "Chụp và nhận diện trên PC" : mode == VisualMapMode.Capture ? "Lưu lượt quét phòng" : mode == VisualMapMode.Review ? "Gửi PC để phân tích" : mode == VisualMapMode.Build ? "Kiểm tra tiến trình" : locating ? "Thử đối chiếu lại" : "";
            primary.gameObject.SetActive(home || mode == VisualMapMode.Capture || mode == VisualMapMode.Review || locating || mode == VisualMapMode.Build || mode == VisualMapMode.Library || mode == VisualMapMode.MapDetail || mode == VisualMapMode.Inspect);
            primary.interactable = !Owner.Busy && (mode == VisualMapMode.Library || mode == VisualMapMode.Review || mode == VisualMapMode.Build || Owner.Ready) && (mode != VisualMapMode.MapDetail || Owner.SelectedMap != null && Owner.SelectedMap.Valid);
            secondary.gameObject.SetActive(home || mode == VisualMapMode.Review || mode == VisualMapMode.Navigate || mode == VisualMapMode.MapLive || locating || mode == VisualMapMode.Library || mode == VisualMapMode.MapDetail || mode == VisualMapMode.Capture);
            secondaryText.text = home ? "Thư viện nhiều map" : mode == VisualMapMode.Library ? "Quét phòng mới" : mode == VisualMapMode.MapDetail ? "Quét bản cập nhật riêng" : mode == VisualMapMode.Capture ? Owner.FloorSet ? "Ghi thêm tuyến A → B" : "Căn sàn (không bắt buộc)" : mode == VisualMapMode.Review ? "Xuất ZIP vào Download" : "Dừng chỉ đường";
            if (mode == VisualMapMode.MapLive) secondaryText.text = "Dừng định vị";
            secondary.interactable = !Owner.Busy;
            connect.gameObject.SetActive(home || mode == VisualMapMode.Review || mode == VisualMapMode.Library);
            connectText.text = "Kết nối PC · " + Owner.PcAddress;
            connect.interactable = !Owner.Busy;
            retry.gameObject.SetActive(home);
            retry.interactable = !Owner.Busy;
            back.gameObject.SetActive(!home);
            note.text = home ? "Chụp/lưu/xuất không cần mạng · phân tích trên PC" : mode == VisualMapMode.Library ? "Chạm một mục để mở · kéo để xem thêm" : mode == VisualMapMode.MapDetail ? "Map định vị không đồng nghĩa đã tránh được vật cản" : mode == VisualMapMode.Inspect ? "Nhận diện loại phổ thông · không phải SKU" : mode == VisualMapMode.Navigate ? "Quan sát lối đi thật" : mode == VisualMapMode.Capture ? "Tự lưu ảnh · " + Owner.DepthFrames + " ảnh có Depth" : recording ? Owner.DepthStatus : "Dữ liệu gốc được giữ; không ghi đè phòng khác";
            nameInput.gameObject.SetActive(mode == VisualMapMode.Review);
            if (mode == VisualMapMode.Review && previousMode != mode) nameInput.SetTextWithoutNotify(Owner.CaptureName);
            nameInput.interactable = !Owner.Busy;
            reticle.gameObject.SetActive(mode == VisualMapMode.Connect || mode == VisualMapMode.StartQr || mode == VisualMapMode.Route || mode == VisualMapMode.NavigateQr || mode == VisualMapMode.Capture);
            fill.anchoredPosition = new Vector2(0,Mathf.Sin(Time.unscaledTime * 1.8f) * 75);
            detail.GetComponent<LayoutElement>().preferredHeight = mode == VisualMapMode.Library ? 48 : mode == VisualMapMode.MapDetail || mode == VisualMapMode.Inspect ? 106 : 84;
            detail.GetComponent<LayoutElement>().minHeight = mode == VisualMapMode.Library ? 48 : 76;
            float top = safe.rect.height - 108,bottom = 14 + card.rect.height + 18;
            bool mapVisible = mode == VisualMapMode.MapDetail || mode == VisualMapMode.Navigate || mode == VisualMapMode.MapLive || locating;
            mapPanel.gameObject.SetActive(mapVisible);
            if (mapVisible)
            {
                float height = Mathf.Max(96,Mathf.Min(mode == VisualMapMode.MapDetail ? 280 : 156,top - bottom));
                Place(mapPanel,new Vector2(0,1),new Vector2(16 + (mode == VisualMapMode.MapDetail ? 190 : 90),-108 - height * .5f),new Vector2(mode == VisualMapMode.MapDetail ? 380 : 180,height));
                mapCaption.text = Owner.MapPoseAvailable ? "● Bạn · hướng camera ▲\nB: cam · sàn sáng · cản đỏ" : Owner.Geometry != null ? "Sơ đồ đã quét\nChưa xác nhận vị trí của bạn" : "Chưa có sơ đồ phòng\nTuyến nếu có · chưa định vị";
            }
            bool snapshot = mode == VisualMapMode.Inspect && Owner.ObjectImage != null;
            objectPreview.gameObject.SetActive(snapshot);
            if (snapshot)
            {
                objectPreview.texture = Owner.ObjectImage;
                float available = Mathf.Max(60,top - bottom),ratio = (float)Owner.ObjectImage.width / Owner.ObjectImage.height;
                float width = Mathf.Min(380,available * ratio);
                Place(objectPreview.rectTransform,new Vector2(.5f,0),new Vector2(0,(top + bottom) * .5f),new Vector2(width,width / ratio));
            }
            reticle.anchorMin = reticle.anchorMax = new Vector2(.5f,0);
            reticle.anchoredPosition = new Vector2(0,(top + bottom) * .5f);
            float guideSize = Mathf.Clamp(top - bottom - 24,100,196);
            reticle.sizeDelta = new Vector2(guideSize,guideSize);
            fill.sizeDelta = new Vector2(guideSize - 24,2);
            fill.anchoredPosition = new Vector2(0,Mathf.Sin(Time.unscaledTime * 1.8f) * (guideSize * .38f));
            library.gameObject.SetActive(mode == VisualMapMode.Library);
            if (mode == VisualMapMode.Library)
            {
                library.offsetMin = new Vector2(16,bottom); library.offsetMax = new Vector2(-16,-108);
                if (libraryVersion != Owner.LibraryVersion) PopulateLibrary();
            }
            previousMode = mode;
        }

        private void PopulateLibrary()
        {
            libraryVersion = Owner.LibraryVersion;
            for (int index = libraryContent.childCount - 1; index >= 0; index--)
            {
                GameObject child = libraryContent.GetChild(index).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }
            if (Owner.LocalCaptures.Count == 0 && Owner.RemoteMaps.Length == 0)
            { Text empty = Label(libraryContent,"Chưa có lượt chụp. Quét phòng mới để bắt đầu; ảnh vẫn lưu khi chưa kết nối PC.",14,Ink,false); Height(empty.gameObject,100); }
            foreach (VisualMapLocalEntry entry in Owner.LocalCaptures)
            {
                Button row = MakeButton(libraryContent,"Điện thoại · " + entry.Name + "\n" + entry.Photos + " ảnh · " + entry.DepthFrames + " Depth" + (entry.Interrupted ? " · lượt bị ngắt" : ""),false,() => Owner.SelectLocal(entry),out Text label);
                label.alignment = TextAnchor.MiddleLeft; label.fontSize = 12; Height(row.gameObject,84);
            }
            foreach (VisualMapModel entry in Owner.RemoteMaps)
            {
                Button row = MakeButton(libraryContent,"PC · " + entry.name + " · r" + entry.revision + "\n" + entry.registered + "/" + entry.total + " ảnh · " + (entry.navigationAllowed ? "tuyến đã duyệt" : "chưa duyệt đường đi"),false,() => Owner.SelectRemote(entry),out Text label);
                label.alignment = TextAnchor.MiddleLeft; label.fontSize = 12; Height(row.gameObject,84);
            }
        }

        private void Secondary()
        {
            if (Owner.Mode == VisualMapMode.Home) Owner.OpenLibrary();
            else if (Owner.Mode == VisualMapMode.Library) Owner.BeginCapture();
            else if (Owner.Mode == VisualMapMode.MapDetail) Owner.BeginRevision();
            else if (Owner.Mode == VisualMapMode.Capture) Owner.CaptureExtra();
            else if (Owner.Mode == VisualMapMode.Review) Owner.Export();
            else Owner.Cancel();
        }

        private void Connect()
        {
            Owner.Connect();
        }

        private Text Label(Transform parent,string text,int size,Color color,bool strong)
        {
            GameObject item = new GameObject("Label",typeof(RectTransform),typeof(Text)); item.transform.SetParent(parent,false);
            Text label = item.GetComponent<Text>(); label.text = text; label.font = strong ? bold : regular;
            label.supportRichText = false;
            label.fontSize = size; label.color = color; label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private Button MakeButton(Transform parent,string text,bool solid,UnityEngine.Events.UnityAction action,out Text label)
        {
            RectTransform rect = Surface(parent,text,solid ? Blue : new Color(.89f,.93f,1));
            rect.GetComponent<Image>().raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
            ColorBlock colors = button.colors; colors.disabledColor = new Color(.66f,.72f,.82f); button.colors = colors;
            label = Label(rect,text,13,solid ? Color.white : Blue,true); label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(8,2); label.rectTransform.offsetMax = new Vector2(-8,-2);
            return button;
        }

        private static RectTransform Panel(Transform parent,string name) => Surface(parent,name,new Color(.97f,.985f,1,.96f));
        private static RectTransform Surface(Transform parent,string name,Color color)
        {
            RectTransform result = Rect(parent,name); Image image = result.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            return result;
        }
        private static RectTransform Rect(Transform parent,string name)
        {
            RectTransform result = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); result.SetParent(parent,false); return result;
        }
        private static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static void Height(GameObject item,float height)
        { LayoutElement layout = item.AddComponent<LayoutElement>(); layout.preferredHeight = layout.minHeight = height; }
    }
}
