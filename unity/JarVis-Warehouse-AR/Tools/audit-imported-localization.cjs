// Read the saved import, not Figma or user input. Never rewrite source scene data.
const fs = require('node:fs');
const scene = fs.readFileSync('Assets/Scenes/JarVisFigmaApp.unity', 'utf8');
if (process.argv.includes('--login')) {
  const blocks = [...scene.matchAll(/^--- !u!\d+ &(\d+)\r?\n([\s\S]*?)(?=^--- !u!|$(?![\s\S]))/gm)];
  const objects = new Map(), transforms = new Map();
  for (const [,id,b] of blocks) {
    if (b.startsWith('GameObject:')) objects.set(id, {name:/  m_Name: (.*)/.exec(b)?.[1]});
    if (b.startsWith('RectTransform:')) transforms.set(id,{go:/  m_GameObject: \{fileID: (\d+)/.exec(b)?.[1],parent:/  m_Father: \{fileID: (\d+)/.exec(b)?.[1]});
  }
  for (const [,id,b] of blocks) if (b.includes('  m_text:')) {
    const go=/  m_GameObject: \{fileID: (\d+)/.exec(b)?.[1];
    let t=[...transforms.values()].find(t=>t.go===go), path=objects.get(go)?.name;
    while(t&&t.parent!=='0'){t=transforms.get(t.parent);path=objects.get(t?.go)?.name+'/'+path;}
    if(path.includes('login-screen'))console.log(path+' = '+/  m_text: (.*)/.exec(b)?.[1]);
  }
  process.exit(0);
}
const strings = [...scene.matchAll(/^  m_text: (.*(?:\r?\n    [^\r\n]*)*)/gm)].map(m => {
  let value = m[1].replace(/\r?\n    /g, ' ');
  if (value.startsWith('"')) {
    value = value.replace(/\\x([0-9a-f]{2})/gi, '\\u00$1').replace(/\\U([0-9a-f]{8})/gi, (_, n) => String.fromCodePoint(parseInt(n, 16)));
    try { return JSON.parse(value); } catch { return value; }
  }
  return value;
});
const unique = [...new Set(strings)].filter(x => /[a-zA-ZÀ-ỹ]/.test(x)).sort();
if (process.argv.includes('--list')) console.log(JSON.stringify(unique, null, 2));
else {
  const table = JSON.parse(fs.readFileSync('Assets/Resources/JarVisImportedTranslations.json', 'utf8')).entries;
  const known = new Set(table.flatMap(x => [x.vi, x.en]));
  const unknown = unique.filter(x => !known.has(x));
  console.log(JSON.stringify({ uniqueLabels: unique.length, entries: table.length, untranslated: unknown }, null, 2));
  process.exitCode = unknown.length ? 1 : 0;
}
