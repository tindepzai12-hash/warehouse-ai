const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

const token = 'local-smoke-test-token';
const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'warehouse-ai-smoke-'));
process.env.DATABASE_PATH = path.join(tempRoot, 'warehouse.sqlite3');
process.env.PROTOTYPE_TOKEN_SHA256 = crypto.createHash('sha256').update(token).digest('hex');
process.env.OPENAI_API_KEY = 'test-api-key';
process.env.OPENAI_MODEL = 'test-model';
process.env.CORS_ALLOWED_ORIGINS = '';

const { createApp } = require('../server');

test('Node service serves the UI and authenticated OpenAI RAG API end to end', async t => {
  const nativeFetch = global.fetch;
  global.fetch = async (url, options) => {
    if (String(url) !== 'https://api.openai.com/v1/responses') return nativeFetch(url, options);
    const body = JSON.parse(options.body);
    assert.equal(body.input[0].content, 'Cần 5 hộp kem nhưng chỉ có 3 tôi phải làm gì?');
    return {
      status: 200,
      ok: true,
      async json() {
        return {
          status: 'completed',
          output: [{ type: 'message', content: [{ type: 'output_text', text: JSON.stringify({
            message: 'Tôi đã phân tích tình huống thiếu hàng theo SOP được truy xuất.',
            sources: [{ documentId: 'SOP-SHORT-PICK', sectionId: 'reporting' }],
            action: { type: 'explain_sop_step', exceptionCode: null, targetId: null },
            needsClarification: false,
            clarifyingQuestion: null,
            feedbackType: 'explanation',
            priority: 'normal',
            cooldownSeconds: 0
          }) }] }]
        };
      }
    };
  };
  const app = createApp();
  await app.locals.bootstrapPromise;
  const server = app.listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;

  t.after(async () => {
    await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
    await new Promise((resolve, reject) => app.locals.store.db.close(error => error ? reject(error) : resolve()));
    global.fetch = nativeFetch;
    fs.rmSync(tempRoot, { recursive: true, force: true });
  });

  for (const route of ['/', '/static/style.css', '/static/app.js', '/static/assets/jarvis-welcome.jpg', '/api/health']) {
    const response = await fetch(`${base}${route}`);
    assert.equal(response.status, 200, route);
  }
  const crossOrigin = await fetch(`${base}/api/health`, { headers: { Origin: 'https://untrusted.example' } });
  assert.equal(crossOrigin.status, 403);

  const auth = { Authorization: `Bearer ${token}` };
  const me = await fetch(`${base}/api/me`, { headers: auth });
  assert.equal(me.status, 200);
  assert.equal((await me.json()).id, 'render-demo');

  const requestId = crypto.randomUUID();
  const payload = {
    requestId,
    taskId: 'TEST-PICK-001',
    language: 'vi',
    locale: 'vi-VN',
    eventType: 'user_question',
    question: 'Cần 5 hộp kem nhưng chỉ có 3 tôi phải làm gì?',
    observation: { reportedVisibleQuantity: 3 }
  };
  const feedback = await fetch(`${base}/api/feedback`, {
    method: 'POST',
    headers: { ...auth, 'Content-Type': 'application/json' },
    body: JSON.stringify(payload)
  });
  assert.equal(feedback.status, 200);
  const answer = await feedback.json();
  assert.equal(answer.mode, 'openai');
  assert.equal(answer.language, 'vi');
  assert.equal(answer.locale, 'vi-VN');
  assert.equal(answer.operationalMutation, false);
  assert.ok(answer.message.includes('phân tích tình huống'));
  assert.ok(answer.sources.length > 0);

  const conflict = await fetch(`${base}/api/feedback`, {
    method: 'POST',
    headers: { ...auth, 'Content-Type': 'application/json' },
    body: JSON.stringify({ ...payload, question: 'Nội dung khác' })
  });
  assert.equal(conflict.status, 409);

  const invalidAuth = await fetch(`${base}/api/me`, { headers: { Authorization: 'Bearer invalid' } });
  assert.equal(invalidAuth.status, 401);
});
