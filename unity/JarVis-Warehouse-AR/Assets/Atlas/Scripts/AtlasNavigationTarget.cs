using UnityEngine;

namespace AtlasAR
{
    // This transform is the saved physical marker centre, not the user's arrival floor point.
    // Parent map-to-AR alignment comes from fixed A; all saved local coordinates remain unchanged.
    public sealed class AtlasNavigationTarget:MonoBehaviour
    {
        public string markerId;
        public Vector3 savedMapPosition;
        public Vector3 arrivalMapPosition;
        public Transform arrivalPose;
    }
}
