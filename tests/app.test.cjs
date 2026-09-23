const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../static/app.js'), 'utf8');

class Element {
  constructor() {
    this.value = '';
    this.textContent = '';
    this.className = '';
    this.dataset = {};
    this.children = [];
    this.disabled = false;
    this.scrollTop = 0;
    this.scrollHeight = 1;
  }

  append(...nodes) { this.children.push(...nodes); }
  replaceChildren(...nodes) { this.children = nodes; }
  focus() { this.focused = true; }
  get lastElementChild() { return this.children[this.children.length - 1]; }
}

function setupApp({ feedback } = {}) {
  const elements = new Map();
  const get = id => {
    if (!elements.has(id)) elements.set(id, new Element());
    return elements.get(id);
  };
  const listeners = new Map();
  const window = {
    JarvisI18n: {
      t: key => key,
      defaults: { vi: 'vi-VN', en: 'en-US', zh: 'zh-TW' }
    },
    addEventListener(type, handler) { listeners.set(type, handler); },
    dispatchEvent(event) { listeners.get(event.type)?.(event); }
  };
  class Event { constructor(type) { this.type = type; } }
  class CustomEvent extends Event { constructor(type, init = {}) { super(type); this.detail = init.detail; } }
  const fetch = async url => {
    if (url === '/api/health') return { json: async () => ({ mode: 'openai' }) };
    if (url === '/api/feedback' && feedback) return feedback;
    if (url === '/api/feedback') throw new Error('network outage');
    throw new Error(`Unexpected URL: ${url}`);
  };
  const context = {
    window,
    document: { getElementById: get, createElement: () => new Element() },
    crypto: { randomUUID: () => 'request-id' },
    AbortSignal,
    Event,
    CustomEvent,
    fetch,
    console
  };
  get('token').value = 'prototype-token';
  get('question').value = '  Keep this exact draft  ';
  get('task').value = 'TASK-001';
  get('language').value = 'vi';
  get('locale').value = 'vi-VN';
  get('event').value = 'user_question';
  vm.runInNewContext(source, context);
  return { get };
}

test('failed feedback restores the exact draft after clearing it immediately', async () => {
  const app = setupApp();
  const pending = app.get('send').onclick();

  assert.equal(app.get('question').value, '', 'the textarea clears while the request is pending');

  await pending;

  assert.equal(app.get('question').value, '  Keep this exact draft  ');
  assert.equal(app.get('retry').disabled, false);
  assert.equal(app.get('status').className, 'error');
});

test('renders a localized RAG source when the API returns source text as a string', async () => {
  const app = setupApp({
    feedback: {
      status: 200,
      ok: true,
      headers: { get: () => 'request-id' },
      json: async () => ({
        sessionId: 'session-id',
        message: 'Đã tìm thấy SOP phù hợp.',
        sourceDetails: [{
          documentId: 'SOP_INB_001',
          sectionId: 'RECEIVING_01',
          text: 'Bước 1: Tiếp nhận thông báo giao hàng.',
          source: 'Local SOP catalog',
          source_status: 'official_release',
          site_approved: false
        }],
        action: { type: 'explain_sop_step', exceptionCode: null },
        mode: 'openai',
        latencyMs: 3,
        contextTurns: 0,
        requestId: 'request-id'
      })
    }
  });

  await app.get('send').onclick();

  assert.equal(app.get('sources').children[0].children[1].textContent, 'Bước 1: Tiếp nhận thông báo giao hàng.');
});
