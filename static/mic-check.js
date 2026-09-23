/* Explicit, local-only microphone diagnostic. No recording or upload. */
(() => {
 const button = document.getElementById('mic-test');
 const status = document.getElementById('mic-diagnostic');
 const meter = document.getElementById('mic-level');
 let stream = null, context = null, interval = null, deadline = null, epoch = 0, running = false;
 function stop(error = false) {
  epoch++;
  clearInterval(interval); clearTimeout(deadline);
  stream?.getTracks().forEach(track => track.stop()); stream = null;
  if (context) { context.close().catch(() => {}); context = null; }
  running = false; meter.value = 0; button.textContent = 'Kiểm tra micro (5 giây)';
  window.dispatchEvent(new CustomEvent('mic-test-end',{detail:{error}}));
 }
 button.onclick = async () => {
  if (running) { stop(); status.textContent = 'Đã dừng kiểm tra micro.'; return; }
  if (!navigator.mediaDevices?.getUserMedia) {
   status.textContent = 'Trình duyệt không hỗ trợ kiểm tra micro. Mở website bằng HTTPS trong trình duyệt đầy đủ.';
   return;
  }
  running = true;
  const current = ++epoch;
  window.dispatchEvent(new Event('mic-test-start'));
  button.textContent = 'Dừng kiểm tra micro';
  status.textContent = 'Đang xin quyền micro. Bài kiểm tra chỉ đo mức âm thanh trên máy, không lưu hay gửi âm thanh.';
  try {
   const acquired = await navigator.mediaDevices.getUserMedia({audio:true});
   if (current !== epoch) { acquired.getTracks().forEach(track => track.stop()); return; }
   stream = acquired;
   const AudioContext = window.AudioContext || window.webkitAudioContext;
   if (!AudioContext) { stop(); status.textContent = 'Đã truy cập được micro nhưng trình duyệt không hỗ trợ đo tín hiệu.'; return; }
   context = new AudioContext();
   await context.resume();
   if (current !== epoch) return;
   const analyser = context.createAnalyser(); analyser.fftSize = 2048;
   context.createMediaStreamSource(stream).connect(analyser); // Never connect to speakers.
   const samples = new Float32Array(analyser.fftSize);
   let peak = 0;
   status.textContent = 'Hãy nói vài từ trong 5 giây. Thanh bên dưới hiển thị mức âm thanh từ micro.';
   interval = setInterval(() => {
    analyser.getFloatTimeDomainData(samples);
    const rms = Math.sqrt(samples.reduce((sum,n) => sum+n*n,0)/samples.length);
    peak = Math.max(peak,rms); meter.value = Math.min(1,rms*8);
   }, 80);
   deadline = setTimeout(() => {
    stop(peak < .005);
    status.textContent = peak >= .005
     ? 'Micro đã thu được tín hiệu âm thanh. Nếu Nói câu hỏi vẫn lỗi, hãy kiểm tra dịch vụ nhận dạng Edge; thử Chrome hoặc Windows + H. Tín hiệu có thể gồm cả tiếng nền.'
     : 'Chưa phát hiện tín hiệu rõ. Kiểm tra nút tắt micro, thiết bị đầu vào và quyền Microphone trong Windows, rồi thử lại.';
   },5000);
  } catch (error) {
   if (current !== epoch) return;
   stop(true);
   const messages = {NotAllowedError:'Quyền micro bị chặn ở trình duyệt hoặc Windows.',NotFoundError:'Không tìm thấy micro.',NotReadableError:'Không mở được micro; kiểm tra thiết bị hoặc ứng dụng khác đang dùng micro.'};
   status.textContent = (messages[error.name] || 'Không kiểm tra được micro.') + ' Mã: ' + error.name;
  }
 };
 function cancel() { if (running) { stop(); status.textContent = 'Đã dừng kiểm tra micro.'; } }
 window.addEventListener('chat-start',cancel);
 window.addEventListener('chat-reset',cancel);
 window.addEventListener('pagehide',cancel);
 document.addEventListener('visibilitychange',() => { if (document.hidden) cancel(); });
})();
