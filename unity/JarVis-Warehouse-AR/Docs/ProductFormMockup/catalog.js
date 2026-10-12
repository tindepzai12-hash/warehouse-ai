const schema=CatalogSchema;
const escapeHtml=value=>String(value ?? '').replace(/[&<>"']/g,character=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
const arrow='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="m9 6 6 6-6 6"/></svg>';
const fixtureProducts=[{id:'example-1',sku:'DEMO-SUA-180',barcode:'JV-DEMO-001',name:'Sữa tươi 180 ml',brand:'Thương hiệu mẫu',lot:'LO-MAU-A',location:'VI-TRI-MAU-A',unit:'Hộp'},{id:'example-2',sku:'DEMO-AO-M',barcode:'JV-DEMO-002',name:'Áo thun đen size M',brand:'Thương hiệu mẫu',lot:'LO-MAU-B',location:'VI-TRI-MAU-B',unit:'Cái'}];
const state={view:'create',step:0,category:'drink',template:null,values:{},drafts:{},custom:[],errors:{},query:'',receipt:false,notice:'',saved:[],history:[],recovery:null,role:'admin',adminCategory:'drink',adminTemplate:null,adminField:'volume'};
const root=document.getElementById('prototype');
const fixtureTemplateIds={'example-1':'drink-beverages-sua-tuoi','example-2':'fashion-tops-ao-thun'};
const categoryLabel=id=>schema.categories.find(category=>category.id===id).label;
const options=(values,selected)=>values.map(value=>'<option value="'+escapeHtml(value)+'"'+(String(value)===String(selected)?' selected':'')+'>'+escapeHtml(value)+'</option>').join('');
function input(name,label,value='',type='text',help='',required=false) {
  return '<label class="field"><span>'+escapeHtml(label)+(required?' *':'')+'</span><input name="'+name+'" type="'+type+'" value="'+escapeHtml(value)+'"'+(['number'].includes(type)?' step="any" inputmode="decimal"':'')+' aria-invalid="'+Boolean(state.errors[name])+'">'+(help?'<small>'+escapeHtml(help)+'</small>':'')+(state.errors[name]?'<small class="field-error">'+escapeHtml(state.errors[name])+'</small>':'')+'</label>';
}
function select(name,label,values,value='',help='') {
  return '<label class="field"><span>'+escapeHtml(label)+'</span><select name="'+name+'">'+options(values,value)+'</select>'+(help?'<small>'+escapeHtml(help)+'</small>':'')+'</label>';
}
function enumField(name,label,choices,required=false) {
  const value=state.values[name] || '';
  return '<label class="field"><span>'+escapeHtml(label)+(required?' *':' · tùy chọn')+'</span><select name="'+name+'" aria-invalid="'+Boolean(state.errors[name])+'">'+options(['',...choices],value)+'<option value="__custom__"'+(value==='__custom__'?' selected':'')+'>Khác — Nhập tay</option></select>'+(value==='__custom__'?'<input class="custom" name="'+name+'Custom" aria-label="'+escapeHtml(label)+' tùy chỉnh" value="'+escapeHtml(state.values[name+'Custom'] || '')+'">':'')+(state.errors[name]?'<small class="field-error">'+escapeHtml(state.errors[name])+'</small>':'')+'</label>';
}
const valueOf=name=>state.values[name]==='__custom__'?state.values[name+'Custom'] || '':state.values[name] || '';
function measureField(definition) {
  const name=definition.id;const value=state.values[name] || '';
  const basis=schema.normalizeMeasure(1,definition.unit);const target=schema.normalizeMeasure(1,state.values[name+'Unit']);
  const choices=basis && target && basis.dimension===target.dimension ? (definition.choices || []).map(number=>String(Number((number*basis.value/target.value).toPrecision(12)))) : [];
  const picker=choices.length?'<select name="'+name+'" aria-label="'+escapeHtml(definition.label)+'">'+options(['',...choices],value)+'<option value="__custom__"'+(value==='__custom__'?' selected':'')+'>Khác — Nhập tay</option></select>':'<input type="number" step="any" inputmode="decimal" name="'+name+'" aria-label="'+escapeHtml(definition.label)+'" value="'+escapeHtml(value)+'">';
  return '<label class="field"><span>'+escapeHtml(definition.label)+(definition.required?' *':' · tùy chọn')+'</span><span class="measure">'+picker+'<select name="'+name+'Unit" aria-label="Đơn vị '+escapeHtml(definition.label)+'">'+options(definition.units,state.values[name+'Unit'])+'</select></span>'+(value==='__custom__'?'<input class="custom" type="number" step="any" inputmode="decimal" name="'+name+'Custom" aria-label="'+escapeHtml(definition.label)+' tùy chỉnh" value="'+escapeHtml(state.values[name+'Custom'] || '')+'">':'')+(state.errors[name]?'<small class="field-error">'+escapeHtml(state.errors[name])+'</small>':'')+'</label>';
}
function attribute(definition) {
  if(definition.kind==='measure') return measureField(definition);
  if(definition.kind==='enum') return enumField(definition.id,definition.label,definition.choices || [],definition.required);
  return input(definition.id,definition.label+(definition.required?'':' · tùy chọn'),state.values[definition.id],['number','integer'].includes(definition.kind)?'number':definition.kind==='date'?'date':'text','',definition.required);
}
function collect() {
  const form=root.querySelector('form');if(!form)return;
  for(const [name,value] of new FormData(form)) state.values[name]=value;
}
function retainDraft() {
  collect();if(state.template) state.drafts[state.template.id]={values:{...state.values},custom:state.custom.map(definition=>({...definition})),receipt:state.receipt,existingProduct:state.existingProduct};
}
function chooseTemplate(id) {
  retainDraft();state.template=schema.templates.find(template=>template.id===id) || state.saved.find(template=>template.id===id);
  state.existingProduct=null;
  const previous=state.drafts[id];state.custom=previous?.custom.map(definition=>({...definition})) || [];
  state.existingProduct=previous?.existingProduct || null;
  state.receipt=previous?.receipt || false;
  state.values=previous?{...previous.values}:{name:state.template.manual?'':state.template.name,brand:'',unit:state.template.units[0],quantity:'1',lotStatus:'Chưa xác minh nhãn',barcodeMode:'Cấp mã nội bộ khi lưu thật'};
  if(!previous) for(const definition of state.template.attributes) {
    state.values[definition.id]=definition.kind==='measure'&&definition.choices?.length?String(definition.choices[0]):definition.id==='colour'?'Đen':definition.id==='size'?definition.choices.includes('M')?'M':definition.choices[0]:'';
    if(definition.units)state.values[definition.id+'Unit']=definition.unit;
  }
  state.step=2;state.errors={};state.notice='';render();
}
function phone(content,title,footer='',steps=true) {
  return '<section class="phone"><header class="phone-head"><h2>'+title+'</h2><p>JarVis · Bản duyệt, chưa ghi vào kho</p>'+(steps?'<div class="steps">'+['1. Ngành hàng','2. Chọn mẫu','3. Kiểm tra'].map((label,index)=>'<span class="'+(state.step===index?'current':'')+'">'+label+'</span>').join('')+'</div>':'')+'</header><div class="scroll">'+content+'</div>'+(footer?'<footer class="phone-foot">'+footer+'</footer>':'')+'</section>';
}
const backButton='<button type="button" class="secondary" data-back>Quay lại</button>';
const fixtureNotice='<p class="notice">Dữ liệu minh họa tách biệt với OMS. Không phải danh sách sản phẩm / barcode thật của kho.</p>';
function templateResults() {
  const query=state.query.trim().toLocaleLowerCase('vi');
  const category=schema.categories.find(item=>item.id===state.category);
  return category.families.map((family,index)=>{
    const matches=schema.templates.filter(template=>template.category===state.category && template.family===family.id && (template.manual || template.name.toLocaleLowerCase('vi').includes(query)));
    return '<details class="family"'+(index===0 || query?' open':'')+'><summary>'+family.label+'</summary>'+matches.map(template=>'<button type="button" class="list-button '+(template.manual?'manual':'')+'" data-template="'+template.id+'"><strong>'+escapeHtml(template.name)+'</strong>'+arrow+'</button>').join('')+'</details>';
  }).join('')+(state.saved.filter(template=>template.category===state.category).length?'<h3 class="section">Mẫu tùy chỉnh của phiên thử</h3>'+state.saved.filter(template=>template.category===state.category && template.name.toLocaleLowerCase('vi').includes(query)).map(template=>'<button type="button" class="list-button" data-template="'+template.id+'">'+escapeHtml(template.name)+arrow+'</button>').join(''):'');
}
function productForm() {
  const template=state.template;
  const lotAllowed=Boolean(template.lot);
  const lotFields=lotAllowed && state.receipt && (template.lot===true || state.values.useExpiry==='Có')?'<h3 class="section">Lô của lần nhận này</h3><p class="sub">NSX/HSD thuộc lô, không thuộc toàn bộ SKU. Ngày không có trên nhãn để trống và đánh dấu cần xác minh.</p>'+select('lotStatus','Tình trạng thông tin ngày',['Chưa xác minh nhãn','Đã đối chiếu nhãn'],state.values.lotStatus)+input('lotCode','Mã lô',state.values.lotCode)+input('manufacture','Ngày sản xuất (NSX)',state.values.manufacture,'date')+input('expiry','Hạn sử dụng (HSD)',state.values.expiry,'date')+'<p class="sub">Ngưỡng cận hạn chưa được kho cấu hình; bản thử không tự đặt 30 ngày.</p>':'';
  return '<p class="sub">'+categoryLabel(template.category)+' / '+escapeHtml(template.familyLabel)+' / '+escapeHtml(template.name)+'</p><h3>Kiểm tra thông tin</h3><p class="sub">Giá trị điền sẵn chỉ là ví dụ. Sửa theo hàng thật. * là trường cần điền trong bản thử.</p><form novalidate>'+input('name','Tên sản phẩm',state.values.name,'text','',true)+(template.brand?enumField('brand','Thương hiệu',schema.brands[template.category] || []):'')+'<h3 class="section">Thuộc tính phiên bản</h3>'+template.attributes.map(attribute).join('')+state.custom.map(attribute).join('')+(template.manual?'<details class="family"><summary>Thêm thuộc tính tùy chỉnh</summary>'+input('customLabel','Tên thuộc tính',state.values.customLabel)+select('customKind','Kiểu dữ liệu',['Văn bản','Số','Ngày'],state.values.customKind || 'Văn bản')+select('customUnit','Đơn vị (nếu là số)',['Không có','ml','l','g','kg','cm','mm'],state.values.customUnit || 'Không có')+'<button type="button" class="secondary" data-add-attribute>Thêm vào form thử</button><p class="sub">Chưa được thêm vào danh mục dùng chung; cần quản trị viên duyệt.</p></details>':'')+'<h3 class="section">Số lượng dự kiến nhận</h3><p class="sub">Không phải tồn kho và không phải số tem. Lưu sản phẩm không cộng số lượng này vào kho.</p><div class="two">'+input('quantity','Số lượng',state.values.quantity,'number','Số nguyên lớn hơn 0',true)+enumField('unit','Đơn vị đếm',template.units,true)+'</div><label class="check"><input type="checkbox" data-receipt'+(state.receipt?' checked':'')+'>Kèm bản nháp lần nhận hàng</label><p class="sub">Kho/vị trí sẽ lấy từ nghiệp vụ nhập hiện có, không nhập lại ở đây. Không tự đổi thùng sang chai.</p>'+(lotAllowed && template.lot==='optional'?select('useExpiry','Nhận hàng có quản lý NSX/HSD?',['Chưa rõ','Có','Không'],state.values.useExpiry || 'Chưa rõ'):'')+lotFields+'<details class="family"><summary>Mã hàng đã có (tùy chọn)</summary>'+select('barcodeMode','Nguồn barcode',['Cấp mã nội bộ khi lưu thật','Giữ mã trên bao bì'],state.values.barcodeMode)+input('sku','SKU đang dùng',state.values.sku)+input('barcode','Barcode đang dùng',state.values.barcode,'text','Giữ số 0 đầu. Bản thử chưa xác minh định dạng GTIN/quyền sở hữu.')+'</details></form>'+(state.notice?'<p class="notice" role="status">'+escapeHtml(state.notice)+'</p>':'');
}
function validate() {
  collect();const errors={};const template=state.template;
  if(!state.values.name?.trim())errors.name='Điền tên sản phẩm.';
  if(!Number.isSafeInteger(Number(state.values.quantity)) || Number(state.values.quantity)<=0)errors.quantity='Số lượng phải là số nguyên lớn hơn 0.';
  if(!valueOf('unit').trim())errors.unit='Chọn hoặc nhập đơn vị đếm.';
  for(const definition of [...template.attributes,...state.custom]) {
    const value=valueOf(definition.id);const present=value!=='';
    if(definition.required && !present)errors[definition.id]='Điền '+definition.label.toLowerCase()+'.';
    if(state.values[definition.id]==='__custom__' && !present)errors[definition.id]='Đã chọn Khác: hãy nhập giá trị.';
    if(present && ['measure','number','integer'].includes(definition.kind)) {
      const number=Number(String(value).replace(',','.'));
      if(!Number.isFinite(number)||number<=0||definition.kind==='integer'&&!Number.isSafeInteger(number))errors[definition.id]='Nhập số hợp lệ lớn hơn 0.';
      if(definition.kind==='measure' && !definition.units.includes(state.values[definition.id+'Unit']))errors[definition.id]='Đơn vị không phù hợp phép đo.';
      if(definition.kind==='measure' && !schema.normalizeMeasure(value,state.values[definition.id+'Unit']))errors[definition.id]='Phép đo vượt giới hạn hoặc không hợp lệ.';
    }
    if(present && definition.kind==='date' && !schema.validDate(value))errors[definition.id]='Ngày lịch không hợp lệ.';
  }
  for(const key of ['brand','unit']) if(state.values[key]==='__custom__'&&!valueOf(key).trim())errors[key]='Đã chọn Khác: hãy nhập giá trị.';
  const lotActive=state.receipt && template.lot && (template.lot===true || state.values.useExpiry==='Có');
  if(lotActive) {
    for(const key of ['manufacture','expiry']) if(state.values[key]&&!schema.validDate(state.values[key]))errors[key]='Ngày lịch không hợp lệ.';
    if(state.values.manufacture && state.values.expiry && state.values.manufacture>state.values.expiry)errors.expiry='NSX không được sau HSD.';
    if(state.values.lotStatus==='Đã đối chiếu nhãn' && !state.values.expiry)errors.expiry='Thiếu HSD: chuyển sang Chưa xác minh nhãn nếu chưa có dữ liệu.';
    if((state.values.manufacture || state.values.expiry) && !state.values.lotCode?.trim())errors.lotCode='Nhập mã lô của lần nhận này.';
  }
  if(state.values.barcodeMode==='Giữ mã trên bao bì' && !state.values.barcode?.trim())errors.barcode='Nhập barcode đang dùng.';
  state.errors=errors;return Object.keys(errors).length===0;
}
function reviewProduct() {
  const template=state.template;const today=new Date().toLocaleDateString('en-CA');
  const measures=[...template.attributes,...state.custom].filter(definition=>definition.kind==='measure' && valueOf(definition.id)).map(definition=>({label:definition.label,...schema.normalizeMeasure(valueOf(definition.id),state.values[definition.id+'Unit'])}));
  const dateWarnings=state.receipt && template.lot?[(state.values.manufacture>today?'NSX ở tương lai: cần xác minh.':''),(state.values.expiry && state.values.expiry<today?'HSD đã hết: không đưa vào luồng hàng có thể xuất.':''),(state.values.lotStatus==='Chưa xác minh nhãn'?'Thông tin ngày chưa xác minh; chưa đủ để xác nhận nhập hàng có HSD.':'')].filter(Boolean):[];
  return '<h3>Tách rõ trước khi lưu</h3><div class="label-preview"><strong>'+escapeHtml(state.values.name)+'</strong><p>'+escapeHtml(state.values.barcode || 'CHƯA CẤP BARCODE')+'</p><div class="label-placeholder">Preview bố cục 50 × 30 mm<br>Chưa tạo vạch thật, không dùng để quét</div></div><dl class="summary"><div><dt>Sản phẩm / SKU</dt><dd>'+escapeHtml(state.values.name)+' · '+escapeHtml(valueOf('brand'))+' · 1 '+escapeHtml(valueOf('unit'))+'</dd></div><div><dt>Phép đo chuẩn hóa để đối chiếu</dt><dd>'+escapeHtml(measures.map(measure=>measure.label+': '+measure.value+' '+measure.unit+' ('+measure.dimension+')').join('; ') || 'Không có phép đo')+'</dd></div><div><dt>Lần nhận — chưa ghi tồn</dt><dd>'+escapeHtml(state.values.quantity)+' '+escapeHtml(valueOf('unit'))+' · '+(state.receipt?'Bản nháp riêng':'Chưa lập phiếu nhận')+'</dd></div><div><dt>Số bản tem</dt><dd>Chọn sau khi sản phẩm đã lưu thật; không lấy từ số lượng nhận.</dd></div></dl>'+dateWarnings.map(warning=>'<p class="notice warning">'+warning+'</p>').join('')+'<p class="notice">Backend thật phải kiểm tra sản phẩm trùng, quyền tạo và barcode trước khi cấp/lưu. Bản thử không thực hiện các thao tác đó.</p><label class="check"><input type="checkbox" data-keep-template'+(template.manual?' checked':'')+'>Giữ mẫu tùy chỉnh để dùng lại trong phiên thử</label>'+(state.notice?'<p class="notice" role="status">'+escapeHtml(state.notice)+'</p>':'');
}
function createView() {
  let content,footer,title;
  if(state.step===0) {title='Chọn ngành hàng';content='<h3>Hàng thuộc nhóm nào?</h3><p class="sub">Chọn đúng nhóm để chỉ thấy các trường liên quan.</p>'+schema.categories.map(category=>'<button class="list-button" data-category="'+category.id+'"><span><strong>'+category.label+'</strong><small>'+category.families.map(family=>family.label).join(' · ')+'</small></span>'+arrow+'</button>').join('')+'<p class="notice">Theo yêu cầu mới, Chất tẩy rửa nằm trong Khác. Vị trí này đang chờ bạn duyệt.</p>';footer='';}
  else if(state.step===1) {title='Chọn mẫu '+categoryLabel(state.category).toLowerCase();content=input('templateQuery','Tìm nhanh mẫu',state.query)+'<div id="template-results">'+templateResults()+'</div>';footer=backButton;}
  else if(state.step===2) {title='Kiểm tra sản phẩm';content=existingProductsPicker()+productForm();footer='<div class="actions">'+backButton+'<button class="primary" data-review>Xem bản xác nhận</button></div>';}
  else {title='Xác nhận / xem tem';content=reviewProduct();footer='<div class="actions">'+backButton+'<button class="primary" data-try-save>Thử xác nhận</button></div>';}
  return '<div class="workspace"><aside class="help"><h1>Chọn mẫu trước.<br>Chỉ nhập phần còn thiếu.</h1><p>Ba bước nhập chính; xem tem và lưu là hành động sau kiểm tra.</p><ul><li>Ngành hàng → mẫu → thông tin.</li><li>Số lượng và ngày thuộc lần nhận.</li><li>Tem hỏng dùng lại barcode cũ.</li></ul><p>Thời trang và gia dụng thông thường không hiện NSX/HSD. Chính sách lô hiện có không bị tắt.</p></aside>'+phone(content,title,footer)+'</div>';
}
function existingProductsPicker() {
  if(state.existingProduct)return '<p class="notice">Đã chọn SKU minh họa có sẵn: '+state.existingProduct.sku+'. Thuộc tính và mã chỉ xem, không đăng ký lại; có thể sửa bản nháp lần nhận.</p>';
  const products=fixtureProducts.filter(product=>schema.templates.find(item=>item.id===fixtureTemplateIds[product.id]).category===state.template.category);
  return '<details class="family"><summary>Chọn hàng đã có (dữ liệu minh họa)</summary>'+fixtureNotice+(products.length?products.map(product=>'<button type="button" class="list-button" data-existing="'+product.id+'"><span>'+product.name+'<small>'+product.sku+'</small></span>'+arrow+'</button>').join(''):'<p class="empty">Bộ minh họa chưa có hàng trong nhóm này. Không phải kết quả tìm kho thật.</p>')+'</details>';
}
function recoveryContent() {
  const query=String(state.values.recoveryQuery || '').toLocaleLowerCase('vi');
  const matches=fixtureProducts.filter(product=>Object.values(product).join(' ').toLocaleLowerCase('vi').includes(query));
  if(!state.recovery)return '<h3>Tìm hàng để lấy lại mã cũ</h3>'+fixtureNotice+'<form>'+input('recoveryQuery','SKU, tên, barcode, thương hiệu, lô hoặc vị trí',state.values.recoveryQuery,'text','Ví dụ: DEMO-SUA-180, LO-MAU-A')+'</form><div id="recovery-results">'+(matches.length?matches.map(product=>'<button class="list-button" data-product="'+product.id+'"><span><strong>'+product.name+'</strong><small>'+product.sku+' · '+product.barcode+'</small></span>'+arrow+'</button>').join(''):'<p class="empty">Không tìm thấy trong dữ liệu minh họa. Thử SKU hoặc tên khác; không tự tạo mã thay thế.</p>')+'</div>';
  const product=state.recovery;
  return '<h3>Xác nhận đúng sản phẩm</h3>'+fixtureNotice+'<dl class="summary"><div><dt>Tên / SKU</dt><dd>'+product.name+' / '+product.sku+'</dd></div><div><dt>Lô / vị trí minh họa</dt><dd>'+product.lot+' / '+product.location+'</dd></div></dl><div class="label-preview"><strong>'+product.name+'</strong><p>'+product.barcode+'</p><div class="label-placeholder">Giữ nguyên mã đã chọn<br>Vạch chưa được render trong prototype</div></div><form>'+input('copies','Số bản tem',state.values.copies || '1','number','Độc lập với số lượng hàng.')+select('reason','Lý do',['Tem rách','Tem mờ','Tem mất','Tem không quét được','Khác'],state.values.reason || 'Tem rách')+(state.values.reason==='Khác'?input('reasonCustom','Lý do cụ thể',state.values.reasonCustom):'')+select('role','Vai trò mô phỏng',['Admin','Quản lý','Nhân viên'],state.values.role || 'Admin','Chính sách hiện tại: chỉ admin sửa dữ liệu kho; quyền in cho vai trò khác chờ duyệt.')+'</form><p class="notice">Chưa chọn máy in. Nút bên dưới chỉ mô phỏng lịch sử, không in hoặc xuất PNG thật. Không tạo SKU/barcode mới và không cộng tồn.</p>'+(state.notice?'<p class="notice" role="status">'+escapeHtml(state.notice)+'</p>':'')+(Object.values(state.errors).length?'<p class="error" role="alert">'+escapeHtml(Object.values(state.errors).join(' '))+'</p>':'');
}
function historyTable() {
  return state.history.length?'<div class="table-scroll"><table><thead><tr><th>Sản phẩm / barcode</th><th>Người / thời gian</th><th>Số tem / lý do</th><th>Thiết bị / trạng thái</th></tr></thead><tbody>'+state.history.map(event=>'<tr><td>'+event.sku+'<br>'+event.barcode+'</td><td>'+escapeHtml(event.actor)+'<br>'+event.time+'</td><td>'+event.copies+' / '+escapeHtml(event.reason)+'</td><td>Không có máy in<br>Mô phỏng, chưa in</td></tr>').join('')+'</tbody></table></div>':'<p class="empty">Chưa có lịch sử của phiên thử. Tìm và chọn sản phẩm để thử luồng phục hồi.</p>';
}
function adminView() {
  const category=schema.categories.find(item=>item.id===state.adminCategory);
  const templates=schema.templates.filter(template=>template.category===category.id);
  const template=templates.find(item=>item.id===state.adminTemplate) || templates[0];state.adminTemplate=template.id;
  const definition=template.attributes.find(item=>item.id===state.adminField) || template.attributes[0];state.adminField=definition.id;
  return '<section class="admin"><h1>Web · Danh mục và thuộc tính</h1><p class="sub">Cấu hình schema chung dự kiến phục vụ web và Unity; đây chỉ là bản duyệt trong RAM, không phải web OMS.</p><div class="admin-grid"><div>'+select('adminCategory','Ngành hàng',schema.categories.map(item=>item.id),state.adminCategory)+select('adminTemplate','Mẫu sản phẩm',templates.map(item=>item.id),state.adminTemplate)+'<p class="notice">Đổi schema có version, duyệt và vô hiệu hóa thay vì xóa dữ liệu đang liên kết. API đồng bộ chưa được triển khai.</p></div><div><h2>'+escapeHtml(template.name)+' · '+template.familyLabel+'</h2><div class="table-scroll"><table><thead><tr><th>Thuộc tính</th><th>Kiểu / đơn vị</th><th>Điều kiện</th></tr></thead><tbody>'+template.attributes.map(item=>'<tr><td>'+item.label+'</td><td>'+item.kind+' / '+(item.units || []).join(', ')+'</td><td>'+(item.required?'Cần nhập trong mẫu này':'Tùy chọn')+'</td></tr>').join('')+'</tbody></table></div><h3 class="section">Đề xuất cấu hình</h3><form>'+select('adminField','Thuộc tính cần sửa',template.attributes.map(item=>item.id),state.adminField)+input('optionProposal','Thêm giá trị lựa chọn',state.values.optionProposal)+input('brandProposal','Thương hiệu đề xuất',state.values.brandProposal)+'</form><button class="primary" data-config>Áp dụng trong bản thử</button><p class="sub">Chỉ áp dụng enum hoặc danh sách số; không tự thêm giá trị nhập tay vào danh mục chung. Thương hiệu đề xuất sẽ dùng trong form của nhóm này ở phiên thử.</p><details class="family"><summary>Thương hiệu gợi ý hiện có</summary><p class="sub">'+escapeHtml(schema.brands[category.id].join(' · ') || 'Chưa cấu hình. Có thể nhập tay.')+'</p></details><details class="family"><summary>Mẫu tùy chỉnh chờ duyệt trong phiên</summary>'+state.saved.map(item=>'<p>'+escapeHtml(item.name)+' · '+categoryLabel(item.category)+' · chưa xuất bản</p>').join('')+'</details>'+(state.notice?'<p class="notice" role="status">'+escapeHtml(state.notice)+'</p>':'')+'</div></div></section>';
}
function render() {
  document.querySelectorAll('[data-view]').forEach(button=>button.classList.toggle('active',button.dataset.view===state.view));
  if(state.view==='create')root.innerHTML=createView();
  else if(state.view==='catalog')root.innerHTML=adminView();
  else if(state.view==='labels')root.innerHTML='<section class="admin"><h1>Web · Quản lý và in lại tem</h1><p class="sub">Tìm dữ liệu cũ, xác nhận, lấy lại đúng barcode và ghi lịch sử. Không tăng tồn kho.</p><div class="admin-grid"><div>'+recoveryContent()+'</div><div><h2>Lịch sử in của phiên thử</h2>'+historyTable()+(state.recovery?'<button class="primary" data-print>Thử ghi lịch sử in lại</button><button class="secondary" data-recovery-back>Chọn sản phẩm khác</button>':'')+'</div></div></section>';
  else root.innerHTML='<div class="workspace"><aside class="help"><h1>Tem hỏng?<br>Tìm mã đã có.</h1><p>Không tạo lại sản phẩm. Chọn đúng hàng, giữ barcode, rồi xuất lại tem theo quyền.</p><p>Tìm theo lô/vị trí chỉ giúp xác định sản phẩm. Barcode trong phạm vi này nhận diện SKU, không mã hóa lô.</p></aside>'+phone(recoveryContent(),'Phục hồi tem',state.recovery?'<div class="actions"><button class="secondary" data-recovery-back>Tìm lại</button><button class="primary" data-print>Thử in lại</button></div>':'',false)+'</div>';
  const adminCategory=root.querySelector('[name=adminCategory]');if(adminCategory)for(const option of adminCategory.options)option.textContent=categoryLabel(option.value);
  for(const selectElement of root.querySelectorAll('[name=adminTemplate]'))for(const option of selectElement.options)option.textContent=schema.templates.find(template=>template.id===option.value).name;
  for(const selectElement of root.querySelectorAll('[name=adminField]'))for(const option of selectElement.options)option.textContent=schema.fields[option.value]?.label || option.value;
  if(state.view==='create' && state.step===2 && state.existingProduct) {
    const readOnlyNames=['name','brand','brandCustom','sku','barcode','barcodeMode','unit',...state.template.attributes.flatMap(definition=>[definition.id,definition.id+'Unit',definition.id+'Custom'])];
    root.querySelectorAll('[name]').forEach(control=>{if(readOnlyNames.includes(control.name))control.disabled=true;});
  }
  root.querySelector('form')?.addEventListener('submit',event=>{event.preventDefault();});
  root.querySelectorAll('[data-template]').forEach(button=>button.addEventListener('click',()=>chooseTemplate(button.dataset.template)));
}
root.addEventListener('input',event=>{
  if(event.target.tagName==='SELECT')return;
  if(event.target.name==='templateQuery'){state.query=event.target.value;root.querySelector('#template-results').innerHTML=templateResults();root.querySelectorAll('[data-template]').forEach(button=>button.addEventListener('click',()=>chooseTemplate(button.dataset.template)));return;}
  collect();
  if(event.target.name==='recoveryQuery'){const selection=event.target.selectionStart;render();const control=root.querySelector('[name=recoveryQuery]');control.focus();control.setSelectionRange(selection,selection);}
});
root.addEventListener('change',event=>{
  if(event.target.hasAttribute('data-receipt')){collect();state.receipt=event.target.checked;render();return;}
  const previous={...state.values};
  collect();
  if(event.target.name?.endsWith('Unit') && state.template) {
    const key=event.target.name.slice(0,-4);const definition=[...state.template.attributes,...state.custom].find(item=>item.id===key);
    if(definition?.kind==='measure') {
      const normalized=schema.normalizeMeasure(previous[key]==='__custom__'?previous[key+'Custom']:previous[key],previous[key+'Unit']);
      const basis=schema.normalizeMeasure(1,event.target.value);
      if(normalized && basis && normalized.dimension===basis.dimension)state.values[previous[key]==='__custom__'?key+'Custom':key]=String(Number((normalized.value/basis.value).toPrecision(12)));
      else {state.values[key]='';state.values[key+'Custom']='';state.notice='Đơn vị khác loại phép đo: nhập lại giá trị theo nhãn, không tự đổi khối lượng sang thể tích.';}
    }
  }
  if(event.target.name==='adminCategory'){state.adminCategory=event.target.value;state.adminTemplate=null;state.notice='';}
  if(event.target.name==='adminTemplate')state.adminTemplate=event.target.value;
  if(event.target.name==='adminField')state.adminField=event.target.value;
  if(event.target.tagName==='SELECT')render();
});
root.addEventListener('click',event=>{
  const target=event.target.closest('button');if(!target)return;
  if(target.dataset.category){retainDraft();state.category=target.dataset.category;state.query='';state.step=1;state.errors={};render();}
  if(target.dataset.existing){
    const product=fixtureProducts.find(item=>item.id===target.dataset.existing);chooseTemplate(fixtureTemplateIds[product.id]);state.existingProduct=product;
    Object.assign(state.values,{name:product.name,brand:'__custom__',brandCustom:product.brand,sku:product.sku,barcode:product.barcode,barcodeMode:'Giữ mã trên bao bì',unit:product.unit});render();
  }
  if(target.hasAttribute('data-back')){retainDraft();state.step=Math.max(0,state.step-1);state.errors={};state.notice='';render();}
  if(target.hasAttribute('data-review')){
    if(validate()){retainDraft();state.step=3;}render();
    const invalid=root.querySelector('[aria-invalid=true]');
    if(invalid){let ancestor=invalid.parentElement;while(ancestor && ancestor!==root){if(ancestor.tagName==='DETAILS')ancestor.open=true;ancestor=ancestor.parentElement;}invalid.focus();}
  }
  if(target.hasAttribute('data-add-attribute')){
    collect();const label=state.values.customLabel?.trim();
    if(!label){state.notice='Điền tên thuộc tính trước khi thêm.';render();return;}
    const receiptFields=['nsx','hsd','ngày sản xuất','hạn sử dụng','mã lô','số lượng','tồn kho','quantity_on_hand','serial','số serial'];
    if(receiptFields.includes(label.toLocaleLowerCase('vi'))){state.notice='Trường này thuộc lô, lần nhận hoặc đơn vị thiết bị, không thuộc thuộc tính SKU. Dùng vùng nghiệp vụ tương ứng; quản lý serial chưa được mô phỏng.';render();return;}
    const kind=state.values.customKind || 'Văn bản';const unit=state.values.customUnit;
    const definition={id:'custom-'+state.custom.length,label,kind:kind==='Ngày'?'date':kind==='Số'?unit && unit!=='Không có'?'measure':'number':'text',required:false};
    if(definition.kind==='measure'){definition.units=[unit];definition.unit=unit;state.values[definition.id+'Unit']=unit;}
    state.custom.push(definition);state.notice='Thuộc tính chỉ ở bản thử. Chưa được duyệt vào danh mục chung.';render();
  }
  if(target.hasAttribute('data-try-save')){
    const keep=root.querySelector('[data-keep-template]')?.checked;
    if(keep && !state.existingProduct && !state.saved.some(item=>item.id==='session-'+state.template.id))state.saved.push({...state.template,id:'session-'+state.template.id,name:state.values.name,manual:true,attributes:[...state.template.attributes,...state.custom]});
    if(keep)state.drafts['session-'+state.template.id]={values:{...state.values},custom:[],receipt:state.receipt};
    state.notice='Đã thử xác nhận; chưa lưu OMS, chưa cấp barcode và chưa nhập kho.';render();
  }
  if(target.dataset.product){collect();state.recovery=fixtureProducts.find(product=>product.id===target.dataset.product);state.values.copies='1';state.notice='';state.errors={};render();}
  if(target.hasAttribute('data-recovery-back')){state.recovery=null;state.notice='';state.errors={};render();}
  if(target.hasAttribute('data-print')){
    collect();state.errors={};const copies=Number(state.values.copies);const reason=state.values.reason==='Khác'?state.values.reasonCustom?.trim():state.values.reason || 'Tem rách';
    if(!Number.isSafeInteger(copies)||copies<=0||copies>10000)state.errors.copies='Số tem phải là số nguyên từ 1 đến 10000.';
    if(!reason)state.errors.reason='Điền lý do in lại.';
    if(state.values.role && state.values.role!=='Admin')state.errors.role='Quyền in của vai trò này chưa được duyệt. Không mô phỏng ghi lịch sử với quyền này.';
    if(!Object.keys(state.errors).length){state.history.push({sku:state.recovery.sku,barcode:state.recovery.barcode,copies,reason,actor:'Admin minh họa',time:new Date().toISOString()});state.notice='Mô phỏng lịch sử: barcode giữ nguyên, tồn kho không đổi. Chưa in hoặc xuất ảnh thật.';}
    render();
  }
  if(target.hasAttribute('data-config')){
    collect();const template=schema.templates.find(item=>item.id===state.adminTemplate);const definition=template.attributes.find(item=>item.id===state.adminField);const proposal=state.values.optionProposal?.trim();const brand=state.values.brandProposal?.trim();
    const numeric=definition.kind==='measure';
    if(proposal && (!['enum','measure'].includes(definition.kind) || numeric && (!Number.isFinite(Number(proposal)) || Number(proposal)<=0)))state.notice='Giá trị không phù hợp kiểu thuộc tính. Chưa áp dụng.';
    else if(!proposal && !brand)state.notice='Nhập ít nhất một giá trị hoặc thương hiệu để thử cấu hình.';
    else{if(proposal)definition.choices=[...new Set([...(definition.choices || []),numeric?Number(proposal):proposal])];if(brand)schema.brands[state.adminCategory]=[...new Set([...schema.brands[state.adminCategory],brand])];state.notice='Đã áp dụng trong phiên prototype. Chưa xuất bản hoặc đồng bộ API.';}
    render();
  }
});
document.querySelectorAll('[data-view]').forEach(button=>button.addEventListener('click',()=>{retainDraft();state.view=button.dataset.view;state.notice='';state.errors={};state.recovery=null;render();}));
render();
