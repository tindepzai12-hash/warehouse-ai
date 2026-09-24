const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { ACTION_TYPES, Store, validateAnswer, providerAnswer, buildSystemPrompt } = require('../server');

const PRESENTATION_ACTIONS = [
  'show_station_intro',
  'show_item_details',
  'highlight_storage_location',
  'show_navigation_route'
];

function answer(type) {
  return {
    message: 'Validated response',
    sources: [{ documentId: 'SOP-TEST', sectionId: '1' }],
    action: { type, exceptionCode: null, targetId: 'demo-target' },
    needsClarification: false,
    clarifyingQuestion: null,
    feedbackType: 'explanation',
    priority: 'normal',
    cooldownSeconds: 0
  };
}

test('the four presentation actions accepted by Structured Outputs pass runtime validation', () => {
  const chunks = [{ documentId: 'SOP-TEST', sectionId: '1', exceptionCode: null }];
  for (const type of PRESENTATION_ACTIONS) {
    assert.ok(ACTION_TYPES.includes(type));
    assert.equal(validateAnswer(answer(type), chunks).action.type, type);
  }
});

test('citation-free general answers and follow-ups pass, but invented citations fail', () => {
  const sourceFree = answer('none');
  sourceFree.sources = [];
  const chunks = [{ documentId: 'SOP-TEST', sectionId: '1', exceptionCode: null }];
  assert.doesNotThrow(() => validateAnswer(sourceFree, []));
  assert.doesNotThrow(() => validateAnswer(sourceFree, chunks, { hasHistory: true }));
  assert.doesNotThrow(() => validateAnswer(sourceFree, chunks));
  const invented = answer('none');
  invented.sources = [{ documentId: 'NOT-RETRIEVED', sectionId: 'X' }];
  assert.throws(() => validateAnswer(invented, chunks), /Feedback source validation failed/);
});

test('SQLite creates a missing data directory, database, and all tables before use', async t => {
  const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'warehouse-ai-'));
  t.after(() => fs.rmSync(tempRoot, { recursive: true, force: true }));
  const database = path.join(tempRoot, 'nested', 'data', 'warehouse.sqlite3');
  const store = new Store(database);
  await store.ready;
  assert.equal(fs.existsSync(database), true);
  const rows = await store.all("SELECT name FROM sqlite_master WHERE type='table'");
  const names = new Set(rows.map(row => row.name));
  for (const name of ['users', 'sessions', 'replies', 'usage', 'inflight']) assert.ok(names.has(name));
  await new Promise((resolve, reject) => store.db.close(error => error ? reject(error) : resolve()));
});

test('OpenAI instructions require the selected response language', async () => {
  let requestBody;
  const fetchImpl = async (_url, options) => {
    requestBody = JSON.parse(options.body);
    return {
      status: 200,
      ok: true,
      async json() {
        return { status: 'completed', output: [{ type: 'message', content: [{ type: 'output_text', text: JSON.stringify({ message: '好的', sources: [], action: { type: 'none', exceptionCode: null, targetId: null }, needsClarification: true, clarifyingQuestion: '请提供任务。', feedbackType: 'explanation', priority: 'normal', cooldownSeconds: 0 }) }] }] };
      }
    };
  };
  await providerAnswer(
    { apiKey: 'test', model: 'test-model', maxTokens: 100, timeout: 1 },
    { language: 'zh', locale: 'zh-TW', question: '請協助我處理盤點差異。' },
    [{ documentId: 'SOP-TEST', sectionId: '1', text: '测试' }],
    [],
    { synthetic: true },
    fetchImpl
  );
  assert.match(requestBody.instructions, /strictly in Traditional Chinese \(zh-TW, Taiwanese Mandarin\)/);
  assert.match(requestBody.instructions, /follow-up related to a retrieved SOP/);
  assert.equal(requestBody.input[0].content, '請協助我處理盤點差異。');
  assert.equal(requestBody.text.format.strict, true);
});

test('general-knowledge prompt explicitly permits a source-free explanation', () => {
  const prompt = buildSystemPrompt(
    { timeZone: 'Asia/Ho_Chi_Minh', instruction: 'Reply strictly in Vietnamese.' },
    { question: 'Kho ngoại quan là gì?' },
    'thứ Tư, 10:00',
    '{}',
    []
  );
  assert.match(prompt, /general knowledge/);
  assert.match(prompt, /action.type = "none"/);
  assert.match(prompt, /sources = \[\]/);
});
