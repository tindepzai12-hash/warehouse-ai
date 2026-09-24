/* Normalize common Vietnamese speech-recognition variants to the assistant name. */
window.normalizeTranscription = function(text) {
 return String(text).replace(/^(\s*(?:(?:xin chào|chào|hello|hey|ê)\s+)?)(?:jar\s*vis|travis|gia\s*vis|gia\s*vít|ja\s*vis|jar\s*vit|jar\s*vít|cha\s*vis|cha\s*vít)(?=$|[^\p{L}\p{N}])/iu, '$1JarVis');
};
window.normalizeJarVisAddress = window.normalizeTranscription;
