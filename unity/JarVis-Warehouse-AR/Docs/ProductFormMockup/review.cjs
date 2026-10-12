const {chromium} = require('C:/Users/Windows/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
const fs = require('node:fs');
const path = require('node:path');
const result = {version:'five-groups',checks:[],errors:[],externalRequests:[],mutationRequests:[]};
const output = path.join(__dirname,'screenshots');
fs.mkdirSync(output,{recursive:true});
const assert = (condition,label) => {result.checks.push({label,pass:Boolean(condition)});if(!condition) throw new Error(label);};
(async() => {
  const browser=await chromium.launch({headless:true,channel:'msedge'});
  const page=await browser.newPage({viewport:{width:1280,height:1080}});
  page.on('pageerror',error => result.errors.push(error.message));
  page.on('request',request => {
    if(!request.url().startsWith('http://127.0.0.1:4196/') && !request.url().startsWith('data:')) result.externalRequests.push(request.url());
    if(request.method() !== 'GET') result.mutationRequests.push(request.url());
  });
  try {
    await page.goto('http://127.0.0.1:4196');await page.evaluate(() => document.fonts.ready);
    assert(await page.locator('[data-group]').count() === 5,'Five top-level groups, in requested order');
    assert((await page.locator('[data-group]').allTextContents()).join('|') === 'Đồ uống|Quần áo|Đồ gia dụng – dụng cụ|Mỹ phẩm|Khác','Requested category labels and ordering');
    assert(await page.locator('[data-pick]').count() === 3,'Only drink templates visible initially');
    assert(await page.locator('[name=name]').count() === 0,'No form required before choosing a preset');
    await page.locator('[data-group=apparel]').click();
    await page.locator('[data-pick=shirt]').click();await page.locator('#phone0-next').click();
    assert(await page.locator('[name=name]').inputValue() === 'Áo thun cổ tròn','Name filled from shirt template');
    assert(await page.locator('[name=size]').inputValue() === 'M','Size prefilled and pickable');
    assert(!await page.locator('[name=group]').isVisible(),'Technical category controls hidden by default');
    assert(!await page.locator('[name=sku]').isVisible(),'SKU not required in simple view');
    await page.locator('[name=size]').selectOption('L');await page.locator('[name=name]').fill('Áo của tôi');await page.locator('#phone0-next').click();
    assert((await page.locator('.label-preview').textContent()).includes('Áo của tôi · Đen · L'),'Picked variant reaches preview');
    await page.locator('#phone0-back').click();assert(await page.locator('[name=name]').inputValue() === 'Áo của tôi','Back retains typed name');
    await page.locator('[data-change-sample]').click();await page.locator('[data-group=drink]').click();await page.locator('[data-pick=milk]').click();await page.locator('#phone0-next').click();
    assert(await page.locator('[name=measureChoice]').inputValue() === '180|ml','Milk amount prefilled');
    await page.locator('[name=measureChoice]').selectOption('250|ml');
    assert(await page.locator('[name=measureChoice]').inputValue() === '250|ml','Amount selected without typing');
    assert(await page.locator('input[type=date]').count() === 0,'No lot expiry date in product form');
    await page.locator('[data-expiry=unknown]').click();await page.locator('#phone0-next').click();await page.locator('#phone0-next').click();
    assert((await page.locator('[role=alert]').textContent()).includes('kiểm tra bao bì'),'Unconfirmed expiry not treated as confirmed');
    await page.locator('#phone0-back').click();await page.locator('[data-expiry=yes]').click();
    await page.locator('summary').filter({hasText:'Kho đã có mã?'}).click();await page.locator('[name=barcode]').fill('0123456789012');await page.locator('#phone0-next').click();
    assert((await page.locator('.label-preview').textContent()).includes('0123456789012'),'Existing barcode retains leading zero');
    await page.locator('#phone0-next').click();assert((await page.locator('[role=status]').textContent()).includes('Chưa gửi dữ liệu'),'No fake save or issued code');
    await page.locator('#phone0-back').click();await page.locator('[name=name]').fill('');await page.locator('#phone0-next').click();
    assert(await page.locator('[role=alert]').count() === 1,'Empty name rejected');
    await page.locator('[name=name]').fill('Sữa của tôi');
    await page.locator('[name=measureChoice]').selectOption('custom');await page.locator('[name=amount]').fill('0');await page.locator('#phone0-next').click();
    assert((await page.locator('[role=alert]').textContent()).includes('lớn hơn 0'),'Invalid custom amount rejected');
    for(const group of ['drink','apparel','household','care','other']) {
      await page.locator('summary').filter({hasText:'Thông tin khác'}).click();
      const previousGroup=await page.locator('[name=group]').inputValue();
      await page.locator('[name=group]').selectOption(group);
      assert(await page.locator('[name=type]').count() === 1,group + ' available in advanced view');
      if(group !== previousGroup) assert(await page.locator('[data-expiry=unknown]').getAttribute('aria-pressed') === 'true',group + ': category change requires new expiry confirmation');
      assert(await page.locator('[name=name]').inputValue() === 'Sữa của tôi',group + ': category change preserves product name');
    }
    await page.locator('[data-change-sample]').click();await page.locator('[data-group=drink]').click();
    assert(await page.locator('[data-pick=milk]').getAttribute('aria-pressed') === 'true','Choosing drink template restores correct category after custom category edit');
    await page.goto('http://127.0.0.1:4196');
    const templates = {drink:['milk','water','juice'],apparel:['shirt','trousers','shoes'],household:['pot','appliance','detergent'],care:['cream','lipstick','shampoo'],other:['general','food','electronics']};
    for(const [group,keys] of Object.entries(templates)) {
      for(const sample of keys) {
        await page.locator('[data-group='+group+']').click();
        assert((await page.locator('[data-pick]').evaluateAll(elements => elements.map(element => element.dataset.pick))).join('|') === keys.join('|'),sample + ': templates scoped to category');
        await page.locator('[data-pick='+sample+']').click();await page.locator('#phone0-next').click();
        assert(Boolean(await page.locator('[name=name]').inputValue()),sample + ': name prefilled');
        assert(await page.locator('input[type=date]').count() === 0,sample + ': no lot dates in SKU form');
        if(sample==='detergent') {
          assert(await page.locator('[name=measureChoice]').inputValue()==='3|l','Detergent quantity prefilled under household');
          assert(await page.locator('[name=material], [name=size]').count()===0,'Detergent does not require pot material or clothing size');
          assert(!(await page.locator('[name=flavour] option').allTextContents()).includes('Có đường'),'Detergent scent choices do not include drink flavour');
        }
        if(sample==='milk') assert(!(await page.locator('[name=flavour] option').allTextContents()).includes('Hương hoa'),'Milk choices do not include detergent scent');
        if(sample==='shoes') assert(await page.locator('[name=size]').inputValue()==='EU 38','Shoe size includes system, not apparel size');
        if(sample==='cream') {
          await page.locator('summary').filter({hasText:'Sau mở nắp'}).click();await page.locator('[name=pao]').selectOption('12M');
          assert(await page.locator('[data-expiry=unknown]').getAttribute('aria-pressed')==='true','PAO does not auto-confirm expiry');
          assert((await page.locator('.optional').filter({hasText:'Sau mở nắp'}).textContent()).includes('Không thay thế HSD'),'PAO explanation separates expiry');
        }
        if(sample==='lipstick') assert(await page.locator('[name=shade]').inputValue()==='Đỏ','Cosmetic shade is prefilled and pickable');
        if(sample==='general') assert(await page.locator('[name=measureChoice], [name=size], [name=model]').count()===0,'General other product has no irrelevant required attributes');
        if(sample==='electronics') {
          await page.locator('[name=model]').fill('');await page.locator('#phone0-next').click();
          assert((await page.locator('[role=alert]').textContent()).includes('model'),'Electronic model missing is rejected');
          await page.locator('[name=model]').fill('MODEL-MAU');
        }
        await page.locator('#phone0-next').click();
        assert(await page.locator('.label-preview').count()===1,sample + ': prefilled form reaches preview');
        await page.locator('#phone0-back').click();await page.locator('[data-change-sample]').click();
      }
    }
    for(const width of [390,412,768,1280]) {
      await page.setViewportSize({width,height:width<500 ? 844 : 1080});await page.goto('http://127.0.0.1:4196');await page.evaluate(() => document.fonts.ready);
      assert(await page.evaluate(() => document.documentElement.scrollWidth<=innerWidth),'No horizontal overflow at '+width);
      assert(await page.locator('#phone0-next').evaluate(element => element.getBoundingClientRect().height>=44),'Touch target at '+width);
      assert(await page.locator('[data-group]').evaluateAll(elements => elements.every(element => element.getBoundingClientRect().height>=44)),'Category touch targets at '+width);
      if(width===390) await page.screenshot({path:path.join(output,'Grouped-Mobile-390.png')});
      await page.locator('#phone0-next').click();
      assert(await page.locator('[name=name]').inputValue() === 'Sữa tươi không đường','Prefilled entry at '+width);
      if(width===390) await page.screenshot({path:path.join(output,'Simple-Mobile-390.png')});
    }
    await page.setViewportSize({width:1440,height:1100});await page.goto('http://127.0.0.1:4196/?flow=1');await page.evaluate(() => document.fonts.ready);
    assert(await page.locator('.phone').count() === 3,'Three simple steps displayed');
    await page.screenshot({path:path.join(output,'Simple-Flow.png'),fullPage:true});
    await page.setViewportSize({width:1920,height:1100});await page.goto('http://127.0.0.1:4196/?board=1');await page.evaluate(() => document.fonts.ready);
    assert(await page.locator('.phone').count() === 6,'Six examples covering five groups and household detergent');
    await page.screenshot({path:path.join(output,'Grouped-Forms.png'),fullPage:true});
    assert(result.errors.length===0,'No browser runtime errors');
    assert(result.externalRequests.length===0,'No external requests');
    assert(result.mutationRequests.length===0,'No mutation requests');
  } catch(error) {result.failure=error.message;process.exitCode=1;}
  finally {fs.writeFileSync(path.join(__dirname,'REVIEW.json'),JSON.stringify(result,null,2));await browser.close();process.stdout.write(JSON.stringify({passed:result.checks.filter(check=>check.pass).length,failed:result.checks.filter(check=>!check.pass).length,failure:result.failure,errors:result.errors,externalRequests:result.externalRequests,mutationRequests:result.mutationRequests}));}
})();
