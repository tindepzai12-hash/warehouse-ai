const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const http = require('http');
const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const sqlite3 = require('sqlite3').verbose();
require('dotenv').config({ path: path.join(__dirname, '.env') });

const ROOT = __dirname;
const LIMITATION = {
  en: 'This prototype has no live inventory, approved route or completed transaction. Which task, verified item/lot or approved site procedure applies?',
  vi: 'Bản thử chưa có tồn kho trực tiếp, tuyến đường được duyệt hoặc giao dịch đã hoàn tất. Bạn đang nói đến nhiệm vụ, hàng/lô đã xác minh hoặc SOP được duyệt nào?',
  zh: '此原型没有实时库存、已批准路线或已完成交易。请提供相关任务、已核实的商品/批次或现场批准的规程。'
};
const LANGUAGE_PROFILES = Object.freeze({
  en: { name: 'English', locale: 'en-GB', timeZone: 'Europe/London', instruction: 'Reply strictly in English.' },
  vi: { name: 'Vietnamese', locale: 'vi-VN', timeZone: 'Asia/Ho_Chi_Minh', instruction: 'Reply strictly in Vietnamese.' },
  zh: { name: 'Traditional Chinese (Taiwan)', locale: 'zh-TW', timeZone: 'Asia/Taipei', instruction: 'Reply strictly in Traditional Chinese (zh-TW, Taiwanese Mandarin).' }
});
const RUNTIME_SOURCE_STATUSES = new Set(['spec_confirmed_project_policy', 'official_release']);
function responseLocale(request) {
  // Chinese output is always Traditional Chinese for Taiwan, even if an older client sends zh-CN.
  return request.language === 'zh' ? LANGUAGE_PROFILES.zh.locale : (request.locale || LANGUAGE_PROFILES[request.language].locale);
}
function parseCorsOrigins(value = '') {
  const origins = new Set();
  for (const rawOrigin of value.split(',').map(item => item.trim()).filter(Boolean)) {
    let parsed;
    try { parsed = new URL(rawOrigin); } catch (_) { throw new Error('CORS_ALLOWED_ORIGINS must contain comma-separated http(s) origins'); }
    if (!['http:', 'https:'].includes(parsed.protocol)) throw new Error('CORS_ALLOWED_ORIGINS must contain comma-separated http(s) origins');
    if (parsed.origin !== rawOrigin.replace(/\/$/, '')) throw new Error('CORS_ALLOWED_ORIGINS must contain origins without paths');
    origins.add(parsed.origin);
  }
  return origins;
}
function corsOptions(allowedOrigins) {
  return {
    origin(origin, callback) {
      if (!origin || allowedOrigins.has(origin)) return callback(null, true);
      return callback(new HttpError(403, 'Origin is not allowed'));
    },
    methods: ['GET', 'POST', 'OPTIONS'],
    allowedHeaders: ['Authorization', 'Content-Type'],
    maxAge: 86400
  };
}

// Structured Outputs and runtime validation must use the same allowlists.
// Keeping one definition prevents an action accepted by OpenAI's schema from
// being rejected by the server afterwards with a 502 response.
const ACTION_TYPES = Object.freeze([
  'none',
  'show_station_intro',
  'show_item_details',
  'highlight_storage_location',
  'show_navigation_route',
  'request_rescan',
  'show_picking_checklist',
  'explain_sop_step',
  'show_safety_warning',
  'open_exception_report',
  'show_task_summary'
]);
const EXCEPTION_CODES = Object.freeze(['EXC-001', 'EXC-002', 'EXC-003', 'EXC-004', 'EXC-005', null]);

class HttpError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

function settingsFromEnv() {
  const settings = {
    database: process.env.DATABASE_PATH || 'data/warehouse.sqlite3',
    // Production always uses the real OpenAI provider.
    mode: 'openai',
    apiKey: process.env.OPENAI_API_KEY || '',
    model: process.env.OPENAI_MODEL || 'gpt-4o-mini',
    rpm: Number(process.env.MAX_REQUESTS_PER_MINUTE || 20),
    daily: Number(process.env.MAX_REQUESTS_PER_DAY || 100),
    maxTokens: Number(process.env.MAX_OUTPUT_TOKENS || 1200),
    timeout: Number(process.env.REQUEST_TIMEOUT_SECONDS || 30),
    corsAllowedOrigins: parseCorsOrigins(process.env.CORS_ALLOWED_ORIGINS || ''),
    prototypeTokenSha256: (process.env.PROTOTYPE_TOKEN_SHA256 || '').trim()
  };
  if (!settings.apiKey || !settings.model) throw new Error('Set OPENAI_API_KEY and OPENAI_MODEL in server environment');
  if ([settings.rpm, settings.daily, settings.maxTokens, settings.timeout].some(value => value <= 0) || settings.timeout > 60) throw new Error('Positive limits required; timeout must be <=60 seconds');
  return settings;
}

function sha256(value) { return crypto.createHash('sha256').update(value).digest('hex'); }
function jsonFingerprint(value) { return sha256(JSON.stringify(value)); }
function normalize(text) { return text.toLowerCase().replace(/đ/g, 'd').normalize('NFD').replace(/[\u0300-\u036f]/g, ''); }
function retrievalQuery(question) {
  const query = normalize(question);
  // This only broadens retrieval; it never generates a hard-coded warehouse answer.
  const reportedQuantityGap = /\b\d+\s+(?:hop|box(?:es)?|unit(?:s)?)\b[\s\S]{0,80}\b(?:chi co|only|but only)\s+\d+\b/.test(query);
  return reportedQuantityGap ? `${query} short pick shortage thieu` : query;
}
class Store {
  constructor(file) {
    this.path = path.isAbsolute(file) ? file : path.join(ROOT, file);
    fs.mkdirSync(path.dirname(this.path), { recursive: true });
    this.ready = new Promise((resolve, reject) => {
      this.db = new sqlite3.Database(this.path, error => {
        if (error) return reject(error);
        this.db.configure('busyTimeout', 10000);
        this.db.serialize(() => {
          this.db.run('PRAGMA journal_mode=WAL', pragmaError => {
            if (pragmaError) return reject(pragmaError);
            this.db.exec(`CREATE TABLE IF NOT EXISTS users(id TEXT PRIMARY KEY,token_hash TEXT UNIQUE,role TEXT,tasks TEXT);
              CREATE TABLE IF NOT EXISTS sessions(id TEXT PRIMARY KEY,user_id TEXT,task TEXT,history TEXT,topic TEXT,busy_until REAL DEFAULT 0);
              CREATE TABLE IF NOT EXISTS replies(user_id TEXT,request_id TEXT,fingerprint TEXT,response TEXT,PRIMARY KEY(user_id,request_id));
              CREATE TABLE IF NOT EXISTS usage(user_id TEXT,at REAL);
              CREATE TABLE IF NOT EXISTS inflight(user_id TEXT,request_id TEXT,until REAL,PRIMARY KEY(user_id,request_id));
              CREATE INDEX IF NOT EXISTS usage_user_time ON usage(user_id,at);`, schemaError => {
              if (schemaError) return reject(schemaError);
              resolve();
            });
          });
        });
      });
    });
  }
  async run(sql, params = []) { await this.ready; return new Promise((resolve, reject) => this.db.run(sql, params, function (error) { error ? reject(error) : resolve({ changes: this.changes, lastID: this.lastID }); })); }
  async get(sql, params = []) { await this.ready; return new Promise((resolve, reject) => this.db.get(sql, params, (error, row) => error ? reject(error) : resolve(row))); }
  async all(sql, params = []) { await this.ready; return new Promise((resolve, reject) => this.db.all(sql, params, (error, rows) => error ? reject(error) : resolve(rows))); }
  async provision(user, role = 'staff') {
    const token = crypto.randomBytes(32).toString('base64url');
    await this.run('INSERT INTO users VALUES(?,?,?,?) ON CONFLICT(id) DO UPDATE SET token_hash=excluded.token_hash,role=excluded.role,tasks=excluded.tasks', [user, sha256(token), role, JSON.stringify(['TEST-PICK-001', 'TEST-PUT-001', 'TEST-COUNT-001'])]);
    return token;
  }
  async bootstrapDemo(tokenHash) {
    if (!/^[0-9a-f]{64}$/.test(tokenHash)) throw new Error('PROTOTYPE_TOKEN_SHA256 must be a lowercase SHA-256 hex digest');
    await this.run('INSERT INTO users VALUES(?,?,?,?) ON CONFLICT(id) DO UPDATE SET token_hash=excluded.token_hash,role=excluded.role,tasks=excluded.tasks', ['render-demo', tokenHash, 'staff', JSON.stringify(['TEST-PICK-001', 'TEST-PUT-001', 'TEST-COUNT-001'])]);
  }
  async authenticate(token) {
    const row = await this.get('SELECT id,role,tasks FROM users WHERE token_hash=?', [sha256(token)]);
    if (!row) throw new HttpError(401, 'Invalid prototype user token');
    return { id: row.id, role: row.role, tasks: JSON.parse(row.tasks) };
  }
  async begin(user, request, settings) {
    const fingerprint = jsonFingerprint(request);
    const now = Date.now() / 1000;
    const prior = await this.get('SELECT fingerprint,response FROM replies WHERE user_id=? AND request_id=?', [user.id, request.requestId]);
    if (prior) {
      if (prior.fingerprint !== fingerprint) throw new HttpError(409, 'requestId already used for another payload');
      return { session: null, cached: JSON.parse(prior.response), fingerprint };
    }
    if (!user.tasks.includes(request.taskId)) throw new HttpError(403, 'Task is not assigned to this prototype user');
    const sid = request.sessionId || crypto.randomUUID();
    const row = await this.get('SELECT user_id,task,history,topic,busy_until FROM sessions WHERE id=?', [sid]);
    if (request.sessionId && (!row || row.user_id !== user.id)) throw new HttpError(404, 'Session not found');
    if (row && row.task !== request.taskId) throw new HttpError(409, 'Start a new session when changing task');
    if (row && row.busy_until > now) throw new HttpError(409, 'Session is processing; retry later');
    await this.run('DELETE FROM inflight WHERE until<?', [now]);
    if (await this.get('SELECT 1 FROM inflight WHERE user_id=? AND request_id=?', [user.id, request.requestId])) throw new HttpError(409, 'Request is processing; retry the same request later');
    await this.run('DELETE FROM usage WHERE at<?', [now - 86400]);
    const usage = await this.get('SELECT COALESCE(SUM(at>?),0) AS minute,COUNT(*) AS day FROM usage WHERE user_id=?', [now - 60, user.id]);
    if (usage.minute >= settings.rpm || usage.day >= settings.daily) throw new HttpError(429, 'Prototype request budget reached; retry later');
    await this.run('INSERT INTO usage VALUES(?,?)', [user.id, now]);
    await this.run('INSERT INTO inflight VALUES(?,?,?)', [user.id, request.requestId, now + 90]);
    if (!row) await this.run('INSERT INTO sessions VALUES(?,?,?,?,?,?)', [sid, user.id, request.taskId, '[]', '', now + 90]);
    else await this.run('UPDATE sessions SET busy_until=? WHERE id=?', [now + 90, sid]);
    return { session: { id: sid, history: row ? JSON.parse(row.history) : [], topic: row ? row.topic : '' }, cached: null, fingerprint };
  }
  async finish(user, request, session, fingerprint, response, topic) {
    const report = { question: request.question, eventType: request.eventType, occurredAtUtc: request.occurredAtUtc || null, observation: request.observation, evidenceStatus: 'worker_report_not_verified_transaction' };
    const history = session.history.concat([{ role: 'user', content: JSON.stringify(report) }, { role: 'assistant', content: response.message }]);
    await this.run('UPDATE sessions SET history=?,topic=?,busy_until=0 WHERE id=?', [JSON.stringify(history.slice(-20)), topic, session.id]);
    await this.run('INSERT INTO replies VALUES(?,?,?,?)', [user.id, request.requestId, fingerprint, JSON.stringify(response)]);
  }
  async release(sid, userId, requestId) {
    await this.run('UPDATE sessions SET busy_until=0 WHERE id=?', [sid]);
    await this.run('DELETE FROM inflight WHERE user_id=? AND request_id=?', [userId, requestId]);
  }
}

class Knowledge {
  constructor() {
    const reviewedChunks = JSON.parse(fs.readFileSync(path.join(ROOT, 'knowledge', 'reviewed.json'), 'utf8'));
    const localDocuments = JSON.parse(fs.readFileSync(path.join(ROOT, 'knowledge', 'warehouse-sops.json'), 'utf8'));
    if (!Array.isArray(reviewedChunks) || !Array.isArray(localDocuments)) throw new Error('Knowledge files must contain JSON arrays');
    for (const document of localDocuments) {
      if (!document || typeof document !== 'object' ||
        !['documentId', 'sectionId', 'title', 'content', 'source_status', 'runtime_approved', 'language'].every(key => Object.hasOwn(document, key)) ||
        typeof document.documentId !== 'string' || typeof document.sectionId !== 'string' || typeof document.title !== 'string' ||
        typeof document.content !== 'string' || typeof document.source_status !== 'string' || typeof document.runtime_approved !== 'boolean' ||
        typeof document.language !== 'string' || !Array.isArray(document.keywords)) {
        throw new Error('Local SOP catalog contains an invalid document');
      }
    }
    this.catalog = localDocuments;
    this.unverifiedDocuments = localDocuments.filter(document => !document.runtime_approved);
    const localRuntimeChunks = localDocuments.filter(document => document.runtime_approved).map(document => ({
      documentId: document.documentId,
      sectionId: document.sectionId,
      title: document.title,
      version: 'user-supplied-1.0',
      source: `Local SOP catalog · ${document.title}`,
      source_status: document.source_status,
      runtime_approved: true,
      // An official release is allowed for RAG, but no site-level operational approval is inferred.
      site_approved: false,
      review_status: `runtime-approved ${document.source_status}; original language: ${document.language}`,
      sourceLanguage: document.language,
      keywords: document.keywords,
      exceptionCode: null,
      // Preserve the supplied Vietnamese source verbatim. The model translates its answer, not the SOP record.
      text: { en: document.content, vi: document.content, zh: document.content }
    }));
    this.chunks = reviewedChunks.concat(localRuntimeChunks);
    const identifiers = new Set();
    for (const chunk of this.chunks) {
      const identifier = `${chunk.documentId}\u0000${chunk.sectionId}`;
      if (identifiers.has(identifier) || !RUNTIME_SOURCE_STATUSES.has(chunk.source_status) || !chunk.runtime_approved || !Array.isArray(chunk.keywords) || !chunk.text) {
        throw new Error('Knowledge corpus contains invalid, duplicate, or unapproved runtime content');
      }
      identifiers.add(identifier);
    }
  }
  retrieve(question, previousTopic = '') {
    const query = retrievalQuery(question);
    const scored = this.chunks.map(chunk => [chunk, chunk.keywords.reduce((score, keyword) => {
      const key = normalize(keyword);
      const match = /[\u3400-\u9fff]/.test(key) ? query.includes(key) : new RegExp(`(?<!\\w)${key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}(?!\\w)`).test(query);
      return score + (match ? key.length + 3 : 0);
    }, 0)]).filter(([, score]) => score).sort((a, b) => b[1] - a[1]).slice(0, 3).map(([chunk]) => chunk);
    if (scored.length) return scored;
    const followup = ['why', 'what next', 'then', 'that', 'it?', 'tai sao', 'tiep theo', 'tom tat', 'vua roi', '为什么', '然后', '总结'].some(phrase => query.includes(phrase));
    return followup && previousTopic ? this.chunks.filter(chunk => chunk.documentId === previousTopic) : [];
  }
  forModel(question, previousTopic, language) {
    const ranked = this.retrieve(question, previousTopic);
    return ranked.map(chunk => { const copy = { ...chunk }; delete copy.keywords; delete copy.text; copy.text = chunk.text[language] || chunk.text[chunk.sourceLanguage] || chunk.text.vi; return copy; });
  }
}

function validateRequest(body) {
  const allowed = new Set(['requestId', 'sessionId', 'taskId', 'language', 'locale', 'eventType', 'occurredAtUtc', 'question', 'observation', 'messages']);
  if (!body || typeof body !== 'object' || Object.keys(body).some(key => !allowed.has(key))) throw new HttpError(422, 'Invalid request schema');
  if (typeof body.requestId !== 'string' || !/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(body.requestId)) throw new HttpError(422, 'Invalid request schema');
  const request = { taskId: 'TEST-PICK-001', language: 'vi', eventType: 'user_question', observation: {}, ...body };
  if (request.sessionId !== undefined && request.sessionId !== null && !/^[0-9a-f-]{36}$/i.test(request.sessionId)) throw new HttpError(422, 'Invalid request schema');
  if (!/^TEST-[A-Z]+-\d{3}$/.test(request.taskId) || !['en', 'vi', 'zh'].includes(request.language) || !['user_question', 'scan_success', 'scan_failure', 'shelf_mismatch', 'route_deviation', 'task_completion', 'summary_request'].includes(request.eventType)) throw new HttpError(422, 'Invalid request schema');
  if (request.locale !== undefined && request.locale !== null && !/^(en|vi|zh)(-[A-Za-z0-9]{2,8}){0,2}$/.test(request.locale)) throw new HttpError(422, 'Invalid request schema');
  if (request.locale && request.locale.split('-')[0] !== request.language) throw new HttpError(422, 'locale must match language');
  if (typeof request.question !== 'string' || request.question.trim().length < 1 || request.question.length > 2000) throw new HttpError(422, 'Invalid request schema');
  request.question = request.question.trim();
  if (typeof request.observation !== 'object' || request.observation === null) throw new HttpError(422, 'Invalid request schema');
  const observationKeys = new Set(['reportedVisibleQuantity', 'scanCode', 'scanFailureCount']);
  if (Object.keys(request.observation).some(key => !observationKeys.has(key))) throw new HttpError(422, 'Invalid request schema');
  if (request.observation.reportedVisibleQuantity !== undefined && (!Number.isInteger(request.observation.reportedVisibleQuantity) || request.observation.reportedVisibleQuantity < 0 || request.observation.reportedVisibleQuantity > 100000)) throw new HttpError(422, 'Invalid request schema');
  if (request.observation.scanFailureCount !== undefined && (!Number.isInteger(request.observation.scanFailureCount) || request.observation.scanFailureCount < 0 || request.observation.scanFailureCount > 1000)) throw new HttpError(422, 'Invalid request schema');
  if (request.observation.scanCode !== undefined && (typeof request.observation.scanCode !== 'string' || request.observation.scanCode.length > 100)) throw new HttpError(422, 'Invalid request schema');
  if (request.occurredAtUtc !== undefined && request.occurredAtUtc !== null && (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?Z$/.test(request.occurredAtUtc) || Number.isNaN(Date.parse(request.occurredAtUtc)))) throw new HttpError(422, 'Invalid request schema');
  if (request.messages !== undefined) {
    if (!Array.isArray(request.messages) || request.messages.length > 20) throw new HttpError(422, 'Invalid request schema');
    request.messages = request.messages.map(message => {
      if (!message || typeof message !== 'object' || Object.keys(message).some(key => !['role', 'content'].includes(key)) || !['user', 'assistant'].includes(message.role) || typeof message.content !== 'string' || message.content.length < 1 || message.content.length > 8000) throw new HttpError(422, 'Invalid request schema');
      return { role: message.role, content: message.content.trim() };
    });
  }
  return request;
}

function validateAnswer(answer, chunks, { hasHistory = false } = {}) {
  if (!answer || typeof answer.message !== 'string' || answer.message.length < 1 || answer.message.length > 8000 || !Array.isArray(answer.sources) || answer.sources.length > 4 || !answer.action) throw new HttpError(502, 'Provider returned incomplete, refused or invalid structured feedback.');
  const valid = new Set(chunks.map(chunk => `${chunk.documentId}\u0000${chunk.sectionId}`));
  const cited = new Set(answer.sources.map(source => `${source.documentId}\u0000${source.sectionId}`));
  // Empty sources are valid for general knowledge and follow-up clarification.
  // A 502 is reserved for a citation that invents a source not retrieved by RAG.
  if ([...cited].some(item => !valid.has(item))) throw new HttpError(502, 'Feedback source validation failed');
  const actionKeys = Object.keys(answer.action).sort();
  if (actionKeys.length !== 3 || actionKeys[0] !== 'exceptionCode' || actionKeys[1] !== 'targetId' || actionKeys[2] !== 'type') throw new HttpError(502, 'Provider returned an invalid action shape');
  if (!ACTION_TYPES.includes(answer.action.type)) throw new HttpError(502, 'Provider returned an unsupported action type');
  if (!EXCEPTION_CODES.includes(answer.action.exceptionCode)) throw new HttpError(502, 'Provider returned an unsupported exception code');
  if (answer.action.targetId !== null && typeof answer.action.targetId !== 'string') throw new HttpError(502, 'Provider returned an invalid action target');
  const codes = new Set(chunks.filter(chunk => cited.has(`${chunk.documentId}\u0000${chunk.sectionId}`)).map(chunk => chunk.exceptionCode));
  if (answer.action.type === 'open_exception_report' && (!answer.action.exceptionCode || !codes.has(answer.action.exceptionCode))) throw new HttpError(502, 'Exception proposal does not match cited SOP');
  if (answer.action.type !== 'open_exception_report') answer.action.exceptionCode = null;
  if (!['hint', 'explanation', 'warning', 'summary'].includes(answer.feedbackType) || !['normal', 'high'].includes(answer.priority) || !Number.isInteger(answer.cooldownSeconds) || answer.cooldownSeconds < 0 || answer.cooldownSeconds > 60) throw new HttpError(502, 'Provider returned incomplete, refused or invalid structured feedback.');
  return answer;
}

function buildSystemPrompt(profile, request, localTime, context, chunks) {
  const citationRule = chunks.length
    ? 'For warehouse operational guidance, cite only matching retrieved SOPs with their documentId and sectionId. If answering a follow-up related to a retrieved SOP, include that citation when it supports the answer; if no new citation is needed, you may return sources = [] and action = {"type":"none","exceptionCode":null,"targetId":null}.'
    : 'If the question is a general definition (for example bonded warehouse, fulfillment, or logistics) and there are no related SOPs, you are allowed to use the model\'s foundational general knowledge to explain it in detail. In this case, you must return sources = [] and action.type = "none" with exceptionCode = null and targetId = null. Do not present it as an approved site rule.';
  return `You are JarVis, a highly intelligent, empathetic, and polite AI warehouse assistant. You analyze situations logically based on standard warehouse operations. The current local time for the user is: ${localTime} (${profile.timeZone}).\n\nCore rules:\n1. Greetings and empathy: if the user greets you or says they are new or confused, greet them warmly based on the current time and reassure them before answering.\n2. Analytical answering: never return static definitions unless asked. For a situation, analyze risks and give safe, step-by-step guidance according to standard warehouse SOPs. Do not claim an operation was completed.\n3. Language: ${profile.instruction} Do not switch languages except for identifiers or source titles.\n4. Evidence and citations: use approved retrieved SOPs for operational guidance. ${citationRule}\n5. General knowledge must never be presented as live inventory, approved route, site policy, or completed transaction.\n\nUse the retrieved project SOP and trusted server facts. The request context is ${context}. Return only the requested structured JSON.`;
}

async function providerAnswer(settings, request, chunks, history, facts, fetchImpl = fetch) {
  const profile = LANGUAGE_PROFILES[request.language] || LANGUAGE_PROFILES.en;
  const schema = {
    type: 'object',
    additionalProperties: false,
    properties: {
      message: { type: 'string', minLength: 1, maxLength: 8000 },
      sources: {
        type: 'array',
        maxItems: 4,
        items: {
          type: 'object',
          properties: {
            documentId: { type: 'string' },
            sectionId: { type: 'string' }
          },
          required: ['documentId', 'sectionId'],
          additionalProperties: false
        }
      },
      action: {
        type: 'object',
        properties: {
          type: { type: 'string', enum: ACTION_TYPES },
          exceptionCode: { type: ['string', 'null'], enum: EXCEPTION_CODES },
          targetId: { type: ['string', 'null'] }
        },
        required: ['type', 'exceptionCode', 'targetId'],
        additionalProperties: false
      },
      needsClarification: { type: 'boolean' },
      clarifyingQuestion: { type: ['string', 'null'] },
      feedbackType: { type: 'string', enum: ['hint', 'explanation', 'warning', 'summary'] },
      priority: { type: 'string', enum: ['normal', 'high'] },
      cooldownSeconds: { type: 'integer', minimum: 0, maximum: 60 }
    },
    required: ['message', 'sources', 'action', 'needsClarification', 'clarifyingQuestion', 'feedbackType', 'priority', 'cooldownSeconds']
  };
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), settings.timeout * 1000);
  try {
    const localTime = new Intl.DateTimeFormat(profile.locale, { timeZone: profile.timeZone, dateStyle: 'full', timeStyle: 'long' }).format(new Date());
    const conversation = Array.isArray(history) ? history.map(message => ({ role: message.role, content: String(message.content) })).filter(message => ['user', 'assistant'].includes(message.role) && message.content) : [];
    const context = JSON.stringify({ locale: responseLocale(request), request: { ...request, messages: undefined }, recentConversation: conversation, trustedFacts: facts, retrievedDocuments: chunks });
    const instructions = buildSystemPrompt(profile, request, localTime, context, chunks);
    const response = await fetchImpl('https://api.openai.com/v1/responses', { method: 'POST', signal: controller.signal, headers: { Authorization: `Bearer ${settings.apiKey}`, 'Content-Type': 'application/json' }, body: JSON.stringify({ model: settings.model, store: false, max_output_tokens: settings.maxTokens, instructions, input: [...conversation, { role: 'user', content: request.question }], text: { format: { type: 'json_schema', name: 'warehouse_feedback', strict: true, schema } } }) });
    if ([401, 403].includes(response.status)) throw new HttpError(502, 'Provider authentication/access failed. Check server key and project model access.');
    if (response.status === 429) throw new HttpError(503, 'Provider quota/rate limit reached. Check server project budget; retry later.');
    if (!response.ok) throw new HttpError(502, 'Provider request failed. Check OPENAI_MODEL supports Responses and structured outputs.');
    const data = await response.json();
    if (data.status !== 'completed') throw new Error('Incomplete response');
    const text = (data.output || []).filter(item => item.type === 'message').flatMap(item => item.content || []).filter(part => part.type === 'output_text').map(part => part.text).join('');
    return JSON.parse(text);
  } catch (error) {
    if (error instanceof HttpError) throw error;
    if (error.name === 'AbortError') throw new HttpError(504, 'Provider timed out; no warehouse operation occurred.');
    throw new HttpError(502, 'Provider connection failed or returned incomplete, refused or invalid structured feedback.');
  } finally { clearTimeout(timer); }
}

function warehouseFacts(user, taskId) {
  const facts = { synthetic: true, taskId, role: user.role, inventoryTransactions: [], approvedRoutes: [], approvedTargets: [], liveInventoryAvailable: false };
  if (taskId === 'TEST-PUT-001') Object.assign(facts, { itemId: 'SKU-2048', assignedLocation: 'B-12-03', locationAvailable: false, approvedAlternative: null });
  return facts;
}

function createApp() {
  const settings = settingsFromEnv();
  const store = new Store(settings.database);
  const knowledge = new Knowledge();
  const app = express();
  app.locals.store = store;
  app.locals.settings = settings;
  app.locals.liveVerified = false;
  app.locals.bootstrapPromise = store.ready.then(() => settings.prototypeTokenSha256 ? store.bootstrapDemo(settings.prototypeTokenSha256) : undefined);
  app.use(cors(corsOptions(settings.corsAllowedOrigins)));
  app.use(bodyParser.json({ limit: '16kb', verify: (req, res, buffer) => { if (buffer.length > 16384) throw new HttpError(413, 'Request body exceeds 16 KiB'); } }));
  app.use((req, res, next) => { req.traceId = crypto.randomUUID(); req.startedAt = Date.now(); res.set('X-Request-ID', req.traceId).set('Cache-Control', 'no-store').set('X-Content-Type-Options', 'nosniff').set('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'"); res.on('finish', () => console.log(`request=${req.traceId} status=${res.statusCode} latency_ms=${Date.now() - req.startedAt}`)); next(); });
  app.use((req, res, next) => app.locals.bootstrapPromise.then(() => next()).catch(next));
  const auth = async (req) => { const header = req.get('Authorization') || ''; if (!header.startsWith('Bearer ') || header.length > 512) throw new HttpError(401, 'Bearer prototype user token required'); return store.authenticate(header.slice(7)); };
  app.get('/api/health', async (req, res, next) => { try { await store.get('SELECT 1'); res.json({ status: 'ok', mode: settings.mode, milestone: 'A', synthetic: true, liveApiVerified: app.locals.liveVerified }); } catch (error) { next(error); } });
  app.get('/api/me', async (req, res, next) => { try { res.json(await auth(req)); } catch (error) { next(error); } });
  app.get('/api/knowledge/:documentId/:sectionId', async (req, res, next) => { try { await auth(req); const source = knowledge.chunks.find(chunk => chunk.documentId === req.params.documentId && chunk.sectionId === req.params.sectionId); if (!source) throw new HttpError(404, 'Knowledge source not found'); res.json(source); } catch (error) { next(error); } });
  app.post('/api/feedback', async (req, res, next) => {
    let session;
    try {
      const user = await auth(req); req.authenticatedUserId = user.id; const request = validateRequest(req.body); const begun = await store.begin(user, request, settings); if (begun.cached) return res.json(begun.cached); session = begun.session;
      const facts = warehouseFacts(user, request.taskId); let chunks = knowledge.retrieve(request.question, session.topic); if (request.eventType === 'summary_request' && !chunks.length && session.topic) chunks = knowledge.retrieve('summary of that', session.topic);
      const conversation = request.messages && request.messages.length ? request.messages : session.history;
      let answer;
      const modelChunks = knowledge.forModel(request.question, session.topic, request.language);
      answer = await providerAnswer(settings, request, modelChunks, conversation, facts);
      answer = validateAnswer(answer, modelChunks, { hasHistory: conversation.length > 0 });
      app.locals.liveVerified = true;
      chunks = modelChunks;
      const result = { ...answer, sessionId: session.id, requestId: request.requestId, language: request.language, locale: responseLocale(request), mode: settings.mode, synthetic: true, operationalMutation: false, latencyMs: Date.now() - req.startedAt, contextTurns: conversation.length / 2, contextWindowTurns: 10, actionExecution: 'proposal_only', sourceDetails: chunks.filter(chunk => answer.sources.some(source => source.documentId === chunk.documentId && source.sectionId === chunk.sectionId)) };
      await store.finish(user, request, session, begun.fingerprint, result, answer.sources[0] ? answer.sources[0].documentId : session.topic);
      res.json(result);
    } catch (error) { next(error); } finally { if (session) { try { const userId = req.authenticatedUserId; if (userId) await store.release(session.id, userId, req.body && req.body.requestId); } catch (_) {} } }
  });
  app.get('/', (req, res) => {
    res.sendFile(path.join(__dirname, 'static', 'index.html'));
  });
  app.use('/static', express.static(path.join(__dirname, 'static')));
  app.use((error, req, res, next) => { if (res.headersSent) return next(error); const status = error instanceof HttpError ? error.status : (error.type === 'entity.too.large' || error.status === 413 ? 413 : error instanceof SyntaxError ? 422 : 500); const detail = error instanceof HttpError ? error.message : (status === 500 ? 'Internal service error; inspect configuration' : 'Invalid request schema'); res.status(status).json({ detail, ...(status === 500 ? { traceId: req.traceId } : {}) }); });
  app.use((req, res) => res.status(404).json({ detail: 'Not found' }));
  app.use((req, res, next) => next());
  return app;
}

async function start() {
  const app = createApp();
  await app.locals.bootstrapPromise;
  const port = Number(process.env.PORT || 8000);
  http.createServer(app).listen(port, '0.0.0.0', () => console.log(`Warehouse AI listening on 0.0.0.0:${port}`));
}

if (require.main === module) start().catch(error => {
  console.error(`Startup failed: ${error.message}`);
  process.exit(1);
});

module.exports = { ACTION_TYPES, EXCEPTION_CODES, createApp, settingsFromEnv, validateRequest, validateAnswer, providerAnswer, buildSystemPrompt, Store, Knowledge, parseCorsOrigins, corsOptions };
