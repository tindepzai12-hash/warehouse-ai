const Busboy = require('busboy');
const MAX_AUDIO_BYTES = 2048044;
function voiceError(code, detail, status = 422) {
  return Object.assign(new Error(detail), {code, status});
}
// Called only after mobile authentication. No temp files; bounded file and envelope.
function readVoiceUpload(req, signal) {
  return new Promise((resolve, reject) => {
    let parser, audio, failure, total = 0, settled = false;
    const finish = (error) => {
      if (settled) return;
      settled = true; clearTimeout(timer);
      req.removeListener('data', count); req.removeListener('aborted', aborted);
      signal?.removeEventListener('abort', aborted);
      req.unpipe(parser);
      if (error) { audio = null; parser?.destroy(); req.resume(); reject(error); }
      else resolve(audio);
    };
    const aborted = () => finish(voiceError('TIMEOUT', 'Voice request cancelled or timed out', 504));
    const count = chunk => { total += chunk.length; if (total > MAX_AUDIO_BYTES + 16384) finish(voiceError('AUDIO_TOO_LARGE', 'Audio upload is too large', 413)); };
    const timer = setTimeout(aborted, 20000);
    if (!/^multipart\/form-data\s*;/i.test(req.get('Content-Type') || '')) {
      finish(voiceError('INVALID_AUDIO', 'Send one WAV file as multipart field audio')); return;
    }
    if (Number(req.get('Content-Length')) > MAX_AUDIO_BYTES + 16384) {
      finish(voiceError('AUDIO_TOO_LARGE', 'Audio upload is too large', 413)); return;
    }
    try { parser = Busboy({headers:req.headers, preservePath:true, limits:{files:1, fields:0, parts:2, fileSize:MAX_AUDIO_BYTES, headerPairs:20}}); }
    catch (_) { finish(voiceError('INVALID_AUDIO', 'Malformed multipart upload')); return; }
    parser.on('file', (name, file, info) => {
      const chunks = []; let size = 0;
      if (name !== 'audio' || !/^[A-Za-z0-9_-]{1,80}\.wav$/i.test(info.filename || '') || !['audio/wav','audio/x-wav','audio/wave'].includes(info.mimeType)) {
        failure = voiceError('INVALID_AUDIO', 'Use the audio field with a WAV filename and MIME type'); file.resume(); return;
      }
      file.on('limit', () => { failure = voiceError('AUDIO_TOO_LARGE', 'Audio upload is too large', 413); chunks.length = 0; });
      file.on('data', chunk => { if (!failure) { size += chunk.length; chunks.push(chunk); } });
      file.on('end', () => { if (!failure) audio = Buffer.concat(chunks, size); chunks.length = 0; });
      file.on('error', () => finish(voiceError('INVALID_AUDIO', 'Incomplete audio upload')));
    });
    for (const event of ['filesLimit','fieldsLimit','partsLimit']) parser.on(event, () => { failure = voiceError('INVALID_AUDIO', 'Send exactly one audio file and no extra fields'); });
    parser.on('error', () => finish(voiceError('INVALID_AUDIO', 'Malformed multipart upload')));
    parser.on('close', () => finish(failure || (!audio?.length ? voiceError('NO_AUDIO', 'No audio was uploaded') : null)));
    req.on('data', count); req.on('aborted', aborted); signal?.addEventListener('abort', aborted, {once:true});
    if (signal?.aborted) { aborted(); return; }
    req.pipe(parser);
  });
}
module.exports = {readVoiceUpload, voiceError, MAX_AUDIO_BYTES};
