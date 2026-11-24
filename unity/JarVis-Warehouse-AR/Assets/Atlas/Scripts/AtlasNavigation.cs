using System;
using UnityEngine;

namespace AtlasAR
{
    // One metric polyline supplies the map, turn instructions and AR chevrons.
    public sealed class AtlasNavigation
    {
        public bool Active {get;private set;}
        public string Target {get;private set;}="";
        public string Instruction {get;private set;}="Chọn điểm rồi bấm Đi tới";
        public float Remaining {get;private set;}
        public Vector3[] Points {get;private set;}=Array.Empty<Vector3>();
        readonly AtlasNavMeshService planner;
        public string CameraCue {get;private set;}="";
        public float NextTurnMetres {get;private set;}
        public Rect GuidanceViewport=new Rect(.08f,.48f,.84f,.34f);
        public Vector3 Arrival(string id)=>planner.Arrival(id);
        Transform root; readonly Transform[] arrows=new Transform[3]; Mesh arrowMesh;Vector3[] arrowVertices;
        readonly MeshRenderer[] renderers=new MeshRenderer[3];readonly float[] opacity=new float[3];
        readonly MaterialPropertyBlock colour=new MaterialPropertyBlock();
        float nextPlan,progress,total,revealAt,lastTick;Vector3 lastPosition;bool observed;
        System.Collections.Generic.List<AtlasRouteResult> alternatives;Vector3 alternativesOrigin;string alternativesTarget;int alternativeIndex;
        public string RouteChoiceInfo{get;private set;}="";
        public AtlasNavigation(AtlasMap map){planner=new AtlasNavMeshService(map);}
        public bool Begin(Vector3 from,string target,float now)
        {
            var result=planner.Plan(from,target);Target=target;
            alternatives=null;RouteChoiceInfo="";
            if(!result.valid){Stop();Instruction=result.reason=="START_BLOCKED"?"Lùi khỏi mép bàn, giữ máy trước người rồi bấm Đi tới lại":"Chưa có lối đi phù hợp · "+result.reason;return false;}
            Points=result.points;Remaining=total=result.length;progress=0;nextPlan=now+1.5f;Active=true;revealAt=now;observed=false;
            Instruction=total<.01f?"Đã tới gần "+target+" · Kiểm tra bàn đích":"Đi tới "+target;return true;
        }
        public bool ChooseAlternative(Vector3 from,string target,float now)
        {
            var floor=from;floor.y=0;
            if(alternatives==null||alternativesTarget!=target||Vector3.Distance(floor,alternativesOrigin)>.5f)
            {alternatives=planner.Alternatives(from,target);alternativesOrigin=floor;alternativesTarget=target;alternativeIndex=0;}
            if(alternatives.Count<2){RouteChoiceInfo="Chỉ có một lối đủ khoảng trống tại vị trí này";return false;}
            alternativeIndex=(alternativeIndex+1)%alternatives.Count;var result=alternatives[alternativeIndex];
            Target=target;Points=result.points;Remaining=total=result.length;progress=0;nextPlan=now+1.5f;Active=true;revealAt=now;observed=false;
            RouteChoiceInfo="Lối "+(alternativeIndex+1)+"/"+alternatives.Count+" · "+result.length.ToString("F1")+" m";Instruction=RouteChoiceInfo+" tới "+target;return true;
        }
        public void Stop(){Active=false;Points=Array.Empty<Vector3>();Remaining=0;Hide();}
        public void Bind(Transform mapRoot)
        {
            Hide();foreach(var arrow in arrows)if(arrow!=null)UnityEngine.Object.Destroy(arrow.gameObject);root=mapRoot;
            if(root==null)return;
            if(arrowMesh==null)
            {
                arrowMesh=new Mesh{name="Navigation chevron"};
                arrowMesh.vertices=new[]{new Vector3(-.20f,0,-.22f),new Vector3(0,0,-.05f),new Vector3(.20f,0,-.22f),new Vector3(.20f,0,-.02f),new Vector3(0,0,.18f),new Vector3(-.20f,0,-.02f)};
                arrowVertices=arrowMesh.vertices;
                // Both sides visible when camera tilts; geometry stays at the measured floor position.
                arrowMesh.triangles=new[]{0,1,5,1,4,5,1,2,3,1,3,4,5,1,0,5,4,1,3,2,1,4,3,1};arrowMesh.RecalculateNormals();
            }
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Route chevron "+(i+1),typeof(MeshFilter),typeof(MeshRenderer));arrows[i]=go.transform;arrows[i].SetParent(root,false);
                go.GetComponent<MeshFilter>().sharedMesh=arrowMesh;renderers[i]=go.GetComponent<MeshRenderer>();renderers[i].sharedMaterial=Resources.Load<Material>("Atlas/RouteMaterial");go.SetActive(false);opacity[i]=0;
            }
        }
        public bool ExternalVisualization;
        public float Progress=>progress;
        public void Tick(bool valid,Vector3 position,Vector3 forward,Camera camera,bool cameraMode,float now)
        {
            if(!Active){Hide();return;}
            if(!valid){Instruction="Tạm dừng chỉ đường · Đang khôi phục vị trí";CameraCue="Giữ máy và nhìn quanh chậm";observed=false;Hide();return;}
            if(Points.Length==0){Stop();Instruction="Chưa có tuyến hợp lệ · Chọn lại điểm đến";return;}
            if(Points.Length<2)
            {
                var floorPosition=position;floorPosition.y=0;
                if(Vector3.Distance(floorPosition,Points[0])>.85f&&now>=nextPlan)
                {if(!Begin(position,Target,now)){Hide();return;}}
                else{Instruction="Đã tới gần "+Target+" · Kiểm tra bàn đích";Hide();return;}
                if(Points.Length<2){Hide();return;}
            }
            float travelled=observed?Vector3.Distance(position,lastPosition):0;
            float bestAlong=AtlasRouteProgress.Locate(Points,position,progress,Mathf.Max(.6f,travelled*1.6f+.15f),out float best);
            lastPosition=position;observed=true;
            if(best>.65f&&now>=nextPlan)
            {
                // Never project the live user dot onto a corridor to conceal a bad localization.
                if(!Begin(position,Target,now)){Hide();return;}bestAlong=0;
            }
            progress=Mathf.Clamp(bestAlong,0,total);Remaining=Mathf.Max(0,total-progress);
            var floor=position;floor.y=0;
            if(Remaining<.65f&&Vector3.Distance(floor,Points[Points.Length-1])<.85f)
            {
                Instruction="Đã tới gần "+Target+" · Kiểm tra bàn đích";Hide();return;
            }
            var aim=Sample(Mathf.Min(total,progress+1.15f));var d=aim-floor;d.y=0;
            string turn=AtlasRouteProgress.NextTurn(Points,progress,out float turnDistance);NextTurnMetres=turnDistance;
            float facing=d.sqrMagnitude<.001f?0:Vector3.SignedAngle(forward,d,Vector3.up);
            CameraCue=Mathf.Abs(facing)>135?"Quay người lại để theo tuyến":facing>45?"Xoay sang phải để thấy tuyến":facing<-45?"Xoay sang trái để thấy tuyến":"Theo mũi tên trên lối đi";
            // The headline must not say 'straight' while the camera faces a different aisle.
            string maneuver=Mathf.Abs(facing)>135?"Quay người lại":facing>45?"Xoay sang phải để theo tuyến":facing<-45?"Xoay sang trái để theo tuyến":turnDistance<1?turn:turnDistance<4?"Đi tiếp · "+turn.ToLowerInvariant()+" sau "+turnDistance.ToString("F1")+" m":"Đi thẳng";
            Instruction=maneuver+" · "+Remaining.ToString("F1")+" m tới "+Target;
            if(ExternalVisualization){Hide();return;}
            float dt=lastTick>0?Mathf.Min(.1f,now-lastTick):.033f;lastTick=now;int shown=0;
            for(int i=0;i<3;i++)
            {
                if(arrows[i]==null)continue;
                float at=progress+.85f+i*.65f;bool visible=cameraMode&&root!=null&&camera!=null&&at<total+.1f;
                if(visible)
                {
                    var p=Sample(Mathf.Min(at,total));var tangent=Sample(Mathf.Min(at+.2f,total))-Sample(Mathf.Max(0,at-.2f));
                    p.y=.07f;arrows[i].localPosition=p;if(tangent.sqrMagnitude>.0001f)arrows[i].localRotation=Quaternion.LookRotation(tangent,Vector3.up);
                    // Test every vertex, not just the centre: no half arrow outside the camera window.
                    foreach(var vertex in arrowVertices){var viewport=camera.WorldToViewportPoint(arrows[i].TransformPoint(vertex));if(viewport.z<.25f||viewport.x<GuidanceViewport.xMin+.025f||viewport.x>GuidanceViewport.xMax-.025f||viewport.y<GuidanceViewport.yMin+.02f||viewport.y>GuidanceViewport.yMax-.02f){visible=false;break;}}
                }
                float targetAlpha=visible?Mathf.SmoothStep(0,1,Mathf.Clamp01((now-revealAt-i*.16f)/.28f)):0;
                opacity[i]=Mathf.MoveTowards(opacity[i],targetAlpha,dt*5);arrows[i].gameObject.SetActive(opacity[i]>.01f);
                colour.SetColor("_Color",new Color(.10f,.45f,1,opacity[i]));renderers[i].SetPropertyBlock(colour);if(visible)shown++;
            }
            if(shown==0&&Mathf.Abs(facing)<=45)CameraCue="Hạ camera nhẹ để thấy mũi tên · Theo tuyến xanh trên map";
        }
        public static string Direction(Vector3 forward,Vector3 delta)
        {
            forward.y=delta.y=0;if(forward.sqrMagnitude<.001f||delta.sqrMagnitude<.001f)return "Giữ máy ổn định";
            float angle=Vector3.SignedAngle(forward,delta,Vector3.up);
            if(Mathf.Abs(angle)>135)return "Quay người lại";
            if(angle>28)return "Rẽ phải";if(angle<-28)return "Rẽ trái";return "Đi thẳng";
        }
        Vector3 Sample(float distance)
        {
            for(int i=0;i<Points.Length-1;i++){float d=Vector3.Distance(Points[i],Points[i+1]);if(distance<=d)return Vector3.Lerp(Points[i],Points[i+1],d>.001f?distance/d:0);distance-=d;}
            return Points[Points.Length-1];
        }
        void Hide(){foreach(var arrow in arrows)if(arrow!=null)arrow.gameObject.SetActive(false);}
        public void Dispose(){Hide();planner.Dispose();if(arrowMesh!=null)UnityEngine.Object.Destroy(arrowMesh);}
    }
}
