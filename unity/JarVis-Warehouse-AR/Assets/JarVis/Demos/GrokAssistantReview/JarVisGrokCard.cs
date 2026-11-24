using System;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Preview
{
    // Presentation only. Commands go back to existing task/media owners.
    // This component never owns a camera, AR session, anchor, mission or network client.
    public sealed class JarVisGrokCard : MonoBehaviour
    {
        public TMP_FontAsset Font;
        public Sprite Logo, Expression;
        public Texture2D Map;
        public Action<string> CommandRequested, MessageRequested, IncidentRequested;
        public string Context="A1 → B · A2 → C";
        public string Mode {get;private set;}="ready";
        public bool ReducedMotion;
        public RectTransform Card {get;private set;}
        public Vector2 EyePosition=>leftEye==null?Vector2.zero:leftEye.anchoredPosition;
        public float WavePeak=>wave==null?0:wave.Peak;
        public float[] AudioLevels;
        public CanvasGroup Group {get;private set;}
        TMP_Text state;
        TMP_InputField input,incident;
        RectTransform leftEye,rightEye;
        JarVisGrokWave wave;
        float openedAt,phase;
        Vector2 eyeOffset;
        string question="Mình đang ở B, tới C thế nào?";
        string answer="A1 tại B đã xong. Chọn Tới C và đi theo mũi tên để giao A2.";
        static Color C(string hex)=>PrototypeUi.Hex(hex);
        void Start(){if(Card==null)Show("ready");}
        public void ShowReply(string user,string reply){question=user;answer=reply;Show("speaking");}
        public void Show(string mode)
        {
            Mode=mode;openedAt=Time.unscaledTime;phase=0;
            if(Card!=null){Card.gameObject.SetActive(false);if(Application.isPlaying)Destroy(Card.gameObject);else DestroyImmediate(Card.gameObject);}
            float h=mode=="chat"?390:mode=="report"?338:mode=="map"?486:mode=="speaking"?276:mode=="thinking"?224:148;
            Card=Rect(transform,"JarVis Grok card",0,0,378,h);
            Card.anchorMin=Card.anchorMax=Card.pivot=new Vector2(.5f,1);Card.anchoredPosition=new Vector2(0,-76);
            Group=Card.gameObject.AddComponent<CanvasGroup>();
            Panel(Card,"Soft shadow",0,5,378,h,"0000000B",25);
            Panel(Card,"Fine shadow",0,2,378,h,"0000000A",25);
            Panel(Card,"Outline",-1,-1,380,h+2,"DCE3EB",25);
            Panel(Card,"White surface",0,0,378,h,"FFFFFF",24).GetComponent<Graphic>().raycastTarget=true;
            BuildToolbar();
            state=Text(Card,mode=="listening"?"Mình đang nghe bạn":mode=="thinking"?"JarVis đang suy nghĩ":mode=="speaking"?"JarVis đang trả lời":mode=="chat"?"Trò chuyện với JarVis":mode=="report"?"Báo cáo sự cố":mode=="map"?"Sơ đồ Roompresent":"JarVis ở đây để hỗ trợ",18,88,342,26,16,true);
            Text(Card,Context,18,117,342,19,11,false,"6D7B8E");
            if(mode=="thinking"||mode=="speaking")
            {
                Bubble(question,true,150);
                if(mode=="speaking")Bubble(answer,false,205);
                else for(int i=0;i<3;i++)Panel(Card,"Thinking dot "+i,20+i*9,207,4,4,"99A7B9",2);
            }
            if(mode=="chat")BuildChat();
            if(mode=="report")BuildReport();
            if(mode=="map")
            {
                var image=Rect(Card,"Expanded reference map",80,144,218,290).gameObject.AddComponent<RawImage>();image.texture=Map;image.raycastTarget=false;
                var b=Text(Card,"Sơ đồ tham khảo · không phải vị trí trực tiếp",18,440,342,27,10,false,"6D7B8E");b.alignment=TextAlignmentOptions.Center;
            }
            ApplyMotion(Time.unscaledTime,0,true);
        }
        void BuildToolbar()
        {
            var logo=Rect(Card,"Original JarVis art",14,14,52,52).gameObject.AddComponent<Image>();
            logo.sprite=Expression;logo.preserveAspect=true;logo.raycastTarget=false;
            leftEye=Panel(logo.transform,"Left eye",18.9f,26.8f,4.7f,7.1f,"EFFFFF",2.35f);
            rightEye=Panel(logo.transform,"Right eye",26,26.8f,4.7f,7.1f,"EFFFFF",2.35f);
            wave=Rect(Card,"Voice amplitude",76,22,68,34).gameObject.AddComponent<JarVisGrokWave>();wave.color=C("238BA4");
            Control("report","alert-triangle","Sự cố",154,()=>Command("report"));
            Control("chat","message-square","Chat",206,()=>Command("chat"));
            Control("voice","mic","Nói",258,()=>Command("voice"));
            Control("close","x","Đóng",310,()=>Command("close"),true);
        }
        void Command(string command)
        {
            CommandRequested?.Invoke(command);
            if(command=="close"){Card.gameObject.SetActive(false);Mode="closed";return;}
            Show(command=="voice"?"listening":command);
        }
        void Control(string name,string symbol,string label,float x,Action action,bool red=false)
        {
            var hit=Rect(Card,"Action "+name,x,10,48,64);
            var bg=Panel(hit,"Circle",3,3,42,42,red?"EA334B":"F3F5F7",21);
            var button=hit.gameObject.AddComponent<Button>();button.targetGraphic=bg.GetComponent<Graphic>();button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>action());
            // A transparent full hit rect keeps the 48 x 64 target, including its label.
            var hitImage=hit.gameObject.AddComponent<Image>();hitImage.color=new Color(1,1,1,.001f);hitImage.raycastTarget=true;
            var icon=Icon(hit,symbol,14,14,20);icon.color=red?Color.white:C("34455B");
            var t=Text(hit,label,0,48,48,16,9,false,red?"BA2840":"6D7B8E");t.alignment=TextAlignmentOptions.Center;
            hit.gameObject.AddComponent<JarVisGrokPress>();
        }
        void BuildChat()
        {
            var viewport=Rect(Card,"Conversation viewport",18,148,342,165);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport,"Conversation content",0,0,342,390);
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var q=Panel(content,"User message",80,0,262,42,"1255D5",15);Text(q,"Gửi lại bản đồ 2D cho mình.",12,8,238,26,12,false,"FFFFFF");
            var reply=Panel(content,"JarVis message",0,52,300,54,"F1F5F8",15);Text(reply,"Đây là sơ đồ A, B và C đã lưu.\nChạm ảnh để xem lớn.",12,8,276,38,12);
            var map=Panel(content,"Map attachment",0,116,144,194,"112236",14);
            var image=Rect(map,"Reference map",4,4,136,181).gameObject.AddComponent<RawImage>();image.texture=Map;image.raycastTarget=false;
            var mapButton=map.gameObject.AddComponent<Button>();mapButton.targetGraphic=map.GetComponent<Graphic>();map.GetComponent<Graphic>().raycastTarget=true;mapButton.onClick.AddListener(()=>Show("map"));
            Text(content,"Sơ đồ tham khảo, không phải định vị trực tiếp.",0,318,332,34,10,false,"6D7B8E");
            var field=Panel(Card,"Message field",18,329,230,44,"F2F5F8",18);input=Input(field,"Nhắn JarVis…",206,28);
            var send=Panel(Card,"Send",306,327,54,48,"1255D5",20);var sb=send.gameObject.AddComponent<Button>();sb.targetGraphic=send.GetComponent<Graphic>();send.GetComponent<Graphic>().raycastTarget=true;
            var sendIcon=Icon(send,"arrow-right",16,13,22);sendIcon.color=Color.white;
            sb.onClick.AddListener(()=>{string value=input.text.Trim();if(value.Length>0){MessageRequested?.Invoke(value);input.text="";}});
            var mic=Panel(Card,"Chat voice",254,327,46,48,"F2F5F8",20);var mb=mic.gameObject.AddComponent<Button>();mb.targetGraphic=mic.GetComponent<Graphic>();mic.GetComponent<Graphic>().raycastTarget=true;mb.onClick.AddListener(()=>Command("voice"));
            Icon(mic,"mic",13,13,20).color=C("34455B");
            scroll.verticalNormalizedPosition=1;
        }
        TMP_InputField Input(Transform parent,string placeholder,float w,float h)
        {
            var view=Rect(parent,"Text viewport",12,8,w,h);view.gameObject.AddComponent<RectMask2D>();
            var label=Text(view,"",0,0,w,h,12);var hint=Text(view,placeholder,0,0,w,h,12,false,"7E8A9B");
            var input=parent.gameObject.AddComponent<TMP_InputField>();input.textViewport=view;input.textComponent=(TextMeshProUGUI)label;input.placeholder=hint;input.targetGraphic=parent.GetComponent<Graphic>();input.characterLimit=800;
            parent.GetComponent<Graphic>().raycastTarget=true;return input;
        }
        void BuildReport()
        {
            var f=Panel(Card,"Incident field",18,148,342,96,"F2F5F8",15);incident=Input(f,"Mô tả sự cố bạn gặp…",318,80);incident.lineType=TMP_InputField.LineType.MultiLineNewline;
            Text(Card,"Kèm ngữ cảnh nhiệm vụ hiện tại.",18,249,342,22,11,false,"6D7B8E");
            var action=Panel(Card,"Submit incident",18,277,342,48,"1255D5",15);var b=action.gameObject.AddComponent<Button>();b.targetGraphic=action.GetComponent<Graphic>();action.GetComponent<Graphic>().raycastTarget=true;
            var t=Text(action,"Gửi mô tả sự cố",12,0,318,48,12,true,"FFFFFF");t.alignment=TextAlignmentOptions.Center;
            b.onClick.AddListener(()=>{string value=incident.text.Trim();if(value.Length>0)IncidentRequested?.Invoke(value);});
        }
        void Bubble(string value,bool user,float y)
        {
            float w=user?254:342;float x=user?106:18;float h=user?44:54;
            var b=Panel(Card,user?"Question bubble":"Reply bubble",x,y,w,h,user?"1255D5":"F1F5F8",15);
            Text(b,value,12,7,w-24,h-14,12,false,user?"FFFFFF":"26384F");
        }
        static RectTransform Rect(Transform p,string name,float x,float y,float w,float h)=>JarVisUi.Rect(p,name,x,y,w,h);
        static Graphic Icon(Transform p,string symbol,float x,float y,float size)
        {
            if(symbol!="mic")return JarVisUi.Icon(p,symbol,x,y,size);
            var mic=Rect(p,"Microphone vector",x,y,size,size).gameObject.AddComponent<ChatActionIcon>();mic.Set(ChatActionIcon.Symbol.Microphone);mic.raycastTarget=false;return mic;
        }
        static RectTransform Panel(Transform p,string name,float x,float y,float w,float h,string color,float radius)
        {
            var r=Rect(p,name,x,y,w,h);var surface=r.gameObject.AddComponent<JarVisGrokSurface>();surface.color=C(color);surface.Radius=radius;surface.raycastTarget=false;return r;
        }
        TMP_Text Text(Transform p,string value,float x,float y,float w,float h,float size,bool bold=false,string color="26384F")
        {
            var text=JarVisUi.Text(p,Font,value,x,y,w,h,size);text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.color=C(color);text.margin=Vector4.zero;text.alignment=TextAlignmentOptions.MidlineLeft;text.raycastTarget=false;text.richText=false;return text;
        }
        void Update(){ApplyMotion(Time.unscaledTime,Time.unscaledDeltaTime);}
        public void ApplyMotion(float time,float dt,bool settle=false)
        {
            if(Card==null||!Card.gameObject.activeSelf)return;
            bool reduced=ReducedMotion||PrototypeUi.ReducedMotion;
            float t=reduced||settle?1:Mathf.Clamp01((time-openedAt)/.28f),ease=1-Mathf.Pow(1-t,3);
            Group.alpha=ease;Card.anchoredPosition=new Vector2(0,-76-(1-ease)*10);
            wave.Levels=AudioLevels;wave.Sample(dt,settle);
            bool speaking=Mode=="speaking";wave.color=C(speaking?"E87920":"238BA4");
            var target=wave.Peak<.01f||reduced?Vector2.zero:new Vector2(1+2*wave.Peak,(wave.Centroid-.5f)*2.6f*wave.Peak);
            eyeOffset=settle?target:Vector2.Lerp(eyeOffset,target,1-Mathf.Exp(-Mathf.Max(0,dt)*10));
            float blink=reduced?1:time%5.8f>5.68f?.18f:1;
            leftEye.anchoredPosition=new Vector2(18.9f,-26.8f)+eyeOffset;rightEye.anchoredPosition=new Vector2(26,-26.8f)+eyeOffset;leftEye.localScale=rightEye.localScale=new Vector3(1,blink,1);
            if(Mode=="thinking")for(int i=0;i<3;i++)
            {
                var dot=Card.Find("Thinking dot "+i).GetComponent<Graphic>();var c=dot.color;c.a=reduced?1:.35f+.65f*(.5f+.5f*Mathf.Sin(time*5-i*.8f));dot.color=c;
            }
            phase=time;
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JarVisGrokSurface:MaskableGraphic
    {
        public float Radius=20;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(Radius,Mathf.Min(r.width,r.height)/2);vh.AddVert(r.center,color,Vector2.zero);
            for(int c=0;c<4;c++)for(int i=0;i<=12;i++)
            {
                float a=(c*90+i*90f/12)*Mathf.Deg2Rad;
                var centre=new Vector2(c==0||c==3?r.xMax-radius:r.xMin+radius,c<2?r.yMax-radius:r.yMin+radius);
                vh.AddVert(centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);
            }
            for(int i=1;i<vh.currentVertCount;i++)vh.AddTriangle(0,i,i==vh.currentVertCount-1?1:i+1);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class JarVisGrokWave:MaskableGraphic
    {
        public float[] Levels;readonly float[] smoothed=new float[20];public float Peak{get;private set;}public float Centroid{get;private set;}
        public void Sample(float dt,bool settle=false)
        {
            float peak=0,sum=0,weight=0;
            for(int i=0;i<smoothed.Length;i++)
            {
                float level=Levels==null||Levels.Length==0?0:Mathf.Clamp01(Levels[i*(Levels.Length-1)/(smoothed.Length-1)]);
                smoothed[i]=settle?level:Mathf.Lerp(smoothed[i],level,1-Mathf.Exp(-Mathf.Max(0,dt)*14));
                peak=Mathf.Max(peak,smoothed[i]);sum+=smoothed[i];weight+=smoothed[i]*i/(smoothed.Length-1);
            }
            Peak=peak;Centroid=sum<.01f?.5f:weight/sum;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            for(int i=0;i<smoothed.Length;i++)
            {
                float w=2.2f,h=2.2f+smoothed[i]*(r.height-3),x=r.xMin+(i+.5f)*r.width/smoothed.Length;int n=vh.currentVertCount;var c=color;c.a*=.38f+.62f*smoothed[i];
                vh.AddVert(new Vector3(x-w/2,r.center.y-h/2),c,Vector2.zero);vh.AddVert(new Vector3(x-w/2,r.center.y+h/2),c,Vector2.zero);vh.AddVert(new Vector3(x+w/2,r.center.y+h/2),c,Vector2.zero);vh.AddVert(new Vector3(x+w/2,r.center.y-h/2),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
    public sealed class JarVisGrokPress:MonoBehaviour,UnityEngine.EventSystems.IPointerDownHandler,UnityEngine.EventSystems.IPointerUpHandler,UnityEngine.EventSystems.IPointerExitHandler
    {
        float target=1;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e){target=.96f;}
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e){target=1;}
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e){target=1;}
        void Update(){transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*(PrototypeUi.ReducedMotion?1:target),1-Mathf.Exp(-Time.unscaledDeltaTime*22));}
    }
}
