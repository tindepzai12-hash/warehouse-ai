using System;
using System.Collections;
using System.Collections.Generic;
using Cdm.Figma.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Preferences and account UI are runtime additions; imported Figma assets remain untouched.
    public sealed class ImportedSettings : MonoBehaviour
    {
        public static float TextScale => new[]{.9f,1f,1.12f}[Mathf.Clamp(PlayerPrefs.GetInt("JarVis.TextSize",1),0,2)];
        public string Theme => PlayerPrefs.GetString("JarVis.Theme","light");
        public GameObject Panel { get; private set; }
        public TMP_InputField CurrentPassword,NewPassword,ConfirmPassword;
        public bool Busy {get;private set;}
        ImportedFigmaNavigation nav; ImportedAppLocalization loc; TMP_FontAsset font;
        string page; RectTransform body,view; TMP_Text error; Button save; float next;
        readonly Dictionary<Graphic,Color> originals=new Dictionary<Graphic,Color>();
        readonly Dictionary<Graphic,Color> applied=new Dictionary<Graphic,Color>();
        [Serializable] sealed class Tone {public string id;public Color fill;public float radius;}
        [Serializable] sealed class Tones {public Tone[] entries;}
        sealed class Paint {public Image image;public Sprite sprite;public Color tint;public Image.Type type;public Tone tone;}
        readonly List<Paint> surfaces=new List<Paint>();
        readonly Dictionary<TMP_Text,float> inputSizes=new Dictionary<TMP_Text,float>();
        TMP_Text themeValue;
        GameObject prototypeMain;string prototypeMainKey;
        public void BuildPrototypeMain()
        {
            if(nav==null)return;var root=Array.Find(nav.screens,s=>s.id=="5:1308").root;
            foreach(Transform child in root.transform)if(child.gameObject!=Panel)child.gameObject.SetActive(false);
            if(prototypeMain!=null)Destroy(prototypeMain);prototypeMainKey=Dark+ImportedAppLocalization.IsEnglish.ToString()+TextScale;
            bool en=ImportedAppLocalization.IsEnglish;string E(string vi,string english)=>en?english:vi;
            var shell=PrototypeUi.Shell(root.transform,font,E("Cài đặt","Settings"),Dark,()=>nav.Show("prototype:profile"),()=>nav.Show("prototype:tools"),out var content);prototypeMain=shell.gameObject;
            var card=PrototypeUi.Panel(content,"Appearance and account",0,0,366,368,Dark);
            PrototypeUi.Text(card,font,E("Cỡ chữ","Text size"),20,20,220,24,14,Dark,true);
            var size=PrototypeUi.Text(card,font,(en?new[]{"Small","Medium","Large"}:new[]{"Nhỏ","Vừa","Lớn"})[PlayerPrefs.GetInt("JarVis.TextSize",1)],245,20,100,24,12,Dark,true);size.alignment=TextAlignmentOptions.TopRight;size.color=PrototypeUi.Accent;
            CreateSizeSlider(card,33,48,300);
            foreach(var item in new[]{new[]{E("Nhỏ","Small"),"20"},new[]{E("Vừa","Medium"),"155"},new[]{E("Lớn","Large"),"290"}})PrototypeUi.Text(card,font,item[0],int.Parse(item[1]),95,55,24,11,Dark,false,true);
            PrototypeUi.Text(card,font,E("Chế độ buổi tối","Dark mode"),20,144,235,28,14,Dark,true);PrototypeUi.Text(card,font,E("Giao diện tối, dịu mắt","A calmer dark appearance"),20,175,235,26,11,Dark,false,true);CreateThemeSwitch(card,260,142);
            var language=JarVisUi.Rect(card,"Language",20,226,326,48);PrototypeUi.Button(language,()=>Open("language"));PrototypeUi.Text(language,font,E("Ngôn ngữ","Language"),0,10,170,28,14,Dark);
            PrototypeUi.Text(language,font,en?"English":"Tiếng Việt",186,10,110,28,12,Dark,false,true);PrototypeUi.Icon(language,"chevron",308,14,16,PrototypeUi.Muted(Dark));
            var password=JarVisUi.Rect(card,"Change password",20,300,326,48);PrototypeUi.Button(password,()=>Open("password"));PrototypeUi.Text(password,font,E("Đổi mật khẩu","Change password"),0,10,270,28,14,Dark);PrototypeUi.Icon(password,"chevron",308,14,16,PrototypeUi.Muted(Dark));
            PrototypeUi.Text(content,font,E("Hiển thị & chuyển động","Display & motion"),0,395,366,35,19,Dark,true);
            var preferences=PrototypeUi.Panel(content,"Motion and transparency",0,424,366,185,Dark);
            Preference(preferences,E("Giảm hiệu ứng kính","Reduce transparency"),E("Ưu tiên nền đặc, dễ đọc","Prefer solid, readable surfaces"),"JarVis.ReduceGlass",16);
            Preference(preferences,E("Giảm chuyển động","Reduce motion"),E("Giữ phản hồi không dùng animation","Feedback without animation"),"JarVis.ReduceMotion",96);
            var logout=PrototypeUi.Action(content,font,E("Đăng xuất","Sign out"),0,634,366,48,Dark,()=>PrototypeDialog.SignOut(nav),true);UniformUiSurface.Use(logout.GetComponent<Image>(),15,PrototypeUi.Hex("d82c3e"),Color.clear,0,false,false);
            content.sizeDelta=new Vector2(366,706);
        }
        void Preference(Transform parent,string title,string subtitle,string preference,float y)
        {
            PrototypeUi.Text(parent,font,title,20,y,246,30,13,Dark);PrototypeUi.Text(parent,font,subtitle,20,y+31,246,30,11,Dark,false,true);
            bool active=PlayerPrefs.GetInt(preference,0)==1;var toggle=PrototypeUi.Panel(parent,title,287,y+10,58,34,Dark,17,active?"1255d5":Dark?"35445b":"dce5f0","00000000",true);
            var thumb=PrototypeUi.Panel(toggle,"Thumb",active?27:3,3,28,28,Dark,14,"ffffff","dce5f0",true);
            PrototypeUi.Button(toggle,()=>{PlayerPrefs.SetInt(preference,active?0:1);PlayerPrefs.Save();BuildPrototypeMain();});
        }
        public bool Dark => Theme=="dark"||(Theme=="system"&&DeviceDark());
        Color Surface=>Dark?new Color(.07f,.1f,.16f):Color.white;
        Color Ink=>Dark?new Color(.9f,.94f,1):new Color(.09f,.16f,.28f);
        Color Card=>Dark?new Color(.13f,.18f,.27f):new Color(.95f,.97f,1);
        Color Accent=>Dark?new Color(.45f,.7f,1):new Color(.07f,.32f,.83f);
        public void Configure(ImportedFigmaNavigation owner,ImportedAppLocalization language)
        {
            nav=owner;loc=language;font=nav.screens[0].root.GetComponentInChildren<TMP_Text>(true).font;
            foreach(var input in nav.GetComponentsInChildren<TMP_InputField>(true))if(input.textComponent!=null)inputSizes[input.textComponent]=input.textComponent.fontSize/(input.textComponent.name=="Chat editable text"?TextScale:1);
            ApplyInputSizes();
            var tones=new Dictionary<string,Tone>();var data=Resources.Load<TextAsset>("JarVisThemeSurfaces");
            if(data!=null)foreach(var tone in JsonUtility.FromJson<Tones>(data.text).entries)tones[tone.id]=tone;
            foreach(var node in nav.transform.GetComponentsInChildren<FigmaNode>(true))
            {var image=node.GetComponent<Image>();if(image!=null&&tones.TryGetValue(node.nodeId,out var tone))surfaces.Add(new Paint{image=image,sprite=image.sprite,tint=image.color,type=image.type,tone=tone});}
            foreach(var graphic in nav.GetComponentsInChildren<Graphic>(true))originals[graphic]=graphic.color;
            loc.Changed+=RefreshRows;ApplyTheme();RefreshRows();
            var settings=Array.Find(nav.screens,s=>s.id=="5:1308").root;
            foreach(var node in settings.GetComponentsInChildren<FigmaNode>(true))
            {
                if(node.nodeName=="toggle-off")
                {
                    node.gameObject.SetActive(false);CreateThemeSwitch(node.transform.parent,260,3);
                }
                if(node.nodeName!="list-item-slider")continue;
                // Replace the non-interactive artwork with a clearly labelled real action.
                foreach(var child in node.GetComponentsInChildren<Image>(true))if(child.gameObject!=node.gameObject)child.enabled=false;
                foreach(var label in node.GetComponentsInChildren<TMP_Text>(true))if(label.GetComponent<FigmaNode>()?.nodeId=="5:1358"||label.GetComponent<FigmaNode>()?.nodeId=="5:1362")label.enabled=false;
                var hit=node.GetComponent<Image>()??node.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);hit.raycastTarget=true;
                var button=node.GetComponent<Button>();if(button!=null)button.enabled=false;
                CreateSizeSlider(node.transform,66,22,210);
                Label(node.transform,"Nhỏ",16,36,44,24,11);var largest=Label(node.transform,"Lớn",300,36,44,24,11);largest.alignment=TextAlignmentOptions.Right;
            }
            foreach(var node in settings.GetComponentsInChildren<FigmaNode>(true))if(node.nodeName=="btn-logout")
            {var bg=node.GetComponent<Image>();if(bg==null)continue;bg.color=Color.white;UniformUiSurface.Use(bg,14,new Color(.82f,.12f,.16f),Color.clear,0,false,false);var text=node.GetComponentInChildren<TMP_Text>(true);if(text!=null){if(text.GetComponent<UiColourOwner>()==null)text.gameObject.AddComponent<UiColourOwner>();text.color=Color.white;text.alignment=TextAlignmentOptions.Center;text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(12,0);text.rectTransform.offsetMax=new Vector2(-12,0);}}
            RefreshRows();
            foreach(var screen in nav.screens)
            foreach(var node in screen.root.GetComponentsInChildren<FigmaNode>(true))
            {
                if(node.nodeName!="mascot-container")continue;
                var plate=new GameObject("JarVis contrast backing",typeof(RectTransform),typeof(Image));plate.transform.SetParent(node.transform,false);plate.transform.SetAsFirstSibling();
                var rect=(RectTransform)plate.transform;rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,1);rect.offsetMin=new Vector2(-6,-6);rect.offsetMax=new Vector2(6,6);
                var image=plate.GetComponent<Image>();image.color=Color.white;image.raycastTarget=false;UniformUiSurface.Use(image,22,new Color(.83f,.9f,.99f),new Color(.4f,.58f,.8f,.65f),1,false,false);
                plate.AddComponent<MascotDarkBacking>().Configure(this);
            }
        }
        static bool DeviceDark()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try {using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))using(var resources=activity.Call<AndroidJavaObject>("getResources"))using(var config=resources.Call<AndroidJavaObject>("getConfiguration"))return (config.Get<int>("uiMode")&0x30)==0x20;}catch(Exception){return false;}
#else
            return false; // Editor has no Android device theme.
#endif
        }
        public void Open(string kind)
        {
            if(Busy)return;Close();page=kind;
            var host=Array.Find(nav.screens,s=>s.id=="5:1308").root.transform;
            Panel=Rect("Settings detail",host,0,0,412,915).gameObject;
            var bg=Panel.AddComponent<Image>();bg.color=Surface;bg.raycastTarget=true;
            nav.BottomNavigation.SetActive(false);
            var back=ButtonAt(Panel.transform,"",16,32,48,48,Close,false);back.GetComponent<Image>().sprite=null;back.GetComponent<Image>().color=Color.clear;
            var arrow=Rect("Back arrow",back.transform,12,12,24,24).gameObject.AddComponent<ImportedUtilityIcon>();arrow.symbol="arrow-left";arrow.color=Ink;arrow.raycastTarget=false;
            Label(Panel.transform,kind=="password"?"Thay đổi mật khẩu":kind=="language"?"Ngôn ngữ":kind=="size"?"Cỡ chữ":"Giao diện",16,94,380,40,25);
            view=Rect("Settings viewport",Panel.transform,16,150,380,680);view.gameObject.AddComponent<RectMask2D>();
            var hit=view.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);hit.raycastTarget=true;
            body=Rect("Settings content",view,0,0,380,650);
            var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.viewport=view;scroll.content=body;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=25;
            if(kind=="password")PasswordForm();
            else if(kind=="language")
            {
                Choice("Tiếng Việt (VI)",0,!ImportedAppLocalization.IsEnglish,()=>{loc.Select(SupportedLanguage.Vi);Open("language");});
                Choice("English (EN)",70,ImportedAppLocalization.IsEnglish,()=>{loc.Select(SupportedLanguage.En);Open("language");});
                Label(body,"Lựa chọn được lưu trên thiết bị và áp dụng cho toàn bộ giao diện.",0,166,380,76,15);
            }
            else if(kind=="size")
            {
                CreateSizeSlider(body,16,32,348);
                Label(body,"Nhỏ",0,100,110,30,14);var middle=Label(body,"Vừa",135,100,110,30,14);middle.alignment=TextAlignmentOptions.Center;var large=Label(body,"Lớn",270,100,110,30,14);large.alignment=TextAlignmentOptions.Right;
                Label(body,"Xem trước",0,242,380,28,14);
                Label(body,"Kiểm kê vị trí B-03-02",0,286,380,50,20);
                Label(body,"Quét mã hàng để kiểm tra số lượng tồn kho tại vị trí này.",0,345,380,92,16);
            }
            else
            {
                Choice("Sáng",0,Theme=="light",()=>SelectTheme("light"));Choice("Tối",70,Theme=="dark",()=>SelectTheme("dark"));Choice("Theo thiết bị",140,Theme=="system",()=>SelectTheme("system"));
                Label(body,"Theo thiết bị sử dụng chế độ sáng hoặc tối của điện thoại.",0,245,380,85,15);
            }
            ApplyTheme();
        }
        void Choice(string label,float y,bool selected,Action action)
        {
            bool literal=label=="Tiếng Việt (VI)"||label=="English (EN)";
            var button=ButtonAt(body,label,0,y,380,58,action,false,!literal);button.GetComponent<Image>().color=selected?(Dark?new Color(.16f,.27f,.43f):new Color(.87f,.93f,1)):Card;
            if(selected){var icon=Rect("Selected",button.transform,340,17,24,24).gameObject.AddComponent<ImportedUtilityIcon>();icon.symbol="check";icon.color=Accent;icon.raycastTarget=false;}
        }
        public void SelectSize(int value){PlayerPrefs.SetInt("JarVis.TextSize",Mathf.Clamp(value,0,2));PlayerPrefs.Save();loc.RefreshPresentation();ApplyInputSizes();foreach(var chat in nav.GetComponentsInChildren<ImportedJarvisChat>(true))chat.ReflowMessages();}
        void CreateSizeSlider(Transform parent,float x,float y,float w)
        {
            var root=Rect("Three-step text size",parent,x,y,w,48);var hit=root.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);
            var slider=root.gameObject.AddComponent<Slider>();slider.minValue=0;slider.maxValue=2;slider.wholeNumbers=true;slider.transition=Selectable.Transition.None;
            var track=Rect("Track",root,0,22,w,4).gameObject.AddComponent<Image>();track.color=new Color(.76f,.81f,.88f);track.raycastTarget=false;UniformUiSurface.Use(track,2,Color.white,Color.clear,0,false,false);
            var fill=Rect("Selected size track",root,0,22,w*.5f,4).gameObject.AddComponent<Image>();fill.color=new Color(.07f,.32f,.83f);fill.raycastTarget=false;UniformUiSurface.Use(fill,2,Color.white,Color.clear,0,false,false);
            for(int i=0;i<3;i++){var tick=Rect("Size detent "+i,root,i*w-2,22,4,4).gameObject.AddComponent<Image>();tick.rectTransform.anchoredPosition=new Vector2(i*w*.5f-2,-22);tick.color=new Color(.42f,.58f,.8f);tick.raycastTarget=false;UniformUiSurface.Use(tick,2,Color.white,Color.clear,0,false,false);}
            var area=Rect("Handle area",root,0,24,w,0);var handle=Rect("Glass handle",area,0,0,24,24).gameObject.AddComponent<Image>();handle.rectTransform.anchorMin=handle.rectTransform.anchorMax=handle.rectTransform.pivot=new Vector2(.5f,.5f);handle.color=new Color(.35f,.65f,1);UniformUiSurface.Use(handle,12,Color.white,new Color(.88f,.96f,1),1,true,false);
            slider.handleRect=handle.rectTransform;slider.targetGraphic=handle;slider.direction=Slider.Direction.LeftToRight;slider.SetValueWithoutNotify(PlayerPrefs.GetInt("JarVis.TextSize",1));slider.onValueChanged.AddListener(v=>SelectSize(Mathf.RoundToInt(v)));root.gameObject.AddComponent<TextSizeSliderMotion>().Configure(slider,fill.rectTransform,w);root.gameObject.AddComponent<UiGlassBackdrop>();
        }
        void CreateThemeSwitch(Transform parent,float x,float y)
        {
            var root=Rect("Glass dark-mode switch",parent,x+16,y,60,48);var hit=root.gameObject.AddComponent<Image>();hit.color=new Color(1,1,1,.001f);var bg=Rect("Glass switch track",root,0,10,60,28).gameObject.AddComponent<Image>();bg.color=Card;bg.raycastTarget=false;UniformUiSurface.Use(bg,14,Color.white,new Color(.35f,.65f,1,.6f),1,true,false);
            var thumb=Rect("Glass toggle thumb",root,2,12,24,24).gameObject.AddComponent<Image>();thumb.color=Color.white;UniformUiSurface.Use(thumb,12,Color.white,new Color(.35f,.65f,1,.65f),1,true,false);
            var button=root.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>SelectTheme(Dark?"light":"dark"));root.gameObject.AddComponent<UiGlassBackdrop>();root.gameObject.AddComponent<ThemeSwitchMotion>().Configure(this,thumb.rectTransform,bg);
        }
        void ApplyInputSizes(){foreach(var entry in inputSizes)if(entry.Key!=null)entry.Key.fontSize=entry.Value*TextScale;}
        public void SelectTheme(string value){if(value!="light"&&value!="dark"&&value!="system")return;PlayerPrefs.SetString("JarVis.Theme",value);PlayerPrefs.Save();ApplyTheme();RefreshRows();if(page=="theme")Open("theme");}
        void PasswordForm()
        {
            Label(body,"Dùng ít nhất 12 ký tự. Sau khi đổi, bạn cần đăng nhập lại trên các thiết bị.",0,0,380,76,15);
            CurrentPassword=Field("Mật khẩu hiện tại",86);NewPassword=Field("Mật khẩu mới",200);ConfirmPassword=Field("Xác nhận mật khẩu mới",314);
            error=Label(body,"",0,430,380,85,14);error.color=Dark?new Color(1,.65f,.55f):new Color(.7f,.16f,.12f);
            save=ButtonAt(body,"Lưu mật khẩu",0,536,380,52,SubmitPassword,true);
            if(!OmsMobileSession.IsReady){error.text=T("Vui lòng đăng nhập để đổi mật khẩu.");save.interactable=false;}
        }
        public void SubmitPassword()
        {
            if(Busy)return;
            if(!OmsMobileSession.IsReady){error.text=T("Vui lòng đăng nhập để đổi mật khẩu.");return;}
            if(string.IsNullOrEmpty(CurrentPassword.text)||NewPassword.text.Length<12||NewPassword.text.Length>256){error.text=T("Mật khẩu mới cần từ 12 đến 256 ký tự.");return;}
            if(NewPassword.text!=ConfirmPassword.text){error.text=T("Mật khẩu xác nhận không khớp.");return;}
            if(CurrentPassword.text==NewPassword.text){error.text=T("Mật khẩu mới phải khác mật khẩu hiện tại.");return;}
            StartCoroutine(Change());
        }
        IEnumerator Change()
        {
            Busy=true;save.interactable=false;CurrentPassword.interactable=NewPassword.interactable=ConfirmPassword.interactable=false;error.text=T("Đang lưu...");
            string failure=null;OmsMobileApi.PasswordReply reply=null;
            yield return OmsMobileApi.ChangePassword(CurrentPassword.text,NewPassword.text,r=>reply=r,e=>failure=e);
            Busy=false;
            if(reply!=null&&reply.ok){CurrentPassword.text=NewPassword.text=ConfirmPassword.text="";OmsMobileSession.Clear();Close();nav.Show("5:36",false);nav.Notice("Đã đổi mật khẩu. Vui lòng đăng nhập lại.");yield break;}
            if(Panel!=null){save.interactable=true;CurrentPassword.interactable=NewPassword.interactable=ConfirmPassword.interactable=true;
                error.text=T(failure=="Current password is incorrect"?"Mật khẩu hiện tại không đúng.":failure!=null&&failure.Contains("Too many")?"Bạn đã thử quá nhiều lần. Vui lòng thử lại sau.":!OmsMobileSession.IsReady?"Phiên đăng nhập đã hết. Vui lòng đăng nhập lại.":"Không lưu được mật khẩu. Kiểm tra kết nối rồi thử lại.");}
        }
        TMP_InputField Field(string title,float y)
        {
            Label(body,title,0,y,380,28,14);
            var box=Rect(title,body,0,y+34,380,58);var image=box.gameObject.AddComponent<Image>();image.sprite=Dark?ImportedUiQuality.ThemedCard(12,Card):ImportedUiQuality.RoundedCard(12);image.type=Image.Type.Sliced;image.color=Dark?Color.white:Card;
            UniformUiSurface.Use(image,12,Dark?Card:Color.white,new Color(.64f,.71f,.81f));
            var area=Rect("Text area",box,14,0,294,58);area.gameObject.AddComponent<RectMask2D>();
            var text=Label(area,"",0,0,294,58,16,false);text.fontSize=16*TextScale;var placeholder=Label(area,"Nhập mật khẩu",0,0,294,58,16);placeholder.color=Dark?new Color(.6f,.7f,.83f):new Color(.4f,.47f,.59f);
            var field=box.gameObject.AddComponent<TMP_InputField>();field.textViewport=area;field.textComponent=text;field.placeholder=placeholder;field.targetGraphic=image;field.contentType=TMP_InputField.ContentType.Password;field.characterLimit=256;field.lineType=TMP_InputField.LineType.SingleLine;field.customCaretColor=true;field.caretColor=Accent;
            box.gameObject.AddComponent<ImportedInputCaretRepair>().Configure(field);
            var eye=ButtonAt(box,"",322,5,48,48,()=>{field.contentType=field.contentType==TMP_InputField.ContentType.Password?TMP_InputField.ContentType.Standard:TMP_InputField.ContentType.Password;field.ForceLabelUpdate();},false);
            eye.GetComponent<Image>().sprite=null;eye.GetComponent<Image>().color=Color.clear;
            var glyph=Rect("Password visibility",eye.transform,12,12,24,24).gameObject.AddComponent<ImportedUtilityIcon>();glyph.symbol="eye";glyph.color=Accent;glyph.raycastTarget=false;
            return field;
        }
        public void Close()
        {
            if(Busy)return;
            if(Panel!=null){Panel.SetActive(false);Destroy(Panel);Panel=null;}
            CurrentPassword=NewPassword=ConfirmPassword=null;
            nav?.RefreshNavigationVisibility();
            page=null;
        }
        void Update()
        {
            if(prototypeMain!=null&&nav.CurrentScreen=="5:1308"&&prototypeMainKey!=Dark+ImportedAppLocalization.IsEnglish.ToString()+TextScale&&Panel==null)BuildPrototypeMain();
            if(Panel!=null&&nav.CurrentScreen!="5:1308"&&!Busy)Close();
            if(Panel!=null&&view!=null){float keyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height/Mathf.Max(1,Screen.height)*915:0;view.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Clamp(680-keyboard,230,680));}
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;ApplyTheme(false);
        }
        bool? lastTheme;
        public void ApplyTheme(bool force=true)
        {
            bool dark=Dark;
            if(!force&&lastTheme==dark)return;
            lastTheme=dark;
            foreach(var graphic in nav.transform.GetComponentsInChildren<Graphic>(false))
            {
                if(graphic.GetComponent<UiColourOwner>()!=null)continue;
                if(graphic!=null&&nav.BottomNavigation!=null&&graphic.transform.IsChildOf(nav.BottomNavigation.transform))continue;
                if(graphic==null||graphic.transform.IsChildOf(Panel!=null?Panel.transform:transform)&&Panel!=null)continue;
                bool text=graphic is TMP_Text,icon=graphic is ImportedUtilityIcon||graphic is ChatActionIcon;
                var image=graphic as Image;
                if(image!=null&&image.GetComponent<UniformUiSurface>()!=null)continue;
                bool surface=image!=null&&(image.sprite==null||image.sprite.name.StartsWith("JarVis crisp card")||image.sprite.name.StartsWith("Figma rounded"));
                if(!text&&!icon&&!surface)continue; // Preserve logos, portraits and warehouse/map imagery.
                if(!originals.TryGetValue(graphic,out var original)){original=graphic.color;originals[graphic]=original;}
                else if(!text&&!icon&&applied.TryGetValue(graphic,out var previous)&&graphic.color!=previous){original=graphic.color;originals[graphic]=original;}
                Color color=original;
                if(dark&&original.a>.01f)
                {
                    if(text||icon){float lum=(original.r+original.g+original.b)/3; if(lum<.8f)color=original.b>.5f&&original.b>original.r*1.5f?new Color(.45f,.7f,1,original.a):new Color(.85f,.9f,.98f,original.a);}
                    else if(original.r>.75f&&original.g>.75f&&original.b>.75f)color=new Color(.1f,.15f,.23f,original.a);
                }
                graphic.color=color;
                applied[graphic]=color;
            }
            foreach(var paint in surfaces)if(paint.image!=null&&paint.image.GetComponent<UniformUiSurface>()==null)
            {
                if(dark&&paint.tone.fill.r>.75f&&paint.tone.fill.g>.75f&&paint.tone.fill.b>.75f&&paint.tone.fill.a>.1f){paint.image.sprite=paint.tone.radius>0?ImportedUiQuality.ThemedCard(Mathf.RoundToInt(paint.tone.radius),new Color(.1f,.15f,.23f)):null;paint.image.type=paint.tone.radius>0?Image.Type.Sliced:Image.Type.Simple;paint.image.color=paint.tone.radius>0?Color.white:new Color(.07f,.1f,.16f,paint.tone.fill.a);}
                else {paint.image.sprite=paint.sprite;paint.image.type=paint.type;paint.image.color=paint.tint;}
                applied[paint.image]=paint.image.color;
            }
        }
        void RefreshRows()
        {
            if(nav==null)return;
            if(themeValue!=null)themeValue.text=T(Theme=="dark"?"Tối":Theme=="system"?"Theo thiết bị":"Sáng");
            var settings=Array.Find(nav.screens,s=>s.id=="5:1308").root;
            foreach(var text in settings.GetComponentsInChildren<TMP_Text>(true))
            {
                var source=ImportedAppLocalization.Source(text);
                if(text.GetComponent<FigmaNode>()?.nodeId=="5:1356")text.text=T(new[]{"Nhỏ","Vừa","Lớn"}[Mathf.Clamp(PlayerPrefs.GetInt("JarVis.TextSize",1),0,2)]);
            }
        }
        static string T(string s)=>ImportedAppLocalization.Text(s);
        RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
        {
            var obj=new GameObject(name,typeof(RectTransform));obj.transform.SetParent(parent,false);var r=(RectTransform)obj.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        TMP_Text Label(Transform parent,string value,float x,float y,float w,float h,float size,bool bind=true)
        {
            var text=Rect(value,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.color=Ink;text.richText=false;text.raycastTarget=false;text.alignment=TextAlignmentOptions.MidlineLeft;text.textWrappingMode=TextWrappingModes.Normal;if(bind)ImportedAppLocalization.Bind(text);return text;
        }
        Button ButtonAt(Transform parent,string value,float x,float y,float w,float h,Action action,bool primary,bool translate=true)
        {
            var rect=Rect(value,parent,x,y,w,h);var bg=rect.gameObject.AddComponent<Image>();bg.sprite=ImportedUiQuality.RoundedCard(12);bg.type=Image.Type.Sliced;bg.color=primary?new Color(.07f,.32f,.83f):Card;
            UniformUiSurface.Use(bg,12,Color.white,new Color(.64f,.71f,.81f));
            var text=Label(rect,value,16,0,w-48,h,16,translate);if(primary)text.color=Color.white;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.onClick.AddListener(()=>action());rect.gameObject.AddComponent<ImportedPressFeedback>();return button;
        }
        void OnDestroy(){if(loc!=null)loc.Changed-=RefreshRows;}
    }
}







