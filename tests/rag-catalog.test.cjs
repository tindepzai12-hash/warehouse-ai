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

test('FMCG SOPs are schema-valid and retrievable in English and Vietnamese', () => {
  const knowledge = new Knowledge();
  const expected = [
    'SOP_FC_PUT_002_EN',
    'SOP_FC_EXP_002_EN',
    'SOP_FC_PAK_002_EN',
    'SOP_FMCG_PK_01_EN',
    'SOP_FMCG_INV_01_EN'
  ];

  for (const documentId of expected) {
    assert.equal(knowledge.chunks.some(chunk => chunk.documentId === documentId), true, `${documentId} must be available to runtime RAG`);
  }

  const fefo = knowledge.forModel('How should FEFO be applied to milk and instant noodles?', '', 'en');
  const contamination = knowledge.forModel('Cách chống nhiễm chéo giữa dầu gội và mì gói?', '', 'vi');

  assert.equal(fefo.some(chunk => chunk.documentId === 'SOP_FMCG_INV_01_EN'), true);
  assert.equal(contamination.some(chunk => chunk.documentId === 'SOP_FMCG_PK_01_EN'), true);
});
