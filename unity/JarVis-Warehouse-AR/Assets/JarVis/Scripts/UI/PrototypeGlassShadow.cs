using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    public sealed class PrototypeGlassShadow:MonoBehaviour
    {
        RectTransform source;Image shadow;Material material;float radius;
        public Image Graphic=>shadow;
        public static void Attach(Image image,float corner)
        {
            var effect=image.GetComponent<PrototypeGlassShadow>()??image.gameObject.AddComponent<PrototypeGlassShadow>();effect.source=image.rectTransform;effect.radius=corner;
            if(effect.shadow!=null)return;
            var rect=JarVisUi.Rect(image.transform.parent,"Prototype glass shadow",-12,-5,image.rectTransform.rect.width+24,image.rectTransform.rect.height+24);
            rect.SetSiblingIndex(image.transform.GetSiblingIndex());rect.gameObject.AddComponent<LayoutElement>().ignoreLayout=true;
            effect.shadow=rect.gameObject.AddComponent<Image>();effect.shadow.raycastTarget=false;rect.gameObject.AddComponent<UiColourOwner>();
            effect.material=new Material(Shader.Find("JarVis/Prototype Shadow"));effect.shadow.material=effect.material;
        }
        void LateUpdate()
        {
            if(shadow==null)return;shadow.enabled=!PrototypeUi.ReducedGlass;shadow.gameObject.SetActive(source.gameObject.activeInHierarchy);
            var rect=shadow.rectTransform;rect.anchorMin=source.anchorMin;rect.anchorMax=source.anchorMax;rect.pivot=source.pivot;rect.sizeDelta=source.sizeDelta+new Vector2(24,24);
            rect.anchoredPosition=source.anchoredPosition+new Vector2(24*(source.pivot.x-.5f),24*(source.pivot.y-.5f)-7);rect.localScale=source.localScale;
            int index=source.GetSiblingIndex();int before=shadow.transform.GetSiblingIndex()<index?index-1:index;if(shadow.transform.GetSiblingIndex()!=before)shadow.transform.SetSiblingIndex(before);
            var size=new Vector4(source.rect.width,source.rect.height,radius,0);if(material.GetVector("_Size")!=size){material.SetVector("_Size",size);shadow.SetMaterialDirty();}
        }
        void OnEnable(){if(shadow!=null)shadow.gameObject.SetActive(true);}
        void OnDisable(){if(shadow!=null)shadow.gameObject.SetActive(false);}
        void OnDestroy(){if(shadow!=null)Destroy(shadow.gameObject);if(material!=null)Destroy(material);}
    }
}
