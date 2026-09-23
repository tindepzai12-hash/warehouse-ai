const $=id=>document.getElementById(id);
const t=(key,values)=>window.JarvisI18n.t(key,values);
let session=null,lastPayload=null,busy=false;
function bubble(text,kind){const p=document.createElement('div');p.className='bubble '+kind;p.textContent=text;$('chat').append(p);$('chat').scrollTop=$('chat').scrollHeight;}
function reset(){window.dispatchEvent(new Event('chat-reset'));session=null;lastPayload=null;const empty=document.createElement('p');empty.dataset.i18n='chatEmpty';empty.textContent=t('chatEmpty');$('chat').replaceChildren(empty);$('sources').replaceChildren();$('proposal').replaceChildren();$('session').textContent=t('noSession');$('retry').disabled=true;}
$('new').onclick=reset;$('task').onchange=reset;$('token').onchange=reset;
async function send(retry=false){
 if(busy)return;
 if(!$('token').value.trim()||(!retry&&!$('question').value.trim())){$('status').textContent=t('missingInput');return;}
 const draftQuestion=retry?null:$('question').value;
 const payload=retry?lastPayload:{requestId:crypto.randomUUID(),sessionId:session,taskId:$('task').value,language:$('language').value,locale:$('locale').value.trim()||null,eventType:$('event').value,occurredAtUtc:new Date().toISOString(),question:$('question').value.trim(),observation:{}};
 if(!retry)$('question').value='';
 if(!payload)return;lastPayload=payload;busy=true;window.dispatchEvent(new Event('chat-start'));$('send').disabled=true;$('new').disabled=true;$('task').disabled=true;$('token').disabled=true;$('retry').disabled=true;$('language').disabled=true;$('locale').disabled=true;
 $('status').textContent=t('processing');$('status').className='';$('request').textContent=JSON.stringify(payload,null,2);
 if(!retry)bubble(payload.question,'question');
 try{
  const response=await fetch('/api/feedback',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+$('token').value.trim()},body:JSON.stringify(payload),signal:AbortSignal.timeout(70000)});
  const data=await response.json();
  if(response.status===401)throw Error(t('invalidToken'));
  if(!response.ok)throw Error(`${response.status}: ${JSON.stringify(data.detail)} · ${response.headers.get('X-Request-ID')||''}`);
  session=data.sessionId;$('session').textContent=t('sessionPrefix',{id:session});bubble(data.message,'answer');
  $('sources').replaceChildren();for(const source of data.sourceDetails){const article=document.createElement('article');const strong=document.createElement('strong');strong.textContent=source.documentId+' / '+source.sectionId;const text=document.createElement('p');text.textContent=typeof source.text==='string'?source.text:(source.text[payload.language]||source.text[source.sourceLanguage]||source.text.vi||'');const meta=document.createElement('small');meta.textContent=source.source+' · '+source.source_status+' · site_approved: '+source.site_approved;article.append(strong,text,meta);$('sources').append(article);}
  $('proposal').replaceChildren();const label=document.createElement('p');label.textContent=t('proposal',{type:data.action.type,code:data.action.exceptionCode||''});$('proposal').append(label);
  if(data.action.type==='open_exception_report'){
   const button=document.createElement('button');button.textContent=t('exceptionDraftButton');button.onclick=()=>{const preview=document.createElement('p');preview.textContent=t('exceptionDraft',{code:data.action.exceptionCode,task:payload.taskId});$('proposal').append(preview);button.disabled=true;};$('proposal').append(button);
  }
  $('status').textContent=t('statusContext',{mode:data.mode.toUpperCase(),latency:data.latencyMs,turns:data.contextTurns,requestId:data.requestId});
  lastPayload=null;
  window.dispatchEvent(new CustomEvent('chat-answer',{detail:{text:data.message,lang:payload.locale||payload.language}}));
  const listen=document.createElement('button');listen.className='secondary answer-read';listen.textContent=t('listenThis');listen.onclick=()=>window.dispatchEvent(new CustomEvent('read-message',{detail:{text:data.message,lang:payload.locale||payload.language}}));$('chat').lastElementChild.append(document.createElement('br'),listen);
 }catch(error){
  if(!retry&&$('question').value==='')$('question').value=draftQuestion;
  $('status').textContent=error.message;$('status').className='error';window.dispatchEvent(new Event('chat-error'));
 }
 finally{busy=false;window.dispatchEvent(new Event('chat-end'));$('send').disabled=false;$('new').disabled=false;$('task').disabled=false;$('token').disabled=false;$('retry').disabled=!lastPayload;$('language').disabled=false;$('locale').disabled=false;}
}
$('send').onclick=()=>send();$('retry').onclick=()=>send(true);
$('example').onclick=()=>{$('question').value=t('exampleShortage');$('question').focus();};
window.addEventListener('language-changed',event=>{$('locale').value=event.detail.locale;$('status').textContent=t('ready');});
if(!$('locale').value)$('locale').value=window.JarvisI18n.defaults[$('language').value];
fetch('/api/health').then(r=>r.json()).then(d=>{delete $('mode').dataset.i18n;$('mode').textContent=d.mode.toUpperCase()+' · SYNTHETIC';}).catch(()=>{delete $('mode').dataset.i18n;$('mode').textContent=t('healthFailed');});
