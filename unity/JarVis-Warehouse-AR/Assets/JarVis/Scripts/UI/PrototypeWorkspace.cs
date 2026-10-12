using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI
{
    // New presentation destinations only. Transactions still use the existing inbound/outbound routes.
    public sealed class PrototypeWorkspace:MonoBehaviour
    {
        ImportedFigmaNavigation nav;TMP_FontAsset font;GameObject current;TMP_Text clock,badge;string key;
        bool En=>ImportedAppLocalization.IsEnglish;string T(string vi,string en)=>En?en:vi;
        public void Configure(ImportedFigmaNavigation owner)
        {
            nav=owner;font=nav.screens[0].root.GetComponentInChildren<TMP_Text>(true).font;
            foreach(string id in new[]{"profile","activity","tools","transactions"})nav.RegisterPrototypeScreen("prototype:"+id,JarVisUi.Rect(nav.screens[0].root.transform.parent,"Prototype "+id,0,0,412,915).gameObject);
            nav.screens.First(s=>s.id=="215:2").root.AddComponent<WarehouseMapScreen>().Configure(nav);
            string[] ids={"5:935","14:387","5:450","5:1029"},kinds={"inbound","outbound","label","incident"};
            for(int i=0;i<ids.Length;i++)nav.screens.First(s=>s.id==ids[i]).root.AddComponent<PrototypeOperationPresentation>().Configure(nav,kinds[i]);
            nav.screens.First(s=>s.id=="5:331").root.AddComponent<PrototypeScannerPresentation>().Configure(nav);
            nav.IncludePrototypeNavigation("5:331");
        }
        void Update()
        {
            if(nav==null)return;string next=nav.CurrentScreen+En+nav.Settings.Dark+ImportedSettings.TextScale;
            if(next!=key){key=next;if(nav.CurrentScreen?.StartsWith("prototype:")==true)Build(nav.CurrentScreen.Substring(10));}
            if(clock!=null){clock.text=nav.Presence.TodayClock;badge.text=nav.Presence.CanWork?T("● Đang đếm","● Counting"):T("○ Đã tạm dừng","○ Paused");}
        }
        void Build(string kind)
        {
            current=nav.screens.First(s=>s.id=="prototype:"+kind).root;foreach(Transform child in current.transform){child.gameObject.SetActive(false);Destroy(child.gameObject);}clock=null;
            bool dark=nav.Settings.Dark;string title=kind=="profile"?T("Hồ sơ","Profile"):kind=="activity"?T("Hoạt động của tôi","My activity"):kind=="transactions"?T("Nhập / Xuất kho","Inbound / Outbound"):T("Công cụ","Tools");
            PrototypeUi.Shell(current.transform,font,title,dark,()=>{if(kind=="activity")nav.Show("prototype:profile");else nav.Home();},()=>nav.Show("prototype:tools"),out var content);
            if(kind=="profile")
            {
                JarVisUi.Rect(content,"Justin photograph",0,5,64,64).gameObject.AddComponent<UserPortraitGraphic>().raycastTarget=false;
                PrototypeUi.Text(content,font,"Justin",80,10,250,28,21,dark,true);PrototypeUi.Text(content,font,T("Nhân viên vận hành","Warehouse operator"),80,42,250,26,13,dark,false,true);
                var card=PrototypeUi.Panel(content,"Profile links",0,95,366,182,dark);
                Row(card,T("Hoạt động của tôi","My activity"),T("Xem thời gian trực tuyến hôm nay","View today's online time"),"chart",0,()=>nav.Show("prototype:activity"));
                Row(card,T("Cài đặt","Settings"),T("Ngôn ngữ, cỡ chữ và giao diện","Language, text size and appearance"),"settings",82,()=>nav.Show("5:1308"));
                PrototypeUi.Text(content,font,T("Thời gian được tính khi bật Online. Tắt rồi bật lại sẽ đếm tiếp trong cùng ngày.","Time is counted while Online. Turning it off and back on continues the same day's total."),0,300,366,80,12,dark,false,true);
                var logout=PrototypeUi.Action(content,font,T("Đăng xuất","Sign out"),0,408,366,48,dark,()=>PrototypeDialog.SignOut(nav),true);UniformUiSurface.Use(logout.GetComponent<Image>(),15,PrototypeUi.Hex("d82c3e"),Color.clear,0,false,false);
            }
            else if(kind=="activity")
            {
                PrototypeUi.Text(content,font,T("Một nơi riêng để xem thời gian hoạt động.","A dedicated place to view activity time."),0,0,366,32,12,dark,false,true);
                var card=PrototypeUi.Panel(content,"Daily activity",0,42,366,282,dark,24);PrototypeUi.Text(card,font,T("Hôm nay","Today"),24,24,120,24,14,dark,true);
                badge=PrototypeUi.Text(card,font,"",160,24,182,24,12,dark,false,true);badge.alignment=TextAlignmentOptions.TopRight;
                PrototypeUi.Text(card,font,T("Thời gian trực tuyến","Online time"),24,67,318,22,12,dark,false,true);
                clock=PrototypeUi.Text(card,font,nav.Presence.TodayClock,24,94,318,65,43,dark,true);
                PrototypeUi.Text(card,font,T("Đặt lại lúc 00:00 · Đài Loan (UTC+8)","Resets at 00:00 · Taiwan (UTC+8)"),24,174,318,48,12,dark,false,true);
                PrototypeUi.Action(card,font,nav.Presence.CanWork?T("Tắt trực tuyến","Go offline"):T("Bật trực tuyến","Go online"),24,222,318,42,dark,()=>PrototypeDialog.Presence(nav),true,"power");
                PrototypeUi.Text(content,font,T("Cách thời gian được ghi nhận","How time is recorded"),0,348,366,35,19,dark,true);
                var rules=PrototypeUi.Panel(content,"Counting rules",0,380,366,182,dark);
                string[] vi={"Bật trực tuyến: bắt đầu đếm.","Tắt trực tuyến: tạm dừng.","Bật lại: cộng tiếp thời gian hôm nay.","Qua 00:00 Đài Loan: bắt đầu ngày mới."},en={"Go online: start counting.","Go offline: pause.","Go online again: continue today's total.","After 00:00 Taiwan: start a new day."},symbols={"check","power","refresh","moon"};
                for(int i=0;i<4;i++){PrototypeUi.Icon(rules,symbols[i],18,21+i*38,16,PrototypeUi.Hex("087b50"));PrototypeUi.Text(rules,font,En?en[i]:vi[i],45,17+i*38,299,36,12,dark);}
                PrototypeUi.Text(content,font,T("Thời gian Online hỗ trợ theo dõi hoạt động, không phải bảng chấm công tính lương.","Online time supports activity tracking; it is not payroll attendance."),0,583,366,65,12,dark,false,true);
            }
            else if(kind=="transactions")
            {
                PrototypeUi.Text(content,font,T("Chọn nghiệp vụ để tiếp tục.","Choose an operation to continue."),0,0,366,36,13,dark,false,true);
                Tool(content,T("Nhập kho","Inbound"),T("Đối chiếu hàng, lô và số lượng nhận.","Check products, lots and received quantities."),"arrowDown",44,"5:935");
                Tool(content,T("Xuất kho","Outbound"),T("Đối chiếu với nhiệm vụ xuất kho.","Check against the assigned goods issue task."),"arrowUp",144,"14:387");
            }
            else
            {
                string[] vi={"Quét mã","Tra tồn kho","Nhiệm vụ","Báo cáo sự cố","Tạo tem hàng","Nhập kho","Xuất kho","AI JarVis"},en={"Scan code","Check inventory","Tasks","Report issue","Create label","Inbound","Outbound","AI JarVis"},icons={"scan","box","tasks","alert","tag","arrowDown","arrowUp","chat"},routes={"5:331","5:780","48:244","5:1029","5:450","5:935","14:387","5:1216"};
                string[] desc={"Tra mã vạch và QR","Sản phẩm, lô và vị trí","Theo dõi việc được giao","Ghi nhận vấn đề tại kho","Mã hàng và tem vị trí","Kiểm đếm hàng nhận","Xác nhận hàng xuất","Hỗ trợ vận hành kho"},descEn={"Read barcodes and QR","Products, lots and locations","Track your assigned work","Record warehouse issues","Product and location labels","Check received stock","Confirm goods issue","Warehouse assistance"};
                PrototypeUi.Text(content,font,T("Một công cụ, một mục đích rõ ràng.","One tool, one clear purpose."),0,0,366,30,12,dark,false,true);
                for(int i=0;i<vi.Length;i++)Tool(content,En?en[i]:vi[i],En?descEn[i]:desc[i],icons[i],42+i*89,routes[i]);content.sizeDelta=new Vector2(366,42+vi.Length*89);
            }
        }
        void Row(Transform parent,string title,string subtitle,string symbol,float y,Action clicked)
        {
            bool dark=nav.Settings.Dark;var rect=JarVisUi.Rect(parent,title,20,y+14,326,72);PrototypeUi.Button(rect,clicked);
            PrototypeUi.Icon(rect,symbol,0,16,24,PrototypeUi.Ink(dark));PrototypeUi.Text(rect,font,title,42,8,250,25,14,dark,true);PrototypeUi.Text(rect,font,subtitle,42,37,250,32,11,dark,false,true);PrototypeUi.Icon(rect,"chevron",306,21,16,PrototypeUi.Muted(dark));
        }
        void Tool(Transform parent,string title,string subtitle,string symbol,float y,string route)
        {
            bool dark=nav.Settings.Dark;string fill=symbol=="box"?"fff1db":symbol=="tasks"?"dcf6f2":symbol=="alert"?"ffe7ed":"e0eeff";
            var card=PrototypeUi.Panel(parent,title,0,y,366,77,dark,18,dark?"243a56":fill,null,true);PrototypeUi.Button(card,()=>nav.Show(route));PrototypeUi.Icon(card,symbol,18,26,24,PrototypeUi.Ink(dark));
            PrototypeUi.Text(card,font,title,58,subtitle==""?23:15,270,28,14,dark,true);if(subtitle!="")PrototypeUi.Text(card,font,subtitle,58,42,270,26,11,dark,false,true);PrototypeUi.Icon(card,"chevron",329,30,18,PrototypeUi.Muted(dark));
        }
    }
}
