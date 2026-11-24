using System;
using JarVis.Warehouse.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Projection metadata is exported from the same checked scene/NavMesh as the
    // packaged map image. A schematic rectangle does not represent surveyed walls.
    [Serializable] public sealed class RoompresentMapProjection
    {
        public int mapId=153613;
        public string registrationSha256;
        public Vector2 center;
        public float scale;
        public Vector3 a,b,c;
        public int width=384,height=512;
        public Vector2 axisX=new Vector2(1,0),axisY=new Vector2(0,1);
        public Vector2 Project(Vector3 local)=>new Vector2(Vector2.Dot(new Vector2(local.x,local.z)-center,axisX)*scale+width*.5f,Vector2.Dot(new Vector2(local.x,local.z)-center,axisY)*scale+height*.5f);
        public static RoompresentMapProjection Load()
        {
            var text=Resources.Load<TextAsset>("RoompresentMapProjection");
            if(text==null)return null;
            try{var data=JsonUtility.FromJson<RoompresentMapProjection>(text.text);return data!=null&&data.mapId==153613&&float.IsFinite(data.scale)&&data.scale>0?data:null;}
            catch(Exception){return null;}
        }
    }

    public static class RoompresentMapPanel
    {
        public static RectTransform Create(Transform parent,TMP_FontAsset font,bool dark,bool english)
        {
            var card=PrototypeUi.Panel(parent,"Roompresent 2D map",0,0,366,488,dark,22);
            PrototypeUi.Text(card,font,english?"2D map":"Bản đồ 2D",18,14,330,30,18,dark,true);
            PrototypeUi.Text(card,font,english?"A1 → B  ·  A2 → C":"A1 → B  ·  A2 → C",18,48,330,24,13,dark,false,true);
            var raw=JarVisUi.Rect(card,"Saved room map",18,80,330,330).gameObject.AddComponent<RawImage>();raw.raycastTarget=false;
            var location=PrototypeUi.Text(card,font,"",18,422,330,34,13,dark,true);
            var overlay=raw.gameObject.AddComponent<RoompresentMapPositionOverlay>();overlay.Configure(raw,location,dark,english,null);
            PrototypeUi.Text(card,font,english?"A: start · B: left cart · C: right cart":"A: xuất phát · B: kệ trái · C: kệ phải",18,466,330,22,11,dark,false,true);
            return card;
        }
    }

    public sealed class RoompresentMapPositionOverlay:MonoBehaviour
    {
        RawImage image;
        TMP_Text location;
        RectTransform dot,halo;
        RoompresentMapProjection projection;
        SmallRoomAR guide;
        Texture2D ownedTexture;
        Vector3 lastLocal;
        bool hadFix,english;
        float nextLookup;
        public string PositionState {get;private set;}
        public Vector3 LocalPosition {get;private set;}
        public void Configure(RawImage target,TMP_Text caption,bool dark,bool en,SmallRoomAR source)
        {
            image=target;location=caption;english=en;guide=source;projection=RoompresentMapProjection.Load();
            if(image.texture==null){ownedTexture=RoompresentMap2D.LoadReference();image.texture=ownedTexture;}
            halo=JarVisUi.Rect(image.transform,"Position highlight",0,0,26,26);
            halo.gameObject.AddComponent<RoompresentPositionGlyph>().SetRing(true);
            dot=JarVisUi.Rect(image.transform,"You are here",0,0,12,12);
            dot.gameObject.AddComponent<RoompresentPositionGlyph>().SetRing(false);
            foreach(var rect in new[]{halo,dot}){rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(.5f,.5f);rect.GetComponent<Graphic>().raycastTarget=false;}
            RefreshPosition();
        }
        void Update(){RefreshPosition();}
        public void RefreshPosition()
        {
            if(image==null||projection==null)
            {
                if(location!=null)location.text=english?"Position unavailable · map reference only":"Chưa có vị trí · chỉ hiển thị sơ đồ";
                if(dot!=null)dot.gameObject.SetActive(false);if(halo!=null)halo.gameObject.SetActive(false);return;
            }
            if(guide==null&&Time.unscaledTime>=nextLookup){nextLookup=Time.unscaledTime+2;guide=FindAnyObjectByType<SmallRoomAR>();}
            bool live=guide!=null&&guide.presentationRoom&&guide.DeliveryTracking&&guide.mapRoot!=null&&guide.localization?.cameraManager!=null;
            if(live)
            {
                var measured=guide.mapRoot.InverseTransformPoint(guide.localization.cameraManager.transform.position);
                if(RoompresentPoseGate.Finite(measured)){lastLocal=measured;hadFix=true;}else live=false;
            }
            LocalPosition=hadFix?lastLocal:projection.a;
            PositionState=live?"LIVE":hadFix?"LAST_KNOWN":"DEFAULT_A";
            var point=projection.Project(LocalPosition);bool onMap=point.x>=16&&point.x<=projection.width-16&&point.y>=16&&point.y<=projection.height-16;
            dot.gameObject.SetActive(onMap&&hadFix);halo.gameObject.SetActive(onMap);
            if(onMap)
            {
                var size=image.rectTransform.rect.size;
                dot.anchoredPosition=halo.anchoredPosition=new Vector2(point.x/projection.width*size.x,point.y/projection.height*size.y);
                halo.localScale=Vector3.one*(hadFix?.85f:1)*(live&&!PrototypeUi.ReducedMotion?1+.08f*Mathf.Sin(Time.unscaledTime*2.5f):1);
                dot.GetComponent<Graphic>().color=live?Color.white:new Color(1,1,1,.7f);
            }
            location.text=live?(english?"Your position · AR tracking":"Vị trí của bạn · Đang tracking AR"):
                hadFix?(english?"Last known position · awaiting tracking":"Vị trí cuối đã biết · Chờ nối lại tracking"):
                english?"You · A (default)\nScan QR A for actual positioning":"Bạn · A (vị trí mặc định)\nQuét QR A để định vị thực tế";
            if(!onMap)location.text+=english?"\nOutside the saved map":"\nNgoài phạm vi sơ đồ";
        }
        void OnDestroy(){if(ownedTexture!=null)Destroy(ownedTexture);}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoompresentPositionGlyph:MaskableGraphic
    {
        bool ring;
        public void SetRing(bool value){ring=value;raycastTarget=false;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;var center=r.center;float radius=Mathf.Min(r.width,r.height)*.5f;
            var ink=new Color32(36,99,235,(byte)(ring?180:255));
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;
                Vector2 u=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int n=vh.currentVertCount;
                if(ring)
                {
                    vh.AddVert(center+u*radius,ink,Vector2.zero);vh.AddVert(center+v*radius,ink,Vector2.zero);
                    vh.AddVert(center+v*(radius-1.6f),ink,Vector2.zero);vh.AddVert(center+u*(radius-1.6f),ink,Vector2.zero);
                    vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
                }
                else
                {
                    vh.AddVert(center,ink,Vector2.zero);vh.AddVert(center+u*(radius-1),ink,Vector2.zero);vh.AddVert(center+v*(radius-1),ink,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
                }
            }
        }
    }
}
