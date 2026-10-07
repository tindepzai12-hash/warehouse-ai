const {test}=require('node:test');const assert=require('node:assert/strict');
const fs=require('node:fs'),os=require('node:os'),path=require('node:path');
const {createApp}=require('../server');const {passwordHash,verifyPassword}=require('../oms/store');
test('explicit cloud hash bootstraps sole admin without demo data and never replaces existing admin',async t=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'jarvis-cloud-')),saved={...process.env};
 Object.assign(process.env,{DATABASE_PATH:path.join(root,'cloud.sqlite'),NODE_ENV:'production',OMS_SEED_DEMO:'false',OPENAI_API_KEY:'',OMS_BOOTSTRAP_ADMIN_HASH:passwordHash('random-test-password-long-enough')});
 const app=createApp();await app.locals.bootstrapPromise;
 t.after(async()=>{await new Promise(r=>app.locals.store.db.close(r));fs.rmSync(root,{recursive:true,force:true});for(const key of Object.keys(process.env))if(!(key in saved))delete process.env[key];Object.assign(process.env,saved);});
 let admin=await app.locals.omsStore.userByUsername('admin');assert.ok(verifyPassword('random-test-password-long-enough',admin.password_hash));
 assert.equal(app.locals.omsStore.synthetic,false);assert.equal((await app.locals.omsStore.inventory('')).length,0);
 await app.locals.omsStore.createInitialAdminHash('admin',passwordHash('different-test-password-long-enough'),true);
 admin=await app.locals.omsStore.userByUsername('admin');assert.ok(verifyPassword('random-test-password-long-enough',admin.password_hash));assert.ok(!verifyPassword('different-test-password-long-enough',admin.password_hash));
 assert.equal((await app.locals.omsStore.all('SELECT id FROM oms_users WHERE is_admin=1')).length,1);
 await assert.rejects(app.locals.omsStore.createInitialAdminHash('admin','not-a-hash',true),/configuration/);
});
