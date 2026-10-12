using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI
{
    public sealed class PrototypeOperationPresentation:MonoBehaviour
    {
        ImportedFigmaNavigation nav;TMP_FontAsset font;string kind,key;GameObject page;Button.ButtonClickedEvent submit;
        readonly Dictionary<int,string> drafts=new Dictionary<int,string>();readonly List<TMP_InputField> fields=new List<TMP_InputField>();
        public void Configure(ImportedFigmaNavigation owner,string operation)
        {
            nav=owner;kind=operation;font=GetComponentInChildren<TMP_Text>(true).font;
            string action=kind=="incident"?"Gửi báo cáo sự cố":kind=="label"?"In tem":kind=="inbound"?"Xác nhận nhập kho":"Xác nhận xuất kho";
            submit=GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.GetComponentsInChildren<TMP_Text>(true).Any(t=>ImportedAppLocalization.Source(t).Trim()==action))?.onClick;
            foreach(Transform child in transform)child.gameObject.SetActive(false);Build();
        }
        void Update(){if(nav!=null&&key!=nav.Settings.Dark+ImportedAppLocalization.IsEnglish.ToString()+ImportedSettings.TextScale)Build();}
        void Build()
        {
            bool dark=nav.Settings.Dark,en=ImportedAppLocalization.IsEnglish;key=dark+en.ToString()+ImportedSettings.TextScale;
            for(int i=0;i<fields.Count;i++)if(fields[i]!=null)drafts[i]=fields[i].text;fields.Clear();if(page!=null){page.SetActive(false);Destroy(page);}
            string title=kind=="incident"?(en?"Report issue":"Báo cáo sự cố"):kind=="label"?(en?"Create label":"Tạo tem hàng"):kind=="inbound"?(en?"Inbound":"Nhập kho"):(en?"Outbound":"Xuất kho");
            var root=PrototypeUi.Shell(transform,font,title,dark,nav.Back,()=>nav.Show("prototype:tools"),out var content);page=root.gameObject;
            string desc=kind=="incident"?"Ghi rõ vị trí và vấn đề để quản lý xử lý.":kind=="label"?"Xem trước mã và tem của sản phẩm.":kind=="inbound"?"Đối chiếu mã hàng, lô và số lượng đã nhận.":"Đối chiếu với nhiệm vụ được giao trước khi xác nhận.";
            PrototypeUi.Text(content,font,en?(kind=="incident"?"Describe the location and issue for your manager.":kind=="label"?"Preview product codes and labels.":kind=="inbound"?"Check products, lots and received quantities.":"Check against your assigned task before confirming."):desc,0,0,366,32,12,dark,false,true);
            string[] labels=kind=="incident"?(en?new[]{"Location","Issue description"}:new[]{"Vị trí","Mô tả sự cố"}):en?new[]{"Product code","Lot code","Actual quantity"}:new[]{"Mã hàng","Mã lô","Số lượng thực tế"};
            int count=kind=="incident"||kind=="label"?2:3;
            for(int i=0;i<count;i++)
            {
                float y=42+i*87;bool area=kind=="incident"&&i==1;PrototypeUi.Text(content,font,labels[i],0,y,366,21,12,dark,true);
                var box=PrototypeUi.Panel(content,labels[i],0,y+21,366,area?92:48,dark,14);
                var viewport=JarVisUi.Rect(box,"Text viewport",14,0,338,area?92:48);viewport.gameObject.AddComponent<RectMask2D>();
                var value=PrototypeUi.Text(viewport,font,"",0,0,338,area?92:48,14,dark);value.alignment=area?TextAlignmentOptions.TopLeft:TextAlignmentOptions.MidlineLeft;
                var input=box.gameObject.AddComponent<TMP_InputField>();input.textComponent=value;input.textViewport=viewport;input.targetGraphic=box.GetComponent<Image>();input.targetGraphic.raycastTarget=true;input.characterLimit=area?1000:120;input.lineType=area?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
                if(i==2)input.contentType=TMP_InputField.ContentType.DecimalNumber;input.text=drafts.TryGetValue(i,out var draft)?draft:"";fields.Add(input);box.gameObject.AddComponent<ImportedInputCaretRepair>().Configure(input);
            }
            float submitY=42+count*87+(kind=="incident"?44:0);PrototypeUi.Action(content,font,kind=="incident"?(en?"Submit issue":"Gửi báo cáo sự cố"):kind=="label"?(en?"Print label":"In tem"):kind=="inbound"?(en?"Confirm inbound":"Xác nhận nhập kho"):(en?"Confirm outbound":"Xác nhận xuất kho"),0,submitY,366,44,dark,()=>{if(submit!=null)submit.Invoke();else nav.Notice(en?"This operation is not connected yet; no data was changed.":"Nghiệp vụ chưa được kết nối; không có dữ liệu nào được thay đổi.");},true);
            content.sizeDelta=new Vector2(366,submitY+80);
        }
    }
}
