/* Turn a spoken greeting addressed to JarVis into a friendly, name-free greeting. */
window.handleSpokenGreeting = function(transcript) {
 const text = String(transcript || '').trim();
 const name = '(?:jar\\s*vis|jarvis|travis|gia\\s*vis|gia\\s*vít|ja\\s*vis|jar\\s*vit|jar\\s*vít|cha\\s*vis|cha\\s*vít)';
 const greeting = '(?:xin\\s+chào|chào|ê|hello|hi)';
 const prefix = new RegExp('^\\s*' + greeting + '\\s+' + name + '(?:\\s+ơi)?(?:\\s*[,;:!?-]\\s*|\\s+|$)', 'iu');
 const vocative = new RegExp('^\\s*' + name + '\\s+ơi(?:\\s*[,;:!?-]\\s*|\\s+|$)', 'iu');
 const match = text.match(prefix) || text.match(vocative);
 if (!match) return text;
 const remainder = text.slice(match[0].length).trim();
 return remainder ? `Xin chào bạn! ${remainder}` : 'Xin chào bạn!';
};

/* Kept as a compatibility alias for callers from earlier builds. */
window.normalizeTranscription = window.handleSpokenGreeting;
window.normalizeJarVisAddress = window.handleSpokenGreeting;
