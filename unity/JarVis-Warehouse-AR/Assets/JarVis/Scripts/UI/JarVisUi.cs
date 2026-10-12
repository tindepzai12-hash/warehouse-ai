using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    // Shared primitives for native warehouse screens. Dimensions are logical Figma units.
    public static class JarVisUi
    {
        public static RectTransform Rect(Transform parent,string name,float x,float y,float width,float height)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);return rect;
        }
        public static TMP_Text Text(Transform parent,TMP_FontAsset font,string copy,float x,float y,float w,float h,float size=15)
        {
            var text=Rect(parent,"Label",x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font;text.text=copy;text.fontSize=size*ImportedSettings.TextScale;
            text.characterSpacing=0;text.wordSpacing=0;text.lineSpacing=0;text.paragraphSpacing=0;
            text.fontStyle=FontStyles.Normal;text.alignment=TextAlignmentOptions.MidlineLeft;
            text.color=parent.GetComponentInParent<ImportedFigmaNavigation>()?.Settings?.Dark==true?JarVisDesignTokens.DarkInk:JarVisDesignTokens.Ink;text.gameObject.AddComponent<UiColourOwner>();text.raycastTarget=false;text.richText=false;
            text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;return text;
        }
        public static Button Button(Transform parent,TMP_FontAsset font,string copy,float x,float y,float w,Action clicked,bool primary=false)
        {
            var rect=Rect(parent,copy,x,y,w,48);var image=rect.gameObject.AddComponent<Image>();image.color=Color.white;
            UniformUiSurface.Use(image,12,primary?JarVisDesignTokens.Accent:Color.white,primary?Color.clear:JarVisDesignTokens.Border,primary?0:1);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
            button.onClick.AddListener(()=>clicked());rect.gameObject.AddComponent<ImportedPressFeedback>();
            var label=Text(rect,font,copy,8,0,w-16,48,14);label.alignment=TextAlignmentOptions.Center;if(primary)label.color=Color.white;
            label.textWrappingMode=TextWrappingModes.NoWrap;label.enableAutoSizing=true;
            label.fontSizeMax=14*ImportedSettings.TextScale;label.fontSizeMin=11*ImportedSettings.TextScale;
            return button;
        }
        public static ImportedUtilityIcon Icon(Transform parent,string symbol,float x,float y,float size=24)
        {
            var icon=Rect(parent,symbol,x,y,size,size).gameObject.AddComponent<ImportedUtilityIcon>();
            icon.symbol=symbol;icon.color=JarVisDesignTokens.Accent;icon.raycastTarget=false;return icon;
        }
        public static ScrollRect Scroll(Transform parent,float x,float y,float w,float h,out RectTransform content,bool horizontal=false)
        {
            var view=Rect(parent,"Scroll viewport",x,y,w,h);var hit=view.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);
            view.gameObject.AddComponent<RectMask2D>();content=Rect(view,"Content",0,0,w,h);
            var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.content=content;
            scroll.horizontal=horizontal;scroll.vertical=!horizontal;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.decelerationRate=.08f;return scroll;
        }
    }
}
