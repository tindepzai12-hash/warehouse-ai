/* Only normalize a known spoken address at the start, never item names/numbers. */
window.normalizeJarVisAddress = function(text) {
 return text.replace(/^(\s*(?:(?:xin chào|chào|hello|hey|ê)\s+)?)(?:jar\s*vis|jar\s*vit|gia\s*v[íiì]t|gia\s*vis|ja\s*v[íi]t|cha\s*v[íi]t|gia\s*vích|gia\s*vít)(?=[\s,!.?:;]|$)/iu, '$1JarVis');
};
