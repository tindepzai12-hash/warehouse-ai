/* Only normalize a known spoken address at the start, never item names/numbers. */
window.normalizeJarVisAddress = function(text) {
 return text.replace(/^(\s*(?:(?:xin chào|chào|hello|hey|ê)\s+)?)(?:jar\s*vis|jar\s*vit|gia\s*v[íiì]t|gia\s*vis|ja\s*v[íi]t|cha\s*v[íi]t|gia\s*vích|gia\s*vít)(?=[\s,!.?:;]|$)/iu, '$1JarVis');
};

/* Keep the assistant state element intact while placing its visual at page center. */
if (typeof window.addEventListener === 'function') window.addEventListener('DOMContentLoaded', () => {
 const assistant = document.getElementById('assistant');
 const layout = document.querySelector('.layout');
 if (!assistant || !layout || document.querySelector('.jarvis-hero')) return;
 const stage = assistant.querySelector('.jarvis-stage');
 if (stage) stage.classList.add('jarvis-articulated');
 const hero = document.createElement('section');
 hero.className = 'jarvis-hero';
 hero.setAttribute('aria-label', 'JarVis');
 layout.before(hero);
 hero.append(assistant);
});
