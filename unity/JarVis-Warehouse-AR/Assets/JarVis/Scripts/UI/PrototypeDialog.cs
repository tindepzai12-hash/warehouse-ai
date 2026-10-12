using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI
{
    // Native counterpart of the prototype's confirmation modal; existing services own the action.
    public sealed class PrototypeDialog:MonoBehaviour
    {
        public static void Presence(ImportedFigmaNavigation nav)
        {
            if(!nav.Presence.CanWork){nav.Presence.Toggle();return;}
            bool en=ImportedAppLocalization.IsEnglish;
            Show(nav,en?"Go offline?":"Tắt trực tuyến?",en?"Today's time pauses at "+nav.Presence.TodayClock+". Going online again continues the total; it resets at 00:00 Taiwan.":"Thời gian hôm nay dừng ở "+nav.Presence.TodayClock+". Bật lại sẽ đếm tiếp; đặt lại lúc 00:00 Đài Loan.",en?"Go offline":"Tắt trực tuyến",en?"Keep working":"Tiếp tục làm việc",nav.Presence.Toggle);
        }
        public static void SignOut(ImportedFigmaNavigation nav)
        {
            bool en=ImportedAppLocalization.IsEnglish;
            Show(nav,en?"Sign out?":"Đăng xuất?",en?"Your online session will end. Today's activity time is retained.":"Phiên trực tuyến sẽ kết thúc. Thời gian hoạt động hôm nay vẫn được giữ lại.",en?"Sign out":"Đăng xuất",en?"Cancel":"Hủy",nav.Presence.SignOut);
        }
        public static GameObject Show(ImportedFigmaNavigation nav,string title,string body,string confirm,string cancel,Action action)
        {
            var root=nav.screens.First(s=>s.id==nav.CurrentScreen).root;
            foreach(var old in root.GetComponentsInChildren<PrototypeDialog>(true)){old.gameObject.SetActive(false);Destroy(old.gameObject);}
            var veil=PrototypeUi.Panel(root.transform,"Prototype confirmation",0,0,412,915,nav.Settings.Dark,0,"182b4099","00000000");veil.GetComponent<Image>().raycastTarget=true;veil.gameObject.AddComponent<PrototypeDialog>();
            var font=root.GetComponentInChildren<TMP_Text>(true).font;
            var card=PrototypeUi.Panel(veil,"Confirmation card",26,284,360,316,nav.Settings.Dark,24);
            PrototypeUi.Text(card,font,title,24,24,312,38,20,nav.Settings.Dark,true);
            PrototypeUi.Text(card,font,body,24,77,312,102,13,nav.Settings.Dark);
            void Close(){veil.gameObject.SetActive(false);Destroy(veil.gameObject);}
            PrototypeUi.Action(card,font,cancel,24,197,312,44,nav.Settings.Dark,Close);
            PrototypeUi.Action(card,font,confirm,24,253,312,44,nav.Settings.Dark,()=>{Close();action();},true);
            return veil.gameObject;
        }
        public static GameObject Details(ImportedFigmaNavigation nav,string title,string summary,string[] facts,string actionLabel,Action action,string mapLabel=null,Action mapAction=null)
        {
            var root=nav.screens.First(s=>s.id==nav.CurrentScreen).root;bool dark=nav.Settings.Dark;
            var veil=PrototypeUi.Panel(root.transform,"Prototype details",0,0,412,915,dark,0,"182b4099","00000000");veil.GetComponent<Image>().raycastTarget=true;veil.gameObject.AddComponent<PrototypeDialog>();
            var font=root.GetComponentInChildren<TMP_Text>(true).font;var card=PrototypeUi.Panel(veil,"Detail card",26,196,360,522,dark,24);
            PrototypeUi.Text(card,font,title,24,24,274,38,21,dark,true);PrototypeUi.Text(card,font,summary,24,76,312,64,13,dark,false,true);
            JarVisUi.Scroll(card,24,149,312,278,out var content);float y=0;
            foreach(string fact in facts){PrototypeUi.Text(content,font,fact,12,y,288,44,12,dark,false,true);y+=48;}content.sizeDelta=new Vector2(312,y);
            void Close(){veil.gameObject.SetActive(false);Destroy(veil.gameObject);}
            var close=JarVisUi.Rect(card,"Close detail",302,18,40,40);PrototypeUi.Button(close,Close);PrototypeUi.Icon(close,"close",10,10,20,PrototypeUi.Muted(dark));
            bool en=ImportedAppLocalization.IsEnglish;
            if(mapAction==null)
            {
                PrototypeUi.Action(card,font,en?"Close":"Đóng",24,454,action==null?312:151,44,dark,Close);
                if(action!=null)PrototypeUi.Action(card,font,actionLabel,185,454,151,44,dark,action,true);
            }
            else
            {
                PrototypeUi.Action(card,font,en?"Close":"Đóng",24,454,action==null?151:98,44,dark,Close);
                PrototypeUi.Action(card,font,mapLabel,action==null?185:129,454,action==null?151:98,44,dark,mapAction,action==null);
                if(action!=null)PrototypeUi.Action(card,font,actionLabel,234,454,102,44,dark,action,true);
            }
            return veil.gameObject;
        }
        void OnDisable(){Destroy(gameObject);}
    }
}
