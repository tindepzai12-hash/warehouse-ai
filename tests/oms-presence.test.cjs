const {test}=require('node:test');
const assert=require('node:assert/strict');
const {summary,taipeiDay,presence}=require('../oms/presence');
const row=(start,end=null,lease='2026-10-06T16:20:00.000Z')=>({id:'s',online_started_at:start,online_ended_at:end,lease_until:lease});
test('Taipei midnight splits a continuous session without closing it',()=>{
 const rows=[row('2026-10-06T15:52:00.000Z')];
 assert.equal(summary(rows,'2026-10-06T15:59:59.000Z').todaySeconds,479);
 assert.equal(summary(rows,'2026-10-06T16:00:00.000Z').todaySeconds,0);
 const next=summary(rows,'2026-10-06T16:14:00.000Z');
 assert.equal(next.todaySeconds,840);assert.equal(next.sessionSeconds,1320);assert.equal(next.status,'ONLINE');
 assert.equal(next.day,'2026-10-07');assert.equal(next.sessionId,'s');
});
test('completed sessions accumulate; offline preserves today total; lease limits crash time',()=>{
 const rows=[row('2026-10-06T01:00:00.000Z','2026-10-06T01:10:00.000Z'),row('2026-10-06T02:00:00.000Z',null,'2026-10-06T02:02:00.000Z')];
 const result=summary(rows,'2026-10-06T05:00:00.000Z');
 assert.equal(result.status,'OFFLINE');assert.equal(result.todaySeconds,720);assert.equal(result.sessionSeconds,0);
});
test('Taipei boundaries are independent of host timezone and offset input',()=>{
 const saved=process.env.TZ;process.env.TZ='America/New_York';
 try{assert.deepEqual(taipeiDay('2026-10-06T12:00:00-04:00'),taipeiDay('2026-10-07T00:00:00+08:00'));}
 finally{if(saved===undefined)delete process.env.TZ;else process.env.TZ=saved;}
});
test('presence API: login stays offline, idempotent online across devices, logout stops all',async t=>{
 const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
 const saved={...process.env},temp=fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-presence-'));
 Object.assign(process.env,{DATABASE_PATH:path.join(temp,'db.sqlite'),NODE_ENV:'test',OMS_SEED_DEMO:'true',OMS_DEMO_PASSWORD:'123456',OPENAI_API_KEY:''});
 const app=require('../server').createApp();await app.locals.bootstrapPromise;
 const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
 t.after(async()=>{await new Promise(r=>server.close(r));await new Promise(r=>app.locals.store.db.close(r));fs.rmSync(temp,{recursive:true,force:true});for(const k of Object.keys(process.env))if(!(k in saved))delete process.env[k];Object.assign(process.env,saved);});
 const url='http://127.0.0.1:'+server.address().port+'/api/oms/mobile';
 const call=(route,body,token)=>fetch(url+route,{method:body===undefined?'GET':'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:body===undefined?undefined:JSON.stringify(body)});
 assert.equal((await call('/presence')).status,401);
 const login=async()=> (await(await call('/login',{employeeCode:'NV-00101',password:'123456'})).json()).accessToken;
 const a=await login(),b=await login();
 assert.equal((await(await call('/presence',undefined,a)).json()).status,'OFFLINE');
 assert.equal((await call('/presence',{status:'ONLINE',userId:'spoof'},a)).status,422);
 const [x,y]=await Promise.all([a,b].map(async token=>(await(await call('/presence',{status:'ONLINE'},token)).json())));
 assert.equal(x.sessionId,y.sessionId);
 assert.equal((await(await call('/presence',undefined,b)).json()).sessionId,x.sessionId);
 assert.equal((await app.locals.omsStore.get('SELECT COUNT(*) AS n FROM oms_online_sessions')).n,1);
 await call('/logout',{},a);assert.equal((await(await call('/presence',undefined,b)).json()).status,'OFFLINE');
 assert.equal((await(await call('/presence/heartbeat',{},b)).json()).status,'OFFLINE');
 const before='2026-10-06T15:59:50.000Z',after='2026-10-06T16:00:20.000Z';
 const user=(await app.locals.omsStore.accounts()).find(u=>u.username==='NV-00101');
 await app.locals.omsStore.run('DELETE FROM oms_online_sessions');
 await presence(app.locals.omsStore,user.id,'ONLINE',before);
 assert.equal((await presence(app.locals.omsStore,user.id,'READ',after)).todaySeconds,20);
 assert.equal((await presence(app.locals.omsStore,user.id,'READ','2026-10-06T17:00:00.000Z')).todaySeconds,110);
 // Turning off pauses the daily total; turning on resumes from that total.
 await app.locals.omsStore.run('DELETE FROM oms_online_sessions');
 await presence(app.locals.omsStore,user.id,'ONLINE','2026-10-07T01:00:00.000Z');
 assert.equal((await presence(app.locals.omsStore,user.id,'OFFLINE','2026-10-07T01:01:00.000Z')).todaySeconds,60);
 assert.equal((await presence(app.locals.omsStore,user.id,'READ','2026-10-07T02:00:00.000Z')).todaySeconds,60);
 assert.equal((await presence(app.locals.omsStore,user.id,'ONLINE','2026-10-07T02:00:00.000Z')).todaySeconds,60);
 const resumed=await presence(app.locals.omsStore,user.id,'READ','2026-10-07T02:00:30.000Z');
 assert.equal(resumed.todaySeconds,90);assert.equal(resumed.sessionSeconds,30);
 assert.equal(resumed.streak.todayQualified,true);assert.equal(resumed.streak.currentStreak,1);
 assert.equal((await presence(app.locals.omsStore,user.id,'READ','2026-10-07T16:00:00.000Z')).todaySeconds,0);
});
