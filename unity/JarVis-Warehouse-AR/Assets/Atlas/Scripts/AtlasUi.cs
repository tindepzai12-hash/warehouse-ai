using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AtlasAR
{
    public sealed class AtlasUi:MonoBehaviour
    {
        static readonly Color Ink=new Color32(20,39,65,255),Muted=new Color32(72,91,116,255),Blue=new Color32(22,91,232,255),Bg=new Color32(243,246,251,255);
        public AtlasSession session;public AtlasMap map;public bool cameraMode;
        Canvas canvas;RectTransform safe;AtlasMapGraphic graphic;LayoutElement mapLayout;GameObject arSpace,headerArea,detailArea,noteArea;
        Text state,help,details,selectionTitle,targetHint,recordText,navigateText;Font font,bold;Image backdrop;Button mapTab,arTab,recordButton;string selected="A";
        Vector3[] lastRoute;GameObject diagnosticArea,targetsRow;bool toolsVisible;readonly Vector3[] viewCorners=new Vector3[4];
        GameObject liveOverlay;Text liveInstruction;
        readonly List<Image> confirmation=new List<Image>();float nextRefresh;Rect lastSafe;int lastWidth,lastHeight;Vector3 lastPosition,lastForward;bool lastValid;
        readonly Dictionary<string,RectTransform> pins=new Dictionary<string,RectTransform>();readonly Dictionary<string,Image> pinImages=new Dictionary<string,Image>();readonly Dictionary<string,Button> choices=new Dictionary<string,Button>();
        public Canvas RootCanvas=>canvas;
        public void Create(AtlasMap data,AtlasSession owner)
        {
            map=data;session=owner;font=Resources.Load<Font>("Atlas/Regular");bold=Resources.Load<Font>("Atlas/Bold");
            var root=new GameObject("Atlas interface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(412,915);scaler.matchWidthOrHeight=.5f;
            backdrop=Panel(root.transform,"Backdrop",Bg);Stretch(backdrop.rectTransform);
            safe=new GameObject("Safe area",typeof(RectTransform)).GetComponent<RectTransform>();safe.SetParent(root.transform,false);
            var layout=safe.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(16,16,12,10);layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandHeight=false;
            var header=Box(safe,"Header",66,Color.clear);var title=Label(header.transform,"ATLAS AR",28,true,Ink);Stretch(title.rectTransform,0,24,0,0);title.alignment=TextAnchor.UpperLeft;
            var subtitle=Label(header.transform,"LAB  ·  A—E đã lưu",14,false,Muted);subtitle.rectTransform.anchorMin=new Vector2(0,0);subtitle.rectTransform.anchorMax=new Vector2(1,0);subtitle.rectTransform.offsetMin=Vector2.zero;subtitle.rectTransform.offsetMax=new Vector2(0,24);
            var statusBox=Box(safe,"Status",104,Color.white);state=Label(statusBox.transform,"Đang mở bản đồ",17,true,Ink);state.rectTransform.anchorMin=new Vector2(0,1);state.rectTransform.anchorMax=Vector2.one;state.rectTransform.offsetMin=new Vector2(14,-36);state.rectTransform.offsetMax=new Vector2(-14,-8);
            help=Label(statusBox.transform,"Bắt đầu tại bàn máy tính: quét thẻ A cũ, thấy trọn khung hoa văn.",14,false,Muted);help.rectTransform.anchorMin=Vector2.zero;help.rectTransform.anchorMax=Vector2.one;help.rectTransform.offsetMin=new Vector2(14,22);help.rectTransform.offsetMax=new Vector2(-14,-40);help.verticalOverflow=VerticalWrapMode.Truncate;
            for(int i=0;i<3;i++){var segment=Panel(statusBox.transform,"Xác nhận "+(i+1),new Color32(225,232,242,255));var r=segment.rectTransform;r.anchorMin=new Vector2(i/3f,0);r.anchorMax=new Vector2((i+1)/3f,0);r.offsetMin=new Vector2(14,9);r.offsetMax=new Vector2(-14,13);confirmation.Add(segment);}
            arSpace=new GameObject("AR viewing space",typeof(RectTransform),typeof(LayoutElement));arSpace.transform.SetParent(safe,false);arSpace.GetComponent<LayoutElement>().flexibleHeight=1;arSpace.SetActive(false);
            var mapBox=Panel(safe,"Approved map",new Color32(232,239,248,255));mapLayout=mapBox.gameObject.AddComponent<LayoutElement>();mapLayout.minHeight=180;mapLayout.flexibleHeight=1;
            var mapObject=new GameObject("Metric vector map",typeof(RectTransform),typeof(CanvasRenderer),typeof(AtlasMapGraphic));mapObject.transform.SetParent(mapBox.transform,false);graphic=mapObject.GetComponent<AtlasMapGraphic>();graphic.map=map;graphic.raycastTarget=false;Stretch(graphic.rectTransform,0,0,0,0);
            foreach(var marker in map.markers)
            {
                string id=marker.id;var button=Button(mapObject.transform,id,()=>Select(id));var rect=button.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(48,48);
                button.image.sprite=Circle();button.image.type=Image.Type.Simple;button.image.color=Blue;button.GetComponentInChildren<Text>().fontSize=19;button.GetComponentInChildren<Text>().color=Color.white;pinImages[id]=button.image;pins[id]=rect;
            }
            var targets=Row(safe,"Saved places",50,8);targetsRow=targets;foreach(var marker in map.markers){string id=marker.id;var b=Button(targets.transform,id,()=>Select(id));b.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;choices[id]=b;}
            var detail=Box(safe,"Selected coordinate",58,Color.white);selectionTitle=Label(detail.transform,"",15,true,Ink);selectionTitle.rectTransform.anchorMin=new Vector2(0,.45f);selectionTitle.rectTransform.anchorMax=Vector2.one;selectionTitle.rectTransform.offsetMin=new Vector2(12,0);selectionTitle.rectTransform.offsetMax=new Vector2(-12,-4);
            details=Label(detail.transform,"",13,false,Muted);details.rectTransform.anchorMin=Vector2.zero;details.rectTransform.anchorMax=new Vector2(1,.5f);details.rectTransform.offsetMin=new Vector2(12,2);details.rectTransform.offsetMax=new Vector2(-12,0);
            var target=Box(safe,"Visible target",42,Color.white);targetHint=Label(target.transform,"A–E đã lưu · Quét thẻ A cũ để bắt đầu",13,false,Muted);Stretch(targetHint.rectTransform,10,3,10,3);
            var navigation=Row(safe,"Navigation",48,8);var navigate=Control(navigation.transform,"Đi tới A",()=>session?.StartNavigation());navigateText=navigate.GetComponentInChildren<Text>();Tint(navigate,true);Control(navigation.transform,"Dừng",()=>session?.StopNavigation());Control(navigation.transform,"Công cụ",()=>{toolsVisible=!toolsVisible;diagnosticArea.SetActive(toolsVisible);});
            var controls=Row(safe,"Controls",50,8);mapTab=Control(controls.transform,"Map 2D",()=>SetCamera(false));arTab=Control(controls.transform,"Camera AR",()=>SetCamera(true));Control(controls.transform,"Quét lại A",()=>session?.Relocate());
            var diagnosticRow=Row(safe,"Local diagnostics",48,8);diagnosticArea=diagnosticRow;recordButton=Control(diagnosticRow.transform,"Ghi lượt thử",()=>session?.ToggleRecording());recordText=recordButton.GetComponentInChildren<Text>();Control(diagnosticRow.transform,"Xuất ZIP",()=>session?.Export());Control(diagnosticRow.transform,"Thử lại camera",()=>session?.RetryCamera());diagnosticArea.SetActive(false);
            var note=Box(safe,"Hint",30,Color.clear);var hint=Label(note.transform,Application.version+" · Căn A một lần · Đi giữa A–E",12,false,Muted);Stretch(hint.rectTransform);hint.alignment=TextAnchor.MiddleCenter;
            if(FindAnyObjectByType<EventSystem>()==null)new GameObject("Input",typeof(EventSystem),typeof(InputSystemUIInputModule));
            liveOverlay=new GameObject("AR navigation overlay",typeof(RectTransform));liveOverlay.transform.SetParent(root.transform,false);Stretch(liveOverlay.GetComponent<RectTransform>());
            var banner=Panel(liveOverlay.transform,"Turn instruction",new Color32(18,22,28,215));var bannerRect=banner.rectTransform;bannerRect.anchorMin=new Vector2(.04f,.91f);bannerRect.anchorMax=new Vector2(.96f,.96f);bannerRect.offsetMin=bannerRect.offsetMax=Vector2.zero;
            liveInstruction=Label(banner.transform,"Chọn điểm A–E",16,true,Color.white);Stretch(liveInstruction.rectTransform,12,4,12,4);liveInstruction.alignment=TextAnchor.MiddleCenter;
            var mapButton=Button(liveOverlay.transform,"Map 2D / Công cụ",()=>SetCamera(false));var mr=mapButton.GetComponent<RectTransform>();mr.anchorMin=new Vector2(.05f,.035f);mr.anchorMax=new Vector2(.58f,.09f);mr.offsetMin=mr.offsetMax=Vector2.zero;mapButton.GetComponentInChildren<Text>().fontSize=14;
            var alternate=Button(liveOverlay.transform,"Lối khác",()=>session?.ChooseAlternative());var ar=alternate.GetComponent<RectTransform>();ar.anchorMin=new Vector2(.63f,.035f);ar.anchorMax=new Vector2(.95f,.09f);ar.offsetMin=ar.offsetMax=Vector2.zero;alternate.GetComponentInChildren<Text>().fontSize=14;
            liveOverlay.SetActive(false);
            headerArea=header;detailArea=detail;noteArea=note;Select("A");SetCamera(true);ApplySafeArea();Canvas.willRenderCanvases+=PlacePins;
        }
        Button Control(Transform parent,string text,Action action){var b=Button(parent,text,action);b.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;b.GetComponentInChildren<Text>().fontSize=14;return b;}
        public void AttachImmersalControls(GameObject bridgeOwner)
        {
            // Keep the actual sample pill/dropdown, using its own CanvasScaler.
            // The map/tools screen remains separately available from the small overlay button.
        }
        public void Select(string id)
        {
            selected=id;var m=map.Marker(id);if(m==null)return;selectionTitle.text=id+" · "+m.label;
            details.text=$"X {m.position.x:F2}   Y {m.position.y:F2}   Z {m.position.z:F2} m";
            if(navigateText!=null)navigateText.text="Đi tới "+id;
            foreach(var pair in choices)Tint(pair.Value,pair.Key==id);foreach(var pair in pinImages)pair.Value.color=pair.Key==id?new Color32(188,111,11,255):Blue;
            if(session!=null)session.Selected=id;
        }
        void Tint(Button b,bool on){b.image.color=on?Blue:Color.white;b.GetComponentInChildren<Text>().color=on?Color.white:Ink;}
        public void SetCamera(bool enabled)
        {
            cameraMode=enabled;backdrop.enabled=!enabled;arSpace.SetActive(enabled);mapLayout.flexibleHeight=enabled?0:1;mapLayout.preferredHeight=enabled?124:-1;mapLayout.minHeight=enabled?124:180;
            headerArea.SetActive(!enabled);detailArea.SetActive(!enabled);noteArea.SetActive(!enabled);graphic.landscape=enabled;
            foreach(var pair in pins){pair.Value.sizeDelta=Vector2.one*(enabled?44:48);pair.Value.GetComponentInChildren<Text>().fontSize=enabled?16:19;}
            Tint(mapTab,!enabled);Tint(arTab,enabled);graphic.SetVerticesDirty();
        }
        void LateUpdate()
        {
            if(map==null)return;ApplySafeArea();if(session==null||Time.realtimeSinceStartup<nextRefresh)return;nextRefresh=Time.realtimeSinceStartup+.1f;
            bool cleanAr=session.PositionValid&&cameraMode;safe.gameObject.SetActive(!cleanAr);liveOverlay.SetActive(cleanAr);
            if(cleanAr){liveInstruction.text=session.HasNotice?session.DisplayStatus+" · "+session.DisplayHint:session.Navigation.Active?session.Navigation.Instruction:"Đã căn A · Chọn đích trong danh sách";session.Navigation.GuidanceViewport=new Rect(.04f,.12f,.92f,.60f);}
            else if(cameraMode){arSpace.GetComponent<RectTransform>().GetWorldCorners(viewCorners);session.Navigation.GuidanceViewport=new Rect(viewCorners[0].x/Screen.width,viewCorners[0].y/Screen.height,(viewCorners[2].x-viewCorners[0].x)/Screen.width,(viewCorners[2].y-viewCorners[0].y)/Screen.height);}
            if(state.text!=session.DisplayStatus)state.text=session.DisplayStatus;if(help.text!=session.DisplayHint)help.text=session.DisplayHint;
            string target=session.TargetHint;if(targetHint.text!=target)targetHint.text=target;
            var route=session.Navigation?.Points;if(!ReferenceEquals(lastRoute,route)){lastRoute=route;graphic.route=route;graphic.SetVerticesDirty();}
            string recording=session.Recording?"Dừng ghi · "+session.CapturedImages+"/12 ảnh":"Ghi lượt thử";if(recordText.text!=recording)recordText.text=recording;
            recordButton.image.color=session.Recording?new Color32(255,229,213,255):Color.white;
            for(int i=0;i<confirmation.Count;i++)confirmation[i].color=session.PositionValid||i<session.Confirmation?Blue:new Color32(225,232,242,255);
            if(lastValid!=session.PositionValid||(lastPosition-session.MapPosition).sqrMagnitude>.0004f||(lastForward-session.MapForward).sqrMagnitude>.001f)
            {
                lastValid=session.PositionValid;lastPosition=session.MapPosition;lastForward=session.MapForward;
                graphic.userValid=lastValid;graphic.userPosition=lastPosition;graphic.userForward=lastForward;graphic.SetVerticesDirty();
            }
        }
        void ApplySafeArea()
        {
            var a=Screen.safeArea;if(Screen.width<1||Screen.height<1||a==lastSafe&&Screen.width==lastWidth&&Screen.height==lastHeight)return;lastSafe=a;lastWidth=Screen.width;lastHeight=Screen.height;
            safe.anchorMin=new Vector2(a.xMin/Screen.width,a.yMin/Screen.height);safe.anchorMax=new Vector2(a.xMax/Screen.width,a.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
        }
        void PlacePins(){if(map==null||graphic==null)return;foreach(var m in map.markers)if(pins.TryGetValue(m.id,out var pin))pin.anchoredPosition=graphic.Local(m.position);}
        void OnDestroy(){Canvas.willRenderCanvases-=PlacePins;}
        public void RefreshForReview(){Canvas.ForceUpdateCanvases();LayoutRebuilder.ForceRebuildLayoutImmediate(safe);PlacePins();graphic.SetVerticesDirty();Canvas.ForceUpdateCanvases();}
        Image Panel(Transform parent,string name,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;image.sprite=Round();image.type=Image.Type.Sliced;return image;
        }
        GameObject Box(Transform parent,string name,float height,Color color){var image=Panel(parent,name,color);image.gameObject.AddComponent<LayoutElement>().preferredHeight=height;return image.gameObject;}
        GameObject Row(Transform parent,string name,float height,float spacing){var go=Box(parent,name,height,Color.clear);var fixedRow=go.GetComponent<LayoutElement>();fixedRow.minHeight=height;fixedRow.flexibleHeight=0;var row=go.AddComponent<HorizontalLayoutGroup>();row.spacing=spacing;row.childControlWidth=row.childControlHeight=true;row.childForceExpandHeight=true;return go;}
        Text Label(Transform parent,string value,int size,bool strong,Color color)
        {
            var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=strong?bold:font;t.fontSize=size;t.text=value;t.color=color;t.alignment=TextAnchor.MiddleLeft;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;
        }
        Button Button(Transform parent,string value,Action action)
        {
            var image=Panel(parent,value,Color.white);image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener(()=>action());var colors=b.colors;colors.pressedColor=new Color(.77f,.84f,.95f);colors.highlightedColor=Color.white;colors.fadeDuration=.1f;b.colors=colors;
            var t=Label(image.transform,value,18,true,Ink);Stretch(t.rectTransform,3,3,3,3);t.alignment=TextAnchor.MiddleCenter;return b;
        }
        static Sprite round,circle;
        static Sprite Round()
        {
            if(round!=null)return round;var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);tex.wrapMode=TextureWrapMode.Clamp;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float dx=Mathf.Max(Mathf.Abs(x-31.5f)-15.5f,0),dy=Mathf.Max(Mathf.Abs(y-31.5f)-15.5f,0);tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(16.5f-Mathf.Sqrt(dx*dx+dy*dy))));}tex.Apply();round=Sprite.Create(tex,new Rect(0,0,64,64),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(20,20,20,20));return round;
        }
        static Sprite Circle()
        {
            if(circle!=null)return circle;var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);for(int y=0;y<64;y++)for(int x=0;x<64;x++)tex.SetPixel(x,y,new Color(1,1,1,Mathf.Clamp01(31-Mathf.Sqrt((x-31.5f)*(x-31.5f)+(y-31.5f)*(y-31.5f)))));tex.Apply();circle=Sprite.Create(tex,new Rect(0,0,64,64),Vector2.one*.5f);return circle;
        }
        static void Stretch(RectTransform r,float l=0,float b=0,float rr=0,float t=0){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(l,b);r.offsetMax=new Vector2(-rr,-t);}
    }
}
