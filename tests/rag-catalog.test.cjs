const { test } = require('node:test');
const assert = require('node:assert/strict');
const { Knowledge } = require('../server');

test('local SOP catalog sends only runtime-approved official releases into RAG', () => {
  const knowledge = new Knowledge();

  assert.ok(knowledge.chunks.some(chunk => chunk.documentId === 'SOP_INB_001'));
  assert.ok(knowledge.chunks.some(chunk => chunk.documentId === 'SOP_OUT_002'));
  assert.ok(knowledge.chunks.some(chunk => chunk.documentId === 'SOP_INV_003'));
  assert.ok(knowledge.chunks.some(chunk => chunk.documentId === 'SOP_MNG_001'));
  assert.deepEqual(
    knowledge.unverifiedDocuments.map(document => document.documentId),
    ['SOP_EXC_001', 'SOP_EXC_002', 'SOP_EXC_003', 'SOP_EXC_004']
  );
  assert.equal(knowledge.chunks.some(chunk => chunk.documentId === 'SOP_EXC_003'), false);
});

test('basic RAG selects matching local SOPs rather than sending the whole corpus', () => {
  const knowledge = new Knowledge();
  const receiving = knowledge.forModel('Tôi nhận hàng ở cửa Dock như thế nào?', '', 'vi');
  const count = knowledge.forModel('Tôi kiểm kê mà số lượng bị chênh lệch thì sao?', '', 'vi');

  assert.deepEqual(receiving.map(chunk => chunk.documentId), ['SOP_INB_001']);
  assert.deepEqual(count.map(chunk => chunk.documentId), ['SOP_INV_003']);
  assert.equal(receiving[0].text.includes('Tiếp nhận thông báo giao hàng'), true);
});
