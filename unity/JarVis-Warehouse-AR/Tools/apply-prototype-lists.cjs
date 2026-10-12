const fs=require('node:fs');const file='Assets/JarVis/Scripts/UI/ImportedWarehouseLists.cs';let s=fs.readFileSync(file,'utf8');let a=s.indexOf('        void Build()'),b=s.indexOf('        IEnumerator Load()',a);
s=s.slice(0,a)+`        void Build()
        {
            presentation=(En?"en":"vi")+ImportedSettings.TextScale+nav.Settings.Dark;
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}chips.Clear();detail=null;
            page=PrototypeUi.Panel(transform,"Live warehouse page",0,0,412,915,nav.Settings.Dark,0,null,"00000000");
            PrototypeUi.Text(page,font,tasks?T("Nhiệm vụ","Tasks"):T("Tra tồn kho","Check inventory"),80,65,250,44,22,nav.Settings.Dark,true).alignment=TextAlignmentOptions.MidlineLeft;
            var back=JarVisUi.Rect(page,"Back",23,65,44,44);PrototypeUi.Button(back,nav.Back);PrototypeUi.Icon(back,"back",10,10,24,PrototypeUi.Ink(nav.Settings.Dark));
            var plus=JarVisUi.Rect(page,"Tools plus",345,65,44,44);PrototypeUi.Button(plus,()=>nav.Show("prototype:tools"));PrototypeUi.Icon(plus,"plus",10,10,24,PrototypeUi.Ink(nav.Settings.Dark));
            feedback=PrototypeUi.Text(page,font,"",23,tasks?190:220,366,58,12,nav.Settings.Dark,false,true);
            if(tasks)
            {
                JarVisUi.Scroll(page,23,130,366,54,out filters,true);float[] widths={96,112,106,112};float x=0;
                for(int i=0;i<4;i++){int choice=i;var rect=PrototypeUi.Panel(filters,"Task filter "+i,x,4,widths[i],38,nav.Settings.Dark,14,null,null,true);var button=PrototypeUi.Button(rect,()=>{selected=choice;selectedAt=Time.unscaledTime;Render();});var label=PrototypeUi.Text(rect,font,"",8,0,widths[i]-16,38,11,nav.Settings.Dark);label.alignment=TextAlignmentOptions.Center;chips.Add(button);x+=widths[i]+8;}
                filters.sizeDelta=new Vector2(x-8,54);JarVisUi.Scroll(page,23,204,366,598,out content);
            }
            else
            {
                PrototypeUi.Text(page,font,T("Tìm theo SKU, mã vạch, lô hoặc vị trí.","Search by SKU, barcode, lot or location."),23,130,366,32,12,nav.Settings.Dark,false,true);
                var box=PrototypeUi.Panel(page,"Stock query",23,170,366,48,nav.Settings.Dark,15);PrototypeUi.Icon(box,"search",14,12,24,PrototypeUi.Muted(nav.Settings.Dark));
                var textArea=JarVisUi.Rect(box,"Text viewport",48,0,304,48);textArea.gameObject.AddComponent<RectMask2D>();
                var value=PrototypeUi.Text(textArea,font,"",0,0,304,48,14,nav.Settings.Dark);value.alignment=TextAlignmentOptions.MidlineLeft;
                var hint=PrototypeUi.Text(textArea,font,T("Tìm hàng hóa trong kho…","Search warehouse stock…"),0,0,304,48,13,nav.Settings.Dark,false,true);hint.alignment=TextAlignmentOptions.MidlineLeft;
                string draft=search!=null?search.text:"";search=box.gameObject.AddComponent<TMP_InputField>();search.textComponent=value;search.placeholder=hint;search.textViewport=textArea;search.targetGraphic=box.GetComponent<Image>();search.targetGraphic.raycastTarget=true;search.characterLimit=200;search.lineType=TMP_InputField.LineType.SingleLine;search.text=draft;search.onSubmit.AddListener(_=>StartCoroutine(Load()));box.gameObject.AddComponent<ImportedInputCaretRepair>().Configure(search);
                JarVisUi.Scroll(page,23,240,366,510,out content);
                PrototypeUi.Action(page,font,T("Quét mã để tra cứu","Scan to look up stock"),23,758,366,48,nav.Settings.Dark,()=>nav.Show("5:331"),true,"scan");
            }
        }
`+s.slice(b);
s=s.replace('string[] keys={null,"IN_PROGRESS","ACCEPTED","COMPLETED"}','string[] keys={null,"IN_PROGRESS","ASSIGNED","COMPLETED"}').replace('T("Đã nhận","Accepted"),T("Hoàn thành","Completed")','T("Đã giao","Assigned"),T("Hoàn tất","Completed")');
s=s.replace('label.color=i==selected?Color.white:nav.Settings.Dark?JarVisDesignTokens.DarkInk:JarVisDesignTokens.Ink','label.color=i==selected?PrototypeUi.Accent:PrototypeUi.Ink(nav.Settings.Dark)');
s=s.replace('UniformUiSurface.Use(chips[i].GetComponent<Image>(),12,i==selected?JarVisDesignTokens.Accent:Color.white,i==selected?Color.clear:JarVisDesignTokens.Border,i==selected?0:1,false,i!=selected)','UniformUiSurface.Use(chips[i].GetComponent<Image>(),14,i==selected?PrototypeUi.Hex(nav.Settings.Dark?"28446b":"e1ecff"):PrototypeUi.Surface(nav.Settings.Dark),PrototypeUi.Hex(nav.Settings.Dark?"6689b5":"a4bfe9"),1,true,false)');
let first=s.indexOf('                foreach(var row in rows)'),end=s.indexOf('\n            }\n            else',first);
s=s.slice(0,first)+`                foreach(var row in rows)
                {
                    var item=PrototypeUi.Panel(content,"Task "+row.task_code,0,y,366,122,nav.Settings.Dark);
                    PrototypeUi.Text(item,font,row.task_code,20,20,174,22,11,nav.Settings.Dark,false,true);
                    var badge=PrototypeUi.Panel(item,"Status",210,16,136,25,nav.Settings.Dark,10,nav.Settings.Dark?"294875":"e7effb","00000000");var stateLabel=PrototypeUi.Text(badge,font,Status(row.status),6,0,124,25,10,nav.Settings.Dark);stateLabel.alignment=TextAlignmentOptions.Center;
                    PrototypeUi.Text(item,font,row.title,20,49,326,40,15,nav.Settings.Dark,true);
                    PrototypeUi.Text(item,font,row.locationCode+" · "+row.task_type,20,93,297,24,12,nav.Settings.Dark,false,true);PrototypeUi.Icon(item,"chevron",326,94,16,PrototypeUi.Muted(nav.Settings.Dark));
                    PrototypeUi.Button(item,()=>TaskDetail(row));y+=134;
                }`+s.slice(end);
// Keep the existing live stock/detail loading; use the approved shared surface radius and color.
s=s.replace('UniformUiSurface.Use(image,16,Color.white,JarVisDesignTokens.Border)','UniformUiSurface.Use(image,22,PrototypeUi.Surface(nav.Settings.Dark),PrototypeUi.Line(nav.Settings.Dark),1,false,false)');
s=s.replace('content.sizeDelta=new Vector2(380,Mathf.Max(540,y))','feedback.gameObject.SetActive(y==238);content.sizeDelta=new Vector2(366,Mathf.Max(598,y))');
fs.writeFileSync(file,s);
