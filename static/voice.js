/* Browser speech is optional. Recognition only edits the draft, never sends it. */
(() => {
 const el = id => document.getElementById(id);
 const t = (key, values) => window.JarvisI18n.t(key, values);
 const Recognition = window.SpeechRecognition || window.webkitSpeechRecognition;
 const synth = window.speechSynthesis;
 let recognition = null, timer = null, generation = 0, speechGeneration = 0;
 let working = false, speaking = false, answer = null, freshAnswer = false, failed = false;
 let diagnostic = false, voiceWaitTimer = null, waitingVoice = false, utteranceRef = null;
 function state() {
  const value = working ? 'thinking' : recognition || diagnostic ? 'listening' : speaking ? 'speaking' : failed ? 'error' : answer ? 'result' : 'idle';
  el('assistant').dataset.state = value;
  el('assistant-state').textContent = t({idle:'assistantIdle',thinking:'assistantThinking',listening:'assistantListening',speaking:'assistantSpeaking',error:'assistantError',result:'assistantResult'}[value]);
  el('mic').disabled = !Recognition || working || diagnostic;
  el('mic').textContent = t(recognition ? 'stopListening' : 'speakQuestion');
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
    el('speech-status').textContent = t('voicesLoading');
    voiceWaitTimer = setTimeout(() => readAnswer(true), 1500);
    state(); return;
   }
   el('speech-status').textContent = t('voiceMissing');
   return;
  }
  const current = ++speechGeneration;
  const chunks = answer.text.match(/[\s\S]{1,180}(?:\s|$)|[\s\S]{1,180}/g) || [];
  let index = 0;
  speaking = true;
  el('speech-status').textContent = t('voiceName',{name:voice.name,lang:voice.lang});
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
    el('speech-status').textContent = t(event?.error === 'not-allowed' ? 'speechBlocked' : 'speechFailed');
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
   if (current === generation) el('voice-status').textContent = t('recognitionStarted');
  };
  active.onresult = event => {
   if (current !== generation || recognition !== active) return;
   for (let i = event.resultIndex; i < event.results.length; i++) {
    if (event.results[i].isFinal) finalText += event.results[i][0].transcript + ' ';
   }
   const normalized = window.normalizeJarVisAddress ? window.normalizeJarVisAddress(finalText.trim()) : finalText.trim();
   el('voice-original').textContent = normalized !== finalText.trim() ? t('recognizedNormalized',{original:finalText.trim()}) : '';
   const combined = [original.trim(), normalized].filter(Boolean).join(' ');
   el('question').value = combined.slice(0, 2000);
   el('voice-status').textContent = t(combined.length > 2000 ? 'draftTooLong' : 'speechCaptured');
  };
  active.onerror = event => {
   if (current !== generation) return;
   const messages = {'not-allowed':'errorNotAllowed','service-not-allowed':'errorServiceNotAllowed','no-speech':'errorNoSpeech','audio-capture':'errorAudioCapture','network':'errorNetwork','language-not-supported':'errorLanguage'};
   failed = true;
   const help = ['network','service-not-allowed'].includes(event.error) ? t('recognitionHelp') : '';
   el('voice-status').textContent = t(messages[event.error] || 'errorRecognition') + ' ' + t('errorCode',{code:event.error}) + help;
   abortListening();
  };
  active.onend = () => {
   if (current !== generation) return;
   clearTimeout(timer);
   recognition = null;
   if (!finalText) el('voice-status').textContent = t('recognitionStopped');
   state();
  };
  el('voice-status').textContent = t('recognitionOpening');
  state();
  try {
   active.start();
   timer = setTimeout(() => { if (recognition === active) el('mic').onclick(); }, 45000);
  } catch (_) {
   failed = true;
   abortListening();
   el('voice-status').textContent = t('micOpenFailed');
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
  el('speech-status').textContent = t(el('auto-read').checked ? 'autoReadOn' : 'autoReadOff');
 };
 for (const id of ['language','locale']) el(id).addEventListener('change', () => {
  abortListening(); stopReading();
  el('voice-status').textContent = t('localeChanged');
 });
 window.addEventListener('language-changed', () => state());
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
  el('speech-status').textContent = ''; el('voice-status').textContent = Recognition ? t('speakQuestion') : t('voiceNotSupported');
 });
 function suspend() { abortListening(); stopReading(); }
 document.addEventListener('visibilitychange', () => { if (document.hidden) suspend(); });
 window.addEventListener('pagehide', suspend);
 el('voice-status').textContent = Recognition ? t('speakQuestion') : t('voiceNotSupported');
 if (!synth) el('speech-status').textContent = t('speechNotSupported');
 state();
})();
