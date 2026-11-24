using System;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.VisualMaps;

namespace JarVis.Warehouse.Lab
{
    // Presentation only. No route/pose/map writes are permitted here.
    public sealed class LabGuideVisual:MonoBehaviour
    {
        LabRouteCueRig rig;
        readonly LabCueChoreography motion=new LabCueChoreography();
        readonly LabCueToken[] boardTokens={new LabCueToken(0,0,0,0,0,1),new LabCueToken(1,0,0,0,0,1),new LabCueToken(2,0,0,0,0,1)};
        double visibleSince=-1;LabCueMode requestedMode;
        Transform pin;TextMesh pinText,pinName;MeshRenderer nameRenderer;
        Mesh pinMesh,namePlateMesh;Vector3[] pinVertices;Material blue,plateMaterial;Transform namePlate;
        public bool CueVisible {get;private set;}
        public int VisibleCount=>CueVisible&&rig!=null?rig.VisibleCount:0;
        public string Reason {get;private set;}="NOT_GUIDING";
        public Rect PinBounds {get;private set;}
        public bool PinVisible=>pin!=null&&pin.gameObject.activeSelf;
        public LabRouteCueRig Rig=>rig;
        public void Initialize(Material source)
        {
            rig=new GameObject("Three shared route slots").AddComponent<LabRouteCueRig>();rig.transform.SetParent(transform,false);rig.Initialize(source);
            blue=new Material(source);blue.SetColor("_BaseColor",new Color(.04f,.23f,.86f));
            pin=new GameObject("Destination pin").transform;pin.SetParent(transform,false);
            pinMesh=new Mesh{name="Lab destination silhouette"};
            pinVertices=new[]{new Vector3(-.34f,.07f,0),new Vector3(-.27f,.46f,0),new Vector3(0,.60f,0),new Vector3(.27f,.46f,0),new Vector3(.34f,.07f,0),new Vector3(0,-.45f,0)};
            pinMesh.vertices=pinVertices;pinMesh.triangles=new[]{0,1,2,0,2,5,2,3,4,2,4,5,2,1,0,5,2,0,4,3,2,5,4,2};pinMesh.RecalculateNormals();pinMesh.RecalculateBounds();
            var body=new GameObject("Blue pin",typeof(MeshFilter),typeof(MeshRenderer));body.transform.SetParent(pin,false);
            body.GetComponent<MeshFilter>().sharedMesh=pinMesh;body.GetComponent<MeshRenderer>().sharedMaterial=blue;
            pinText=new GameObject("Destination ID",typeof(TextMesh)).GetComponent<TextMesh>();pinText.transform.SetParent(pin,false);
            pinText.anchor=TextAnchor.MiddleCenter;pinText.fontSize=64;pinText.characterSize=.07f;pinText.color=Color.white;pinText.transform.localPosition=new Vector3(0,.23f,-.015f);
            pinName=new GameObject("Destination name",typeof(TextMesh)).GetComponent<TextMesh>();pinName.transform.SetParent(pin,false);
            pinName.anchor=TextAnchor.MiddleCenter;pinName.fontSize=64;pinName.characterSize=.08f;pinName.color=new Color(.05f,.15f,.4f);pinName.transform.localPosition=new Vector3(0,.9f,-.015f);
            nameRenderer=pinName.GetComponent<MeshRenderer>();
            plateMaterial=new Material(source);plateMaterial.SetColor("_BaseColor",new Color(.97f,.985f,1));
            namePlateMesh=new Mesh{name="Destination name backing"};namePlateMesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};namePlateMesh.triangles=new[]{0,1,2,0,2,3,2,1,0,3,2,0};namePlateMesh.RecalculateBounds();
            namePlate=new GameObject("Destination name backing",typeof(MeshFilter),typeof(MeshRenderer)).transform;namePlate.SetParent(pin,false);namePlate.GetComponent<MeshFilter>().sharedMesh=namePlateMesh;namePlate.GetComponent<MeshRenderer>().sharedMaterial=plateMaterial;
            pin.gameObject.SetActive(false);
        }
        void Context(LabNavigationDecision decision,LabCueMode mode,double now)
        {
            if(requestedMode!=mode){visibleSince=-1;CueVisible=false;requestedMode=mode;}
            motion.Context(decision.referenceEpoch,decision.routeRevision,decision.maneuverId,mode,now);
        }
        bool Fits(Rect bounds,Rect safe,Func<Rect,bool> allowed,bool readable=true)
        {
            return bounds.xMin>=safe.xMin&&bounds.xMax<=safe.xMax&&bounds.yMin>=safe.yMin&&bounds.yMax<=safe.yMax&&
                (!readable||bounds.width*412>=24&&bounds.width<=safe.width*.35f)&&
                (allowed==null||allowed(bounds))&&(!PinVisible||!bounds.Overlaps(PinBounds));
        }
        bool Commit(bool fits,double now)
        {
            if(fits){if(visibleSince<0)visibleSince=now;}else visibleSince=-1;
            bool result=fits&&(CueVisible||!Application.isPlaying||now-visibleSince>=.18);
            CueVisible=result;if(!result)rig.Hide();return result;
        }
        public bool ShowPath(Camera camera,Transform frame,LabNavigationDecision decision,LabCueToken[] tokens,int count,float height,
            Rect safe,Func<Rect,bool> allowed,double now,bool reduced)
        {
            if(camera==null||frame==null||decision==null||!decision.VisualMatches){HideCue();Reason="STALE_VISUAL_ROUTE";return false;}
            Context(decision,LabCueMode.Path,now);motion.Sync(tokens,count,now);rig.PlacePath(frame,tokens,count,height);
            // Enter centrally; retain an already readable cluster a little longer at the edge.
            float inset=CueVisible?.08f:.14f;
            var central=new Rect(safe.xMin+safe.width*inset,safe.yMin,safe.width*(1-2*inset),safe.height);
            bool allFit=count>0;
            var minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<count;i++)
            {
                bool fits=rig.ProjectedBounds(camera,i,out var bounds)&&Fits(bounds,safe,allowed);
                allFit&=fits;minimum=Vector2.Min(minimum,bounds.min);maximum=Vector2.Max(maximum,bounds.max);
                rig.Paint(i,motion.Alpha(tokens[i].cell,now,reduced),LabCueChoreography.Light(now,i,reduced));
            }
            allFit&=minimum.x>=central.xMin&&maximum.x<=central.xMax;
            // The HUD presents the complete instruction when any selected glyph would be clipped.
            bool result=Commit(allFit,now);Reason=result?"WORLD_PATH":count==0?"HUD_PATH_NO_FOOTPRINT":"HUD_PATH_CLUSTER_BOUNDS";
            return result;
        }
        public bool ShowBoard(Camera camera,Vector3 position,float bearing,LabNavigationDecision decision,Rect safe,Func<Rect,bool> allowed,double now,bool reduced)
        {
            if(camera==null||decision==null){HideCue();return false;}
            Context(decision,LabCueMode.TurnBoard,now);motion.Sync(boardTokens,3,now);rig.PlaceBoard(camera,position,bearing);
            bool fits=true;var minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            for(int i=0;i<3;i++)
            {
                fits&=rig.ProjectedBounds(camera,i,out var bounds);minimum=Vector2.Min(minimum,bounds.min);maximum=Vector2.Max(maximum,bounds.max);
                rig.Paint(i,motion.Alpha(i,now,reduced),LabCueChoreography.Light(now,i,reduced));
            }
            fits&=Fits(Rect.MinMaxRect(minimum.x,minimum.y,maximum.x,maximum.y),safe,allowed,false);
            float inset=CueVisible?.08f:.14f;
            fits&=minimum.x>=safe.xMin+safe.width*inset&&maximum.x<=safe.xMax-safe.width*inset;
            bool result=Commit(fits,now);Reason=result?"WORLD_TURN_BOARD":"HUD_TURN_BOUNDS";return result;
        }
        // Compatibility entry for the focused projection check; runtime uses the decision-bearing methods.
        public bool Show(Camera camera,Vector3 position,float bearing,bool turning,Rect safe,Func<Vector2,bool> allowed=null,Func<Rect,bool> allowedBounds=null)
        {
            var d=new LabNavigationDecision(0,0,0,0,true,false,"B","",null,null,1,1,bearing,LabTurnPhase.AtTurn,"fixture");
            bool result=ShowBoard(camera,position,bearing,d,safe,allowedBounds,Time.realtimeSinceStartupAsDouble,true);
            if(result&&allowed!=null)for(int i=0;i<3;i++)if(rig.ProjectedBounds(camera,i,out var b)&&
                (!allowed(b.min)||!allowed(b.max))){HideCue();return false;}
            return result;
        }
        public void HideCue(){rig?.Hide();motion.Invalidate();CueVisible=false;visibleSince=-1;requestedMode=LabCueMode.None;}
        public void ShowDestination(Camera camera,Vector3 position,string id,string description,Func<Rect,bool> allowedBounds=null)
        {
            if(pin==null||camera==null){return;}
            if(pinText.text!=id)pinText.text=id;if(pinName.text!=description)pinName.text=description;
            pin.gameObject.SetActive(true);pin.position=position+Vector3.up*.25f;
            pin.rotation=Quaternion.LookRotation(pin.position-camera.transform.position,Vector3.up);
            var nameBounds=nameRenderer.localBounds;namePlate.localPosition=pinName.transform.localPosition+nameBounds.center+Vector3.forward*.03f;namePlate.localScale=new Vector3(nameBounds.size.x+.20f,nameBounds.size.y+.12f,1);
            pin.localScale=Vector3.one*Mathf.Clamp(Vector3.Distance(pin.position,camera.transform.position)*.055f,.12f,.65f);
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);bool visible=true;
            foreach(var vertex in pinVertices)Include(camera,pin.TransformPoint(vertex),ref min,ref max,ref visible);
            var b=namePlate.GetComponent<MeshRenderer>().bounds;
            for(int i=0;i<8;i++)Include(camera,new Vector3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z),ref min,ref max,ref visible);
            var bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            visible&=min.x>=.03f&&max.x<=.97f&&min.y>=.05f&&max.y<=.95f&&(allowedBounds==null||allowedBounds(bounds));
            pin.gameObject.SetActive(visible);PinBounds=visible?bounds:Rect.zero;
        }
        static void Include(Camera camera,Vector3 world,ref Vector2 min,ref Vector2 max,ref bool visible)
        {
            var p=camera.WorldToViewportPoint(world);if(p.z<=camera.nearClipPlane+.03f)visible=false;
            min=Vector2.Min(min,new Vector2(p.x,p.y));max=Vector2.Max(max,new Vector2(p.x,p.y));
        }
        public void HideDestination(){if(pin!=null)pin.gameObject.SetActive(false);PinBounds=Rect.zero;}
        public void Hide(){HideCue();HideDestination();Reason="NOT_GUIDING";}
        void OnDestroy(){Release(blue);Release(pinMesh);Release(plateMaterial);Release(namePlateMesh);}
        static void Release(UnityEngine.Object o){if(o==null)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabScreenDirection:MaskableGraphic
    {
        bool coaching;int side=1;float drawnAngle;readonly float[] alpha={1,1,1};
        public void SetMotion(float a,float b,float c)
        {if(Mathf.Abs(alpha[0]-a)+Mathf.Abs(alpha[1]-b)+Mathf.Abs(alpha[2]-c)<.01f)return;alpha[0]=a;alpha[1]=b;alpha[2]=c;SetVerticesDirty();}
        public void SetState(bool look,int direction,float angle)
        {if(coaching!=look||side!=direction){coaching=look;side=direction;SetVerticesDirty();}if(Mathf.Abs(Mathf.DeltaAngle(drawnAngle,angle))>.5f){drawnAngle=angle;rectTransform.localEulerAngles=new Vector3(0,0,look?0:-angle);}}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float unit=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)/(coaching?1.4f:1.94f);
            if(coaching)
            {
                // Phone silhouette with a distinct orbit arrow: never mistaken for a travel turn.
                var blue=new Color(.065f,.22f,.68f);Quad(vh,new Vector2(-.24f,-.39f),new Vector2(.24f,.39f),blue,unit);Quad(vh,new Vector2(-.19f,-.31f),new Vector2(.19f,.31f),Color.white,unit);
                for(int k=0;k<12;k++){float a=(155-k*10)*Mathf.Deg2Rad,b=(145-k*10)*Mathf.Deg2Rad;Line(vh,new Vector2(side*Mathf.Cos(a)*.55f,Mathf.Sin(a)*.55f+.02f),new Vector2(side*Mathf.Cos(b)*.55f,Mathf.Sin(b)*.55f+.02f),.065f,blue,unit);}
                var tip=new Vector2(side*.50f,.25f);var d=new Vector2(side*.42f,-.91f);var v=new Vector2(-d.y,d.x)*.13f;
                int n=vh.currentVertCount;vh.AddVert((tip+d*.16f)*unit,blue,Vector2.zero);vh.AddVert((tip-d*.08f+v)*unit,blue,Vector2.zero);vh.AddVert((tip-d*.08f-v)*unit,blue,Vector2.zero);vh.AddTriangle(n,n+1,n+2);return;
            }
            for(int k=-1;k<=1;k++){Shape(vh,true,new Color(.065f,.22f,.68f,alpha[k+1]),k,unit);Shape(vh,false,new Color(1,1,1,alpha[k+1]),k,unit);}
        }
        static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Color c,float scale)
        {int n=vh.currentVertCount;vh.AddVert(a*scale,c,Vector2.zero);vh.AddVert(new Vector2(a.x,b.y)*scale,c,Vector2.zero);vh.AddVert(b*scale,c,Vector2.zero);vh.AddVert(new Vector2(b.x,a.y)*scale,c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c,float scale)
        {var d=(b-a).normalized;var v=new Vector2(-d.y,d.x)*width*.5f;int n=vh.currentVertCount;vh.AddVert((a-v)*scale,c,Vector2.zero);vh.AddVert((a+v)*scale,c,Vector2.zero);vh.AddVert((b+v)*scale,c,Vector2.zero);vh.AddVert((b-v)*scale,c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        static void Shape(VertexHelper vh,bool outline,Color tint,int k,float unit)
        {int n=vh.currentVertCount;foreach(var p in outline?LabChevronShape.Outline:LabChevronShape.Points)vh.AddVert((p+Vector2.up*(k*.64f))*unit,tint,Vector2.zero);var t=LabChevronShape.Triangles;for(int i=0;i<t.Length;i+=3)vh.AddTriangle(n+t[i],n+t[i+1],n+t[i+2]);}
    }
}
