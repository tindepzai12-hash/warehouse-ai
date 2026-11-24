const schema = {...CatalogSchema,categories:structuredClone(CatalogSchema.categories),templates:CatalogSchema.templates.map(template=>structuredClone(template))};
const fixtures = [
  {id:'milk',name:'Sữa tươi 180 ml',sku:'DEMO-SUA-180',barcode:'JV-DEMO-001',source:'Mã nội bộ mẫu',unit:'Hộp',category:'drink',summary:'180 ml · nguyên bản',hasLayout:true,expiryPolicy:true},
  {id:'shirt',name:'Áo thun đen · size M',sku:'DEMO-AO-M',barcode:'JV-DEMO-002',source:'Mã nội bộ mẫu',unit:'Cái',category:'fashion',summary:'Đen · M',hasLayout:true},
  {id:'pan',name:'Nồi inox 24 cm',sku:'DEMO-NOI-24',barcode:'JV-DEMO-003',source:'Mã nội bộ mẫu',unit:'Cái',category:'home',summary:'Inox · 24 cm',hasLayout:false},
  {id:'missing',name:'Nước giặt 2 l · thiếu mã gốc',sku:'DEMO-GIAT-2L',barcode:'',source:'Chưa xác minh',unit:'Can',category:'other',summary:'2 l · không có barcode trong dữ liệu mẫu',hasLayout:false}
];
const state = {view:'products',creating:false,step:0,category:'drink',template:null,values:{},drafts:{},custom:[],query:'',products:structuredClone(fixtures),history:[],labelTab:'find',role:'manager',employeePrint:false,offline:false,fail:false,busy:false,errors:{},success:null,dialog:null,selected:null,proposals:[],configCategory:'drink',configTemplate:null};
const main = document.querySelector('#main');
const dialog = document.querySelector('#dialog');
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g,character=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
const icon = name => {
  const paths = {box:'<path d="m3 7 9-4 9 4v10l-9 4-9-4Z M3 7l9 4 9-4 M12 11v10 M7 5l10 4"/>',label:'<path d="M3 3h9l9 9-9 9-9-9Z"/><circle cx="8" cy="8" r="1.5"/>',settings:'<path d="M4 6h16 M4 12h16 M4 18h16"/><circle cx="9" cy="6" r="2"/><circle cx="15" cy="12" r="2"/><circle cx="8" cy="18" r="2"/>',search:'<circle cx="10" cy="10" r="6"/><path d="m15 15 5 5"/>',plus:'<path d="M12 4v16 M4 12h16"/>',close:'<path d="m6 6 12 12 M6 18 18 6"/>',check:'<circle cx="12" cy="12" r="9"/><path d="m7 12 3 3 7-7"/>',print:'<path d="M7 8V3h10v5 M7 17H4V9h16v8h-3 M7 14h10v7H7Z"/>',arrow:'<path d="M4 12h16 m-6-6 6 6-6 6"/>'};
  return '<svg class="icon" viewBox="0 0 24 24" aria-hidden="true">'+paths[name]+'</svg>';
};
const button = (text,action,kind='',extra='') => '<button type="button" class="'+kind+'" data-action="'+action+'" '+extra+'>'+text+'</button>';
let noticeTimer;
const notice = text => {clearTimeout(noticeTimer);document.querySelector('#notice').textContent=text;noticeTimer=setTimeout(()=>document.querySelector('#notice').textContent='',3500);};
const canManage = () => state.role==='manager';
const canPrint = () => canManage() || state.employeePrint;
const getTemplate = () => schema.templates.find(template=>template.id===state.template);
const getConfigTemplate = () => schema.templates.find(template=>template.id===state.configTemplate);
const autoSku = () => 'MOCK-SKU-'+String(state.products.length+1).padStart(4,'0');
const autoBarcode = () => 'MOCK-JV-'+String(state.products.length+1).padStart(6,'0');
const activeSku = () => state.values.skuMode==='manual' ? (state.values.sku || '').trim() : autoSku();
const activeBarcode = () => state.values.barcodeMode==='package' ? (state.values.barcode || '').trim() : autoBarcode();
function field(label,name,value,options={}) {
  const error=state.errors[name];
  const attrs='id="f-'+name+'" name="'+name+'" '+(options.required?'required ':'')+'aria-invalid="'+Boolean(error)+'" '+(error?'aria-describedby="err-'+name+'" ':'');
  let control;
  if(options.choices) control='<select '+attrs+'>'+options.choices.map(choice=>{const pair=Array.isArray(choice)?choice:[choice,choice];return '<option value="'+escapeHtml(pair[0])+'" '+(String(value)===String(pair[0])?'selected':'')+'>'+escapeHtml(pair[1])+'</option>';}).join('')+'</select>';
  else control='<input '+attrs+'type="'+(options.type || 'text')+'" value="'+escapeHtml(value)+'" '+(options.type==='number'?'min="0" step="any" inputmode="decimal" ':'')+(options.readonly?'readonly ':'')+(name==='brand'?'list="brand-suggestions" ':'')+'autocomplete="off">';
  return '<label class="field '+(options.wide?'wide':'')+'" for="f-'+name+'"><span>'+escapeHtml(label)+(options.required?' *':' <em>Tùy chọn</em>')+'</span>'+control+(error?'<span class="error" id="err-'+name+'">'+escapeHtml(error)+'</span>':'')+(options.hint?'<span class="hint">'+escapeHtml(options.hint)+'</span>':'')+'</label>';
}
function attributeField(attribute) {
  const value=state.values[attribute.id] ?? '';
  if(attribute.kind==='measure') {
    const error=state.errors[attribute.id];
    return '<div class="field"><label for="f-'+attribute.id+'"><span>'+escapeHtml(attribute.label)+(attribute.required?' *':' · tùy chọn')+'</span></label><div class="measure"><input id="f-'+attribute.id+'" name="'+attribute.id+'" type="number" inputmode="decimal" min="0" step="any" value="'+escapeHtml(value)+'" aria-invalid="'+Boolean(error)+'" '+(error?'aria-describedby="err-'+attribute.id+'"':'')+(attribute.required?' required':'')+' list="choices-'+attribute.id+'"><datalist id="choices-'+attribute.id+'">'+(attribute.choices || []).map(choice=>'<option value="'+choice+'">').join('')+'</datalist><select name="'+attribute.id+'Unit" aria-label="Đơn vị '+escapeHtml(attribute.label)+'">'+attribute.units.map(unit=>'<option '+((state.values[attribute.id+'Unit'] || attribute.unit)===unit?'selected':'')+'>'+unit+'</option>').join('')+'</select></div>'+(error?'<span class="error" id="err-'+attribute.id+'">'+escapeHtml(error)+'</span>':'')+'</div>';
  }
  return field(attribute.label,attribute.id,value,{required:attribute.required,type:['number','integer'].includes(attribute.kind)?'number':'text',choices:attribute.kind==='enum'?[['','Chọn…'],...attribute.choices]:null});
}
function tools() {
  document.querySelector('#demo-tools').innerHTML='<p>Chỉ giả lập trạng thái; không thay quyền tài khoản thật.</p><label>Vai trò demo<select id="demo-role"><option value="manager" '+(canManage()?'selected':'')+'>Quản lý</option><option value="employee" '+(!canManage()?'selected':'')+'>Nhân viên</option></select></label><label class="check"><input id="demo-permission" type="checkbox" '+(state.employeePrint?'checked':'')+'>Cho nhân viên in (giả định)</label><label class="check"><input id="demo-offline" type="checkbox" '+(state.offline?'checked':'')+'>Mất kết nối</label><label class="check"><input id="demo-fail" type="checkbox" '+(state.fail?'checked':'')+'>Mô phỏng lỗi lưu / in</label><p>Tải lại trang sẽ xóa dữ liệu phiên demo.</p>';
}
function render(focus=false) {
  document.querySelector('#notice').textContent='';
  document.querySelector('#nav').innerHTML=[['products','box','Sản phẩm'],['labels','label','Tem sản phẩm'],['manage','settings','Quản lý']].map(([view,symbol,label])=>button(icon(symbol)+label,'navigate',state.view===view?'active':'','data-view="'+view+'" '+(state.view===view?'aria-current="page"':''))).join('');
  tools();
  if(state.view==='products') main.innerHTML=state.creating?creation():productOverview();
  if(state.view==='labels') main.innerHTML=labels();
  if(state.view==='manage') main.innerHTML=management();
  if(state.creating && state.step===0 && state.view==='products') templateResults();
  if(document.querySelector('[name=brand]'))main.insertAdjacentHTML('beforeend','<datalist id="brand-suggestions">'+(schema.brands[state.category] || []).map(brand=>'<option value="'+escapeHtml(brand)+'">').join('')+'</datalist>');
  if(document.querySelector('[name=customUnit]'))document.querySelector('[name=customUnit]').closest('label').insertAdjacentHTML('beforebegin',field('Kiểu dữ liệu','customKind','text',{choices:[['text','Chữ'],['number','Số'],['measure','Số đo + đơn vị']]}));
  if(state.view==='products' && state.creating && state.step===1 && !state.success && getTemplate() && !getTemplate().manual) {
    const similar=state.products.filter(product=>schemaText(product.name).includes(schemaText(getTemplate().name)));
    if(similar.length)main.querySelector('#product-form').insertAdjacentHTML('afterbegin','<div class="callout section"><strong>Kho có hàng tương tự — kiểm tra để tránh tạo trùng</strong>'+similar.map(product=>'<div class="row">'+escapeHtml(product.name)+' '+button('Dùng hàng này để xem/in tem','use-existing','','data-id="'+product.id+'"')+'</div>').join('')+'</div>');
  }
  if(focus) {main.focus({preventScroll:true});window.scrollTo({top:0,behavior:'instant'});}
}
function productRows(products,action) {
  if(!products.length) return '<div class="empty"><h3>Không tìm thấy sản phẩm</h3><p class="muted">Thử tên hoặc mã khác. Không tạo SKU mới chỉ vì tem bị hỏng.</p>'+button('Xóa tìm kiếm','clear-search')+'</div>';
  return products.map(product=>'<div class="product-row"><div><strong>'+escapeHtml(product.name)+'</strong><p>'+escapeHtml(product.sku)+' · '+escapeHtml(product.unit)+'</p><p>'+escapeHtml(product.summary)+'</p>'+(product.barcode?'':'<span class="tag">Thiếu mã gốc</span>')+'</div>'+button(action==='open-label'?'Xem tem':'Chi tiết',action,'','data-id="'+product.id+'"')+'</div>').join('');
}
function productOverview() {
  return '<div class="page-head"><div><h1>Sản phẩm</h1><p class="muted">Tạo một SKU cho một biến thể bán / lấy hàng.</p></div>'+button(icon('plus')+'Tạo sản phẩm','new','primary btn-inline')+'</div><div class="panel"><div class="row between"><h2>Danh sách sản phẩm</h2><span class="tag">'+state.products.length+' bản ghi mẫu</span></div><div class="toolbar"><input name="productSearch" aria-label="Tìm sản phẩm" placeholder="Tìm tên, SKU hoặc barcode"></div><div id="product-results" class="product-list">'+productRows(state.products,'product-detail')+'</div></div>';
}
function creation() {
  if(state.success) return '<div class="panel success">'+icon('check')+'<h1>Đã lưu trong phiên demo</h1><p>'+escapeHtml(state.success.name)+'</p><p class="code">'+escapeHtml(state.success.sku)+'</p><div class="callout">Chưa lưu vào OMS. Sản phẩm chưa có tồn kho; số lô và HSD được nhập khi nhận hàng.</div><div class="row">'+button('In tem mẫu','success-print','primary')+button('Về danh sách','finish')+button('Tạo sản phẩm tiếp','new')+'</div><div class="divider">'+button('Nhận hàng là bước nào?','receipt-help','subtle')+'</div></div>';
  const template=getTemplate();
  return '<div class="page-head"><div><h1>Tạo sản phẩm</h1><p class="muted">'+(template?escapeHtml(template.name)+' · bản nháp được giữ khi quay lại':'Chọn mẫu gần đúng; có thể nhập thủ công.')+'</p></div>'+button('Về danh sách','overview','subtle')+'</div><ol class="stepper" aria-label="Tiến trình">'+['Chọn mẫu','Điền thông tin','Xác nhận'].map((text,index)=>'<li class="'+(index===state.step?'current':index<state.step?'done':'')+'" '+(index===state.step?'aria-current="step"':'')+'><span>'+(index+1)+'</span>'+text+'</li>').join('')+'</ol><div class="panel">'+(state.step===0?chooseScreen():state.step===1?formScreen():confirmation())+'</div>';
}
function chooseScreen() {
  return '<h2>Chọn ngành và mẫu</h2><div class="category-tabs" role="group" aria-label="Ngành hàng">'+schema.categories.map(category=>button(escapeHtml(category.label),'category',state.category===category.id?'selected':'','data-id="'+category.id+'" aria-pressed="'+(state.category===category.id)+'"')).join('')+'</div><label class="search">'+icon('search')+'<input name="templateSearch" aria-label="Tìm mẫu trong ngành đã chọn" placeholder="Tìm mẫu: sữa, áo thun, nồi…" value="'+escapeHtml(state.query)+'"></label><div class="hint">Tìm trong ngành đã chọn. Chọn mẫu để điền thông tin.</div><div id="templates"></div><details class="optional"><summary>Không có mẫu phù hợp?</summary><p class="muted">Chọn mẫu thủ công trong nhóm gần nhất. Có thể thêm thuộc tính và đề xuất mẫu ở bước tiếp theo.</p>'+button('Dùng mẫu thủ công','manual')+'</details>';
}
function templateResults() {
  const category=schema.categories.find(item=>item.id===state.category);
  const normalized=value=>schemaText(value);
  const matches=schema.templates.filter(template=>template.category===state.category && normalized(template.name).includes(normalized(state.query)));
  document.querySelector('#templates').innerHTML=matches.length?category.families.map((family,index)=>{
    const members=matches.filter(template=>template.family===family.id);
    if(!members.length)return '';
    return '<details class="family" '+(state.query || index===0?'open':'')+'><summary>'+escapeHtml(family.label)+' <span class="hint">'+members.length+' mẫu</span></summary><div class="template-grid">'+members.map(template=>button('<span>'+escapeHtml(template.name)+(template.id===state.template?'<small> · đã chọn</small>':'')+'</span>','template','template '+(template.manual?'manual ':'')+(template.id===state.template?'selected':''),'data-id="'+template.id+'"')).join('')+'</div></details>';
  }).join(''):'<div class="empty"><h3>Chưa có mẫu này</h3><p class="muted">Thử từ khóa ngắn hơn hoặc tự nhập sản phẩm.</p>'+button('Xóa tìm kiếm','clear-template')+' '+button('Nhập thủ công','manual','primary')+'</div>';
}
function schemaText(value) {return String(value).normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/đ/g,'d').toLowerCase().replace(/\s+/g,'');}
function matchesSearch(value,query) {const text=schemaText(value),search=schemaText(query);if(text.includes(search))return true;const tokens=search.match(/[a-z]+|\d+/g) || [];return tokens.length>0 && tokens.every(token=>text.includes(token));}
function selectTemplate(id) {
  if(state.template) state.drafts[state.template]={values:structuredClone(state.values),custom:structuredClone(state.custom)};
  state.template=id;const template=getTemplate();const draft=state.drafts[id];
  state.values=draft?draft.values:{name:template.manual?'':template.name,unit:template.units[0],skuMode:'auto',barcodeMode:'internal',expiryPolicy:template.lot===true?'yes':'no',packLevel:'single'};
  state.custom=draft?draft.custom:[];state.step=1;state.errors={};render(true);
}
function formScreen() {
  const template=getTemplate();
  const secondary=['length','width','height','power','configuration','specification'];
  const primary=template.attributes.filter(attribute=>attribute.required || !secondary.includes(attribute.id) || template.manual);
  const extra=template.attributes.filter(attribute=>!primary.includes(attribute));
  return '<form id="product-form" novalidate>'+(Object.keys(state.errors).length?'<div class="callout error error-list" role="alert">Kiểm tra các trường được đánh dấu bên dưới.</div>':'')+'<div class="section"><div class="row between"><h2>Thông tin nhận diện</h2>'+button('Điền ví dụ mẫu','example','subtle')+'</div><div class="grid">'+field('Tên sản phẩm','name',state.values.name,{required:true,wide:true})+(template.brand?field('Thương hiệu','brand',state.values.brand,{hint:'Nhập nếu cần phân biệt sản phẩm. Không mặc định thương hiệu.'}):'')+field('Đơn vị lấy hàng','unit',state.values.unit,{required:true,choices:template.units})+primary.map(attributeField).join('')+field('Quy cách của SKU','packLevel',state.values.packLevel,{required:true,choices:[['single','Đơn chiếc'],['pack','Lốc / hộp nhiều chiếc'],['case','Thùng nhiều chiếc']],hint:'Nếu kho lấy riêng từng chiếc và thùng, tạo mã riêng cho mỗi quy cách.'})+(state.values.packLevel!=='single'?field('Số chiếc trong quy cách này','contained',state.values.contained,{type:'number',required:true,hint:'Định nghĩa quy cách, không phải số lượng nhập kho hoặc số tem.'}):'')+'</div></div><div class="section divider"><h2>Mã sản phẩm</h2><div class="grid">'+field('SKU','skuMode',state.values.skuMode,{required:true,choices:[['auto','Hệ thống cấp (mẫu)'],['manual','Dùng mã SKU kho đang có']]})+field('Nguồn barcode','barcodeMode',state.values.barcodeMode,{required:true,choices:[['internal','Mã nội bộ (mô phỏng Code128)'],['package','Mã có trên bao bì']]})+field('Mã SKU','sku',state.values.skuMode==='manual'?state.values.sku:autoSku(),{required:state.values.skuMode==='manual',readonly:state.values.skuMode!=='manual'})+field('Barcode','barcode',state.values.barcodeMode==='package'?state.values.barcode:autoBarcode(),{required:state.values.barcodeMode==='package',readonly:state.values.barcodeMode!=='package'})+'</div>'+(state.values.barcodeMode==='package'?'<div class="row">'+button('Quét mã · mô phỏng','scan','subtle')+'<span class="hint">Không mở camera trong mockup.</span></div>':'')+'<div class="hint">SKU nhận diện hàng trong kho. Barcode là mã máy quét đọc. Mã MOCK không phải GTIN được GS1 cấp.</div></div><div class="section divider">'+field('Theo dõi HSD theo lô','expiryPolicy',state.values.expiryPolicy,{required:true,choices:[['yes','Có — nhập HSD khi nhận từng lô'],['no','Không / chưa áp dụng']],hint:'Đây là chính sách SKU. Không nhập NSX/HSD hay suy hạn từ ngành hàng tại đây.'})+'</div><details class="optional divider"><summary>Thông tin bổ sung & nâng cao</summary><div class="grid">'+extra.map(attributeField).join('')+field('Ghi chú bảo quản / xử lý','handling',state.values.handling,{wide:true})+'</div><h3 class="divider">Thuộc tính riêng</h3><div id="custom-fields">'+state.custom.map((attribute,index)=>field(attribute.label,'custom-'+index,state.values['custom-'+index],{required:true,hint:attribute.unit || 'Thuộc tính nhận diện'})).join('')+'</div><div class="grid divider">'+field('Tên thuộc tính','customLabel','')+field('Đơn vị (nếu có)','customUnit','')+'</div>'+button('Thêm thuộc tính','add-custom')+'<div class="hint">Không thêm số lô, HSD, NSX hoặc số lượng tồn vào thuộc tính SKU.</div><div class="row divider">'+button('Đề xuất mẫu từ bản nháp','propose')+'</div></details><div class="actions">'+button('Quay lại chọn mẫu','back')+'<div class="row">'+button('Kiểm tra sản phẩm '+icon('arrow'),'review','primary btn-inline')+'</div></div></form>';
}
function validate() {
  const errors={};const values=state.values;const template=getTemplate();
  if(!(values.name || '').trim()) errors.name='Nhập tên để phân biệt sản phẩm.';
  if(!template.units.includes(values.unit))errors.unit='Chọn một đơn vị lấy hàng.';
  template.attributes.forEach(attribute=>{
    const value=values[attribute.id];
    if(attribute.required && !String(value ?? '').trim())errors[attribute.id]='Chọn hoặc nhập '+attribute.label.toLowerCase()+'.';
    if(value!==undefined && value!=='') {
      if(attribute.kind==='measure' && !schema.normalizeMeasure(value,values[attribute.id+'Unit'] || attribute.unit))errors[attribute.id]='Nhập số đo lớn hơn 0, hữu hạn.';
      if(['number','integer'].includes(attribute.kind) && (!Number.isFinite(Number(value)) || Number(value)<=0 || (attribute.kind==='integer' && !Number.isSafeInteger(Number(value)))))errors[attribute.id]='Nhập số dương'+(attribute.kind==='integer'?' nguyên':'')+'.';
      if(attribute.kind==='enum' && !attribute.choices.includes(value))errors[attribute.id]='Chọn giá trị trong danh sách.';
    }
  });
  if(values.packLevel!=='single' && (!Number.isSafeInteger(Number(values.contained)) || Number(values.contained)<2))errors.contained='Nhập số nguyên từ 2 trở lên.';
  const sku=activeSku(),barcode=activeBarcode();
  if(!sku || sku.length>64 || !/^[A-Za-z0-9._-]+$/.test(sku))errors.sku='Dùng 1–64 ký tự chữ không dấu, số, . _ hoặc -.';
  if(state.products.some(product=>product.sku.toUpperCase()===sku.toUpperCase()))errors.sku='SKU đã có. Mở sản phẩm cũ thay vì tạo lại.';
  if(!barcode || barcode.length>64 || !/^[\x21-\x7e]+$/.test(barcode))errors.barcode='Nhập mã 1–64 ký tự ASCII, không có dấu cách.';
  if(state.products.some(product=>product.barcode && product.barcode===barcode))errors.barcode='Barcode đã gắn với sản phẩm khác. Không ghi đè.';
  if(values.barcodeMode==='package' && /^\d+$/.test(barcode) && [8,12,13,14].includes(barcode.length)) {
    const digits=barcode.slice(0,-1).split('').reverse();const sum=digits.reduce((total,digit,index)=>total+Number(digit)*(index%2===0?3:1),0);
    if((10-sum%10)%10!==Number(barcode.slice(-1)))errors.barcode='Sai số kiểm tra EAN/UPC/GTIN. Kiểm tra lại bao bì.';
  }
  state.custom.forEach((attribute,index)=>{
    const value=values['custom-'+index];
    if(!String(value || '').trim())errors['custom-'+index]='Nhập '+attribute.label.toLowerCase()+'.';
    else if(attribute.kind==='number' && (!Number.isFinite(Number(value)) || Number(value)<=0))errors['custom-'+index]='Nhập số dương hữu hạn.';
    else if(attribute.kind==='measure' && !schema.normalizeMeasure(value,attribute.unit))errors['custom-'+index]='Nhập số đo dương và dùng ml, l, g, kg, cm hoặc mm.';
  });
  state.errors=errors;return !Object.keys(errors).length;
}
function draftProduct() {
  const template=getTemplate();
  const summary=template.attributes.map(attribute=>{const value=state.values[attribute.id];if(!value)return '';if(attribute.kind==='measure'){const measure=schema.normalizeMeasure(value,state.values[attribute.id+'Unit'] || attribute.unit);return attribute.label+': '+measure.value+' '+measure.unit;}return attribute.label+': '+value;}).filter(Boolean).concat(state.custom.map((attribute,index)=>attribute.label+': '+state.values['custom-'+index]+(attribute.unit?' '+attribute.unit:''))).join(' · ');
  return {id:'session-'+(state.products.length+1),name:state.values.name.trim(),sku:activeSku(),barcode:activeBarcode(),source:state.values.barcodeMode==='package'?'Mã trên bao bì (chưa xác minh quyền sở hữu)':'Mã nội bộ MOCK',unit:state.values.unit,category:state.category,summary,hasLayout:true,expiryPolicy:state.values.expiryPolicy==='yes',packLevel:state.values.packLevel,contained:state.values.contained,brand:state.values.brand || '',handling:state.values.handling || '',attributes:structuredClone(state.values),custom:structuredClone(state.custom)};
}
function labelPreview(product) {
  return '<div class="preview" aria-label="Bản xem bố cục tem"><strong>'+escapeHtml(product.name)+'</strong>'+(product.brand?'<p class="hint">'+escapeHtml(product.brand)+'</p>':'')+'<p class="hint">'+escapeHtml(product.unit)+' · '+escapeHtml(product.summary)+'</p><div class="barcode-space">Vùng barcode · chưa mã hóa để quét</div><div class="code">'+escapeHtml(product.barcode || 'Không có mã gốc')+'</div><p class="hint">SKU: '+escapeHtml(product.sku)+'</p></div>'+(product.handling?'<p class="hint">Xử lý / bảo quản: '+escapeHtml(product.handling)+'</p>':'')+'<p class="hint">Bố cục mẫu 50 × 30 mm. Chưa kiểm tra kích thước in vật lý.</p>';
}
function confirmation() {
  const product=draftProduct();
  return '<h2>Kiểm tra trước khi lưu</h2><div class="review-grid"><div><dl class="keyvalues"><dt>Tên</dt><dd>'+escapeHtml(product.name)+'</dd><dt>SKU</dt><dd class="code">'+escapeHtml(product.sku)+'</dd><dt>Nguồn SKU</dt><dd>'+(state.values.skuMode==='manual'?'Mã kho nhập':'Hệ thống cấp — mô phỏng')+'</dd><dt>Barcode</dt><dd class="code">'+escapeHtml(product.barcode)+'</dd><dt>Nguồn mã</dt><dd>'+escapeHtml(product.source)+'</dd><dt>Quy cách</dt><dd>'+escapeHtml(product.unit)+(product.packLevel!=='single'?' · '+escapeHtml(product.contained)+' chiếc':' · đơn chiếc')+'</dd><dt>Thuộc tính</dt><dd>'+escapeHtml(product.summary || 'Chưa có thuộc tính bổ sung')+'</dd><dt>HSD</dt><dd>'+(product.expiryPolicy?'Theo dõi theo từng lô':'Không / chưa áp dụng')+'</dd></dl><div class="callout">Lưu sản phẩm không cộng tồn kho, không tạo lô, không in tem tự động.</div><details class="optional"><summary>Về mã và hạn sử dụng</summary><p class="muted">Kiểm tra mã trùng chỉ trên dữ liệu mẫu. Check digit đúng không chứng minh GS1 đã cấp GTIN. HSD của lô khác thời hạn sau mở nắp (PAO).</p></details></div><div><h3>Tem sản phẩm</h3>'+labelPreview(product)+'</div></div>'+(state.offline?'<div class="callout error" role="alert">Đang offline. Bản nháp được giữ; bật lại kết nối trong Công cụ demo để thử lưu.</div>':'')+(state.errors.save?'<div class="callout error" role="alert">'+escapeHtml(state.errors.save)+'</div>':'')+'<div class="actions">'+button('Sửa thông tin','back','',''+(state.busy?'disabled':''))+'<div class="row">'+button(state.busy?'Đang lưu…':'Lưu sản phẩm · demo','save','primary',(state.busy || state.offline?'disabled':''))+'</div></div>';
}
function labels() {
  return '<div class="page-head"><div><h1>Tem sản phẩm</h1><p class="muted">Tìm hàng đã có để in hoặc thay tem bị hỏng.</p></div></div><div class="tabs">'+button('Tìm & in tem','label-tab',state.labelTab==='find'?'active':'','data-tab="find"')+button('Lịch sử in','label-tab',state.labelTab==='history'?'active':'','data-tab="history"')+'</div><div class="panel">'+(state.labelTab==='find'?'<h2>Chọn sản phẩm</h2><div class="toolbar"><input name="labelSearch" aria-label="Tìm tem theo tên SKU barcode" placeholder="Tên sản phẩm, SKU hoặc barcode"></div><div class="hint">Tem hỏng: in lại mã cũ. Thiếu dữ liệu: cần xác minh, không tự đổi mã.</div><div id="label-results">'+productRows(state.products,'open-label')+'</div>':historyScreen())+'</div>';
}
function historyScreen() {
  return '<h2>Lịch sử trong phiên demo</h2><div class="toolbar"><input name="historySearch" aria-label="Tìm lịch sử" placeholder="Tìm tên hoặc mã"><select name="historyFilter" aria-label="Trạng thái lịch sử"><option value="all">Mọi trạng thái</option><option value="success">Đã mô phỏng in</option><option value="failed">Lỗi in</option></select></div><div id="history-results">'+historyRows(state.history)+'</div>';
}
function historyRows(items) {
  if(!items.length)return '<div class="empty"><h3>Chưa có lượt in phù hợp</h3><p class="muted">Lịch sử xuất hiện sau thao tác in thử. Không có tem nào được in thật.</p>'+button('Tìm sản phẩm để in','label-tab','primary','data-tab="find"')+'</div>';
  return items.slice().reverse().map(item=>'<div class="history-item"><div><strong>'+escapeHtml(item.name)+'</strong><p>'+escapeHtml(item.sku)+' · '+item.copies+' tem giấy · '+escapeHtml(item.time)+'</p><span class="tag">'+(item.status==='failed'?'Lỗi in mô phỏng':'Đã mô phỏng in · chưa in thật')+'</span></div>'+button('Chi tiết','history-detail','','data-id="'+item.id+'"')+'</div>').join('');
}
function management() {
  if(!canManage())return '<div class="panel empty"><h1>Chỉ dành cho quản lý</h1><p class="muted">Nhân viên vẫn được tra cứu sản phẩm. Quyền thật sẽ do tài khoản và backend quyết định.</p>'+button('Về sản phẩm','navigate','primary','data-view="products"')+'</div>';
  const template=getConfigTemplate();
  const config='<div class="grid divider">'+field('Ngành','configCategory',state.configCategory,{choices:schema.categories.map(category=>[category.id,category.label])})+field('Chọn mẫu','configTemplate',state.configTemplate || '',{choices:[['','Chọn mẫu'],...schema.templates.filter(item=>item.category===state.configCategory).map(item=>[item.id,item.name+' · '+item.familyLabel])]})+'</div><details class="optional"><summary>Gợi ý thương hiệu của ngành</summary><p class="muted">'+escapeHtml(schema.brands[state.configCategory].join(', ') || 'Chưa có gợi ý')+'</p>'+field('Thương hiệu mới','configBrand','')+button('Thêm gợi ý · demo','brand-add')+'</details>';
  const attributes=template?'<div class="table-wrap divider"><table><thead><tr><th>Thuộc tính</th><th>Trong form</th><th>Lựa chọn hiện có</th></tr></thead><tbody>'+template.attributes.map(attribute=>'<tr><td>'+escapeHtml(attribute.label)+'</td><td>'+(attribute.required?'Bắt buộc theo mẫu':'Tùy chọn')+'</td><td>'+escapeHtml((attribute.choices || []).join(', ') || 'Nhập tự do')+'</td></tr>').join('')+'</tbody></table></div><div class="grid divider">'+field('Thuộc tính cần thêm lựa chọn','configAttribute','',{choices:[['','Chọn thuộc tính'],...template.attributes.filter(attribute=>attribute.choices).map(attribute=>[attribute.id,attribute.label])]})+field('Giá trị mới','configValue','')+'</div>'+button('Thêm lựa chọn · demo','config-add'):'';
  const proposals=state.proposals.length?state.proposals.map((item,index)=>'<div class="product-row"><div><strong>'+escapeHtml(item.name)+'</strong><p>'+escapeHtml(item.category)+' · '+escapeHtml(item.note)+'</p><span class="tag">Nháp phiên · chưa xuất bản</span></div>'+button('Dùng lại nháp','reuse-proposal','','data-index="'+index+'"')+'</div>').join(''):'<div class="empty"><p class="muted">Chưa có đề xuất. Từ bản nháp sản phẩm, mở Nâng cao → Đề xuất mẫu.</p></div>';
  return '<div class="page-head"><div><h1>Quản lý danh mục</h1><p class="muted">Cấu hình và đề xuất chỉ có hiệu lực trong phiên demo.</p></div></div><div class="stack"><div class="panel"><h2>Mẫu và thuộc tính</h2><p class="hint">Giữ toàn bộ '+schema.templates.length+' mẫu và ID của bản cũ. Không đổi ngành hay xóa mẫu.</p>'+config+attributes+'<details class="optional divider"><summary>Quy tắc phân loại cần duyệt</summary><p class="muted">Schema gần nhất đặt chất tẩy rửa trong Khác. Yêu cầu trước muốn gộp vào Đồ gia dụng. Bản này giữ schema để không tự đổi phân loại; sẽ chuyển sau khi bạn duyệt.</p></details></div><div class="panel"><h2>Đề xuất mẫu</h2><div id="proposal-results">'+proposals+'</div></div></div>';
}
function showDialog(title,body,footer='') {
  const wasOpen=dialog.open;
  dialog.innerHTML='<div class="dialog-head"><h2 id="dialog-title">'+title+'</h2>'+button(icon('close'),'close','','aria-label="Đóng"')+'</div>'+body+footer;
  if(!wasOpen)dialog.showModal();
}
function printDialog(product,mode='print') {
  state.selected=product;state.dialog=mode;
  let body='<div class="callout">'+(mode==='reprint'?'In lại cùng SKU/barcode đã lưu; không sinh mã mới.':'In tem cho sản phẩm đã có; không cộng số lượng hàng.')+'</div>'+labelPreview(product);
  if(!product.barcode) {
    showDialog('Thiếu mã gốc',body+'<div class="callout error" role="alert">Không có barcode để phục hồi. Cần tìm mã trên bao bì hoặc dữ liệu đã xác minh. Không tự tạo mã thay cho mã cũ.</div>'+button('Quay lại tìm sản phẩm','close'));return;
  }
  if(!product.hasLayout) {
    showDialog('Khôi phục bố cục tem',body+'<div class="callout">Mã gốc vẫn có. Chỉ thiếu bố cục tem; áp dụng bố cục mẫu và giữ nguyên barcode.</div><div class="actions">'+button('Quay lại','close')+button('Dùng bố cục mẫu','restore','primary')+'</div>');return;
  }
  body+='<form id="print-form" novalidate><div class="grid">'+field('Số tem giấy','copies',1,{type:'number',required:true,hint:'Ví dụ 3 tem ≠ nhập 3 sản phẩm.'})+field('Khổ tem','paper','50x30',{required:true,choices:[['50x30','50 × 30 mm'],['60x40','60 × 40 mm']]})+'</div>'+(mode==='reprint'?'<div class="divider">'+field('Lý do in lại','reason','damaged',{required:true,choices:[['damaged','Tem bị rách / mờ'],['lost','Mất tem'],['other','Lý do khác']]})+'</div>':'')+'<div class="hint">Máy in: giả lập tại máy. Chưa có barcode có thể quét.</div><div id="print-message" role="alert">'+(!canPrint()?'<div class="callout error">Bạn chưa được cấp quyền in. Liên hệ quản lý.</div>':state.offline?'<div class="callout error">Offline: chưa gửi được lệnh in. Thử lại sau khi có kết nối.</div>':'')+'</div><div class="actions">'+button('Hủy','close')+button(mode==='reprint'?'In lại · mô phỏng':'In · mô phỏng','print','primary',(!canPrint() || state.offline?'disabled':''))+'</div></form>';
  showDialog(mode==='reprint'?'In lại tem':'In tem sản phẩm',body);
}
function openLabel(product) {
  state.selected=product;state.dialog='label';
  showDialog('Tem của sản phẩm',labelPreview(product)+'<dl class="keyvalues"><dt>Nguồn mã</dt><dd>'+escapeHtml(product.source)+'</dd><dt>SKU</dt><dd>'+escapeHtml(product.sku)+'</dd></dl><p class="hint">In thêm cho hàng đã có, hoặc thay tem rách/mất và ghi lý do. Cả hai giữ mã gốc.</p><div class="actions">'+button('In thêm tem','begin-print','primary')+button('Thay tem rách / mất','begin-reprint')+'</div>');
}
function syncForm() {
  const form=document.querySelector('#product-form');if(!form)return;
  for(const [name,value] of new FormData(form)) {
    if(['customLabel','customUnit'].includes(name))continue;
    if((name==='sku' && state.values.skuMode!=='manual') || (name==='barcode' && state.values.barcodeMode!=='package'))continue;
    state.values[name]=value;
  }
}
async function save() {
  if(state.busy || state.offline)return;
  if(!validate()){state.step=1;render(true);return;}
  state.busy=true;state.errors={};render();
  await new Promise(resolve=>setTimeout(resolve,400));
  state.busy=false;
  if(state.offline || state.fail) {state.errors.save='Chưa lưu được. Bản nháp còn nguyên; kiểm tra kết nối / tắt lỗi demo rồi thử lại.';render();return;}
  const product=draftProduct();state.products.push(product);state.success=product;render(true);notice('Đã lưu mẫu vào phiên demo; không ghi OMS.');
}
async function print() {
  if(state.busy || !canPrint() || state.offline)return;
  const form=document.querySelector('#print-form');const values=Object.fromEntries(new FormData(form));const copies=Number(values.copies);
  if(!Number.isSafeInteger(copies) || copies<1 || copies>999) {document.querySelector('#print-message').innerHTML='<div class="callout error">Nhập số tem nguyên từ 1 đến 999.</div>';form.elements.copies.focus();return;}
  const product=state.selected;const mode=state.dialog;
  state.busy=true;form.querySelector('[data-action=print]').disabled=true;form.querySelector('[data-action=print]').textContent='Đang mô phỏng in…';form.querySelector('[data-action=close]').disabled=true;
  dialog.querySelector('.dialog-head button').disabled=true;
  await new Promise(resolve=>setTimeout(resolve,400));
  state.busy=false;
  const failed=state.fail || state.offline || !canPrint();
  const entry={id:'history-'+(state.history.length+1),productId:product.id,name:product.name,sku:product.sku,barcode:product.barcode,copies,paper:values.paper,reason:mode==='reprint'?values.reason:'In tem',mode,time:new Date().toLocaleString('vi-VN'),role:state.role,status:failed?'failed':'success'};
  state.history.push(entry);
  if(failed) {
    document.querySelector('#print-message').innerHTML='<div class="callout error">Lỗi in mô phỏng. Mã gốc và số tem vẫn giữ; tắt lỗi demo rồi thử lại.</div>';
    form.querySelector('[data-action=print]').disabled=state.offline || !canPrint();form.querySelector('[data-action=print]').textContent='Thử in lại · mô phỏng';form.querySelector('[data-action=close]').disabled=false;dialog.querySelector('.dialog-head button').disabled=false;return;
  }
  state.dialog='print-success';showDialog('Đã mô phỏng in', '<div class="callout good">'+copies+' tem giấy · barcode giữ nguyên '+escapeHtml(product.barcode)+'. Chưa in ra giấy.</div><div class="actions">'+button('Đóng','close')+button('Xem lịch sử','go-history','primary')+'</div>');notice('Lịch sử đã ghi thao tác mô phỏng; tồn kho không đổi.');
}
document.addEventListener('input',event=>{
  const target=event.target;
  if(target.closest('#product-form') && target.tagName!=='SELECT' && !['customLabel','customUnit'].includes(target.name))syncForm();
  if(target.name==='templateSearch'){state.query=target.value;templateResults();}
  if(['productSearch','labelSearch'].includes(target.name)) {const products=state.products.filter(product=>matchesSearch(product.name+' '+product.sku+' '+product.barcode,target.value));document.querySelector(target.name==='productSearch'?'#product-results':'#label-results').innerHTML=productRows(products,target.name==='productSearch'?'product-detail':'open-label');}
  if(target.name==='historySearch')filterHistory();
});
function filterHistory() {
  const query=schemaText(document.querySelector('[name=historySearch]').value),filter=document.querySelector('[name=historyFilter]').value;
  document.querySelector('#history-results').innerHTML=historyRows(state.history.filter(item=>schemaText(item.name+' '+item.sku+' '+item.barcode).includes(query) && (filter==='all' || filter===item.status)));
}
document.addEventListener('change',event=>{
  const target=event.target;
  if(target.name==='paper') {
    const caption=[...dialog.querySelectorAll('p.hint')].find(item=>item.textContent.startsWith('Bố cục mẫu') || item.textContent.startsWith('Khổ đã chọn'));
    if(caption)caption.textContent='Khổ đã chọn: '+target.selectedOptions[0].textContent+'. Preview chưa mô phỏng tỉ lệ/kích thước in vật lý.';
  }
  if(target.id.startsWith('demo-')) {
    if(target.id==='demo-role')state.role=target.value;
    if(target.id==='demo-permission')state.employeePrint=target.checked;
    if(target.id==='demo-offline')state.offline=target.checked;
    if(target.id==='demo-fail')state.fail=target.checked;
    syncForm();render();notice('Đã thay trạng thái demo.');return;
  }
  if(target.closest('#product-form')) {
    const previousUnit=state.values[target.name];
    const previousSku=state.values.sku,previousBarcode=state.values.barcode;
    const previousSkuMode=state.values.skuMode,previousBarcodeMode=state.values.barcodeMode;
    syncForm();
    if(target.name.endsWith('Unit')) {
      const attribute=getTemplate().attributes.find(item=>item.id+'Unit'===target.name);
      if(attribute) {
        const previous=previousUnit || attribute.unit;const measure=schema.normalizeMeasure(state.values[attribute.id],previous);
        if(measure) {
          const next=schema.normalizeMeasure(1,target.value);
          state.values[attribute.id]=next.dimension===measure.dimension?String(Number((measure.value/next.value).toPrecision(12))):'';
        }
        target.dataset.previous=target.value;
        document.querySelector('[name="'+attribute.id+'"]').value=state.values[attribute.id] || '';
        return;
      }
    }
    if(target.name==='skuMode') {
      if(previousSkuMode==='manual')state.values.manualSku=previousSku;
      if(target.value==='manual')state.values.sku=state.values.manualSku || '';
    }
    if(target.name==='barcodeMode') {
      if(previousBarcodeMode==='package')state.values.packageBarcode=previousBarcode;
      if(target.value==='package')state.values.barcode=state.values.packageBarcode || '';
    }
    if(['skuMode','barcodeMode','packLevel'].includes(target.name)){render();document.querySelector('[name="'+target.name+'"]').focus();}
  }
  if(target.name==='historyFilter')filterHistory();
  if(target.name==='configCategory'){state.configCategory=target.value;state.configTemplate=null;render();}
  if(target.name==='configTemplate'){state.configTemplate=target.value || null;render();}
});
document.addEventListener('submit',event=>event.preventDefault());
dialog.addEventListener('cancel',event=>{if(state.busy)event.preventDefault();});
document.addEventListener('click',event=>{
  const target=event.target.closest('[data-action]');if(!target || target.disabled)return;
  const action=target.dataset.action;
  if(state.busy && action!=='close')return;
  syncForm();
  if(action==='navigate'){state.view=target.dataset.view;render(true);}
  if(action==='new'){state.creating=true;state.success=null;state.step=0;state.query='';state.errors={};render(true);}
  if(action==='overview' || action==='finish'){state.creating=false;render(true);}
  if(action==='category'){state.category=target.dataset.id;state.query='';render();}
  if(action==='template')selectTemplate(target.dataset.id);
  if(action==='manual')selectTemplate(schema.templates.find(template=>template.category===state.category && template.manual).id);
  if(action==='clear-template'){state.query='';render();}
  if(action==='back'){state.step=Math.max(0,state.step-1);state.errors={};render(true);}
  if(action==='review') {if(validate()){state.step=2;render(true);}else{render();document.querySelector('[aria-invalid=true]')?.focus();}}
  if(action==='save')save();
  if(action==='example') {
    if(target.dataset.confirmed!=='yes' && getTemplate().attributes.some(attribute=>String(state.values[attribute.id] || '').trim())) {
      showDialog('Thay dữ liệu bằng ví dụ?', '<p>Bạn đã nhập thông tin. Ví dụ chỉ dành cho thử giao diện; có thể ghi đè thuộc tính đã nhập.</p><div class="actions">'+button('Giữ thông tin của tôi','close')+button('Thay bằng ví dụ','example','primary','data-confirmed="yes"')+'</div>');return;
    }
    if(dialog.open)dialog.close();
    const template=getTemplate();state.values.name=template.manual?'Sản phẩm nhập tay mẫu':template.name;
    template.attributes.forEach(attribute=>{state.values[attribute.id]=attribute.choices?.[0] ?? (attribute.kind==='text'?'MẪU-01':'20');if(attribute.unit)state.values[attribute.id+'Unit']=attribute.unit;});
    if(template.name==='Sữa tươi')state.values.volume='180';
    if(template.name==='Áo thun'){state.values.colour='Đen';state.values.size='M';}
    if(template.name==='Nồi') {state.values.material='Inox';state.values.diameter='24';}
    state.errors={};render();notice('Đã điền dữ liệu minh họa. Kiểm tra theo hàng thật trước khi dùng.');
  }
  if(action==='add-custom') {
    const label=document.querySelector('[name=customLabel]').value.trim(),unit=document.querySelector('[name=customUnit]').value.trim(),kind=document.querySelector('[name=customKind]').value;
    if(!label || /hsd|nsx|hạn|san xuat|sản xuất|lô|lot|số lượng|tồn/i.test(label) || state.custom.some(item=>schemaText(item.label)===schemaText(label))) {notice('Nhập tên thuộc tính SKU không trùng; lô, HSD và tồn kho thuộc nhận hàng.');return;}
    if(kind==='measure' && !schema.normalizeMeasure(1,unit)){notice('Số đo cần đơn vị ml, l, g, kg, cm hoặc mm.');return;}
    state.custom.push({label,unit,kind});render();const details=document.querySelector('details.optional');details.open=true;document.querySelector('[name="custom-'+(state.custom.length-1)+'"]').focus();
  }
  if(action==='propose') {
    showDialog('Đề xuất mẫu riêng','<p class="muted">Giữ mẫu hiện tại, thêm đề xuất trong phiên; không xuất bản danh mục thật.</p><div class="grid divider">'+field('Tên mẫu đề xuất','proposalName',state.values.name,{required:true,wide:true})+field('Ghi chú','proposalNote','',{wide:true})+'</div><div class="actions">'+button('Hủy','close')+button('Gửi đề xuất · demo','proposal-save','primary')+'</div>');
  }
  if(action==='proposal-save') {
    const name=dialog.querySelector('[name=proposalName]').value.trim();if(!name){notice('Nhập tên mẫu.');return;}
    state.proposals.push({name,category:state.category,note:dialog.querySelector('[name=proposalNote]').value.trim(),template:state.template,values:structuredClone(state.values),custom:structuredClone(state.custom)});
    dialog.close();notice('Đề xuất mẫu đang chờ duyệt trong Quản lý.');
  }
  if(action==='scan')showDialog('Quét barcode · mô phỏng','<p class="muted">Camera không hoạt động trong bản này. Chọn kết quả giả lập để kiểm tra form.</p><div class="stack divider">'+button('Mã mẫu có trên bao bì','scan-result','','data-code="4006381333931"')+button('Mã đã thuộc sản phẩm khác','scan-result','','data-code="JV-DEMO-001"')+button('Không đọc được — nhập tay','scan-error')+'</div>');
  if(action==='scan-result'){state.values.barcode=target.dataset.code;dialog.close();render();notice('Đã nhận mã giả lập. Cần kiểm tra với bao bì thật.');}
  if(action==='scan-error'){dialog.close();document.querySelector('[name=barcode]').focus();notice('Không đọc được mã mẫu. Nhập barcode từ bao bì.');}
  if(action==='clear-search'){const input=document.querySelector('[name=productSearch],[name=labelSearch]');input.value='';input.dispatchEvent(new Event('input',{bubbles:true}));input.focus();}
  if(action==='label-tab'){state.labelTab=target.dataset.tab;render(true);}
  if(action==='open-label')openLabel(state.products.find(product=>product.id===target.dataset.id));
  if(action==='use-existing'){state.view='labels';state.labelTab='find';render(true);openLabel(state.products.find(product=>product.id===target.dataset.id));}
  if(action==='product-detail'){const product=state.products.find(item=>item.id===target.dataset.id);state.selected=product;showDialog('Chi tiết sản phẩm','<h3>'+escapeHtml(product.name)+'</h3><dl class="keyvalues"><dt>SKU</dt><dd>'+escapeHtml(product.sku)+'</dd><dt>Đơn vị</dt><dd>'+escapeHtml(product.unit)+'</dd><dt>Thuộc tính</dt><dd>'+escapeHtml(product.summary)+'</dd><dt>HSD</dt><dd>'+(product.expiryPolicy?'Theo từng lô khi nhận hàng':'Không / chưa áp dụng')+'</dd></dl><div class="callout">Không có số lượng tồn kho trong mockup sản phẩm.</div><div class="actions">'+button('Đóng','close')+button('Xem tem','detail-label','primary')+'</div>');}
  if(action==='detail-label')openLabel(state.selected);
  if(action==='begin-print')printDialog(state.selected,'print');
  if(action==='begin-reprint')printDialog(state.selected,'reprint');
  if(action==='success-print')printDialog(state.success,'print');
  if(action==='restore'){if(!canPrint()){notice('Bạn chưa được cấp quyền khôi phục bố cục/in.');return;}state.selected.hasLayout=true;notice('Khôi phục bố cục trong demo, giữ nguyên mã gốc.');printDialog(state.selected,state.dialog);}
  if(action==='close'){if(!state.busy)dialog.close();}
  if(action==='print')print();
  if(action==='go-history'){dialog.close();state.view='labels';state.labelTab='history';render(true);}
  if(action==='history-detail') {const item=state.history.find(entry=>entry.id===target.dataset.id);showDialog('Chi tiết lượt in','<h3>'+escapeHtml(item.name)+'</h3><dl class="keyvalues"><dt>SKU</dt><dd>'+escapeHtml(item.sku)+'</dd><dt>Barcode</dt><dd>'+escapeHtml(item.barcode)+'</dd><dt>Số tem giấy</dt><dd>'+item.copies+'</dd><dt>Khổ tem</dt><dd>'+escapeHtml(item.paper)+'</dd><dt>Thao tác</dt><dd>'+escapeHtml(item.mode)+'</dd><dt>Lý do</dt><dd>'+escapeHtml(item.reason)+'</dd><dt>Vai trò demo</dt><dd>'+escapeHtml(item.role)+'</dd><dt>Thời điểm</dt><dd>'+escapeHtml(item.time)+'</dd><dt>Trạng thái</dt><dd>'+(item.status==='failed'?'Lỗi mô phỏng':'Đã mô phỏng · chưa in thật')+'</dd></dl><div class="actions">'+button('Đóng','close')+'</div>');}
  if(action==='receipt-help')showDialog('Nhận hàng — luồng độc lập','<p>Ở phiếu nhận hàng, chọn SKU đã có → nhập số lượng hàng, số lô, NSX/HSD nếu SKU theo dõi hạn → xác nhận vị trí. Đó không phải số tem giấy.</p><div class="callout divider">Chỉ giải thích quy trình; mockup này không tạo phiếu nhập hay cộng tồn.</div><div class="actions">'+button('Đã hiểu','close','primary')+'</div>');
  if(action==='config-add') {
    const id=document.querySelector('[name=configAttribute]').value,value=document.querySelector('[name=configValue]').value.trim();
    const attribute=getConfigTemplate()?.attributes.find(item=>item.id===id);if(!attribute || !value){notice('Chọn thuộc tính và nhập giá trị mới.');return;}
    const numeric=attribute.kind==='measure';const parsed=numeric?Number(value.replace(',','.')):value;
    if(numeric && (!Number.isFinite(parsed) || parsed<=0)){notice('Giá trị số phải dương và hữu hạn.');return;}
    if(attribute.choices.includes(parsed)){notice('Lựa chọn đã có.');return;}
    attribute.choices.push(parsed);render();notice('Đã thêm lựa chọn trong phiên demo; chưa xuất bản.');
  }
  if(action==='reuse-proposal' && canManage()) {
    const proposal=state.proposals[Number(target.dataset.index)];
    state.category=proposal.category;state.template=proposal.template;state.values=structuredClone(proposal.values);state.custom=structuredClone(proposal.custom);
    state.values.skuMode='auto';state.values.barcodeMode='internal';state.creating=true;state.success=null;state.step=1;state.view='products';state.errors={};render(true);notice('Đã dùng lại nháp; SKU/barcode mới phải kiểm tra trước khi lưu.');
  }
  if(action==='brand-add' && canManage()) {
    const brand=document.querySelector('[name=configBrand]').value.trim();
    if(!brand || schema.brands[state.configCategory].some(value=>schemaText(value)===schemaText(brand))){notice('Nhập thương hiệu mới, không trùng danh sách.');return;}
    schema.brands[state.configCategory].push(brand);render();notice('Thêm gợi ý thương hiệu trong demo; chưa xuất bản.');
  }
});
document.addEventListener('pointerdown',event=>{const demo=document.querySelector('.demo');if(!demo.contains(event.target))demo.open=false;});
render();
