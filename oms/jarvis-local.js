// Grounded local lookup. It never transmits warehouse records to a model provider.
const firstMatch=(text,pattern)=>text.match(pattern)?.[0]||'';
const source=(type,id)=>({type,id});
const available=(row)=>row.available;

async function answerLocal({message,user,oms,knowledge}) {
  const dataLabel=oms.synthetic?'Dữ liệu mẫu':'Dữ liệu kho';
  const question=message.trim();
  const lower=question.toLocaleLowerCase('vi-VN');
  const barcode=firstMatch(question,/\b\d{8,14}\b/);
  const lotCode=firstMatch(question,/\bLOT-[A-Z0-9-]+\b/i);
  const sku=firstMatch(question,/\bSKU-[A-Z0-9-]+\b/i);
  const orderCode=firstMatch(question,/\bORD-[A-Z0-9-]+\b/i);
  const returnCode=firstMatch(question,/\bRET-[A-Z0-9-]+\b/i);
  const taskCode=firstMatch(question,/\bTASK-[A-Z0-9-]+\b/i);
  const employeeCode=firstMatch(question,/\bNV-\d{5}\b/i);
  const code=barcode||lotCode||sku;

  if (code) {
    const rows=await oms.inventory(code);
    if (!rows.length) return {message:`Không tìm thấy sản phẩm hoặc lô tương ứng với “${code}” trong dữ liệu kho hiện tại.`,sources:[]};
    const product=rows[0];
    const total=rows.reduce((sum,row)=>sum+available(row),0);
    const details=rows.map(row=>`${row.lotCode}: ${row.available} ${row.unit} khả dụng tại ${row.locationCode}${row.expiryDate?`, hạn ${row.expiryDate}`:''}`).join('; ');
    return {
      message:`${dataLabel}: ${product.name} (SKU ${product.sku}, barcode ${product.barcode}) có tổng ${total} ${product.unit} khả dụng. ${details}.`,
      sources:rows.map(row=>source('InventoryLot',row.lotCode)).slice(0,10)
    };
  }
  if (orderCode) {
    if(user.role!=='MANAGER') return {message:'Tài khoản này không có quyền xem đơn hàng.',sources:[]};
    const order=await oms.order(orderCode);
    if(!order)return {message:`Không tìm thấy đơn ${orderCode}.`,sources:[]};
    return {message:`${dataLabel}: đơn ${order.order_code} đang ở trạng thái ${order.status}. Hàng: ${order.items.map(i=>`${i.name}, SKU ${i.sku}, barcode ${i.barcode}, số lượng ${i.quantity}`).join('; ')}.`,sources:[source('Order',order.order_code)]};
  }
  if (returnCode) {
    if(user.role!=='MANAGER') return {message:'Tài khoản này không có quyền xem hàng hoàn.',sources:[]};
    const value=await oms.returnDetail(returnCode);
    if(!value)return {message:`Không tìm thấy phiếu hoàn ${returnCode}.`,sources:[]};
    return {message:`${dataLabel}: phiếu hoàn ${value.return_code} từ đơn ${value.order_code}, trạng thái ${value.status}, lý do: ${value.reason}. Hàng chưa được tự động cộng tồn.`,sources:[source('Return',value.return_code)]};
  }
  if (taskCode) {
    const task=await oms.task(taskCode);
    if(!task || (user.role==='EMPLOYEE'&&task.employee_id!==user.employee_id))return {message:`Không tìm thấy nhiệm vụ ${taskCode} trong quyền của bạn.`,sources:[]};
    if (/(gợi ý|phù hợp|assign|suggest|ai làm)/i.test(lower)) {
      if(user.role!=='MANAGER')return {message:'Chỉ quản lý được xem gợi ý phân công.',sources:[]};
      const suggested=await oms.suggestEmployee(task.id);
      if(!suggested)return {message:'Hiện không có nhân viên Trực tuyến phù hợp để gợi ý.',sources:[source('Task',task.task_code)]};
      return {message:`${dataLabel}: gợi ý ${suggested.employeeCode} cho ${task.task_code}. Lý do: ${suggested.reason} Quản lý cần xác nhận trước khi giao.`,sources:[source('Task',task.task_code),source('Employee',suggested.employeeCode)]};
    }
    return {message:`${dataLabel}: ${task.task_code} — ${task.title}; trạng thái ${task.status}. Mã nhân viên hiện tại được xem ở trang Nhiệm vụ.`,sources:[source('Task',task.task_code)]};
  }
  if(employeeCode && user.role==='MANAGER') {
    const employee=(await oms.employees()).find(row=>row.employeeCode===employeeCode);
    if(!employee)return {message:`Không tìm thấy mã nhân viên ${employeeCode}.`,sources:[]};
    return {message:`${dataLabel}: ${employee.employeeCode} đang ${employee.status==='ONLINE'?'Trực tuyến':'Không trực tuyến'}, khu ${employee.warehouseZone||'chưa gán'}, ${employee.openTasks} nhiệm vụ đang mở.`,sources:[source('Employee',employee.employeeCode)]};
  }
  if(/(thiếu hàng|shortage|nhập kho)/i.test(lower) && user.role==='MANAGER') {
    const rows=(await oms.receivings()).filter(row=>row.status==='SHORTAGE');
    if(!rows.length)return {message:'Không có phiếu nhập trạng thái Thiếu hàng trong dữ liệu hiện tại.',sources:[]};
    return {message:`${dataLabel}: ${rows.map(row=>`${row.receiving_code}: dự kiến ${row.expected_quantity}, thực nhận ${row.received_quantity}, thiếu ${row.shortage_quantity}`).join('; ')}.`,sources:rows.map(row=>source('Receiving',row.receiving_code))};
  }
  if(/(bao nhiêu đơn|tổng quan|dashboard|hoàn hàng)/i.test(lower) && user.role==='MANAGER') {
    const d=await oms.dashboard();
    return {message:`${dataLabel}: ${d.newOrders} đơn mới, ${d.activeReturns} phiếu hoàn đang mở, ${d.openTasks} nhiệm vụ mở và ${d.availableUnits} đơn vị tồn khả dụng. Các số này đến từ database local lúc ${d.updatedAt}.`,sources:[source('Dashboard','current')]};
  }
  const sop=knowledge.forModel(question,'','vi').slice(0,2);
  if(sop.length) {
    return {message:`SOP được duyệt trong kho kiến thức: ${sop.map(row=>`${row.title}: ${String(row.text).slice(0,600)}`).join(' ')}`,sources:sop.map(row=>source('SOP',`${row.documentId}/${row.sectionId}`))};
  }
  return {message:'Tôi có thể tra barcode, SKU, mã lô, mã đơn ORD-, phiếu hoàn RET-, nhiệm vụ TASK- hoặc phiếu nhập thiếu hàng. Hãy cho tôi một mã cụ thể để truy vấn dữ liệu kho. Phần diễn giải bằng model AI hiện chưa được bật.',sources:[]};
}

module.exports={answerLocal};
