const symbols = {
  back:'<path d="m14 6-6 6 6 6"/>',
  reset:'<path d="M3 10a9 9 0 1 1 2 8M3 4v6h6"/>',
  next:'<path d="m9 6 6 6-6 6"/>',
  milk:'<path d="M8 3h8l3 5v13H5V8zM8 3v5h11M5 8h3M9 12h6v5H9z"/>',
  shirt:'<path d="m8 4-5 3 3 5 2-1v10h8V11l2 1 3-5-5-3c-1 3-7 3-8 0Z"/>',
  pot:'<path d="M5 9h14v8a3 3 0 0 1-3 3H8a3 3 0 0 1-3-3ZM8 6h8M12 6V3M2 11h3M19 11h3"/>',
  bottle:'<path d="M9 3h6v4l3 4v10H6V11l3-4zM9 3v4h6M6 13h12"/>',
  care:'<path d="M7 10h10v11H7zM8 6h8v4H8zM9 3h6v3H9z"/>',
  box:'<path d="m3 7 9-4 9 4v10l-9 4-9-4zM3 7l9 4 9-4M12 11v10"/>',
  signal:'<path d="M3 20v-3M8 20v-7M13 20V9M18 20V5"/>',
  battery:'<rect x="2" y="6" width="17" height="12" rx="2"/><path d="M22 10v4M5 9h10v6H5z"/>'
};
const icon = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${symbols[name] || symbols.milk}</svg>`;
const escapeText = value => String(value ?? '').replace(/[&<>"']/g, character => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[character]));
const groups = {drink:'Đồ uống',apparel:'Quần áo',household:'Đồ gia dụng – dụng cụ',care:'Mỹ phẩm',other:'Khác'};
const groupDescriptions = {drink:'Sữa, nước uống, nước trái cây',apparel:'Áo, quần; có mẫu giày dép',household:'Dụng cụ, thiết bị, chất tẩy rửa',care:'Chăm sóc da, trang điểm, chăm sóc cá nhân',other:'Hàng khác, thực phẩm, điện tử'};
const groupSymbols = {drink:'milk',apparel:'shirt',household:'pot',care:'care',other:'box'};
const profiles = {
  drink:{types:['Sữa hộp','Nước uống','Nước trái cây','Nước ngọt','Khác'],title:'Quy cách đồ uống',measure:'Dung tích',measureUnit:'ml',extras:[['flavour','Hương vị','']],hint:'Định lượng trong một bao bì không phải số lượng nhập kho. Kiểm tra HSD và bảo quản theo nhãn.'},
  apparel:{types:['Áo thun','Quần','Quần áo khác','Giày dép'],title:'Thuộc tính phiên bản',extras:[['colour','Màu sắc',''],['size','Size','']],hint:'Mỗi màu / cỡ quản lý riêng là một phiên bản riêng. Có thể chọn Một cỡ hoặc Không áp dụng nếu đúng với hàng.'},
  household:{types:['Dụng cụ nấu ăn','Thiết bị gia dụng','Chất tẩy rửa','Khác'],title:'Thuộc tính gia dụng',extras:[['material','Chất liệu','Inox'],['dimension','Kích thước / model','20 cm']],hint:'Chỉ điền thuộc tính giúp phân biệt phiên bản. Không mặc định gia dụng không có HSD.'},
  care:{types:['Chăm sóc da','Trang điểm','Chăm sóc cá nhân','Khác'],title:'Quy cách mỹ phẩm',measure:'Định lượng',measureUnit:'g',extras:[['line','Dòng / phiên bản','']],hint:'12M sau mở nắp không phải HSD của lô. Đối chiếu nhãn; không tự tính ngày hết hạn từ ngày nhận.'},
  other:{types:['Hàng khác','Thực phẩm đóng gói','Điện tử / phụ kiện'],title:'Thông tin nhận diện',extras:[['specification','Quy cách / phiên bản','']],hint:'Mô tả đủ để tránh lấy nhầm hàng. Không ép nhập dung tích hoặc size nếu không phù hợp.'}
};
const samples = {
  milk:{title:'Sữa hộp 180 ml',description:'Đồ uống · có HSD',symbol:'milk',group:'drink',type:'Sữa hộp',name:'Sữa tươi không đường',unit:'Hộp',amount:'180',measureUnit:'ml',flavour:'Không đường',expiry:true},
  water:{title:'Nước uống 500 ml',description:'Chai · dung tích trên nhãn',symbol:'bottle',group:'drink',type:'Nước uống',name:'Nước uống đóng chai',unit:'Chai',amount:'500',measureUnit:'ml',flavour:'Không hương vị',expiry:null},
  juice:{title:'Nước cam 1 l',description:'Hộp · dung tích và hương vị',symbol:'milk',group:'drink',type:'Nước trái cây',name:'Nước trái cây',unit:'Hộp',amount:'1',measureUnit:'l',flavour:'Cam',expiry:null},
  shirt:{title:'Áo thun đen · M',description:'Quần áo · màu và size',symbol:'shirt',group:'apparel',type:'Áo thun',name:'Áo thun cổ tròn',unit:'Cái',colour:'Đen',size:'M',expiry:false},
  trousers:{title:'Quần đen · L',description:'Cái · màu và size',symbol:'shirt',group:'apparel',type:'Quần',name:'Quần dài',unit:'Cái',colour:'Đen',size:'L',expiry:null},
  shoes:{title:'Giày trắng · EU 38',description:'Đôi · màu và hệ size',symbol:'box',group:'apparel',type:'Giày dép',name:'Giày thể thao',unit:'Đôi',colour:'Trắng',size:'EU 38',expiry:null},
  pot:{title:'Nồi inox 20 cm',description:'Gia dụng · dụng cụ',symbol:'pot',group:'household',type:'Dụng cụ nấu ăn',name:'Nồi inox',unit:'Cái',material:'Inox',dimension:'20 cm',expiry:false},
  appliance:{title:'Ấm điện · model mẫu',description:'Cái · model và màu',symbol:'pot',group:'household',type:'Thiết bị gia dụng',name:'Ấm điện',unit:'Cái',model:'MODEL-MAU',configuration:'Trắng',expiry:null},
  detergent:{title:'Nước giặt 3 l',description:'Gia dụng · chất tẩy rửa',symbol:'bottle',group:'household',type:'Chất tẩy rửa',name:'Nước giặt hương hoa',unit:'Chai',amount:'3',measureUnit:'l',flavour:'Hương hoa',expiry:true},
  cream:{title:'Kem dưỡng 50 g',description:'Chăm sóc da · định lượng',symbol:'care',group:'care',type:'Chăm sóc da',name:'Kem dưỡng da',unit:'Hũ',amount:'50',measureUnit:'g',line:'Dưỡng ẩm',expiry:null},
  lipstick:{title:'Son 3.5 g · đỏ',description:'Trang điểm · định lượng và màu',symbol:'care',group:'care',type:'Trang điểm',name:'Son môi',unit:'Thỏi',amount:'3.5',measureUnit:'g',shade:'Đỏ',expiry:null},
  shampoo:{title:'Dầu gội 500 ml',description:'Chăm sóc cá nhân · định lượng và mùi',symbol:'bottle',group:'care',type:'Chăm sóc cá nhân',name:'Dầu gội',unit:'Chai',amount:'500',measureUnit:'ml',scent:'Không mùi',expiry:null},
  general:{title:'Hàng khác · mẫu trống',description:'Tự sửa tên và quy cách',symbol:'box',group:'other',type:'Hàng khác',name:'Sản phẩm mẫu',unit:'Cái',specification:'',expiry:null},
  food:{title:'Bánh quy 250 g',description:'Thực phẩm · định lượng và vị',symbol:'box',group:'other',type:'Thực phẩm đóng gói',name:'Bánh quy',unit:'Gói',amount:'250',measureUnit:'g',flavour:'Vani',expiry:null},
  electronics:{title:'Phụ kiện điện tử · model mẫu',description:'Cái · model và phiên bản',symbol:'box',group:'other',type:'Điện tử / phụ kiện',name:'Phụ kiện điện tử',unit:'Cái',model:'MODEL-MAU',configuration:'Đen',expiry:null}
};
const groupSample = group => Object.keys(samples).find(key => samples[key].group === group);
const boardSamples = ['milk','shirt','pot','detergent','cream','general'];
let selectedSample = 'milk';
let boardMode = new URLSearchParams(location.search).get('board') === '1';
let flowMode = new URLSearchParams(location.search).get('flow') === '1';
const options = (values, selected) => values.map(value => '<option value="' + escapeText(value) + '"' + (value === selected ? ' selected' : '') + '>' + escapeText(value) + '</option>').join('');
let fieldSequence = 0;
const field = (name,label,value,values=null,help='') => {
  const fieldId = 'entry-' + name + '-' + fieldSequence++;
  return '<div class="field"><label for="' + fieldId + '">' + label + '</label>' + (values ? '<select id="' + fieldId + '" name="' + name + '">' + options([...new Set([value,...values])],value) + '</select>' : '<input id="' + fieldId + '" name="' + name + '" value="' + escapeText(value) + '" autocomplete="off">') + (help ? '<small>' + help + '</small>' : '') + '</div>';
};
const extraChoices = {colour:['Đen','Trắng','Xanh','Không áp dụng'],size:['S','M','L','XL','Một cỡ','Không áp dụng'],material:['Inox','Nhôm','Nhựa'],dimension:['18 cm','20 cm','24 cm','28 cm'],shade:['Đỏ','Hồng','Cam','Nude'],scent:['Không mùi','Hương hoa','Hương trái cây'],line:['Dưỡng ẩm','Làm sạch','Chống nắng'],configuration:['Đen','Trắng','Xanh']};
const defaultDraft = sampleKey => ({...samples[sampleKey],sampleKey,step:0,barcode:'',sku:'',brand:'',hasBarcode:'no',expiryChoice:samples[sampleKey].expiry === null ? 'unknown' : samples[sampleKey].expiry ? 'yes' : 'no',error:'',notice:'',storage:'Theo bao bì',pao:'Chưa kiểm tra nhãn',customMeasure:false,customAttributes:false});
const drafts = Object.fromEntries(Object.keys(samples).map(key => [key,defaultDraft(key)]));
const sampleDraft = sampleKey => drafts[sampleKey].group === samples[sampleKey].group && drafts[sampleKey].type === samples[sampleKey].type ? drafts[sampleKey] : defaultDraft(sampleKey);
function mountPhone(host, sampleKey, phoneIndex, initialStep=0) {
  let state = {...sampleDraft(sampleKey),step:initialStep};
  const collect = () => host.querySelectorAll('[name]').forEach(control => {
    if(!['measureChoice','templateSearch'].includes(control.name)) state[control.name] = control.value;
  });
  const keepDraft = () => {collect();drafts[state.sampleKey]={...state};};
  function profile() {
    if(state.group === 'household' && state.type === 'Chất tẩy rửa') return {title:'Chất tẩy rửa',measure:'Định lượng',measureUnit:'l',extras:[['flavour','Mùi / dòng','']],hint:'Nhập lít hoặc kg đúng trên bao bì. HSD và yêu cầu xử lý phải kiểm tra theo nhãn; không tự phân loại hàng nguy hiểm.'};
    if(state.group === 'household' && state.type === 'Thiết bị gia dụng') return {title:'Thiết bị gia dụng',extras:[['model','Model trên nhãn',''],['configuration','Phiên bản / màu','']],hint:'Model giúp tránh lấy nhầm phiên bản. Serial, pin và bảo hành để sau, chưa bắt nhập.'};
    if(state.group === 'care' && state.type === 'Trang điểm') return {...profiles.care,extras:[['shade','Màu / mã màu trên nhãn','']]};
    if(state.group === 'care' && state.type === 'Chăm sóc cá nhân') return {...profiles.care,measureUnit:'ml',extras:[['scent','Mùi / dòng','']]};
    if(state.group === 'other' && state.type === 'Thực phẩm đóng gói') return {measure:'Định lượng',measureUnit:'g',extras:[['flavour','Loại / hương vị','']],hint:'Định lượng và bảo quản theo bao bì. Lô và HSD cụ thể nhập khi nhận hàng.'};
    if(state.group === 'other' && state.type === 'Điện tử / phụ kiện') return {extras:[['model','Model trên nhãn',''],['configuration','Cấu hình / màu','']],hint:'Nhập model / phiên bản nhận diện. Serial, pin, bảo hành và vận chuyển chưa nằm trong mockup này.'};
    return profiles[state.group];
  }
  function attributeChoices(name) {
    if(name === 'flavour') return state.type === 'Chất tẩy rửa' ? ['Hương hoa','Không mùi','Hương trái cây'] : state.type === 'Thực phẩm đóng gói' ? ['Vani','Socola','Nguyên vị'] : ['Không đường','Có đường','Không hương vị','Cam','Táo'];
    if(name === 'size' && state.type === 'Giày dép') return ['EU 36','EU 37','EU 38','EU 39','EU 40','EU 41','Không áp dụng'];
    return extraChoices[name] || null;
  }
  function chosenSummary() {
    const current = profile();
    const attributes = [current.measure ? state.amount + ' ' + state.measureUnit : '',...current.extras.map(([name]) => state[name])].filter(value => value && !state.name.toLocaleLowerCase('vi').includes(value.toLocaleLowerCase('vi')));
    return [state.name,...new Set(attributes)].join(' · ');
  }
  function picker() {
    return '<h3 class="section-heading large-question">Hàng của bạn thuộc nhóm nào?</h3><p class="section-description">Chọn nhóm, rồi chọn mẫu bên dưới. Thông tin sẽ được điền sẵn.</p><div class="group-picker" aria-label="Năm nhóm hàng">' + Object.entries(groups).map(([key,label]) => '<button type="button" data-group="' + key + '" aria-pressed="' + (state.group === key) + '" class="' + (state.group === key ? 'selected' : '') + '">' + label + '</button>').join('') + '</div><h3 class="section-heading template-heading">Chọn mẫu ' + groups[state.group].toLowerCase() + '</h3><p class="plain-help">' + groupDescriptions[state.group] + '</p><div class="sample-list">' + Object.entries(samples).filter(([,sample]) => sample.group === state.group).map(([key,sample]) => '<button type="button" class="sample-pick ' + (state.sampleKey === key ? 'selected' : '') + '" data-pick="' + key + '" aria-pressed="' + (state.sampleKey === key) + '">' + icon(sample.symbol) + '<span><strong>' + sample.title + '</strong><small>' + sample.description + '</small></span><span class="pick-dot" aria-hidden="true"></span></button>').join('') + '</div><p class="plain-help">Mẫu minh họa, không phải hàng thật trong kho. Đối chiếu bao bì trước khi sử dụng.</p><details class="optional"><summary>Không có mẫu phù hợp?</summary><p class="plain-help">Chọn mẫu gần nhất trong nhóm rồi sửa loại và quy cách ở mục “Thông tin khác”. Không dùng nguyên thông tin mẫu cho hàng thật.</p></details>';
  }
  function simpleInformation() {
    const current = profile();
    let attributes = '';
    if(current.measure) {
      const pairs = state.group === 'drink' ? ['180|ml','250|ml','500|ml','1|l'] : state.type === 'Chất tẩy rửa' ? ['2|l','3|l','4|l','3|kg'] : state.group === 'care' ? state.type === 'Trang điểm' ? ['3.5|g','4|g','5|g'] : ['30|g','50|g','100|ml','500|ml'] : ['100|g','250|g','500|g','1|kg'];
      const chosen = state.amount + '|' + state.measureUnit;
      const values = [...new Set([chosen,...pairs])].filter(value => !value.startsWith('|'));
      attributes = '<div class="field"><label for="' + phoneIndex + '-measure">Mỗi ' + escapeText(state.unit.toLowerCase()) + ' chứa bao nhiêu?</label><select id="' + phoneIndex + '-measure" name="measureChoice">' + values.map(value => '<option value="' + value + '"' + (value === chosen ? ' selected' : '') + '>' + value.replace('|',' ') + '</option>').join('') + '<option value="custom"' + (state.customMeasure ? ' selected' : '') + '>Nhập quy cách khác…</option></select></div>';
      if(state.customMeasure) attributes += '<div class="field-grid">' + field('amount','Số trên bao bì',state.amount) + field('measureUnit','Đơn vị ghi trên bao bì',state.measureUnit,['ml','l','g','kg','cái']) + '</div>';
    }
    attributes += current.extras.map(([name,label,fallback]) => field(name,name === 'colour' ? 'Màu nào?' : name === 'size' ? 'Cỡ nào?' : label,state[name] ?? fallback,state.customAttributes ? null : attributeChoices(name))).join('');
    attributes += '<p class="plain-help">' + escapeText(current.hint) + '</p>';
    if(state.group === 'apparel') attributes += '<details class="optional"><summary>Chất liệu (không bắt buộc)</summary>' + field('fabric','Chất liệu trên nhãn',state.fabric || '',['Cotton','Polyester','Vải pha','Chưa kiểm tra nhãn']) + '</details>';
    if(state.group === 'care') attributes += '<details class="optional"><summary>Sau mở nắp (nếu nhãn có ghi)</summary>' + field('pao','Thời hạn dùng sau mở nắp',state.pao,['Chưa kiểm tra nhãn','Không ghi trên nhãn','6M','12M','24M'], 'Ví dụ 12M = 12 tháng sau mở nắp. Không thay thế HSD; không dùng để tính HSD của lô.') + '</details>';
    return '<div class="selected-template"><span>Mẫu đã chọn</span><strong>' + escapeText(samples[state.sampleKey].title) + '</strong><button type="button" data-change-sample>Đổi mẫu</button></div><h3 class="section-heading large-question">Thông tin đã điền sẵn</h3><p class="section-description">Đối chiếu bao bì. Chỉ sửa những chỗ khác với sản phẩm của bạn.</p>' + field('name','Tên trên bao bì',state.name,null,'Ví dụ đang dùng tên mẫu; bạn có thể thay bằng tên hàng thật.') + attributes + '<div class="unit-explanation">Một mã nhận diện <strong>1 ' + escapeText(state.unit.toLowerCase()) + '</strong> của phiên bản này. Không phải số lượng nhập kho.</div><div class="expiry-question"><h3>Bao bì có ghi hạn sử dụng không?</h3><div class="answer-buttons">' + [['yes','Có'],['no','Không'],['unknown','Chưa rõ']].map(([value,label]) => '<button type="button" data-expiry="' + value + '" aria-pressed="' + (state.expiryChoice === value) + '" class="' + (state.expiryChoice === value ? 'selected' : '') + '">' + label + '</button>').join('') + '</div><p>' + (state.expiryChoice === 'yes' ? 'Ngày hết hạn sẽ nhập theo từng lô khi nhận hàng. Không cần điền ngày ở đây.' : state.expiryChoice === 'no' ? 'Không theo dõi HSD cho mẫu này. Không xóa các lô đã có.' : 'Có thể xem mẫu tem, nhưng cần kiểm tra nhãn trước khi lưu hàng thật.') + '</p></div><details class="optional codes"><summary>Kho đã có mã? (không bắt buộc)</summary><p class="plain-help">Nếu chưa có mã, để trống: bản app thật sẽ cấp. Mockup không cấp mã thật.</p>' + field('sku','Mã hàng của kho (SKU)',state.sku) + field('barcode','Mã vạch trên bao bì',state.barcode,null,'Nhập thử, chưa xác minh mã hoặc quyền sở hữu.') + '</details><details class="optional"><summary>Thông tin khác (không bắt buộc)</summary>' + field('group','Nhóm hàng',state.group,Object.keys(groups)) + field('type','Loại hàng',state.type,profiles[state.group].types) + field('unit','Tính hàng theo từng',state.unit,['Cái','Hộp','Chai','Đôi','Gói','Lốc','Thùng']) + field('brand','Thương hiệu',state.brand) + field('storage','Bảo quản',state.storage,['Theo bao bì','Không yêu cầu riêng','Giữ mát theo nhà sản xuất','Đông lạnh theo nhà sản xuất']) + '<button type="button" class="text-button" data-custom-attributes>Nhập màu / size / quy cách khác</button></details>';
  }
  function preview() {
    const bars = Array.from({length:60},(_,index) => '<rect x="' + (12+index*4.4) + '" y="0" width="' + (index%4 === 0 ? 3 : index%3 === 0 ? 2 : 1) + '" height="65"/>').join('');
    return '<h3 class="section-heading large-question">Tem sẽ trông như thế này</h3><p class="section-description">Kiểm tra tên và quy cách. Đây chưa phải tem dùng cho hàng thật.</p><div class="label-preview"><h3>' + escapeText(chosenSummary()) + '</h3><p>Mã hàng: ' + escapeText(state.sku || 'App sẽ cấp khi lưu thật') + '</p><svg class="barcode" viewBox="0 0 290 75" aria-label="Vạch chỉ minh họa, không dùng để quét">' + bars + '</svg><p>' + escapeText(state.barcode || 'CHƯA CẤP MÃ THẬT') + '</p><p class="barcode-note">Vạch minh họa, không dùng để quét.</p></div><dl class="summary-list"><div><dt>Một đơn vị</dt><dd>1 ' + escapeText(state.unit.toLowerCase()) + '</dd></div><div><dt>Hạn sử dụng</dt><dd>' + (state.expiryChoice === 'yes' ? 'Nhập theo lô khi nhận hàng' : state.expiryChoice === 'no' ? 'Không theo dõi HSD' : 'Chưa xác nhận') + '</dd></div><div><dt>Mã hàng</dt><dd>' + (state.sku ? 'Dùng mã đã nhập' : 'Để app cấp, không cần tự đặt') + '</dd></div></dl><p class="hint subsection">Lưu sản phẩm không làm tăng tồn kho. Hàng và HSD của lô được ghi nhận ở phiếu nhập kho.</p>' + (state.notice ? '<p class="hint" role="status">' + escapeText(state.notice) + '</p>' : '');
  }
  function render() {
    drafts[state.sampleKey]={...state};
    if(!boardMode && !flowMode) document.querySelectorAll('[data-family]').forEach(button => button.classList.toggle('active',button.dataset.family === state.group));
    const titles = ['Chọn mẫu sản phẩm','Kiểm tra sản phẩm','Xem mẫu tem'];
    host.innerHTML = '<article class="phone"><div class="statusbar"><span>9:41</span><span class="system-icons">' + icon('signal') + icon('battery') + '</span></div><header class="phone-header"><button class="icon-button" id="' + phoneIndex + '-back" aria-label="' + (state.step ? 'Bước trước' : 'Đặt lại mẫu minh họa') + '">' + icon(state.step ? 'back' : 'reset') + '</button><div><h2>' + titles[state.step] + '</h2><p>Chọn sẵn, kiểm tra lại, không cần nghĩ mã.</p></div><span class="mock-label">Mockup</span></header><nav class="stepper" aria-label="Các bước">' + ['Chọn mẫu','Kiểm tra','Xem tem'].map((label,index) => '<div class="step ' + (index === state.step ? 'current' : index < state.step ? 'complete' : '') + '"' + (index === state.step ? ' aria-current="step"' : '') + '><span><b>' + (index+1) + '</b>' + label + '</span></div>').join('') + '</nav><form class="form-scroll" novalidate>' + (state.step === 0 ? picker() : state.step === 1 ? simpleInformation() : preview()) + '</form><footer class="phone-footer">' + (state.error ? '<p class="error" role="alert">' + escapeText(state.error) + '</p>' : '') + '<button class="primary" id="' + phoneIndex + '-next">' + (state.step === 0 ? 'Dùng mẫu này' : state.step === 1 ? 'Xem mẫu tem' : 'Thử xác nhận') + icon('next') + '</button><p class="footer-note">Mẫu minh họa · Không lưu vào kho thật</p><div class="home-line"></div></footer></article>';
    const groupSelect = host.querySelector('[name=group]');
    if(groupSelect) for(const option of groupSelect.options) option.textContent=groups[option.value];
    host.querySelector('form').addEventListener('submit',event => {event.preventDefault();next();});
    host.querySelector('form').addEventListener('input',event => {if(event.target.tagName === 'INPUT') keepDraft();});
    host.querySelector('#' + phoneIndex + '-next').addEventListener('click',next);
    host.querySelector('#' + phoneIndex + '-back').addEventListener('click',() => {
      if(state.step) {keepDraft();state.step--;state.error='';}
      else {state=defaultDraft(state.sampleKey);drafts[state.sampleKey]={...state};}
      render();
    });
    host.querySelectorAll('[data-pick]').forEach(button => button.addEventListener('click',() => {
      keepDraft();state={...sampleDraft(button.dataset.pick),step:0,error:''};selectedSample=state.sampleKey;render();
    }));
    host.querySelectorAll('[data-group]').forEach(button => button.addEventListener('click',() => {
      keepDraft();const nextSample=groupSample(button.dataset.group);state={...sampleDraft(nextSample),step:0,error:''};selectedSample=state.sampleKey;render();
    }));
    host.querySelectorAll('[data-expiry]').forEach(button => button.addEventListener('click',() => {
      collect();state.expiryChoice=button.dataset.expiry;render();
    }));
    host.querySelector('[data-change-sample]')?.addEventListener('click',() => {keepDraft();state.step=0;render();});
    host.querySelector('[data-custom-attributes]')?.addEventListener('click',() => {collect();state.customAttributes=true;render();});
    host.querySelectorAll('select').forEach(control => control.addEventListener('change',() => {
      const previousGroup=state.group;
      const previousType=state.type;
      collect();
      if(control.name === 'measureChoice') {
        if(control.value === 'custom') state.customMeasure=true;
        else {const [amount,measureUnit]=control.value.split('|');state.amount=amount;state.measureUnit=measureUnit;state.customMeasure=false;}
      }
      if(control.name === 'group' && state.group !== previousGroup) {
        state.type=profiles[state.group].types[0];
        state.amount='';
        state.measureUnit=profiles[state.group].measureUnit || '';
        state.customMeasure=Boolean(profiles[state.group].measure);
        state.customAttributes=true;
        state.expiryChoice='unknown';
        state.pao='Chưa kiểm tra nhãn';
        for(const [name] of profiles[state.group].extras) state[name]='';
      }
      if(control.name === 'type' && state.type !== previousType) {
        const nextProfile=profile();
        state.customAttributes=true;
        state.expiryChoice='unknown';
        if(nextProfile.measure) {state.amount='';state.measureUnit=nextProfile.measureUnit;state.customMeasure=true;}
        for(const [name] of nextProfile.extras) state[name]='';
      }
      state.error='';render();
    }));
  }
  function next() {
    collect();
    if(state.step === 1) {
      if(!state.name.trim()) {state.error='Điền tên trên bao bì, hoặc dùng tên mẫu để thử.';render();host.querySelector('[name=name]').focus();return;}
      const current=profile();
      const amount=Number(String(state.amount).replace(',','.'));
      if(current.measure && (!Number.isFinite(amount) || amount<=0)) {state.error='Số trên bao bì phải lớn hơn 0.';render();return;}
      if(state.group === 'apparel' && (!state.colour?.trim() || !state.size?.trim())) {state.error='Chọn màu và cỡ để phân biệt đúng phiên bản.';render();return;}
      if((state.type === 'Điện tử / phụ kiện' || state.type === 'Thiết bị gia dụng') && !state.model?.trim()) {state.error='Điền model trên nhãn để tránh nhầm phiên bản.';render();return;}
    }
    if(state.step === 2) {
      if(state.expiryChoice === 'unknown') {state.error='Chưa rõ HSD: hãy kiểm tra bao bì trước khi xác nhận. Mockup không lưu dữ liệu.';render();return;}
      state.notice='Bạn đã thử hết luồng. Chưa gửi dữ liệu, chưa cấp mã và chưa lưu vào kho thật.';
      state.error='';render();return;
    }
    keepDraft();state.step++;state.error='';render();
  }
  render();
}
function renderStudio() {
  document.body.classList.toggle('board',boardMode || flowMode);
  document.body.classList.toggle('flow',flowMode);
  for(const containerId of ['examples','mobile-examples']) {
    const container=document.getElementById(containerId);
    container.innerHTML=Object.entries(groups).map(([key,label]) => '<button class="example ' + (key === samples[selectedSample].group && !boardMode && !flowMode ? 'active' : '') + '" data-family="' + key + '">' + (containerId === 'examples' ? icon(groupSymbols[key]) : '') + '<span>' + label + (containerId === 'examples' ? '<small>' + groupDescriptions[key] + '</small>' : '') + '</span></button>').join('');
    container.querySelectorAll('button').forEach(button => button.addEventListener('click',() => {selectedSample=groupSample(button.dataset.family);boardMode=false;flowMode=false;renderStudio();}));
  }
  const phones=document.getElementById('phones');
  phones.innerHTML='';
  const views=flowMode ? [['milk',0,'Chọn nhóm rồi chọn mẫu'],['milk',1,'Kiểm tra thông tin điền sẵn'],['milk',2,'Xem tem trước khi lưu']] : boardMode ? boardSamples.map(key => [key,1,groups[samples[key].group] + ' · ' + samples[key].title]) : [[selectedSample,0,'']];
  views.forEach(([sampleKey,step,captionText],index) => {
    const wrapper=document.createElement('div');
    const host=document.createElement('div');
    wrapper.append(host);
    if(captionText) {const caption=document.createElement('p');caption.className='board-caption';caption.textContent=captionText;wrapper.append(caption);}
    phones.append(wrapper);mountPhone(host,sampleKey,'phone'+index,step);
  });
  document.getElementById('board-toggle').textContent=boardMode || flowMode ? 'Về bản chọn mẫu' : 'Xem các mẫu đã điền sẵn';
}
document.getElementById('board-toggle').addEventListener('click',() => {boardMode=!boardMode;flowMode=false;renderStudio();});
renderStudio();
