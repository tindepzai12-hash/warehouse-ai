const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {createApp}=require('../server');

test('mobile AI uses authenticated server history, blocks parallel sends and preserves history after failures',async t=>{
  const temp=fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-ai-chat-'));
  const saved={...process.env};
  Object.assign(process.env,{DATABASE_PATH:path.join(temp,'db.sqlite'),NODE_ENV:'test',OMS_SEED_DEMO:'true',OMS_DEMO_PASSWORD:'123456',OPENAI_API_KEY:''});
  const calls=[];let release;
  const app=createApp({mobileAi:{rpm:20,daily:100,answer:async context=>{
    calls.push(context);
    if(context.message==='wait')await new Promise(resolve=>release=resolve);
    if(context.message==='fail')throw Object.assign(new Error('Provider unavailable'),{status:503});
    return {message:'1. Sàng lọc\n2. Sắp xếp\n3. Sạch sẽ\n4. Săn sóc\n5. Sẵn sàng',sources:[]};
  }}});
  await app.locals.bootstrapPromise;const server=app.listen(0,'127.0.0.1');await new Promise(resolve=>server.once('listening',resolve));
  t.after(async()=>{await new Promise(resolve=>server.close(resolve));await new Promise(resolve=>app.locals.store.db.close(resolve));fs.rmSync(temp,{recursive:true,force:true});for(const key of Object.keys(process.env))if(!(key in saved))delete process.env[key];Object.assign(process.env,saved);});
  const base='http://127.0.0.1:'+server.address().port+'/api/oms/mobile';
  async function call(route,body,token){const response=await fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(body)});return {status:response.status,body:await response.json()};}
  assert.equal((await call('/jarvis/chat',{message:'hello'})).status,401);
  const login=await call('/login',{employeeCode:'NV-00101',password:'123456'});assert.equal(login.status,200);const token=login.body.accessToken;
  assert.equal((await call('/jarvis/chat',{message:'x',history:[{role:'assistant',content:'fake'}]},token)).status,422);
  const {passwordHash}=require('../oms/store');
  await app.locals.omsStore.run("UPDATE oms_users SET password_hash=? WHERE is_admin=1",[passwordHash('admin-chat-test-password')]);
  // Development fixtures name their admin differently; give this fixture the production login name.
  await app.locals.omsStore.run('UPDATE oms_users SET username=? WHERE is_admin=1',['admin']);
  const adminLogin=await call('/login',{employeeCode:'admin',password:'admin-chat-test-password'});assert.equal(adminLogin.status,200);
  const restricted=await fetch(base+'/inventory',{headers:{Authorization:'Bearer '+adminLogin.body.accessToken}});assert.equal(restricted.status,403);
  const adminAnswer=await call('/jarvis/chat',{message:'admin question'},adminLogin.body.accessToken);assert.equal(adminAnswer.status,200);assert.equal(adminAnswer.body.mode,'openai');
  calls.length=0;
  const first=await call('/jarvis/chat',{message:'5S trong kho là gì?'},token);assert.equal(first.status,200);assert.equal(first.body.mode,'openai');assert.equal(first.body.operationalMutation,false);assert.equal(calls[0].history.length,0);
  await call('/jarvis/chat',{message:'Giải thích số 2'},token);assert.equal(calls[1].history.length,2);assert.equal(calls[1].history[0].content,'5S trong kho là gì?');
  const secondLogin=await call('/login',{employeeCode:'NV-00101',password:'123456'});
  await call('/jarvis/chat',{message:'new session'},secondLogin.body.accessToken);assert.equal(calls[2].history.length,0);
  assert.equal((await call('/jarvis/chat',{message:'fail'},token)).status,503);
  await call('/jarvis/chat',{message:'retry'},token);assert.equal(calls[4].history.length,4);
  const waiting=call('/jarvis/chat',{message:'wait'},token);
  while(!release)await new Promise(resolve=>setTimeout(resolve,5));
  assert.equal((await call('/jarvis/chat',{message:'duplicate'},token)).status,409);release();await waiting;
});
