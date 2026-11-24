using System;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public sealed partial class LabTestSession
    {
        long decisionSequence;int routeRevision;
        double nextDecision;
        readonly LabCameraCoach cameraCoach=new LabCameraCoach();
        readonly LabTurnTracker turnTracker=new LabTurnTracker();
        readonly LabMovementEstimate movement=new LabMovementEstimate();
        LabPoint renderAim;int renderRevision=-1;string renderManeuver;
        readonly LabPresentationSample presentation=new LabPresentationSample();
        LabNavigationDecision decision;
        readonly LabRouteCueSampler routeCueSampler=new LabRouteCueSampler();
        readonly LabCueToken[] routeCueTokens=new LabCueToken[3];
        readonly LabPoint footprintA=new LabPoint(),footprintB=new LabPoint();
        readonly LabFloorVisualReference floorVisual=new LabFloorVisualReference();
        long tokenDecision=-1;int routeCueCount;
        bool reducedMotion;bool preferencesLoaded;
        string labelTitle;LabCueIntent labelIntent;int labelSide;bool labelHorizontal;LabFloorVisualSource labelFloor;
        float labelTurn=float.NaN,labelRemaining=float.NaN;
        public bool ReducedCueMotion {get {LoadCuePreferences();return reducedMotion;}}
        public float VisualFloorOffset=>floorVisual.Offset;
        public string VisualFloorSource=>floorVisual.Source==LabFloorVisualSource.Unavailable?"Unavailable":floorVisual.Source==LabFloorVisualSource.EstimatedPhoneHeight?"EstimatedPhoneHeight":"UserAdjusted";
        public void HideNavigationDestination(){navigationVisual?.HideDestination();}
        public bool CameraGuidanceOccluded=>ui!=null&&ui.BlocksCameraGuidance;
        public Rect CameraAiViewport=>cameraAi!=null?cameraAi.OccupiedViewport:Rect.zero;
        public Rect NavigationPinBounds=>navigationVisual!=null&&navigationVisual.PinVisible?navigationVisual.PinBounds:Rect.zero;
        public string TargetDescription=>Exercise?.Target=="A"?"Bàn máy tính":Exercise?.Target=="B"?"Bàn tường trắng":Exercise?.Target=="C"?"Bàn cạnh cửa sổ":"Mốc cố định "+Exercise?.Target;
        void LoadCuePreferences(){if(preferencesLoaded)return;reducedMotion=PlayerPrefs.GetInt("JarVisLab.reducedCueMotion",0)!=0;preferencesLoaded=true;}
        public void ToggleCueMotion(){LoadCuePreferences();reducedMotion=!reducedMotion;PlayerPrefs.SetInt("JarVisLab.reducedCueMotion",reducedMotion?1:0);}
        public void AdjustVisualHeight(float delta){floorVisual.Adjust(delta);}
        public void ResetVisualHeight(){floorVisual.Restore();}
        public void OpenVisualSettings(){ui?.OpenVisualSettings();}
        public LabNavigationDecision Decision=>decision;
        public LabPresentationSample Presentation=>presentation;
        public bool NavigationInProgress=>Exercise!=null&&!Exercise.Complete&&journeyStarted&&!AwaitingDirection;
        public double RecoverySeconds=>trackingRecovery.Active?Math.Max(0,Time.realtimeSinceStartupAsDouble-trackingRecovery.Began):0;
        public string RecoveryStage=>trackingRecovery.Stage;
        public double LastKnownAge=>lastPoseTime>0?Math.Max(0,Time.realtimeSinceStartupAsDouble-lastPoseTime):0;
        void PublishDecision(double now,LabPoint aim=null,bool arrived=false,string reason=null)
        {
            bool usable=NavigationPoseValid&&aim!=null&&!arrived&&reason==null;
            float glyph=turnTracker.Phase!=LabTurnPhase.Cruise?
                currentTurn?.type=="RIGHT"?90:currentTurn?.type=="LEFT"?-90:currentTurn?.type=="UTURN"?180:0:0;
            if(activeRoute!=null&&(visualRoute==null||visualRoute.epoch!=epoch))visualRoute=new LabRouteVisualContext(activeRoute.points,epoch,routeRevision,Exercise?.Target);
            decision=new LabNavigationDecision(++decisionSequence,routeRevision,epoch,now,usable,arrived,Exercise?.Target,ManeuverTitle,reason,aim,
                Remaining,TurnDistance,glyph,turnTracker.Phase,currentTurn?.id,visualRoute,routeProgress.Along,
                currentTurn!=null?currentTurn.along-.3f:float.PositiveInfinity,currentTurn?.point);
        }
        // Called in HUD LateUpdate: one presentation sample for labels/icon/pin, with the latest camera.
        public void RefreshPresentation()
        {
#if UNITY_EDITOR
            if((reviewLocated||reviewCameraTracking)&&!reviewRuntime){RefreshReviewPresentation();return;}
#endif
            presentation.decisionId=decision?.id??0;presentation.cameraSampleId=cameraSampleId;presentation.hud=false;
            presentation.intent=LabCueIntent.Hold;presentation.reason=null;
            presentation.visibleTokens=0;presentation.mode=LabCueMode.None;
            if(!NavigationInProgress){presentation.title=null;navigationVisual?.Hide();return;}
            if(CameraGuidanceOccluded){Hold("CHỈ ĐƯỜNG ĐANG CHẠY","Đích và vị trí vẫn được giữ khi xem dữ liệu.","PANEL_OPEN");return;}
            if(RecoveryNeedsMarker){Hold("CẦN CĂN LẠI VỊ TRÍ","Quét một thẻ A–E đã lưu gần bạn.",routeError);return;}
            if(!Tracking||recovering)
            {
                string stage=trackingRecovery.Stage;
                string text=stage=="POSE_STABILIZING"?"GIỮ MÁY ỔN ĐỊNH":stage=="MAP_VALIDATING"?"KIỂM TRA VỊ TRÍ":"ĐANG TÌM LẠI VỊ TRÍ";
                string sub=stage=="POSE_STABILIZING"?Mathf.FloorToInt(RecoveryProgress*100)+"% kiểm tra ổn định":stage=="MAP_VALIDATING"?"Đang đối chiếu với lối đi":"Đã chờ "+Mathf.FloorToInt((float)RecoverySeconds)+" giây · giữ đích "+Exercise.Target;
                Hold(text,sub,trackingRecovery.Reason);return;
            }
            if(!NavigationPoseValid){Hold(LabPoseFeedback.Title(PoseReason),"Đứng yên · giữ đích "+Exercise.Target,PoseReason);return;}
            if(decision==null||decision.referenceEpoch!=epoch||decision.routeRevision!=routeRevision||decision.target!=Exercise.Target)
            {Hold("ĐANG CẬP NHẬT TUYẾN","Giữ đích · chọn đường từ vị trí hiện tại.","STALE_DECISION");return;}
            if(decision.arrived){Hold("ĐÃ GẦN "+Exercise.Target,"Đối chiếu bàn đích rồi xác nhận.","ARRIVED");ShowDestination();return;}
            if(!decision.usable){Hold(ManeuverTitle,Direction,decision.reason);return;}
            var camera=CameraOwner!=null?CameraOwner.arCamera:null;
            if(camera==null||MapFrame==null||!decision.VisualMatches){Hold("ĐANG CẬP NHẬT TUYẾN","Đang chuẩn bị chỉ dẫn cùng bản đồ.","INVALID_VISUAL_CONTEXT");return;}
            bool resetAim=renderAim==null||renderRevision!=decision.routeRevision||renderManeuver!=decision.maneuverId||!floorRouter.KnownFloorSegment(renderAim,decision.aim);
            if(renderAim==null)renderAim=new LabPoint();
            if(resetAim){renderAim.x=decision.aim.x;renderAim.y=0;renderAim.z=decision.aim.z;}
            else {float t=1-Mathf.Exp(-Time.unscaledDeltaTime/.08f);renderAim.x=Mathf.Lerp(renderAim.x,decision.aim.x,t);renderAim.z=Mathf.Lerp(renderAim.z,decision.aim.z,t);}
            renderRevision=decision.routeRevision;renderManeuver=decision.maneuverId;
            bool turnBoard=decision.phase!=LabTurnPhase.Cruise&&decision.glyphBearing!=0&&
                (decision.title.Contains("RẼ")||decision.title=="QUAY ĐẦU");
            var boardPoint=turnBoard&&decision.turnPoint!=null?decision.turnPoint:renderAim;
            Vector3 world=MapFrame.TransformPoint(new Vector3(boardPoint.x,.75f+floorVisual.Offset,boardPoint.z));
            var coachWorld=world;
            if(turnBoard&&decision.turnDistance<.35f&&decision.visualRoute.Sample(decision.along+.7f,out var afterCorner))
                coachWorld=MapFrame.TransformPoint(new Vector3(afterCorner.x,.75f,afterCorner.z));
            var f=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up);
            var direction=Vector3.ProjectOnPlane(coachWorld-camera.transform.position,Vector3.up);
            bool horizontal=f.sqrMagnitude>.04f&&direction.sqrMagnitude>.001f;
            GuideBearing=horizontal?Vector3.SignedAngle(f,direction,Vector3.up):0;
            var intent=cameraCoach.Update(true,horizontal,GuideBearing);
            presentation.intent=intent;presentation.bearing=GuideBearing;presentation.cameraSide=cameraCoach.Side;
            presentation.glyphBearing=decision.glyphBearing!=0?decision.glyphBearing:GuideBearing;
            UpdateTravelLabels(intent,horizontal,turnBoard);
            ShowDestination();
            if(intent==LabCueIntent.LookToRoute)
            {navigationVisual?.HideCue();presentation.hud=true;presentation.reason="LOOK_TO_ROUTE";presentation.mode=LabCueMode.Look;}
            else
            {
                var visual=EnsureNavigationVisual();bool visible=false;double now=Time.realtimeSinceStartupAsDouble;
                var safe=ui!=null?ui.GuideViewport:new Rect(.04f,.12f,.92f,.73f);
                if(turnBoard||floorVisual.Source==LabFloorVisualSource.Unavailable)
                {
                    presentation.mode=LabCueMode.TurnBoard;
                    visible=visual!=null&&visual.ShowBoard(camera,world,presentation.glyphBearing,decision,safe,ui?.GuideBoundsAllowed,now,ReducedCueMotion);
                }
                else
                {
                    if(tokenDecision!=decision.id)
                    {
                        tokenDecision=decision.id;int n=routeCueSampler.Select(decision.visualRoute,decision.along,decision.stopAt,routeCueTokens);routeCueCount=0;
                        for(int i=0;i<n;i++)if(LabRouteCueSampler.Footprint(routeCueTokens[i],floorRouter,footprintA,footprintB))routeCueTokens[routeCueCount++]=routeCueTokens[i];
                    }
                    presentation.mode=LabCueMode.Path;
                    visible=visual!=null&&visual.ShowPath(camera,MapFrame,decision,routeCueTokens,routeCueCount,.08f+floorVisual.Offset,safe,ui?.GuideBoundsAllowed,now,ReducedCueMotion);
                }
                presentation.hud=!visible;presentation.reason=visual?.Reason??"HUD_ONLY";
                presentation.visibleTokens=visual?.VisibleCount??0;
            }
            guideEdge=presentation.hud;
        }
        void UpdateTravelLabels(LabCueIntent intent,bool horizontal,bool turnBoard)
        {
            float remaining=Mathf.Round(decision.remaining*2)*.5f;
            float turn=float.IsInfinity(decision.turnDistance)?-1:Mathf.Round(decision.turnDistance*10)*.1f;
            if(labelTitle==decision.title&&labelIntent==intent&&labelSide==cameraCoach.Side&&labelHorizontal==horizontal&&labelFloor==floorVisual.Source&&labelTurn==turn&&labelRemaining==remaining)return;
            labelTitle=decision.title;labelIntent=intent;labelSide=cameraCoach.Side;labelHorizontal=horizontal;labelFloor=floorVisual.Source;labelTurn=turn;labelRemaining=remaining;
            presentation.title=intent==LabCueIntent.LookToRoute?cameraCoach.Title(horizontal):!turnBoard&&remaining<1?"TIẾP CẬN "+decision.target:decision.title;
            presentation.subtitle=intent==LabCueIntent.LookToRoute?"Hướng máy về lối · tiếp theo: "+decision.title.ToLowerInvariant():
                (turnBoard&&turn>=0?"Rẽ sau "+turn.ToString("F1")+" m":"Đi theo lối đã chọn")+(PreviewMode?" · Ước lượng":"");
        }
        void Hold(string title,string subtitle,string reason)
        {
            presentation.intent=LabCueIntent.Hold;presentation.title=title;presentation.subtitle=subtitle;presentation.reason=reason;presentation.mode=LabCueMode.None;presentation.visibleTokens=0;labelTitle=null;guideEdge=false;navigationVisual?.Hide();
        }
#if UNITY_EDITOR
        void RefreshReviewPresentation()
        {
            presentation.intent=LabCueIntent.Hold;presentation.hud=false;
            if(!reviewLocated)
            {presentation.title=reviewCameraTracking?"CẦN CĂN LẠI VỊ TRÍ":"ĐANG TÌM LẠI VỊ TRÍ";presentation.subtitle=reviewCameraTracking?"Mở map để chọn mốc · giữ đích C":"3 giây · nhìn vùng có chi tiết";return;}
            presentation.title=ManeuverTitle;presentation.subtitle="Tới "+Exercise?.Target+" · 8,5 m";
            if(NavigationPoseValid&&!AtTarget){presentation.intent=guideEdge?LabCueIntent.LookToRoute:LabCueIntent.Travel;presentation.hud=true;presentation.bearing=65;presentation.glyphBearing=ManeuverTitle.Contains("PHẢI")?90:ManeuverTitle.Contains("TRÁI")?-90:ManeuverTitle=="QUAY ĐẦU"?180:0;if(guideEdge){presentation.title="XOAY MÁY SANG PHẢI";presentation.subtitle="Tiếp theo: rẽ phải";}}
        }
#endif
    }
}
