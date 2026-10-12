const fs=require('node:fs');
const decode=s=>s.replace(/\\n/g,'\n');
const entries=fs.readFileSync('Tools/imported-translations.tsv','utf8').trimEnd().split(/\r?\n/).map((row,index)=>{
  const at=row.indexOf('|');if(at<0)throw new Error('Missing separator at '+(index+1));
  return {vi:decode(row.slice(0,at)),en:decode(row.slice(at+1))};
});
if(new Set(entries.map(x=>x.vi)).size!==entries.length)throw new Error('Duplicate Vietnamese label');
fs.writeFileSync('Assets/Resources/JarVisImportedTranslations.json',JSON.stringify({entries},null,2)+'\n');
const escape=s=>s.replace(/\n/g,'<br>').replace(/\|/g,'\\|');
fs.mkdirSync('Docs',{recursive:true});
fs.writeFileSync('Docs/APP_TRANSLATIONS_VI_EN.md','# JarVis app — Vietnamese / English\n\nSelect VI or EN on Login to change the imported app interface. The choice is saved on the device. UI language does not translate product names, SKU/lot/location codes, employee identities, user messages or AI answers; JarVis answers in the language of the question. Static illustrative Figma labels are translated.\n\nWarehouse terminology: goods receipt = nhập kho; goods dispatch = xuất kho; stocktake = kiểm kê; stock on hand = số lượng tồn; rack / level / bin = kệ / tầng / ô; lot = lô; accepted = đã nhận (different from assigned / đã giao); case = thùng; carton = hộp. Expired refers to goods, overdue refers to a task.\n\nEdit `Tools/imported-translations.tsv`, then run `node Tools/build-imported-translations.cjs`. The JSON and this table are generated from that single source.\n\n| Tiếng Việt | English |\n| --- | --- |\n'+entries.map(x=>'| '+escape(x.vi)+' | '+escape(x.en)+' |').join('\n')+'\n');
console.log('Generated '+entries.length+' translation entries and app translation table.');
