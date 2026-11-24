using UnityEngine;

namespace AtlasAR
{
    public static class AtlasRouteProgress
    {
        // A nearby parallel aisle must not teleport progress across a table.
        public static float Locate(Vector3[] route,Vector3 position,float previous,float travelledBudget,out float distance)
        {
            position.y=0;distance=float.PositiveInfinity;float best=previous,along=0;
            for(int i=1;i<route.Length;i++)
            {
                var a=route[i-1];var delta=route[i]-a;float length=delta.magnitude;
                if(length<.0001f)continue;
                float t=Mathf.Clamp01(Vector3.Dot(position-a,delta)/(length*length));float at=along+t*length;
                float gap=Vector3.Distance(position,a+t*delta);
                if(at>=previous-travelledBudget&&at<=previous+travelledBudget&&gap<distance){distance=gap;best=at;}
                along+=length;
            }
            return best;
        }
        public static string NextTurn(Vector3[] route,float progress,out float metres)
        {
            float at=0;metres=float.PositiveInfinity;
            for(int i=1;i<route.Length-1;i++)
            {
                at+=Vector3.Distance(route[i-1],route[i]);if(at<progress-.25f)continue;
                float angle=Vector3.SignedAngle(route[i]-route[i-1],route[i+1]-route[i],Vector3.up);
                if(Mathf.Abs(angle)<35)continue;
                metres=Mathf.Max(0,at-progress);return Mathf.Abs(angle)>135?"Quay lại":angle>0?"Rẽ phải":"Rẽ trái";
            }
            return "Đi thẳng";
        }
    }
}
