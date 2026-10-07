const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

const root = path.join(__dirname, '..');
require('dotenv').config({ path: path.join(root, '.env') });
const { Store } = require('../server');
const { OmsStore } = require('../oms/store');

async function main() {
  if (process.env.NODE_ENV === 'test' || process.env.OMS_SEED_DEMO === 'true')
    throw new Error('Disable demo seeding before bootstrapping the live admin');
  const databasePath = process.env.DATABASE_PATH || 'data/warehouse-live.sqlite3';
  const store = new Store(databasePath);
  try {
    const oms = new OmsStore(store);
    await oms.init();
    if (await oms.get('SELECT 1 FROM oms_users WHERE is_admin=1 LIMIT 1')) {
      process.stdout.write('Admin already exists; no credentials changed.\n');
      return;
    }
    const username = 'admin';
    const password = crypto.randomBytes(32).toString('base64url');
    const credentialsPath = path.join(path.dirname(store.path), 'oms-admin-credentials.txt');
    fs.writeFileSync(credentialsPath,
      `JarVis Warehouse administrator\nUsername: ${username}\nPassword: ${password}\nURL: http://localhost:${process.env.PORT || 8000}/warehouse/\n`,
      { flag: 'wx', mode: 0o600 });
    try {
      await oms.createInitialAdmin(username, password);
    } catch (error) {
      fs.rmSync(credentialsPath, { force: true });
      throw error;
    }
    process.stdout.write(`Created the sole admin. Credentials are in ${credentialsPath}\n`);
  } finally {
    await store.ready;
    await new Promise((resolve,reject)=>store.db.close(error=>error?reject(error):resolve()));
  }
}

main().catch(error=>{ process.stderr.write(`${error.message}\n`); process.exitCode=1; });
