const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {createApp,providerAnswer}=require('../server');
const {replyLanguage,chatImages,voiceWav,voiceProvider}=require('../oms/chat-media');
const image={mimeType:'image/png',data:'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aJ1cAAAAASUVORK5CYII='};
function wav(seconds=1){const n=16000*seconds*2,b=Buffer.alloc(44+n);b.write('RIFF');b.writeUInt32LE(36+n,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(16000,24);b.writeUInt32LE(32000,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(n,40);return b;}
test('bilingual STT preserves provider words and never forces locale, retries or leaks configuration',async()=>{
  const transcripts=['Tên tôi là gì?','Where is SKU B-03?','Kiểm tra stock location B-03 nhé.'];
  const requests=[];
  const provider=voiceProvider({apiKey:'test-only',baseUrl:'https://example.test/v1'},async(url,options)=>{
    requests.push({url,options});return {ok:true,status:200,json:async()=>({text:transcripts[requests.length-1]})};
  });
  for(const expected of transcripts)assert.equal((await provider.transcribe(wav())).text,expected);
  assert.equal(requests.length,3);
  for(const {url,options} of requests){
    assert.equal(url,'https://example.test/v1/audio/transcriptions');
    assert.equal(options.body.get('model'),'gpt-4o-mini-transcribe');
    assert.equal(options.body.get('response_format'),'json');
    assert.equal(options.body.has('language'),false);
    assert.match(options.body.get('prompt'),/Vietnamese, English/);
    assert.match(options.body.get('prompt'),/JarVis.*SKU.*tồn kho/);
    assert.equal(options.body.get('file').name,'question.wav');
    assert.equal(options.body.get('file').size,wav().length);
  }
  let calls=0;
  const failing=voiceProvider({apiKey:'test-only',baseUrl:'https://example.test/v1',transcriptionModel:'whisper-1'},async(url,options)=>{
    calls++;assert.equal(options.body.get('model'),'whisper-1');return {ok:false,status:403};
  });
  await assert.rejects(failing.transcribe(wav()),e=>e.code==='OPENAI_CONFIGURATION_ERROR'&&!e.message.includes('test-only'));
  assert.equal(calls,1);
  const empty=voiceProvider({apiKey:'test-only',baseUrl:'https://example.test/v1'},async()=>({ok:true,status:200,json:async()=>({text:'  '})}));
  await assert.rejects(empty.transcribe(wav()),e=>e.code==='EMPTY_TRANSCRIPT');
});
test('latest question selects language; malformed/oversized media is rejected',()=>{
  assert.equal(replyLanguage('What is 5S in a warehouse?'),'en');
  assert.equal(replyLanguage('5S trong kho là gì vậy JarVis?','en'),'vi');
  assert.equal(replyLanguage('giai thich 5S'),'vi');
  assert.equal(replyLanguage('Explain this in Vietnamese'),'vi');
  assert.equal(replyLanguage('5S?','en'),'en');
  assert.deepEqual(chatImages([image]),[image]);
  assert.throws(()=>chatImages([{mimeType:'image/jpeg',data:image.data}]),/JPEG or PNG/);
  assert.throws(()=>chatImages([image,image]),/at most one/);
  assert.throws(()=>chatImages([{...image,data:'a'.repeat(3000000)}]),/oversized/);
  assert.throws(()=>chatImages([{...image,url:'https://example.com/x'}]),/fields/);
  assert.equal(voiceWav(wav().toString('base64')).length,32044);
  assert.throws(()=>voiceWav(wav(33).toString('base64')),/seconds/);
  assert.throws(()=>voiceWav(image.data),/WAV/);
});
test('provider receives actual input_image, language rule and no base64 in instructions',async()=>{
  let payload;
  await providerAnswer({apiKey:'test',baseUrl:'https://example.com/v1',model:'test',timeout:2,maxTokens:100},
    {question:'What is in this photo?',language:'en',autoLanguage:true,images:[image]},[],[],{},
    async(url,options)=>{payload=JSON.parse(options.body);return {ok:true,status:200,json:async()=>({status:'completed',output:[{type:'message',content:[{type:'output_text',text:'{}'}]}]})};});
  assert.equal(payload.input.at(-1).content[1].type,'input_image');
  assert.equal(payload.input.at(-1).content[1].image_url,'data:image/png;base64,'+image.data);
  assert.match(payload.instructions,/Match the language of the latest/);
  assert.match(payload.instructions,/untrusted visual context/);
  assert.ok(!payload.instructions.includes(image.data));
});
test('authenticated media reaches provider, photo is not retained, voice never submits a chat',async t=>{
  const temp=fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-chat-media-')),saved={...process.env};
  Object.assign(process.env,{DATABASE_PATH:path.join(temp,'db.sqlite'),NODE_ENV:'test',OMS_SEED_DEMO:'true',OMS_DEMO_PASSWORD:'123456',OPENAI_API_KEY:''});
  const chats=[],recordings=[],spoken=[];
  const app=createApp({mobileAi:{rpm:20,daily:100,answer:async context=>{chats.push(context);return {message:context.language==='en'?'Hello':'Xin chào',sources:[]};},transcribe:async bytes=>{recordings.push(bytes);return {text:'What is 5S?',language:'en'};},speak:async text=>{spoken.push(text);return Buffer.from('test audio');}}});
  await app.locals.bootstrapPromise;const server=app.listen(0,'127.0.0.1');await new Promise(r=>server.once('listening',r));
  t.after(async()=>{await new Promise(r=>server.close(r));await new Promise(r=>app.locals.store.db.close(r));fs.rmSync(temp,{recursive:true,force:true});for(const k of Object.keys(process.env))if(!(k in saved))delete process.env[k];Object.assign(process.env,saved);});
  const base='http://127.0.0.1:'+server.address().port+'/api/oms/mobile';
  const call=(route,body,token)=>fetch(base+route,{method:'POST',headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{})},body:JSON.stringify(body)});
  assert.equal((await call('/jarvis/transcribe',{audio:wav().toString('base64')})).status,401);
  assert.equal((await call('/jarvis/speech',{text:'hi'})).status,401);
  const login=await (await call('/login',{employeeCode:'NV-00101',password:'123456'})).json(),token=login.accessToken;
  const en=await (await call('/jarvis/chat',{message:'What is in this photo?',images:[image]},token)).json();assert.equal(en.language,'en');assert.deepEqual(chats[0].images,[image]);
  const vi=await (await call('/jarvis/chat',{message:'Giải thích bằng tiếng Việt'},token)).json();assert.equal(vi.language,'vi');assert.ok(!JSON.stringify(chats[1].history).includes(image.data));
  assert.equal((await call('/jarvis/chat',{message:'x',images:[{...image,mimeType:'image/jpeg'}]},token)).status,422);assert.equal(chats.length,2);
  const transcript=await (await call('/jarvis/transcribe',{audio:wav().toString('base64')},token)).json();assert.equal(transcript.text,'What is 5S?');assert.equal(chats.length,2);assert.equal(recordings.length,1);
  assert.equal((await call('/jarvis/transcribe',{audio:image.data},token)).status,422);
  const audio=await call('/jarvis/speech',{text:'Xin chào'},token);assert.equal(audio.status,200);assert.match(audio.headers.get('content-type'),/audio\/mpeg/);assert.deepEqual(spoken,['Xin chào']);
  assert.equal((await call('/jarvis/speech',{text:'x'.repeat(4001)},token)).status,422);
  assert.equal((await call('/jarvis/transcribe',{audio:wav().toString('base64'),employeeId:'spoof'},token)).status,422);
});
