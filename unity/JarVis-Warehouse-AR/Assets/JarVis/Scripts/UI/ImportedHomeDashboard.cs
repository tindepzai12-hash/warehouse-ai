using System;
using System.Collections;
using System.Linq;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Home presentation owns no attendance/inventory data. Presence and tasks stay server-owned.
    public sealed class ImportedHomeDashboard : MonoBehaviour
    {
        ImportedFigmaNavigation nav;
        TMP_FontAsset font;
        RectTransform page,content;
        ScrollRect scroll;
        TMP_Text streakTitle,praise,rule,taskCopy,taskMeta;
        RectTransform streakHeading;
        OmsMobileApi.TaskRow latestTask;
        bool latestIsTrial;
        UnityEngine.UI.Image[] days;
        ImportedUtilityIcon[] checks;
        OmsMobileApi.TaskRow[] tasks;
        bool configured,busy,failed;
        int generation;
        string presentation;
        double next;
        bool backdropPreview;
        float backdropScroll;
        bool En=>ImportedAppLocalization.IsEnglish;
        string T(string vi,string en)=>En?en:vi;
        Color Ink=>nav.Settings.Dark?JarVisDesignTokens.DarkInk:JarVisDesignTokens.Ink;
        Color Muted=>nav.Settings.Dark?new Color(.68f,.76f,.86f):JarVisDesignTokens.Secondary;
        Color Fill(Color light)=>nav.Settings.Dark?new Color(.1f,.16f,.24f):light;
        Color Edge(Color light)=>nav.Settings.Dark?new Color(.28f,.38f,.5f):light;

        public void Configure(ImportedFigmaNavigation owner)
        {
            nav=owner;font=nav.screens[0].root.GetComponentInChildren<TMP_Text>(true).font;
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            configured=true;Build();if(isActiveAndEnabled)StartCoroutine(LoadTasks());
        }
        void OnEnable(){if(configured&&!backdropPreview)StartCoroutine(LoadTasks());}
        public void BeginBackdropPreview(){backdropPreview=true;backdropScroll=scroll!=null?scroll.verticalNormalizedPosition:1;if(presentation!=(En?"en":"vi")+nav.Settings.Dark+ImportedSettings.TextScale)Build();Paint();if(scroll!=null)scroll.verticalNormalizedPosition=1;}
        public void EndBackdropPreview(){backdropPreview=false;if(scroll!=null)scroll.verticalNormalizedPosition=backdropScroll;}
        void OnDisable(){generation++;StopAllCoroutines();busy=false;tasks=null;failed=false;}
        void Update()
        {
            if(!configured||Time.realtimeSinceStartupAsDouble<next)return;
            next=Time.realtimeSinceStartupAsDouble+.3;
            string key=(En?"en":"vi")+nav.Settings.Dark+ImportedSettings.TextScale;
            if(key!=presentation)Build();Paint();
        }
        TMP_Text Label(Transform parent,string vi,string en,float x,float y,float w,float h,float size,bool bold=false)
        {
            var text=JarVisUi.Text(parent,font,T(vi,en),x,y,w,h,size);text.color=Ink;
            if(bold)text.fontStyle=FontStyles.Bold;return text;
        }
        RectTransform Card(Transform parent,string name,float x,float y,float w,float h,Color fill,Color border)
        {
            var rect=JarVisUi.Rect(parent,name,x,y,w,h);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            UniformUiSurface.Use(image,16,Fill(fill),Edge(border),1,false,false);image.raycastTarget=false;return rect;
        }
        void Build()
        {
            float previous=scroll!=null?scroll.verticalNormalizedPosition:1;
            nav.Presence.ClearCards();
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}
            presentation=(En?"en":"vi")+nav.Settings.Dark+ImportedSettings.TextScale;
            bool dark=nav.Settings.Dark;page=PrototypeUi.Panel(transform,"Approved Home dashboard",0,0,412,915,dark,0,null,"00000000");
            var portrait=JarVisUi.Rect(page,"Justin avatar",29,65.5f,44,44).gameObject.AddComponent<UserPortraitGraphic>();portrait.raycastTarget=false;
            PrototypeUi.Button(JarVisUi.Rect(page,"Profile",23,58,52,52),()=>nav.Show("prototype:profile"));
            PrototypeUi.Text(page,font,T("Xin chào, Justin","Hello, Justin"),91,67.2f,242,27,18,dark,true);
            PrototypeUi.Text(page,font,T("Nhân viên vận hành","Warehouse operator"),91,94.2f,242,20,11,dark,false,true);
            var bell=PrototypeUi.Action(page,font,"",345,63,44,44,dark,()=>nav.Notice(T("Chưa có thông báo mới.","No new notifications.")));
            PrototypeUi.Icon(bell.transform,"bell",10,10,24,PrototypeUi.Ink(dark));
            scroll=JarVisUi.Scroll(page,23,141,366,678,out content);
            var column=PrototypeUi.Column(content,16);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            nav.Presence.AttachCompactCard(content);
            var presence=content.Find("Online presence") as RectTransform;PrototypeUi.Flow(presence,70);
            float extra=Mathf.Max(0,ImportedSettings.TextScale-1)*180;
            var streak=PrototypeUi.Panel(content,"Activity streak",0,0,366,226+extra,dark,22,dark?"213249":"eef6ff","00000000");PrototypeUi.Flow(streak,226+extra);if(!dark)UniformUiSurface.Gradient(streak.GetComponent<UnityEngine.UI.Image>(),PrototypeUi.Hex("f5f9ff"));
            streakHeading=JarVisUi.Rect(streak,"Left aligned streak heading",16,12,334,42);
            var headingRow=streakHeading.gameObject.AddComponent<HorizontalLayoutGroup>();
            headingRow.childAlignment=TextAnchor.MiddleLeft;headingRow.spacing=10;
            headingRow.childControlWidth=false;headingRow.childControlHeight=false;headingRow.childForceExpandWidth=false;headingRow.childForceExpandHeight=false;
            var fire=JarVisUi.Rect(streakHeading,"Living flame",0,0,28,34).gameObject.AddComponent<PrototypeFlame>();fire.raycastTarget=false;
            streakTitle=PrototypeUi.Text(streakHeading,font,"",0,0,296,42,16,dark,true);
            streakTitle.alignment=TextAlignmentOptions.MidlineLeft;streakTitle.textWrappingMode=TextWrappingModes.NoWrap;
            streakTitle.enableAutoSizing=true;streakTitle.fontSizeMax=16*ImportedSettings.TextScale;streakTitle.fontSizeMin=13;
            var mascot=JarVisUi.Rect(streak,"Approved JarVis encouragement",266,55,84,82).gameObject.AddComponent<PrototypeMascot>();mascot.Configure();
            days=new UnityEngine.UI.Image[7];checks=new ImportedUtilityIcon[7];
            string[] vi={"T2","T3","T4","T5","T6","T7","CN"},en={"Mon","Tue","Wed","Thu","Fri","Sat","Sun"};
            for(int i=0;i<7;i++)
            {
                var d=JarVisUi.Rect(streak,"Activity day "+i,17+i*33,72,26,26);days[i]=d.gameObject.AddComponent<UnityEngine.UI.Image>();
                checks[i]=JarVisUi.Icon(d,"check",5,5,16);checks[i].color=Color.white;
                var caption=PrototypeUi.Text(streak,font,En?en[i]:vi[i],15+i*33,106,30,18,10.5f,dark,true);caption.alignment=TextAlignmentOptions.Center;
            }
            var encouragement=PrototypeUi.Panel(streak,"Praise",17,135,332,55+extra,dark,15,dark?"273b54":"ffffff","00000000");
            praise=PrototypeUi.Text(encouragement,font,"",12,8,308,43+extra,12,dark);praise.richText=true;
            PrototypeUi.Icon(streak,"info",17,202+extra,14,PrototypeUi.Muted(dark));
            rule=PrototypeUi.Text(streak,font,"",37,199+extra,312,24,10,dark,false,true);
            var heading=PrototypeUi.Text(content,font,T("Phím tắt nhanh","Quick actions"),0,0,366,24,19,dark,true);PrototypeUi.Flow(heading.rectTransform,24);heading.overflowMode=TextOverflowModes.Overflow;
            var grid=JarVisUi.Rect(content,"Quick actions",0,0,366,138);PrototypeUi.Flow(grid,138);
            var layout=grid.gameObject.AddComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;layout.cellSize=new Vector2(177,63);layout.spacing=new Vector2(12,12);
            Shortcut(grid,"Nhập / Xuất kho","Inbound / Outbound","arrowDown","e0eeff","1255d5",()=>nav.Show("prototype:transactions"));
            Shortcut(grid,"Tra tồn kho","Check inventory","box","fff1db","a8540b",()=>nav.Show("5:780"));
            Shortcut(grid,"Nhiệm vụ","Tasks","tasks","dcf6f2","086c68",()=>nav.Show("48:244"));
            Shortcut(grid,"Báo cáo sự cố","Report issue","alert","ffe7ed","b82640",()=>nav.Show("5:1029"));
            var taskHeading=PrototypeUi.Text(content,font,T("Nhiệm vụ hôm nay","Today's tasks"),0,0,366,24,19,dark,true);PrototypeUi.Flow(taskHeading.rectTransform,24);taskHeading.overflowMode=TextOverflowModes.Overflow;
            var taskCard=PrototypeUi.Panel(content,"Assigned tasks summary",0,0,366,190+extra,dark,22,dark?"203b60":"edf4ff",dark?"5078aa":"a7c6f3");PrototypeUi.Flow(taskCard,190+extra);
            var taskBadge=PrototypeUi.Panel(taskCard,"Task icon badge",18,14,32,32,dark,10,dark?"315786":"d5e5ff","00000000");
            PrototypeUi.Icon(taskBadge,"tasks",6,6,20,PrototypeUi.Hex(dark?"bdd6ff":"1255d5"));
            var taskKicker=PrototypeUi.Text(taskCard,font,T("NHIỆM VỤ GẦN NHẤT","LATEST TASK"),62,14,286,32,11,dark,true);
            taskKicker.alignment=TextAlignmentOptions.MidlineLeft;taskKicker.color=PrototypeUi.Hex(dark?"bdd6ff":"254e8a");
            taskCopy=PrototypeUi.Text(taskCard,font,"",18,55,330,48+extra,17,dark,true);taskCopy.alignment=TextAlignmentOptions.MidlineLeft;
            taskMeta=PrototypeUi.Text(taskCard,font,"",18,108+extra,330,22,11,dark,false,true);taskMeta.alignment=TextAlignmentOptions.MidlineLeft;taskMeta.textWrappingMode=TextWrappingModes.NoWrap;
            var taskAction=PrototypeUi.Panel(taskCard,"Open latest task action",18,132+extra,330,44,dark,13,"1255d5","00000000");
            var actionText=PrototypeUi.Text(taskAction,font,T("Xem nhiệm vụ","View task"),14,0,270,44,13,dark,true);actionText.alignment=TextAlignmentOptions.MidlineLeft;actionText.color=Color.white;
            PrototypeUi.Icon(taskAction,"chevron",298,13,18,Color.white);PrototypeUi.Button(taskCard,OpenLatestTask);
            content.sizeDelta=new Vector2(366,850+extra*2);Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=previous;Paint();
        }
        void Shortcut(Transform parent,string vi,string en,string symbol,string fill,string accent,Action action)
        {
            bool dark=nav.Settings.Dark;string darkFill=symbol=="box"?"423326":symbol=="tasks"?"203b3b":symbol=="alert"?"442c37":"243a56";
            Color tone=PrototypeUi.Hex(accent);if(dark)tone=PrototypeUi.Hex(symbol=="box"?"ffc286":symbol=="tasks"?"82ded2":symbol=="alert"?"ffafbe":"9bc3ff");
            var rect=PrototypeUi.Panel(parent,"Quick action "+symbol,0,0,177,63,dark,18,dark?darkFill:fill,"00000000",true);
            PrototypeUi.Button(rect,action);PrototypeUi.Icon(rect,symbol,13,20,24,tone);
            var text=PrototypeUi.Text(rect,font,T(vi,en),49,13,104,40,13,dark,true);text.alignment=TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing=true;text.fontSizeMax=13*ImportedSettings.TextScale;text.fontSizeMin=11;
            PrototypeUi.Icon(rect,"chevron",157,25,14,tone);
        }
        void Paint()
        {
            var streak=nav.Presence.Snapshot?.streak;
            if(streakTitle==null)return;
            streakTitle.text=streak==null?T("Chưa có dữ liệu chuỗi hoạt động","Activity streak unavailable"):
                streak.currentStreak==0?T("Bắt đầu chuỗi hoạt động","Start your activity streak"):
                T(streak.currentStreak+" ngày hoạt động liên tiếp",streak.currentStreak+(streak.currentStreak==1?" consecutive active day":" consecutive active days"));
            float headingWidth=Mathf.Min(296,Mathf.Ceil(streakTitle.GetPreferredValues(streakTitle.text).x));
            if(Mathf.Abs(streakTitle.rectTransform.sizeDelta.x-headingWidth)>.5f){streakTitle.rectTransform.sizeDelta=new Vector2(headingWidth,42);LayoutRebuilder.MarkLayoutForRebuild(streakHeading);}
            praise.text=streak==null?T("<b>Bạn đang làm rất tốt!</b>\nCứ bình tĩnh, từng việc một nhé.","<b>You are doing well!</b>\nTake it one task at a time."):
                streak.todayQualified?T("<b>Rất tốt, Justin!</b>\nBạn đang giữ nhịp làm việc đều đặn.","<b>Well done, Justin!</b>\nYou're keeping a steady working rhythm."):
                T("<b>Cố gắng của bạn rất đáng ghi nhận!</b>\nChúc bạn một ca làm việc thuận lợi.","<b>Your effort matters!</b>\nHave a steady and rewarding shift.");
            rule.text=T("Tính ngày khi Online đủ 1 phút · Đài Loan","Day counts after 1 minute online · Taiwan");
            for(int i=0;i<days.Length;i++)
            {
                bool qualified=streak?.week!=null&&i<streak.week.Length&&streak.week[i].qualified;
                UniformUiSurface.Use(days[i],14,qualified?JarVisDesignTokens.Accent:Fill(Color.white),Edge(new Color(.73f,.82f,.94f)),qualified?0:1,false,false);checks[i].enabled=qualified;
            }
            latestTask=LatestTask(tasks);latestIsTrial=false;
            if(!OmsMobileSession.IsReady){taskCopy.text=T("Đăng nhập để xem nhiệm vụ.","Sign in to view tasks.");taskMeta.text="";}
            else if(latestTask!=null)
            {
                taskCopy.text=string.IsNullOrWhiteSpace(latestTask.title)?latestTask.task_code:latestTask.title;
                taskMeta.text=TaskStatus(latestTask.status)+(string.IsNullOrWhiteSpace(latestTask.locationCode)?"":" · "+latestTask.locationCode)+(failed?T(" · Chưa làm mới"," · Refresh pending"):"");
            }
            else if(OmsMobileSession.EmployeeCode=="admin"||tasks!=null&&!failed)
            {
                latestIsTrial=true;taskCopy.text=T("Roompresent · A1 → B, A2 → C","Roompresent · A1 → B, A2 → C");
                taskMeta.text=T("NHIỆM VỤ THỬ","TEST MISSION")+" · "+RoompresentDeliveryMission.Summary;
            }
            else{taskCopy.text=failed?T("Chưa tải được nhiệm vụ","Couldn't load tasks"):T("Đang tải nhiệm vụ…","Loading tasks…");taskMeta.text=failed?T("Chạm để thử lại trong Nhiệm vụ","Tap to retry in Tasks"):"";}
        }
        // Prefer assignment time, then creation time. Keep server order when dates are absent/equal.
        static OmsMobileApi.TaskRow LatestTask(OmsMobileApi.TaskRow[] rows)
        {return rows?.Where(r=>r!=null).OrderByDescending(TaskTime).FirstOrDefault();}
        static DateTimeOffset TaskTime(OmsMobileApi.TaskRow task)
        {
            foreach(string value in new[]{task.assignedAt,task.assigned_at,task.createdAt,task.created_at})
                if(DateTimeOffset.TryParse(value,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeUniversal,out var date))return date;
            return DateTimeOffset.MinValue;
        }
        string TaskStatus(string state)
        {switch(state){case "ASSIGNED":return T("Được giao","Assigned");case "ACCEPTED":return T("Đã nhận","Accepted");case "IN_PROGRESS":return T("Đang làm","In progress");case "COMPLETED":return T("Hoàn thành","Completed");case "BLOCKED":return T("Tạm dừng","Blocked");case "CANCELLED":return T("Đã hủy","Cancelled");default:return state??"";}}
        void OpenLatestTask()
        {
            if(latestIsTrial){RoompresentDeliveryMission.Open(nav);return;}
            nav.Show("48:244");
            if(latestTask!=null)nav.screens.First(s=>s.id=="48:244").root.GetComponent<ImportedWarehouseLists>()?.TaskDetail(latestTask);
        }

        IEnumerator LoadTasks()
        {
            if(busy||!OmsMobileSession.IsReady)yield break;
            if(OmsMobileSession.EmployeeCode=="admin"){failed=true;tasks=null;Paint();yield break;}
            busy=true;int version=generation;string requestToken=OmsMobileSession.AccessToken;OmsMobileApi.TaskList reply=null;string error=null;
            yield return OmsMobileApi.GetTasks(r=>reply=r,e=>error=e);
            if(version!=generation)yield break;busy=false;if(requestToken!=OmsMobileSession.AccessToken){tasks=null;failed=false;Paint();yield break;}failed=error!=null||reply==null;
            if(!failed)tasks=reply.tasks??Array.Empty<OmsMobileApi.TaskRow>();Paint();
        }
    }
}
