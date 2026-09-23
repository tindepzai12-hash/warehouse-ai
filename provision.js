const fs = require('fs');
const { Store, settingsFromEnv } = require('./server');

async function main() {
  const args = process.argv.slice(2);
  if (args[0] !== 'create-user') throw new Error('Usage: npm run create-user -- create-user --user worker-demo [--role staff] [--token-file .local-token]');
  const value = name => { const index = args.indexOf(name); return index >= 0 ? args[index + 1] : undefined; };
  const user = value('--user');
  if (!user) throw new Error('--user is required');
  const role = value('--role') || 'staff';
  if (!['staff', 'icqa', 'supervisor'].includes(role)) throw new Error('--role must be staff, icqa or supervisor');
  const tokenFile = value('--token-file') || '.local-token';
  const token = await new Store(settingsFromEnv().database).provision(user, role);
  fs.writeFileSync(tokenFile, token, 'utf8');
  console.log(`Prototype token written to ${tokenFile}. Keep it private. Existing token for this user is revoked.`);
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });