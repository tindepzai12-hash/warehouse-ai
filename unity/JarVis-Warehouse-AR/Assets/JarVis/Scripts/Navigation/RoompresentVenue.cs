using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Immersal;
using Immersal.Samples.Navigation;

namespace JarVis.Warehouse.Navigation
{
    // Venue-specific readiness; independent scans must be registered before AR use.
    [DefaultExecutionOrder(-900)]
    public sealed class RoompresentVenue : MonoBehaviour
    {
        public int mapId = 153613;
        public string startMarkerAlias = "E -> A";
        public bool alignmentVerified;
        [System.NonSerialized] public bool previewAlignment;
        public GameObject referenceScan;
        public GameObject navigationUi;
        public GameObject targets;
        public Renderer[] floorRenderers;
        bool guidanceVisible;
        bool paused, wasLocalized;
        float nextRecovery;
        public bool IsLocalized
        {
            get
            {
                var sdk = ImmersalSDK.Instance;
                var status = sdk == null ? null : sdk.TrackingAnalyzer?.TrackingStatus;
                return !paused && ARSession.state == ARSessionState.SessionTracking && status != null && status.LocalizationSuccessCount > 0 && status.TrackingQuality > 0;
            }
        }
        [TextArea] public string placementEvidence = "A: code on presentation table. B: left book cart. C: right book cart, from the owner-confirmed view. Positions are scan picks, not surveyed physical coordinates.";

        void Awake()
        {
            if (referenceScan != null) referenceScan.SetActive(false);
            if (floorRenderers != null) foreach (var item in floorRenderers) if (item != null) item.enabled = false;
            SetGuidance(false);
        }

        void Update()
        {
            // The localization session remains alive while unreliable guidance is hidden.
            // StartSession is idempotent and also recovers a session stopped by an SDK error.
            if (!paused && !IsLocalized && Time.unscaledTime >= nextRecovery)
            {
                nextRecovery = Time.unscaledTime + 5f;
                if (ImmersalSDK.Instance != null && ImmersalSDK.Instance.IsReady) ImmersalSDK.Instance.Session?.StartSession();
            }
            if (wasLocalized != IsLocalized)
            {
                wasLocalized = IsLocalized;
                Debug.Log("[Roompresent] " + (wasLocalized ? "Tracking recovered; resuming current mission." : "Tracking lost; relocalization continues; mission retained."));
            }
            bool ready=alignmentVerified && IsLocalized;
            if(ready!=guidanceVisible)SetGuidance(ready);
            if (targets != null) targets.SetActive(ready || previewAlignment && IsLocalized);
            if (!ready) foreach (var path in FindObjectsByType<NavigationPath>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var renderer in path.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        }
        void OnApplicationPause(bool value) { paused = value; if (!value) nextRecovery = 0; }

        void SetGuidance(bool visible)
        {
            guidanceVisible=visible;
            if(navigationUi!=null)navigationUi.SetActive(visible);
            if(targets!=null)targets.SetActive(visible);
            foreach(var path in FindObjectsByType<NavigationPath>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                foreach(var renderer in path.GetComponentsInChildren<Renderer>(true))renderer.enabled=visible;
        }

        void OnGUI()
        {
            if (GetComponent<RoompresentDeliveryAR>() != null) return;
            if (guidanceVisible) return;
            GUI.Box(new Rect(16, 70, Mathf.Min(Screen.width - 32, 720), 72),
                alignmentVerified ? "Look around the room to localize. AR guidance resumes when tracking is available." :
                "Roompresent: map registration pending.\nAlign the Immersal point cloud with the Scaniverse room before AR guidance.");
        }
    }
}
