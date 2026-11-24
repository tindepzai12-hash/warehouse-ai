using System;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Lab
{
    // Scrollable form keeps the optional numeric entry usable with the Android keyboard open.
    public sealed class LabPositionEntryPanel : MonoBehaviour
    {
        InputField x,z,height,tolerance;Text status;LabMapPlacement placement;LabFloorPlan floor;string id;Action applied;
        Font regular,bold;RectTransform content;float y;
        public void Initialize(LabMapPlacement draft,LabFloorPlan plan,string marker,Action onApplied)
        {
            placement=draft;floor=plan;id=marker;applied=onApplied;
            regular=Resources.Load<Font>("BeVietnamPro-Regular");bold=Resources.Load<Font>("BeVietnamPro-Bold");
            var root=GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            gameObject.AddComponent<Image>().color=new Color(.98f,.99f,1);
            Label(transform,"Nhập vị trí thử · "+id,20,16,18,380,34,true);
            Label(transform,"Có thể ước lượng; chưa thay tọa độ chỉ đường.",12,16,56,380,38);
            var viewport=new GameObject("Entry scroll",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect)).GetComponent<RectTransform>();viewport.SetParent(transform,false);
            viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(16,82);viewport.offsetMax=new Vector2(-16,-100);viewport.GetComponent<Image>().color=Color.white;
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            content=new GameObject("Entry content",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;scroll.content=content;
            var pin=placement.pins.FirstOrDefault(p=>p.id==id);y=0;
            Help("Chạm map là cách dễ nhất. X/Z dưới đây dùng hệ tọa độ map hiện có, tính bằng mét; không phải GPS.",60);
            x=Field("X · mét",pin?.position?.x.ToString("F3",CultureInfo.InvariantCulture)??"", "Ví dụ −5,82");
            z=Field("Z · mét",pin?.position?.z.ToString("F3",CultureInfo.InvariantCulture)??"", "Ví dụ 3,42");
            height=Field("Cao tâm toàn thẻ từ sàn · cm · tùy chọn",pin?.heightEntered==true?(pin.position.y*100).ToString("F1",CultureInfo.InvariantCulture):"","Chưa biết thì để trống");
            tolerance=Field("Sai số vị trí dự kiến · cm",pin?.expectedErrorMetres>0?(pin.expectedErrorMetres*100).ToString("F0",CultureInfo.InvariantCulture):"50","50");
            Help("Sai số là mức bạn cho phép khi nhập, không phải độ chính xác đã đo. Thẻ có thể đặt trên bàn hoặc tường; tâm thẻ khác chỗ đứng gần đích.",78);
            Help("QR: 10 × 10 cm. Giữ nguyên khung hoa văn. Không dùng chiều rộng tờ A4 thay cho chiều rộng ảnh thẻ.",62);
            status=Label(content,"",12,8,y,356,64);y+=70;content.sizeDelta=new Vector2(0,y);
            Button("Hủy",16,16,118,50,()=>Destroy(gameObject));
            Button("Áp dụng vào bản nháp",144,16,252,50,Apply);
        }
        void Apply()
        {
            if(!LabPositionEntry.TrySet(placement,floor,id,x.text,z.text,height.text,tolerance.text,out string reason)){status.text=reason;return;}
            applied?.Invoke();Destroy(gameObject);
        }
        InputField Field(string title,string value,string hint)
        {
            Label(content,title,12,8,y,356,24,true);y+=26;
            var box=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(InputField)).GetComponent<RectTransform>();box.SetParent(content,false);Top(box,8,y,356,46);y+=58;
            box.GetComponent<Image>().color=new Color(.9f,.94f,1);var field=box.GetComponent<InputField>();field.targetGraphic=box.GetComponent<Image>();
            var text=Label(box,"",17,10,0,336,46);text.alignment=TextAnchor.MiddleLeft;text.supportRichText=false;
            var placeholder=Label(box,hint,13,10,0,336,46);placeholder.alignment=TextAnchor.MiddleLeft;placeholder.color=new Color(.34f,.42f,.54f);
            field.textComponent=text;field.placeholder=placeholder;field.contentType=InputField.ContentType.Standard;field.keyboardType=TouchScreenKeyboardType.NumbersAndPunctuation;field.lineType=InputField.LineType.SingleLine;field.characterLimit=14;field.text=value;return field;
        }
        void Help(string value,float h){Label(content,value,12,8,y,356,h);y+=h+8;}
        Text Label(Transform parent,string value,int size,float left,float top,float width,float h,bool heavy=false)
        {
            var text=new GameObject(value.Length>35?value.Substring(0,35):value,typeof(RectTransform),typeof(Text)).GetComponent<Text>();text.transform.SetParent(parent,false);Top(text.rectTransform,left,top,width,h);text.font=heavy?bold:regular;text.fontSize=size;text.text=value;text.color=new Color(.04f,.09f,.2f);text.raycastTarget=false;return text;
        }
        static void Top(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        void Button(string title,float x,float bottom,float width,float h,Action action)
        {
            var r=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();r.SetParent(transform,false);r.anchorMin=r.anchorMax=Vector2.zero;r.pivot=Vector2.zero;r.anchoredPosition=new Vector2(x,bottom);r.sizeDelta=new Vector2(width,h);r.GetComponent<Image>().color=new Color(.84f,.91f,1);
            r.GetComponent<Button>().onClick.AddListener(()=>action());var text=Label(r,title,13,6,0,width-12,h,true);text.alignment=TextAnchor.MiddleCenter;
        }
    }
}
