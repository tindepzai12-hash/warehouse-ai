const $=id=>document.getElementById(id);
let session=null,lastPayload=null,busy=false;
function bubble(text,kind){const p=document.createElement('div');p.className='bubble '+kind;p.textContent=text;$('chat').append(p);$('chat').scrollTop=$('chat').scrollHeight;}
function reset(){window.dispatchEvent(new Event('chat-reset'));session=null;lastPayload=null;$('chat').replaceChildren();$('sources').replaceChildren();$('proposal').replaceChildren();$('session').textContent='Phiên mới';$('retry').disabled=true;}
$('new').onclick=reset;$('task').onchange=reset;$('token').onchange=reset;
async function send(retry=false){
 if(busy)return;
 if(!$('token').value.trim()||(!retry&&!$('question').value.trim())){$('status').textContent='Nhập token thử nghiệm và câu hỏi.';return;}
 const payload=retry?lastPayload:{requestId:crypto.randomUUID(),sessionId:session,taskId:$('task').value,language:$('language').value,locale:$('locale').value.trim()||null,eventType:$('event').value,occurredAtUtc:new Date().toISOString(),question:$('question').value.trim(),observation:{}};
 if(!payload)return;lastPayload=payload;busy=true;window.dispatchEvent(new Event('chat-start'));$('send').disabled=true;$('new').disabled=true;$('task').disabled=true;$('token').disabled=true;$('retry').disabled=true;$('language').disabled=true;$('locale').disabled=true;
 $('status').textContent='Đang truy xuất / xử lý…';$('status').className='';$('request').textContent=JSON.stringify(payload,null,2);
 if(!retry)bubble(payload.question,'question');
 try{
  const response=await fetch('/api/feedback',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+$('token').value.trim()},body:JSON.stringify(payload),signal:AbortSignal.timeout(70000)});
  const data=await response.json();
  if(response.status===401)throw Error('Token thử nghiệm không hợp lệ. Sao chép lại nội dung file E:\\warehouse-ai\\.local-token rồi dán vào ô Token người dùng thử nghiệm (không nhập lệnh PowerShell hoặc OpenAI key). Sau đó gửi lại câu hỏi.');
  if(!response.ok)throw Error(`${response.status}: ${JSON.stringify(data.detail)} · ${response.headers.get('X-Request-ID')||''}`);
  session=data.sessionId;$('session').textContent='Session: '+session;bubble(data.message,'answer');
  $('sources').replaceChildren();for(const source of data.sourceDetails){const article=document.createElement('article');const strong=document.createElement('strong');strong.textContent=source.documentId+' / '+source.sectionId;const text=document.createElement('p');text.textContent=source.text[payload.language];const meta=document.createElement('small');meta.textContent=source.source+' · '+source.source_status+' · site_approved: '+source.site_approved;article.append(strong,text,meta);$('sources').append(article);}
  $('proposal').replaceChildren();const label=document.createElement('p');label.textContent='Đề xuất: '+data.action.type+' '+(data.action.exceptionCode||'');$('proposal').append(label);
  if(data.action.type==='open_exception_report'){
   const button=document.createElement('button');button.textContent='Xem bản nháp ngoại lệ';button.onclick=()=>{const preview=document.createElement('p');preview.textContent='BẢN NHÁP · '+data.action.exceptionCode+' · '+payload.taskId+' · Chưa gửi, chưa thay đổi tồn kho. Workflow sẽ được xây ở Milestone B.';$('proposal').append(preview);button.disabled=true;};$('proposal').append(button);
  }
  $('status').textContent=`${data.mode.toUpperCase()} · ${data.latencyMs} ms · ${data.contextTurns} lượt ngữ cảnh · ${data.requestId}`;
  lastPayload=null;
  window.dispatchEvent(new CustomEvent('chat-answer',{detail:{text:data.message,lang:payload.locale||payload.language}}));
 }catch(error){$('status').textContent=error.message;$('status').className='error';}
 finally{busy=false;window.dispatchEvent(new Event('chat-end'));$('send').disabled=false;$('new').disabled=false;$('task').disabled=false;$('token').disabled=false;$('retry').disabled=!lastPayload;$('language').disabled=false;$('locale').disabled=false;}
}
$('send').onclick=()=>send();$('retry').onclick=()=>send(true);
$('example').onclick=()=>{$('question').value='Đơn cần năm hộp kem mà trên kệ còn ba, tôi làm gì?';$('question').focus();};
fetch('/api/health').then(r=>r.json()).then(d=>$('mode').textContent=d.mode.toUpperCase()+' · SYNTHETIC').catch(()=>$('mode').textContent='Không kết nối được');
