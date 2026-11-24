using System;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Preview
{
    // Isolated presentation candidate. No scene change, AR restart or spatial write here.
    public sealed class ArDeliveryFinishPreview : MonoBehaviour
    {
        public RoompresentDeliveryRecord Record;
        public Action ReturnRequested, ExitRequested;
        public bool Returning { get; private set; }
        public bool CompletionVisible => completion != null && completion.gameObject.activeSelf;
        public string Selected { get; private set; } = "B";
        public readonly RoompresentDeliveryVerification Proof = new RoompresentDeliveryVerification();
        public string Feedback { get; private set; }
        public float Remaining = 5.5f;
        TMP_FontAsset regular, bold;
        RectTransform root, dock, rows, completion, scan;
        TMP_Text heading, secondary, scanTitle, scanText;
        public ArSavedStartPin StartPin { get; private set; }
        RoompresentCodeFrame frame;
        readonly Button[] routes = new Button[2], confirms = new Button[2];
        Button backA;
        bool acknowledged;
        static Color C(string value) => PrototypeUi.Hex(value);
        public void Configure(RoompresentDeliveryRecord record, TMP_FontAsset font, TMP_FontAsset heavy)
        { Record = record; regular = font; bold = heavy; Build(); Refresh(); }
        RectTransform Panel(Transform p,string name,float x,float y,float w,float h,string fill="172A40",string border="36516C",float radius=18)
        { var r=JarVisUi.Rect(p,name,x,y,w,h);var image=r.gameObject.AddComponent<Image>();image.color=Color.white;image.raycastTarget=false;UniformUiSurface.Use(image,radius,C(fill),C(border),fill==border?0:1,false,false);return r; }
        TMP_Text Text(Transform p,string value,float x,float y,float w,float h,float size,bool heavy=false,string color="F4F7FC",bool center=false)
        { var t=JarVisUi.Text(p,heavy?bold:regular,value,x,y,w,h,size);t.fontSize=size;t.fontStyle=FontStyles.Normal;t.characterSpacing=0;t.color=C(color);t.alignment=center?TextAlignmentOptions.Center:TextAlignmentOptions.MidlineLeft;t.overflowMode=TextOverflowModes.Overflow;return t; }
        Button Button(Transform p,string name,string label,float x,float y,float w,float h,Action action,string fill="1255D5",string color="FFFFFF",string icon=null,float size=13)
        {
            var r=Panel(p,name,x,y,w,h,fill,fill,12);var b=PrototypeUi.Button(r,action);var effect=b.GetComponent<ImportedPressFeedback>();if(effect!=null)effect.enabled=false;
            var t=Text(r,label,8,0,w-16,h,size,true,color,true);
            if(icon!=null)
            {
                float group=Mathf.Min(w-16,t.GetPreferredValues(label).x+26),left=(w-group)/2;
                var i=JarVisUi.Icon(r,icon,left,(h-20)/2,20);i.color=C(color);
                t.rectTransform.anchoredPosition=new Vector2(left+26,0);t.rectTransform.sizeDelta=new Vector2(group-26,h);
            }
            return b;
        }
        RectTransform Bottom(string name,float h)
        {
            var p=Panel(root,name,0,0,380,h);p.anchorMin=p.anchorMax=p.pivot=new Vector2(.5f,0);p.anchoredPosition=new Vector2(0,16);return p;
        }
        void Build()
        {
            root=(RectTransform)transform;
            var pin=JarVisUi.Rect(root,"Saved world A pin",0,0,34,48);StartPin=pin.gameObject.AddComponent<ArSavedStartPin>();
            Button(root,"Exit AR preview","Thoát",16,12,58,46,()=>ExitRequested?.Invoke(),"C52B43");
            Button(root,"Realign preview","Quét A",82,12,106,46,()=>Feedback="Quét lại A trong cùng phiên AR.","263C54",icon:"refresh-arrows");
            Button(root,"Refresh preview","Làm mới camera",196,12,200,46,()=>Feedback="Bản demo không có luồng camera thật.","263C54",icon:"refresh-arrows");
            var jarvis=Button(root,"JarVis preview","JarVis",16,70,112,44,()=>Feedback="Cửa sổ JarVis tiếp tục dùng cùng phiên AR.","172A40",size:14);
            var logo=JarVisUi.Rect(jarvis.transform,"JarVis mascot",9,8,28,28).gameObject.AddComponent<Image>();logo.sprite=Resources.Load<Sprite>("Prototype/JarVis");logo.preserveAspect=true;logo.raycastTarget=false;
            var jt=jarvis.GetComponentInChildren<TMP_Text>();jt.rectTransform.anchoredPosition=new Vector2(40,0);jt.rectTransform.sizeDelta=new Vector2(64,44);
            dock=Bottom("Compact direction dock",208);
            heading=Text(dock,"",16,10,258,34,21,true);
            secondary=Text(dock,"",16,44,348,26,12,false,"BED0E3");
            // Visually smaller than the route heading, still a 48-high touch target.
            backA=Button(dock,"Return A compact","Về A",286,9,78,48,GoA,"2D4560",size:12);
            rows=JarVisUi.Rect(dock,"Pending delivery rows",12,78,356,118);
            for(int n=0;n<2;n++)
            {
                string stop=n==0?"B":"C",book=n==0?"A1":"A2";float y=n*60;
                routes[n]=Button(rows,"Go "+stop,"Tới "+stop,0,y,78,50,()=>Choose(stop),"29435F");
                confirms[n]=Button(rows,"Verify "+stop,"Xác nhận giao "+book+" tại "+stop,86,y,270,50,()=>Begin(stop),size:13);
            }
            scan=Panel(root,"Ordered QR and barcode overlay",0,0,352,424,"FFFFFF","D7E2F0",20);
            scan.anchorMin=scan.anchorMax=scan.pivot=new Vector2(.5f,.5f);scan.anchoredPosition=new Vector2(0,5);
            scan.GetComponent<Image>().raycastTarget=true;
            scanTitle=Text(scan,"",18,12,266,34,18,true,"152640");
            Button(scan,"Cancel proof","",296,8,44,44,()=>{Proof.Cancel();Refresh();},"FFF0F2","C52B43","x");
            var well=Panel(scan,"Scan area",49,62,254,254,"233A50","233A50",12);
            var fr=JarVisUi.Rect(well,"QR versus barcode frame",9,9,236,236);frame=fr.gameObject.AddComponent<RoompresentCodeFrame>();frame.SetBarcode(false);
            scanText=Text(scan,"",16,330,320,55,14,true,"152640",true);
            completion=Panel(root,"Task completion popup",0,0,348,306,"FFFFFF","D7E2F0",22);
            completion.anchorMin=completion.anchorMax=completion.pivot=new Vector2(.5f,.5f);completion.anchoredPosition=new Vector2(0,8);completion.GetComponent<Image>().raycastTarget=true;
            Button(completion,"Close completion","",296,10,42,44,CloseCompletion,"FFF0F2","C52B43","x");
            var circle=Panel(completion,"Success icon",143,21,62,62,"00965F","00965F",31);var check=JarVisUi.Icon(circle,"check",16,16,30);check.color=Color.white;
            Text(completion,"ĐÃ HOÀN THÀNH\nNHIỆM VỤ",16,94,316,70,23,true,"152640",true);
            Text(completion,"#00001 · CẤT HÀNG",16,173,316,26,12,true,"5A6D83",true);
            Button(completion,"Return A from completion","Về A",20,226,308,50,GoA,icon:"arrow-left",size:16);
            Text(root,"DEMO UNITY · ẢNH NỀN THAM KHẢO",126,76,270,30,9,false,"F0F6FF",true);
        }
        void Choose(string stop)
        { if(Record.Complete||stop=="B"&&Record.deliveredB||stop=="C"&&Record.deliveredC)return;Selected=stop;Returning=false;Proof.Cancel();Refresh(); }
        public void Begin(string stop)
        { if(Proof.Begin(Record,stop)){Selected=stop;Feedback=null;Refresh();} }
        // Only called by the explicit Editor demo harness; these booleans do not certify native tracking.
        public DeliveryScanOutcome ObserveDemo(string code,bool measuredTracking,float measuredDistance)
        {
            string at=Proof.Stop;
            var outcome=Proof.Observe(Record,code,"RearCamera",measuredTracking,measuredDistance);
            Feedback=outcome==DeliveryScanOutcome.WrongDestination?"Chưa đúng QR điểm giao.":outcome==DeliveryScanOutcome.WrongProduct?"Chưa đúng barcode sách.":outcome==DeliveryScanOutcome.Unavailable?"Chưa có vị trí tin cậy để xác nhận.":null;
            if(outcome==DeliveryScanOutcome.Completed){Selected=Record.Complete?null:at=="B"?"C":"B";}
            Refresh();return outcome;
        }
        public void CloseCompletion(){acknowledged=true;Refresh();}
        public void GoA()
        {
            Proof.Cancel();acknowledged=Record.Complete;Returning=true;Selected="A";
            ReturnRequested?.Invoke();Refresh();
        }
        public void Refresh()
        {
            if(dock==null)return;
            bool done=Record.Complete;
            rows.gameObject.SetActive(!done);dock.sizeDelta=new Vector2(380,done?86:208);
            heading.text=Returning?"Về bàn A"+(float.IsFinite(Remaining)?" · "+Remaining.ToString("F1")+" m":""):done?"Đã hoàn thành":"Tới "+Selected+" · 5,4 m";
            heading.fontSize=Returning?21:done?20:21;
            secondary.text=Returning?"Đi theo mũi tên để về A.":done?"Chọn Về A để quay lại bàn xuất phát.":"A1 → B · A2 → C";
            for(int n=0;n<2;n++)
            {
                bool delivered=n==0?Record.deliveredB:Record.deliveredC;routes[n].interactable=confirms[n].interactable=!delivered&&!Proof.Active;
                foreach(var b in new[]{routes[n],confirms[n]})
                {var cg=b.GetComponent<CanvasGroup>();if(cg==null)cg=b.gameObject.AddComponent<CanvasGroup>();cg.alpha=delivered?.48f:1;}
            }
            scan.gameObject.SetActive(Proof.Active);
            if(Proof.Active)
            {
                bool barcode=Proof.WaitingForProduct;scanTitle.text="Xác thực tại "+Proof.Stop;
                frame.SetBarcode(barcode);frame.rectTransform.anchoredPosition=barcode?new Vector2(-23,-61):new Vector2(9,-9);
                scanText.text=Feedback??(barcode?"Bước 2/2 · Quét barcode "+Proof.ProductName:"Bước 1/2 · Quét QR "+Proof.Stop);
            }
            completion.gameObject.SetActive(done&&!acknowledged&&!Returning);
            backA.interactable=!Proof.Active;
            if(CompletionVisible)completion.SetAsLastSibling();
        }
    }
}
