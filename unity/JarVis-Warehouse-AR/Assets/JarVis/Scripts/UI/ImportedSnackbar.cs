using System.Collections;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // One compact notification in the same 412×915 coordinate space as the imported screens.
    public sealed class ImportedSnackbar:MonoBehaviour
    {
        public GameObject Panel {get;private set;}
        public string Message=>label!=null?label.text:"";
        TMP_Text label;
        CanvasGroup fade;
        RectTransform rect;
        Coroutine lifetime;
        bool aboveFooter;
        public void Show(Transform host,TMP_FontAsset font,string message,bool error,bool hasFooter,float duration=6)
        {
            Dismiss();aboveFooter=hasFooter;
            Panel=new GameObject("JarVis snackbar",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(CanvasGroup));Panel.transform.SetParent(host,false);
            rect=(RectTransform)Panel.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);
            rect.sizeDelta=new Vector2(380,80);
            var background=Panel.GetComponent<UnityEngine.UI.Image>();background.sprite=ImportedUiQuality.RoundedCard(16);background.type=UnityEngine.UI.Image.Type.Sliced;background.color=new Color(.06f,.16f,.31f);background.raycastTarget=false;
            fade=Panel.GetComponent<CanvasGroup>();fade.alpha=0;fade.blocksRaycasts=true;
            var accent=new GameObject("Severity accent",typeof(RectTransform),typeof(UnityEngine.UI.Image));accent.transform.SetParent(Panel.transform,false);
            var ar=(RectTransform)accent.transform;ar.anchorMin=new Vector2(0,.15f);ar.anchorMax=new Vector2(0,.85f);ar.pivot=new Vector2(0,.5f);ar.anchoredPosition=new Vector2(8,0);ar.sizeDelta=new Vector2(3,0);
            accent.GetComponent<UnityEngine.UI.Image>().color=error?new Color(1,.66f,.47f):new Color(.35f,.73f,1);accent.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var text=new GameObject("Notification message",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(Panel.transform,false);
            label=text.GetComponent<TMP_Text>();label.font=font;label.text=ImportedAppLocalization.Text(message);label.fontSize=14;label.color=Color.white;label.richText=false;label.raycastTarget=false;label.alignment=TextAlignmentOptions.MidlineLeft;label.textWrappingMode=TextWrappingModes.Normal;
            label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(22,12);label.rectTransform.offsetMax=new Vector2(-56,-12);
            float height=Mathf.Clamp(label.GetPreferredValues(label.text,302,0).y+28,64,132);rect.sizeDelta=new Vector2(380,height);
            var close=new GameObject("Dismiss notification",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button));close.transform.SetParent(Panel.transform,false);
            var cr=(RectTransform)close.transform;cr.anchorMin=cr.anchorMax=cr.pivot=new Vector2(1,.5f);cr.anchoredPosition=new Vector2(-4,0);cr.sizeDelta=new Vector2(48,48);
            close.GetComponent<UnityEngine.UI.Image>().color=new Color(1,1,1,.001f);
            var symbol=new GameObject("Close icon",typeof(RectTransform),typeof(ChatActionIcon));symbol.transform.SetParent(close.transform,false);
            var icon=symbol.GetComponent<ChatActionIcon>();icon.symbol=ChatActionIcon.Symbol.Close;icon.color=Color.white;icon.raycastTarget=false;icon.rectTransform.sizeDelta=new Vector2(20,20);
            var button=close.GetComponent<UnityEngine.UI.Button>();button.targetGraphic=icon;button.onClick.AddListener(Dismiss);
            ImportedAppLocalization.Bind(label);Position();lifetime=StartCoroutine(Animate(duration));
        }
        void LateUpdate(){if(Panel!=null)Position();}
        void Position()
        {
            if(TouchScreenKeyboard.visible){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,1);rect.anchoredPosition=new Vector2(0,-108);}
            else{rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,aboveFooter?128:40);}
            Panel.transform.SetAsLastSibling();
        }
        IEnumerator Animate(float duration)
        {
            float t=0;while(t<.16f){t+=Time.unscaledDeltaTime;fade.alpha=Mathf.Clamp01(t/.16f);yield return null;}
            if(duration<=0)yield break;
            yield return new WaitForSecondsRealtime(duration);
            t=0;while(t<.16f){t+=Time.unscaledDeltaTime;fade.alpha=1-Mathf.Clamp01(t/.16f);yield return null;}
            lifetime=null;Dismiss();
        }
        public void Dismiss(){if(lifetime!=null){StopCoroutine(lifetime);lifetime=null;}if(Panel!=null){Panel.SetActive(false);Destroy(Panel);Panel=null;}}
        void OnDisable(){Dismiss();}
    }
}
