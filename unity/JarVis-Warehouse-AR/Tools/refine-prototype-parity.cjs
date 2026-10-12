const fs=require('node:fs');
function edit(path,changes){let s=fs.readFileSync(path,'utf8');for(const [a,b]of changes){if(!s.includes(a))throw Error('Missing expected text: '+a);s=s.replace(a,b);}fs.writeFileSync(path,s);}
edit('Assets/JarVis/Scripts/UI/ImportedPresence.cs',[
 ['card.button.onClick.AddListener(Toggle);','card.button.onClick.AddListener(()=>PrototypeDialog.Presence(nav));'],
 ['c.state.color=ink;','c.state.color=CanWork?PrototypeUi.Hex(dark?"98deb8":"0d684c"):ink;'],
 ['c.action.color=new Color(.75f,.08f,.16f);','c.action.color=PrototypeUi.Ink(dark);']
]);
edit('Assets/JarVis/Scripts/UI/UniformUiSurface.cs',[
 ['bool adaptTheme=true,initialized;','bool adaptTheme=true,initialized,requestedGlass,lastGlass;'],
 ['c.material.SetFloat("_Glass",glass?1:0);','c.requestedGlass=glass;c.material.SetFloat("_Glass",glass&&!PrototypeUi.ReducedGlass?1:0);'],
 ['void Refresh(){if(image==null||material==null)return;','void Refresh(){if(image==null||material==null)return;bool glass=requestedGlass&&!PrototypeUi.ReducedGlass;if(!initialized||glass!=lastGlass){material.SetFloat("_Glass",glass?1:0);lastGlass=glass;image.SetMaterialDirty();}']
]);
