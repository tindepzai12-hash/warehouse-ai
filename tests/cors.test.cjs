const { test } = require('node:test');
const assert = require('node:assert/strict');
const { corsOptions, parseCorsOrigins } = require('../server');

test('CORS accepts only configured cross-origin clients and keeps same-origin requests working', () => {
  const options = corsOptions(parseCorsOrigins('https://warehouse.example, http://localhost:5173/'));

  let allowed;
  options.origin('https://warehouse.example', (error, result) => { assert.equal(error, null); allowed = result; });
  assert.equal(allowed, true);

  let sameOrigin;
  options.origin(undefined, (error, result) => { assert.equal(error, null); sameOrigin = result; });
  assert.equal(sameOrigin, true);

  let denied;
  options.origin('https://untrusted.example', (error, result) => { denied = { error, result }; });
  assert.equal(denied.result, undefined);
  assert.equal(denied.error.status, 403);
});

test('CORS configuration rejects paths and non-web origins', () => {
  assert.throws(() => parseCorsOrigins('https://warehouse.example/app'), /origins without paths/);
  assert.throws(() => parseCorsOrigins('file:///C:/warehouse'), /comma-separated http\(s\) origins/);
});
