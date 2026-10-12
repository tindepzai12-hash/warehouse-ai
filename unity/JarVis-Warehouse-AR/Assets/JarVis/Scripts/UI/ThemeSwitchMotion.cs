using System;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    public sealed class ThemeSwitchMotion:MonoBehaviour
    {
        Func<bool> value,dark;RectTransform thumb;Image track;
        public void Configure(Func<bool> current,Func<bool> appearance,RectTransform handle,Image surface)
        {value=current;dark=appearance;thumb=handle;track=surface;Paint(true);}
        void Update(){Paint(PrototypeUi.ReducedMotion);}
        void Paint(bool immediate)
        {
            if(value==null)return;bool active=value();float target=active?29:3;
            thumb.anchoredPosition=new Vector2(immediate?target:Mathf.Lerp(thumb.anchoredPosition.x,target,1-Mathf.Exp(-Time.unscaledDeltaTime*20)),-11);
            track.color=active?PrototypeUi.Hex("1255d5"):PrototypeUi.Hex(dark()?"35445b":"dce5f0");thumb.GetComponent<Image>().color=Color.white;
        }
    }
}


