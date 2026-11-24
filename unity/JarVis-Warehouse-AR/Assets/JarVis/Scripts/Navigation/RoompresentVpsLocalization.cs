using System;
using System.Collections;
using System.Threading.Tasks;
using Immersal;
using Immersal.XR;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.Navigation
{
    // The pose compared here is map-to-tracker, NOT the moving camera pose.
    public sealed class RoompresentPoseGate
    {
        public const int Required = 3;
        public int Consistent { get; private set; }
        public float LastAccepted { get; private set; } = -100;
        public bool Acquired => Consistent >= Required;
        // Field-tuned guard: the opposite-facing matches observed on Samsung
        // had confidence 12-13; the repeatable view had confidence 21-23.
        public static bool QualityAllowed(int confidence,double rmse) => confidence>=20&&double.IsFinite(rmse)&&rmse>=0&&rmse<=3;
        Vector3 position;
        Quaternion rotation;
        float latest = -100;
        public void Invalidate() { Consistent = 0; LastAccepted = latest = -100; }
        public bool Fresh(float now) => Consistent >= Required && now >= LastAccepted && now - LastAccepted <= 8;
        public bool Observe(int id, Vector3 p, Quaternion q, float now,int confidence=50,double rmse=0)
        {
            if (id != 153613 || !Finite(p) || !Finite(q) || !float.IsFinite(now)) { Invalidate(); return false; }
            if(!QualityAllowed(confidence,rmse))return false;
            q = Quaternion.Normalize(q);
            // A map fix stays in the same VIO frame while ARCore is tracking.
            // Sparse VPS matches need not arrive within the freshness display window.
            if (now < latest || (!Acquired && now - latest > 60) || Consistent == 0 || Vector3.Distance(position,p) > .3f || Quaternion.Angle(rotation,q) > 8)
            { Consistent = 1; position = p; rotation = q; LastAccepted = -100; }
            else Consistent = Math.Min(Required, Consistent + 1);
            latest = now;
            // Compare to a fixed first solution until acquired; then allow bounded drift correction.
            if (Consistent < Required) return false;
            position = p; rotation = q; LastAccepted = now; return true;
        }
        public static bool Finite(Vector3 p) => float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z);
        static bool Finite(Quaternion q) => float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w)&&q.x*q.x+q.y*q.y+q.z*q.z+q.w*q.w>.5f;
    }

    [DefaultExecutionOrder(-950)]
    public sealed class RoompresentVpsLocalization : MonoBehaviour, ISceneUpdater
    {
        public SceneUpdater nativeUpdater;
        public ARCameraManager cameraManager;
        public ImmersalSDK sdk;
        public TextAsset mapFile;
        public string mapFingerprint, scanFingerprint;
        public readonly RoompresentPoseGate gate = new RoompresentPoseGate();
        public int Attempts { get; private set; }
        public int Matches { get; private set; }
        public int Accepted { get; private set; }
        public int QualityRejected { get; private set; }
        public string Failure { get; private set; }
        public int Revision { get; private set; }
        public bool AutoFocus { get; private set; }
        public bool MarkerOwnsFrame { get; set; }
        float searchStarted;
        bool detailedImages;
        public bool SearchSlow => !Localized && Time.unscaledTime-searchStarted>=45;
        public void RetryLocalization()
        {
            Invalidate("retry requested");searchStarted=Time.unscaledTime;retryAt=0;
            // Keep the ARCore coordinate frame and task; never reset to camera-at-A.
            if(sdk!=null&&sdk.IsReady)sdk.Session?.StartSession();
        }
        public void ToggleFocus()
        {
            SetAutoFocus(!AutoFocus);
        }
        float nextFocusRefresh;
        Coroutine focusRoutine;
        bool cameraRestarting;
        public bool FocusRefreshing {get;private set;}
        // Reopen only the image subsystem. ARSession, origin, planes and anchors remain intact.
        public void RefreshCameraFocus()
        {
            if(cameraManager==null||FocusRefreshing||Time.unscaledTime<nextFocusRefresh)return;
            nextFocusRefresh=Time.unscaledTime+12;focusRoutine=StartCoroutine(Refocus());
        }
        IEnumerator Refocus()
        {
            FocusRefreshing=true;cameraRestarting=true;
            cameraManager.autoFocusRequested=false;
            cameraManager.enabled=false;
            yield return null;
            yield return null;
            cameraManager.enabled=true;cameraRestarting=false;
            float reopened=Time.unscaledTime;
            SetAutoFocus(true);
            float deadline=reopened+5;
            while(!paused&&frameAt<reopened&&Time.unscaledTime<deadline)yield return null;
            FocusRefreshing=false;focusRoutine=null;
            Debug.Log("[RoompresentVPS] Camera reopened; freshFrame="+(frameAt>=reopened)+"; same AR session, origin and anchors retained.");
        }
        public void SetAutoFocus(bool value)
        {
            AutoFocus=value;
            if(cameraManager!=null)cameraManager.autoFocusRequested=AutoFocus;
            Debug.Log("[RoompresentVPS] focus="+(AutoFocus?"auto":"room-fixed"));
        }
        bool paused, wasCamera, subscribed, applying;
        int epoch;
        float frameAt = -100, retryAt, settleUntil;
        Localizer localizer;
        public bool CameraReady => !paused && cameraManager != null && cameraManager.enabled && ARSession.state == ARSessionState.SessionTracking && ARSession.notTrackingReason == NotTrackingReason.None && Time.unscaledTime-frameAt < .8f;
        public bool Localized => CameraReady && gate.Acquired && !applying && Time.unscaledTime>=settleUntil;
        void Awake()
        {
            searchStarted=Time.unscaledTime;
            // Room localization uses the ARCore fixed-focus tracking mode.
            // The separate barcode scanner retains its close-range autofocus.
            if(cameraManager!=null)cameraManager.autoFocusRequested=false;
            if(sdk!=null&&sdk.Session is ImmersalSession session){session.SessionUpdateInterval=.7f;session.BurstMode=false;}
        }
        void OnEnable() { if(cameraManager!=null){cameraManager.frameReceived-=Frame;cameraManager.frameReceived+=Frame;}nextFocusRefresh=Time.unscaledTime+8; }
        void Frame(ARCameraFrameEventArgs _) { frameAt=Time.unscaledTime; }
        void Update()
        {
            if(sdk!=null&&sdk.IsReady&&!detailedImages)
            {
                sdk.downsample=false;detailedImages=true;
                var size=cameraManager.currentConfiguration;
                cameraManager.TryGetIntrinsics(out var intrinsics);
                Debug.Log("[RoompresentVPS] full-resolution localization image="+intrinsics.resolution+" camera="+size+" focusRequested="+cameraManager.autoFocusRequested);
            }
            if(!subscribed && sdk!=null && sdk.Localizer is Localizer value)
            {localizer=value;localizer.OnLocalizationResult.AddListener(Result);subscribed=true;}
            // Fresh frames can continue during poor tracking: do not restart for blur,
            // weak map matches or missing QR. Restart only a feed that has stopped.
            if(!paused&&cameraManager!=null&&!FocusRefreshing&&Time.unscaledTime>=nextFocusRefresh&&Time.unscaledTime-frameAt>5)
                RefreshCameraFocus();
            bool ready=CameraReady;
            if(Localized)searchStarted=Time.unscaledTime;
            if(wasCamera&&!ready)Invalidate("camera tracking interrupted");
            wasCamera=ready;
            if(!paused&&!MarkerOwnsFrame&&Time.unscaledTime>=retryAt)
            {
                retryAt=Time.unscaledTime+3;
                if(sdk!=null&&sdk.IsReady)sdk.Session?.StartSession();
                Debug.Log("[RoompresentVPS] attempts="+Attempts+" matches="+Matches+" accepted="+Accepted+" rejected="+QualityRejected+" stable="+gate.Consistent+" localized="+Localized+" camera="+ready+" fixAge="+(Time.unscaledTime-gate.LastAccepted).ToString("F1")+" mapLoaded="+MapHandleMapping.TryGetHandle(153613,out _));
            }
        }
        void Result(ILocalizationResults results)
        {
            Attempts++;
            if(results?.Results==null)return;
            foreach(var result in results.Results)if(result.Success&&result.MapId==153613)Matches++;
        }
        public async Task UpdateScene(MapEntry entry, ICameraData camera, ILocalizationResult result)
        {
            int request=epoch;
            if(MarkerOwnsFrame||!CameraReady||camera==null||entry?.Map==null||result==null||!result.Success)return;
            if(entry.Map.mapId!=153613||result.MapId!=153613){Invalidate("unexpected map");return;}
            var info=result.LocalizeInfo;
            if(!double.IsFinite(info.rmse)||info.rmse<0||info.confidence<0||!RoompresentPoseGate.Finite(info.position)) {Invalidate("invalid native pose");return;}
            if(!RoompresentPoseGate.QualityAllowed(info.confidence,info.rmse)){QualityRejected++;Debug.Log("[RoompresentVPS] rejected weak match confidence="+info.confidence+" rmse="+info.rmse.ToString("F3"));return;}
            var pos=info.position;var rot=info.rotation*camera.ScreenOrientation;
            pos.SwitchHandedness();rot.SwitchHandedness();
            var relation=entry.Relation;
            var mapPose=Matrix4x4.TRS(relation.Position,relation.Rotation,Vector3.one)*Matrix4x4.TRS(Vector3.Scale(pos,relation.Scale),rot,Vector3.one);
            var tracker=Matrix4x4.TRS(camera.CameraPositionOnCapture,camera.CameraRotationOnCapture,Vector3.one);
            var candidate=tracker*mapPose.inverse;
            if(!candidate.ValidTRS()){Invalidate("invalid map transform");return;}
            bool accept=gate.Observe(result.MapId,candidate.GetPosition(),candidate.rotation,Time.unscaledTime,info.confidence,info.rmse);
            Debug.Log("[RoompresentVPS] candidate stable="+gate.Consistent+" position="+candidate.GetPosition().ToString("F3")+" euler="+candidate.rotation.eulerAngles.ToString("F1")+" confidence="+info.confidence+" rmse="+info.rmse.ToString("F3"));
            if(!accept)return;
            applying=true;
            try
            {
                await nativeUpdater.UpdateScene(entry,camera,result);
                if(this==null||request!=epoch||!CameraReady)return;
                Accepted++;Revision++;Failure=null;
                if(Accepted==1)settleUntil=Time.unscaledTime+.5f;
                Debug.Log("[RoompresentVPS] accepted map=153613 confidence="+info.confidence+" reprojectionRmse="+info.rmse.ToString("F3")+" cameraMap="+pos.ToString("F3"));
            }
            catch(Exception e){Invalidate("scene update "+e.GetType().Name);}
            finally{applying=false;}
        }
        void Invalidate(string reason){epoch++;gate.Invalidate();Failure=reason;settleUntil=Time.unscaledTime+.5f;}
        void CancelCameraRefresh()
        {
            if(focusRoutine!=null)StopCoroutine(focusRoutine);
            focusRoutine=null;
            if(cameraRestarting&&cameraManager!=null)cameraManager.enabled=true;
            cameraRestarting=false;FocusRefreshing=false;
        }
        void OnApplicationPause(bool value){CancelCameraRefresh();nextFocusRefresh=Time.unscaledTime+8;paused=value;Invalidate(value?"app paused":"relocalizing after resume");frameAt=-100;wasCamera=false;retryAt=0;}
        void OnDisable(){CancelCameraRefresh();Invalidate("scene closed");if(cameraManager!=null)cameraManager.frameReceived-=Frame;if(localizer!=null&&subscribed)localizer.OnLocalizationResult.RemoveListener(Result);subscribed=false;}
    }
}
