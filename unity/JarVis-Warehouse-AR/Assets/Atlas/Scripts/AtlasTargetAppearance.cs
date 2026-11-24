using UnityEngine;

namespace AtlasAR
{
    public static class AtlasTargetAppearance
    {
        // Keep the target Transform at saved XYZ; put only the pin's visual tip above the surface.
        // Owner raised A's visual in the review. Preserve that presentation only;
        // the fixed QR pose and Navigation Target Transform must not move with it.
        public static float DisplayLift(string markerId)=>markerId=="A"?.43f:0f;
        public static void PlaceOnMarker(Transform target,string markerId=null)
        {
            foreach(var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                if(renderer.name.Contains("ground")||renderer.name.Contains("Marker centre")||renderer.GetComponent<TextMesh>()!=null)continue;
                var bottom=target.InverseTransformPoint(new Vector3(renderer.bounds.center.x,renderer.bounds.min.y,renderer.bounds.center.z));
                renderer.transform.position+=target.up*(.015f+DisplayLift(markerId)-bottom.y);
            }
        }
    }
}
