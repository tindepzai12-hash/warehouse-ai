using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Lab
{
    public sealed class LabTestUi : MonoBehaviour
    {
        public LabTestSession Owner;
        RectTransform safe,actions,reticle,progress,miniRect,edge;
        Button arrival,saveMarkerButton;
        Canvas hudCanvas;
        float ViewWidth=>hudCanvas!=null&&hudCanvas.renderMode!=RenderMode.ScreenSpaceOverlay&&hudCanvas.worldCamera!=null?hudCanvas.worldCamera.pixelWidth:Screen.width;
        float ViewHeight=>hudCanvas!=null&&hudCanvas.renderMode!=RenderMode.ScreenSpaceOverlay&&hudCanvas.worldCamera!=null?hudCanvas.worldCamera.pixelHeight:Screen.height;
        Text edgeText;LabScreenDirection screenGlyph;RectTransform header;LayoutElement hintLayout;Image coachPlate;
        public Func<Vector2,bool> GuidePointAllowed;
        public Func<Rect,bool> GuideBoundsAllowed;
        readonly Vector2[] exclusionsMin=new Vector2[4],exclusionsMax=new Vector2[4];
        readonly Vector3[] viewportCorners=new Vector3[4];
        Text heading,status,hint,scan,caption,travelContext,settingsInfo,settingsMotion;Button orientButton;
        string contextTarget,settingsSource;float contextRemaining=-1,settingsHeight=float.NaN;bool settingsReduced;
        RectTransform visualSettings;Rect safeViewport=new Rect(0,0,1,1);
        readonly LabCueChoreography hudMotion=new LabCueChoreography();
        readonly LabCueToken[] hudTokens={new LabCueToken(0,0,0,0,0,1),new LabCueToken(1,0,0,0,0,1),new LabCueToken(2,0,0,0,0,1)};
        LabFloorViewport mini;
        LabMapPanel mapPanel;
        LabSavedMarkersPanel savedMarkersPanel;
        public bool BlocksCameraGuidance=>showingSuite||showingNavigationLog||mapPanel!=null||savedMarkersPanel!=null;
        Font font,bold;
        string view;
        bool choosingDestination,choosingPreview,showingData,showingNavigationLog,showingSuite;string suiteUiKey;Text suiteDetails;
        static readonly Color Ink=new Color(.035f,.075f,.17f),Blue=new Color(.04f,.25f,.93f);
        public Rect GuideViewport
        {
            get
            {
                return new Rect(safeViewport.x+.025f,safeViewport.y+.015f,safeViewport.width-.05f,safeViewport.height-.03f);
            }
        }
        void Start()
        {
            font=Resources.Load<Font>("BeVietnamPro-Regular");bold=Resources.Load<Font>("BeVietnamPro-Bold");
            var canvas=new GameObject("Lab test HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.transform.SetParent(transform,false);
            hudCanvas=canvas.GetComponent<Canvas>();canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=20;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(412,915);scaler.matchWidthOrHeight=0;
            safe=Rect(canvas.transform,"Safe area");safe.anchorMin=Vector2.zero;safe.anchorMax=Vector2.one;safe.offsetMin=safe.offsetMax=Vector2.zero;
            header=Panel(safe,"Header",new Color(.97f,.985f,1,.97f));Place(header,new Vector2(.5f,1),new Vector2(0,-12),new Vector2(380,94),new Vector2(.5f,1));
            heading=Label(header,"JarVis Lab",24,true);Place(heading.rectTransform,new Vector2(0,1),new Vector2(16,-10),new Vector2(348,38),new Vector2(0,1));
            status=Label(header,"NGOẠI TUYẾN · "+LabTestSession.Version,14,false);Place(status.rectTransform,new Vector2(0,1),new Vector2(16,-51),new Vector2(348,36),new Vector2(0,1));
            var suiteAccess=header.gameObject.AddComponent<Button>();suiteAccess.targetGraphic=header.GetComponent<Image>();suiteAccess.transition=Selectable.Transition.None;suiteAccess.onClick.AddListener(()=>{if(Owner.SuiteRunning)showingSuite=true;});
            miniRect=Panel(safe,"Walked path diagram",new Color(.97f,.985f,1,.96f));Place(miniRect,new Vector2(1,1),new Vector2(-16,-118),new Vector2(124,122),new Vector2(1,1));
            var miniContent=Rect(miniRect,"Map lines");miniContent.anchorMin=Vector2.zero;miniContent.anchorMax=Vector2.one;miniContent.offsetMin=new Vector2(3,30);miniContent.offsetMax=new Vector2(-3,-3);
            mini=miniContent.gameObject.AddComponent<LabFloorViewport>();mini.Owner=Owner;mini.Compact=true;mini.Opened=()=>OpenMap(false);mini.Initialize();
            caption=Label(miniRect,"Lối đã ghi",10,false);Place(caption.rectTransform,new Vector2(.5f,0),new Vector2(0,4),new Vector2(118,28),new Vector2(.5f,0));caption.alignment=TextAnchor.MiddleCenter;
            reticle=Rect(safe,"Marker frame");Place(reticle,new Vector2(.5f,.59f),Vector2.zero,new Vector2(284,341),new Vector2(.5f,.5f));
            foreach(var corner in new[]{Vector2.zero,Vector2.up,Vector2.right,Vector2.one})
            {
                var h=Panel(reticle,"Corner",Color.white);Place(h,corner,new Vector2(corner.x==0?14:-14,0),new Vector2(30,3),new Vector2(.5f,.5f));
                var v=Panel(reticle,"Corner",Color.white);Place(v,corner,new Vector2(0,corner.y==0?14:-14),new Vector2(3,30),new Vector2(.5f,.5f));
            }
            var strip=Panel(reticle,"Progress track",new Color(.03f,.07f,.15f,.9f));Place(strip,new Vector2(.5f,0),new Vector2(0,-18),new Vector2(284,5),new Vector2(.5f,.5f));
            progress=Panel(strip,"Measured marker stability",new Color(.1f,.65f,1));Place(progress,new Vector2(0,.5f),Vector2.zero,new Vector2(0,5),new Vector2(0,.5f));
            scan=Label(reticle,"",12,true);scan.color=Color.white;scan.alignment=TextAnchor.UpperCenter;
            Place(scan.rectTransform,new Vector2(.5f,0),new Vector2(0,-25),new Vector2(310,62),new Vector2(.5f,1));
            edge=Rect(safe,"Direction outside camera");Place(edge,new Vector2(.5f,.48f),Vector2.zero,new Vector2(164,168),new Vector2(.5f,.5f));
            coachPlate=edge.gameObject.AddComponent<Image>();coachPlate.raycastTarget=false;coachPlate.color=Color.clear;
            var glyph=Rect(edge,"Direction glyph");Place(glyph,new Vector2(.5f,1),new Vector2(0,-64),new Vector2(100,100),new Vector2(.5f,.5f));screenGlyph=glyph.gameObject.AddComponent<LabScreenDirection>();screenGlyph.raycastTarget=false;
            edgeText=Label(edge,"",14,true);edgeText.color=Ink;edgeText.alignment=TextAnchor.MiddleCenter;
            Place(edgeText.rectTransform,new Vector2(.5f,0),Vector2.zero,new Vector2(160,32),new Vector2(.5f,0));
            actions=Panel(safe,"Actions",new Color(.97f,.985f,1,.98f));Place(actions,new Vector2(.5f,0),new Vector2(0,14),new Vector2(380,0),new Vector2(.5f,0));
            var layout=actions.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(12,12,10,10);layout.spacing=8;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            actions.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            hint=Label(actions,"",13,false);Height(hint.gameObject,70);hint.alignment=TextAnchor.UpperLeft;hintLayout=hint.GetComponent<LayoutElement>();GuidePointAllowed=PointAllowed;GuideBoundsAllowed=BoundsAllowed;
        }
        void LateUpdate()
        {
            if(Owner==null||safe==null)return;
            var area=hudCanvas.renderMode==RenderMode.ScreenSpaceOverlay?Screen.safeArea:new Rect(0,0,ViewWidth,ViewHeight);safe.anchorMin=new Vector2(area.xMin/ViewWidth,area.yMin/ViewHeight);safe.anchorMax=new Vector2(area.xMax/ViewWidth,area.yMax/ViewHeight);safe.offsetMin=safe.offsetMax=Vector2.zero;safeViewport=new Rect(area.x/ViewWidth,area.y/ViewHeight,area.width/ViewWidth,area.height/ViewHeight);
            CacheExclusions();Owner.RefreshPresentation();
            bool active=Owner.FixedSurvey||Owner.SettingUp||Owner.Recording||Owner.Exercise!=null||Owner.Locating;
            string next=showingSuite?"test-suite":showingNavigationLog?"navigation-log":Owner.ImportReady?"import-review":showingData?"data":Owner.ScanNeedsHelp?"scan-help":Owner.FixedSurvey?"fixed-survey":Owner.AwaitingDirection?"orient":Owner.SettingUp?"setup":Owner.NavigationInProgress&&(Owner.RecoveringTracking||Owner.RecoveryNeedsMarker||!Owner.Tracking||Owner.Located&&!Owner.NavigationPoseValid)?"recover":choosingDestination?"destinations":Owner.Recording?"record":Owner.Locating?"locate":Owner.Exercise!=null?Owner.Exercise.Complete?"done":Owner.AtTarget?"arrival":"exercise":"home";
            string suiteKey=Owner.SuiteUiKey;if(showingSuite&&suiteKey!=suiteUiKey){suiteUiKey=suiteKey;view="";}
            if(next!=view){view=next;BuildActions();Canvas.ForceUpdateCanvases();CacheExclusions();Owner.RefreshPresentation();}
            var sample=Owner.Presentation;
            bool navigating=!Owner.AwaitingDirection&&(Owner.NavigationInProgress||Owner.Located&&Owner.Exercise!=null&&!Owner.Exercise.Complete);
            SetText(heading,navigating&&sample.title!=null?sample.title:Owner.FixedSurvey?"Ghi mốc A–E":Owner.SettingUp?Owner.SetupStep:Owner.Locating?"Vị trí của tôi":Owner.Exercise!=null?Owner.Exercise.Complete?"Đã tới "+Owner.Exercise.Destination:"Tới "+Owner.Exercise.Target:"JarVis Lab");
            SetText(status,(Owner.TrialNavigation&&!Owner.FixedSurvey?"THỬ · XẤP XỈ · ":"")+(navigating&&sample.subtitle!=null?sample.subtitle:LabTestSession.Version+" · "+Owner.NavigationState));
            bool walking=view=="exercise"||view=="arrival";
            hint.gameObject.SetActive(!walking&&view!="test-suite");hintLayout.preferredHeight=hintLayout.minHeight=walking?0:view=="recover"?82:70;
            if(travelContext!=null){travelContext.gameObject.SetActive(walking);float rounded=Owner.RemainingKnown?Mathf.Round(Owner.Remaining*2)*.5f:-1;if(contextTarget!=Owner.Exercise?.Target||contextRemaining!=rounded||travelContext.text.Length==0){contextTarget=Owner.Exercise?.Target;contextRemaining=rounded;SetText(travelContext,contextTarget+" · "+Owner.TargetDescription+" · "+(rounded>=0?"còn "+rounded.ToString("F1")+" m":"đang xác định quãng đường"));}}
            if(visualSettings!=null)RefreshVisualSettings();
            if(orientButton!=null)orientButton.interactable=Owner.HeadingProgress>=1&&Owner.Tracking;
            if(saveMarkerButton!=null){saveMarkerButton.interactable=Owner.CanSaveMarker;SetText(saveMarkerButton.GetComponentInChildren<Text>(),Owner.SaveMarkerButton);}
            if(!walking){hint.text=Owner.Message;
            if(choosingDestination)hint.text=Owner.TrialNavigation?"Chỉ đường thử theo bố trí bạn đã chọn. Chọn A–E rồi quét một mốc gần bạn; vị trí xấp xỉ.":"Chọn mốc đã ghi. Có thể bắt đầu từ vị trí hiện tại hoặc quét một mốc đã lưu gần bạn.";
            else if(Owner.Located&&Owner.Exercise!=null&&!Owner.Exercise.Complete)
                hint.text=Owner.NavigationPoseValid?Owner.Direction+" · Quan sát lối đi thực tế.":Owner.PoseFeedback;
            else if(view=="home")hint.text=Owner.FixedSummary;
            if(view=="orient"){hint.text=Owner.Message;hintLayout.preferredHeight=hintLayout.minHeight=118;SetText(heading,"Căn hướng tại "+Owner.DirectionOriginId);}}
            header.GetComponent<Image>().raycastTarget=Owner.SuiteRunning;header.GetComponent<Button>().interactable=Owner.SuiteRunning;
            if(Owner.SuiteRunning&&!showingSuite)SetText(status,"Bài "+Owner.SuiteCaseNumber+"/15 · "+Owner.Exercise?.Target+" · chạm kết thúc bài");
            if(view=="test-suite"){SetText(heading,"Bộ bài test");SetText(status,Owner.SuiteBadge);if(suiteDetails!=null)SetText(suiteDetails,Owner.SuiteInstructions);hintLayout.preferredHeight=hintLayout.minHeight=0;miniRect.gameObject.SetActive(false);}
            if(view=="import-review")hint.text=Owner.ImportSummary;
            if(view=="navigation-log"){SetText(heading,"Nhật ký chỉ đường");SetText(status,Owner.NavigationLogSummary.Split('\n')[0]);hint.text=Owner.NavigationExportStatus??Owner.NavigationLogSummary;hintLayout.preferredHeight=hintLayout.minHeight=140;foreach(var b in actions.GetComponentsInChildren<Button>())if(b.name=="Xuất ZIP lượt chỉ đường")b.interactable=Owner.HasNavigationLog&&!Owner.NavigationExportBusy;}
            else if(navigating&&Owner.NavigationRecordingFailed)SetText(status,"Nhật ký chưa lưu · mở Lưu / xuất để kiểm tra");
            bool scanning=active&&Owner.NeedsCalibration&&!Owner.AwaitingDirection;
            reticle.gameObject.SetActive(scanning);
            if(scanning)
            {
                float lower=actions.rect.height+14,upper=safe.rect.height-106;
                float height=Mathf.Min(341,Mathf.Max(120,upper-lower-130));
                reticle.anchorMin=reticle.anchorMax=new Vector2(.5f,0);reticle.pivot=new Vector2(.5f,.5f);
                reticle.anchoredPosition=new Vector2(0,lower+110+height*.5f);reticle.sizeDelta=new Vector2(height*5/6,height);
                ((RectTransform)progress.parent).sizeDelta=new Vector2(reticle.sizeDelta.x,5);
            }
            progress.sizeDelta=new Vector2(reticle.sizeDelta.x*Owner.ScanProgress,5);
            scan.text=Owner.ScanLabel;
            miniRect.gameObject.SetActive(Owner.HasData&&!scanning&&!showingSuite);
            caption.text=Owner.Located&&!Owner.NavigationPoseValid?"Vị trí chưa khớp lối\nChạm map kiểm tra":Owner.Located?"Xanh: bạn · cam: đích\nChạm để xem lớn":Owner.LastKnownPosition!=null?"Xám: vị trí lần cuối\nChạm để xem lớn":"Map 28/10 · chạm xem\nChưa định vị";
            edge.gameObject.SetActive(sample.hud&&sample.intent!=LabCueIntent.Hold);
            if(sample.hud&&sample.intent!=LabCueIntent.Hold)
            {
                var look=sample.intent==LabCueIntent.LookToRoute;
                screenGlyph.SetState(look,sample.cameraSide,look?0:sample.glyphBearing);
                coachPlate.color=new Color(.97f,.985f,1,.92f);SetText(edgeText,look?"Hướng máy về lối đi":"Chỉ dẫn trên màn hình");
                double now=Time.realtimeSinceStartupAsDouble;
                var d=Owner.Decision;hudMotion.Context(d?.referenceEpoch??0,d?.routeRevision??0,d?.maneuverId,look?LabCueMode.Look:LabCueMode.Hud,now);hudMotion.Sync(hudTokens,3,now);
                screenGlyph.SetMotion(hudMotion.Alpha(0,now,Owner.ReducedCueMotion),hudMotion.Alpha(1,now,Owner.ReducedCueMotion),hudMotion.Alpha(2,now,Owner.ReducedCueMotion));
                PlaceHudCue();

            }
            else hudMotion.Invalidate();
        }
        static void SetText(Text t,string value){if(t.text!=value)t.text=value;}
        void CompactActions()
        {
            var row=Rect(actions,"Navigation controls");Height(row.gameObject,48);var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=10;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;
            var buttons=actions.GetComponentsInChildren<Button>();foreach(var b in buttons)b.transform.SetParent(row,false);
        }
        void CacheExclusions()
        {
            CacheRect(0,header);CacheRect(1,miniRect);CacheRect(2,actions);CacheRect(3,visualSettings);
        }
        void CacheRect(int i,RectTransform r)
        {
            if(r==null||!r.gameObject.activeInHierarchy){exclusionsMin[i]=exclusionsMax[i]=new Vector2(-10,-10);return;}
            r.GetWorldCorners(viewportCorners);var lo=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var hi=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int k=0;k<4;k++)
            {
                var local=safe.InverseTransformPoint(viewportCorners[k]);var p=new Vector2(safeViewport.x+(local.x-safe.rect.xMin)/safe.rect.width*safeViewport.width,safeViewport.y+(local.y-safe.rect.yMin)/safe.rect.height*safeViewport.height);
                lo=Vector2.Min(lo,p);hi=Vector2.Max(hi,p);
            }
            exclusionsMin[i]=lo-new Vector2(.012f,.012f);exclusionsMax[i]=hi+new Vector2(.012f,.012f);
        }
        bool PointAllowed(Vector2 p)
        {for(int i=0;i<4;i++){if(i==1&&!miniRect.gameObject.activeSelf)continue;if(p.x>=exclusionsMin[i].x&&p.x<=exclusionsMax[i].x&&p.y>=exclusionsMin[i].y&&p.y<=exclusionsMax[i].y)return false;}return true;}
        bool BoundsAllowed(Rect bounds)
        {if(BlocksCameraGuidance)return false;
        var ai=Owner.CameraAiViewport;if(ai.width>0&&bounds.Overlaps(ai))return false;
        for(int i=0;i<4;i++){if(i==1&&!miniRect.gameObject.activeSelf)continue;var r=UnityEngine.Rect.MinMaxRect(exclusionsMin[i].x,exclusionsMin[i].y,exclusionsMax[i].x,exclusionsMax[i].y);if(bounds.Overlaps(r))return false;}return true;}
        void PlaceHudCue()
        {
            var c=hudCanvas;float scale=c.scaleFactor;var usable=GuideViewport;
            float w=164*scale/ViewWidth,h=168*scale/ViewHeight;
            var pin=Owner.NavigationPinBounds;bool found=false;Vector2 centre=Vector2.zero;
            for(int pass=0;pass<2&&!found;pass++)for(int k=0;k<9;k++)
            {
                var p=new Vector2(k%3==0?.5f:k%3==1?.28f:.72f,k/3==0?.48f:k/3==1?.36f:.60f);
                var bounds=new Rect(p.x-w*.5f,p.y-h*.5f,w,h);
                if(bounds.xMin<usable.xMin||bounds.xMax>usable.xMax||bounds.yMin<usable.yMin||bounds.yMax>usable.yMax||!BoundsAllowed(bounds))continue;
                if(pass==0&&pin.width>0&&bounds.Overlaps(pin))continue;
                if(pass==1)Owner.HideNavigationDestination();centre=p;found=true;break;
            }
            if(!found){edge.gameObject.SetActive(false);return;}
            var local=new Vector2(safe.rect.xMin+(centre.x-safeViewport.xMin)/safeViewport.width*safe.rect.width,safe.rect.yMin+(centre.y-safeViewport.yMin)/safeViewport.height*safe.rect.height);
            edge.anchorMin=edge.anchorMax=new Vector2(.5f,.5f);edge.anchoredPosition=local;
        }
        public void OpenVisualSettings()
        {
            if(visualSettings!=null)return;
            visualSettings=Panel(safe,"Visual cue settings",new Color(.97f,.985f,1,.99f));visualSettings.GetComponent<Image>().raycastTarget=true;
            Place(visualSettings,new Vector2(.5f,0),new Vector2(0,14),new Vector2(380,282),new Vector2(.5f,0));
            var title=Label(visualSettings,"Căn chỉnh mũi tên",20,true);Place(title.rectTransform,new Vector2(0,1),new Vector2(14,-10),new Vector2(352,30),new Vector2(0,1));
            settingsInfo=Label(visualSettings,"",13,false);Place(settingsInfo.rectTransform,new Vector2(0,1),new Vector2(14,-45),new Vector2(352,52),new Vector2(0,1));
            SettingsButton("Hạ 10 cm",14,-104,171,()=>Owner.AdjustVisualHeight(-.1f));SettingsButton("Nâng 10 cm",195,-104,171,()=>Owner.AdjustVisualHeight(.1f));
            SettingsButton("Khôi phục độ cao",14,-162,171,Owner.ResetVisualHeight);
            settingsMotion=SettingsButton("",195,-162,171,()=>{Owner.ToggleCueMotion();RefreshVisualSettings();}).GetComponentInChildren<Text>();
            SettingsButton("Xong",14,-220,352,()=>{visualSettings.gameObject.SetActive(false);Destroy(visualSettings.gameObject);visualSettings=null;});RefreshVisualSettings();
        }
        void RefreshVisualSettings()
        {
            if(settingsSource==Owner.VisualFloorSource&&settingsHeight==Owner.VisualFloorOffset&&settingsReduced==Owner.ReducedCueMotion&&settingsInfo.text.Length>0)return;settingsSource=Owner.VisualFloorSource;settingsHeight=Owner.VisualFloorOffset;settingsReduced=Owner.ReducedCueMotion;
            SetText(settingsInfo,"Chỉ đổi hình ảnh, không đổi vị trí.\n"+(Owner.VisualFloorSource=="Unavailable"?"Chưa có cao độ sàn":Owner.VisualFloorSource=="EstimatedPhoneHeight"?"Sàn ước lượng theo máy cao 1,35 m":"Bạn đã chỉnh độ cao")+" · "+Owner.VisualFloorOffset.ToString("+0.0;-0.0;0.0")+" m");
            SetText(settingsMotion,Owner.ReducedCueMotion?"Chuyển động: giảm":"Chuyển động: đầy đủ");
        }
        Button SettingsButton(string text,float x,float y,float width,Action action)
        {
            var box=Panel(visualSettings,text,new Color(.89f,.93f,1));Place(box,new Vector2(0,1),new Vector2(x,y),new Vector2(width,48),new Vector2(0,1));box.GetComponent<Image>().raycastTarget=true;
            var b=box.gameObject.AddComponent<Button>();b.targetGraphic=box.GetComponent<Image>();b.onClick.AddListener(()=>action());
            var label=Label(box,text,12,true);label.alignment=TextAnchor.MiddleCenter;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(5,0);label.rectTransform.offsetMax=new Vector2(-5,0);return b;
        }
        public void OpenMap(bool editing)
        {
            if(safe==null)return;
            var previous=safe.GetComponentInChildren<LabMapPanel>();if(previous!=null)Destroy(previous.gameObject);
            var panel=new GameObject("Lab map placement",typeof(RectTransform),typeof(LabMapPanel)).GetComponent<LabMapPanel>();panel.transform.SetParent(safe,false);mapPanel=panel;panel.Initialize(Owner,editing);
        }
        public void OpenSavedMarkers()
        {
            if(safe==null)return;
            if(savedMarkersPanel!=null)Destroy(savedMarkersPanel.gameObject);
            savedMarkersPanel=new GameObject("Saved markers",typeof(RectTransform),typeof(LabSavedMarkersPanel)).GetComponent<LabSavedMarkersPanel>();
            savedMarkersPanel.transform.SetParent(safe,false);savedMarkersPanel.Initialize(Owner);
        }
        void BuildActions()
        {
            foreach(var child in actions.Cast<Transform>().ToArray())if(child!=hint.transform){child.gameObject.SetActive(false);if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);}
            arrival=null;orientButton=null;travelContext=null;saveMarkerButton=null;
            if(view=="exercise"||view=="arrival"){travelContext=Label(actions,"",13,true);Height(travelContext.gameObject,36);travelContext.alignment=TextAnchor.MiddleLeft;}
            if(view=="test-suite")
            {
                var scrollBox=Panel(actions,"Test instructions",Color.white);Height(scrollBox.gameObject,190);
                scrollBox.GetComponent<Image>().raycastTarget=true;scrollBox.gameObject.AddComponent<RectMask2D>();
                var scroll=scrollBox.gameObject.AddComponent<ScrollRect>();scroll.viewport=scrollBox;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
                suiteDetails=Label(scrollBox,Owner.SuiteInstructions,13,false);suiteDetails.alignment=TextAnchor.UpperLeft;
                var details=suiteDetails.rectTransform;details.anchorMin=new Vector2(0,1);details.anchorMax=Vector2.one;details.pivot=new Vector2(.5f,1);details.offsetMin=new Vector2(8,0);details.offsetMax=new Vector2(-8,0);
                details.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;scroll.content=details;
                if(Owner.SuiteRunning)
                {
                    Button("Đúng mong đợi · lưu bài",()=>Owner.FinishSuiteCase("USER_OK"),true);
                    Button("Có lỗi · lưu để phân tích",()=>Owner.FinishSuiteCase("USER_ISSUE"),false);
                    Button("Tiếp tục bài đang chạy",()=>showingSuite=false,false);
                }
                else
                {
                    Button("Bắt đầu / làm lại bài này",()=>{if(Owner.BeginSuiteCase())showingSuite=false;},true).interactable=Owner.FixedReady;
                    Button("Bài tiếp theo / bỏ qua",Owner.NextSuiteCase,false);
                    Button("Bài trước",Owner.PreviousSuiteCase,false);
                }
                Button("Xuất một ZIP toàn bộ bài test",Owner.ExportTestSuite,false).interactable=!Owner.SuiteExportBusy;
                Button("Về app",()=>showingSuite=false,false);return;
            }
            if(view=="navigation-log")
            {
                Button("Xuất ZIP lượt chỉ đường",Owner.ExportNavigationRun,true).interactable=Owner.HasNavigationLog&&!Owner.NavigationExportBusy;
                Button("Xuất nhật ký lỗi / văng app",Owner.ExportStability,false).interactable=!Owner.StabilityExportBusy;
                Button("Về chỉ đường / trang chính",()=>showingNavigationLog=false,false);return;
            }
            if(view=="orient")
            {
                orientButton=Button("Đang nhìn về "+Owner.DirectionReferenceId+" · xác nhận",Owner.ConfirmDirection,true);
                if(Owner.TrialNavigation)Button("Đổi mốc cần nhìn",()=>{Owner.ChangeDirectionReference();view="";},false);
                Button("Quét lại mốc gần tôi",Owner.RetryCalibration,false);return;
            }
            if(Owner.NewScan)
            {
                if(view=="scan-help")
                {Button("Quét lại · giữ dữ liệu và đích",Owner.RetryFixedScan,true);Button("Về",Owner.Back,false);return;}
                if(view=="import-review")
                {Button("Dùng bộ mốc vừa chọn",Owner.ConfirmFixedImport,true);Button("Hủy nhập",Owner.CancelFixedImport,false);return;}
                if(view=="data")
                {
                    Button("Lượt chỉ đường đã lưu",()=>showingNavigationLog=true,true);
                    Button("Xem map 2D",()=>OpenMap(false),false);
                    Button("Mốc đã lưu / tọa độ",Owner.OpenSavedMarkers,false);
                    Button("Ghi lượt mới · giữ bản trước",()=>{showingData=false;Owner.RestartFixedSurvey();},false);
                    Button("Xuất gói map để giữ / nhập lại",Owner.ExportFixedPackage,false).interactable=Owner.FixedPublishedReady;
                    Button("Nhập gói map",Owner.PickFixedPackage,false);
                    Button("Xuất ZIP kiểm tra / bản nháp",Owner.Export,false);
                    Button("Về",()=>showingData=false,false);return;
                }
                if(view=="fixed-survey")
                {
                    saveMarkerButton=Button("Lưu mốc",Owner.ConfirmSaveMarker,true);
                    saveMarkerButton.interactable=Owner.CanSaveMarker;
                    Button("Ghi xong · gắn vị trí lên map",Owner.FinishFixedSurvey,false);
                    Button("Lưu nháp và về",Owner.LeaveFixedSurvey,false);return;
                }
                if(view=="destinations")
                {
                    foreach(string id in new[]{"A","B","C","D","E"})
                    {string target=id;var b=Button("Tới "+id+(Owner.CanChooseFixed(id)?"":" · chưa ghi"),()=>{choosingDestination=false;Owner.StartFixedDestination(target);},Owner.CanChooseFixed(id));b.interactable=Owner.CanChooseFixed(id);}
                    Button("Quay lại",()=>choosingDestination=false,false);return;
                }
                if(view=="recover")
                {Button("Quét mốc gần tôi",Owner.RetryCalibration,true);Button(Owner.SuiteRunning?"Kết thúc bài":"Lưu / xuất lượt",()=>{if(Owner.SuiteRunning)showingSuite=true;else showingNavigationLog=true;},false);Button("Dừng chỉ đường",()=>{Owner.SuspendSuiteCase();Owner.Back();},false);return;}
                if(view=="exercise"||view=="arrival")
                {
                    if(view=="arrival")arrival=Button("Xác nhận tới đích",Owner.ConfirmArrival,true);
                    else Button("Map / đổi đích",()=>OpenMap(false),false);
                    Button(Owner.SuiteRunning?"Kết thúc bài":"Lưu / xuất",()=>{if(Owner.SuiteRunning)showingSuite=true;else showingNavigationLog=true;},false);Button("Dừng",()=>{Owner.SuspendSuiteCase();Owner.Back();},false);CompactActions();return;
                }
                if(view=="locate")
                {Button("Chọn đích",()=>choosingDestination=true,true);Button("Quét mốc gần tôi",Owner.RetryCalibration,false);Button("Về",Owner.Back,false);return;}
                if(view=="done")
                {Button("Chọn đích khác",()=>choosingDestination=true,true);Button("Lưu / xuất lượt",()=>showingNavigationLog=true,false);Button("Về",Owner.Back,false);return;}
                Button(Owner.TrialNavigation?"Chỉ đường thử tới A–E":"Chỉ đường tới A–E",()=>choosingDestination=true,true).interactable=Owner.FixedReady;
                Button(Owner.FixedDraftAvailable?"Tiếp tục ghi lại vị trí A–E":"Ghi lại vị trí A–E",()=>Owner.BeginFixedSurvey(),false);
                if(Owner.FixedReady)Button("Vị trí của tôi",Owner.LocateFixed,false);
                Button("Bộ bài test / xuất dữ liệu",()=>showingSuite=true,false);
                Button("Map và dữ liệu",()=>showingData=true,false);return;
            }
            if(view=="destinations")
            {
                foreach(var id in new[]{"A","B","C"}){var selected=id;Button("Đi tới "+id,()=>{choosingDestination=false;Owner.BeginDestination(selected);},true);}
                Button("Quay lại",()=>choosingDestination=false,false);return;
            }
            if(view=="record")
            {
                Button("Dừng và lưu lượt ghi",Owner.StopRecording,true);
                return;
            }
            if(view=="exercise") {Button("Dừng bài thử",Owner.Back,false);return;}
            if(view=="locate") {Button("Về trang chính",Owner.Back,false);return;}
            if(view=="done"){Button("Về bài thử",Owner.Back,true);Button("Xuất ZIP kết quả",Owner.Export,false);return;}
            Button("Thử dẫn đường tới A / B / C",()=>choosingDestination=true,true);
            Button("Ghi lượt mới · 5 mốc A–E",Owner.RecordNew,false);
            if(Owner.HasData)Button("Ghi thêm một lối khác",Owner.RecordMore,false);
            Button("Gắn mốc A–E vào bản đồ lab",()=>OpenMap(true),false);
            if(Owner.HasData)Button("Xem vị trí của tôi",Owner.BeginLocate,false);
            Button("Thử 1 · quét hàng A → lưu tại B",()=>Owner.BeginExercise(false),false);
            Button("Thử 2 · tới B → lấy hàng → giao C",()=>Owner.BeginExercise(true),false);
            if(Owner.HasData)Button("Xuất ZIP dữ liệu thử",Owner.Export,false);
        }
        Button Button(string text,Action action,bool primary)
        {
            var box=Panel(actions,text,primary?Blue:new Color(.89f,.93f,1));Height(box.gameObject,48);box.GetComponent<Image>().raycastTarget=true;
            var button=box.gameObject.AddComponent<Button>();button.targetGraphic=box.GetComponent<Image>();button.onClick.AddListener(()=>action());
            var label=Label(box,text,13,true);label.color=primary?Color.white:Ink;label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(8,2);label.rectTransform.offsetMax=new Vector2(-8,-2);return button;
        }
        static void Height(GameObject obj,float n){var layout=obj.AddComponent<LayoutElement>();layout.preferredHeight=layout.minHeight=n;}
        Text Label(Transform parent,string text,int size,bool weight)
        {
            var result=new GameObject(text,typeof(RectTransform),typeof(Text)).GetComponent<Text>();result.transform.SetParent(parent,false);result.font=weight?bold:font;result.text=text;result.fontSize=size;result.color=Ink;result.raycastTarget=false;result.horizontalOverflow=HorizontalWrapMode.Wrap;result.verticalOverflow=VerticalWrapMode.Truncate;return result;
        }
        static RectTransform Rect(Transform parent,string name){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        static RectTransform Panel(Transform parent,string name,Color color){var rect=Rect(parent,name);var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return rect;}
        static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot){rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;}
    }

}

