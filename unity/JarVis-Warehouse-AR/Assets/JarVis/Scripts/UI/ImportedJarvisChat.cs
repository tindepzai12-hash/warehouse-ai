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
    // Behaviour on the imported screen; keeps its header, composer and bottom navigation.
    public sealed class ImportedJarvisChat : MonoBehaviour
    {
        public TMP_InputField Input { get; private set; }
        public bool IsSending { get; private set; }
        public ImportedChatMedia Media { get; private set; }
        ScrollRect scroll;
        RectTransform content;
        TMP_Text status, empty, pending;
        Graphic statusDot;
        UnityEngine.UI.Button send;
        TMP_FontAsset font;
        Coroutine request;
        RectTransform mascot;
        RectTransform screenRect,composer,fieldRect,conversation,quickActions;
        ImportedFigmaNavigation navigation;
        bool keyboardMode;
        bool voiceSending;
        float lastComposerY=-1;
        float lastFocusedAt;
        public bool ActionsCollapsed => false; // Compact inline controls share one flexible composer row.
        readonly List<UnityEngine.UI.Image> thinkingDots=new List<UnityEngine.UI.Image>();

        public bool CanChat
        {
            get
            {
return OmsMobileSession.IsReady;
            }
        }
        public UnityEngine.UI.Button SendButton => send;
        public string StatusText => status!=null?status.text:"";
        public int MessageCount => rows.Count;
        public string LastReplyText { get; private set; }
        public string LastReplyLanguage { get; private set; }
        string sessionToken, lastQuestion;
        readonly List<GameObject> rows=new List<GameObject>();
        readonly Dictionary<GameObject,Texture2D> imageTextures=new Dictionary<GameObject,Texture2D>();
        static readonly Color Ink=new Color(.09f,.17f,.29f);
        static readonly Color Blue=new Color(.07f,.34f,.83f);

        public void Configure(GameObject root)
        {
            if(Input!=null)return;
            var nodes=root.GetComponentsInChildren<FigmaNode>(true);
            FigmaNode Node(string id)=>Array.Find(nodes,n=>n.nodeId==id);
            font=Node("5:1278").GetComponent<TMP_Text>().font;
            status=Node("5:1237").GetComponent<TMP_Text>();
            status.rectTransform.sizeDelta=new Vector2(200,status.rectTransform.sizeDelta.y);ImportedUiPolish.Fit(status);
            statusDot=Node("5:1236").GetComponent<Graphic>();
            if(statusDot is UnityEngine.UI.Image dot){dot.sprite=ImportedUiQuality.RoundedCard(3);dot.type=UnityEngine.UI.Image.Type.Sliced;}

            // The source contains two overlapping mascot copies. Use the canonical Figma export once.
            foreach(var node in nodes)
                if(node.nodeId=="31:2"||node.nodeId=="14:488"||node.nodeId=="5:1231")node.gameObject.SetActive(false);
            var avatar=Node("5:1230").rectTransform;
            // Keep the approved photograph of the mascot visible without the old blue disk.
            var avatarBackground=avatar.GetComponent<UnityEngine.UI.Image>();
            if(avatarBackground!=null)avatarBackground.enabled=false;
            mascot=Rect("Canonical Login JarVis",avatar);
            mascot.anchorMin=Vector2.zero;mascot.anchorMax=Vector2.one;
            mascot.offsetMin=new Vector2(-6,-6);mascot.offsetMax=new Vector2(6,6);
            var art=mascot.gameObject.AddComponent<UnityEngine.UI.Image>();
            art.sprite=Resources.Load<Sprite>("Prototype/JarVis");
            art.color=Color.white;art.preserveAspect=true;art.raycastTarget=false;
            // Alpha artwork has no matte and can grow inside the reserved header area.
            // An imported 100px ellipsis was overflowing its 22px container.
            var more=Node("5:1480");if(more!=null)more.gameObject.SetActive(false);
            Node("5:1238").gameObject.SetActive(false); // Keep the requested plus menu instead of a second ellipsis menu.

            var area=Node("5:1240").rectTransform;
            foreach(Transform child in area)child.gameObject.SetActive(false); // Remove illustrative stock/messages, not source assets.
            conversation=area;screenRect=(RectTransform)root.transform;navigation=root.GetComponentInParent<ImportedFigmaNavigation>();
            Place(area,root.transform,16,120,380,556);
            var viewport=Rect("Conversation viewport",area);Stretch(viewport,0);
            var hit=viewport.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=new Color(1,1,1,.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content=Rect("Messages",viewport);
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            content.sizeDelta=Vector2.zero;content.anchoredPosition=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding=new RectOffset(0,0,12,16);layout.spacing=12;
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll=area.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
            scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=28;
            empty=Label("Empty conversation",content,"Hỏi JarVis về công việc trong kho\n\nNhập câu hỏi bên dưới để bắt đầu.\nDữ liệu kho được tra theo quyền tài khoản.",15,new Color(.38f,.46f,.57f));
            empty.alignment=TextAlignmentOptions.Center;empty.textWrappingMode=TextWrappingModes.Normal;
            var emptyLayout=empty.gameObject.AddComponent<LayoutElement>();emptyLayout.minWidth=0;emptyLayout.preferredWidth=0;emptyLayout.preferredHeight=150;

            quickActions=Node("5:1269").rectTransform;Place(quickActions,root.transform,16,682,380,48);
            int shortcutIndex=0;
            foreach(Transform shortcut in quickActions)
            {
                if(!(shortcut is RectTransform shortcutRect))continue;
                Place(shortcutRect,quickActions,shortcutIndex++*130,0,120,48);
                foreach(var label in shortcut.GetComponentsInChildren<TMP_Text>(true))
                {
                    Stretch(label.rectTransform,6);label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
                    label.enableAutoSizing=true;label.fontSizeMin=11;label.fontSizeMax=13;
                }
            }
            var bar=Node("5:1276").rectTransform;Place(bar,root.transform,16,738,380,48);
            composer=bar;
            var field=Node("5:1277");
            fieldRect=field.rectTransform;
            var fieldImage=field.GetComponent<UnityEngine.UI.Image>();
            fieldImage.sprite=ImportedUiQuality.RoundedCard(16);fieldImage.type=UnityEngine.UI.Image.Type.Sliced;fieldImage.color=Color.white;
            var placeholder=Node("5:1278").GetComponent<TMP_Text>();
            var textViewport=Rect("Chat text viewport",field.transform);Stretch(textViewport,14);
            textViewport.gameObject.AddComponent<RectMask2D>();
            placeholder.transform.SetParent(textViewport,false);Stretch(placeholder.rectTransform,0);
            placeholder.text="Hỏi JarVis…";placeholder.color=new Color(.42f,.49f,.6f);Normalize(placeholder);
            var typed=Label("Chat editable text",textViewport,"",15,Ink);Stretch(typed.rectTransform,0);
            Input=field.gameObject.AddComponent<TMP_InputField>();Input.textComponent=typed;Input.placeholder=placeholder;Input.textViewport=textViewport;
            Input.characterLimit=1000;Input.lineType=TMP_InputField.LineType.SingleLine;
            Input.customCaretColor=true;Input.caretColor=Blue;Input.caretWidth=2;Input.shouldHideMobileInput=true;
            field.gameObject.AddComponent<ChatComposerTypingTarget>().owner=this;
            Input.selectionColor=new Color(.17f,.43f,.9f,.22f);Input.restoreOriginalTextOnEscape=false;
            Input.targetGraphic=field.GetComponent<UnityEngine.UI.Image>();Input.targetGraphic.raycastTarget=true;
            Input.transition=Selectable.Transition.None;
            Input.onSubmit.AddListener(_=>Send());Input.onValueChanged.AddListener(_=>UpdateSend());
            var buttonNode=Node("5:1281");
            send=buttonNode.GetComponent<UnityEngine.UI.Button>()??buttonNode.gameObject.AddComponent<UnityEngine.UI.Button>();
            send.targetGraphic=buttonNode.GetComponent<UnityEngine.UI.Image>();send.targetGraphic.raycastTarget=true;
            var sendImage=(UnityEngine.UI.Image)send.targetGraphic;sendImage.sprite=ImportedUiQuality.RoundedCard(24);sendImage.type=UnityEngine.UI.Image.Type.Sliced;sendImage.color=Color.white;
            send.transition=Selectable.Transition.ColorTint;
            var colors=send.colors;colors.normalColor=colors.highlightedColor=Blue;colors.pressedColor=new Color(.05f,.26f,.69f);colors.disabledColor=new Color(.6f,.68f,.8f);send.colors=colors;
            send.onClick.RemoveAllListeners();send.onClick.AddListener(Send);
            // Crisp right arrow at the actual send button, independent of font glyph support.
            Arrow(buttonNode.transform,"Arrow shaft",0,0,18,2,0);
            Arrow(buttonNode.transform,"Arrow upper",6,3,9,2,-45);
            Arrow(buttonNode.transform,"Arrow lower",6,-3,9,2,45);

            Media=root.GetComponent<ImportedChatMedia>()??root.AddComponent<ImportedChatMedia>();
            Media.Configure(this,bar,font);
            field.transform.SetParent(bar,false);send.transform.SetParent(bar,false);
            // The row owns its children; only the input flexes when the available width changes.
            foreach(Transform child in bar){var item=child.GetComponent<LayoutElement>()??child.gameObject.AddComponent<LayoutElement>();item.ignoreLayout=true;}
            var row=bar.GetComponent<HorizontalLayoutGroup>()??bar.gameObject.AddComponent<HorizontalLayoutGroup>();row.enabled=true;row.padding=new RectOffset();row.spacing=4;row.childAlignment=TextAnchor.MiddleCenter;
            row.childControlWidth=true;row.childControlHeight=false;row.childForceExpandWidth=false;row.childForceExpandHeight=false;
            int index=0;
            foreach(var item in new[]{Media.PhotoButton.transform,Media.MicButton.transform,Media.ListenButton.transform,field.transform,send.transform})
            {item.SetSiblingIndex(index++);var sizing=item.GetComponent<LayoutElement>();sizing.ignoreLayout=false;sizing.minWidth=item==field.transform?60:44;sizing.preferredWidth=item==field.transform?-1:44;sizing.flexibleWidth=item==field.transform?1:0;((RectTransform)item).sizeDelta=new Vector2(44,48);}
            ApplyComposerLayout(738);
            UpdateSession();UpdateSend();
        }

        void Update()
        {
            if(Input!=null)
            {
                bool focused=Input.isFocused;
                if(focused){lastFocusedAt=Time.unscaledTime;if(!keyboardMode)SetKeyboardMode(true);}
                if(!focused&&keyboardMode&&!TouchScreenKeyboard.visible&&!Media.IsBusy&&Time.unscaledTime-lastFocusedAt>.25f)SetKeyboardMode(false);
                float y=738;
                float keyboardHeight=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.yMax:0;
#if UNITY_ANDROID && !UNITY_EDITOR
                if(keyboardMode&&keyboardHeight<=0)
                    try {using(var native=new AndroidJavaClass("com.jarvis.warehouse.ChatPhotoPicker"))keyboardHeight=native.CallStatic<int>("keyboardHeight");}catch(AndroidJavaException){ }
#endif
                if(keyboardMode&&keyboardHeight>0)
                {
                    var canvas=GetComponentInParent<Canvas>();
                    var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
                    if(RectTransformUtility.ScreenPointToLocalPointInRectangle(screenRect,new Vector2(Screen.width/2f,keyboardHeight),camera,out var local))
                        y=Mathf.Clamp(screenRect.rect.yMax-local.y-60,180,738);
                }
                if(!Mathf.Approximately(y,lastComposerY))ApplyComposerLayout(y);
            }
            if(!IsSending)return;
            float time=Time.unscaledTime;
            if(mascot!=null)mascot.localScale=Vector3.one*(1+.035f*(.5f+.5f*Mathf.Sin(time*5)));
            for(int i=0;i<thinkingDots.Count;i++)
            {
                var dot=thinkingDots[i];if(dot==null)continue;
                float wave=.5f+.5f*Mathf.Sin(time*7-i*1.1f);
                var color=Blue;color.a=.3f+.7f*wave;dot.color=color;
                dot.rectTransform.localScale=Vector3.one*(.8f+.3f*wave);
            }
        }
        public void SetKeyboardMode(bool active)
        {
            keyboardMode=active;
            lastFocusedAt=Time.unscaledTime;
            if(navigation!=null)navigation.SetChatKeyboardVisible(active);
            ApplyComposerLayout(lastComposerY>0?lastComposerY:738);
        }
        public void ExpandMediaActions(){Media?.TogglePlusMenu();}
        void ApplyComposerLayout(float y)
        {
            if(Media==null)return;
            lastComposerY=y;composer.anchorMin=new Vector2(0,1);composer.anchorMax=Vector2.one;composer.pivot=new Vector2(.5f,1);composer.sizeDelta=new Vector2(-32,48);composer.anchoredPosition=new Vector2(0,-y);
            Media.ShowCollapsed(ActionsCollapsed);Media.PositionAttachment(y);
            fieldRect.gameObject.SetActive(!Media.VoiceMode);send.gameObject.SetActive(!Media.VoiceMode);
            quickActions.gameObject.SetActive(!keyboardMode&&!Media.VoiceMode&&Media.Photo==null&&!Media.PlusMenuOpen);
            conversation.sizeDelta=new Vector2(380,Mathf.Max(60,y-120-(keyboardMode?12:74)-(Media.Photo!=null?56:0)));
        }

        void OnEnable(){if(Input!=null)UpdateSession();}
        void OnDisable()
        {
            if(Input!=null){Input.DeactivateInputField();SetKeyboardMode(false);}
            if(request!=null){StopCoroutine(request);request=null;}
            if(IsSending){IsSending=false;RemovePending();if(!voiceSending)Input.text=lastQuestion??"";status.text="Đã dừng gửi — gửi lại khi cần";UpdateSend();}
        }
        void UpdateSession()
        {
            if(sessionToken!=OmsMobileSession.AccessToken)
            {
                foreach(var row in rows)RemoveRow(row);rows.Clear();Input.text="";lastQuestion=null;
                Media?.ResetSession();
                LastReplyText=null;LastReplyLanguage=null;
                sessionToken=OmsMobileSession.AccessToken;empty.gameObject.SetActive(true);

            }
            status.text=OmsMobileSession.IsReady?"Sẵn sàng nhận câu hỏi":"Cần đăng nhập";
            if(statusDot!=null)statusDot.color=CanChat?Blue:new Color(.6f,.66f,.74f);
        }
        void UpdateSend(){if(send!=null)send.interactable=!IsSending&&!(Media?.IsBusy??false)&&(!string.IsNullOrWhiteSpace(Input.text)||Media?.Photo!=null);Media?.Refresh();}
        public void RefreshComposer(){UpdateSend();if(Media!=null)ApplyComposerLayout(lastComposerY>0?lastComposerY:738);}
        public void MediaStatus(string message){if(status!=null)status.text=ImportedAppLocalization.Text(message);}
        public void Send()
        {
            if(!isActiveAndEnabled)return;
            if(IsSending||(Media?.IsBusy??false)||(string.IsNullOrWhiteSpace(Input.text)&&Media?.Photo==null))return;
            if(!CanChat)
            {
                status.text="Đăng nhập để dùng AI";
                empty.text="Chat cần tài khoản nhân viên.\nVui lòng quay lại Đăng nhập.\nChế độ Demo chỉ xem giao diện khi chưa cấu hình AI.";
                return;
            }
            if(Media?.Photo!=null&&!OmsMobileSession.IsReady){status.text="Đăng nhập để gửi ảnh cho JarVis";return;}
            Media?.StopSpeech();
            string question=Input.text.Trim();Input.text="";lastQuestion=question;
            byte[] photo=Media?.Photo;
            empty.gameObject.SetActive(false);
            if(!string.IsNullOrEmpty(question))AddMessage(question,true);
            if(photo!=null)AddImageMessage(photo);
            IsSending=true;status.text="JarVis đang trả lời…";UpdateSend();
            pending=Label("JarVis replying",content,"JarVis đang suy nghĩ…",13,new Color(.4f,.49f,.6f));
            pending.gameObject.AddComponent<LayoutElement>().preferredHeight=52;
            for(int i=0;i<3;i++)
            {
                var dotRect=Rect("Thinking dot",pending.transform);Place(dotRect,pending.transform,4+i*12,30,6,6);
                var dot=dotRect.gameObject.AddComponent<UnityEngine.UI.Image>();dot.sprite=ImportedUiQuality.RoundedCard(3);dot.type=UnityEngine.UI.Image.Type.Sliced;dot.color=Blue;dot.raycastTarget=false;thinkingDots.Add(dot);
            }
            Bottom();
            request=StartCoroutine(Ask(question,photo));
        }
        IEnumerator Ask(string question,byte[] photo)
        {
            OmsMobileApi.JarvisReply reply=null;string error=null;
            if(OmsMobileSession.IsReady)
                yield return OmsMobileApi.AskJarvis(question,photo,value=>reply=value,value=>error=value);

            RemovePending();IsSending=false;request=null;
            if(reply==null)
            {
                AddMessage(ImportedAppLocalization.Text("Không nhận được câu trả lời. Kiểm tra kết nối hoặc phiên đăng nhập, rồi nhấn Gửi để thử lại."),false);
                status.text=CanChat?"Kết nối AI gặp lỗi":"Phiên đăng nhập đã hết";
                if(statusDot!=null)statusDot.color=new Color(.83f,.36f,.16f);
                Input.text=question; // Preserve draft; do not fabricate an answer or auto-spend on retries.
                Debug.LogWarning("[JarVis chat] Request failed: "+error);
            }
            else
            {
                Media?.ClearPhoto();AcceptReply(reply);
            }
            UpdateSend();Bottom();
        }
        // Typed, photo and voice replies share rendering, source disclosure and auto-read behavior.
        public void AcceptVoice(OmsMobileApi.VoiceReply reply)
        {
            AcceptReply(reply.Answer);Bottom();
        }
        public VoiceMessageView AddVoiceMessage(VoiceMessage message,VoicePlayback playback,Action<VoiceMessage> retry)
        {
            empty.gameObject.SetActive(false);
            var row=Rect("User voice message",content);
            var view=row.gameObject.AddComponent<VoiceMessageView>();view.Configure(message,playback,font,retry);
            rows.Add(row.gameObject);TrimRows();Bottom();return view;
        }
        public void SetVoicePending(bool active)
        {
            if(active){voiceSending=true;IsSending=true;pending=Label("JarVis listening",content,"JarVis đang nghe và xử lý…",13,new Color(.4f,.49f,.6f));pending.gameObject.AddComponent<LayoutElement>().preferredHeight=52;Bottom();}
            else if(voiceSending){voiceSending=false;IsSending=false;RemovePending();}
            UpdateSend();
        }
        void AcceptReply(OmsMobileApi.JarvisReply reply)
        {
                LastReplyText=reply.message;LastReplyLanguage=reply.language;
                string answer=reply.message;
                if(reply.sources!=null&&reply.sources.Length>0)
                {
                    var sources=new List<string>();
                    foreach(var source in reply.sources)
                    {
                        string label=source.type=="SOP"?(source.id??"").Split('/')[0]:source.type+": "+source.id;
                        if(!sources.Contains(label))sources.Add(label);
                    }
                    answer+=(reply.language=="en"?"\n\nSources: ":"\n\nNguồn: ")+string.Join(" · ",sources);
                }
                if(reply.synthetic)answer+=reply.language=="en"?"\n\nNote: warehouse data is in demo mode.":"\n\nLưu ý: dữ liệu kho đang ở chế độ mẫu.";
                AddMessage(answer,false);
                Media?.SetAnswer(reply.message);
                status.text=reply.mode=="openai"?"OpenAI • Đã kết nối":reply.mode=="openai-preview"?"OpenAI • Chế độ kiểm thử":"Tra cứu cục bộ";
                if(statusDot!=null)statusDot.color=new Color(.02f,.57f,.37f);
        }
        void RemovePending(){thinkingDots.Clear();if(mascot!=null)mascot.localScale=Vector3.one;if(pending!=null){pending.gameObject.SetActive(false);Destroy(pending.gameObject);pending=null;}}
        public void AddMessage(string message,bool user)
        {
            empty.gameObject.SetActive(false);
            var row=Rect(user?"User message":"JarVis message",content);
            var bubble=Rect("Bubble",row);bubble.anchorMin=bubble.anchorMax=new Vector2(user?1:0,1);bubble.pivot=new Vector2(user?1:0,1);
            var image=bubble.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=ImportedUiQuality.RoundedCard(14);image.type=UnityEngine.UI.Image.Type.Sliced;
            image.color=user?new Color(.9f,.95f,1):Color.white;image.raycastTarget=false;
            UniformUiSurface.Use(image,14,Color.white,new Color(.64f,.71f,.81f));
            var author=Label("Author",bubble,user?"Bạn":"JarVis",11,user?Blue:new Color(.37f,.45f,.57f));
            Place(author.rectTransform,bubble,14,12,288,18);
            // Show model Markdown as readable plain text, never interpret model-supplied TMP tags.
            message=System.Text.RegularExpressions.Regex.Replace(message,@"\*\*(.*?)\*\*","$1");
            message=System.Text.RegularExpressions.Regex.Replace(message,@"(?m)^#{1,6}\s+","");
            var text=Label("Message",bubble,message,15,Ink);
            text.textWrappingMode=TextWrappingModes.Normal;
            float height=text.GetPreferredValues(message,288,0).y;
            Place(text.rectTransform,bubble,14,36,288,height+4);
            float total=height+54;bubble.sizeDelta=new Vector2(316,total);
            var sizing=row.gameObject.AddComponent<LayoutElement>();sizing.preferredHeight=total;sizing.minHeight=total;
            rows.Add(row.gameObject);
            TrimRows();
            Bottom();
        }
        public void ReflowMessages()
        {
            foreach(var row in rows)
            {
                if(row==null)continue;var bubble=row.transform.Find("Bubble") as RectTransform;if(bubble==null)continue;
                var text=bubble.Find("Message")?.GetComponent<TMP_Text>();if(text==null)continue;
                text.fontSize=15*ImportedSettings.TextScale;
                float height=text.GetPreferredValues(text.text,288,0).y;Place(text.rectTransform,bubble,14,36,288,height+4);bubble.sizeDelta=new Vector2(316,height+54);
                var element=row.GetComponent<LayoutElement>();if(element!=null)element.preferredHeight=element.minHeight=height+54;
            }
            if(content!=null)LayoutRebuilder.MarkLayoutForRebuild(content);
        }
        public void AddImageMessage(byte[] jpeg)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGB24,false);
            if(!ImageConversion.LoadImage(texture,jpeg)){Destroy(texture);return;}
            var row=Rect("User photo",content);var bubble=Rect("Photo bubble",row);
            bubble.anchorMin=bubble.anchorMax=bubble.pivot=new Vector2(1,1);
            float width=260,height=Mathf.Min(220,width*texture.height/texture.width);
            width=Mathf.Min(width,height*texture.width/texture.height);
            bubble.sizeDelta=new Vector2(width+16,height+16);
            var background=bubble.gameObject.AddComponent<UnityEngine.UI.Image>();background.sprite=ImportedUiQuality.RoundedCard(14);background.type=UnityEngine.UI.Image.Type.Sliced;background.color=new Color(.9f,.95f,1);background.raycastTarget=false;
            var rect=Rect("Actual attached photo",bubble);Place(rect,bubble,8,8,width,height);
            var photo=rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();photo.texture=texture;photo.raycastTarget=false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=height+16;
            imageTextures[row.gameObject]=texture;rows.Add(row.gameObject);TrimRows();Bottom();
        }
        void TrimRows(){if(rows.Count>60){RemoveRow(rows[0]);rows.RemoveAt(0);}}
        void RemoveRow(GameObject row){if(imageTextures.TryGetValue(row,out var texture)){Destroy(texture);imageTextures.Remove(row);}row.SetActive(false);Destroy(row);}
        void OnDestroy(){foreach(var texture in imageTextures.Values)if(texture!=null)Destroy(texture);imageTextures.Clear();}
        void Bottom(){if(isActiveAndEnabled)StartCoroutine(ScrollBottom());}
        IEnumerator ScrollBottom(){yield return null;Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;}
        TMP_Text Label(string name,Transform parent,string value,float size,Color color)
        {
            var rect=Rect(name,parent);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font=font;text.text=value;text.color=color;text.fontSize=size*ImportedSettings.TextScale;Normalize(text);
            if(name!="Message"&&name!="Chat editable text")ImportedAppLocalization.Bind(text);
            return text;
        }
        static void Normalize(TMP_Text text)
        {
            text.fontStyle=FontStyles.Normal;text.characterSpacing=text.wordSpacing=0;text.margin=Vector4.zero;
            text.richText=false;text.enableAutoSizing=false;text.raycastTarget=false;text.alignment=TextAlignmentOptions.TopLeft;
            if(namePlaceholder(text))text.alignment=TextAlignmentOptions.MidlineLeft;
        }
        static bool namePlaceholder(TMP_Text text)=>text.name=="Chat editable text"||text.GetComponent<FigmaNode>()!=null;
        static RectTransform Rect(string name,Transform parent)
        {
            var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;
        }
        static void Stretch(RectTransform rect,float inset)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*inset;rect.offsetMax=Vector2.one*-inset;rect.localScale=Vector3.one;}
        static void Place(RectTransform rect,Transform parent,float x,float y,float width,float height)
        {rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height);rect.localScale=Vector3.one;}
        static void Arrow(Transform parent,string name,float x,float y,float width,float height,float angle)
        {
            var rect=Rect(name,parent);rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(x,y);rect.localEulerAngles=new Vector3(0,0,angle);
            var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=Color.white;image.raycastTarget=false;image.gameObject.AddComponent<UiColourOwner>();
        }
    }
    public sealed class ChatComposerTypingTarget:MonoBehaviour,UnityEngine.EventSystems.IPointerDownHandler
    {
        public ImportedJarvisChat owner;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData){owner.SetKeyboardMode(true);}
    }
}
