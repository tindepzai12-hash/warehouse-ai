using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace JarVis.Warehouse.RouteRecording
{
    public sealed class WalkingRouteRecorderUi : MonoBehaviour
    {
        public WalkingRouteRecorder Recorder;
        private RectTransform safe,footer;
        private Text title,status,heading,detail,caption,primaryText,secondaryText;
        private Button primary,secondary,back;
        private Font regular,bold;
        private RectTransform scanGuide;
        private RoomScanReticle reticle;
        private Text scanLabel;
        private RectTransform progressRow,progressFill;
        private Text progressText;
        private RoomRouteDirectionGlyph directionGlyph;
        public const string DisplayVersion = "0.3.5";
        private static readonly Color Blue = new Color(0.02f,0.28f,0.94f);
        private static readonly Color Ink = new Color(0.035f,0.08f,0.19f);
        private static readonly Color Muted = new Color(0.29f,0.36f,0.48f);

        private void Start()
        {
            regular = Resources.Load<Font>("BeVietnamPro-Regular");
            bold = Resources.Load<Font>("BeVietnamPro-Bold");
            GameObject canvas = new GameObject("Room route HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(412,915);
            scaler.matchWidthOrHeight = 0;
            safe = new GameObject("Safe area",typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(canvas.transform,false);
            RectTransform header = Panel(safe,"Header");
            Anchor(header,new Vector2(0,1),Vector2.one,new Vector2(0,-59),new Vector2(-32,86));
            title = Label(header,"JarVis Routes",21,Ink,true);
            Anchor(title.rectTransform,Vector2.zero,Vector2.one,new Vector2(27,13),new Vector2(-90,-40));
            status = Label(header,"Đang chuẩn bị AR…",11,Muted,false);
            Anchor(status.rectTransform,Vector2.zero,Vector2.one,new Vector2(27,-19),new Vector2(-90,-60));
            back = Button(header,"‹",false,() => Back(),out Text backText);
            backText.fontSize = 30;
            Anchor(back.GetComponent<RectTransform>(),new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(28,0),new Vector2(44,48));
            footer = Panel(safe,"Action card");
            VerticalLayoutGroup stack = footer.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(18,18,16,16);
            stack.spacing = 8;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            RectTransform headingRow = new GameObject("Destination heading",typeof(RectTransform),typeof(LayoutElement)).GetComponent<RectTransform>();
            headingRow.SetParent(footer,false);
            headingRow.GetComponent<LayoutElement>().preferredHeight = 32;
            heading = Label(headingRow,"Ghi một tuyến trong phòng",21,Ink,true);
            Anchor(heading.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            RectTransform glyph = new GameObject("Navigation direction",typeof(RectTransform),typeof(CanvasRenderer),typeof(RoomRouteDirectionGlyph)).GetComponent<RectTransform>();
            glyph.SetParent(headingRow,false);
            Anchor(glyph,new Vector2(0,0.5f),new Vector2(0,0.5f),new Vector2(16,0),new Vector2(32,32));
            directionGlyph = glyph.GetComponent<RoomRouteDirectionGlyph>();
            directionGlyph.color = Blue;
            directionGlyph.raycastTarget = false;
            progressRow = new GameObject("Preparation progress",typeof(RectTransform),typeof(LayoutElement)).GetComponent<RectTransform>();
            progressRow.SetParent(footer,false);
            progressRow.GetComponent<LayoutElement>().preferredHeight = 26;
            progressText = Label(progressRow,"Chuẩn bị · 0%",12,Blue,true);
            Anchor(progressText.rectTransform,Vector2.zero,Vector2.one,new Vector2(0,4),new Vector2(0,-8));
            RectTransform track = new GameObject("Preparation track",typeof(RectTransform),typeof(CanvasRenderer),typeof(WalkingRouteSurface)).GetComponent<RectTransform>();
            track.SetParent(progressRow,false);
            track.GetComponent<WalkingRouteSurface>().color = new Color(0.87f,0.92f,1);
            track.GetComponent<WalkingRouteSurface>().raycastTarget = false;
            Anchor(track,Vector2.zero,new Vector2(1,0),new Vector2(0,3),new Vector2(0,5));
            progressFill = new GameObject("Preparation fill",typeof(RectTransform),typeof(CanvasRenderer),typeof(WalkingRouteSurface)).GetComponent<RectTransform>();
            progressFill.SetParent(track,false);
            progressFill.GetComponent<WalkingRouteSurface>().color = Blue;
            progressFill.GetComponent<WalkingRouteSurface>().raycastTarget = false;
            detail = Row(footer,"",13,Muted,false,54);
            detail.alignment = TextAnchor.UpperLeft;
            primary = Button(footer,"Ghi tuyến",true,() => Primary(),out primaryText);
            secondary = Button(footer,"Chỉ đường",false,() => Secondary(),out secondaryText);
            caption = Row(footer,"Thẻ 18 cm · cùng tầng · quan sát lối đi",10,Muted,false,22);
            caption.alignment = TextAnchor.MiddleCenter;
            scanGuide = new GameObject("QR scan guide",typeof(RectTransform),typeof(CanvasRenderer),typeof(RoomScanReticle)).GetComponent<RectTransform>();
            scanGuide.SetParent(safe,false);
            Anchor(scanGuide,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(0,66),new Vector2(244,244));
            reticle = scanGuide.GetComponent<RoomScanReticle>();
            reticle.raycastTarget = false;
            RectTransform scanCard = Panel(scanGuide,"Scan status");
            Anchor(scanCard,new Vector2(0.5f,0),new Vector2(0.5f,0),new Vector2(0,-36),new Vector2(344,44));
            scanLabel = Label(scanCard,"Đang chờ ảnh camera…",13,Ink,true);
            scanLabel.alignment = TextAnchor.MiddleCenter;
            Anchor(scanLabel.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-20,-6));
            if (EventSystem.current == null)
            {
                GameObject events = new GameObject("Room route input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
        }

        private void Update()
        {
            if (Recorder == null || safe == null) return;
            Rect area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width,area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width,area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            bool home = Recorder.Home;
            bool navigating = Recorder.NavigationMode;
            WalkingRouteFollower follower = Recorder.Follower;
            bool departure = Recorder.AwaitingDeparture;
            bool following = navigating && follower.Active && !departure;
            bool arrived = navigating && follower.Arrived;
            bool confirming = Recorder.ReplacementPending;
            bool preparing = Recorder.PreparingEnvironment && !Recorder.Environment.Ready;
            int percent = Mathf.FloorToInt(Recorder.PreparationProgress * 100);
            bool scanningB = Recorder.Route.AwaitingEnd;
            bool showingProgress = Recorder.PreparingEnvironment || scanningB;
            int shownPercent = scanningB ? Recorder.Scan.State == RoomScanState.Locked ? 100 : Mathf.Min(99,Mathf.FloorToInt(Recorder.Scan.Progress * 100)) : percent;
            string stage = scanningB ? Recorder.Scan.State == RoomScanState.Locating ? "Khóa vị trí B" : "Chờ quét thẻ B" : preparing ? "1/2 · Quan sát môi trường" : "2/2 · Khóa vị trí A";
            title.text = home ? "JarVis Routes" : navigating ? "Chỉ đường · A → B" : "Ghi tuyến · A → B";
            status.text = Recorder.Tracking ? "AR sẵn sàng" : Recorder.Ready ? "Đang tìm lại môi trường…" : "Camera AR chưa sẵn sàng";
            status.color = Recorder.Tracking ? new Color(0.02f,0.4f,0.27f) : Muted;
            if (navigating && follower.Suspended) { status.text = "AR tạm dừng · đứng yên"; status.color = new Color(0.64f,0.36f,0.05f); }
            else if (navigating && follower.WaitingQr) { status.text = "Cần khóa vị trí thẻ A"; status.color = Blue; }
            else if (home && Recorder.Scan.State == RoomScanState.Locked) { status.text = Recorder.Scan.Label + " · 100%"; status.color = new Color(0.02f,0.4f,0.27f); }
            if (preparing) { status.text = "Chuẩn bị AR · quan sát xung quanh"; status.color = Blue; }
            else if (following && follower.RouteCaution && !follower.Suspended) { status.text = "Dừng · cần đối chiếu vị trí"; status.color = new Color(0.64f,0.36f,0.05f); }
            if (departure && !preparing) { status.text = "Đã căn A · chờ bạn bắt đầu"; status.color = Blue; }
            if (showingProgress) status.text = stage + " · " + shownPercent + "%";
            else if (following && Recorder.Tracking && !follower.Suspended && !follower.RouteCaution) status.text = "AR đã chuẩn bị · 100%";
            status.text += " · v" + DisplayVersion;
            back.gameObject.SetActive(!home || WalkingRouteLauncher.Embedded);
            heading.text = confirming ? "Ghi lại tuyến A → B?" : home ? Recorder.HasSavedRoute ? "Tuyến A → B đã sẵn sàng" : "Ghi tuyến đầu tiên" : following ? follower.OffRoute ? "Dừng · đang lệch tuyến" : "B · còn ~" + follower.Remaining.ToString("F1") + " m" : arrived ? "Đã tới gần B" : navigating ? follower.WaitingQr ? "Quét thẻ A để đi" : "Cần quét A lại" : Recorder.Route.Active ? "Đi tới B" : "Quét thẻ A để ghi";
            heading.fontSize = heading.text.Length > 26 ? 18 : 21;
            if (following && follower.Suspended) heading.text = "Đang tìm lại vị trí";
            else if (following && follower.RouteChecking) heading.text = "Dừng · đang đối chiếu tuyến";
            else if (preparing) heading.text = "Quan sát xung quanh";
            if (departure && !preparing) heading.text = "Sẵn sàng đi · 100%";
            bool directionVisible = following && Recorder.Tracking && follower.ShowArrow;
            UpdateDirection(follower,directionVisible);
            detail.text = confirming ? "Tuyến hiện tại chỉ được thay khi lượt ghi mới kết thúc tại B và lưu thành công." : following ? follower.OffRoute ? "Dừng và trở lại lối đã ghi. Không đi xuyên vật cản." : follower.Direction + " · đi theo các mũi tên cyan" : arrived ? "Đích B màu xanh dương là vị trí đã ghi. Kiểm tra vị trí thật; khoảng cách AR là ước lượng." : home && !Recorder.SaveFailed ? Recorder.HasSavedRoute ? Recorder.SavedRoute.horizontalMetres.ToString("F1") + " m đã ghi. Quay về A, chọn Chỉ đường rồi quét toàn bộ thẻ A." : "Chọn Ghi tuyến → quét A → đi tới B → quét B. Ứng dụng chỉ giữ một tuyến hiện hành." : Recorder.DisplayMessage;
            detail.GetComponent<LayoutElement>().preferredHeight = 58;
            if (home && !confirming && (!Recorder.Ready || Recorder.Route.Data?.state == "interrupted")) detail.text = Recorder.DisplayMessage;
            if (following && follower.Suspended) detail.text = follower.Message;
            else if (following && follower.RouteCaution) detail.text = "Lệch ước lượng ~" + follower.Distance.ToString("F1") + " m. Dừng, đối chiếu lối thật; B màu cam chỉ là vị trí ước lượng.";
            else if (following && !string.IsNullOrEmpty(RoomRoutePresentation.CameraHint(follower))) detail.text = RoomRoutePresentation.Instruction(follower) + " · " + RoomRoutePresentation.CameraHint(follower);
            else if (following && Recorder.LiveView != null && !string.IsNullOrEmpty(Recorder.LiveView.ViewHint)) detail.text = RoomRoutePresentation.Instruction(follower) + " · " + Recorder.LiveView.ViewHint;
            else if (following) detail.text = RoomRoutePresentation.Instruction(follower) + " · đi theo tuyến đã ghi";
            else if (preparing) detail.text = Recorder.Environment.Message;
            else if (navigating && follower.WaitingQr) detail.text = "Quét toàn bộ thẻ A. Khóa vị trí xong, bấm Bắt đầu đi.";
            if (departure && !preparing) detail.text = "Tuyến đã lưu: ~" + follower.TotalLength.ToString("F1") + " m. Nếu đổi phòng hoặc vị trí A/B, hãy ghi lại tuyến. Cùng vị trí thì bấm Bắt đầu đi.";
            if (preparing && Recorder.Environment.WaitingTooLong(Time.realtimeSinceStartupAsDouble)) detail.text = "Chưa đủ điều kiện. " + Recorder.Environment.Message;
            progressRow.gameObject.SetActive(showingProgress);
            progressText.text = stage + " · " + shownPercent + "%";
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(shownPercent / 100f,1);
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            if (showingProgress) detail.GetComponent<LayoutElement>().preferredHeight = 66;
            primaryText.text = confirming ? "Ghi lại tuyến" : home ? Recorder.SaveFailed ? "Thử lưu lại" : Recorder.Ready ? "Ghi tuyến" : "Thử lại camera AR" : arrived ? "Về hai chức năng" : following || navigating ? "Dừng chỉ đường" : "Hủy ghi";
            if (following && follower.OffRoute && !follower.Suspended) primaryText.text = "Quét A lại · về vị trí A trước";
            if (departure) primaryText.text = preparing || follower.Suspended ? "Đang quan sát môi trường…" : "Bắt đầu đi";
            secondaryText.text = confirming ? "Giữ tuyến hiện tại" : "Chỉ đường";
            primary.interactable = !departure || (!preparing && !follower.Suspended && !follower.RouteCaution);
            secondary.gameObject.SetActive(home);
            secondary.interactable = confirming || (Recorder.HasSavedRoute && Recorder.Ready && !Recorder.SaveFailed);
            caption.text = Recorder.PreparingEnvironment ? "Tiến độ chuẩn bị · không phải % diện tích phòng" : scanningB ? "Tiến độ khóa thẻ B · không phải % quãng đường" : following ? "Quan sát lối đi thật · không phát hiện vật cản" : "Thẻ 18 cm · cùng tầng · quan sát lối đi";
            float height = home ? 280 : showingProgress ? 260 : 218;
            Anchor(footer,Vector2.zero,new Vector2(1,0),new Vector2(0,16 + height * 0.5f),new Vector2(-32,height));
            float openHeight = safe.rect.height - 118 - height;
            float guideSize = Mathf.Clamp(openHeight - 82,150,244);
            Anchor(scanGuide,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(0,(height - 86) * 0.5f + 29),new Vector2(guideSize,guideSize));
            RoomScanFeedback scan = Recorder.Scan;
            bool scanExpected = Recorder.ScanningStart || Recorder.Route.AwaitingEnd || (navigating && follower.WaitingQr) || (departure && preparing);
            scanGuide.gameObject.SetActive(!home && (scanExpected || scan.State == RoomScanState.Locked) && scan.State != RoomScanState.Hidden && !follower.Suspended);
            reticle.State = scan.State;
            reticle.Progress = Recorder.PreparingEnvironment ? Recorder.PreparationProgress : scan.Progress;
            reticle.ShowProgress = scanExpected || scan.State == RoomScanState.Locked;
            reticle.Phase = PlayerPrefs.GetInt("JarVis.ReduceMotion",0) == 1 || !Recorder.Environment.Fresh(Time.realtimeSinceStartupAsDouble) ? 0 : Time.unscaledTime;
            reticle.color = scan.State == RoomScanState.Locked ? new Color(0.02f,0.8f,0.5f) : scan.State == RoomScanState.WrongCode ? new Color(0.95f,0.22f,0.18f) : scan.State == RoomScanState.WaitingCamera || scan.State == RoomScanState.WaitingTracking ? new Color(1,0.7f,0.2f) : new Color(0.15f,0.85f,1);
            reticle.SetVerticesDirty();
            scanLabel.text = scan.Label;
            if (Recorder.PreparingEnvironment) scanLabel.text = preparing ? "Môi trường · " + percent + "%" : scan.State == RoomScanState.Locked ? "Đã khóa A · 100%" : scan.State == RoomScanState.Locating ? "Khóa vị trí A · " + percent + "%" : scan.State == RoomScanState.Searching ? "Chuẩn bị " + percent + "% · quét thẻ A" : scan.Label + " · " + percent + "%";
            else if (scanExpected || scan.State == RoomScanState.Locked) scanLabel.text = scan.Label + " · " + (scan.State == RoomScanState.Locked ? 100 : Mathf.Min(99,Mathf.FloorToInt(scan.Progress * 100))) + "%";
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }

        private void UpdateDirection(WalkingRouteFollower follower,bool permitted)
        {
            directionGlyph.gameObject.SetActive(permitted);
            directionGlyph.Degrees = permitted ? RoomRoutePresentation.CueAngle(follower) : 0;
            directionGlyph.SetVerticesDirty();
            heading.rectTransform.offsetMin = new Vector2(permitted ? 44 : 0,0);
        }

        private void Primary()
        {
            if (Recorder.ReplacementPending) Recorder.BeginRecording();
            else Recorder.Primary();
        }
        private void Secondary()
        {
            if (Recorder.ReplacementPending) Recorder.Cancel();
            else Recorder.NavigateSaved();
        }
        private void Back()
        {
            if (Recorder.Home && WalkingRouteLauncher.Embedded) WalkingRouteLauncher.Close();
            else Recorder.Cancel();
        }
        private RectTransform Panel(Transform parent,string name)
        {
            RectTransform rect = new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(WalkingRouteSurface)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);
            WalkingRouteSurface surface = rect.GetComponent<WalkingRouteSurface>();
            surface.color = new Color(1,1,1,0.97f);
            surface.raycastTarget = false;
            return rect;
        }
        private Text Label(Transform parent,string value,int size,Color color,bool heavy)
        {
            Text text = new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent,false);
            text.font = heavy ? bold : regular;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }
        private Text Row(Transform parent,string value,int size,Color color,bool heavy,float height)
        {
            Text text = Label(parent,value,size,color,heavy);
            text.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return text;
        }
        private Button Button(Transform parent,string value,bool accent,UnityEngine.Events.UnityAction action,out Text label)
        {
            RectTransform rect = Panel(parent,value);
            WalkingRouteSurface surface = rect.GetComponent<WalkingRouteSurface>();
            surface.color = accent ? Blue : new Color(0.9f,0.94f,1);
            surface.raycastTarget = true;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = surface;
            button.onClick.AddListener(action);
            label = Label(rect,value,14,accent ? Color.white : Blue,true);
            label.alignment = TextAnchor.MiddleCenter;
            Anchor(label.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-16,0));
            return button;
        }
        private static void Anchor(RectTransform rect,Vector2 min,Vector2 max,Vector2 position,Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f,0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
