const fs=require('node:fs');const file='Assets/JarVis/Scripts/UI/ImportedWarehouseLists.cs';let s=fs.readFileSync(file,'utf8');
const start=s.indexOf('        void StockDetail('),end=s.indexOf('        IEnumerator Transition(',start);if(start<0||end<0)throw Error('Detail owners missing');
s=s.slice(0,start)+`        void StockDetail(OmsMobileApi.InventoryRow row)
        {
            CloseDetail();
            detail=PrototypeDialog.Details(nav,T("Chi tiết tồn kho","Stock details"),row.sku+" · "+row.name,new[]{
                T("Mã vạch: ","Barcode: ")+row.barcode,T("Lô hàng: ","Lot: ")+row.lotCode,T("Vị trí: ","Location: ")+row.locationCode,
                T("Ngày nhập: ","Received: ")+row.receivedAt,T("Hạn dùng: ","Expiry: ")+(string.IsNullOrEmpty(row.expiryDate)?T("Không ghi nhận","Not recorded"):row.expiryDate),
                T("Tồn thực tế: ","On hand: ")+row.onHand+" · "+T("Đã giữ: ","Reserved: ")+row.reserved,
                T("Hư hỏng: ","Damaged: ")+row.damaged+" · "+T("Khả dụng: ","Available: ")+row.available+" "+row.unit},null,null);
        }
        void TaskDetail(OmsMobileApi.TaskRow row)
        {
            CloseDetail();string next=row.status=="ASSIGNED"?"ACCEPTED":row.status=="ACCEPTED"||row.status=="BLOCKED"?"IN_PROGRESS":row.status=="IN_PROGRESS"?"COMPLETED":null;
            detail=PrototypeDialog.Details(nav,T("Chi tiết nhiệm vụ","Task details"),row.task_code+" · "+row.title,new[]{
                T("Nhân viên: ","Employee: ")+row.employeeCode,T("Trạng thái: ","Status: ")+Status(row.status),
                T("Loại: ","Type: ")+row.task_type+" · "+T("Vị trí: ","Location: ")+row.locationCode,
                T("Hàng hóa: ","Product: ")+row.productName+" · "+row.sku,
                T("Số lượng yêu cầu: ","Expected quantity: ")+row.expected_quantity+" "+row.unit},T("Tiếp tục","Continue"),next==null?null:(Action)(()=>StartCoroutine(Transition(row,next))));
        }
`+s.slice(end);
fs.writeFileSync(file,s);
