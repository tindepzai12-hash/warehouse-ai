using System.Linq;
using Cdm.Figma.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    public static class ImportedUiPolish
    {
        static readonly string[] symbols={"home","tasks","list-check","map","jarvis","message-square","search","filter","package","alert-circle","alert-triangle","user","lock","chevron-right","chevron-down","arrow-left","arrow-right","menu","plus","minus","x","calendar","scan-barcode","camera","bell"};
        static readonly Color Ink=JarVisDesignTokens.Ink;
        static Material brandMaterial;
        public static void BrandArtwork(UnityEngine.UI.Image image)
        {
            if(brandMaterial==null){var shader=Resources.Load<Shader>("JarVisShaders/BrandArtwork");if(shader!=null)brandMaterial=new Material(shader){name="Approved JarVis matte removal"};}
            if(brandMaterial!=null)image.material=brandMaterial;
            image.color=Color.white;image.preserveAspect=true;image.raycastTarget=false;
        }
        public static void Apply(GameObject root)
        {
            foreach(var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                if(symbols.Contains(node.nodeName)&&node.GetComponent<TMP_Text>()==null&&node.GetComponentsInChildren<FigmaNode>(true).Any(n=>n.nodeName=="Vector"))
                    Icon(node);
                if(node.nodeName=="screen-header")
                {var image=node.GetComponent<Image>();if(image!=null){image.sprite=null;image.color=Color.white;}}
                if(node.nodeName=="header-left")node.rectTransform.sizeDelta=new Vector2(330,node.rectTransform.sizeDelta.y);
            }
            foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if(label.GetComponentInParent<TMP_InputField>()!=null)continue;
                label.characterSpacing=0;label.wordSpacing=0;label.transform.localScale=Vector3.one;
                if(label.rectTransform.rect.height<=48&&!label.text.Contains('\n')){ExpandLabel(label);Fit(label);}
            }
            var id=root.GetComponent<FigmaNode>()?.nodeId;
            if(id=="5:36"||id=="5:11")
            {
                var sprite=Resources.Load<Sprite>("Figma/JarVisMascotApproved");
                if(sprite!=null)
                foreach(var container in root.GetComponentsInChildren<FigmaNode>(true).Where(n=>n.nodeName=="mascot-container"))
                {
                    foreach(var graphic in container.GetComponentsInChildren<Graphic>(true))
                        if(graphic.name!="Approved JarVis logo")graphic.enabled=false;
                    var holder=container.transform.Find("Approved JarVis logo");
                    var art=holder!=null?holder.GetComponent<UnityEngine.UI.Image>():new GameObject("Approved JarVis logo",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
                    art.transform.SetParent(container.transform,false);
                    art.rectTransform.anchorMin=Vector2.zero;art.rectTransform.anchorMax=Vector2.one;
                    art.rectTransform.offsetMin=art.rectTransform.offsetMax=Vector2.zero;
                    art.sprite=sprite;art.color=Color.white;art.preserveAspect=true;art.raycastTarget=false;art.enabled=true;
                    BrandArtwork(art);
                }
            }
            if(id=="5:780"||id=="182:2")Inventory(root);
            if(id=="5:1308")Settings(root);
            foreach(var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                if((node.nodeName??"").StartsWith("step-")&&int.TryParse(node.nodeName.Substring(5),out int index))
                {
                    Place(node.rectTransform,16+index*95,0,95,24);
                    foreach(var line in node.GetComponentsInChildren<FigmaNode>(true).Where(n=>n.nodeName=="Line"))line.gameObject.SetActive(false);
                    var text=node.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.transform.parent==node.transform);
                    if(text!=null){Place(text.rectTransform,28,0,65,24);text.fontSize=11;Fit(text);}
                }
                if(node.nodeName=="text-status")node.rectTransform.sizeDelta=new Vector2(220,node.rectTransform.sizeDelta.y);
                if(node.nodeName=="suggest-chip"||node.nodeName=="btn-action-fill"||node.nodeName=="btn-action")
                {var text=node.GetComponentInChildren<TMP_Text>(true);if(text!=null){Place(text.rectTransform,6,0,node.rectTransform.rect.width-12,node.rectTransform.rect.height);text.alignment=TextAlignmentOptions.Center;Fit(text);if(node.nodeName=="suggest-chip")text.textWrappingMode=TextWrappingModes.Normal;}}
                if(node.nodeName=="legend-item")
                {var text=node.GetComponentInChildren<TMP_Text>(true);if(text!=null){Place(text.rectTransform,18,0,82,18);text.fontSize=11;Fit(text);}}
            }
            foreach(var status in root.GetComponentsInChildren<FigmaNode>(true).Where(n=>n.nodeName=="text-status"))
            foreach(var text in status.GetComponentsInChildren<TMP_Text>(true))
            {text.rectTransform.sizeDelta=new Vector2(220,text.rectTransform.sizeDelta.y);Fit(text);}
            if(id=="5:36")
            {
                var pill=Find(root,"lang-pill");if(pill!=null){pill.rectTransform.anchorMin=pill.rectTransform.anchorMax=pill.rectTransform.pivot=new Vector2(1,1);pill.rectTransform.anchoredPosition=new Vector2(-16,-56);}
            }
        }
        static FigmaNode Find(GameObject root,string name)=>root.GetComponentsInChildren<FigmaNode>(true).FirstOrDefault(n=>n.nodeName==name);
        public static void Fit(TMP_Text label)
        {
            float size=label.fontSize;label.enableAutoSizing=true;label.fontSizeMax=size;label.fontSizeMin=Mathf.Min(size,Mathf.Max(11,size*.78f));
            label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;label.margin=Vector4.zero;
        }
        static void ExpandLabel(TMP_Text text)
        {
            var rect=text.rectTransform;var parent=rect.parent as RectTransform;
            if(parent==null||parent.rect.width<=rect.rect.width||rect.anchorMin!=rect.anchorMax)return;
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var left=parent.InverseTransformPoint(corners[0]);var right=parent.InverseTransformPoint(corners[2]);
            float start=left.x,end=parent.rect.xMax-4;
            foreach(RectTransform sibling in parent)
            {
                if(sibling==rect||!sibling.gameObject.activeSelf)continue;sibling.GetWorldCorners(corners);
                var a=parent.InverseTransformPoint(corners[0]);var b=parent.InverseTransformPoint(corners[2]);
                if(b.y<=left.y+.5f||a.y>=right.y-.5f)continue;
                if(a.x>=right.x-1)end=Mathf.Min(end,a.x-8);
            }
            float available=end-start;
            if(available>rect.rect.width+1){float delta=available-rect.rect.width;rect.sizeDelta=new Vector2(available,rect.sizeDelta.y);rect.anchoredPosition+=new Vector2(delta*rect.pivot.x,0);}
        }
        static void Place(RectTransform rect,float x,float y,float w,float h)
        {rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);rect.localScale=Vector3.one;}
        static void Icon(FigmaNode node)
        {
            foreach(var image in node.GetComponentsInChildren<Image>(true))if(image.gameObject!=node.gameObject)image.enabled=false;
            var holder=node.transform.Find("Sharp interface icon");if(holder!=null)return;
            var icon=new GameObject("Sharp interface icon",typeof(RectTransform),typeof(ImportedUtilityIcon)).GetComponent<ImportedUtilityIcon>();icon.transform.SetParent(node.transform,false);
            icon.rectTransform.anchorMin=Vector2.zero;icon.rectTransform.anchorMax=Vector2.one;icon.rectTransform.offsetMin=icon.rectTransform.offsetMax=Vector2.zero;
            icon.symbol=node.nodeName;icon.color=node.GetComponentsInParent<FigmaNode>(true).Any(n=>n.nodeName=="center-scan-btn")?Color.white:Ink;icon.raycastTarget=false;
        }
        public static void Footer(GameObject footer)
        {
            var root=(RectTransform)footer.transform;float width=root.rect.width;
            var tabs=footer.GetComponentsInChildren<FigmaNode>(true).Where(n=>(n.nodeName??"").StartsWith("tab-")).ToArray();var scan=Find(footer,"center-scan-btn");
            foreach(var image in footer.GetComponentsInChildren<UnityEngine.UI.Image>(true))image.enabled=false;
            var pill=FooterSurface("Floating navigation pill",root,17,5,width-34,77,30,Color.white,true);UniformUiSurface.Use(pill,30,new Color(1,1,1,.85f),new Color(.85f,.9f,.96f),1,true);pill.transform.SetAsFirstSibling();
            PrototypeGlassShadow.Attach(pill,30);
            string[] symbols={"home","tasks","map",""};float[] x={22,97,240,315};
            for(int i=0;i<tabs.Length;i++)
            {
                var tab=tabs[i];tab.transform.SetParent(root,false);Place(tab.rectTransform,x[i],13.5f,75,60);
                var hit=tab.GetComponent<Image>();if(hit!=null){hit.enabled=true;hit.color=new Color(1,1,1,.001f);}
                foreach(var old in tab.GetComponentsInChildren<ImportedUtilityIcon>(true))old.enabled=false;
                var label=tab.GetComponentInChildren<TMP_Text>(true);if(label!=null){Place(label.rectTransform,0,38,75,22);label.alignment=TextAlignmentOptions.Center;label.fontSize=11;label.fontStyle=FontStyles.Bold;Fit(label);}
                if(i==3){var art=new GameObject("Canonical JarVis mascot",typeof(RectTransform),typeof(Image)).GetComponent<Image>();art.transform.SetParent(tab.transform,false);Place(art.rectTransform,23.5f,5,28,28);art.sprite=Resources.Load<Sprite>("Figma/JarVisMascotApproved");BrandArtwork(art);}
                else {var icon=new GameObject("Bold dock "+symbols[i],typeof(RectTransform),typeof(ImportedUtilityIcon)).GetComponent<ImportedUtilityIcon>();icon.transform.SetParent(tab.transform,false);Place(icon.rectTransform,25.5f,7,24,24);icon.symbol=symbols[i];icon.strokeWidth=2.4f;icon.color=JarVisDesignTokens.Ink;icon.raycastTarget=false;}
            }
            if(scan!=null)
            {
                scan.transform.SetParent(root,false);Place(scan.rectTransform,177,-.5f,58,58);var image=scan.GetComponent<Image>();image.enabled=true;image.color=Color.white;UniformUiSurface.Use(image,29,PrototypeUi.Accent,Color.white,3,false,false);
                foreach(var old in scan.GetComponentsInChildren<ImportedUtilityIcon>(true))old.enabled=false;PrototypeUi.Icon(scan.transform,"scan",16,16,26,Color.white);scan.transform.SetAsLastSibling();
            }
        }
        static UnityEngine.UI.Image FooterSurface(string name,RectTransform parent,float x,float y,float width,float height,float radius,Color color,bool adaptTheme)
        {
            var holder=parent.Find(name);
            var image=holder!=null?holder.GetComponent<UnityEngine.UI.Image>():new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
            image.transform.SetParent(parent,false);Place(image.rectTransform,x,y,width,height);image.enabled=true;image.color=Color.white;image.raycastTarget=false;
            UniformUiSurface.Use(image,radius,color,Color.clear,0,adaptTheme:adaptTheme);return image;
        }
        static void Inventory(GameObject root)
        {
            foreach(var card in root.GetComponentsInChildren<FigmaNode>(true).Where(n=>n.nodeName=="stat-card"))
            {
                var info=card.GetComponentsInChildren<FigmaNode>(true).FirstOrDefault(n=>n.nodeName=="stat-info");if(info==null)continue;
                Place(info.rectTransform,46,12,card.rectTransform.rect.width-52,36);
                var labels=info.GetComponentsInChildren<TMP_Text>(true);for(int i=0;i<labels.Length;i++){Place(labels[i].rectTransform,0,i==0?0:19,info.rectTransform.rect.width,20);labels[i].fontSize=i==0?11:15;Fit(labels[i]);}
                var bg=card.GetComponentsInChildren<FigmaNode>(true).FirstOrDefault(n=>n.nodeName=="icon-bg");
                if(bg!=null){Place(bg.rectTransform,12,13,30,30);var glyph=bg.GetComponentInChildren<ImportedUtilityIcon>(true);if(glyph!=null){glyph.transform.SetParent(bg.transform,false);Place(glyph.rectTransform,5,5,20,20);}}
            }
            var search=Find(root,"search-box");if(search!=null){var text=search.GetComponentInChildren<TMP_Text>(true);if(text!=null){Place(text.rectTransform,44,0,search.rectTransform.rect.width-60,46);text.alignment=TextAlignmentOptions.Left;Fit(text);}}
            foreach(var chip in root.GetComponentsInChildren<FigmaNode>(true).Where(n=>n.nodeName.StartsWith("chip-")))
            {var text=chip.GetComponentInChildren<TMP_Text>(true);if(text!=null){Place(text.rectTransform,8,0,chip.rectTransform.rect.width-16,chip.rectTransform.rect.height);text.alignment=TextAlignmentOptions.Center;Fit(text);}}
        }
        static void Settings(GameObject root)
        {
            foreach(var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                if(node.nodeName=="list-item"||node.nodeName=="list-item-slider")
                {var bg=node.GetComponent<Image>();if(bg!=null){bg.sprite=null;bg.color=new Color(1,1,1,.001f);}}
                if(node.nodeName=="list-item")
                {
                    var texts=node.GetComponentsInChildren<TMP_Text>(true);if(texts.Length==0)continue;
                    bool pair=texts.Length>1&&texts[1].transform.parent==node.transform;
                    Place(texts[0].rectTransform,16,0,pair?192:238,node.rectTransform.rect.height);texts[0].alignment=TextAlignmentOptions.Left;Fit(texts[0]);
                    if(pair){Place(texts[1].rectTransform,212,0,node.rectTransform.rect.width-228,node.rectTransform.rect.height);texts[1].alignment=TextAlignmentOptions.Right;texts[1].fontSize=12;Fit(texts[1]);}
                }
            }
            var slider=Find(root,"list-item-slider");if(slider!=null)
            {
                var labels=slider.GetComponentsInChildren<TMP_Text>(true);foreach(var text in labels)
                {if(text.GetComponent<FigmaNode>()?.nodeId=="5:1356"){Place(text.rectTransform,240,0,108,25);text.alignment=TextAlignmentOptions.Right;Fit(text);}
                 if(text.GetComponent<FigmaNode>()?.nodeId=="5:1358"){Place(text.rectTransform,0,0,55,18);Fit(text);}
                 if(text.GetComponent<FigmaNode>()?.nodeId=="5:1362"){Place(text.rectTransform,293,0,55,18);text.alignment=TextAlignmentOptions.Right;Fit(text);}}
            }
            var meta=Find(root,"profile-meta");if(meta!=null){var title=meta.GetComponentInChildren<TMP_Text>(true);Place(title.rectTransform,0,0,260,28);title.fontSize=17;Fit(title);}
            var tag=Find(root,"tag");if(tag!=null){var text=tag.GetComponentInChildren<TMP_Text>(true);Place(text.rectTransform,4,0,tag.rectTransform.rect.width-8,tag.rectTransform.rect.height);text.color=Color.white;Fit(text);}
        }
    }
}

