const {test}=require('node:test');
const assert=require('node:assert/strict');
const schema=require('./catalog-schema.js');
test('five categories and all requested named/manual templates',()=>{
  assert.deepEqual(schema.categories.map(category=>category.label),['Đồ uống','Thời trang','Đồ gia dụng','Mỹ phẩm','Khác']);
  assert.equal(schema.templates.length,134);
  assert.equal(schema.templates.filter(template=>!template.manual).length,120);
  assert.equal(new Set(schema.templates.map(template=>template.id)).size,134);
  for(const category of schema.categories)for(const family of category.families)assert.equal(schema.templates.filter(template=>template.category===category.id && template.family===family.id && template.manual).length,1);
});
test('brands, colours, count units and manual entries are separate configuration',()=>{
  for(const category of ['drink','fashion','home','care'])assert.equal(schema.brands[category].length,10);
  assert.equal(schema.colours.length,12);
  assert.deepEqual(schema.templates.find(template=>template.name==='Nước suối').units,['Hộp','Chai','Thùng']);
  assert.equal(schema.templates.find(template=>template.name==='Nước giặt').category,'other');
});
test('shoe size is EU and accessory form does not require garment attributes',()=>{
  const shoe=schema.templates.find(template=>template.name==='Giày thể thao');
  assert.deepEqual(shoe.attributes.find(attribute=>attribute.id==='size').choices,Array.from({length:12},(_,index)=>'EU '+(35+index)));
  const accessory=schema.templates.find(template=>template.name==='Túi xách');
  assert.deepEqual(accessory.attributes.map(attribute=>attribute.id),['fabric']);assert.equal(accessory.brand,false);
});
test('pan diameter, oven capacity and dimensions cannot share a generic size',()=>{
  const pan=schema.templates.find(template=>template.name==='Chảo');
  assert.deepEqual(pan.attributes.find(attribute=>attribute.id==='diameter').units,['cm']);
  const oven=schema.templates.find(template=>template.name==='Lò vi sóng');
  assert.deepEqual(oven.attributes.find(attribute=>attribute.id==='capacity').units,['l']);
  assert.deepEqual(oven.attributes.find(attribute=>attribute.id==='length').units,['cm']);
  assert.equal(oven.attributes.some(attribute=>attribute.id==='size'),false);
});
test('fashion and home normal templates have no expiry, cleaning is conditional',()=>{
  for(const template of schema.templates.filter(template=>['fashion','home'].includes(template.category)))assert.equal(template.lot,false);
  assert.equal(schema.templates.find(template=>template.name==='Nước giặt').lot,'optional');
  assert.equal(schema.templates.find(template=>template.name==='Son môi').lot,true);
  assert.equal(schema.templates.find(template=>template.name==='Son môi').attributes.find(attribute=>attribute.id==='cosmetic').unit,'g');
});
test('normalized measurements compare same dimension but never mass with volume',()=>{
  assert.deepEqual(schema.normalizeMeasure(1,'l'),schema.normalizeMeasure(1000,'ml'));
  assert.deepEqual(schema.normalizeMeasure(1,'kg'),schema.normalizeMeasure(1000,'g'));
  assert.notDeepEqual(schema.normalizeMeasure(1,'g'),schema.normalizeMeasure(1,'ml'));
  assert.equal(schema.normalizeMeasure('3,5','g').value,3.5);
  assert.equal(schema.normalizeMeasure(0,'g'),null);assert.equal(schema.normalizeMeasure(-2,'l'),null);
  assert.equal(schema.normalizeMeasure('NaN','ml'),null);assert.equal(schema.normalizeMeasure(1,'unknown'),null);
  assert.equal(schema.normalizeMeasure('1e308','l'),null);
});
test('calendar validation rejects nonexistent dates and respects leap years',()=>{
  assert.equal(schema.validDate('2026-02-30'),false);assert.equal(schema.validDate('2026-02-29'),false);
  assert.equal(schema.validDate('2024-02-29'),true);assert.equal(schema.validDate('2026-13-01'),false);
  assert.equal(schema.validDate(''),false);assert.equal(schema.validDate('08/10/2026'),false);
});
