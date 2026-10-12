const CatalogSchema = (() => {
  const colours = ['Đen','Trắng','Xám','Đỏ','Xanh dương','Xanh lá','Vàng','Cam','Hồng','Tím','Nâu','Be'];
  const sizes = ['XS','S','M','L','XL','XXL','XXXL','Free Size'];
  const brands = {
    drink:['Coca-Cola','Pepsi','Vinamilk','TH true MILK','La Vie','Aquafina','Sprite','Fanta','7UP','Red Bull'],
    fashion:['Nike','Adidas','Puma','Uniqlo','Zara','H&M',"Levi's",'New Balance','Converse','MLB'],
    home:['Philips','Panasonic','Sharp','Toshiba','Electrolux','Lock&Lock','Sunhouse','Kangaroo','Tefal','Elmich'],
    care:["L'Oréal",'Maybelline','Nivea','Dove','Pantene','Head & Shoulders','Sunsilk','Garnier','La Roche-Posay','CeraVe'],
    other:[]
  };
  const fields = {
    volume:{label:'Dung tích trên nhãn',kind:'measure',dimension:'volume',units:['ml','l'],choices:[180,390,500,1000],unit:'ml',required:true},
    cosmetic:{label:'Định lượng trên nhãn',kind:'measure',dimension:'netContent',units:['ml','g'],choices:[5,10,15,20,30,50,75,100,200,500],unit:'ml',required:true},
    mass:{label:'Khối lượng trên nhãn',kind:'measure',dimension:'mass',units:['g','kg'],choices:[100,250,500,1000],unit:'g',required:true},
    detergent:{label:'Định lượng trên nhãn',kind:'measure',dimension:'netContent',units:['ml','l','g','kg'],choices:[500,1000,2000,3000],unit:'ml',required:true},
    diameter:{label:'Đường kính',kind:'measure',dimension:'length',units:['cm'],choices:[16,18,20,22,24,26,28,30,32],unit:'cm',required:true},
    capacity:{label:'Dung tích chứa',kind:'measure',dimension:'volume',units:['l'],choices:[1,1.5,2,3,5],unit:'l',required:false},
    blade:{label:'Chiều dài lưỡi dao',kind:'measure',dimension:'length',units:['cm'],choices:[12,16,20],unit:'cm',required:true},
    length:{label:'Chiều dài',kind:'measure',dimension:'length',units:['cm'],choices:[],unit:'cm',required:false},
    width:{label:'Chiều rộng',kind:'measure',dimension:'length',units:['cm'],choices:[],unit:'cm',required:false},
    height:{label:'Chiều cao',kind:'measure',dimension:'length',units:['cm'],choices:[],unit:'cm',required:false},
    zones:{label:'Số vùng nấu',kind:'integer',required:true},
    power:{label:'Công suất (W)',kind:'number',required:false},
    flavour:{label:'Hương vị',kind:'enum',choices:['Nguyên bản / không hương vị','Dâu','Cam','Nho','Táo','Chanh'],required:false},
    sugar:{label:'Mức đường',kind:'enum',choices:['Bình thường','Ít đường','Không đường','Đường ăn kiêng'],required:false},
    colour:{label:'Màu sắc',kind:'enum',choices:colours,required:true},
    size:{label:'Size',kind:'enum',choices:sizes,required:true},
    model:{label:'Model / mã phiên bản',kind:'text',required:true},
    configuration:{label:'Thông số / phiên bản',kind:'text',required:false},
    material:{label:'Chất liệu',kind:'enum',choices:['Inox','Nhôm','Gang','Gốm/sứ','Thủy tinh','Nhựa','Gỗ','Silicone','Vật liệu kết hợp'],required:false},
    fabric:{label:'Chất liệu',kind:'enum',choices:['Cotton','Polyester','Vải pha','Da','Nhựa','Kim loại'],required:true},
    shade:{label:'Màu / mã màu',kind:'enum',choices:colours,required:false},
    scent:{label:'Mùi / dòng',kind:'enum',choices:['Không mùi','Hương hoa','Hương trái cây'],required:false},
    specification:{label:'Quy cách / mô tả nhận diện',kind:'text',required:false}
  };
  const categories = [
    {id:'drink',label:'Đồ uống',families:[{id:'beverages',label:'Các loại đồ uống',names:['Nước suối','Nước ngọt có gas','Nước trái cây','Sữa tươi','Sữa hạt','Trà đóng chai','Cà phê đóng chai','Nước tăng lực','Nước thể thao'],attributes:['volume','flavour','sugar'],lot:true,units:['Hộp','Chai','Thùng']}]},
    {id:'fashion',label:'Thời trang',families:[
      {id:'tops',label:'Áo',names:['Áo thun','Áo sơ mi','Áo khoác','Áo len','Áo sweater','Áo hoodie','Áo polo','Áo blazer','Áo cardigan'],attributes:['colour','size'],units:['Cái']},
      {id:'trousers',label:'Quần',names:['Quần dài','Quần ngắn','Quần lửng','Quần jeans','Quần tây','Quần jogger','Quần thể thao','Quần kaki'],attributes:['colour','size'],size:[...sizes,'26','27','28','29','30','31','32','34','36'],units:['Cái']},
      {id:'dresses',label:'Váy',names:['Váy học sinh','Váy công sở','Váy maxi','Váy chữ A','Váy xòe','Chân váy','Váy body','Váy dạ hội'],attributes:['colour','size'],size:['XS','S','M','L','XL','XXL','Free Size'],units:['Cái']},
      {id:'shoes',label:'Giày',names:['Giày cổ cao','Giày cổ thấp','Giày thể thao','Giày chạy bộ','Giày da','Giày cao gót','Giày sandal','Giày boots'],attributes:['colour','size'],size:Array.from({length:12},(_,index)=>'EU '+(35+index)),units:['Đôi']},
      {id:'hats',label:'Mũ',names:['Mũ lưỡi trai','Mũ bucket','Mũ len','Mũ vành rộng','Mũ beret','Mũ snapback'],attributes:['colour','size'],size:['Free Size','S','M','L','XL','Chu vi 54 cm','Chu vi 56 cm','Chu vi 58 cm'],units:['Cái']},
      {id:'accessories',label:'Phụ kiện',names:['Túi xách','Thắt lưng','Khăn quàng','Găng tay','Tất'],attributes:['fabric'],units:['Cái','Đôi'],brand:false}
    ]},
    {id:'home',label:'Đồ gia dụng',families:[
      {id:'kitchen',label:'Dụng cụ nhà bếp',names:['Chảo','Nồi','Dao','Kéo nhà bếp','Thớt','Vá nấu ăn','Muỗng','Đũa','Rổ','Rây lọc','Nồi hấp','Khay nướng','Bình giữ nhiệt'],attributes:['material'],units:['Cái']},
      {id:'appliances',label:'Thiết bị điện gia dụng',names:['Lò vi sóng','Nồi chiên không dầu','Nồi cơm điện','Bếp từ','Lò nướng','Máy xay sinh tố','Máy ép trái cây','Ấm siêu tốc','Máy đánh trứng','Máy pha cà phê'],attributes:['model','material'],units:['Cái']}
    ]},
    {id:'care',label:'Mỹ phẩm',families:[{id:'cosmetics',label:'Mỹ phẩm / chăm sóc',names:['Dầu gội','Dầu xả','Kem ủ tóc','Tinh dầu dưỡng tóc','Keo xịt tóc','Sữa rửa mặt','Nước tẩy trang','Toner','Serum','Kem dưỡng da','Kem chống nắng','Son môi','Sữa tắm','Tẩy tế bào chết','Nước hoa','Kem dưỡng tay'],attributes:['cosmetic'],lot:true,units:['Chai','Tuýp','Hũ','Thỏi']}]},
    {id:'other',label:'Khác',families:[
      {id:'electronics',label:'Điện tử',names:['Điện thoại','Laptop','Máy tính bảng','Tai nghe','Loa','Camera','Tivi','Chuột máy tính','Bàn phím','Bộ sạc'],attributes:['model','configuration','shade'],units:['Cái']},
      {id:'dryFood',label:'Đồ ăn khô',names:['Mì ăn liền','Bánh quy','Ngũ cốc','Gạo','Đậu','Hạt dinh dưỡng','Trái cây sấy','Cà phê','Trà','Gia vị'],attributes:['mass','flavour'],lot:true,units:['Gói','Hộp','Túi']},
      {id:'cleaning',label:'Chất tẩy rửa',names:['Nước rửa chén','Nước giặt','Bột giặt','Nước lau sàn','Nước lau kính','Nước xả vải','Chất tẩy bồn cầu','Chất tẩy đa năng'],attributes:['detergent','scent'],lot:'optional',units:['Chai','Can','Túi','Hộp']},
      {id:'manual',label:'Sản phẩm khác',names:[],attributes:['specification'],lot:'optional',units:['Cái','Hộp','Chai','Gói']}
    ]}
  ];
  const overrides = {
    'Chảo':{attributes:['material','diameter']},
    'Nồi':{attributes:['material','diameter','capacity']},
    'Dao':{attributes:['material','blade']},
    'Thớt':{attributes:['material','length','width'],required:['length','width']},
    'Bình giữ nhiệt':{attributes:['material','capacity']},
    'Lò vi sóng':{attributes:['model','material','capacity','length','width','height'],capacity:[17,20,23,25,28,30,32],required:['capacity']},
    'Nồi chiên không dầu':{attributes:['model','material','capacity'],capacity:[3,4,5,5.5,6,7,8,10],required:['capacity']},
    'Nồi cơm điện':{attributes:['model','material','capacity'],required:['capacity']},
    'Bếp từ':{attributes:['model','material','length','width','zones','power'],required:['length','width']},
    'Ấm siêu tốc':{attributes:['model','material','capacity'],required:['capacity']},
    'Son môi':{attributes:['cosmetic','shade'],cosmetic:[2,3,3.5,4,5],cosmeticUnit:'g'},
    'Bột giặt':{detergentUnit:'g',detergent:[500,1000,2000,3000]}
  };
  const slug = value => value.normalize('NFD').replace(/[\u0300-\u036f]/g,'').replace(/[đĐ]/g,'d').toLowerCase().replace(/[^a-z0-9]+/g,'-');
  const templates = categories.flatMap(category=>category.families.flatMap(family=>[...family.names,'Khác — Nhập thủ công'].map(name=>{
    const manual=name==='Khác — Nhập thủ công';
    const override=overrides[name] || {};
    const attributes=(override.attributes || family.attributes).map(key=>({...fields[key],id:key,choices:override[key] || (key==='size' ? family.size : null) || fields[key].choices,unit:override[key+'Unit'] || fields[key].unit,required:fields[key].required || (override.required || []).includes(key)}));
    return {id:category.id+'-'+family.id+'-'+slug(name),category:category.id,family:family.id,familyLabel:family.label,name,manual,attributes,lot:family.lot || false,units:family.units,brand:family.brand!==false};
  })));
  const units = {ml:{dimension:'volume',factor:1,base:'ml'},l:{dimension:'volume',factor:1000,base:'ml'},g:{dimension:'mass',factor:1,base:'g'},kg:{dimension:'mass',factor:1000,base:'g'},cm:{dimension:'length',factor:10,base:'mm'},mm:{dimension:'length',factor:1,base:'mm'}};
  const normalizeMeasure = (value,unit) => {
    const number=Number(String(value).replace(',','.'));const definition=units[unit];
    return definition && Number.isFinite(number) && number>0 && Number.isFinite(number*definition.factor) ? {value:Number((number*definition.factor).toPrecision(12)),unit:definition.base,dimension:definition.dimension} : null;
  };
  const validDate = value => /^\d{4}-\d{2}-\d{2}$/.test(value) && !Number.isNaN(Date.parse(value+'T00:00:00Z')) && new Date(value+'T00:00:00Z').toISOString().slice(0,10)===value;
  return {version:'proposal-1',categories,templates,fields,brands,colours,normalizeMeasure,validDate};
})();
if(typeof module!=='undefined') module.exports=CatalogSchema;
