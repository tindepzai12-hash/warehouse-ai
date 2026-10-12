using System.Collections;
using System.Linq;
using Cdm.Figma.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI
{
    // Compact offline shell; the actual dashboard and shared footer are captured and blurred.
    public sealed class ImportedOfflineBlur : MonoBehaviour
    {
        ImportedFigmaNavigation nav;
        GameObject overlay;
        RawImage blurImage;
        RenderTexture texture;
        TMP_Text greeting, role, action, heading, message;
        Button online;
        Image header;
        bool dark, refreshing;
        string language;
        int size;
        public void Configure(ImportedFigmaNavigation owner)
        {
            nav=owner;
            var old=GetComponentsInChildren<FigmaNode>(true).FirstOrDefault(n=>n.nodeName=="locked-state-overlay");
            if(old!=null)old.gameObject.SetActive(false);
            var font=GetComponentInChildren<TMP_Text>(true).font;
            // The new Home supplies the backdrop. Disable legacy offline artwork completely
            // so old labels/cards cannot show through the captured texture.
            foreach(Transform child in transform)child.gameObject.SetActive(false);
            // Hide the imported tall header before adding the compact replacement.
            // Preserve dashboard graphics below it and the original Figma assets.
            var corners=new Vector3[4];
            foreach(var graphic in GetComponentsInChildren<Graphic>(true))
            {
                graphic.rectTransform.GetWorldCorners(corners);
                float bottom=transform.InverseTransformPoint(corners[0]).y;
                float top=transform.InverseTransformPoint(corners[1]).y;
                if(top<=1&&bottom>=-136&&top-bottom<=136)graphic.enabled=false;
            }
            var surface=JarVisUi.Rect(transform,"Offline header surface",0,0,412,203);
            header=surface.gameObject.AddComponent<Image>();header.color=Color.white;
            var bar=JarVisUi.Rect(surface,"Compact offline header",0,44,412,159);
            var portrait=JarVisUi.Rect(bar,"Justin profile",29,21.5f,44,44).gameObject.AddComponent<UserPortraitGraphic>();portrait.raycastTarget=false;
            var profile=JarVisUi.Rect(bar,"Profile action",23,17.5f,56,48).gameObject.AddComponent<Image>();profile.color=new Color(1,1,1,.001f);
            var profileButton=profile.gameObject.AddComponent<Button>();profileButton.onClick.AddListener(()=>nav.Show("prototype:profile"));
            greeting=JarVisUi.Text(bar,font,"",91,23.2f,242,27,18);greeting.fontStyle=FontStyles.Bold;
            role=JarVisUi.Text(bar,font,"",91,50.2f,242,22,12);
            online=JarVisUi.Button(bar,font,"",23,97,366,()=>nav.Presence.Toggle());
            var buttonRect=(RectTransform)online.transform;buttonRect.sizeDelta=new Vector2(366,44);
            UniformUiSurface.Use(online.GetComponent<Image>(),22,PrototypeUi.Accent,Color.clear,0,false,false);
            action=online.GetComponentInChildren<TMP_Text>();action.color=Color.white;action.fontSizeMax=14;action.fontSizeMin=11;action.rectTransform.sizeDelta=new Vector2(342,44);
            var bell=PrototypeUi.Action(bar,font,"",345,19,44,44,nav.Settings.Dark,()=>nav.Notice(ImportedAppLocalization.IsEnglish?"No new notifications.":"Chưa có thông báo mới."));
            PrototypeUi.Icon(bell.transform,"bell",10,10,24,PrototypeUi.Ink(nav.Settings.Dark));
            overlay=JarVisUi.Rect(transform,"Offline blur and centered message",0,0,412,915).gameObject;
            blurImage=JarVisUi.Rect(overlay.transform,"Blurred dashboard including navigation",0,203,412,712).gameObject.AddComponent<RawImage>();
            blurImage.uvRect=new Rect(0,0,1,712f/915);blurImage.raycastTarget=true;
            var shade=JarVisUi.Rect(blurImage.transform,"Content dimming",0,0,412,712).gameObject.AddComponent<Image>();
            shade.color=new Color(24/255f,43/255f,64/255f,.68f);shade.raycastTarget=false;
            PrototypeUi.Icon(overlay.transform,"lock",166,403,80,Color.white);
            heading=JarVisUi.Text(overlay.transform,font,"",29,497,354,40,24);heading.fontStyle=FontStyles.Bold;
            message=JarVisUi.Text(overlay.transform,font,"",29,548,354,54,16);
            heading.alignment=message.alignment=TextAlignmentOptions.Center;heading.color=message.color=Color.white;
            heading.overflowMode=message.overflowMode=TextOverflowModes.Overflow;
            overlay.SetActive(false);Paint();if(isActiveAndEnabled)StartCoroutine(Refresh());
        }
        void Paint()
        {
            bool en=ImportedAppLocalization.IsEnglish;
            greeting.text=en?"Hello, Justin":"Xin chào, Justin";
            role.text=en?"Warehouse operator":"Nhân viên vận hành";
            bool busy=nav.Presence.State==PresenceState.Connecting||nav.Presence.State==PresenceState.Loading;
            online.interactable=!busy;
            action.text=busy?(en?"Connecting…":"Đang kết nối…"):
                nav.Presence.State==PresenceState.ConnectionLost||nav.Presence.State==PresenceState.Reconnecting?(en?"Retry":"Thử lại"):(en?"Go online":"Bật trực tuyến");
            heading.text=en?"Features are locked.":"Tính năng đang bị khóa.";
            message.text=en?"Please go ONLINE\nto access these features.":"Vui lòng bật chế độ TRỰC TUYẾN\nđể truy cập các tính năng.";
            var ink=nav.Settings.Dark?JarVisDesignTokens.DarkInk:JarVisDesignTokens.Ink;
            greeting.color=ink;role.color=nav.Settings.Dark?new Color(.7f,.78f,.88f):JarVisDesignTokens.Secondary;action.color=Color.white;
            header.color=PrototypeUi.Surface(nav.Settings.Dark);
            float scale=ImportedSettings.TextScale;
            greeting.fontSize=18*scale;role.fontSize=11*scale;heading.fontSize=24*scale;message.fontSize=16*scale;
        }
        void OnEnable(){if(nav!=null)StartCoroutine(Refresh());}
        IEnumerator Refresh()
        {
            if(refreshing)yield break;refreshing=true;overlay.SetActive(false);
            yield return null;yield return null;nav.Settings.ApplyTheme();Paint();
            // Footer is visible for the snapshot, then the full-screen blur covers it and blocks input.
            nav.BottomNavigation.transform.SetAsLastSibling();
            // Capture the same current Home used online, without activating API/task loading.
            var home=nav.screens.First(s=>s.id=="126:8").root;
            var dashboard=home.GetComponent<ImportedHomeDashboard>();
            bool wasActive=home.activeSelf;
            var visibility=GetComponent<CanvasGroup>()??gameObject.AddComponent<CanvasGroup>();
            float previousAlpha=visibility.alpha;var homeRect=(RectTransform)home.transform;var originalPosition=homeRect.anchoredPosition;
            RenderTexture fresh=null;
            try
            {
                dashboard.BeginBackdropPreview();home.SetActive(true);homeRect.anchoredPosition=originalPosition+Vector2.down*62;visibility.alpha=0;
                nav.BottomNavigation.transform.SetAsLastSibling();
                fresh=UiBackdropCapture.Capture(nav,4);
            }
            finally{homeRect.anchoredPosition=originalPosition;visibility.alpha=previousAlpha;home.SetActive(wasActive);dashboard.EndBackdropPreview();}
            transform.SetAsLastSibling();
            if(texture!=null){texture.Release();Destroy(texture);}texture=fresh;blurImage.texture=texture;
            overlay.SetActive(true);overlay.transform.SetAsLastSibling();
            dark=nav.Settings.Dark;language=ImportedAppLocalization.IsEnglish?"en":"vi";size=PlayerPrefs.GetInt("JarVis.TextSize",1);refreshing=false;
        }
        void Update()
        {
            if(nav==null||overlay==null)return;Paint();
            if(overlay.activeSelf&&!refreshing&&(dark!=nav.Settings.Dark||language!=(ImportedAppLocalization.IsEnglish?"en":"vi")||size!=PlayerPrefs.GetInt("JarVis.TextSize",1)))StartCoroutine(Refresh());
        }
        void OnDisable(){StopAllCoroutines();refreshing=false;if(overlay!=null)overlay.SetActive(false);}
        void OnDestroy(){if(texture!=null){texture.Release();Destroy(texture);}}
    }
}
