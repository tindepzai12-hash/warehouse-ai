/* Explicit, local-only microphone diagnostic. No recording or upload. */
(() => {
 const button = document.getElementById('mic-test');
 const status = document.getElementById('mic-diagnostic');
 const meter = document.getElementById('mic-level');
 const t = (key, values) => window.JarvisI18n.t(key, values);
 button.textContent = t('micTest');
 let stream = null, context = null, interval = null, deadline = null, epoch = 0, running = false;
 function stop(error = false) {
  epoch++;
  clearInterval(interval); clearTimeout(deadline);
  stream?.getTracks().forEach(track => track.stop()); stream = null;
  if (context) { context.close().catch(() => {}); context = null; }
  running = false; meter.value = 0; button.textContent = t('micTest');
  window.dispatchEvent(new CustomEvent('mic-test-end',{detail:{error}}));
 }
 button.onclick = async () => {
  if (running) { stop(); status.textContent = t('micTestStopped'); return; }
  if (!navigator.mediaDevices?.getUserMedia) {
   status.textContent = t('micTestUnsupported');
   return;
  }
  running = true;
  const current = ++epoch;
  window.dispatchEvent(new Event('mic-test-start'));
  button.textContent = t('micTestStop');
  status.textContent = t('micTestRequest');
  try {
   const acquired = await navigator.mediaDevices.getUserMedia({audio:true});
   if (current !== epoch) { acquired.getTracks().forEach(track => track.stop()); return; }
   stream = acquired;
   const AudioContext = window.AudioContext || window.webkitAudioContext;
   if (!AudioContext) { stop(); status.textContent = t('micTestNoMeter'); return; }
   context = new AudioContext();
   await context.resume();
   if (current !== epoch) return;
   const analyser = context.createAnalyser(); analyser.fftSize = 2048;
   context.createMediaStreamSource(stream).connect(analyser); // Never connect to speakers.
   const samples = new Float32Array(analyser.fftSize);
   let peak = 0;
   status.textContent = t('micTestSpeak');
   interval = setInterval(() => {
    analyser.getFloatTimeDomainData(samples);
    const rms = Math.sqrt(samples.reduce((sum,n) => sum+n*n,0)/samples.length);
    peak = Math.max(peak,rms); meter.value = Math.min(1,rms*8);
   }, 80);
   deadline = setTimeout(() => {
    stop(peak < .005);
    status.textContent = t(peak >= .005 ? 'micTestSuccess' : 'micTestSilent');
   },5000);
  } catch (error) {
   if (current !== epoch) return;
   stop(true);
   const messages = {NotAllowedError:'micTestPermission',NotFoundError:'micTestMissing',NotReadableError:'micTestBusy'};
   status.textContent = t(messages[error.name] || 'micTestGeneric') + ' ' + t('errorCode',{code:error.name});
  }
 };
 function cancel() { if (running) { stop(); status.textContent = t('micTestStopped'); } }
 window.addEventListener('chat-start',cancel);
 window.addEventListener('chat-reset',cancel);
 window.addEventListener('language-changed',() => { if (!running) button.textContent = t('micTest'); });
 window.addEventListener('pagehide',cancel);
 document.addEventListener('visibilitychange',() => { if (document.hidden) cancel(); });
})();
