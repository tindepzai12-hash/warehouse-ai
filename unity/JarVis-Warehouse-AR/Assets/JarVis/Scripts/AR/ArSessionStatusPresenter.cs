using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace JarVis.Warehouse.AR
{
    public sealed class ArSessionStatusPresenter : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Text statusLabel;

        private ARSessionState displayedState = (ARSessionState)(-1);

        private void OnEnable()
        {
            RefreshStatus();
        }

        private void Update()
        {
            if (displayedState != ARSession.state)
                RefreshStatus();
        }

        private void RefreshStatus()
        {
            displayedState = ARSession.state;
            if (statusLabel == null)
                return;

            statusLabel.text = displayedState switch
            {
                ARSessionState.SessionTracking => "AR TRACKING",
                ARSessionState.CheckingAvailability => "AR CHECKING",
                ARSessionState.NeedsInstall => "AR INSTALL REQUIRED",
                ARSessionState.Installing => "AR INSTALLING",
                ARSessionState.Ready => "AR READY",
                ARSessionState.Unsupported => "AR UNSUPPORTED",
                _ => "AR STARTING"
            };
        }
    }
}
