using System;
using System.Linq;
using JarVis.Warehouse.Localization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JarVis.Warehouse.UI
{
    public static class RoompresentScanPopup
    {
        public static void Show(ImportedFigmaNavigation nav,string code,bool both,Action acknowledge)
        {
            var root=nav.screens.First(s=>s.id==nav.CurrentScreen).root;
            Create(root.transform,root.GetComponentInChildren<TMP_Text>(true).font,nav.Settings.Dark,ImportedAppLocalization.IsEnglish,code,both,acknowledge);
        }
        public static GameObject Create(Transform parent,TMP_FontAsset font,bool dark,bool en,string code,bool both,Action acknowledge)
        {
            var veil=PrototypeUi.Panel(parent,"Book scan success",0,0,412,915,dark,0,"101b30cc","00000000");
            veil.GetComponent<Image>().raycastTarget=true;veil.SetAsLastSibling();
            veil.gameObject.AddComponent<PrototypeDialog>();
            var card=PrototypeUi.Panel(veil,"Verified book",26,280,360,308,dark,24);
            bool a1=code==RoompresentDeliveryRecord.A1;string book=a1?"A1":"A2",stop=a1?"B":"C";
            PrototypeUi.Text(card,font,en?"Scan successful":"Quét thành công",20,14,320,38,21,dark,true);
            PrototypeUi.Text(card,font,(en?"Book ":"Vở ")+book,20,57,320,34,17,dark,true);
            string[] facts={ (en?"Barcode: ":"Mã barcode: ")+code,(en?"Product code: ":"Mã sản phẩm: ")+(a1?"CPNB-1004":"CPNB-1003"),(en?"Delivery point: ":"Điểm giao: ")+stop};
            for(int i=0;i<facts.Length;i++)
            {
                PrototypeUi.Panel(card,"Bullet "+i,25,104+i*28,5,5,dark,2.5f,dark?"83b4ff":"1255d5","00000000");
                PrototypeUi.Text(card,font,facts[i],40,95+i*28,296,30,13,dark);
            }
            PrototypeUi.Text(card,font,both?(en?"Both books verified. Next: scan QR A to align the room.":"Đã đủ hai vở. Tiếp theo: quét QR A để căn bản đồ."):(en?"Tap OK to scan the other book.":"Bấm OK để quét quyển còn lại."),20,184,320,48,13,dark,false,true);
            bool clicked=false;
            var button=PrototypeUi.Action(card,font,both?(en?"OK · Scan QR A":"OK · Quét QR A"):(en?"OK · Scan next":"OK · Quét tiếp"),20,246,320,44,dark,()=>{
                if(clicked)return;clicked=true;veil.gameObject.SetActive(false);UnityEngine.Object.Destroy(veil.gameObject);acknowledge?.Invoke();
            },true);
            button.name="Acknowledge book";
            return veil.gameObject;
        }
    }
}
