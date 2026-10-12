using System.Linq;
using Cdm.Figma.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI {
 public sealed class ImportedTaskFilterMotion:MonoBehaviour {
 TMP_Text[] labels;TMP_Text chosen;float started=-10;
 public void Configure(){labels=GetComponentsInChildren<TMP_Text>(true).Where(IsFilter).ToArray();if(labels.Length>0)Select(labels[0],false);}
 static bool IsFilter(TMP_Text text){var value=ImportedAppLocalization.Source(text).Trim();return new[]{"Tất cả (","Đang làm (","Đã nhận (","Đã hoàn thành ("}.Any(value.StartsWith);}
 public void Select(TMP_Text label,bool animate=true){chosen=label;started=animate?Time.unscaledTime:-10;Paint();}
 void Update(){Paint();}
 void Paint(){if(labels==null)return;foreach(var label in labels){bool active=label==chosen;var image=label.transform.parent.GetComponent<Image>();if(image!=null){if(image.GetComponent<UniformUiSurface>()==null)return;var target=active?new Color(.07f,.32f,.83f):Color.white;if(!image.TryGetComponent<TaskChipSurface>(out var style)){style=image.gameObject.AddComponent<TaskChipSurface>();style.Configure(image);}style.Tint(target);float t=(Time.unscaledTime-started)/.24f;image.transform.localScale=Vector3.one*(active&&t>=0&&t<1?1+Mathf.Sin(t*Mathf.PI)*.045f:1);}label.color=active?Color.white:new Color(.45f,.49f,.56f);}}
 }
 public sealed class TaskChipSurface:MonoBehaviour {Image image;public void Configure(Image target){image=target;UniformUiSurface.Use(image,16,Color.white,new Color(.82f,.86f,.92f),1,false,false);}public void Tint(Color value){image.color=Color.Lerp(image.color,value,1-Mathf.Exp(-Time.unscaledDeltaTime*25));}}
}
