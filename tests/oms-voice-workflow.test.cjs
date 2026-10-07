const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {createApp}=require('../server');
function wav(silent=false){const b=Buffer.alloc(32044);b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(16000,24);b.writeUInt32LE(32000,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(32000,40);if(!silent)for(let i=44;i<b.length;i+=2)b.writeInt16LE(Math.round(Math.sin(i*.1)*8000),i);return b;}
test('multipart voice shares text history, guards concurrency, validates bytes and recovers after failures',async t=>{
 const temp=fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-voice-')),saved={...process.env};
 Object.assign(process.env,{DATABASE_PATH:path.join(temp,'db.sqlite'),NODE_ENV:'test',OMS_SEED_DEMO:'true',OMS_DEMO_PASSWORD:'123456',OPENAI_API_KEY:''});
 const turns=[];let stt=0,mode='ok',release;
 const app=createApp({mobileAi:{daily:100,rpm:100,answer:async context=>{turns.push(context);if(mode==='ai-error')throw new Error('private secret');return {message:'Reply '+context.message,sources:[]};},transcribe:async(bytes,signal)=>{stt++;assert.equal(bytes.length,32044);if(mode==='hold')await new Promise(r=>{release=r;signal.addEventListener('abort',r,{once:true});});if(mode==='stt-error')throw new Error('private provider key');return {text:mode==='empty'?' ':'What did I ask before?'};}}});
 await app.locals.bootstrapPromise;const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
 t.after(async()=>{await new Promise(r=>server.close(r));await new Promise(r=>app.locals.store.db.close(r));fs.rmSync(temp,{recursive:true,force:true});for(const k of Object.keys(process.env))if(!(k in saved))delete process.env[k];Object.assign(process.env,saved);});
 const base='http://127.0.0.1:'+server.address().port+'/api/oms/mobile';
 const json=(route,body,token)=>fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(body)});
 const login=await(await json('/login',{employeeCode:'NV-00101',password:'123456'})).json(),token=login.accessToken;
 const upload=(bytes=wav(),options={})=>{const form=new FormData();if(bytes!==null)form.append(options.field||'audio',new Blob([bytes],{type:options.mime||'audio/wav'}),options.filename||'question.wav');if(options.extra)form.append('employeeId','spoof');if(options.two)form.append('audio',new Blob([wav()],{type:'audio/wav'}),'two.wav');return fetch(base+'/jarvis/voice',{method:'POST',headers:options.anonymous?{}:{Authorization:'Bearer '+token},body:form,signal:options.signal});};
 assert.equal((await upload(wav(),{anonymous:true})).status,401);assert.equal(stt,0);
 for(const [bytes,options,code] of [[null,{},'NO_AUDIO'],[wav(true),{},'NO_AUDIO'],[Buffer.from('RIFFfake'),{},'INVALID_AUDIO'],[wav(),{mime:'image/png'},'INVALID_AUDIO'],[wav(),{filename:'../q.wav'},'INVALID_AUDIO'],[wav(),{field:'file'},'INVALID_AUDIO'],[wav(),{extra:true},'INVALID_AUDIO'],[wav(),{two:true},'INVALID_AUDIO'],[Buffer.alloc(2048045),{},'AUDIO_TOO_LARGE']]){
   const response=await upload(bytes,options);assert.ok(response.status>=400);assert.equal((await response.json()).code,code);
 }
 assert.equal(stt,0);
 await json('/jarvis/chat',{message:'Hello warehouse'},token);
 const voice=await(await upload()).json();assert.equal(voice.success,true);assert.equal(voice.transcript,'What did I ask before?');assert.equal(voice.mode,'openai');assert.equal(turns.length,2);assert.equal(turns[1].history[0].content,'Hello warehouse');
 await json('/jarvis/chat',{message:'Continue please'},token);assert.equal(turns[2].history[2].content,voice.transcript);assert.ok(!JSON.stringify(turns).includes('RIFF'));assert.equal(voice.operationalMutation,false);
 for(const [value,code] of [['empty','EMPTY_TRANSCRIPT'],['stt-error','TRANSCRIPTION_FAILED'],['ai-error','AI_REQUEST_FAILED']]){mode=value;const response=await upload();const error=await response.json();assert.equal(error.code,code);assert.ok(!JSON.stringify(error).includes('private'));}
 mode='hold';const pending=upload();while(!release)await new Promise(r=>setTimeout(r,5));
 assert.equal((await upload()).status,409);assert.equal((await json('/jarvis/chat',{message:'Concurrent'},token)).status,409);release();assert.equal((await pending).status,200);
 release=null;const controller=new AbortController(),cancelled=upload(wav(),{signal:controller.signal}).catch(()=>null);while(!release)await new Promise(r=>setTimeout(r,5));controller.abort();await cancelled;await new Promise(r=>setTimeout(r,50));
 mode='ok';assert.equal((await upload()).status,200);
});
test('voice configuration failure is explicit, no local/model substitute',async t=>{
 const temp=fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-voice-off-')),saved={...process.env};Object.assign(process.env,{DATABASE_PATH:path.join(temp,'db.sqlite'),NODE_ENV:'test',OMS_SEED_DEMO:'true',OMS_DEMO_PASSWORD:'123456',OPENAI_API_KEY:''});
 const app=createApp({mobileAi:{}});await app.locals.bootstrapPromise;const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
 t.after(async()=>{await new Promise(r=>server.close(r));await new Promise(r=>app.locals.store.db.close(r));fs.rmSync(temp,{recursive:true,force:true});for(const k of Object.keys(process.env))if(!(k in saved))delete process.env[k];Object.assign(process.env,saved);});
 const base='http://127.0.0.1:'+server.address().port+'/api/oms/mobile';const login=await(await fetch(base+'/login',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({employeeCode:'NV-00101',password:'123456'})})).json();
 const response=await fetch(base+'/jarvis/voice',{method:'POST',headers:{Authorization:'Bearer '+login.accessToken}});assert.equal(response.status,503);assert.equal((await response.json()).code,'OPENAI_CONFIGURATION_ERROR');
});
