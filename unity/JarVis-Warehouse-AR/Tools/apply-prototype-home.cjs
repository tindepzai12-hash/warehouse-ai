const fs=require('node:fs');const file='Assets/JarVis/Scripts/UI/ImportedHomeDashboard.cs';let s=fs.readFileSync(file,'utf8');
const a=s.indexOf('        void Build()'),b=s.indexOf('        void Paint()',a);if(a<0||b<0)throw Error('Home owner not found');
s=s.slice(0,a)+`        void Build()
        {
            float previous=scroll!=null?scroll.verticalNormalizedPosition:1;
            nav.Presence.ClearCards();
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}
            presentation=(En?"en":"vi")+nav.Settings.Dark+ImportedSettings.TextScale;
            bool dark=nav.Settings.Dark;page=PrototypeUi.Panel(transform,"Approved Home dashboard",0,0,412,915,dark,0,null,"00000000");
            var portrait=JarVisUi.Rect(page,"Justin avatar",27,62,44,44).gameObject.AddComponent<UserPortraitGraphic>();portrait.raycastTarget=false;
            PrototypeUi.Button(JarVisUi.Rect(page,"Profile",23,58,52,52),()=>nav.Show("prototype:profile"));
            PrototypeUi.Text(page,font,T("Xin chào, Justin","Hello, Justin"),83,62,242,28,18,dark,true);
            PrototypeUi.Text(page,font,T("Nhân viên vận hành","Warehouse operator"),83,90,242,20,11,dark,false,true);
            var bell=PrototypeUi.Action(page,font,"",345,63,44,44,dark,()=>nav.Notice(T("Chưa có thông báo mới.","No new notifications.")));
            PrototypeUi.Icon(bell.transform,"bell",10,10,24,PrototypeUi.Ink(dark));
            scroll=JarVisUi.Scroll(page,23,141,366,678,out content);
            var column=PrototypeUi.Column(content,16);content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            nav.Presence.AttachCompactCard(content);
            var presence=content.Find("Online presence") as RectTransform;PrototypeUi.Flow(presence,70);
            float extra=Mathf.Max(0,ImportedSettings.TextScale-1)*180;
            var streak=PrototypeUi.Panel(content,"Activity streak",0,0,366,226+extra,dark,22,dark?"213249":"eef7ff",dark?"3a516e":"d3e6ff");PrototypeUi.Flow(streak,226+extra);
            var fire=JarVisUi.Rect(streak,"Living flame",16,16,28,34).gameObject.AddComponent<PrototypeFlame>();fire.raycastTarget=false;
            streakTitle=PrototypeUi.Text(streak,font,"",54,17,295,42,16,dark,true);streakTitle.enableAutoSizing=true;streakTitle.fontSizeMax=16*ImportedSettings.TextScale;streakTitle.fontSizeMin=13;
            var mascot=JarVisUi.Rect(streak,"Approved JarVis encouragement",266,55,84,82).gameObject.AddComponent<PrototypeMascot>();mascot.Configure();
            days=new UnityEngine.UI.Image[7];checks=new ImportedUtilityIcon[7];
            string[] vi={"T2","T3","T4","T5","T6","T7","CN"},en={"M","T","W","T","F","S","S"};
            for(int i=0;i<7;i++)
            {
                var d=JarVisUi.Rect(streak,"Activity day "+i,17+i*33,72,26,26);days[i]=d.gameObject.AddComponent<UnityEngine.UI.Image>();
                checks[i]=JarVisUi.Icon(d,"check",5,5,16);checks[i].color=Color.white;
                var caption=PrototypeUi.Text(streak,font,En?en[i]:vi[i],12+i*33,106,36,18,10,dark,false,true);caption.alignment=TextAlignmentOptions.Center;
            }
            var encouragement=PrototypeUi.Panel(streak,"Praise",17,135,332,55+extra,dark,15,dark?"273b54":"ffffff",dark?"405773":"dbeaff");
            praise=PrototypeUi.Text(encouragement,font,"",12,8,308,43+extra,12,dark);praise.richText=true;
            PrototypeUi.Icon(streak,"info",17,202+extra,14,PrototypeUi.Muted(dark));
            rule=PrototypeUi.Text(streak,font,"",37,199+extra,312,24,10,dark,false,true);
            var heading=PrototypeUi.Text(content,font,T("Phím tắt nhanh","Quick actions"),0,0,366,24,19,dark,true);PrototypeUi.Flow(heading.rectTransform,24);
            var grid=JarVisUi.Rect(content,"Quick actions",0,0,366,138);PrototypeUi.Flow(grid,138);
            var layout=grid.gameObject.AddComponent<GridLayoutGroup>();layout.constraint=GridLayoutGroup.Constraint.FixedColumnCount;layout.constraintCount=2;layout.cellSize=new Vector2(177,63);layout.spacing=new Vector2(12,12);
            Shortcut(grid,"Nhập / Xuất kho","Inbound / Outbound","arrowDown","e0eeff","1255d5",()=>nav.Show("prototype:transactions"));
            Shortcut(grid,"Tra tồn kho","Check inventory","box","fff1db","a8540b",()=>nav.Show("5:780"));
            Shortcut(grid,"Nhiệm vụ","Tasks","tasks","dcf6f2","086c68",()=>nav.Show("48:244"));
            Shortcut(grid,"Báo cáo sự cố","Report issue","alert","ffe7ed","b82640",()=>nav.Show("5:1029"));
            var taskHeading=PrototypeUi.Text(content,font,T("Nhiệm vụ hôm nay","Today's tasks"),0,0,366,24,19,dark,true);PrototypeUi.Flow(taskHeading.rectTransform,24);
            var taskCard=PrototypeUi.Panel(content,"Assigned tasks summary",0,0,366,68+extra,dark);PrototypeUi.Flow(taskCard,68+extra);
            PrototypeUi.Icon(taskCard,"file",16,22,24,PrototypeUi.Muted(dark));taskCopy=PrototypeUi.Text(taskCard,font,"",56,12,275,46+extra,13,dark,false,true);
            PrototypeUi.Icon(taskCard,"chevron",337,24,18,PrototypeUi.Muted(dark));PrototypeUi.Button(taskCard,()=>nav.Show("48:244"));
            content.sizeDelta=new Vector2(366,660+extra*2);Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=previous;Paint();
        }
        void Shortcut(Transform parent,string vi,string en,string symbol,string fill,string accent,Action action)
        {
            bool dark=nav.Settings.Dark;string darkFill=symbol=="box"?"423326":symbol=="tasks"?"203b3b":symbol=="alert"?"442c37":"243a56";
            Color tone=PrototypeUi.Hex(accent);if(dark)tone=PrototypeUi.Hex(symbol=="box"?"ffc286":symbol=="tasks"?"82ded2":symbol=="alert"?"ffafbe":"9bc3ff");
            var rect=PrototypeUi.Panel(parent,"Quick action "+symbol,0,0,177,63,dark,18,dark?darkFill:fill,dark?"526580":fill,true);
            PrototypeUi.Button(rect,action);PrototypeUi.Icon(rect,symbol,13,20,24,tone);
            var text=PrototypeUi.Text(rect,font,T(vi,en),49,13,104,40,13,dark,true);text.alignment=TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing=true;text.fontSizeMax=13*ImportedSettings.TextScale;text.fontSizeMin=11;
            PrototypeUi.Icon(rect,"chevron",157,25,14,tone);
        }
`+s.slice(b);
s=s.replace('T("Rất tốt, Justin! Hôm nay đã được ghi nhận.","Well done, Justin! Today\'s activity is recorded.")','T("<b>Rất tốt, Justin!</b>\\nHôm nay đã được ghi nhận.","<b>Well done, Justin!</b>\\nToday\'s activity is recorded.")');
fs.writeFileSync(file,s);
