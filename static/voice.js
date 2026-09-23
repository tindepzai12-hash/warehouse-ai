/* Browser speech is optional. Recognition only edits the draft, never sends it. */
(() => {
 const el = id => document.getElementById(id);
 const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
 const synth = window.speechSynthesis;
 let recognition = null, timer = null, generation = 0, speechGeneration = 0;
 let working = false, speaking = false, answer = null, freshAnswer = false;
 function state() {
  const value = working ? 'thinking' : recognition ? 'listening' : speaking ? 'speaking' : 'idle';
  el('assistant').dataset.state = value;
  el('assistant-state').textContent = {idle:'Sẵn sàng hỗ trợ',thinking:'Đang tìm hướng dẫn phù hợp…',listening:'Đang nghe bạn nói…',speaking:'Đang đọc câu trả lời…'}[value];
  el('mic').disabled = !Recognition || working;
  el('mic').textContent = recognition ? 'Dừng ghi giọng nói' : 'Nói câu hỏi';
  el('mic').setAttribute('aria-pressed', String(Boolean(recognition)));
  el('read').disabled = !synth || !answer || working || Boolean(recognition);
  el('stop-speech').disabled = !speaking;
  el('question').readOnly = Boolean(recognition);
  el('example').disabled = Boolean(recognition) || working;
 }
 function stopReading() {
  speechGeneration++;
  if (synth) synth.cancel();
  speaking = false;
  state();
 }
 function abortListening() {
  generation++;
  clearTimeout(timer);
  const old = recognition;
  recognition = null;
  if (old) { try { old.abort(); } catch (_) {} }
  state();
 }
 function readAnswer() {
  if (!synth || !answer || working || recognition) return;
  stopReading();
  const voices = synth.getVoices();
  const language = answer.lang.toLowerCase();
  const voice = voices.find(v => v.lang.toLowerCase() === language) ||
   voices.find(v => v.lang.toLowerCase().split('-')[0] === language.split('-')[0]);
  if (!voice) {
   el('speech-status').textContent = 'Thiết bị chưa có giọng đọc cho ngôn ngữ này. Bạn vẫn có thể đọc câu trả lời.';
   return;
  }
  const current = ++speechGeneration;
  const chunks = answer.text.match(/[\s\S]{1,180}(?:\s|$)|[\s\S]{1,180}/g) || [];
  let index = 0;
  speaking = true;
  el('speech-status').textContent = 'Giọng đọc: ' + voice.name + ' (' + voice.lang + ')';
  state();
  function next() {
   if (current !== speechGeneration) return;
   if (index >= chunks.length) { speaking = false; state(); return; }
   const utterance = new SpeechSynthesisUtterance(chunks[index++]);
   utterance.voice = voice;
   utterance.lang = voice.lang;
   utterance.onend = next;
   utterance.onerror = () => {
    if (current !== speechGeneration) return;
    speaking = false;
    el('speech-status').textContent = 'Không đọc được trên thiết bị này. Hãy bấm Nghe trả lời để thử lại.';
    state();
   };
   try { synth.speak(utterance); } catch (_) { utterance.onerror(); }
  }
  next();
 }
 el('mic').onclick = () => {
  if (!Recognition || working) return;
  if (recognition) {
   const active = recognition;
   try { active.stop(); } catch (_) { abortListening(); }
   // Force cleanup if a browser never emits its end event.
   clearTimeout(timer);
   timer = setTimeout(() => { if (recognition === active) abortListening(); }, 3000);
   return;
  }
  stopReading();
  const current = ++generation;
  const original = el('question').value;
  let finalText = '';
  const active = new Recognition();
  recognition = active;
  active.lang = el('locale').value.trim() || el('language').value;
  active.continuous = false;
  active.interimResults = false;
  active.onresult = event => {
   if (current !== generation || recognition !== active) return;
   for (let i = event.resultIndex; i < event.results.length; i++) {
    if (event.results[i].isFinal) finalText += event.results[i][0].transcript + ' ';
   }
   const combined = [original.trim(), finalText.trim()].filter(Boolean).join(' ');
   el('question').value = combined.slice(0, 2000);
   el('voice-status').textContent = combined.length > 2000 ? 'Đã đạt giới hạn 2.000 ký tự. Hãy kiểm tra nội dung trước khi gửi.' : 'Đã nhận lời nói. Xem lại nội dung, rồi bấm Gửi câu hỏi.';
  };
  active.onerror = event => {
   if (current !== generation) return;
   const messages = {'not-allowed':'Micro chưa được cho phép. Bạn có thể cấp quyền trong trình duyệt hoặc nhập chữ.','service-not-allowed':'Trình duyệt không cho phép nhận dạng giọng nói. Hãy nhập chữ.','no-speech':'Chưa nghe rõ lời nói. Bấm Nói câu hỏi để thử lại.','audio-capture':'Không tìm thấy micro hoạt động. Hãy kiểm tra thiết bị.','network':'Dịch vụ nhận dạng chưa kết nối được. Hãy nhập chữ hoặc thử lại.','language-not-supported':'Trình duyệt chưa hỗ trợ nhận dạng ngôn ngữ này.'};
   el('voice-status').textContent = messages[event.error] || 'Đã dừng nhận dạng giọng nói. Bạn vẫn có thể nhập chữ.';
   abortListening();
  };
  active.onend = () => {
   if (current !== generation) return;
   clearTimeout(timer);
   recognition = null;
   if (!finalText) el('voice-status').textContent = 'Đã dừng micro. Chưa có lời nói mới; bạn có thể nhập chữ hoặc thử lại.';
   state();
  };
  el('voice-status').textContent = 'Đang nghe, tối đa 45 giây. Bấm Dừng khi nói xong.';
  state();
  try {
   active.start();
   timer = setTimeout(() => { if (recognition === active) el('mic').onclick(); }, 45000);
  } catch (_) {
   abortListening();
   el('voice-status').textContent = 'Không mở được micro. Bạn vẫn có thể nhập câu hỏi.';
  }
 };
 el('read').onclick = readAnswer;
 el('stop-speech').onclick = stopReading;
 el('auto-read').disabled = !synth;
 el('auto-read').onchange = () => { if (!el('auto-read').checked) stopReading(); };
 for (const id of ['language','locale']) el(id).addEventListener('change', () => {
  abortListening(); stopReading();
  el('voice-status').textContent = 'Đã đổi ngôn ngữ. Bấm Nói câu hỏi để bắt đầu lại.';
 });
 window.addEventListener('chat-start', () => {
  working = true; freshAnswer = false; abortListening(); stopReading(); state();
 });
 window.addEventListener('chat-answer', event => { answer = event.detail; freshAnswer = true; });
 window.addEventListener('chat-end', () => {
  working = false; state();
  if (freshAnswer && answer && el('auto-read').checked && !document.hidden) readAnswer();
 });
 window.addEventListener('chat-reset', () => {
  abortListening(); stopReading(); answer = null; state();
  el('speech-status').textContent = ''; el('voice-status').textContent = Recognition ? 'Bấm Nói câu hỏi để bật micro.' : 'Trình duyệt này chưa hỗ trợ nhận dạng giọng nói. Hãy nhập câu hỏi.';
 });
 function suspend() { abortListening(); stopReading(); }
 document.addEventListener('visibilitychange', () => { if (document.hidden) suspend(); });
 window.addEventListener('pagehide', suspend);
 el('voice-status').textContent = Recognition ? 'Bấm Nói câu hỏi để bật micro.' : 'Trình duyệt này chưa hỗ trợ nhận dạng giọng nói. Hãy nhập câu hỏi.';
 if (!synth) el('speech-status').textContent = 'Trình duyệt này chưa hỗ trợ đọc câu trả lời.';
 state();
})();
