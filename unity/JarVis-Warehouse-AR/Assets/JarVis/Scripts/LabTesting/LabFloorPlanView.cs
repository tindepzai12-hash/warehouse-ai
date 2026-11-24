using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JarVis.Warehouse.Lab
{
    public sealed class LabFloorViewport : MonoBehaviour,IPointerDownHandler,IDragHandler,IPointerUpHandler
    {
        public LabTestSession Owner;
        public LabMapPlacement PlacementOverride;
        public bool Editable,Compact;
        public string Selected="A";
        public Action<LabPoint> Picked;
        public Action Opened;
        public float Zoom {get;private set;}=1;
        public LabMapPlacement Placement=>PlacementOverride??Owner?.Map?.placement;
        public LabMapFit Fit=>PlacementOverride==null||PlacementOverride==Owner?.Map?.placement?Owner.Registration:LabRegistration.Solve(Owner?.Map,Owner?.Floor,Placement);
        public LabFloorOverlay Overlay {get;private set;}
        public float Scale {get;private set;}
        RectTransform rect,imageRect;
        Texture2D texture;
        Vector2 pan,down,last;
        bool dragging;
        double nextRefresh;
        public Vector2 Rotate(Vector2 p){float a=(Owner?.Floor?.displayYaw??0)*Mathf.Deg2Rad;float x=p.x*Mathf.Cos(a)-p.y*Mathf.Sin(a);return new Vector2(Owner?.Floor?.displayMirrorX==true?-x:x,p.x*Mathf.Sin(a)+p.y*Mathf.Cos(a));}
        readonly Dictionary<string,Text> labels=new Dictionary<string,Text>();
        public void Initialize()
        {
            rect=GetComponent<RectTransform>();
            var hit=gameObject.AddComponent<Image>();hit.color=new Color(.94f,.96f,.98f);hit.raycastTarget=true;
            gameObject.AddComponent<RectMask2D>();
            var image=new GameObject("GLB floor plan",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();image.transform.SetParent(transform,false);image.raycastTarget=false;imageRect=image.rectTransform;
            imageRect.anchorMin=imageRect.anchorMax=imageRect.pivot=new Vector2(.5f,.5f);
            if(Owner?.Floor?.Valid==true)
            {
                var floor=Owner.Floor;texture=new Texture2D(floor.width,floor.height,TextureFormat.RGB24,false);texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
                var palette=new[]{new Color32(229,235,242,255),new Color32(255,255,255,255),new Color32(103,119,141,255)};
                texture.SetPixels32(floor.cells.Select(c=>palette[Math.Max(0,Math.Min(2,c))]).ToArray());texture.Apply();image.texture=texture;
            }
            Overlay=new GameObject("Map markers and phone",typeof(RectTransform),typeof(CanvasRenderer),typeof(LabFloorOverlay)).GetComponent<LabFloorOverlay>();Overlay.transform.SetParent(transform,false);Overlay.View=this;Overlay.raycastTarget=false;
            var overlayRect=Overlay.rectTransform;overlayRect.anchorMin=Vector2.zero;overlayRect.anchorMax=Vector2.one;overlayRect.offsetMin=overlayRect.offsetMax=Vector2.zero;
            Refresh();
        }
        public Vector2 Project(LabPoint p)
        {
            var f=Owner.Floor;return Rotate(new Vector2((p.x-f.originX-f.width*f.resolution*.5f)*Scale,(p.z-f.originZ-f.height*f.resolution*.5f)*Scale))+pan;
        }
        public LabPoint Unproject(Vector2 p)
        {
            var f=Owner.Floor;var q=Rotate(Vector2.right);var r=Rotate(Vector2.up);var v=p-pan;v=new Vector2(Vector2.Dot(v,q),Vector2.Dot(v,r));return new LabPoint(v.x/Scale+f.originX+f.width*f.resolution*.5f,0,v.y/Scale+f.originZ+f.height*f.resolution*.5f);
        }
        public void ChangeZoom(float value){Zoom=Mathf.Clamp(value,1,6);Refresh();}
        public void ResetView(){Zoom=1;pan=Vector2.zero;Refresh();}
        void Update(){if(Time.realtimeSinceStartupAsDouble<nextRefresh)return;nextRefresh=Time.realtimeSinceStartupAsDouble+1.0/15;Refresh();}
        public void Refresh()
        {
            if(rect==null||Owner?.Floor?.Valid!=true)return;
            var f=Owner.Floor;float width=f.width*f.resolution,height=f.height*f.resolution;
            var w=Rotate(new Vector2(width,0));var h=Rotate(new Vector2(0,height));float boxWidth=Mathf.Abs(w.x)+Mathf.Abs(h.x),boxHeight=Mathf.Abs(w.y)+Mathf.Abs(h.y);
            Scale=Mathf.Min((rect.rect.width-12)/boxWidth,(rect.rect.height-12)/boxHeight)*Zoom;
            if(Scale<=0)return;
            pan.x=Mathf.Clamp(pan.x,-Mathf.Max(0,(boxWidth*Scale-rect.rect.width)*.5f),Mathf.Max(0,(boxWidth*Scale-rect.rect.width)*.5f));
            pan.y=Mathf.Clamp(pan.y,-Mathf.Max(0,(boxHeight*Scale-rect.rect.height)*.5f),Mathf.Max(0,(boxHeight*Scale-rect.rect.height)*.5f));
            imageRect.localScale=new Vector3(f.displayMirrorX?-1:1,1,1);imageRect.localEulerAngles=new Vector3(0,0,f.displayMirrorX?-f.displayYaw:f.displayYaw);imageRect.sizeDelta=new Vector2(width*Scale,height*Scale);imageRect.anchoredPosition=pan;Overlay.SetVerticesDirty();
            if(!Compact&&f.labels!=null)foreach(var label in f.labels)Label(label.id,new LabPoint(label.x,0,label.z),new Color(.15f,.22f,.33f),9);
            if(Placement?.pins!=null)foreach(var pin in Placement.pins)Label(pin.id,pin.position,new Color(.02f,.1f,.3f),Compact?11:16);
        }
        void Label(string id,LabPoint p,Color colour,int size)
        {
            if(!labels.TryGetValue(id,out var text))
            {
                text=new GameObject("Map label "+id,typeof(RectTransform),typeof(Text)).GetComponent<Text>();text.transform.SetParent(Overlay.transform,false);text.font=Resources.Load<Font>("BeVietnamPro-Bold");text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
                text.rectTransform.anchorMin=text.rectTransform.anchorMax=text.rectTransform.pivot=new Vector2(.5f,.5f);text.rectTransform.sizeDelta=new Vector2(44,24);labels.Add(id,text);
            }
            text.gameObject.SetActive(true);text.text=id;text.color=colour;text.fontSize=size;text.rectTransform.anchoredPosition=Project(p)+Vector2.up*(Compact?12:18);
        }
        Vector2 Local(PointerEventData e){RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,e.position,e.pressEventCamera,out var p);return p;}
        public void OnPointerDown(PointerEventData e){down=last=Local(e);dragging=false;}
        public void OnDrag(PointerEventData e){var p=Local(e);if((p-down).magnitude>8)dragging=true;if(!Compact){pan+=p-last;Refresh();}last=p;}
        public void OnPointerUp(PointerEventData e)
        {
            if(dragging)return;if(Compact){Opened?.Invoke();return;}
            if(Editable&&Scale>0){var p=Unproject(Local(e));if(Owner.Floor.Contains(p))Picked?.Invoke(p);}
        }
        void OnDestroy(){if(texture!=null)Destroy(texture);}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LabFloorOverlay : MaskableGraphic
    {
        public LabFloorViewport View;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(View?.Owner?.Floor?.Valid!=true||View.Scale<=0)return;
            var owner=View.Owner;var fit=View.Fit;
            if(fit.valid)
            {
                foreach(var leg in owner.Map.legs.Where(l=>l.valid))for(int i=1;i<leg.points.Length;i++)Line(vh,View.Project(fit.Transform(leg.points[i-1])),View.Project(fit.Transform(leg.points[i])),View.Compact?2:3,new Color(.6f,.68f,.8f));
                if(!View.Compact)foreach(var choice in owner.RouteChoices)if(choice.id!=owner.RouteId)
                    for(int j=1;j<choice.points.Length;j++)Line(vh,View.Project(fit.Transform(choice.points[j-1])),View.Project(fit.Transform(choice.points[j])),3,new Color(.7f,.76f,.83f));
                for(int i=1;i<owner.Route.Count;i++)Line(vh,View.Project(fit.Transform(owner.Route[i-1])),View.Project(fit.Transform(owner.Route[i])),View.Compact?3:5,new Color(.03f,.25f,.95f));
            }
            if(View.Placement?.pins!=null)foreach(var pin in View.Placement.pins)
            {
                var p=View.Project(pin.position);Dot(vh,p,View.Compact?5:8,pin.id==owner.Exercise?.Target?new Color(1,.5f,.05f):new Color(.15f,.35f,.68f));
                if(View.Editable&&pin.id==View.Selected)Dot(vh,p,3,Color.white);
            }
            var phone=owner.DisplayPosition??owner.LastKnownPosition;
            if(fit.valid&&!View.Editable&&phone!=null)
            {
                var p=View.Project(fit.Transform(phone));Dot(vh,p,View.Compact?8:11,Color.white);Dot(vh,p,View.Compact?5:8,owner.NavigationPoseValid?new Color(.02f,.25f,.98f):Color.gray);
                float yaw=(owner.Heading+fit.yaw)*Mathf.Deg2Rad;var forward=View.Rotate(new Vector2(Mathf.Sin(yaw),Mathf.Cos(yaw)));if(owner.NavigationPoseValid)Line(vh,p,p+forward*(View.Compact?16:26),4,new Color(.02f,.25f,.98f));
            }
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color colour)
        {
            if((b-a).sqrMagnitude<.001f)return;Vector2 n=Vector2.Perpendicular((b-a).normalized)*width*.5f;int i=vh.currentVertCount;foreach(var p in new[]{a-n,a+n,b+n,b-n})vh.AddVert(p,colour,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        static void Dot(VertexHelper vh,Vector2 p,float radius,Color colour)
        {
            int first=vh.currentVertCount;vh.AddVert(p,colour,Vector2.zero);for(int i=0;i<=16;i++){float a=i*Mathf.PI/8;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,colour,Vector2.zero);}for(int i=0;i<16;i++)vh.AddTriangle(first,first+1+i,first+2+i);
        }
    }
    public sealed class LabMapPanel : MonoBehaviour
    {
        LabTestSession owner;LabFloorViewport map;LabMapPlacement draft;Text feedback;bool editing;string selected="A";string notice;readonly Dictionary<string,Button> selectors=new Dictionary<string,Button>();
        public void Initialize(LabTestSession session,bool edit)
        {
            owner=session;editing=edit;
            var root=GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;gameObject.AddComponent<Image>().color=new Color(.98f,.99f,1);
            draft=owner.Map?.placement!=null?JsonUtility.FromJson<LabMapPlacement>(JsonUtility.ToJson(owner.Map.placement)):new LabMapPlacement{mapId=owner.Floor?.id,revision=owner.Floor?.revision??0,geometrySha256=owner.Floor?.geometrySha256};
            Text("Title",editing?"Nhập vị trí A–E · bản thử":"Vị trí trên bản đồ lab",20,16,16,380,32);
            Text("Instructions",editing?"Chọn mốc đã ghi → chạm vị trí ước lượng.\nLưu nháp được, chưa cần đủ 5 mốc hay quay vòng.":"Quét một mốc đã gắn để căn vị trí điện thoại.\nChấm xanh là bạn; A–E là các thẻ cố định.",13,16,57,380,50);
            for(int i=0;i<(editing?5:0);i++)
            {
                string id=((char)('A'+i)).ToString();var button=Button(id,16+i*76,116,68,44,()=>{selected=id;notice=null;});button.interactable=editing&&owner.Map?.markers.Any(m=>m.id==id)==true;selectors[id]=button;
            }
            if(editing)
            {
                Button("Tọa độ / chiều cao",16,170,230,42,OpenEntry);
                Button("Kiểm tra để dùng",254,170,142,42,()=>{if(owner.PublishFixedPlacement(draft))Destroy(gameObject);else notice=owner.Message;});
            }
            var viewport=new GameObject("Floor plan interaction",typeof(RectTransform)).GetComponent<RectTransform>();viewport.SetParent(transform,false);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(16,owner.NewScan&&!editing&&(owner.Located||owner.CanPreviewRecover)?320:216);viewport.offsetMax=new Vector2(-16,editing?-222:owner.NewScan?-164:-174);
            map=viewport.gameObject.AddComponent<LabFloorViewport>();map.Owner=owner;map.Editable=editing;map.PlacementOverride=draft;map.Picked=Pick;map.Initialize();
            Button("−",16,-160,48,44,()=>map.ChangeZoom(map.Zoom/1.5f),true);Button("Vừa màn",72,-160,112,44,map.ResetView,true);Button("+",192,-160,48,44,()=>map.ChangeZoom(map.Zoom*1.5f),true);
            feedback=Text("Registration feedback","",12,16,-106,380,52,true);
            Text("Legend","Trắng: lối đi · xám: bàn / tường · xanh: bạn",11,16,-72,380,26,true);
            if(owner.NewScan&&!editing)
            {
                if(!owner.Located&&owner.CanPreviewRecover)foreach(var id in new[]{"A","B","C"}){string at=id;var pick=Button("Đang ở "+id,16+(id[0]-'A')*128,-212,120,44,()=>{owner.BeginPreviewRecoveryAt(at);Destroy(gameObject);},true);pick.interactable=owner.Tracking;}
                if(owner.Located)for(int i=0;i<5;i++){string target=((char)('A'+i)).ToString();var pick=Button("Tới "+target,16+i*76,-212,68,44,()=>{owner.ChangeDestination(target);Destroy(gameObject);},true);pick.interactable=owner.CanChooseFixed(target);}
                if(owner.Located){Button("Lối này bị chặn",16,-264,182,44,()=>{owner.BlockCurrentAisle();Destroy(gameObject);},true);Button("Bỏ chặn lối",210,-264,186,44,()=>{owner.ClearBlockedAisles();Destroy(gameObject);},true);}
                for(int i=0;i<owner.RouteChoices.Count;i++){int selectedRoute=i;var c=owner.RouteChoices[i];Button("Lối "+(i+1)+" · "+c.length.ToString("F1")+" m",16+i*128,116,120,44,()=>{owner.ChooseRoute(selectedRoute);notice="Đã chọn lối "+(selectedRoute+1);});}
                Button("Quét mốc gần tôi",248,-160,148,44,()=>{if(owner.CanPreviewRecover)owner.OpenPreviewRecovery();else{owner.RetryCalibration();Destroy(gameObject);}},true);
                Button("Quay lại",16,-16,182,48,()=>Destroy(gameObject),true);Button("Căn chỉnh mũi tên",210,-16,186,48,()=>{owner.OpenVisualSettings();gameObject.SetActive(false);Destroy(gameObject);},false);
            }
            else
            {
                Button(editing?"Hủy / quay lại":"Quay lại",16,-16,182,48,()=>Destroy(gameObject),true);
                Button(editing?"Lưu nháp vị trí":"Đóng bản đồ",210,-16,186,48,()=>{if(!editing||(owner.NewScan?owner.SavePositionEntryDraft(draft):owner.SetPlacement(draft)))Destroy(gameObject);else notice=owner.Message;},true);
            }
        }
        public void Pick(LabPoint point)
        {
            if(!editing||owner.Map?.markers.Any(m=>m.id==selected)!=true){notice="Ghi mốc "+selected+" bằng camera trước.";return;}
            var pin=draft.pins.FirstOrDefault(p=>p.id==selected);
            if(LabPositionEntry.TrySet(draft,owner.Floor,selected,point.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture),point.z.ToString("R",System.Globalization.CultureInfo.InvariantCulture),pin?.heightEntered==true?(pin.position.y*100).ToString("F2",System.Globalization.CultureInfo.InvariantCulture):"",pin?.expectedErrorMetres>0?(pin.expectedErrorMetres*100).ToString("F0",System.Globalization.CultureInfo.InvariantCulture):"50",out string reason))notice="Đã đặt "+selected+" ước lượng · lưu nháp để giữ. ";else notice=reason;map.Refresh();
        }
        void OpenEntry()
        {
            if(owner.Map?.markers.Any(m=>m.id==selected)!=true){notice="Quét và ghi mốc "+selected+" trước; không cần đủ 5 mốc.";return;}
            var panel=new GameObject("Approximate position entry",typeof(RectTransform),typeof(LabPositionEntryPanel)).GetComponent<LabPositionEntryPanel>();panel.transform.SetParent(transform,false);
            panel.Initialize(draft,owner.Floor,selected,()=>{notice="Đã nhập "+selected+" · bấm Lưu nháp vị trí để giữ.";map.Refresh();});
        }
        void Update()
        {
            if(map==null)return;map.Selected=selected;
            foreach(var pair in selectors){var colours=pair.Value.colors;colours.normalColor=pair.Key==selected?new Color(.58f,.74f,1):Color.white;pair.Value.colors=colours;}
            var fit=LabRegistration.Solve(owner.Map,owner.Floor,draft);
            if(owner.NewScan&&!editing){feedback.text=notice??(!owner.Located&&owner.CanPreviewRecover?"Đứng đúng điểm A/B/C rồi chọn để căn lại.\nVị trí do bạn xác nhận, chưa được tự xác minh.":"Lối xám: thay thế · xanh: tuyến đang theo.\n"+(owner.PreviewMode?"Căn nhanh ước lượng · ":"")+owner.PoseReason);return;}
            var selectedPin=draft.pins.FirstOrDefault(p=>p.id==selected);
            feedback.text=notice??(editing&&selectedPin?.position!=null?"Mốc "+selected+" · X "+selectedPin.position.x.ToString("F2")+" · Z "+selectedPin.position.z.ToString("F2")+" m\n"+(selectedPin.entrySource==LabPositionEntry.Source?"Vị trí map đã nhập · bấm Lưu nháp vị trí.":"Pin tham chiếu · chạm đúng vị trí thẻ để sửa."):editing?"Chọn mốc đã ghi rồi chạm vị trí trên map.":owner.HasData?fit.reason:"Chưa ghi mốc bằng camera. Quay lại và quét một mốc A–E bất kỳ.");
        }
        Text Text(string name,string value,int size,float x,float y,float width,float height,bool bottom=false)
        {
            var text=new GameObject(name,typeof(RectTransform),typeof(Text)).GetComponent<Text>();text.transform.SetParent(transform,false);text.font=Resources.Load<Font>(size>=18?"BeVietnamPro-Bold":"BeVietnamPro-Regular");text.fontSize=size;text.text=value;text.color=new Color(.04f,.09f,.2f);text.raycastTarget=false;
            var r=text.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,bottom?0:1);r.pivot=new Vector2(0,bottom?0:1);r.anchoredPosition=new Vector2(x,bottom?-y:-y);r.sizeDelta=new Vector2(width,height);return text;
        }
        Button Button(string value,float x,float y,float width,float height,Action action,bool bottom=false)
        {
            var r=new GameObject(value,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();r.SetParent(transform,false);r.anchorMin=r.anchorMax=new Vector2(0,bottom?0:1);r.pivot=new Vector2(0,bottom?0:1);r.anchoredPosition=new Vector2(x,bottom?-y:-y);r.sizeDelta=new Vector2(width,height);
            var image=r.GetComponent<Image>();image.color=new Color(.87f,.93f,1);var button=r.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());
            var label=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(r,false);label.text=value;label.font=Resources.Load<Font>("BeVietnamPro-Bold");label.fontSize=13;label.alignment=TextAnchor.MiddleCenter;label.color=new Color(.02f,.12f,.37f);label.raycastTarget=false;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;return button;
        }
    }
}
