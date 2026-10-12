using System.Linq;
using Cdm.Figma.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI
{
    // Presentation reuses the original camera decoder/manual input; no second scanner service.
    public sealed class PrototypeScannerPresentation:MonoBehaviour
    {
        ImportedFigmaNavigation owner;string appearance;
        void Update(){if(owner!=null&&appearance!=owner.Settings.Dark+ImportedAppLocalization.IsEnglish.ToString())Configure(owner);}
        public void Configure(ImportedFigmaNavigation nav)
        {
            owner=nav;appearance=nav.Settings.Dark+ImportedAppLocalization.IsEnglish.ToString();
            bool dark=nav.Settings.Dark;var font=GetComponentInChildren<TMP_Text>(true).font;
            var nodes=GetComponentsInChildren<FigmaNode>(true);var box=nodes.First(n=>n.nodeName=="viewfinder-box").rectTransform;
            var panel=nodes.First(n=>n.nodeName=="bottom-scans-panel").rectTransform;
            box.SetParent(transform,false);panel.SetParent(transform,false);
            foreach(Transform child in transform){child.gameObject.SetActive(false);if(child.name.StartsWith("Prototype "))Destroy(child.gameObject);}
            var page=PrototypeUi.Shell(transform,font,ImportedAppLocalization.IsEnglish?"Scan code":"Quét mã",dark,nav.Back,()=>nav.Show("prototype:tools"),out var content);
            PrototypeUi.Text(content,font,ImportedAppLocalization.IsEnglish?"Center the barcode / QR inside the blue frame.":"Căn giữa mã vạch / QR trong khung xanh.",0,0,366,32,12,dark,false,true);
            box.SetParent(content,false);box.gameObject.SetActive(true);Place(box,0,42,366,370);
            var background=box.GetComponent<Image>();if(background!=null){var surface=background.GetComponent<UniformUiSurface>();if(surface!=null){surface.enabled=false;Destroy(surface);}background.enabled=true;background.material=null;background.color=new Color(.1f,.15f,.17f);background.sprite=ImportedUiQuality.RoundedCard(24);background.type=Image.Type.Sliced;if(box.GetComponent<Mask>()==null)box.gameObject.AddComponent<Mask>();}
            var preview=box.GetComponentInChildren<RawImage>(true);preview.enabled=true;preview.rectTransform.anchorMin=Vector2.zero;preview.rectTransform.anchorMax=Vector2.one;preview.rectTransform.offsetMin=preview.rectTransform.offsetMax=Vector2.zero;
            foreach(var text in box.GetComponentsInChildren<TMP_Text>(true))text.gameObject.SetActive(false);
            var target=nodes.First(n=>n.nodeName=="scan-target-frame").rectTransform;target.SetParent(box,false);target.gameObject.SetActive(true);Place(target,64,82,238,208);
            foreach(var image in target.GetComponentsInChildren<Image>())if(image.gameObject!=target.gameObject&&image.name!="Red scanning sweep")image.enabled=false;
            var border=target.GetComponent<Image>()??target.gameObject.AddComponent<Image>();UniformUiSurface.Use(border,16,Color.clear,PrototypeUi.Accent,3,false,false);border.raycastTarget=false;
            foreach(Transform child in box)if(child.name=="Frame guidance"){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var guidance=PrototypeUi.Text(box,font,ImportedAppLocalization.IsEnglish?"Center the barcode / QR inside the blue frame":"Căn giữa mã vạch / QR trong khung xanh",20,326,326,28,11,dark,true);guidance.name="Frame guidance";guidance.color=Color.white;guidance.alignment=TextAlignmentOptions.Center;
            panel.SetParent(content,false);panel.gameObject.SetActive(true);Place(panel,0,434,366,205);
            foreach(var graphic in panel.GetComponentsInChildren<Graphic>(true))if(graphic.gameObject==panel.gameObject||graphic.GetComponentInParent<TMP_InputField>()==null&&graphic.name!="Camera status"&&graphic.GetComponentInParent<Button>()==null)graphic.enabled=false;
            var manual=panel.GetComponentInChildren<TMP_InputField>(true);Place((RectTransform)manual.transform,0,55,244,48);var fieldImage=manual.GetComponent<Image>();fieldImage.enabled=true;fieldImage.color=Color.white;UniformUiSurface.Use(fieldImage,14,PrototypeUi.Surface(dark),PrototypeUi.Line(dark),1,false,false);
            manual.textComponent.color=PrototypeUi.Ink(dark);((TMP_Text)manual.placeholder).text=ImportedAppLocalization.IsEnglish?"Enter QR / barcode":"Nhập mã QR / barcode";
            ((TMP_Text)manual.placeholder).color=PrototypeUi.Muted(dark);
            foreach(var label in manual.GetComponentsInChildren<TMP_Text>()){label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(10,0);label.rectTransform.offsetMax=new Vector2(-10,0);label.alignment=TextAlignmentOptions.MidlineLeft;label.fontSize=13;}
            var lookup=panel.GetComponentsInChildren<Button>(true).First(b=>b.name=="Lookup code");lookup.gameObject.SetActive(true);Place((RectTransform)lookup.transform,256,55,110,48);var lookupImage=lookup.GetComponent<Image>();lookupImage.enabled=true;lookupImage.color=Color.white;UniformUiSurface.Use(lookupImage,14,PrototypeUi.Accent,Color.clear,0,false,false);var lookupLabel=lookup.GetComponentInChildren<TMP_Text>(true);lookupLabel.enabled=true;lookupLabel.text=ImportedAppLocalization.IsEnglish?"Look up":"Tra mã";lookupLabel.color=Color.white;
            var status=panel.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Camera status");Place(status.rectTransform,0,118,366,85);status.color=PrototypeUi.Muted(dark);status.fontSize=12;
            foreach(Transform child in panel)if(child.name=="Prototype manual action"){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            var manualAction=PrototypeUi.Action(panel,font,ImportedAppLocalization.IsEnglish?"Enter manually":"Nhập thủ công",0,0,366,44,dark,()=>manual.ActivateInputField());manualAction.name="Prototype manual action";
            content.sizeDelta=new Vector2(366,665);
        }
        static void Place(RectTransform rect,float x,float y,float w,float h){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);rect.localScale=Vector3.one;}
    }
}
