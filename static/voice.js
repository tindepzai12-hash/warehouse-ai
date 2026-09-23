/* Browser speech is optional. Recognition only edits the draft, never sends it. */
(() => {
 const el = id => document.getElementById(id);
 const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
 const synth = window.speechSynthesis;
 let recognition = null, timer = null, generation = 0, speechGeneration = 0;
 let working = false, speaking = false, answer = null, freshAnswer = false, failed = false;
 let diagnostic = false, voiceWaitTimer = null, waitingVoice = false, utteranceRef = null;
 function state() {
  const value = working ? 'thinking' : recognition || diagnostic ? 'listening' : speaking ? 'speaking' : failed ? 'error' : answer ? 'result' : 'idle';
  el('assistant').dataset.state = value;
  el('assistant-state').textContent = {idle:'JarVis sẵn sàng hỗ trợ',thinking:'JarVis đang suy nghĩ…',listening:'JarVis đang nghe bạn…',speaking:'JarVis đang đọc câu trả lời…',error:'Có lỗi cần kiểm tra — xem hướng dẫn bên dưới',result:'JarVis đã có câu trả lời'}[value];
  el('mic').disabled = !Recognition || working || diagnostic;
  el('mic').textContent = recognition ? 'Dừng ghi giọng nói' : 'Nói câu hỏi';
  el('mic').setAttribute('aria-pressed', String(Boolean(recognition)));
  el('read').disabled = !synth || !answer || working || Boolean(recognition) || diagnostic;
  el('stop-speech').disabled = !speaking && !waitingVoice;
  el('mic-test').disabled = working || Boolean(recognition);
  el('question').readOnly = Boolean(recognition);
  el('example').disabled = Boolean(recognition) || working;
 }
 function stopReading() {
  speechGeneration++;
  clearTimeout(voiceWaitTimer); waitingVoice = false; utteranceRef = null;
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
 function readAnswer(waited = false) {
  if (!synth || !answer || working || recognition || diagnostic) return;
  stopReading();
  const voices = synth.getVoices();
  const language = answer.lang.toLowerCase();
  const voice = voices.find(v => v.lang.toLowerCase() === language) ||
   voices.find(v => v.lang.toLowerCase().split('-')[0] === language.split('-')[0]);
  if (!voice) {
   if (!voices.length && !waited) {
    waitingVoice = true;
    el('speech-status').textContent = 'Đang tải danh sách giọng đọc trên thiết bị…';
    voiceWaitTimer = setTimeout(() => readAnswer(true), 1500);
    state(); return;
   }
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
   utteranceRef = utterance;
   utterance.voice = voice;
   utterance.lang = voice.lang;
   utterance.onend = next;
   utterance.onerror = event => {
    if (current !== speechGeneration) return;
    speaking = false;
    el('speech-status').textContent = event?.error === 'not-allowed' ? 'Trình duyệt chặn tự phát âm thanh. Bấm Nghe trả lời để cho phép đọc theo thao tác của bạn.' : 'Không đọc được trên thiết bị này. Kiểm tra loa/giọng đọc rồi bấm Nghe trả lời để thử lại.';
    state();
   };
   try { synth.speak(utterance); } catch (_) { utterance.onerror(); }
  }
  next();
 }
 el('mic').onclick = () => {
  if (!Recognition || working || diagnostic) return;
  if (recognition) {
   const active = recognition;
   try { active.stop(); } catch (_) { abortListening(); }
   // Force cleanup if a browser never emits its end event.
   clearTimeout(timer);
   timer = setTimeout(() => { if (recognition === active) abortListening(); }, 3000);
   return;
  }
  stopReading();
  failed = false;
  const current = ++generation;
  const original = el('question').value;
  let finalText = '';
  const active = new Recognition();
  recognition = active;
  active.lang = el('locale').value.trim() || ({vi:'vi-VN',en:'en-US',zh:'zh'}[el('language').value]);
  active.continuous = false;
  active.interimResults = false;
  active.maxAlternatives = 3;
  active.onstart = () => {
   if (current === generation) el('voice-status').textContent = 'Đã mở nhận dạng. Hãy nói câu hỏi; bấm Dừng khi nói xong.';
  };
  active.onresult = event => {
   if (current !== generation || recognition !== active) return;
   for (let i = event.resultIndex; i < event.results.length; i++) {
    if (event.results[i].isFinal) finalText += event.results[i][0].transcript + ' ';
   }
   const normalized = window.normalizeJarVisAddress ? window.normalizeJarVisAddress(finalText.trim()) : finalText.trim();
   el('voice-original').textContent = normalized !== finalText.trim() ? 'Đã nhận: “' + finalText.trim() + '” → chuẩn hóa tên gọi thành JarVis. Bạn có thể sửa lại trước khi gửi.' : '';
   const combined = [original.trim(), normalized].filter(Boolean).join(' ');
   el('question').value = combined.slice(0, 2000);
   el('voice-status').textContent = combined.length > 2000 ? 'Đã đạt giới hạn 2.000 ký tự. Hãy kiểm tra nội dung trước khi gửi.' : 'Đã nhận lời nói. Xem lại nội dung, rồi bấm Gửi câu hỏi.';
  };
  active.onerror = event => {
   if (current !== generation) return;
   const messages = {'not-allowed':'Micro chưa được cho phép. Bạn có thể cấp quyền trong trình duyệt hoặc nhập chữ.','service-not-allowed':'Trình duyệt không cho phép nhận dạng giọng nói. Hãy nhập chữ.','no-speech':'Chưa nghe rõ lời nói. Bấm Nói câu hỏi để thử lại.','audio-capture':'Không tìm thấy micro hoạt động. Hãy kiểm tra thiết bị.','network':'Dịch vụ nhận dạng chưa kết nối được. Hãy nhập chữ hoặc thử lại.','language-not-supported':'Trình duyệt chưa hỗ trợ nhận dạng ngôn ngữ này.'};
   failed = true;
   const help = ['network','service-not-allowed'].includes(event.error) ? ' Quyền micro và dịch vụ nhận dạng là hai phần riêng. Bấm Kiểm tra micro; nếu có tín hiệu, thử Chrome hoặc nhập giọng nói Windows + H trong ô câu hỏi.' : ' Bấm Kiểm tra micro để kiểm tra đầu vào.';
   el('voice-status').textContent = (messages[event.error] || 'Đã dừng nhận dạng giọng nói. Bạn vẫn có thể nhập chữ.') + ' Mã: ' + event.error + '.' + help;
   abortListening();
  };
  active.onend = () => {
   if (current !== generation) return;
   clearTimeout(timer);
   recognition = null;
   if (!finalText) el('voice-status').textContent = 'Đã dừng micro. Chưa có lời nói mới; bạn có thể nhập chữ hoặc thử lại.';
   state();
  };
  el('voice-status').textContent = 'Đang mở nhận dạng giọng nói, tối đa 45 giây…';
  state();
  try {
   active.start();
   timer = setTimeout(() => { if (recognition === active) el('mic').onclick(); }, 45000);
  } catch (_) {
   failed = true;
   abortListening();
   el('voice-status').textContent = 'Không mở được micro. Bạn vẫn có thể nhập câu hỏi.';
  }
 };
 el('read').onclick = () => readAnswer();
 if (synth?.addEventListener) synth.addEventListener('voiceschanged', () => { if (waitingVoice && !document.hidden) readAnswer(true); });
 el('stop-speech').onclick = stopReading;
 el('auto-read').disabled = !synth;
 try { el('auto-read').checked = window.localStorage?.getItem('jarvis-auto-read') === 'true'; } catch (_) {}
 el('auto-read').onchange = () => {
  try { window.localStorage?.setItem('jarvis-auto-read', String(el('auto-read').checked)); } catch (_) {}
  if (!el('auto-read').checked) stopReading();
  el('speech-status').textContent = el('auto-read').checked ? 'Đã bật: JarVis sẽ đọc câu trả lời mới. Bạn có thể tắt bất cứ lúc nào.' : 'Đã tắt tự đọc. Bấm Nghe trả lời khi bạn muốn nghe.';
 };
 for (const id of ['language','locale']) el(id).addEventListener('change', () => {
  abortListening(); stopReading();
  el('voice-status').textContent = 'Đã đổi ngôn ngữ. Bấm Nói câu hỏi để bắt đầu lại.';
 });
 window.addEventListener('chat-start', () => {
  working = true; failed = false; freshAnswer = false; abortListening(); stopReading(); state();
 });
 window.addEventListener('chat-error', () => { failed = true; state(); });
 window.addEventListener('chat-answer', event => { answer = event.detail; freshAnswer = true; failed = false; });
 window.addEventListener('read-message', event => { answer = event.detail; readAnswer(); });
 window.addEventListener('mic-test-start', () => { abortListening(); stopReading(); diagnostic = true; failed = false; state(); });
 window.addEventListener('mic-test-end', event => { diagnostic = false; failed = Boolean(event.detail?.error); state(); });
 window.addEventListener('chat-end', () => {
  working = false; state();
  if (freshAnswer && answer && el('auto-read').checked && !document.hidden) readAnswer();
 });
 window.addEventListener('chat-reset', () => {
  abortListening(); stopReading(); answer = null; failed = false; state();
  el('speech-status').textContent = ''; el('voice-status').textContent = Recognition ? 'Bấm Nói câu hỏi để bật micro.' : 'Trình duyệt này chưa hỗ trợ nhận dạng giọng nói. Hãy nhập câu hỏi.';
 });
 function suspend() { abortListening(); stopReading(); }
 document.addEventListener('visibilitychange', () => { if (document.hidden) suspend(); });
 window.addEventListener('pagehide', suspend);
 el('voice-status').textContent = Recognition ? 'Bấm Nói câu hỏi để bật micro.' : 'Trình duyệt này chưa hỗ trợ nhận dạng giọng nói. Hãy nhập câu hỏi.';
 if (!synth) el('speech-status').textContent = 'Trình duyệt này chưa hỗ trợ đọc câu trả lời.';
 state();
})();
