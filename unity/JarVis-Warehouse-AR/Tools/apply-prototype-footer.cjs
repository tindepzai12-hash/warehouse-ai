const fs=require('node:fs');const p='Assets/JarVis/Scripts/UI/ImportedUiPolish.cs';let s=fs.readFileSync(p,'utf8');let a=s.indexOf('        public static void Footer('),b=s.indexOf('        static UnityEngine.UI.Image FooterSurface',a);if(a<0||b<0)throw Error('Footer target missing');
s=s.slice(0,a)+`        public static void Footer(GameObject footer)
        {
            var root=(RectTransform)footer.transform;float width=root.rect.width;
            var tabs=footer.GetComponentsInChildren<FigmaNode>(true).Where(n=>(n.nodeName??"").StartsWith("tab-")).ToArray();var scan=Find(footer,"center-scan-btn");
            foreach(var image in footer.GetComponentsInChildren<UnityEngine.UI.Image>(true))image.enabled=false;
            var pill=FooterSurface("Floating navigation pill",root,17,5,width-34,77,30,Color.white,true);UniformUiSurface.Use(pill,30,new Color(1,1,1,.85f),new Color(.85f,.9f,.96f),1,true);pill.transform.SetAsFirstSibling();
            string[] symbols={"home","tasks","map",""};float[] x={22,97,240,315};
            for(int i=0;i<tabs.Length;i++)
            {
                var tab=tabs[i];tab.transform.SetParent(root,false);Place(tab.rectTransform,x[i],13.5f,75,60);
                var hit=tab.GetComponent<Image>();if(hit!=null){hit.enabled=true;hit.color=new Color(1,1,1,.001f);}
                foreach(var old in tab.GetComponentsInChildren<ImportedUtilityIcon>(true))old.enabled=false;
                var label=tab.GetComponentInChildren<TMP_Text>(true);if(label!=null){Place(label.rectTransform,0,38,75,22);label.alignment=TextAlignmentOptions.Center;label.fontSize=10;Fit(label);}
                if(i==3){var art=new GameObject("Canonical JarVis mascot",typeof(RectTransform),typeof(Image)).GetComponent<Image>();art.transform.SetParent(tab.transform,false);Place(art.rectTransform,23.5f,5,28,28);art.sprite=Resources.Load<Sprite>("Figma/JarVisMascotApproved");BrandArtwork(art);}
                else PrototypeUi.Icon(tab.transform,symbols[i],25.5f,7,24,JarVisDesignTokens.Ink);
            }
            if(scan!=null)
            {
                scan.transform.SetParent(root,false);Place(scan.rectTransform,177,-.5f,58,58);var image=scan.GetComponent<Image>();image.enabled=true;image.color=Color.white;UniformUiSurface.Use(image,29,PrototypeUi.Accent,Color.white,3,false,false);
                foreach(var old in scan.GetComponentsInChildren<ImportedUtilityIcon>(true))old.enabled=false;PrototypeUi.Icon(scan.transform,"scan",16,16,26,Color.white);scan.transform.SetAsLastSibling();
            }
        }
`+s.slice(b);fs.writeFileSync(p,s);
