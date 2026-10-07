const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { createRequire } = require('node:module');

const runtimeRequire = createRequire('C:/Users/Windows/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/package.json');
const { chromium } = runtimeRequire('playwright');
const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-oms-ui-'));
const { createApp } = require('../server');

test('OMS browser login, warehouse data, task assignment, returns and local JarVis', async t => {
  process.env.DATABASE_PATH = path.join(tempRoot,'warehouse.sqlite3');
  process.env.NODE_ENV = 'test';
  process.env.OMS_DEMO_PASSWORD = '123456';
  process.env.OPENAI_API_KEY = '';
  const app=createApp();
  await app.locals.bootstrapPromise;
  const server=app.listen(0,'127.0.0.1');
  await new Promise(resolve=>server.once('listening',resolve));
  const browser=await chromium.launch({
    headless:true,
    executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'
  });
  t.after(async()=>{
    await browser.close();
    await new Promise(resolve=>server.close(resolve));
    await new Promise(resolve=>app.locals.store.db.close(resolve));
    fs.rmSync(tempRoot,{recursive:true,force:true});
  });
  const page=await browser.newPage({viewport:{width:1440,height:900}});
  const base=`http://127.0.0.1:${server.address().port}/warehouse/`;
  await page.goto(base);
  await page.getByRole('textbox',{name:'Tên đăng nhập'}).fill('JustinandJarVis');
  await page.getByLabel('Mật khẩu').fill('123456');
  await page.getByRole('button',{name:'Đăng nhập'}).click();
  await page.getByRole('heading',{name:'Chào buổi sáng, JustinandJarVis'}).waitFor();
  assert.equal(await page.getByRole('link',{name:/Bắt đầu làm việc/}).count(),1);
  const orb=page.locator('.core-stage');
  const bounds=await orb.boundingBox();
  await page.mouse.move(bounds.x+bounds.width*.8,bounds.y+bounds.height*.35);
  await page.waitForFunction(()=>{
    const value=document.querySelector('.core-stage')?.style.getPropertyValue('--mx');
    return value && value!=='0px';
  });
  await page.getByRole('link',{name:/Hoàn hàng 1/}).click();
  await page.getByRole('heading',{name:'Hàng hoàn',exact:true}).waitFor();
  await page.getByText('RET-00125').waitFor();
  assert.equal(await page.getByText('RET-00125').count()>0,true);
  await page.getByRole('link',{name:'Đơn hàng',exact:true}).click();
  await page.getByText('8938500000012').waitFor();
  assert.equal(await page.getByText('Thiếu 2 sản phẩm').count(),0);
  await page.getByRole('link',{name:'Nhập kho'}).click();
  await page.getByText('RCV-00101').waitFor();
  assert.equal(await page.getByText('Thiếu hàng',{exact:true}).count()>0,true);
  await page.getByRole('link',{name:'Tồn kho'}).click();
  await page.getByLabel('Barcode, SKU, tên, mã lô hoặc vị trí').fill('8938500000012');
  await page.getByRole('button',{name:'Tra cứu'}).click();
  await page.getByText('LOT-A2402').waitFor();
  assert.equal(await page.getByText('LOT-A2401').count()>0,true);
  await page.getByRole('link',{name:'Nhiệm vụ'}).click();
  await page.getByRole('button',{name:'Gợi ý nhân viên'}).click();
  await page.getByText(/^NV-00103$/).waitFor();
  await page.getByRole('button',{name:'Giao việc'}).click();
  await page.getByText('NV-00103').waitFor();
  await page.getByRole('link',{name:'JarVis AI'}).click();
  await page.getByLabel('Câu hỏi cho JarVis').fill('Barcode 8938500000012 có những lô nào?');
  await page.getByRole('button',{name:'Gửi'}).click();
  await page.getByText(/LOT-A2401: 120/).waitFor();
  assert.equal(await page.getByText('InventoryLot · LOT-A2402').count(),1);
  await page.reload();
  await page.getByText(/LOT-A2401: 120/).waitFor();
  await page.getByRole('link',{name:'Cài đặt'}).click();
  await page.getByRole('heading',{name:'Thông tin người dùng'}).waitFor();
  await page.getByRole('combobox',{name:'Ngôn ngữ giao diện'}).selectOption('en');
  await page.getByRole('button',{name:'Lưu ngôn ngữ'}).click();
  await page.getByRole('link',{name:'Dashboard'}).waitFor();
  await page.goto(base+'#/cash');
  await page.getByText('Chưa có sổ tài chính vận hành').waitFor();
  await page.emulateMedia({reducedMotion:'reduce'});
  await page.goto(base+'#/dashboard');
  await page.getByRole('heading',{name:'Chào buổi sáng, JustinandJarVis'}).waitFor();
  assert.equal(await page.locator('.core-orb').evaluate(el=>getComputedStyle(el).animationName),'none');
  const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth+1);
  assert.equal(overflow,false);
  const worker=await browser.newPage({viewport:{width:390,height:844}});
  await worker.goto(base);
  await worker.getByRole('textbox',{name:'Tên đăng nhập'}).fill('NV-00101');
  await worker.getByLabel('Mật khẩu').fill('123456');
  await worker.getByRole('button',{name:'Đăng nhập'}).click();
  await worker.getByRole('heading',{name:'Nhiệm vụ',exact:true}).waitFor();
  assert.equal(await worker.getByRole('link',{name:'Đơn hàng'}).count(),0);
  assert.equal(await worker.getByText('TASK-001').count()>0,true);
  assert.equal(await worker.getByText('TASK-002').count(),0);
  await page.goto(base+'#/chat/NV-00101');
  await page.getByLabel('Tin nhắn').fill('Bản nháp của quản lý');
  await worker.goto(base+'#/chat/NV-00101');
  assert.equal(await worker.getByLabel('Tin nhắn').count(),0);
  const mobileLogin=await fetch(`http://127.0.0.1:${server.address().port}/api/oms/mobile/login`,{
    method:'POST',headers:{'content-type':'application/json'},body:JSON.stringify({employeeCode:'NV-00101',password:'123456'})
  });
  const mobileSession=await mobileLogin.json();
  assert.equal(mobileLogin.status,200);
  const mobileConversations=await fetch(`http://127.0.0.1:${server.address().port}/api/oms/mobile/conversations`,{
    headers:{authorization:`Bearer ${mobileSession.accessToken}`}
  }).then(response=>response.json());
  const sent=await fetch(`http://127.0.0.1:${server.address().port}/api/oms/mobile/conversations/${mobileConversations.conversations[0].id}/messages`,{
    method:'POST',headers:{authorization:`Bearer ${mobileSession.accessToken}`,'content-type':'application/json'},
    body:JSON.stringify({content:'Em đang kiểm tra kệ D.'})
  });
  assert.equal(sent.status,201);
  await page.bringToFront();
  await page.locator('.chat-thread .message').filter({hasText:'Em đang kiểm tra kệ D.'}).waitFor({timeout:16000});
  assert.equal(await page.getByLabel('Tin nhắn').inputValue(),'Bản nháp của quản lý');
  await page.getByLabel('Tin nhắn').fill('Đã nhận, cảm ơn em.');
  await page.getByRole('button',{name:'Gửi'}).click();
  await worker.bringToFront();
  await worker.locator('.chat-thread .message').filter({hasText:'Đã nhận, cảm ơn em.'}).waitFor({timeout:16000});
});
