using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 public sealed class TextSizeSliderMotion:MonoBehaviour {
 Slider slider;RectTransform fill;float width;
 public void Configure(Slider control,RectTransform selected,float trackWidth){slider=control;fill=selected;width=trackWidth;}
 void Update(){if(slider==null)return;int size=Mathf.Clamp(PlayerPrefs.GetInt("JarVis.TextSize",1),0,2);if(slider.value!=size)slider.SetValueWithoutNotify(size);fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width*size*.5f);}
 }
}
