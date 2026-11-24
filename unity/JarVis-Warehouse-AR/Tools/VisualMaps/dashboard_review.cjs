const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const html = fs.readFileSync(process.argv[2], 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1];
const elements = new Map();
const waits = new Map();
function element(identifier) {
  if (!elements.has(identifier)) elements.set(identifier, { textContent: '', hidden: true, append() {}, replaceChildren() {} });
  return elements.get(identifier);
}
function reply(data) { return { ok: true, json: async () => data }; }
const context = vm.createContext({
  document: { getElementById: element, createElement: () => ({}) },
  fetch: async path => {
    if (path === '/status') return reply({ state: 'idle', stage: 'Ready' });
    if (path === '/maps') return reply({ maps: [] });
    return new Promise(resolve => waits.set(path, resolve));
  },
  setTimeout() {}, prompt: () => null,
});
vm.runInContext(script, context);
const settle = () => new Promise(resolve => setImmediate(resolve));
async function run() {
  await settle();
  assert.equal(element('maps').textContent, 'Chưa có map dựng thành công. Nhập ZIP để bắt đầu.');
  const first = vm.runInContext('show("room_a")', context);
  waits.get('/maps/room_a')(reply({ id: 'room_a', name: 'Room A', points: [], registered: 20, total: 20 }));
  await settle();
  const second = vm.runInContext('show("room_b")', context);
  waits.get('/maps/room_b')(reply({ id: 'room_b', name: 'Room B', points: [], registered: 22, total: 25 }));
  await settle();
  waits.get('/maps/room_b/geometry')(reply({ state: 'missing' }));
  await settle();
  waits.get('/maps/room_b/objects')(reply({ state: 'complete', observations: [], note: 'B only' }));
  await second;
  waits.get('/maps/room_a/geometry')(reply({ state: 'missing' }));
  await first;
  assert.equal(element('name').textContent, 'Room B');
  assert.match(element('objects').textContent, /B only/);
  assert.ok(!waits.has('/maps/room_a/objects'));
  await vm.runInContext('document.getElementById("rename").onclick()', context);
  assert.equal(element('name').textContent, 'Room B');
  console.log('PASS dashboard syntax, empty library, late-map response isolation and rename cancel; DOM fixture, not browser visual acceptance.');
}
run().catch(error => { console.error(error); process.exitCode = 1; });
