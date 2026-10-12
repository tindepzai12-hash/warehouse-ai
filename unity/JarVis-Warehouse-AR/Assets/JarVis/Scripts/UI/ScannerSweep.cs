using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 public sealed class ScannerSweep:MonoBehaviour {
 RectTransform line,host;
 void Awake(){host=(RectTransform)transform;var obj=new GameObject("Red scanning sweep",typeof(RectTransform),typeof(Image));obj.transform.SetParent(transform,false);line=(RectTransform)obj.transform;line.anchorMin=line.anchorMax=line.pivot=new Vector2(.5f,1);var image=obj.GetComponent<Image>();image.color=new Color(1,.13f,.19f,.9f);image.raycastTarget=false;UniformUiSurface.Use(image,1.5f,Color.white,Color.clear,0,false,false);}
 void Update(){float inset=Mathf.Min(8,Mathf.Min(host.rect.width,host.rect.height)*.1f);line.sizeDelta=new Vector2(Mathf.Max(0,host.rect.width-inset*2),3);float t=.5f-.5f*Mathf.Cos(Time.unscaledTime*Mathf.PI/.95f);line.anchoredPosition=new Vector2(0,-Mathf.Lerp(inset+1.5f,Mathf.Max(inset+1.5f,host.rect.height-inset-1.5f),t));line.SetAsLastSibling();}
 }
}
