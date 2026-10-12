using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Values measured from the approved B prototype; all coordinates are 412 x 915 logical units.
    public static class PrototypeUi
    {
        public static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
        public static Color Ink(bool dark)=>Hex(dark?"eef4ff":"152640");
        public static Color Muted(bool dark)=>Hex(dark?"b0bfd4":"54657d");
        public static Color Surface(bool dark)=>Hex(dark?"1a2636":"ffffff");
        public static Color Line(bool dark)=>Hex(dark?"35445b":"dce5f0");
        public static Color Accent=>Hex("1255d5");
        public static bool ReducedMotion=>PlayerPrefs.GetInt("JarVis.ReduceMotion",0)==1;
        public static bool ReducedGlass=>PlayerPrefs.GetInt("JarVis.ReduceGlass",0)==1;
        public static RectTransform Panel(Transform parent,string name,float x,float y,float w,float h,bool dark,float radius=22,string fill=null,string border=null,bool glass=false)
        {
            var rect=JarVisUi.Rect(parent,name,x,y,w,h);var image=rect.gameObject.AddComponent<Image>();image.color=Color.white;image.raycastTarget=false;
            UniformUiSurface.Use(image,radius,fill==null?Surface(dark):Hex(fill),border==null?Line(dark):Hex(border),border=="00000000"?0:1,glass,false);
            image.gameObject.AddComponent<UiColourOwner>();if(glass)PrototypeGlassShadow.Attach(image,radius);return rect;
        }
        public static TMP_Text Text(Transform parent,TMP_FontAsset font,string text,float x,float y,float w,float h,float size,bool dark,bool bold=false,bool muted=false)
        {
            var label=JarVisUi.Text(parent,font,text,x,y,w,h,size);label.color=muted?Muted(dark):Ink(dark);label.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;
            label.alignment=TextAlignmentOptions.TopLeft;label.margin=Vector4.zero;return label;
        }
        public static Image Icon(Transform parent,string symbol,float x,float y,float size,Color color)
        {
            var image=JarVisUi.Rect(parent,symbol,x,y,size,size).gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("Prototype/Icons/"+symbol);image.color=color;image.preserveAspect=true;image.raycastTarget=false;image.gameObject.AddComponent<UiColourOwner>();return image;
        }
        public static Button Button(RectTransform rect,Action callback)
        {
            var graphic=rect.GetComponent<Graphic>();if(graphic==null){graphic=rect.gameObject.AddComponent<Image>();graphic.color=new Color(1,1,1,.001f);}graphic.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=graphic;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>callback());rect.gameObject.AddComponent<ImportedPressFeedback>();return button;
        }
        public static Button Action(Transform parent,TMP_FontAsset font,string text,float x,float y,float w,float h,bool dark,Action callback,bool primary=false,string icon=null)
        {
            var rect=Panel(parent,text,x,y,w,h,dark,primary?15:14,primary?"1255d5":dark?"293a51":"f1f6fc",primary?"0c3e976b":dark?"66809f":"ffffffcc",true);
            var button=Button(rect,callback);var label=Text(rect,font,text,10,0,w-20,h,12,dark,true);label.alignment=TextAlignmentOptions.Center;if(primary)label.color=Color.white;
            if(icon!=null){float group=Mathf.Min(w-24,label.GetPreferredValues(text).x+28),start=(w-group)/2;label.rectTransform.anchoredPosition=new Vector2(start+28,0);label.rectTransform.sizeDelta=new Vector2(group-28,h);Icon(rect,icon,start,(h-20)/2,20,primary?Color.white:Ink(dark));}return button;
        }
        public static RectTransform Shell(Transform parent,TMP_FontAsset font,string title,bool dark,Action back,Action tools,out RectTransform content)
        {
            var page=Panel(parent,"Prototype "+title,0,0,412,915,dark,0,null,"00000000");
            Button(JarVisUi.Rect(page,"Back",23,65,44,44),back);Icon(page.Find("Back"),"back",10,10,24,Ink(dark));
            Text(page,font,title,80,65,250,44,20,dark,true).alignment=TextAlignmentOptions.Midline;
            Button(JarVisUi.Rect(page,"Tools plus",345,65,44,44),tools);Icon(page.Find("Tools plus"),"plus",10,10,24,Ink(dark));
            JarVisUi.Scroll(page,23,130,366,678,out content);return page;
        }
        public static LayoutElement Flow(RectTransform rect,float height)
        {var item=rect.gameObject.AddComponent<LayoutElement>();item.minHeight=item.preferredHeight=height;item.flexibleHeight=0;return item;}
        public static VerticalLayoutGroup Column(RectTransform root,float gap=16)
        {var layout=root.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=gap;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;return layout;}
    }
}
