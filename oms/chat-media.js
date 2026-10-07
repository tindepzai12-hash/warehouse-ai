// Ephemeral chat media: validated in memory, never saved as warehouse evidence or inventory.
const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
function invalid(message, status = 422) { throw Object.assign(new Error(message), { status }); }
function replyLanguage(message, fallback = 'vi') {
  const text = String(message || '').toLowerCase();
  if (/\b(?:in english|reply in english|answer in english)\b|bằng tiếng anh|bang tieng anh/.test(text)) return 'en';
  if (/\b(?:in vietnamese|reply in vietnamese)\b|bằng tiếng việt|bang tieng viet/.test(text)) return 'vi';
  if (/[àáạảãăằắặẳẵâầấậẩẫèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ]/.test(text) || /\b(?:toi|ban|trong kho|la gi|giai thich|sang loc|sap xep|cam on|tiep tuc)\b/.test(text)) return 'vi';
  if (/\b(?:what|how|why|where|when|which|explain|please|warehouse|inventory|hello|thanks|thank you|yes|continue|show|this|these|image|photo|safety|help)\b/.test(text)) return 'en';
  return fallback === 'en' ? 'en' : 'vi';
}
function decodeBase64(value, maxBytes) {
  if (typeof value !== 'string' || !value.length || value.length > Math.ceil(maxBytes / 3) * 4 || value.length % 4 || !/^[A-Za-z0-9+/]*={0,2}$/.test(value)) invalid('Invalid or oversized media');
  const bytes = Buffer.from(value, 'base64');
  if (!bytes.length || bytes.length > maxBytes || bytes.toString('base64') !== value) invalid('Invalid or oversized media');
  return bytes;
}
function chatImages(images) {
  if (images === undefined) return [];
  if (!Array.isArray(images) || images.length > 1) invalid('Attach at most one photo');
  return images.map(image => {
    if (!image || Object.keys(image).some(k => !['mimeType', 'data'].includes(k))) invalid('Invalid photo fields');
    const bytes = decodeBase64(image.data, MAX_IMAGE_BYTES);
    const png = image.mimeType === 'image/png' && bytes.subarray(0,8).equals(Buffer.from([137,80,78,71,13,10,26,10]));
    const jpeg = image.mimeType === 'image/jpeg' && bytes[0] === 255 && bytes[1] === 216 && bytes[2] === 255 && bytes.at(-2) === 255 && bytes.at(-1) === 217;
    if (!png && !jpeg) invalid('Only JPEG or PNG photos are supported');
    return { mimeType: image.mimeType, data: image.data };
  });
}
function voiceWav(data) {
  const bytes = decodeBase64(data, 2048044);
  return validateWav(bytes);
}
function validateWav(bytes, rejectSilence = false) {
  if (!Buffer.isBuffer(bytes) || bytes.length > 2048044) invalid('Invalid or oversized audio');
  if (bytes.length < 44 || bytes.toString('ascii',0,4) !== 'RIFF' || bytes.toString('ascii',8,12) !== 'WAVE' || bytes.toString('ascii',12,16) !== 'fmt ' || bytes.readUInt32LE(16) !== 16 || bytes.readUInt16LE(20) !== 1 || bytes.readUInt16LE(22) !== 1 || bytes.readUInt16LE(34) !== 16 || bytes.toString('ascii',36,40) !== 'data') invalid('Use mono PCM16 WAV audio');
  const rate=bytes.readUInt32LE(24), size=bytes.readUInt32LE(40);
  if (rate < 8000 || rate > 32000 || bytes.readUInt32LE(28) !== rate*2 || bytes.readUInt16LE(32) !== 2 || size !== bytes.length-44 || size%2 || bytes.readUInt32LE(4) !== bytes.length-8 || size/(rate*2) < .25 || size/(rate*2) > 32) invalid('Voice recording must be 0.25–32 seconds');
  if (rejectSilence) {
    let energy=0,peak=0;
    for(let i=44;i<bytes.length;i+=2){const sample=bytes.readInt16LE(i)/32768;energy+=sample*sample;peak=Math.max(peak,Math.abs(sample));}
    if(peak<.008||Math.sqrt(energy/(size/2))<.001) throw Object.assign(new Error('No audible speech detected; try speaking closer to the microphone'),{status:422,code:'NO_AUDIO'});
  }
  return bytes;
}
async function mediaFetch(settings, route, options, fetchImpl = fetch, signal) {
  if (!settings.apiKey) throw Object.assign(new Error('OpenAI voice is not configured'),{status:503,code:'OPENAI_CONFIGURATION_ERROR'});
  const controller=new AbortController(), timer=setTimeout(()=>controller.abort(),65000);
  const cancel=()=>controller.abort();signal?.addEventListener('abort',cancel,{once:true});if(signal?.aborted)cancel();
  try {
    const response=await fetchImpl(settings.baseUrl+route,{...options,signal:controller.signal,headers:{Authorization:'Bearer '+settings.apiKey,...options.headers}});
    if(response.status===429)invalid('OpenAI quota/rate limit reached',503);
    if(response.status===401||response.status===403)throw Object.assign(new Error('OpenAI voice configuration needs attention'),{status:503,code:'OPENAI_CONFIGURATION_ERROR'});
    if(!response.ok)throw Object.assign(new Error('OpenAI voice request failed'),{status:502,code:'TRANSCRIPTION_FAILED'});
    if(route.endsWith('transcriptions')) {
      const result=await response.json();
      if(typeof result.text!=='string'||!result.text.trim())throw Object.assign(new Error('No usable speech detected'),{status:422,code:'EMPTY_TRANSCRIPT'});
      if(result.text.length>1000)invalid('Record a shorter question',422);
      return {text:result.text.trim(),language:replyLanguage(result.text)};
    }
    const bytes=Buffer.from(await response.arrayBuffer());
    if(!bytes.length||bytes.length>4*1024*1024)invalid('Invalid speech response',502);
    return bytes;
  } catch(error) { if(error.status)throw error;throw Object.assign(new Error(error.name==='AbortError'?'OpenAI voice timed out':'OpenAI voice connection failed'),{status:error.name==='AbortError'?504:502,code:error.name==='AbortError'?'TIMEOUT':'NETWORK_ERROR'}); }
  finally {clearTimeout(timer);signal?.removeEventListener('abort',cancel);}
}
// Vocabulary hints only: never seed an expected answer or force the UI locale.
const TRANSCRIPTION_PROMPT = 'Speech may be Vietnamese, English, or a mixture of both. Preserve the spoken language and Vietnamese diacritics; do not translate or answer. Warehouse vocabulary, when spoken: JarVis, Justin, SKU, QR code, barcode, 5S, FIFO, FEFO, QC, inventory, stock location, mã hàng, mã vạch, tồn kho, vị trí kệ, kiểm kê, nhập kho, xuất kho, nhiệm vụ.';
function voiceProvider(settings, fetchImpl = fetch) {
  return {
    transcribe: async (bytes,signal) => {
      const form=new FormData();form.append('model',settings.transcriptionModel || 'gpt-4o-mini-transcribe');form.append('file',new Blob([bytes],{type:'audio/wav'}),'question.wav');form.append('response_format','json');form.append('prompt',TRANSCRIPTION_PROMPT);
      return mediaFetch(settings,'/audio/transcriptions',{method:'POST',body:form},fetchImpl,signal);
    },
    speak: async text => mediaFetch(settings,'/audio/speech',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({model:'tts-1',voice:'nova',input:text,response_format:'mp3'})},fetchImpl)
  };
}
module.exports={replyLanguage,chatImages,voiceWav,validateWav,voiceProvider};
