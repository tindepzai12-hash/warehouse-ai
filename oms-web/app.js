(() => {
  'use strict';
  const root = document.getElementById('app');
  const state = {
    user: null, csrf: '', dataMode: 'live', route: '', data: null, loading: false, error: '', flash: '',
    query: { orders:'', returns:'', inventory:'', products:'' },
    suggestion: {}, messages: [], aiMessages: [], aiConversationId: null, aiPending: false, menuOpen: false,
  };
  let chatPollPending=false;
  let chatWritePending=false;
  let chatRevision=0;
  const dict = {
    vi:{ dashboard:'Bảng điều hành',orders:'Đơn hàng',returns:'Hàng hoàn',inventory:'Tồn kho',receiving:'Nhập kho',tasks:'Nhiệm vụ',employees:'Nhân sự',cash:'Dòng tiền',jarvis:'JarVis AI',settings:'Cài đặt',products:'Hàng hóa',chat:'Chat với nhân viên',logout:'Đăng xuất',start:'Bắt đầu làm việc',newOrders:'Đơn mới',onTime:'Đúng hạn',returned:'Hoàn hàng',available:'Tồn khả dụng',loading:'Đang tải dữ liệu…' },
    en:{ dashboard:'Dashboard',orders:'Orders',returns:'Returns',inventory:'Inventory',receiving:'Receiving',tasks:'Tasks',employees:'Employees',cash:'Cash Flow',jarvis:'JarVis AI',settings:'Settings',products:'Products',chat:'Employee Chat',logout:'Sign out',start:'Start Working',newOrders:'New orders',onTime:'On time',returned:'Returns',available:'Available stock',loading:'Loading data…' }
  };
  const text=(key)=>dict[state.user?.language==='en'?'en':'vi'][key]||key;
  const escape=(value)=>String(value??'').replace(/[&<>"']/g,(ch)=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
  const route=()=>decodeURIComponent(location.hash.replace(/^#\/?/,'')||'dashboard');
  const label=(key)=>text(key);
  const canEdit=()=>state.user?.canEdit===true;
  const short=(value,max=180)=>String(value??'').slice(0,max);
  const status=(value)=>{
    const map={NEW:'Mới',ALLOCATED:'Đã giữ tồn',PICKING:'Đang nhặt',PACKING:'Đóng gói',SHIPPED:'Đã giao',CANCELLED:'Đã hủy',REQUESTED:'Yêu cầu hoàn',IN_TRANSIT:'Đang vận chuyển',RECEIVED:'Đã nhận',INSPECTING:'Đang kiểm tra',RESTOCKED:'Đã nhập lại',CLOSED:'Đã đóng',PENDING:'Chờ nhận',COMPLETE:'Đủ hàng',SHORTAGE:'Thiếu hàng',UNASSIGNED:'Chưa giao',ASSIGNED:'Đã giao',ACCEPTED:'Đã nhận',IN_PROGRESS:'Đang làm',BLOCKED:'Bị chặn',COMPLETED:'Hoàn tất',ONLINE:'Trực tuyến',OFFLINE:'Không trực tuyến'};
    const tone=['CANCELLED','BLOCKED'].includes(value)?'red':['SHORTAGE','REQUESTED','PENDING','UNASSIGNED'].includes(value)?'orange':['COMPLETE','SHIPPED','RESTOCKED','COMPLETED','ONLINE'].includes(value)?'green':['OFFLINE'].includes(value)?'gray':'';
    return `<span class="status ${tone}">${escape(map[value]||value)}</span>`;
  };
  const date=(value)=>value?new Date(value).toLocaleString('vi-VN',{dateStyle:'short',timeStyle:'short'}):'—';
  const empty=(title,detail)=>`<div class="empty"><strong>${escape(title)}</strong>${escape(detail)}</div>`;
  const notice=(message,tone='')=>`<div class="notice ${tone}">${escape(message)}</div>`;
  const heading=(eyebrow,title,subtitle='',actions='')=>`<div class="page-heading"><div><span class="eyebrow">${escape(eyebrow)}</span><h1>${escape(title)}</h1><p>${escape(subtitle)}</p></div><div class="page-heading-actions">${actions}</div></div>`;
  const panel=(title,body,subtitle='')=>`<section class="card"><h2>${escape(title)}</h2>${subtitle?`<p>${escape(subtitle)}</p>`:''}${body}</section>`;
  const table=(headers,rows)=>`<div class="table-wrap"><table class="table"><thead><tr>${headers.map(h=>`<th>${escape(h)}</th>`).join('')}</tr></thead><tbody>${rows.join('')}</tbody></table></div>`;
  const kpi=(title,value,note,href='')=>`<${href?'a':'div'} class="kpi ${href?'link':''}" ${href?`href="#/${href}"`:''}><span>${escape(title)}</span><strong>${escape(value)}</strong><small>${escape(note)}</small></${href?'a':'div'}>`;

  async function api(path,options={}) {
    const headers={...(options.headers||{})};
    if(options.body!==undefined){headers['Content-Type']='application/json';headers['X-CSRF-Token']=state.csrf;}
    const response=await fetch('/api/oms'+path,{credentials:'same-origin',...options,headers});
    let data;
    try{data=await response.json();}catch{data={detail:'Phản hồi máy chủ không hợp lệ'};}
    if(!response.ok){const error=new Error(data.detail||'Yêu cầu không thành công');error.status=response.status;throw error;}
    return data;
  }
  function loginView(){
    return `<div class="login-layout"><section class="login-visual"><div class="login-wordmark"><i></i>JARVIS WAREHOUSE</div><h1>Điều hành kho rõ ràng,<br>từ dữ liệu đáng tin.</h1><p>Đơn hàng, tồn kho, nhiệm vụ và JarVis AI trong cùng một hệ thống làm việc.</p><div class="login-bottom">Dữ liệu được lưu từ các thao tác vận hành đã xác nhận</div></section><main class="login-main"><div class="login-card"><span class="eyebrow">CHÀO MỪNG TRỞ LẠI</span><h2>Đăng nhập</h2><p>Dùng tài khoản đã được cấp quyền cho kho của bạn.</p>${state.error?notice(state.error,'error'):''}<form id="login-form" class="form-stack"><label class="field">Tên đăng nhập<input name="username" autocomplete="username" maxlength="80" required autofocus></label><label class="field">Mật khẩu<input name="password" type="password" autocomplete="current-password" required></label><button class="primary" type="submit">Đăng nhập →</button></form><div class="login-note">Chỉ quản trị viên được thay đổi dữ liệu kho. Tài khoản nhân viên xem dữ liệu và xử lý nhiệm vụ trong app.</div></div></main></div>`;
  }
  function navigation(){
    const manager=[
      ['dashboard','▦'],['orders','▤'],['returns','↩'],['products','◫'],['inventory','⬡'],['receiving','⇣'],
      ['tasks','☑'],['employees','♙'],['chat','◌'],['cash','◈'],['jarvis','✦'],['settings','⚙']
    ];
    const worker=[['tasks','☑'],['chat','◌'],['inventory','⬡'],['jarvis','✦'],['settings','⚙']];
    return (state.user.role==='EMPLOYEE'?worker:manager).map(([key,icon])=>`<a href="#/${key}" class="${state.route.split('/')[0]===key?'active':''}"><span class="nav-icon" aria-hidden="true">${icon}</span><span>${escape(label(key))}</span></a>`).join('');
  }
  function shell(content){
    return `<div class="shell ${state.menuOpen?'menu-open':''}"><div class="drawer-backdrop" data-action="close-menu"></div><aside class="sidebar"><div class="wordmark"><i></i>JARVIS WAREHOUSE</div><div class="side-label">ĐIỀU HÀNH</div><nav class="nav" aria-label="Điều hướng chính">${navigation()}</nav><div class="sidebar-foot"><strong>${escape(state.user.username)}</strong><span>${canEdit()?'Quản trị viên':'Chỉ xem'}</span><button data-action="logout">${escape(text('logout'))}</button></div></aside><div class="main-column"><header class="topbar"><div class="topbar-left"><button class="menu-toggle" data-action="menu" aria-label="Mở điều hướng">☰</button><strong>JARVIS WAREHOUSE</strong></div><div class="topbar-right"><span class="badge ${state.dataMode==='sample'?'synthetic':''}">${state.dataMode==='sample'?'DỮ LIỆU MẪU':'DỮ LIỆU VẬN HÀNH'}</span><span class="topbar-user">${escape(state.user.username)}</span><a class="ghost" href="#/jarvis">✦ JarVis</a></div></header><main class="page" id="main-content">${state.flash?`<div class="flash">${notice(state.flash)}</div>`:''}${state.error?`<div class="flash">${notice(state.error,'error')}</div>`:''}${content}</main></div></div>`;
  }
  function dashboard(){
    const d=state.data?.summary||{};
    const orders=state.data?.orders||[];
    const tasks=state.data?.tasks||[];
    const orb=`<div class="core-stage" aria-hidden="true"><div class="core-halo"></div><div class="core-orb"></div><div class="core-status"><i></i>TRA CỨU KHO SẴN SÀNG</div></div>`;
    const rows=orders.slice(0,4).map(o=>`<tr><td><a href="#/orders/${encodeURIComponent(o.order_code)}"><strong>${escape(o.order_code)}</strong></a><small>${escape(o.customer_reference)}</small></td><td>${o.items.map(i=>escape(i.name)).join(', ')}</td><td>${status(o.status)}</td></tr>`);
    const taskRows=tasks.slice(0,4).map(t=>`<div class="task-summary-row"><div><strong>${escape(t.task_code)}</strong><small>${escape(t.title)}</small></div><span>${escape(t.employeeCode||'Chưa giao')}</span>${status(t.status)}</div>`);
    return `${heading('BẢNG ĐIỀU HÀNH',`Chào buổi sáng, ${state.user.username}`,'Dữ liệu kho và công việc đang cần bạn xử lý.')}
      <section class="hero"><div><small>TRUNG TÂM VẬN HÀNH</small><h2>Hôm nay, bắt đầu từ việc quan trọng nhất.</h2><p>JarVis hiện tra cứu dữ liệu kho và SOP nội bộ. Kết nối model AI cho dữ liệu vận hành chưa được bật.</p><a class="primary" href="#/tasks">${escape(text('start'))} →</a></div>${orb}</section>
      <section class="kpis">${kpi(text('newOrders'),d.newOrders??0,'Đơn chờ xử lý','orders')}${kpi('Nhiệm vụ mở',d.openTasks??0,'Cần theo dõi','tasks')}${kpi(text('onTime'),d.onTimeRate??0,d.onTimeRate==null?'Chưa đủ dữ liệu hạn':'Từ nhiệm vụ có hạn')}${kpi(text('returned'),d.activeReturns??0,'Hàng hoàn đang mở','returns')}${kpi(text('available'),d.availableUnits??0,'Đơn vị sau khi trừ giữ/hỏng','inventory')}</section>
      <div class="grid-2"><section class="card"><div class="card-head"><h2>Đơn cần theo dõi</h2><a class="ghost" href="#/orders">Xem tất cả →</a></div>${rows.length?table(['Đơn','Sản phẩm','Trạng thái'],rows):empty('Chưa có đơn','Đơn mới sẽ xuất hiện tại đây.')}</section><section class="card"><div class="card-head"><h2>Nhiệm vụ hôm nay</h2><a class="ghost" href="#/tasks">Mở hàng việc →</a></div>${taskRows.length?`<div class="task-summary-list">${taskRows.join('')}</div>`:empty('Chưa có nhiệm vụ','Tạo nhiệm vụ hoặc nhận đơn nhập/xuất.')}</section></div>`;
  }
  function orders(){
    const rows=(state.data||[]).map(o=>`<tr><td><a href="#/orders/${encodeURIComponent(o.order_code)}"><strong>${escape(o.order_code)}</strong></a><small>${escape(o.customer_reference)}</small></td><td>${o.items.map(i=>`<strong>${escape(i.name)}</strong><small>SKU: ${escape(i.sku)} · Barcode: ${escape(i.barcode)} · SL: ${i.quantity}</small>`).join('<br>')}</td><td>${status(o.status)}</td><td>${date(o.created_at)}</td></tr>`);
    return `${heading('ĐƠN HÀNG','Đơn hàng','Mã barcode trên từng sản phẩm được lấy từ danh mục trong database.',`<a class="secondary" href="#/returns">Hàng hoàn →</a>`)}
      <div class="tabs"><a class="active" href="#/orders">Tất cả đơn</a><a href="#/returns">Hàng hoàn</a></div>
      ${panel('Danh sách đơn',`<form class="search-form" id="orders-search"><label class="field">Tìm mã đơn hoặc mã tham chiếu<input name="q" value="${escape(state.query.orders)}" placeholder="ORD-00125"></label><button class="secondary" type="submit">Tìm</button></form>${rows.length?table(['Mã đơn / nguồn','Sản phẩm · SKU · barcode · số lượng','Trạng thái','Tạo lúc'],rows):empty('Không tìm thấy đơn','Thử mã đơn hoặc mã tham chiếu khác.')}`,'Trạng thái đơn theo quy trình xuất; hàng hoàn được quản lý ở tab riêng.')}`;
  }
  function orderDetail(){
    const o=state.data;
    if(!o)return empty('Không tìm thấy đơn','Kiểm tra lại mã đơn.');
    return `${heading('CHI TIẾT ĐƠN',o.order_code,o.customer_reference,`<a class="secondary" href="#/orders">← Danh sách đơn</a>`)}
      <div class="grid-2"><section class="card"><h2>Sản phẩm trong đơn</h2>${table(['Tên hàng','SKU','Barcode','Số lượng'],o.items.map(i=>`<tr><td><strong>${escape(i.name)}</strong></td><td>${escape(i.sku)}</td><td>${escape(i.barcode)}</td><td>${i.quantity}</td></tr>`))}</section>
      ${panel('Thông tin',`<div class="detail-list"><div class="detail-row"><span>Trạng thái</span>${status(o.status)}</div><div class="detail-row"><span>Mã tham chiếu</span><strong>${escape(o.customer_reference)}</strong></div><div class="detail-row"><span>Thời gian tạo</span><strong>${date(o.created_at)}</strong></div></div>`)}</div>`;
  }
  function returns(){
    const rows=(state.data?.returns||[]).map(r=>`<tr><td><a href="#/returns/${encodeURIComponent(r.return_code)}"><strong>${escape(r.return_code)}</strong></a><small>${escape(r.order_code)}</small></td><td>${escape(r.reason)}</td><td>${r.item_quantity}</td><td>${status(r.status)}</td><td>${date(r.created_at)}</td></tr>`);
    return `${heading('ĐƠN HÀNG / HÀNG HOÀN','Hàng hoàn','Kiểm tra hàng trước khi quyết định có nhập lại kho hay không.',`<a class="secondary" href="#/orders">← Đơn hàng</a>`)}
      ${canEdit()?`<section class="card" style="margin-bottom:17px"><h2>Tạo yêu cầu hàng hoàn</h2><p>Chỉ tạo phiếu hoàn cho đơn đã giao. Nhận, kiểm tra và nhập lại kho là các bước riêng.</p><form id="return-create" class="form-stack"><label class="field">Đơn và sản phẩm<select name="orderItem" required><option value="">Chọn đơn đã giao / sản phẩm</option>${(state.data?.orders||[]).filter(o=>o.status==='SHIPPED').flatMap(o=>o.items.map(i=>`<option value="${escape(o.id)}|${escape(i.orderItemId)}|${i.quantity}">${escape(o.order_code)} · ${escape(i.sku)} · tối đa ${i.quantity}</option>`)).join('')}</select></label><div class="form-grid"><label class="field">Số lượng hoàn<input name="quantity" type="number" min="1" required></label><label class="field">Lý do<input name="reason" maxlength="200" required placeholder="Ví dụ: kiện bị trả lại"></label></div><button class="primary" type="submit">Tạo phiếu hoàn</button></form></section>`:''}
      ${panel('Danh sách hàng hoàn',`<form class="search-form" id="returns-search"><label class="field">Tìm mã hoàn, mã đơn hoặc barcode<input name="q" value="${escape(state.query.returns)}" placeholder="RET-00125"></label><button class="secondary" type="submit">Tìm</button></form>${rows.length?table(['Mã hoàn / đơn','Lý do','Số lượng','Trạng thái','Tạo lúc'],rows):empty('Chưa có hàng hoàn','Mã hoàn hoặc barcode không có trong phạm vi này.')}`,'Nhận hàng hoàn không tự cộng tồn; cần kiểm tra tình trạng và xác nhận nhập lại.')}`;
  }
  function returnDetail(){
    const r=state.data;
    if(!r)return empty('Không tìm thấy hàng hoàn','Kiểm tra mã hoàn.');
    return `${heading('CHI TIẾT HÀNG HOÀN',r.return_code,`Đơn nguồn ${r.order_code}`,`<a class="secondary" href="#/returns">← Hàng hoàn</a>`)}
      <div class="grid-2">${panel('Sản phẩm hoàn',table(['Sản phẩm','SKU','Barcode','Lô','SL','Tình trạng'],r.items.map(i=>`<tr><td>${escape(i.name)}</td><td>${escape(i.sku)}</td><td>${escape(i.barcode)}</td><td>${escape(i.lot_code||'—')}</td><td>${i.quantity}</td><td>${escape(i.condition)}</td></tr>`)))}${panel('Xử lý',`<div class="detail-list"><div class="detail-row"><span>Trạng thái</span>${status(r.status)}</div><div class="detail-row"><span>Lý do</span><strong>${escape(r.reason)}</strong></div><div class="detail-row"><span>Nhận lúc</span><strong>${date(r.received_at)}</strong></div></div>${canEdit()&&['REQUESTED','IN_TRANSIT','RECEIVED'].includes(r.status)?`<div class="row-actions" style="margin:15px 0"><button data-action="return-status" data-code="${escape(r.return_code)}" data-status="${r.status==='RECEIVED'?'INSPECTING':'RECEIVED'}">${r.status==='RECEIVED'?'Bắt đầu kiểm tra':'Xác nhận đã nhận'}</button></div>`:''}${canEdit()&&r.status==='INSPECTING'&&r.items.some(i=>!i.disposition)?`<form id="return-restock" class="form-stack" data-code="${escape(r.return_code)}"><label class="field">Sản phẩm hoàn<select name="returnItemId" required>${r.items.filter(i=>!i.disposition).map(i=>`<option value="${escape(i.id)}" data-product-id="${escape(i.product_id)}">${escape(i.sku)} · SL ${i.quantity}</option>`).join('')}</select></label><label class="field">Lô nhập lại<select name="lotId" required><option value="">Chọn lô</option>${(state.data?.lots||[]).map(l=>`<option value="${escape(l.id)}" data-product-id="${escape(l.productId)}">${escape(l.sku)} · ${escape(l.lotCode)}</option>`).join('')}</select></label><label class="field">Vị trí<select name="locationId" required><option value="">Chọn ô kệ</option>${(state.data?.locations||[]).map(l=>`<option value="${escape(l.id)}">${escape(l.code)}</option>`).join('')}</select></label><label class="field">Số lượng đủ điều kiện<input name="quantity" type="number" min="1" max="${r.items[0]?.quantity||1}" required></label><button class="primary" type="submit">Xác nhận nhập lại kho</button></form>`:''}${notice('Hàng hoàn chỉ được cộng tồn sau kiểm tra và xác nhận nghiệp vụ.', 'warn')}`)}</div>`;
  }
  function inventory(){
    const rows=state.data||[];
    const groups=new Map();
    for(const row of rows){const key=row.productId;if(!groups.has(key))groups.set(key,{product:row,lots:[]});groups.get(key).lots.push(row);}
    const cards=[...groups.values()].map(({product,lots})=>`<section class="lot-group"><h3>${escape(product.name)} <span class="chip">${escape(product.sku)}</span></h3><p>Barcode: <strong>${escape(product.barcode)}</strong> · Tổng khả dụng: <strong>${lots.reduce((n,l)=>n+l.available,0)}</strong> ${escape(product.unit)}</p>${table(['Lô','Vị trí','Nhận ngày','Hạn dùng','Tồn thực','Đã giữ','Khả dụng'],lots.map(l=>`<tr><td><strong>${escape(l.lotCode)}</strong></td><td>${escape(l.locationCode)}</td><td>${escape(l.receivedAt||'—')}</td><td>${escape(l.expiryDate||'—')}</td><td>${l.onHand}</td><td>${l.reserved}</td><td><strong>${l.available}</strong></td></tr>`))}</section>`).join('');
    return `${heading('TRA CỨU KHO','Tồn kho','Barcode xác định sản phẩm; một sản phẩm có thể có nhiều lô và vị trí.')}
      ${panel('Tìm hàng hóa, lô hoặc ô kệ',`<form class="search-form" id="inventory-search"><label class="field">Barcode, SKU, tên, mã lô hoặc vị trí<input name="q" value="${escape(state.query.inventory)}" placeholder="8938500000012 hoặc LOT-A2402"></label><button class="primary" type="submit">Tra cứu</button></form>${cards||empty(state.query.inventory?'Không tìm thấy sản phẩm hoặc lô này':'Chưa có tồn kho','Kiểm tra lại barcode, SKU, mã lô hoặc vị trí.')}<p class="list-note">Thứ tự lô được sắp theo hạn dùng (FEFO), sau đó ngày nhận (FIFO). Đây là gợi ý, không tự thay đổi đơn.</p>`)}`;
  }
  function products(){
    const rows=(state.data||[]).map(p=>`<tr><td><strong>${escape(p.sku)}</strong></td><td>${escape(p.name)}</td><td>${escape(p.barcode)}</td><td>${escape(p.unit)}</td><td>${p.lotTrackingRequired?'Có':'Không'}</td></tr>`);
    return `${heading('DANH MỤC','Hàng hóa','SKU và barcode là dữ liệu gốc để nhận, quét và tra cứu hàng.')}
      ${canEdit()?`<section class="card" style="margin-bottom:17px"><h2>Tạo sản phẩm</h2><p>Danh mục mới chưa làm tăng tồn kho; số lượng chỉ tăng sau khi xác nhận nhập.</p><form id="product-create" class="form-stack"><div class="form-grid"><label class="field">SKU<input name="sku" maxlength="64" required placeholder="SKU-D004"></label><label class="field">Barcode<input name="barcode" maxlength="64" required placeholder="8938500000043"></label></div><div class="form-grid"><label class="field">Tên sản phẩm<input name="name" maxlength="180" required></label><label class="field">Đơn vị<input name="unit" maxlength="24" required placeholder="hộp, chai, gói"></label></div><button class="primary" type="submit">Lưu sản phẩm</button></form></section>`:''}
      ${panel('Sản phẩm',`<form class="search-form" id="products-search"><label class="field">Tìm SKU, tên hoặc barcode<input name="q" value="${escape(state.query.products)}" placeholder="SKU-A001"></label><button class="secondary" type="submit">Tìm</button></form>${rows.length?table(['SKU','Tên hàng','Barcode','Đơn vị','Theo dõi lô'],rows):empty('Chưa có sản phẩm','Thử mã SKU hoặc barcode khác.')}`)}`;
  }
  function receiving(){
    const rows=(state.data?.receivings||[]).map(r=>`<tr><td><strong>${escape(r.receiving_code)}</strong><small>${escape(r.name)}</small></td><td>${escape(r.barcode)}<small>${escape(r.lotCode)} · ${escape(r.locationCode)}</small></td><td>${r.expected_quantity}</td><td>${r.received_quantity}</td><td>${r.shortage_quantity}</td><td>${status(r.status)}</td><td>${r.status==='PENDING' && canEdit()?`<form class="row-actions receiving-form" data-id="${escape(r.id)}"><label class="field small">Thực nhận<input name="quantity" type="number" min="0" max="100000" required value="${r.expected_quantity}" style="width:90px"></label><button type="submit">Xác nhận</button></form>`:''}</td></tr>`);
    return `${heading('NHẬP KHO','Tiếp nhận hàng','Nhập theo thứ tự: hàng hóa → vị trí → lô → phiếu nhập → xác nhận thực nhận.')}
      ${canEdit()?`<section class="card" style="margin-bottom:17px"><h2>Tạo vị trí kho</h2><p>Mỗi ô kệ cần mã duy nhất trước khi lập phiếu nhập. Tạo sản phẩm ở mục Hàng hóa trước.</p><form id="location-create" class="form-stack"><div class="form-grid"><label class="field">Mã vị trí<input name="code" maxlength="40" pattern="[A-Za-z0-9-]{2,40}" required placeholder="A-01"></label><label class="field">Khu vực<input name="zone" maxlength="30" required placeholder="A"></label></div><button class="primary" type="submit">Lưu vị trí</button></form></section>`:''}
      ${canEdit()?`<div class="grid-2 equal" style="margin-bottom:17px"><section class="card"><h2>Tạo lô sản phẩm</h2><form id="lot-create" class="form-stack"><label class="field">Sản phẩm<select name="productId" required><option value="">Chọn sản phẩm</option>${(state.data?.products||[]).map(p=>`<option value="${escape(p.id)}">${escape(p.sku)} · ${escape(p.name)}</option>`).join('')}</select></label><div class="form-grid"><label class="field">Mã lô<input name="lotCode" required maxlength="64" placeholder="LOT-D2401"></label><label class="field">Ngày nhận<input name="receivedAt" type="date" required value="${new Date().toISOString().slice(0,10)}"></label></div><label class="field">Hạn sử dụng (nếu có)<input name="expiryDate" type="date"></label><button class="primary" type="submit">Lưu lô</button></form></section><section class="card"><h2>Tạo phiếu nhập</h2><form id="receiving-create" class="form-stack"><label class="field">Lô đã khai báo<select name="lotId" required><option value="">Chọn lô</option>${(state.data?.lots||[]).map(l=>`<option value="${escape(l.id)}" data-product-id="${escape(l.productId)}">${escape(l.sku)} · ${escape(l.lotCode)}</option>`).join('')}</select></label><div class="form-grid"><label class="field">Vị trí nhập<select name="locationId" required><option value="">Chọn vị trí</option>${(state.data?.locations||[]).map(l=>`<option value="${escape(l.id)}">${escape(l.code)}</option>`).join('')}</select></label><label class="field">Số dự kiến<input name="expectedQuantity" type="number" min="0" required></label></div><button class="primary" type="submit">Tạo phiếu nhận</button></form></section></div>`:''}
      ${panel('Phiếu nhận',rows.length?table(['Phiếu / hàng','Barcode · lô · vị trí','Dự kiến','Thực nhận','Thiếu','Trạng thái','Xử lý'],rows):empty('Chưa có phiếu nhập','Phiếu mới sẽ xuất hiện tại đây.'),'Số thực nhận vượt dự kiến cần quy trình duyệt riêng. Gửi lại cùng requestId không cộng tồn hai lần.')}`;
  }
  function tasks(){
    const rows=(state.data?.tasks||[]).map(t=>{
      const suggestion=state.suggestion[t.id];
      const manager=canEdit();
      const next=t.status==='ASSIGNED'?'ACCEPTED':t.status==='ACCEPTED'?'IN_PROGRESS':t.status==='IN_PROGRESS'?'COMPLETED':t.status==='BLOCKED'?'IN_PROGRESS':null;
      return `<tr><td><strong>${escape(t.task_code)}</strong><small>${escape(t.title)}</small></td><td>${escape(t.employeeCode||'—')}</td><td>${status(t.status)}</td><td>${escape(t.sku||'—')}<small>${escape(t.locationCode||'—')}</small></td><td><div class="row-actions">${manager&&t.status==='UNASSIGNED'?`<button data-action="suggest" data-id="${escape(t.id)}">Gợi ý nhân viên</button>`:''}${manager&&suggestion?`<div class="suggestion"><strong>${escape(suggestion.employeeCode)}</strong><p>${escape(suggestion.reason)}</p><button data-action="assign" data-id="${escape(t.id)}" data-code="${escape(suggestion.employeeCode)}">Giao việc</button></div>`:''}${manager&&next?`<button data-action="task-status" data-id="${escape(t.id)}" data-status="${next}">${escape({ACCEPTED:'Nhận',IN_PROGRESS:'Bắt đầu',COMPLETED:'Hoàn tất'}[next]||'Tiếp tục')}</button>`:''}</div></td></tr>`;
    });
    const options=(state.data?.products||[]).map(p=>`<option value="${escape(p.id)}">${escape(p.sku)} · ${escape(p.name)}</option>`).join('');
    const locations=(state.data?.locations||[]).map(l=>`<option value="${escape(l.id)}">${escape(l.code)}</option>`).join('');
    const create=canEdit()?`<section class="card"><h2>Tạo nhiệm vụ từ mẫu</h2><p>Chọn loại việc, sản phẩm, vị trí và số lượng trước khi gợi ý người làm.</p><form id="task-create" class="form-stack"><div class="form-grid"><label class="field">Loại việc<select name="taskType" required><option value="RECEIVING">Kiểm đếm nhập</option><option value="PUTAWAY">Lên kệ</option><option value="PICKING">Nhặt hàng</option><option value="PACKING">Đóng gói</option><option value="COUNTING">Kiểm kê</option><option value="EXPIRY_CHECK">Kiểm tra hạn</option></select></label><label class="field">Ưu tiên<select name="priority"><option value="NORMAL">Bình thường</option><option value="HIGH">Cao</option><option value="LOW">Thấp</option></select></label></div><label class="field">Nội dung nhiệm vụ<input name="title" maxlength="180" required placeholder="Ví dụ: Kiểm đếm lô sữa tại cửa A"></label><div class="form-grid"><label class="field">Sản phẩm<select name="productId"><option value="">Không áp dụng</option>${options}</select></label><label class="field">Vị trí<select name="locationId"><option value="">Không áp dụng</option>${locations}</select></label></div><div class="form-grid"><label class="field">Số lượng dự kiến<input name="expectedQuantity" type="number" min="0" placeholder="Không áp dụng"></label><label class="field">Mã phiếu nguồn<input name="sourceId" placeholder="ASN hoặc mã đơn"></label></div><button class="primary" type="submit">Tạo nhiệm vụ</button></form></section>`:'';
    return `${heading('HÀNG VIỆC',text('tasks'),'Đề xuất nhân viên hiển thị bằng mã; không có nhân viên dự phòng.')}${create}<section class="card" style="margin-top:17px"><h2>Danh sách nhiệm vụ</h2>${rows.length?table(['Mã / nội dung','Mã nhân viên','Trạng thái','SKU / vị trí','Hành động'],rows):empty('Chưa có nhiệm vụ','Quản lý tạo nhiệm vụ để nhân viên nhận trong app.')}</section>`;
  }
  function employees(){
    const rows=(state.data||[]).map(e=>`<tr><td><strong>${escape(e.employeeCode)}</strong><small>${escape(e.name||'')}</small></td><td>${escape(e.role)}</td><td>${escape(e.warehouseZone||'—')}</td><td>${status(e.status)}</td><td>${e.openTasks}</td><td><div class="row-actions"><a href="#/chat/${encodeURIComponent(e.employeeCode)}">Chat</a></div></td></tr>`);
    return `${heading('NHÂN SỰ','Nhân viên','Tài khoản mới chỉ xem trên web; nhân viên xử lý nhiệm vụ trong app.')}
      ${canEdit()?`<section class="card" style="margin-bottom:17px"><h2>Thêm nhân viên</h2><p>Mã nhân viên dùng để đăng nhập app. Trạng thái ban đầu là Ngoại tuyến.</p><form id="employee-create" class="form-stack"><div class="form-grid"><label class="field">Mã nhân viên<input name="username" required pattern="NV-[0-9]{5}" placeholder="NV-00001"></label><label class="field">Họ tên<input name="displayName" maxlength="120" required></label></div><div class="form-grid"><label class="field">Công việc chính<select name="workRole" required><option value="RECEIVING">Nhập kho</option><option value="PUTAWAY">Lên kệ</option><option value="PICKING">Nhặt hàng</option><option value="PACKING">Đóng gói</option><option value="COUNTING">Kiểm kê</option><option value="EXPIRY_CHECK">Kiểm hạn</option></select></label><label class="field">Khu vực<input name="warehouseZone" maxlength="30" placeholder="A"></label></div><label class="field">Mật khẩu tạm (ít nhất 12 ký tự)<input name="password" type="password" autocomplete="new-password" minlength="12" required></label><button class="primary" type="submit">Tạo tài khoản nhân viên</button></form></section>`:''}
      ${panel('Đội ngũ kho',rows.length?table(['Mã nhân viên','Vai trò','Khu vực','Trạng thái','Việc mở','Liên hệ'],rows):empty('Chưa có nhân viên','Thêm tài khoản nhân viên trước khi giao việc.'),'Mã nhân viên là định danh chính trong gợi ý và chat.')}`;
  }
  function chatLinks(convs,chosen){
    return convs.map(c=>`<a href="#/chat/${encodeURIComponent(c.employeeCode)}" class="${chosen?.id===c.id?'active':''}"><strong>${escape(c.employeeCode)}</strong><small>${escape(c.latestMessage||'Chưa có tin nhắn')} · ${escape(c.status)}</small></a>`).join('')||empty('Chưa có hội thoại','Mở trang Nhân sự để bắt đầu.');
  }
  function chatMessages(messages){
    return messages.length?messages.map(m=>`<div class="message ${m.senderId===state.user.id?'mine':''}"><small>${escape(m.sender)} · ${date(m.createdAt)}</small>${escape(m.content)}</div>`).join(''):empty('Chưa có tin nhắn','Bắt đầu trao đổi về một nhiệm vụ.');
  }
  function chat(){
    const convs=state.data?.conversations||[];
    const chosen=state.data?.selected;
    const messages=state.data?.messages||[];
    const thread=chosen?`<div class="chat-thread"><div class="chat-thread-head">${escape(chosen.employeeCode)} · ${status(chosen.status)}</div><div class="messages" aria-live="polite">${chatMessages(messages)}</div>${canEdit()?`<form id="message-form" data-id="${escape(chosen.id)}" class="chat-compose"><input name="content" maxlength="2000" required placeholder="Nhập tin nhắn cho ${escape(chosen.employeeCode)}" aria-label="Tin nhắn"><button class="primary" type="submit">Gửi</button></form>`:''}</div>`:`<div class="chat-thread"><div class="messages">${empty('Chọn nhân viên','Chọn hội thoại bên trái hoặc mở từ trang Nhân sự.')}</div></div>`;
    return `${heading('TRAO ĐỔI CÔNG VIỆC',text('chat'),'Tin nhắn được lưu trong database và gắn với mã nhân viên.')}<section class="chat-layout"><div class="chat-list"><h2>Hội thoại</h2><div class="chat-links">${chatLinks(convs,chosen)}</div></div>${thread}</section>`;
  }
  async function pollChat(){
    if(!state.user||state.loading||state.route.split('/')[0]!=='chat'||document.hidden||chatPollPending||chatWritePending)return;
    const expectedRoute=state.route;
    const expectedRevision=chatRevision;
    const old=state.data;
    chatPollPending=true;
    try{
      const conversations=await api('/conversations');
      const selected=old?.selected?conversations.find(c=>c.id===old.selected.id)||old.selected:null;
      const messages=selected?await api(`/conversations/${encodeURIComponent(selected.id)}/messages`):[];
      if(state.route!==expectedRoute||state.loading||chatWritePending||chatRevision!==expectedRevision||!root.querySelector('.chat-layout'))return;
      state.data={...old,conversations,selected,messages};
      if(JSON.stringify(conversations)!==JSON.stringify(old?.conversations))
        root.querySelector('.chat-links').innerHTML=chatLinks(conversations,selected);
      if(selected&&old?.selected?.status!==selected.status)
        root.querySelector('.chat-thread-head').innerHTML=`${escape(selected.employeeCode)} · ${status(selected.status)}`;
      if(selected&&messages.map(m=>m.id).join('|')!==(old?.messages||[]).map(m=>m.id).join('|')){
        const box=root.querySelector('.chat-thread .messages');
        const atBottom=box.scrollHeight-box.scrollTop-box.clientHeight<32;
        const previousScroll=box.scrollTop;
        box.innerHTML=chatMessages(messages);
        box.scrollTop=atBottom?box.scrollHeight:previousScroll;
      }
    }catch(error){
      if(error.status===401){state.user=null;state.csrf='';state.error='Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';render();}
    }finally{chatPollPending=false;}
  }
  function settings(){
    const u=state.user;
    return `${heading('HỒ SƠ & NGÔN NGỮ',text('settings'),'Ngôn ngữ được lưu trên tài khoản; vai trò không thể tự sửa.')}<div class="grid-2 equal">${panel('Thông tin người dùng',`<div class="detail-list"><div class="detail-row"><span>Tài khoản</span><strong>${escape(u.username)}</strong></div><div class="detail-row"><span>Tên hiển thị</span><strong>${escape(u.displayName)}</strong></div><div class="detail-row"><span>Mã nhân viên</span><strong>${escape(u.employeeId||'—')}</strong></div><div class="detail-row"><span>Vai trò</span><strong>${escape(u.role)}</strong></div></div>`)}${panel('Ngôn ngữ',`<form id="language-form" class="form-stack"><label class="field">Ngôn ngữ giao diện<select name="language"><option value="vi" ${u.language==='vi'?'selected':''}>Tiếng Việt</option><option value="en" ${u.language==='en'?'selected':''}>English</option></select></label><button class="primary" type="submit">Lưu ngôn ngữ</button></form>`,'Các nhãn điều hướng chính dùng từ điển vi/en.')}</div>${canEdit()?`<section class="card" style="margin-top:17px"><h2>Tạo tài khoản chỉ xem</h2><p>Tài khoản này xem bảng điều hành và dữ liệu kho, không thể tạo hoặc sửa dữ liệu.</p><form id="viewer-create" class="form-stack"><div class="form-grid"><label class="field">Tên đăng nhập<input name="username" pattern="[A-Za-z0-9._-]{3,80}" maxlength="80" required></label><label class="field">Tên hiển thị<input name="displayName" maxlength="120" required></label></div><label class="field">Mật khẩu tạm (ít nhất 12 ký tự)<input name="password" type="password" autocomplete="new-password" minlength="12" required></label><button class="primary" type="submit">Tạo tài khoản chỉ xem</button></form></section>`:''}`;
  }
  function finance(){
    return `${heading('TÀI CHÍNH',text('cash'),'Phần tiền cần nguồn chứng từ riêng để không nhầm doanh số sàn với tiền đã thu.')}
      ${panel('Chưa có sổ tài chính vận hành',empty('Chưa có giao dịch được xác minh','Dòng tiền trong mockup trước vẫn là bản thiết kế. Khi có biểu phí và chứng từ, module này sẽ đọc ledger thật; hiện không hiển thị con số giả.'),'Không thay đổi nghiệp vụ tài chính hiện có trong giai đoạn nối dữ liệu kho.')}`;
  }
  function jarvis(){
    const messages=state.aiMessages.map(m=>`<div class="message ${m.role==='user'?'mine':''}"><small>${m.role==='user'?escape(state.user.username):'JarVis AI'}</small>${escape(m.text)}${m.sources?.length?`<div class="source-list">${m.sources.map(s=>`<span>${escape(s.type)} · ${escape(s.id)}</span>`).join('')}</div>`:''}</div>`).join('');
    return `${heading('TRỢ LÝ KHO',text('jarvis'),'Hỏi về barcode, lô, đơn hàng, hoàn hàng hoặc nhiệm vụ. Câu trả lời lấy từ database và có nguồn.')}
      ${panel('JarVis · Tra cứu nội bộ',`${notice('Chế độ hiện tại: tra cứu dữ liệu/SOP trong backend. Dữ liệu kho chưa được gửi tới model OpenAI.')}<div class="ai-chat"><div class="messages">${messages||empty('Bắt đầu hội thoại','Ví dụ: Barcode 8938500000012 có những lô nào?')}${state.aiPending?'<div class="message">JarVis đang truy vấn dữ liệu…</div>':''}</div><form id="ai-form" class="chat-compose"><input name="message" maxlength="1000" required placeholder="Hỏi JarVis về dữ liệu kho…" aria-label="Câu hỏi cho JarVis"><button class="primary" type="submit" ${state.aiPending?'disabled':''}>Gửi</button></form></div>`,'Phần diễn giải bằng model AI chỉ bật sau khi phạm vi dữ liệu được phép gửi được xác nhận.')}`;
  }
  function loadingView(){return `${heading('JARVIS WAREHOUSE',label(state.route.split('/')[0])||'Trang','')}${empty(text('loading'),'Vui lòng chờ trong giây lát.')}`;}
  function content(){
    if(state.loading)return loadingView();
    const [base,detail]=state.route.split('/');
    switch(base){
      case 'dashboard':return dashboard();
      case 'orders':return detail?orderDetail():orders();
      case 'returns':return detail?returnDetail():returns();
      case 'inventory':return inventory();
      case 'products':return products();
      case 'receiving':return receiving();
      case 'tasks':return tasks();
      case 'employees':return employees();
      case 'chat':return chat();
      case 'settings':return settings();
      case 'cash':return finance();
      case 'jarvis':return jarvis();
      default:return empty('Không tìm thấy trang','Chọn một mục trong điều hướng.');
    }
  }
  function render(){
    root.innerHTML=state.user?shell(content()):loginView();
    const core=root.querySelector('.core-stage');
    if(core && !matchMedia('(prefers-reduced-motion: reduce)').matches && matchMedia('(pointer:fine)').matches){
      let frame=0;
      core.addEventListener('pointermove',(event)=>{
        const box=core.getBoundingClientRect();
        const x=Math.max(-1,Math.min(1,(event.clientX-box.left-box.width/2)/(box.width/2)));
        const y=Math.max(-1,Math.min(1,(event.clientY-box.top-box.height/2)/(box.height/2)));
        cancelAnimationFrame(frame);
        frame=requestAnimationFrame(()=>{core.style.setProperty('--mx',`${(x*5).toFixed(1)}px`);core.style.setProperty('--my',`${(y*5).toFixed(1)}px`);core.style.setProperty('--rx',`${(-y*3).toFixed(1)}deg`);core.style.setProperty('--ry',`${(x*3).toFixed(1)}deg`);});
      });
      core.addEventListener('pointerleave',()=>{cancelAnimationFrame(frame);for(const key of ['--mx','--my'])core.style.setProperty(key,'0px');for(const key of ['--rx','--ry'])core.style.setProperty(key,'0deg');});
    }
  }
  async function loadRoute(){
    if(!state.user)return render();
    const next=route();
    state.route=next;
    state.error='';
    state.menuOpen=false;
    state.loading=true;
    render();
    try{
      const [base,detail]=next.split('/');
      let data=null;
      if(base==='dashboard'){
        const [summary,orders,tasks]=await Promise.all([api('/dashboard'),api('/orders'),api('/tasks')]);
        data={summary,orders,tasks};
      }else if(base==='orders')data=await api(detail?`/orders/${encodeURIComponent(detail)}`:`/orders?q=${encodeURIComponent(state.query.orders)}`);
      else if(base==='returns'){
        if(detail){const [value,lots,locations]=await Promise.all([api(`/returns/${encodeURIComponent(detail)}`),api('/lots'),api('/locations')]);data={...value,lots,locations};}
        else{const [returns,orders]=await Promise.all([api(`/returns?q=${encodeURIComponent(state.query.returns)}`),api('/orders')]);data={returns,orders};}
      }
      else if(base==='inventory')data=await api(`/inventory/search?q=${encodeURIComponent(state.query.inventory)}`);
      else if(base==='products')data=await api(`/products?q=${encodeURIComponent(state.query.products)}`);
      else if(base==='receiving'){
        const [receivings,products,lots,locations]=await Promise.all([api('/receiving'),api('/products'),api('/lots'),api('/locations')]);
        data={receivings,products,lots,locations};
      }
      else if(base==='tasks'){const [tasks,products,locations]=await Promise.all([api('/tasks'),api('/products'),api('/locations')]);data={tasks,products,locations};}
      else if(base==='employees')data=await api('/employees');
      else if(base==='chat'){
        const conversations=await api('/conversations');
        let selected=conversations.find(c=>c.employeeCode===detail||c.id===detail);
        if(!selected && detail && canEdit()){
          selected=await api('/conversations',{method:'POST',body:JSON.stringify({employeeCode:detail})});
          selected={...selected,status:'ONLINE'};
        }
        const messages=selected?await api(`/conversations/${encodeURIComponent(selected.id)}/messages`):[];
        data={conversations,selected,messages};
      }
      else if(base==='jarvis'){
        const conversations=await api('/jarvis/conversations');
        const latest=conversations[0];
        state.aiConversationId=latest?.id||null;
        const messages=latest?await api(`/jarvis/conversations/${encodeURIComponent(latest.id)}/messages`):[];
        state.aiMessages=messages.map(item=>({role:item.role,text:item.content,sources:item.sources}));
      }
      if(state.route!==next)return;
      state.data=data;
    }catch(error){
      if(error.status===401){state.user=null;state.csrf='';state.error='Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.';}
      else state.error=error.message;
    }finally{if(state.route===next){state.loading=false;render();}}
  }
  async function refresh(){
    const next=state.route;
    if(location.hash.replace(/^#\/?/,'')!==next){location.hash='#/'+next;return;}
    await loadRoute();
  }
  async function startup(){
    try{
      const value=await api('/auth/me');
      state.user=value.user;
      state.csrf=value.csrfToken;state.dataMode=value.dataMode||'live';
      if(!location.hash)location.hash=state.user.role==='EMPLOYEE'?'#/tasks':'#/dashboard';
      await loadRoute();
    }catch{state.user=null;state.csrf='';state.error='';render();}
  }
  document.addEventListener('submit',async(event)=>{
    const form=event.target;
    if(!(form instanceof HTMLFormElement))return;
    event.preventDefault();
    state.error='';state.flash='';
    const values=Object.fromEntries(new FormData(form));
    try{
      if(form.id==='login-form'){
        const value=await api('/auth/login',{method:'POST',body:JSON.stringify({username:values.username,password:values.password})});
        state.user=value.user;state.csrf=value.csrfToken;state.dataMode=value.dataMode||'live';
        location.hash=state.user.role==='EMPLOYEE'?'#/tasks':'#/dashboard';
        await loadRoute();
      }else if(form.id==='orders-search'||form.id==='returns-search'||form.id==='inventory-search'||form.id==='products-search'){
        const name=form.id.replace('-search','');state.query[name]=short(values.q,120);await refresh();
      }else if(form.id==='receiving-form'||form.classList.contains('receiving-form')){
        const value=await api(`/receiving/${encodeURIComponent(form.dataset.id)}/confirm`,{method:'POST',body:JSON.stringify({actualQuantity:Number(values.quantity),requestId:crypto.randomUUID()})});
        state.flash=`Phiếu ${value.receiving_code}: thực nhận ${value.received_quantity}, thiếu ${value.shortage_quantity}.`;await refresh();
      }else if(form.id==='task-create'){
        const body={taskType:values.taskType,title:values.title,priority:values.priority};
        if(values.productId)body.productId=values.productId;
        if(values.locationId)body.locationId=values.locationId;
        if(values.expectedQuantity)body.expectedQuantity=Number(values.expectedQuantity);
        if(values.sourceId){body.sourceId=values.sourceId;body.sourceType='MANUAL';}
        const created=await api('/tasks',{method:'POST',body:JSON.stringify(body)});
        state.flash=`Đã tạo ${created.task_code}. Chọn “Gợi ý nhân viên” để giao.`;await refresh();
      }else if(form.id==='location-create'){
        const location=await api('/locations',{method:'POST',body:JSON.stringify({code:values.code,zone:values.zone})});
        state.flash=`Đã tạo vị trí ${location.code}.`;await refresh();
      }else if(form.id==='employee-create'||form.id==='viewer-create'){
        const role=form.id==='employee-create'?'EMPLOYEE':'MANAGER';
        const body={role,username:values.username,displayName:values.displayName,password:values.password};
        if(role==='EMPLOYEE'){body.workRole=values.workRole;body.warehouseZone=values.warehouseZone;}
        const account=await api('/accounts',{method:'POST',body:JSON.stringify(body)});
        state.flash=`Đã tạo tài khoản ${account.username} (chỉ xem trên web).`;await refresh();
      }else if(form.id==='product-create'){
        const product=await api('/products',{method:'POST',body:JSON.stringify({sku:values.sku,name:values.name,barcode:values.barcode,unit:values.unit,lotTrackingRequired:true})});
        state.flash=`Đã tạo sản phẩm ${product.sku}.`;await refresh();
      }else if(form.id==='lot-create'){
        const value=await api('/lots',{method:'POST',body:JSON.stringify({productId:values.productId,lotCode:values.lotCode,receivedAt:values.receivedAt,expiryDate:values.expiryDate||null})});
        state.flash=`Đã tạo lô ${value.lot_code}.`;await refresh();
      }else if(form.id==='receiving-create'){
        const selected=form.querySelector('select[name="lotId"] option:checked');
        const value=await api('/receiving',{method:'POST',body:JSON.stringify({productId:selected.dataset.productId,lotId:values.lotId,locationId:values.locationId,expectedQuantity:Number(values.expectedQuantity)})});
        state.flash=`Đã tạo phiếu nhập ${value.receiving_code}.`;await refresh();
      }else if(form.id==='return-create'){
        const [orderId,orderItemId,max]=values.orderItem.split('|');
        if(Number(values.quantity)>Number(max))throw new Error('Số lượng hoàn vượt quá sản phẩm của đơn.');
        const value=await api('/returns',{method:'POST',body:JSON.stringify({orderId,orderItemId,quantity:Number(values.quantity),reason:values.reason})});
        location.hash='#/returns/'+encodeURIComponent(value.return_code);
      }else if(form.id==='return-restock'){
        const item=form.querySelector('select[name="returnItemId"] option:checked');
        const lot=form.querySelector('select[name="lotId"] option:checked');
        if(item.dataset.productId!==lot.dataset.productId)throw new Error('Lô nhập lại phải thuộc đúng sản phẩm hoàn.');
        await api('/returns/'+encodeURIComponent(form.dataset.code)+'/restock',{method:'POST',body:JSON.stringify({returnItemId:values.returnItemId,lotId:values.lotId,locationId:values.locationId,quantity:Number(values.quantity),requestId:crypto.randomUUID()})});
        state.flash='Đã xác nhận nhập lại kho sau kiểm tra.';await refresh();
      }else if(form.id==='message-form'){
        chatRevision++;
        chatWritePending=true;
        try{
          await api(`/conversations/${encodeURIComponent(form.dataset.id)}/messages`,{method:'POST',body:JSON.stringify({content:values.content})});
          await refresh();
        }finally{chatWritePending=false;}
      }else if(form.id==='language-form'){
        await api('/settings',{method:'PATCH',body:JSON.stringify({language:values.language})});
        state.user.language=values.language;state.flash='Đã lưu ngôn ngữ.';render();
      }else if(form.id==='ai-form'){
        const question=short(values.message,1000);
        state.aiMessages.push({role:'user',text:question});
        state.aiPending=true;render();
        try{
          const answer=await api('/jarvis/chat',{method:'POST',body:JSON.stringify({message:question,conversationId:state.aiConversationId})});
          state.aiConversationId=answer.conversationId;
          state.aiMessages.push({role:'assistant',text:answer.message,sources:answer.sources||[]});
        }catch(error){state.aiMessages.push({role:'assistant',text:error.message||'JarVis hiện không thể kết nối AI. Các chức năng kho vẫn hoạt động bình thường.'});}
        state.aiPending=false;render();
      }
    }catch(error){state.error=error.message;render();}
  });
  document.addEventListener('click',async(event)=>{
    const target=event.target.closest('[data-action]');
    if(!target)return;
    const action=target.dataset.action;
    if(action==='menu'||action==='close-menu'){state.menuOpen=action==='menu'?!state.menuOpen:false;render();return;}
    if(action==='logout'){
      try{await api('/auth/logout',{method:'POST',body:'{}'});}catch(_){}
      state.user=null;state.csrf='';state.error='';state.flash='';location.hash='';render();return;
    }
    try{
      if(action==='suggest'){
        const suggestion=await api(`/tasks/${encodeURIComponent(target.dataset.id)}/suggestion`);
        state.suggestion[target.dataset.id]=suggestion;render();
      }else if(action==='assign'){
        await api(`/tasks/${encodeURIComponent(target.dataset.id)}/assign`,{method:'POST',body:JSON.stringify({employeeCode:target.dataset.code})});
        await refresh();
      }else if(action==='task-status'){
        await api(`/tasks/${encodeURIComponent(target.dataset.id)}/status`,{method:'POST',body:JSON.stringify({status:target.dataset.status})});
        await refresh();
      }else if(action==='return-status'){
        await api('/returns/'+encodeURIComponent(target.dataset.code)+'/status',{method:'POST',body:JSON.stringify({status:target.dataset.status})});
        await refresh();
      }
    }catch(error){state.error=error.message;render();}
  });
  window.addEventListener('hashchange',loadRoute);
  window.setInterval(pollChat,10000);
  startup();
})();
