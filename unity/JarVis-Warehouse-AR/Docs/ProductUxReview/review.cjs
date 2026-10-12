const {chromium} = require('C:/Users/Windows/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const schema = require('../ProductFormMockup/catalog-schema.js');
const result = {checks:[], errors:[], externalRequests:[], mutations:[], screenshots:[]};
const check = (pass,label) => {result.checks.push({label,pass:Boolean(pass)});if(!pass)throw new Error(label);};
const directory = path.join(__dirname,'screenshots');
fs.mkdirSync(directory,{recursive:true});
(async () => {
  const browser = await chromium.launch({headless:true,channel:'msedge'});
  const page = await browser.newPage({viewport:{width:1440,height:1000}});
  page.on('pageerror',error=>result.errors.push(error.message));
  page.on('request',request=>{
    if(!/^http:\/\/127\.0\.0\.1:(4197|4198)\//.test(request.url()) && !request.url().startsWith('data:'))result.externalRequests.push(request.url());
    if(request.method()!=='GET')result.mutations.push(request.url());
  });
  const click = async action => {await page.locator('[data-action="'+action+'"]').first().click();if(action==='example' && await page.locator('#dialog[open] [data-action=example][data-confirmed=yes]').count())await page.locator('#dialog [data-action=example][data-confirmed=yes]').click();};
  const shot = async name => {await page.evaluate(()=>document.fonts.ready);await page.evaluate(()=>window.scrollTo({top:0,behavior:'instant'}));const file=name+'.png';await page.screenshot({path:path.join(directory,file),fullPage:true});result.screenshots.push(file);};
  const start = async () => {await page.goto('http://127.0.0.1:4198/');await page.evaluate(()=>document.fonts.ready);};
  const select = async name => {
    await click('new');const template=schema.templates.find(item=>item.name===name);
    await page.locator('[data-action=category][data-id='+template.category+']').click();
    await page.locator('[name=templateSearch]').fill(name);
    await page.locator('[data-action=template][data-id="'+template.id+'"]').click();
  };
  const nav = async view => page.locator('nav [data-view='+view+']').click();
  const close = async () => page.locator('#dialog [data-action=close]').first().click();
  const tools = async () => {await page.locator('.demo').evaluate(element=>element.open=true);};
  try {
    await page.goto('http://127.0.0.1:4197/catalog.html');await shot('Before-Desktop');
    await start();check(await page.locator('nav button').count()===3,'Exactly three primary destinations');await shot('Overview-1440');
    await page.locator('[name=productSearch]').fill('sữa180ml');check(await page.locator('#product-results .product-row').count()===1,'Novice compact search finds milk');await page.locator('[name=productSearch]').fill('');
    await click('new');check(await page.locator('.stepper li').allTextContents().then(items=>items.length===3 && items[0].includes('Chọn mẫu') && items[1].includes('Điền thông tin') && items[2].includes('Xác nhận')),'Three steps correspond to actual interaction');
    for(const category of schema.categories) {
      await page.locator('[data-action=category][data-id='+category.id+']').click();
      check(await page.locator('[data-action=template]').count()===schema.templates.filter(template=>template.category===category.id).length,'All templates preserved: '+category.id);
    }
    await page.locator('[data-action=category][data-id=drink]').click();await shot('Templates-1440');
    await page.locator('[name=templateSearch]').fill('zzzzz');check(await page.getByText('Chưa có mẫu này',{exact:true}).isVisible(),'Template no-result state');await click('clear-template');
    await click('overview');await select('Sữa tươi');await shot('Form-1440');
    check(await page.locator('[name=quantity],[name=manufacture],[name=expiry],[name=lotCode]').count()===0,'SKU form has no receipt quantity or lot dates');
    check(await page.locator('[name=sku]').inputValue()==='MOCK-SKU-0005','Auto SKU explicitly MOCK');
    await click('review');check(await page.locator('[aria-invalid=true]').count()>0,'Missing required content rejected');
    check(await page.locator('[data-action=use-existing]').count()>0,'Similar existing product suggested before new creation');
    await click('example');await page.locator('[name=volume]').fill('390');await page.locator('main [data-action=example]').click();check(await page.getByText('Thay dữ liệu bằng ví dụ?',{exact:true}).isVisible(),'Example overwrite asks for confirmation');await close();check(await page.locator('[name=volume]').inputValue()==='390','Cancel example overwrite preserves input');await page.locator('[name=volume]').fill('180');await page.locator('[name=volumeUnit]').selectOption('l');check(await page.locator('[name=volume]').inputValue()==='0.18','180ml converts to 0.18l');
    await page.locator('[name=volume]').fill('0');await click('review');check(await page.locator('[name=volume]').getAttribute('aria-invalid')==='true','Nonpositive measurement rejected');
    await page.locator('[name=volume]').fill('0.18');await page.locator('[name=skuMode]').selectOption('manual');await page.locator('[name=sku]').fill('DEMO-SUA-180');await click('review');check(await page.locator('.error').allTextContents().then(items=>items.some(item=>item.includes('SKU đã có'))),'Duplicate SKU blocked');
    await page.locator('[name=sku]').fill('TEST-SUA-180');await page.locator('[name=barcodeMode]').selectOption('package');await click('scan');await page.locator('[data-action=scan-result][data-code="JV-DEMO-001"]').click();await click('review');check(await page.locator('.error').allTextContents().then(items=>items.some(item=>item.includes('Barcode đã gắn'))),'Duplicate scanned barcode blocked');
    await page.locator('[name=barcode]').fill('4006381333932');await click('review');check(await page.locator('.error').allTextContents().then(items=>items.some(item=>item.includes('Sai số kiểm tra'))),'Invalid EAN check digit rejected');
    await page.locator('[name=barcode]').fill('4006381333931');await click('review');check(await page.getByText('Kiểm tra trước khi lưu',{exact:true}).isVisible(),'A: valid product confirmation');await shot('Confirm-1440');
    await click('back');check(await page.locator('[name=sku]').inputValue()==='TEST-SUA-180','Back retains manual SKU');check(await page.locator('[name=volume]').inputValue()==='0.18','Back retains measurement');
    await click('back');await page.locator('[name=templateSearch]').fill('Sữa tươi');await page.locator('[data-action=template][data-id=drink-beverages-sua-tuoi]').click();check(await page.locator('[name=sku]').inputValue()==='TEST-SUA-180','Template revisit restores draft');
    await click('review');await tools();await page.locator('#demo-offline').check();check(await page.locator('[data-action=save]').isDisabled(),'Offline save blocked without draft loss');await shot('Offline-1440');
    await page.locator('#demo-offline').uncheck();await tools();await page.locator('#demo-fail').check();await click('save');await page.getByText('Chưa lưu được.',{exact:false}).waitFor();check(await page.locator('[data-action=save]').isEnabled(),'Save error recoverable');
    await tools();await page.locator('#demo-fail').uncheck();await click('save');await page.getByText('Đã lưu trong phiên demo',{exact:true}).waitFor();check(await page.locator('.success').isVisible(),'A: product success');await shot('Success-1440');
    await click('receipt-help');check(await page.locator('#dialog').textContent().then(text=>text.includes('không tạo phiếu nhập')),'Receiving follow-up explicitly separate');await close();await click('finish');
    await select('Áo thun');await click('example');check(await page.locator('[name=colour]').inputValue()==='Đen' && await page.locator('[name=size]').inputValue()==='M','Fashion identity fields visible and prefilled via explicit example');
    await click('overview');await select('Giày thể thao');check(await page.locator('[name=size] option').allTextContents().then(items=>items.includes('EU 46') && !items.includes('M')),'Shoe sizing preserved');
    await click('overview');await select('Son môi');await click('example');await page.locator('[name=cosmeticUnit]').selectOption('ml');check(await page.locator('[name=cosmetic]').inputValue()==='','No invented mass-volume conversion');
    await click('overview');await select('Nồi');await click('example');check(await page.locator('[name=diameter]').inputValue()==='24','Cookware diameter example');await shot('Pan-1440');await click('overview');
    await nav('labels');await click('label-tab');await page.locator('[data-tab=history]').click();check(await page.getByText('Chưa có lượt in phù hợp',{exact:true}).isVisible(),'D: empty history');
    await page.locator('[data-tab=find]').first().click();await page.locator('[name=labelSearch]').fill('zzzzz');check(await page.getByText('Không tìm thấy sản phẩm',{exact:true}).isVisible(),'Label search empty state');await click('clear-search');
    await page.locator('[data-action=open-label][data-id=milk]').click();await click('begin-print');await page.locator('[name=copies]').fill('0');await click('print');check(await page.locator('#print-message').textContent().then(text=>text.includes('1 đến 999')),'Copies validated separately');
    await page.locator('[name=copies]').fill('3');await page.locator('[name=paper]').selectOption('60x40');check(await page.locator('#dialog').textContent().then(text=>text.includes('Khổ đã chọn: 60 × 40 mm')),'Print size caption follows selection');await shot('Print-1440');await click('print');await page.getByText('Đã mô phỏng in',{exact:true}).waitFor();check(await page.locator('#dialog').textContent().then(text=>text.includes('JV-DEMO-001')),'B: existing product print preserves barcode');await close();
    await tools();await page.locator('#demo-fail').check();await page.locator('[data-action=open-label][data-id=shirt]').click();await click('begin-reprint');await click('print');await page.getByText('Lỗi in mô phỏng.',{exact:false}).waitFor();check(await page.locator('[name=copies]').inputValue()==='1','C: print failure keeps copies');await shot('Print-Failure-1440');await close();await tools();await page.locator('#demo-fail').uncheck();
    await page.locator('[data-action=open-label][data-id=shirt]').click();await click('begin-reprint');await page.locator('[name=copies]').fill('2');await click('print');await page.getByText('Đã mô phỏng in',{exact:true}).waitFor();check(await page.locator('#dialog').textContent().then(text=>text.includes('JV-DEMO-002')),'C: reprint preserves original code');await click('go-history');
    check(await page.locator('.history-item').count()===3,'D: successes and failure logged');await page.locator('[name=historyFilter]').selectOption('failed');check(await page.locator('.history-item').count()===1,'D: failure filter');await click('history-detail');check(await page.locator('#dialog').textContent().then(text=>text.includes('damaged') && text.includes('JV-DEMO-002')),'D: history detail reason and original code');await close();await page.locator('[name=historyFilter]').selectOption('all');await shot('History-1440');
    await page.locator('[data-tab=find]').click();await page.locator('[data-action=open-label][data-id=pan]').click();await click('begin-print');check(await page.getByText('Khôi phục bố cục tem',{exact:true}).isVisible(),'Layout loss separate from paper reprint');await click('restore');check(await page.locator('.preview').textContent().then(text=>text.includes('JV-DEMO-003')),'Layout restoration keeps code');await close();
    await page.locator('[data-action=open-label][data-id=missing]').click();await click('begin-reprint');check(await page.locator('#dialog [data-action=print]').count()===0,'Missing original barcode cannot be invented');await shot('Missing-Code-1440');await close();
    await tools();await page.locator('#demo-role').selectOption('employee');await nav('manage');check(await page.getByText('Chỉ dành cho quản lý',{exact:true}).isVisible(),'Manager-only configuration');await nav('labels');await page.locator('[data-action=open-label][data-id=milk]').click();await click('begin-print');check(await page.locator('#dialog [data-action=print]').isDisabled(),'Employee without capability cannot print');await shot('Permission-1440');await close();
    await tools();await page.locator('#demo-role').selectOption('manager');await nav('products');await click('new');await page.locator('[name=templateSearch]').fill('khong-co');await click('manual');await page.locator('[name=name]').fill('Hàng riêng mẫu');await click('example');
    await page.locator('details.optional').evaluate(element=>element.open=true);await page.locator('[name=customLabel]').fill('HSD');await click('add-custom');check(await page.locator('[name=custom-0]').count()===0,'E: lot date cannot become SKU custom attribute');
    await page.locator('[name=customLabel]').fill('Hình dạng');await click('add-custom');await page.locator('[name=custom-0]').fill('Vuông');await click('review');check(await page.locator('main').textContent().then(text=>text.includes('Hình dạng: Vuông')),'E: manual product custom attribute confirmed');await click('back');
    await page.locator('details.optional').evaluate(element=>element.open=true);await click('propose');await page.locator('[name=proposalName]').fill('Mẫu hàng riêng');await click('proposal-save');await nav('manage');check(await page.getByText('Mẫu hàng riêng',{exact:true}).isVisible(),'E: proposed template visible for approval');
    await page.locator('[name=configCategory]').selectOption('fashion');await page.locator('[name=configTemplate]').selectOption('fashion-tops-ao-thun');await page.locator('[name=configAttribute]').selectOption('colour');await page.locator('[name=configValue]').fill('Xanh test');await click('config-add');check(await page.locator('table').textContent().then(text=>text.includes('Xanh test')),'Manager config adds option in RAM');
    await page.locator('summary').filter({hasText:'Gợi ý thương hiệu'}).click();await page.locator('[name=configBrand]').fill('Thương hiệu thử');await click('brand-add');await shot('Management-1440');
    await nav('products');check(await page.locator('[name=custom-0]').inputValue()==='Vuông','Management navigation does not overwrite creation draft');
    await nav('manage');await click('reuse-proposal');check(await page.locator('[name=custom-0]').inputValue()==='Vuông','Proposed draft reusable within session');await click('overview');
    await select('Áo thun');check(await page.locator('[name=colour] option').allTextContents().then(items=>items.includes('Xanh test')),'Configured enum used by intended template');
    check(await page.locator('#brand-suggestions option[value="Thương hiệu thử"]').count()===1,'New brand suggestion consumed by form');await click('overview');
    await select('Sữa tươi');await click('example');await page.locator('[name=skuMode]').selectOption('auto');await page.locator('[name=barcodeMode]').selectOption('internal');await page.locator('[name=packLevel]').selectOption('case');await page.locator('[name=contained]').fill('1.5');await click('review');check(await page.locator('[name=contained]').getAttribute('aria-invalid')==='true','Packaging count integer validated');await page.locator('[name=contained]').fill('24');await click('review');check(await page.locator('main').textContent().then(text=>text.includes('24 chiếc')),'Packaging definition separate from receipt and copies');await click('back');await page.locator('[name=volumeUnit]').selectOption('l');await page.locator('[name=packLevel]').selectOption('single');await page.locator('[name=volumeUnit]').selectOption('ml');check(await page.locator('[name=volume]').inputValue()==='180','Unit conversion survives conditional rerender');
    await start();
    for(const width of [1440,768,390,360]) {
      await page.setViewportSize({width,height:width<500?844:1000});await start();
      check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Overview no overflow '+width);
      await shot('Overview-'+width);
      await select('Sữa tươi');await click('example');
      check(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth),'Form no overflow '+width);
      check(await page.locator('button,input,select').evaluateAll(elements=>elements.filter(element=>element.checkVisibility()).every(element=>element.getBoundingClientRect().height>=44)),'Visible touch controls >=44px '+width);
      await shot('Form-'+width);await click('review');await shot('Confirm-'+width);
      await nav('labels');await page.locator('[data-action=open-label][data-id=milk]').click();await click('begin-print');
      check(await page.evaluate(()=>document.querySelector('#dialog').scrollWidth<=document.querySelector('#dialog').clientWidth),'Dialog no internal overflow '+width);await shot('Print-'+width);await close();
    }
    await start();await page.keyboard.press('Tab');check(await page.locator('.skip').evaluate(element=>element===document.activeElement),'Keyboard skip link first');await page.keyboard.press('Enter');check(await page.locator('#main').evaluate(element=>element===document.activeElement),'Skip link focuses main');
    await page.locator('[data-action=product-detail]').first().click();await page.keyboard.press('Escape');check(!await page.locator('#dialog').evaluate(element=>element.open),'Escape closes dialog');
    check(result.errors.length===0,'No browser JS errors');check(result.externalRequests.length===0,'No source/data sent externally');check(result.mutations.length===0,'GET-only local mockup');
  } catch(error) {result.failure=error.stack;process.exitCode=1;}
  finally {fs.writeFileSync(path.join(__dirname,'QA_RESULTS.json'),JSON.stringify(result,null,2));await browser.close();console.log(JSON.stringify({passed:result.checks.filter(item=>item.pass).length,total:result.checks.length,failure:result.failure,errors:result.errors}));}
})();
