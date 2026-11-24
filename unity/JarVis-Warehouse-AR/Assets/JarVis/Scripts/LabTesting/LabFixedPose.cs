using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public static class LabFixedPose
    {
        // T_world_map = T_world_marker * inverse(T_map_marker).
        // Storing the complete marker quaternion makes horizontal and wall cards
        // equivalent; projecting the card's normal to yaw would lose this information.
        public static bool TryWorldFromMap(Pose observed,LabPoint centre,float[] rotationInMap,out Pose result)
        {
            result=Pose.identity;
            if(!LabPaths.Valid(centre)||!LabPaths.Valid(new LabPoint(observed.position.x,observed.position.y,observed.position.z))||!LabPrintedCard.QuaternionValid(rotationInMap)||!LabPrintedCard.QuaternionValid(new[]{observed.rotation.x,observed.rotation.y,observed.rotation.z,observed.rotation.w}))return false;
            var q=new Quaternion(rotationInMap[0],rotationInMap[1],rotationInMap[2],rotationInMap[3]);
            var candidate=observed.rotation*Quaternion.Inverse(q);
            var forward=Vector3.ProjectOnPlane(candidate*Vector3.forward,Vector3.up);
            if(forward.sqrMagnitude<.81f)return false;
            var rotation=Quaternion.LookRotation(forward.normalized,Vector3.up);
            if(Quaternion.Angle(candidate,rotation)>8)return false;
            result=new Pose(observed.position-rotation*new Vector3(centre.x,centre.y,centre.z),rotation);return true;
        }
    }
}
