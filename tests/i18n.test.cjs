const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function loadI18n() {
  const window = { dispatchEvent() {} };
  const document = { querySelectorAll: () => [], documentElement: {}, getElementById: () => null, title: '' };
  const localStorage = { getItem: () => '', setItem() {} };
  vm.runInNewContext(fs.readFileSync(path.join(__dirname, '../static/i18n.js'), 'utf8'), { window, document, localStorage, CustomEvent: class {} });
  return window.JarvisI18n;
}

test('EN, ZH and VI dictionaries expose the same complete key set', () => {
  const i18n = loadI18n();
  const expected = Object.keys(i18n.dictionaries.vi).sort();
  assert.deepEqual(Object.keys(i18n.dictionaries.en).sort(), expected);
  assert.deepEqual(Object.keys(i18n.dictionaries.zh).sort(), expected);
  assert.ok(expected.length > 90);
});

test('language state changes translations and interpolates dynamic values', () => {
  const i18n = loadI18n();
  i18n.setLanguage('en', false);
  assert.equal(i18n.t('newSession'), 'New session');
  assert.equal(i18n.t('sessionPrefix', { id: 'abc' }), 'Session: abc');
  i18n.setLanguage('zh', false);
  assert.equal(i18n.t('newSession'), '新会话');
  assert.equal(i18n.defaults.zh, 'zh-TW');
  assert.match(i18n.t('localePlaceholder'), /zh-TW/);
});
