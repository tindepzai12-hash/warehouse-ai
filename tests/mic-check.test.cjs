const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const fs=require('node:fs');
const path=require('node:path');
const i18nSource=fs.readFileSync(path.join(__dirname,'../static/i18n.js'),'utf8');
function setup() {
 const elements={},handlers={},timers=new Map();
 let nextId=0,calls=0,resolve,reject,stops=0,closed=0;
 const promise=new Promise((a,b)=>{resolve=a;reject=b;});
 const document={hidden:false,getElementById:id=>elements[id]||=( {} ),addEventListener:(k,v)=>handlers[k]=v};
 class AudioContext {
  async resume(){}
  async close(){closed++;}
  createAnalyser(){return {fftSize:2048,getFloatTimeDomainData:s=>s.fill(.1)};}
  createMediaStreamSource(){return {connect(){}};}
 }
 const stream={getTracks:()=>[{stop(){stops++;}}]};
 const schedule=fn=>{timers.set(++nextId,fn);return nextId;};
 const window={AudioContext,dispatchEvent(){},addEventListener:(k,v)=>handlers[k]=v};
 const context={
  document,window,
  navigator:{mediaDevices:{getUserMedia(){calls++;return promise;}}},
  CustomEvent:class{},Event:class{},Float32Array,setTimeout:schedule,setInterval:schedule,
  clearTimeout:id=>timers.delete(id),clearInterval:id=>timers.delete(id)
 };
 vm.runInNewContext(i18nSource,context);
 vm.runInNewContext(fs.readFileSync(path.join(__dirname,'../static/mic-check.js'),'utf8'),context);
 return {elements,handlers,timers,document,resolve:()=>resolve(stream),reject,
  stats:()=>({calls,stops,closed})};
}
test('diagnostic never opens microphone before click and stops after sample window',async()=>{
 const t=setup();assert.equal(t.stats().calls,0);
 const pending=t.elements['mic-test'].onclick();t.resolve();await pending;
 assert.equal(t.stats().calls,1);
 [...t.timers.values()][0]();[...t.timers.values()][1]();
 assert.equal(t.stats().stops,1);assert.equal(t.stats().closed,1);
 assert.equal(t.timers.size,0);assert.match(t.elements['mic-diagnostic'].textContent,/đã thu/);
});
test('late permission result after cancellation immediately releases microphone',async()=>{
 const t=setup();const pending=t.elements['mic-test'].onclick();
 t.document.hidden=true;t.handlers.visibilitychange();t.resolve();await pending;
 assert.equal(t.stats().stops,1);assert.equal(t.timers.size,0);
});
test('permission refusal produces actionable message without active timers',async()=>{
 const t=setup();const pending=t.elements['mic-test'].onclick();
 t.reject({name:'NotAllowedError'});await pending;
 assert.match(t.elements['mic-diagnostic'].textContent,/Quyền micro bị chặn/);
 assert.equal(t.timers.size,0);
});
