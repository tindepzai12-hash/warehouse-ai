using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    // Assigned tasks and stock use existing OMS endpoints; no Figma/sample records enter the list.
    public sealed class ImportedWarehouseLists:MonoBehaviour
    {
        ImportedFigmaNavigation nav;TMP_FontAsset font;bool tasks,busy,hasData;
        RectTransform page,content,filters;TMP_Text feedback;TMP_InputField search;
        OmsMobileApi.TaskRow[] taskRows=Array.Empty<OmsMobileApi.TaskRow>();
        OmsMobileApi.InventoryRow[] stockRows=Array.Empty<OmsMobileApi.InventoryRow>();
        readonly List<Button> chips=new List<Button>();int selected,generation;
        string presentation;GameObject detail;bool configured;float selectedAt=-10;
        bool En=>ImportedAppLocalization.IsEnglish;
        string T(string vi,string en)=>En?en:vi;
        public void Configure(ImportedFigmaNavigation owner,bool isTasks)
        {
            nav=owner;tasks=isTasks;font=nav.screens[0].root.GetComponentInChildren<TMP_Text>(true).font;
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            configured=true;Build();if(isActiveAndEnabled)StartCoroutine(Load());
        }
        void OnEnable(){if(configured)StartCoroutine(Load());}
        void OnDisable(){generation++;StopAllCoroutines();busy=false;if(detail!=null){detail.SetActive(false);Destroy(detail);detail=null;}}
        void Update()
        {
            string next=(En?"en":"vi")+ImportedSettings.TextScale+nav.Settings.Dark;
            if(configured&&presentation!=next&&!busy){Build();Render();nav.Settings.ApplyTheme();}
            for(int i=0;i<chips.Count;i++)
            {
                float t=(Time.unscaledTime-selectedAt)/.22f;
                float scale=!PrototypeUi.ReducedMotion&&i==selected&&t>=0&&t<1?1+Mathf.Sin(t*Mathf.PI)*.025f:1;
                var rect=(RectTransform)chips[i].transform;rect.localScale=Vector3.one*scale;
                float x=0;for(int j=0;j<i;j++)x+=((RectTransform)chips[j].transform).rect.width+8;
                rect.anchoredPosition=new Vector2(x-(scale-1)*rect.rect.width*.5f,-2+(scale-1)*24);
            }
        }
        void Build()
        {
            presentation=(En?"en":"vi")+ImportedSettings.TextScale+nav.Settings.Dark;
            if(page!=null){page.gameObject.SetActive(false);Destroy(page.gameObject);}chips.Clear();detail=null;
            page=PrototypeUi.Panel(transform,"Live warehouse page",0,0,412,915,nav.Settings.Dark,0,null,"00000000");
            PrototypeUi.Text(page,font,tasks?T("Nhiệm vụ","Tasks"):T("Tra tồn kho","Check inventory"),80,65,250,44,20,nav.Settings.Dark,true).alignment=TextAlignmentOptions.Midline;
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
                JarVisUi.Scroll(page,23,240,366,568,out content);
            }
        }
        IEnumerator Load()
        {
            if(!OmsMobileSession.IsReady)
            {
                Render();feedback.text=T("Đăng nhập để xem dữ liệu kho của bạn.","Sign in to view your warehouse data.");yield break;
            }
            if(busy)yield break;busy=true;int version=generation;
            feedback.text=T("Đang tải dữ liệu kho…","Loading warehouse data…");
            string error=null;
            if(tasks)yield return OmsMobileApi.GetTasks(r=>{taskRows=r.tasks??Array.Empty<OmsMobileApi.TaskRow>();hasData=true;},e=>error=e);
            else if(!string.IsNullOrWhiteSpace(search.text))yield return OmsMobileApi.SearchInventory(search.text.Trim(),r=>{stockRows=r.items??Array.Empty<OmsMobileApi.InventoryRow>();hasData=true;},e=>error=e);
            else yield return OmsMobileApi.GetInventory(r=>{stockRows=r.items??Array.Empty<OmsMobileApi.InventoryRow>();hasData=true;},e=>error=e);
            if(version!=generation)yield break;busy=false;Render();
            if(error!=null)feedback.text=!OmsMobileSession.IsReady?T("Đăng nhập để xem dữ liệu kho của bạn.","Sign in to view your warehouse data."):
                OmsMobileSession.EmployeeCode=="admin"?T("Tài khoản admin quản lý dữ liệu trên web. App dành cho nhân viên kho.","Admin manages data on the web. This workspace is for warehouse employees."):
                T("Chưa tải được dữ liệu. Kiểm tra kết nối và thử lại.","Couldn't load data. Check your connection and retry.");
        }
        void ClearContent()
        {foreach(Transform child in content){child.gameObject.SetActive(false);Destroy(child.gameObject);}}
        void Render()
        {
            ClearContent();float y=0;
            if(tasks)
            {
                string[] keys={null,"IN_PROGRESS","ASSIGNED","COMPLETED"};string[] names={T("Tất cả","All"),T("Đang làm","Working"),T("Đã giao","Assigned"),T("Hoàn tất","Completed")};
                for(int i=0;i<chips.Count;i++){int count=i==0?taskRows.Length:taskRows.Count(r=>r.status==keys[i]);var label=chips[i].GetComponentInChildren<TMP_Text>();label.text=names[i]+" ("+(hasData?count.ToString():"—")+")";label.color=i==selected?PrototypeUi.Accent:PrototypeUi.Ink(nav.Settings.Dark);
                    UniformUiSurface.Use(chips[i].GetComponent<Image>(),14,i==selected?PrototypeUi.Hex(nav.Settings.Dark?"28446b":"e1ecff"):PrototypeUi.Surface(nav.Settings.Dark),PrototypeUi.Hex(nav.Settings.Dark?"6689b5":"a4bfe9"),1,true,false);}
                var rows=taskRows.Where(r=>selected==0||r.status==keys[selected]).ToArray();
                feedback.text=rows.Length==0?T("Chưa có nhiệm vụ ở mục này. Nhiệm vụ quản lý giao sẽ xuất hiện tại đây.","No tasks in this view. Tasks assigned by your manager appear here."):T("Chọn nhiệm vụ để xem nội dung và cập nhật tiến độ.","Select a task to review its instructions and update progress.");
                foreach(var row in rows)
                {
                    var item=PrototypeUi.Panel(content,"Task "+row.task_code,0,y,366,122,nav.Settings.Dark);
                    PrototypeUi.Text(item,font,row.task_code,20,20,174,22,11,nav.Settings.Dark,false,true);
                    var badge=PrototypeUi.Panel(item,"Status",210,16,136,25,nav.Settings.Dark,10,nav.Settings.Dark?"294875":"e7effb","00000000");var stateLabel=PrototypeUi.Text(badge,font,Status(row.status),6,0,124,25,10,nav.Settings.Dark);stateLabel.alignment=TextAlignmentOptions.Center;
                    PrototypeUi.Text(item,font,row.title,20,49,326,40,15,nav.Settings.Dark,true);
                    PrototypeUi.Text(item,font,row.locationCode+" · "+row.task_type,20,93,297,24,12,nav.Settings.Dark,false,true);PrototypeUi.Icon(item,"chevron",326,94,16,PrototypeUi.Muted(nav.Settings.Dark));
                    PrototypeUi.Button(item,()=>TaskDetail(row));y+=134;
                }
            }
            else
            {
                feedback.text=stockRows.Length==0?T("Chưa có hàng hóa phù hợp. Quản lý nhập hàng và số dư trên web trước.","No matching stock. Your manager must enter products and opening balances on the web."):stockRows.Length+T(" vị trí tồn kho"," stock locations");
                foreach(var row in stockRows){var item=PrototypeUi.Panel(content,"Stock "+row.sku,0,y,366,118.8f,nav.Settings.Dark);
                    PrototypeUi.Text(item,font,row.sku,20,20,230,24,12,nav.Settings.Dark,false,true);
                    var badge=PrototypeUi.Panel(item,"Available",245,18,101,25,nav.Settings.Dark,10,nav.Settings.Dark?"214b3d":"dbf3e8","00000000");
                    var total=PrototypeUi.Text(badge,font,row.available+T(" khả dụng"," available"),6,0,89,25,10,nav.Settings.Dark,true);total.alignment=TextAlignmentOptions.Center;total.color=PrototypeUi.Hex(nav.Settings.Dark?"a0e9c1":"087b50");
                    PrototypeUi.Text(item,font,row.name,20,55,326,35,15,nav.Settings.Dark,true);
                    PrototypeUi.Text(item,font,row.lotCode+" · "+row.locationCode,20,91,326,26,12,nav.Settings.Dark,false,true);
                    PrototypeUi.Button(item,()=>StockDetail(row));y+=130.8f;}
            }
            if(y==0){JarVisUi.Icon(content,tasks?"tasks":"package",161,36,44);JarVisUi.Text(content,font,T("Dữ liệu được đồng bộ từ OMS","Data is synchronized from OMS"),16,98,334,48,15).alignment=TextAlignmentOptions.Center;
                JarVisUi.Button(content,font,T("Tải lại","Refresh"),73,170,220,()=>StartCoroutine(Load()));y=238;}
            feedback.gameObject.SetActive(y==238);content.sizeDelta=new Vector2(366,Mathf.Max(598,y));nav.Settings.ApplyTheme();
            if(!tasks){PrototypeUi.Action(content,font,T("Quét mã để tra cứu","Scan to look up stock"),0,y,366,44,nav.Settings.Dark,()=>nav.Show("5:331"),true,"scan");content.sizeDelta=new Vector2(366,Mathf.Max(568,y+66));}
        }
        void Card(RectTransform rect){var image=rect.gameObject.AddComponent<Image>();image.color=Color.white;image.raycastTarget=false;UniformUiSurface.Use(image,22,PrototypeUi.Surface(nav.Settings.Dark),PrototypeUi.Line(nav.Settings.Dark),1,false,false);}
        public bool CloseDetail()
        {
            if(detail==null)return false;
            detail.SetActive(false);Destroy(detail);detail=null;return true;
        }
        string Status(string status)
        {switch(status){case "ASSIGNED":return T("Được giao","Assigned");case "ACCEPTED":return T("Đã nhận","Accepted");case "IN_PROGRESS":return T("Đang làm","In progress");case "COMPLETED":return T("Hoàn thành","Completed");case "BLOCKED":return T("Tạm dừng","Blocked");default:return status;}}
        void StockDetail(OmsMobileApi.InventoryRow row)
        {
            CloseDetail();
            detail=PrototypeDialog.Details(nav,T("Chi tiết tồn kho","Stock details"),row.sku+" · "+row.name,new[]{
                T("Mã vạch: ","Barcode: ")+row.barcode,T("Lô hàng: ","Lot: ")+row.lotCode,T("Vị trí: ","Location: ")+row.locationCode,
                T("Ngày nhập: ","Received: ")+row.receivedAt,T("Hạn dùng: ","Expiry: ")+(string.IsNullOrEmpty(row.expiryDate)?T("Không ghi nhận","Not recorded"):row.expiryDate),
                T("Tồn thực tế: ","On hand: ")+row.onHand+" · "+T("Đã giữ: ","Reserved: ")+row.reserved,
                T("Hư hỏng: ","Damaged: ")+row.damaged+" · "+T("Khả dụng: ","Available: ")+row.available+" "+row.unit},null,null,
                T("Chỉ đường","Map"),()=>{CloseDetail();nav.OpenMap(row.locationCode);});
        }
        void TaskDetail(OmsMobileApi.TaskRow row)
        {
            CloseDetail();string next=row.status=="ASSIGNED"?"ACCEPTED":row.status=="ACCEPTED"||row.status=="BLOCKED"?"IN_PROGRESS":row.status=="IN_PROGRESS"?"COMPLETED":null;
            detail=PrototypeDialog.Details(nav,T("Chi tiết nhiệm vụ","Task details"),row.task_code+" · "+row.title,new[]{
                T("Nhân viên: ","Employee: ")+row.employeeCode,T("Trạng thái: ","Status: ")+Status(row.status),
                T("Loại: ","Type: ")+row.task_type+" · "+T("Vị trí: ","Location: ")+row.locationCode,
                T("Hàng hóa: ","Product: ")+row.productName+" · "+row.sku,
                T("Số lượng yêu cầu: ","Expected quantity: ")+row.expected_quantity+" "+row.unit},T("Tiếp tục","Continue"),next==null?null:(Action)(()=>StartCoroutine(Transition(row,next))),
                T("Chỉ đường","Map"),()=>{CloseDetail();nav.OpenMap(row.locationCode);});
        }
        IEnumerator Transition(OmsMobileApi.TaskRow task,string next)
        {
            if(busy)yield break;busy=true;string error=null;OmsMobileApi.TaskRow result=null;
            yield return OmsMobileApi.ChangeTaskStatus(task.id,next,r=>result=r,e=>error=e);busy=false;
            if(result==null){nav.Notice(T("Chưa cập nhật được nhiệm vụ. Kiểm tra kết nối và thử lại.","Couldn't update the task. Check your connection and retry."));yield break;}
            task.status=result.status;Render();TaskDetail(task);nav.Notice(T("Đã cập nhật tiến độ nhiệm vụ.","Task progress updated."));
        }
    }
}
