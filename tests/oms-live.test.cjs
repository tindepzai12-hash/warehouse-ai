const { test } = require('node:test');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');

test('empty live OMS starts at zero and only its sole admin can mutate warehouse records', async t => {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'jarvis-live-'));
  process.env.DATABASE_PATH = path.join(directory, 'live.sqlite3');
  process.env.OMS_SEED_DEMO = 'false';
  process.env.NODE_ENV = 'live-test';
  process.env.PROTOTYPE_TOKEN_SHA256 = '';
  const { createApp } = require('../server');
  const app = createApp();
  await app.locals.bootstrapPromise;
  const oms = app.locals.omsStore;
  await oms.createInitialAdmin('admin', 'a-long-random-test-password');
  await assert.rejects(oms.createInitialAdmin('admin2', 'another-long-test-password'), /admin account already exists/);
  const server = app.listen(0, '127.0.0.1');
  await new Promise(resolve => server.once('listening', resolve));
  t.after(async () => {
    await new Promise(resolve => server.close(resolve));
    await new Promise(resolve => app.locals.store.db.close(resolve));
    fs.rmSync(directory, { recursive: true, force: true });
  });
  const base = `http://127.0.0.1:${server.address().port}/api/oms`;
  async function request(url, options = {}) {
    const response = await fetch(base + url, options);
    return { status: response.status, body: await response.json(), cookie: response.headers.get('set-cookie') };
  }
  async function login(username, password) {
    const response = await request('/auth/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ username, password }) });
    assert.equal(response.status, 200);
    return { cookie: response.cookie.split(';')[0], csrf: response.body.csrfToken, user: response.body.user, dataMode: response.body.dataMode };
  }
  async function as(user, url, method = 'GET', body) {
    const headers = { cookie: user.cookie };
    if (body !== undefined) { headers['content-type'] = 'application/json'; headers['x-csrf-token'] = user.csrf; }
    return request(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
  }
  const admin = await login('admin', 'a-long-random-test-password');
  assert.equal(admin.user.canEdit, true);
  assert.equal(admin.dataMode, 'live');
  const initial = await as(admin, '/dashboard');
  assert.equal(initial.status, 200);
  assert.deepEqual([initial.body.newOrders, initial.body.activeReturns, initial.body.openTasks, initial.body.availableUnits], [0, 0, 0, 0]);
  assert.equal(initial.body.synthetic, false);
  for (const endpoint of ['/products', '/locations', '/lots', '/orders', '/receiving', '/tasks', '/inventory/search'])
    assert.equal((await as(admin, endpoint)).body.length, 0, endpoint);

  const account = await as(admin, '/accounts', 'POST', { username: 'viewer', displayName: 'Kho Viewer', password: 'long-test-password-123', role: 'MANAGER' });
  assert.equal(account.status, 201);
  assert.equal(account.body.canEdit, false);
  const viewer = await login('viewer', 'long-test-password-123');
  assert.equal(viewer.user.canEdit, false);
  assert.equal((await as(viewer, '/dashboard')).status, 200);
  assert.equal((await as(viewer, '/products', 'POST', { sku: 'SKU-1', name: 'Milk', barcode: '1234567890123', unit: 'box' })).status, 403);
  assert.equal((await as(viewer, '/locations', 'POST', { code: 'A-01', zone: 'A' })).status, 403);
  assert.equal((await as(viewer, '/accounts', 'POST', { username: 'other', displayName: 'Other', password: 'another-long-password', role: 'MANAGER' })).status, 403);
  assert.equal((await as(viewer, '/accounts')).status, 403);
  assert.equal((await as(viewer, '/conversations/missing/messages', 'POST', {content:'Unauthorized'})).status, 403);

  const product = await as(admin, '/products', 'POST', { sku: 'MILK-001', name: 'Sữa UHT', barcode: '8938500000123', unit: 'hộp' });
  const location = await as(admin, '/locations', 'POST', { code: 'A-01', zone: 'A' });
  assert.equal(product.status, 201);
  assert.equal(location.status, 201);
  const lot = await as(admin, '/lots', 'POST', { productId: product.body.id, lotCode: 'LOT-001', receivedAt: '2026-10-03', expiryDate: '2027-03-03' });
  assert.equal(lot.status, 201);
  const receiving = await as(admin, '/receiving', 'POST', { productId: product.body.id, lotId: lot.body.id, locationId: location.body.id, expectedQuantity: 10 });
  assert.equal(receiving.status, 201);
  assert.equal((await as(admin, '/dashboard')).body.availableUnits, 0);
  const confirmed = await as(admin, `/receiving/${receiving.body.id}/confirm`, 'POST', { actualQuantity: 8, requestId: crypto.randomUUID() });
  assert.equal(confirmed.status, 200);
  assert.equal((await as(admin, '/dashboard')).body.availableUnits, 8);
  assert.equal((await as(viewer, '/inventory/search')).body[0].available, 8);

  const worker = await as(admin, '/accounts', 'POST', { username: 'NV-00001', displayName: 'Nhân viên 1', password: 'worker-test-password', role: 'EMPLOYEE', workRole: 'COUNTING', warehouseZone: 'A' });
  assert.equal(worker.status, 201);
  assert.equal((await as(admin, '/employees')).body[0].status, 'OFFLINE');
  const mobile = await request('/mobile/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ employeeCode: 'NV-00001', password: 'worker-test-password' }) });
  assert.equal(mobile.status, 200);
  const mobileInventory = await request('/mobile/inventory', { headers: { authorization: `Bearer ${mobile.body.accessToken}` } });
  assert.equal(mobileInventory.body.synthetic, false);
  assert.equal(mobileInventory.body.items[0].available, 8);
});
