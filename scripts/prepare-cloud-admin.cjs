// Creates a separate research-cloud identity. Never reads or copies the local admin/database.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {passwordHash}=require('../oms/store');
const root=path.resolve(__dirname,'..','data');
fs.mkdirSync(root,{recursive:true});
const file=path.join(root,'oms-cloud-admin-credentials.txt');
if(!fs.existsSync(file)){
  const password=crypto.randomBytes(32).toString('base64url');
  fs.writeFileSync(file,`JarVis research cloud administrator\nUsername: admin\nPassword: ${password}\nURL: https://warehouse-ai-fmjc.onrender.com/warehouse/\nRender Free: warehouse data is ephemeral; this is a separate cloud account.\n`,{flag:'wx',mode:0o600});
}
if(process.argv.includes('--hash')){
  const password=fs.readFileSync(file,'utf8').match(/^Password: ([A-Za-z0-9_-]{43})$/m)?.[1];
  if(!password)throw new Error('Invalid cloud credential file; do not overwrite');
  process.stdout.write(JSON.stringify({hash:passwordHash(password),file}));
}else process.stdout.write(`Cloud credentials stored locally in ${file}\n`);
