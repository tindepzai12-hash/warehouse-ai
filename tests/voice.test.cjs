const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const source = fs.readFileSync(require('node:path').join(__dirname,'../static/voice.js'),'utf8');
function setup({supported=true, voices=[{lang:'vi-VN',name:'Vietnamese'}]}={}) {
 const elements = new Map(), events = {}, docEvents = {}, timers = new Map();
 let timerId = 0;
 const get = id => {
  if (!elements.has(id)) elements.set(id,{value:'',dataset:{},disabled:false,checked:false,textContent:'',handlers:{},setAttribute(k,v){this[k]=v;},addEventListener(k,v){this.handlers[k]=v;}});
  return elements.get(id);
 };
 get('language').value='vi';
 const instances=[], spoken=[];
 class Recognition {
  constructor(){instances.push(this);}
  start(){this.started=true;}
  stop(){this.stopped=true;this.onend();}
  abort(){this.aborted=true;this.onend();}
 }
 const synth={getVoices:()=>voices,cancel(){},speak:u=>spoken.push(u)};
 const document={hidden:false,getElementById:get,addEventListener:(k,v)=>docEvents[k]=v};
 const window={SpeechRecognition:supported?Recognition:undefined,speechSynthesis:synth,addEventListener:(k,v)=>events[k]=v};
 vm.runInNewContext(source,{window,document,SpeechSynthesisUtterance:class {constructor(text){this.text=text;}},setTimeout:fn=>{timers.set(++timerId,fn);return timerId;},clearTimeout:id=>timers.delete(id)});
 const emit=(name,detail)=>events[name]({detail});
 const result=(r,text)=>r.onresult({resultIndex:0,results:[Object.assign([{transcript:text}],{isFinal:true})]});
 return {get,instances,spoken,emit,result,document,docEvents,timers};
}
test('unsupported recognition leaves text input usable',()=>{
 const t=setup({supported:false});
 assert.equal(t.get('mic').disabled,true);
 assert.equal(t.get('question').readOnly,false);
 assert.match(t.get('voice-status').textContent,/nhập câu hỏi/);
});
test('speech appends draft, respects language and length, never sends',()=>{
 const t=setup();t.get('question').value='Đơn cần 5';t.get('locale').value='vi-VN';
 t.get('mic').onclick();const r=t.instances[0];
 assert.equal(r.lang,'vi-VN');assert.equal(t.get('question').readOnly,true);
 t.result(r,'còn 3');assert.equal(t.get('question').value,'Đơn cần 5 còn 3');
 t.result(r,'x'.repeat(3000));assert.equal(t.get('question').value.length,2000);
 r.onend();assert.equal(t.get('question').readOnly,false);
 assert.equal(t.get('assistant').dataset.state,'idle');
});
test('reset aborts microphone and ignores late transcripts',()=>{
 const t=setup();t.get('question').value='draft';t.get('mic').onclick();
 const r=t.instances[0];t.emit('chat-reset');t.result(r,'late');
 assert.equal(r.aborted,true);assert.equal(t.get('question').value,'draft');
 assert.equal(t.timers.size,0);
});
test('permission refusal recovers text input and explains error',()=>{
 const t=setup();t.get('mic').onclick();t.instances[0].onerror({error:'not-allowed'});
 assert.equal(t.get('mic').disabled,false);assert.equal(t.get('question').readOnly,false);
 assert.match(t.get('voice-status').textContent,/quyền/);
});
test('request cancels microphone; failed request never reads old answer',()=>{
 const t=setup();t.emit('chat-answer',{text:'Old',lang:'vi'});t.emit('chat-end');
 t.get('auto-read').checked=true;t.get('mic').onclick();t.emit('chat-start');
 assert.equal(t.instances[0].aborted,true);assert.equal(t.get('assistant').dataset.state,'thinking');
 t.emit('chat-end');assert.equal(t.spoken.length,0);
});
test('speech only on opt-in; stop invalidates queued callbacks',()=>{
 const t=setup();t.emit('chat-answer',{text:'Answer',lang:'vi'});t.emit('chat-end');
 assert.equal(t.spoken.length,0);t.get('read').onclick();assert.equal(t.spoken.length,1);
 assert.equal(t.get('assistant').dataset.state,'speaking');
 t.get('stop-speech').onclick();t.spoken[0].onend();
 assert.equal(t.get('assistant').dataset.state,'result');assert.equal(t.spoken.length,1);
});
test('missing matching voice never reads in unrelated language',()=>{
 const t=setup({voices:[{lang:'en-US',name:'English'}]});
 t.emit('chat-answer',{text:'Xin chào',lang:'vi'});t.get('read').onclick();
 assert.equal(t.spoken.length,0);assert.match(t.get('speech-status').textContent,/chưa có giọng/);
});
test('hiding page stops recording and reading; no auto speech while hidden',()=>{
 const t=setup();t.get('mic').onclick();t.document.hidden=true;t.docEvents.visibilitychange();
 assert.equal(t.instances[0].aborted,true);
 t.get('auto-read').checked=true;t.emit('chat-answer',{text:'Answer',lang:'vi'});t.emit('chat-end');
 assert.equal(t.spoken.length,0);
});
test('Vietnamese recognition uses full locale and network error retains diagnostics',()=>{
 const t=setup();t.get('mic').onclick();const r=t.instances[0];
 assert.equal(r.lang,'vi-VN');r.onerror({error:'network'});r.onend();
 assert.match(t.get('voice-status').textContent,/Mã: network/);
 assert.match(t.get('voice-status').textContent,/Kiểm tra micro/);
 assert.equal(t.get('assistant').dataset.state,'error');
});
test('request error uses error art, next success uses result art',()=>{
 const t=setup();t.emit('chat-start');t.emit('chat-error');t.emit('chat-end');
 assert.equal(t.get('assistant').dataset.state,'error');
 t.emit('chat-start');assert.equal(t.get('assistant').dataset.state,'thinking');
 t.emit('chat-answer',{text:'OK',lang:'vi'});t.emit('chat-end');
 assert.equal(t.get('assistant').dataset.state,'result');
});
test('diagnostic excludes recognition and speech',()=>{
 const t=setup();t.emit('mic-test-start');t.get('mic').onclick();
 assert.equal(t.instances.length,0);
 t.emit('chat-answer',{text:'OK',lang:'vi'});t.get('read').onclick();
 assert.equal(t.spoken.length,0);t.emit('mic-test-end',{error:false});
 assert.equal(t.get('mic').disabled,false);
});
test('empty voice list waits then can be cancelled',()=>{
 const t=setup({voices:[]});t.emit('chat-answer',{text:'OK',lang:'vi'});
 t.get('read').onclick();assert.equal(t.timers.size,1);
 t.get('stop-speech').onclick();assert.equal(t.timers.size,0);
 assert.equal(t.spoken.length,0);
});
test('known JarVis addresses normalize without changing quantities or product text',()=>{
 const context={window:{}};
 vm.runInNewContext(fs.readFileSync(require('node:path').join(__dirname,'../static/jarvis.js'),'utf8'),context);
 const normalize=context.window.normalizeJarVisAddress;
 assert.equal(normalize('Chào gia vít ơi, cần 5 hộp nhưng có 3'),'Chào JarVis ơi, cần 5 hộp nhưng có 3');
 assert.equal(normalize('cha vít giúp tôi'),'JarVis giúp tôi');
 assert.equal(normalize('Jarvis, hello'),'JarVis, hello');
 assert.equal(normalize('cần 5 hộp gia vít'),'cần 5 hộp gia vít');
 assert.equal(normalize('giá vịt là bao nhiêu'),'giá vịt là bao nhiêu');
});
