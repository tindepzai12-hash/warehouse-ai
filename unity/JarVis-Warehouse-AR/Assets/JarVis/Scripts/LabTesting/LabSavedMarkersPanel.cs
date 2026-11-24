using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Lab
{
    public sealed class LabSavedMarkersPanel : MonoBehaviour
    {
        Font font,bold;
        public void Initialize(LabTestSession owner)
        {
            font=Resources.Load<Font>("BeVietnamPro-Regular");bold=Resources.Load<Font>("BeVietnamPro-Bold");
            var root=GetComponent<RectTransform>();root.anchorMin=Vector2.zero;root.anchorMax=Vector2.one;root.offsetMin=root.offsetMax=Vector2.zero;
            gameObject.AddComponent<Image>().color=new Color(.98f,.99f,1);
            Label(root,"Mốc đã lưu · "+owner.SavedMarkerCount+"/5",20,true,16,14,380,32);
            Label(root,"X/Z là vị trí trên mặt bằng, đơn vị mét.\nTọa độ AR tạm khác tọa độ map đã nhập.",12,false,16,54,380,42);
            var viewport=new GameObject("Saved marker scroll",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect)).GetComponent<RectTransform>();
            viewport.SetParent(root,false);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(16,134);viewport.offsetMax=new Vector2(-16,-106);viewport.GetComponent<Image>().color=Color.white;
            var content=new GameObject("Saved marker rows",typeof(RectTransform)).GetComponent<RectTransform>();content.SetParent(viewport,false);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            float top=0;
            foreach(var row in owner.SavedMarkerRows)
            {
                float height=!row.captured?52:row.mapEntered?132:118;if(row.text.Contains("Cần đối chiếu"))height+=26;
                var box=new GameObject(row.id,typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();box.SetParent(content,false);Top(box,0,top,380,height);box.GetComponent<Image>().color=row.captured?new Color(.91f,.95f,1):new Color(.95f,.96f,.98f);
                Label(box,row.text,13,false,10,8,360,height-16);top+=height+8;
            }
            content.sizeDelta=new Vector2(0,top);
            Button(root,"Gắn / sửa vị trí trên map",16,76,380,46,()=>{Destroy(gameObject);owner.OpenPositionEntry();},owner.PositionDraftAvailable);
            Button(root,"Quay lại",16,18,182,46,()=>Destroy(gameObject),true);
            Button(root,"Xuất ZIP dữ liệu mốc",214,18,182,46,owner.Export,owner.SavedMarkerCount>0);
        }
        static void Top(RectTransform r,float x,float y,float width,float height){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);}
        Text Label(Transform parent,string value,int size,bool heavy,float x,float y,float width,float height)
        {
            var label=new GameObject("Saved marker text",typeof(RectTransform),typeof(Text)).GetComponent<Text>();label.transform.SetParent(parent,false);Top(label.rectTransform,x,y,width,height);
            label.font=heavy?bold:font;label.fontSize=size;label.text=value;label.color=new Color(.04f,.09f,.2f);label.raycastTarget=false;label.supportRichText=false;return label;
        }
        void Button(Transform root,string title,float x,float y,float width,float height,UnityEngine.Events.UnityAction action,bool enabled)
        {
            var rect=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();rect.SetParent(root,false);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=new Vector2(x,y);rect.sizeDelta=new Vector2(width,height);
            rect.GetComponent<Image>().color=new Color(.84f,.91f,1);var button=rect.GetComponent<Button>();button.interactable=enabled;button.onClick.AddListener(action);
            var label=Label(rect,title,13,true,6,0,width-12,height);label.alignment=TextAnchor.MiddleCenter;
        }
    }
}
