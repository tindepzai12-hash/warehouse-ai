using System;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    public sealed class PrototypeLoginPresentation:MonoBehaviour
    {
        ImportedFigmaNavigation nav;TMP_FontAsset font;GameObject page;TMP_InputField[] inputs;Button.ButtonClickedEvent login;string key;
        public void Configure(ImportedFigmaNavigation owner)
        {
            nav=owner;font=GetComponentInChildren<TMP_Text>(true).font;
            inputs=GetComponentsInChildren<TMP_InputField>(true).OrderBy(i=>i.contentType==TMP_InputField.ContentType.Password?1:0).ToArray();
            login=new Button.ButtonClickedEvent();login.AddListener(nav.SubmitLogin);
            Build();
        }
        void Update(){if(nav!=null&&key!=nav.Settings.Dark+ImportedSettings.TextScale.ToString()+ImportedAppLocalization.IsEnglish)Build();}
        void Build()
        {
            bool dark=nav.Settings.Dark,en=ImportedAppLocalization.IsEnglish;key=dark+ImportedSettings.TextScale.ToString()+en;
            foreach(var input in inputs)input.transform.SetParent(transform,false);
            foreach(Transform child in transform)if(!inputs.Any(i=>i.transform==child))child.gameObject.SetActive(false);
            if(page!=null)Destroy(page);var root=PrototypeUi.Panel(transform,"Prototype login",0,0,412,915,dark,0,null,"00000000");page=root.gameObject;
            var mascot=JarVisUi.Rect(root,"JarVis logo",168,91,76,73).gameObject.AddComponent<Image>();mascot.sprite=Resources.Load<Sprite>("Prototype/JarVis");mascot.preserveAspect=true;mascot.raycastTarget=false;
            var title=PrototypeUi.Text(root,font,"JarVis Fulfillment",23,176,366,35,20,dark,true);title.color=dark?PrototypeUi.Hex("85b4ff"):PrototypeUi.Accent;title.alignment=TextAlignmentOptions.Center;
            PrototypeUi.Text(root,font,en?"Sign in to your warehouse account.":"Đăng nhập tài khoản kho của bạn.",23,217,366,26,12,dark,false,true).alignment=TextAlignmentOptions.Center;
            string[] labels=en?new[]{"Employee ID","Password"}:new[]{"Mã nhân viên","Mật khẩu"};
            for(int i=0;i<inputs.Length;i++)
            {
                var input=inputs[i];input.transform.SetParent(root,false);input.gameObject.SetActive(true);
                var rect=(RectTransform)input.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(23,-(268+i*87));rect.sizeDelta=new Vector2(366,48);rect.localScale=Vector3.one;
                foreach(var image in input.GetComponentsInChildren<Image>(true))if(image.gameObject!=input.gameObject)image.enabled=false;
                foreach(var glyph in input.GetComponentsInChildren<ImportedUtilityIcon>(true))glyph.enabled=false;
                foreach(var button in input.GetComponentsInChildren<Button>(true))button.enabled=false;
                // The presentation hides legacy child images, including the old viewport hit image.
                // Keep the full input background as its live pointer target instead.
                var background=input.GetComponent<Image>();background.enabled=true;background.raycastTarget=true;
                input.targetGraphic=background;input.enabled=true;input.interactable=true;input.readOnly=false;
                input.transition=Selectable.Transition.None;background.color=Color.white;
                UniformUiSurface.Use(background,14,PrototypeUi.Surface(dark),PrototypeUi.Line(dark),1,false,false);
                var viewport=input.textViewport;viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(14,0);viewport.offsetMax=new Vector2(-14,0);
                foreach(var text in viewport.GetComponentsInChildren<TMP_Text>(true)){text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;text.color=text==input.textComponent?PrototypeUi.Ink(dark):PrototypeUi.Muted(dark);text.fontSize=14*ImportedSettings.TextScale;text.alignment=TextAlignmentOptions.MidlineLeft;}
                PrototypeUi.Text(root,font,labels[i],23,247+i*87,366,21,12,dark,true);
            }
            PrototypeUi.Action(root,font,en?"Sign in":"Đăng nhập",23,421,366,49,dark,()=>login.Invoke(),true);
            var demo=PrototypeUi.Text(root,font,en?"Try demo":"Dùng thử Demo",23,493,366,40,12,dark);demo.color=dark?PrototypeUi.Hex("85b4ff"):PrototypeUi.Accent;demo.alignment=TextAlignmentOptions.Center;PrototypeUi.Button(demo.rectTransform,()=>{JarVis.Warehouse.Backend.OmsMobileSession.Clear();nav.Show("85:14");});
        }
    }
}
