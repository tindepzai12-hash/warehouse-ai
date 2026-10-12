const fs=require('node:fs');
function edit(path,changes){let s=fs.readFileSync(path,'utf8');for(const [a,b]of changes){if(!s.includes(a))throw Error('Missing expected text: '+a);s=s.replace(a,b);}fs.writeFileSync(path,s);}
edit('Assets/Resources/JarVisShaders/UniformSurface.shader',[
 ['_Glass("Glass",Float)=0','_FillEnd("Gradient end",Color)=(1,1,1,1) _Gradient("Gradient",Float)=0 _Glass("Glass",Float)=0'],
 ['float4 _Fill,_Stroke,_Size,_ClipRect,_RectCenter;float _Glass;','float4 _Fill,_FillEnd,_Stroke,_Size,_ClipRect,_RectCenter;float _Glass,_Gradient;'],
 ['float4 c=lerp(_Fill,_Stroke,edge);','float4 baseFill=lerp(_Fill,_FillEnd,_Gradient*saturate((i.uv.x+1-i.uv.y)*.5));float4 c=lerp(baseFill,_Stroke,edge);'],
 ['saturate(.6-i.uv.y)*edge','saturate(i.uv.y-.4)*edge'],
 ['pow(saturate(1-i.uv.y),5)*.09;c.a=max(c.a,.88);','pow(saturate(i.uv.y),5)*.09;']
]);
edit('Assets/JarVis/Scripts/UI/UniformUiSurface.cs',[
 [' public bool Glass=>requestedGlass&&!PrototypeUi.ReducedGlass;',' public bool Glass=>requestedGlass&&!PrototypeUi.ReducedGlass;\n public static void Gradient(Image image,Color end){var surface=image.GetComponent<UniformUiSurface>();if(surface?.material==null)return;surface.material.SetColor("_FillEnd",end);surface.material.SetFloat("_Gradient",1);image.SetMaterialDirty();}'],
 ['c.requestedGlass=glass;','c.material.SetFloat("_Gradient",0);c.requestedGlass=glass;']
]);
edit('Assets/JarVis/Scripts/UI/ImportedHomeDashboard.cs',[
 ['dark?"213249":"eef7ff",dark?"3a516e":"d3e6ff"','dark?"213249":"eef6ff",dark?"3a516e":"d5e6fc"'],
 ['PrototypeUi.Flow(streak,226+extra);','PrototypeUi.Flow(streak,226+extra);if(!dark)UniformUiSurface.Gradient(streak.GetComponent<UnityEngine.UI.Image>(),PrototypeUi.Hex("f5f9ff"));'],
 ['dark?"526580":fill,true','dark?"526580":"ffffffcc",true'],
 ['var tone=','var tone=']
].slice(0,3));
edit('Assets/JarVis/Scripts/UI/ImportedPresence.cs',[
 ['new Color(.945f,.984f,.969f),new Color(.77f,.91f,.855f),1)','PrototypeUi.Hex(nav.Settings.Dark?"192f2b":"f1fbf7"),PrototypeUi.Hex(nav.Settings.Dark?"385b4c":"c4e8da"),1,false,false)'],
 ['UniformUiSurface.Use(bg,12,new Color(.945f,.965f,.985f),new Color(.83f,.9f,.96f),1,true,false);','UniformUiSurface.Use(bg,14,PrototypeUi.Hex(nav.Settings.Dark?"30435a":"dfe8f0d1"),PrototypeUi.Hex(nav.Settings.Dark?"66809f":"ffffffcc"),1,true,false);PrototypeGlassShadow.Attach(bg,14);'],
 ['c.action.color=PrototypeUi.Ink(dark);','c.action.color=PrototypeUi.Ink(dark);c.action.fontStyle=FontStyles.Bold;'],
 ['c.state.fontSize=19*scale;','c.state.fontSize=20*scale;'],
 ['c.state.fontSizeMax=19*scale;','c.state.fontSizeMax=20*scale;'],
 ['c.action.fontSize=12*scale;','c.action.fontSize=11*scale;'],
 ['c.action.fontSizeMax=12*scale;','c.action.fontSizeMax=11*scale;']
]);
