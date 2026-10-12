using UnityEngine;using UnityEngine.UI;
namespace JarVis.Warehouse.UI { public sealed class ThemeSwitchMotion:MonoBehaviour { ImportedSettings settings;RectTransform thumb;Image track;public void Configure(ImportedSettings s,RectTransform t,Image image){settings=s;thumb=t;track=image;}void Update(){if(settings==null)return;float target=settings.Dark?34:2;thumb.anchoredPosition=new Vector2(Mathf.Lerp(thumb.anchoredPosition.x,target,1-Mathf.Exp(-Time.unscaledDeltaTime*20)),-12);track.color=settings.Dark?new Color(.16f,.42f,.8f):new Color(.78f,.84f,.94f);} } }


