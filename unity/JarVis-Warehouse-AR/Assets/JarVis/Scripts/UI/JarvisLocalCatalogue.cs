using System;
using System.Linq;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    // The three physical samples explicitly supplied by the owner. Never seeds OMS.
    public static class JarvisLocalCatalogue
    {
        [Serializable] public sealed class Item
        {public string barcode,sku,code,name,nameEn,color,colorEn,kind,kindEn,image,lot,expiry;public int quantity=1;}
        [Serializable] sealed class Data{public Item[] items;}
        static Item[] items;
        public static Item[] Items=>items??(items=JsonUtility.FromJson<Data>(Resources.Load<TextAsset>("JarvisLocalCatalogue").text).items);
        public static Item Find(string barcode)=>Items.FirstOrDefault(p=>p.barcode==barcode);
        public static string Name(Item p,bool en)=>en?p.nameEn:p.name;
        public static string Location(Item p,bool en)=>p.code=="MILK"?"B":RoompresentDeliveryMission.Current.Complete?(p.code=="A1"?"B":"C"):(en?"Not updated":"Chưa cập nhật");
        public static string Context(string question,string barcode,bool en)
        {
            var p=Find(barcode);if(p==null)return question??"";
            string data="barcode="+p.barcode+"; SKU="+p.sku+"; name="+Name(p,en)+"; location="+Location(p,en)+"; stockQuantity="+p.quantity+"; color="+(en?p.colorEn:p.color)+"; lot="+p.lot+" ("+(en?"sample lot":"lô mẫu")+"); expiry="+(p.expiry??"N/A");
            return (question??"")+(en?"\n[Exact app catalogue match; owner supplied sample, not live OMS. Answer from these known fields; stockQuantity is known. Answer directly. No generic confirmation request or disclaimer. Books without completed putaway have location 'Not updated'; do not invent B/C. ":"\n[Danh mục app khớp chính xác barcode; mẫu do chủ app cung cấp, chưa đồng bộ OMS. Trả lời theo thông tin này, stockQuantity đã biết. Trả lời thẳng; không kết bằng yêu cầu xác nhận hay chú thích chung. Vở chưa hoàn tất nhiệm vụ có vị trí 'Chưa cập nhật', không đoán B/C. ")+data+"]";
        }
        public static float Cards(Transform content,TMP_FontAsset font,ImportedFigmaNavigation nav,string query)
        {
            bool en=ImportedAppLocalization.IsEnglish,dark=nav.Settings.Dark;float y=0;string q=RoompresentMap2D.Normalize(query);
            foreach(var p in Items)
            {
                if(!string.IsNullOrWhiteSpace(q)&&!RoompresentMap2D.Normalize(p.barcode+" "+p.sku+" "+p.code+" "+p.name+" "+p.nameEn+" "+p.lot+" "+Location(p,en)+" "+p.color).Contains(q))continue;
                var card=PrototypeUi.Panel(content,"Catalogue "+p.code,0,y,366,124,dark,18);
                Picture(card,p,12,12,80,100);
                var sku=PrototypeUi.Text(card,font,p.sku,106,10,244,18,11,dark,false,true);
                float titleY=10+FitHeight(sku,16)+4;
                var title=ProductTitle(card,font,Name(p,en),106,titleY,244,14,dark);
                float stockY=titleY+title.rectTransform.sizeDelta.y+6;
                var badge=StockBadge(card,font,p,en,106,stockY,dark);
                float locationX=106+badge.sizeDelta.x+8,locationWidth=350-locationX;
                string location=(en?"Location: ":"Vị trí: ")+Location(p,en);
                var place=PrototypeUi.Text(card,font,location,locationX,stockY+3,locationWidth,20,11,dark,true);
                float detailsY;
                if(place.GetPreferredValues(location,float.PositiveInfinity,0).x<=locationWidth)
                {
                    place.textWrappingMode=TextWrappingModes.NoWrap;
                    detailsY=stockY+Mathf.Max(badge.sizeDelta.y,FitHeight(place,18)+3)+6;
                }
                else
                {
                    place.rectTransform.anchoredPosition=new Vector2(106,-(stockY+badge.sizeDelta.y+4));
                    place.rectTransform.sizeDelta=new Vector2(244,20);
                    detailsY=stockY+badge.sizeDelta.y+4+FitHeight(place,18)+6;
                }
                var details=PrototypeUi.Text(card,font,p.barcode+"\n"+p.lot+" · "+(en?"Sample lot":"Lô mẫu"),106,detailsY,244,30,10,dark,false,true);
                float height=Mathf.Max(124,detailsY+FitHeight(details,28)+10);
                card.sizeDelta=new Vector2(366,height);
                PrototypeUi.Button(card,()=>Show(nav,p));y+=height+10;
            }
            return y;
        }
        static float FitHeight(TMP_Text text,float minimum)
        {
            float height=Mathf.Max(minimum,Mathf.Ceil(text.GetPreferredValues(text.text,text.rectTransform.sizeDelta.x,0).y)+2);
            text.rectTransform.sizeDelta=new Vector2(text.rectTransform.sizeDelta.x,height);
            return height;
        }
        static TMP_Text ProductTitle(Transform parent,TMP_FontAsset font,string value,float x,float y,float width,float size,bool dark)
        {
            var title=PrototypeUi.Text(parent,font,value,x,y,width,40,size,dark,true);title.name="Product title";
            title.textWrappingMode=TextWrappingModes.NoWrap;
            // Keep the original font; shorten only the size of names that need it.
            for(float candidate=size;candidate>=size-2;candidate--)
            {
                title.fontSize=candidate*ImportedSettings.TextScale;
                if(title.GetPreferredValues(value,float.PositiveInfinity,0).x<=width)break;
            }
            if(title.GetPreferredValues(value,float.PositiveInfinity,0).x>width)
            {
                // At large accessibility sizes, keep the quantity/unit together on a deliberate second line.
                int separator=value.LastIndexOf(" · ",StringComparison.Ordinal);
                if(separator>0)title.text=value.Substring(0,separator)+"\n"+value.Substring(separator+3);
                title.textWrappingMode=TextWrappingModes.Normal;
            }
            FitHeight(title,18);return title;
        }
        static RectTransform StockBadge(Transform parent,TMP_FontAsset font,Item p,bool en,float x,float y,bool dark)
        {
            var badge=PrototypeUi.Panel(parent,"Stock badge",x,y,100,22,dark,7,dark?"234b3f":"ddf5e5","00000000");
            var label=PrototypeUi.Text(badge,font,p.quantity+(en?" available":" có sẵn"),8,0,120,22,11,dark,true);
            label.name="Stock quantity";label.color=PrototypeUi.Hex(dark?"a8ecc4":"216447");label.textWrappingMode=TextWrappingModes.NoWrap;
            label.alignment=TextAlignmentOptions.MidlineLeft;
            var needed=label.GetPreferredValues(label.text,float.PositiveInfinity,0);
            float width=Mathf.Ceil(needed.x)+16,height=Mathf.Max(22,Mathf.Ceil(needed.y)+6);
            badge.sizeDelta=new Vector2(width,height);label.rectTransform.sizeDelta=new Vector2(width-16,height);return badge;
        }
        public static void Picture(Transform parent,Item p,float x,float y,float w,float h)
        {
            var tex=Resources.Load<Texture2D>(p.image);if(tex==null)return;
            float scale=Mathf.Min(w/tex.width,h/tex.height),rw=tex.width*scale,rh=tex.height*scale;
            var raw=JarVisUi.Rect(parent,"Photo "+p.code,x+(w-rw)/2,y+(h-rh)/2,rw,rh).gameObject.AddComponent<RawImage>();raw.texture=tex;raw.raycastTarget=false;
        }
        public static GameObject Show(ImportedFigmaNavigation nav,Item p)
        {
            bool en=ImportedAppLocalization.IsEnglish,dark=nav.Settings.Dark;var root=nav.screens.First(s=>s.id==nav.CurrentScreen).root;var font=root.GetComponentInChildren<TMP_Text>(true).font;
            var veil=PrototypeUi.Panel(root.transform,"Catalogue details",0,0,412,915,dark,0,"182b4099","00000000");veil.GetComponent<Image>().raycastTarget=true;veil.gameObject.AddComponent<PrototypeDialog>();
            var card=PrototypeUi.Panel(veil,"Product details card",24,p.code=="MILK"?190:218,364,p.code=="MILK"?520:464,dark,24);
            var title=ProductTitle(card,font,Name(p,en),20,16,282,18,dark);
            float photoY=16+title.rectTransform.sizeDelta.y+16;
            void Close(){veil.gameObject.SetActive(false);UnityEngine.Object.Destroy(veil.gameObject);}
            var close=PrototypeUi.Action(card,font,"",308,12,44,44,dark,Close);
            UniformUiSurface.Use(close.GetComponent<Image>(),12,PrototypeUi.Hex(dark?"542936":"fff0f2"),PrototypeUi.Hex("f2bdc6"),1,false,false);
            PrototypeUi.Icon(close.transform,"close",12,12,20,PrototypeUi.Hex(dark?"ff8e9c":"d73550"));
            Picture(card,p,20,photoY,92,108);
            var sku=PrototypeUi.Text(card,font,"SKU: "+p.sku,128,photoY+2,216,20,11,dark,false,true);
            float badgeY=photoY+2+FitHeight(sku,18)+8;
            var badge=StockBadge(card,font,p,en,128,badgeY,dark);
            var place=PrototypeUi.Text(card,font,(en?"Location: ":"Vị trí: ")+Location(p,en),128,badgeY+badge.sizeDelta.y+8,216,36,12,dark,true);
            float summaryBottom=badgeY+badge.sizeDelta.y+8+FitHeight(place,18);
            float bodyY=Mathf.Max(photoY+108,summaryBottom)+12;
            JarVisUi.Scroll(card,20,bodyY,324,p.code=="MILK"?246:190,out var body);float y=0;
            string[] facts={"Barcode: "+p.barcode,(en?"Type: ":"Loại: ")+(en?p.kindEn:p.kind),(en?"Color: ":"Màu sắc: ")+(en?p.colorEn:p.color),(en?"Lot: ":"Lô: ")+p.lot+" ("+(en?"sample":"mẫu")+")",(en?"Expiry: ":"Hạn sử dụng: ")+(p.expiry??(en?"Not applicable":"Không áp dụng"))};
            if(p.code=="MILK")facts=facts.Concat(new[]{en?"Manufactured: 14/06/2027 · Received: 10/11/2027":"Sản xuất: 14/06/2027 · Nhập kho: 10/11/2027",en?"Nutrition: 128 kcal · calcium 120 mg/carton":"Dinh dưỡng: 128 kcal · canxi 120 mg/hộp"}).ToArray();
            foreach(var f in facts){var t=PrototypeUi.Text(body,font,f,0,y,324,42,12,dark);t.textWrappingMode=TextWrappingModes.Normal;float height=Mathf.Max(26,t.GetPreferredValues(f,324,0).y+6);t.rectTransform.sizeDelta=new Vector2(324,height);y+=height;}
            body.sizeDelta=new Vector2(324,y);
            float visible=Mathf.Min(y,p.code=="MILK"?246:190),buttonY=bodyY+visible+12,totalHeight=buttonY+64;
            ((RectTransform)body.parent).sizeDelta=new Vector2(324,visible);
            card.sizeDelta=new Vector2(364,totalHeight);card.anchoredPosition=new Vector2(24,-(915-totalHeight)*.5f);
            PrototypeUi.Action(card,font,en?"Close":"Đóng",20,buttonY,324,44,dark,Close,true);
            var canvas=veil.gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=100;veil.gameObject.AddComponent<GraphicRaycaster>();return veil.gameObject;
        }
    }
}
