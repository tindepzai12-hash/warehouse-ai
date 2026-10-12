using System;
using System.Collections.Generic;
using Cdm.Figma.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Runtime behaviour is separate from the imported Figma visuals.
    public sealed class ImportedFigmaNavigation : MonoBehaviour
    {
        [Serializable] public sealed class Screen { public string id; public GameObject root; }
        public Screen[] screens;
        public string initialScreen = "5:36";
        public string CurrentScreen { get; private set; }
        public int BoundButtonCount { get; private set; }
        public GameObject LastClickedButton { get; private set; }
        readonly Stack<string> history = new Stack<string>();
        readonly Dictionary<string, GameObject> lookup = new Dictionary<string, GameObject>();
        readonly HashSet<GameObject> bound = new HashSet<GameObject>();
        readonly HashSet<string> screensWithNavigation = new HashSet<string>();
        public GameObject BottomNavigation { get; private set; }
        bool ready;
        bool? footerDark;
        ImportedAppLocalization localization;
        TMP_Text viChoice,enChoice;
        UnityEngine.UI.Image languageHighlight;
        ImportedSnackbar snackbar;
        public ImportedSettings Settings { get; private set; }
        public ImportedPresence Presence { get; private set; }

        void Awake() { Initialize(); }
        void Update()
        {
            RefreshNavigationVisibility();
            if(BottomNavigation!=null&&footerDark!=(Settings!=null&&Settings.Dark))UpdateSharedTabs(CurrentScreen);
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }
        public void Initialize()
        {
            if (ready) return;
            ready = true;
            if(Application.isPlaying&&FindFirstObjectByType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
            foreach (Screen screen in screens) lookup.Add(screen.id, screen.root);
            foreach (Screen screen in screens)
            {
                if(Application.isPlaying){ImportedUiQuality.Apply(screen.root);ImportedUiPolish.Apply(screen.root);}
                BindScreen(screen);
            }
            CreateSharedBottomNavigation();
            if(Application.isPlaying)
            {
                localization=gameObject.AddComponent<ImportedAppLocalization>();
                localization.Configure(transform);localization.Changed+=UpdateLanguageChoices;UpdateLanguageChoices();
                Settings=gameObject.AddComponent<ImportedSettings>();Settings.Configure(this,localization);
                foreach(var screen in screens){if(screen.id=="126:8"||screen.id=="85:14"||screen.id=="5:1308"){foreach(var text in screen.root.GetComponentsInChildren<TMP_Text>(true))if(ImportedAppLocalization.Source(text).StartsWith("Xin chào"))text.text="Justin";foreach(var node in screen.root.GetComponentsInChildren<FigmaNode>(true))if(node.nodeName=="avatar-ring"||(node.nodeName=="avatar"&&!Array.Exists(node.GetComponentsInParent<FigmaNode>(true),n=>n!=node&&n.nodeName=="avatar-ring"))){foreach(var graphic in node.GetComponentsInChildren<Graphic>(true))graphic.enabled=false;var obj=new GameObject("Justin profile photograph",typeof(RectTransform),typeof(UserPortraitGraphic));obj.transform.SetParent(node.transform,false);var rect=(RectTransform)obj.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}}}
                Presence=gameObject.AddComponent<ImportedPresence>();Presence.Configure(this);
                lookup["126:8"].AddComponent<ImportedHomeDashboard>().Configure(this);
                foreach(var screen in screens)if(screen.id=="48:244"||screen.id=="5:780"||screen.id=="182:2")screen.root.AddComponent<ImportedWarehouseLists>().Configure(this,screen.id=="48:244");
                var offline=lookup["85:14"].AddComponent<ImportedOfflineBlur>();offline.Configure(this);
                gameObject.AddComponent<PrototypeWorkspace>().Configure(this);
                Settings.BuildPrototypeMain();
                lookup["5:36"].AddComponent<PrototypeLoginPresentation>().Configure(this);
            }
            Show(initialScreen, false);
        }
        public void Show(string id, bool remember = true)
        {
            if (!lookup.ContainsKey(id)) throw new ArgumentException("Unknown Figma screen " + id);
            if(OmsMobileSession.IsReady&&Presence!=null&&!Presence.CanWork&&id!="5:36"&&id!="5:11"&&id!="85:14"&&id!="5:1308"&&id!="prototype:profile"&&id!="prototype:activity")
            {Notice(ImportedAppLocalization.IsEnglish?"Go online before using warehouse features.":"Bật trực tuyến trước khi sử dụng tính năng kho.");return;}
            var feedback = transform.Find("Runtime feedback");
            if (feedback != null) Destroy(feedback.gameObject);
            snackbar?.Dismiss();
            if (remember && CurrentScreen != null && CurrentScreen != id) history.Push(CurrentScreen);
            CurrentScreen = id;
            foreach (var entry in lookup) entry.Value.SetActive(entry.Key == id);
            Shader.SetGlobalFloat("_JarVisHasBackdrop",0); // A prior page's captured text must never show inside glass controls.
            if(BottomNavigation!=null)
            {
                RefreshNavigationVisibility();
                BottomNavigation.transform.SetAsLastSibling();
                UpdateSharedTabs(id); Settings?.ApplyTheme();UpdateLanguageChoices();
                if(id=="85:14")lookup[id].transform.SetAsLastSibling();
            }
        }
        public void Back()
        {
            if(CurrentScreen=="5:36"||CurrentScreen=="5:11")return;
            if(Settings!=null&&Settings.Panel!=null){Settings.Close();return;}
            if(CurrentScreen!=null&&lookup[CurrentScreen].GetComponent<ImportedWarehouseLists>()?.CloseDetail()==true)return;
            if (history.Count > 0) Show(history.Pop(), false);
            else Home();
        }
        public void RegisterPrototypeScreen(string id,GameObject root)
        {
            lookup.Add(id,root);var list=new List<Screen>(screens){new Screen{id=id,root=root}};screens=list.ToArray();screensWithNavigation.Add(id);root.SetActive(false);
        }
        public void IncludePrototypeNavigation(string id){screensWithNavigation.Add(id);}
        public void Home() { history.Clear(); Show(Presence!=null&&Presence.CanWork?"126:8":"85:14", false); }
        public void OpenMap(string locationCode)
        {
            lookup["215:2"].GetComponent<WarehouseMapScreen>()?.SetDestination(locationCode);
            Show("215:2");
        }
        public bool OpenMapFromMarker(string code)
        {
            var map=lookup["215:2"].GetComponent<WarehouseMapScreen>();
            if(map==null||!map.AcceptMarkerCode(code))return false;
            Show("215:2");
            return true;
        }
        public void SetChatKeyboardVisible(bool visible)
        {
            if(CurrentScreen=="5:1216")chatKeyboardVisible=visible;
            RefreshNavigationVisibility();
        }
        bool chatKeyboardVisible;
        public void RefreshNavigationVisibility()
        {
            if(BottomNavigation==null)return;
            bool visible=CurrentScreen!=null&&CurrentScreen!="5:36"&&CurrentScreen!="5:11"&&screensWithNavigation.Contains(CurrentScreen);
            if(CurrentScreen=="5:1216"&&chatKeyboardVisible)visible=false;
            if(Settings!=null&&Settings.Panel!=null&&CurrentScreen=="5:1308")visible=false;
            if(BottomNavigation.activeSelf!=visible)BottomNavigation.SetActive(visible);
        }
        void CreateSharedBottomNavigation()
        {
            FigmaNode source=null;
            foreach(var screen in screens)
                foreach(var node in screen.root.GetComponentsInChildren<FigmaNode>(true))
                    if(node.nodeName=="bottom-navigation")
                    {
                        screensWithNavigation.Add(screen.id);
                        if(screen.id=="126:8")source=node;
                    }
            if(source==null)return;
            // One actual imported Home footer survives every page transition.
            BottomNavigation=Instantiate(source.gameObject,lookup["126:8"].transform.parent,false);
            BottomNavigation.name="Shared imported bottom navigation";
            var rect=(RectTransform)BottomNavigation.transform;
            rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(0,101);rect.localScale=Vector3.one;
            foreach(var screen in screens)
                foreach(var node in screen.root.GetComponentsInChildren<FigmaNode>(true))
                    if(node.nodeName=="bottom-navigation")node.gameObject.SetActive(false);
            foreach(var graphic in BottomNavigation.GetComponentsInChildren<Graphic>(true))graphic.raycastTarget=false;
            foreach(var node in BottomNavigation.GetComponentsInChildren<FigmaNode>(true))
            {
                if(node.nodeName=="tab-Trang chủ")Bind(node.gameObject,Home);
                else if(node.nodeName=="tab-Nhiệm vụ")Bind(node.gameObject,()=>Show("48:244"));
                else if(node.nodeName=="tab-Bản đồ")Bind(node.gameObject,()=>Show("215:2"));
                else if(node.nodeName=="tab-JarVis")Bind(node.gameObject,()=>Show("5:1216"));
                else if(node.nodeName=="center-scan-btn")Bind(node.gameObject,()=>Show("5:331"));
            }
            ImportedUiQuality.PrepareTabIcons(BottomNavigation);
            if(Application.isPlaying)ImportedUiPolish.Footer(BottomNavigation);
        }
        void UpdateSharedTabs(string id)
        {
            bool dark=Settings!=null&&Settings.Dark;footerDark=dark;
            string active=id=="215:2"?"tab-Bản đồ":id=="5:1216"||id=="182:151"?"tab-JarVis":
                id=="48:244"||id=="83:89"||id=="14:130"||id=="14:215"||id=="14:300"||id=="32:149"?"tab-Nhiệm vụ":id=="126:8"||id=="85:14"?"tab-Trang chủ":"";
            foreach(var node in BottomNavigation.GetComponentsInChildren<FigmaNode>(true))
                if((node.nodeName??"").StartsWith("tab-"))
                {
                    Color color=node.nodeName==active?(dark?new Color(.45f,.7f,1):JarVisDesignTokens.Accent):(dark?new Color(.65f,.7f,.78f):new Color(.43f,.42f,.41f));
                    foreach(var text in node.GetComponentsInChildren<TMP_Text>(true)){text.color=color;text.fontStyle=node.nodeName==active?FontStyles.Bold:FontStyles.Normal;}
                    foreach(var icon in node.GetComponentsInChildren<ImportedUtilityIcon>(true))icon.color=color;
                    foreach(var image in node.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                        if(image.gameObject!=node.gameObject && image.sprite!=null)image.color=image.name=="Canonical JarVis mascot"?Color.white:color;
                }
            // Runtime SVG images are not FigmaNode children after layout repair.
            foreach(var image in BottomNavigation.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if(image.sprite==null||image.name=="Canonical JarVis mascot"||image.name=="scan")continue;
                if(image.name=="home"||image.name=="tasks"||image.name=="map")
                {
                    string tab=image.name=="home"?"tab-Trang chủ":image.name=="tasks"?"tab-Nhiệm vụ":"tab-Bản đồ";
                    image.color=tab==active?(dark?new Color(.45f,.7f,1):JarVisDesignTokens.Accent):(dark?new Color(.65f,.7f,.78f):new Color(.43f,.42f,.41f));
                }
            }
        }
        void BindScreen(Screen screen)
        {
            foreach (Graphic graphic in screen.root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (FigmaNode node in screen.root.GetComponentsInChildren<FigmaNode>(true))
            {
                string name = node.nodeName ?? "";
                if (name == "tab-Trang chủ") Bind(node.gameObject, Home);
                else if (name == "tab-Nhiệm vụ") Route(node, "48:244");
                else if (name == "tab-Bản đồ") Route(node, "215:2");
                else if (name == "tab-JarVis") Route(node, "5:1216");
                else if (name == "center-scan-btn") Route(node, "5:331");
                else if (name == "back-action" && screen.id == "48:244") Bind(node.gameObject,Home);
                else if (name == "back-icon-wrap" || name == "back-icon" || name == "back-btn" || name == "back-action") Bind(node.gameObject, Back);
                else if (name == "user-profile") Route(node, "5:1308");
                else if (name == "action-ring") Bind(node.gameObject, () => Notice("Chưa có thông báo."));
                else if (name == "menu-icon" || name == "menu-btn" || name == "menu") Route(node, "5:1308");
                else if (node.nodeId == "5:1238") Route(node, "182:151");
            }
            foreach (TMP_Text text in screen.root.GetComponentsInChildren<TMP_Text>(true))
            {
                string label = text.text.Trim();
                GameObject target = text.transform.parent.gameObject;
                var owner = target.GetComponent<FigmaNode>();
                if (owner != null && owner.nodeName.StartsWith("step-")) continue;
                if (owner != null && owner.nodeName == "panel-header") target = text.gameObject;
                switch (label)
                {
                    case "Đăng nhập": Bind(target, Login); break;
                    case "Dùng thử Demo": Bind(target, () => { OmsMobileSession.Clear(); Show("85:14"); }); break;
                    case "BẬT TRỰC TUYẾN": case "ĐANG TRỰC TUYẾN": Bind(target, () => Presence?.Toggle()); break;
                    case "Quét mã nhanh": case "Quét mã": Bind(target, () => Show("5:331")); break;
                    case "Tạo tem hàng": Bind(target, () => Show("5:450")); break;
                    case "Tra tồn kho": case "Kiểm tra tồn kho": Bind(target, () => Show("5:780")); break;
                    case "Báo cáo sự cố": case "Báo cáo lệch": Bind(target, () => Show("5:1029")); break;
                    case "Tìm vị trí hàng": Bind(target, () => Show("215:2")); break;
                    case "Chat quản lý": if (owner == null || owner.nodeName != "text-status") Bind(target, () => Show("182:151")); break;
                    case "Nhập kho": Bind(target, () => Show("5:935")); break;
                    case "Xuất kho": Bind(target, () => Show("14:387")); break;
                    case "Đăng xuất tài khoản": Bind(target, () => Presence?.SignOut()); break;
                    case "Tiếp tục": case "Bắt đầu": case "Bắt đầu nhiệm vụ": Bind(target, () => Show("83:89")); break;
                    case "Xem lại": Bind(target, () => Show("32:149")); break;
                    case "Tôi đã đến nơi !": Bind(target, () => Show("14:130")); break;
                    case "Xác nhận số lượng": Bind(target, () => Notice("Chưa có nhiệm vụ hoặc số lượng để xác nhận.")); break;
                    case "Hoàn tất & về danh sách": Bind(target, () => Show("48:244")); break;
                    case "Chế độ chỉ đường trực tiếp": case "Chỉ đường đi":
                        Bind(target, () => Show("215:2")); break;
                    case "Xác nhận nhập kho": case "Xác nhận xuất kho":
                        Bind(target, () => Notice("Chưa chọn hàng hóa, lô hàng và vị trí kho. Không có giao dịch nào được ghi.")); break;
                    case "In tem": case "Xuất PDF": Bind(target, () => Notice("Chưa có sản phẩm để tạo tem.")); break;
                    case "Gửi báo cáo sự cố": Bind(target, () => Notice("Cần nhập mô tả và chọn vị trí. Chưa gửi báo cáo.")); break;
                    case "Nhập thủ công": if (screen.id != "5:331") Bind(target, () => Notice("Chưa có mã hàng. Nhập dữ liệu hàng hóa trên web quản lý trước.")); break;
                    case "Thay đổi mật khẩu": Bind(target,()=>Settings?.Open("password"));break;
                    case "Cỡ chữ": Bind(target,()=>Settings?.Open("size"));break;
                    case "Thông tin cá nhân": case "Vai trò & Phân quyền":
                        Bind(target, () => Notice("Chức năng tài khoản cần phiên đăng nhập backend. Đây là bản kiểm tra điều hướng Figma.")); break;
                    case "Ngôn ngữ": Bind(target,()=>Settings?.Open("language")); break;
                    case "Thông tin ứng dụng": Bind(target,()=>Notice("JarVis Fulfillment — Ứng dụng hỗ trợ nhân viên vận hành kho.")); break;
                    case "Chế độ buổi tối": Bind(target,()=>Settings?.Open("theme")); break;
                }
                if (label.StartsWith("Tất cả (") || label.StartsWith("Đang làm (") || label.StartsWith("Đã nhận (") || label.StartsWith("Đã hoàn thành ("))
                    Bind(target, () => SelectTaskFilter(screen.root, text));
            }
            foreach (FigmaNode node in screen.root.GetComponentsInChildren<FigmaNode>(true))
                if (node.nodeName == "send-btn" && screen.id != "5:1216") Bind(node.gameObject, () => Notice("Chưa có nội dung tin nhắn. Chat nhân viên chưa được gắn vào bản import này."));
            foreach (var scroll in screen.root.GetComponentsInChildren<ScrollRect>(true))
            {
                var graphic = scroll.GetComponent<Graphic>(); if (graphic != null) graphic.raycastTarget = true;
            }
            if (screen.id == "5:36")
            {
                CreateLanguageChoices(screen.root);
                AddInput(screen.root,"5:64","5:66",false);
                var password = AddInput(screen.root,"5:69","5:72",true);
                if(password!=null)password.onSubmit.AddListener(_=>Login());
                foreach (var node in screen.root.GetComponentsInChildren<FigmaNode>(true))
                    if (node.nodeId == "5:564" && password != null)
                        Bind(node.gameObject, () => { password.contentType = password.contentType == TMP_InputField.ContentType.Password ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password; password.ForceLabelUpdate(); });
            }
            if (screen.id == "5:331" && Application.isPlaying)
            {
                var scanner = screen.root.GetComponent<ImportedCameraScanner>() ?? screen.root.AddComponent<ImportedCameraScanner>();
                scanner.Configure(screen.root);
            }
            if (screen.id == "5:1216" && Application.isPlaying)
            {
                var chat=screen.root.GetComponent<ImportedJarvisChat>() ?? screen.root.AddComponent<ImportedJarvisChat>();
                chat.Configure(screen.root);
            }
        }
        void CreateLanguageChoices(GameObject root)
        {
            var pill=Array.Find(root.GetComponentsInChildren<FigmaNode>(true),n=>n.nodeName=="lang-pill");if(pill==null)return;
            TMP_FontAsset font=pill.GetComponentInChildren<TMP_Text>(true).font;
            foreach(Transform child in pill.transform)child.gameObject.SetActive(false);
            pill.rectTransform.sizeDelta=new Vector2(104,48);
            var highlight=new GameObject("Selected language",typeof(RectTransform),typeof(UnityEngine.UI.Image));highlight.transform.SetParent(pill.transform,false);
            var highlightRect=(RectTransform)highlight.transform;highlightRect.anchorMin=highlightRect.anchorMax=highlightRect.pivot=new Vector2(0,.5f);highlightRect.sizeDelta=new Vector2(48,40);
            languageHighlight=highlight.GetComponent<UnityEngine.UI.Image>();languageHighlight.sprite=ImportedUiQuality.RoundedCard(12);languageHighlight.type=UnityEngine.UI.Image.Type.Sliced;languageHighlight.color=new Color(.07f,.34f,.83f);languageHighlight.raycastTarget=false;
            TMP_Text Choice(string label,float x,SupportedLanguage language)
            {
                var target=new GameObject("Choose "+label,typeof(RectTransform));target.transform.SetParent(pill.transform,false);
                var rect=(RectTransform)target.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=new Vector2(x,0);rect.sizeDelta=new Vector2(48,48);
                var textObject=new GameObject("Language label",typeof(RectTransform),typeof(TextMeshProUGUI));textObject.transform.SetParent(target.transform,false);
                var text=textObject.GetComponent<TMP_Text>();text.font=font;text.text=label;text.fontSize=14;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
                text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
                Bind(target,()=>localization.Select(language));return text;
            }
            viChoice=Choice("VI",4,SupportedLanguage.Vi);enChoice=Choice("EN",52,SupportedLanguage.En);
        }
        void UpdateLanguageChoices()
        {
            if(viChoice==null)return;bool english=ImportedAppLocalization.IsEnglish;
            viChoice.color=english?new Color(.07f,.3f,.65f):Color.white;enChoice.color=english?Color.white:new Color(.07f,.3f,.65f);
            languageHighlight.rectTransform.anchoredPosition=new Vector2(english?52:4,0);
        }
        void Route(FigmaNode node, string id) { Bind(node.gameObject, () => Show(id)); }
        public void RepairLoginInputs()
        {
            var root=Array.Find(screens,s=>s.id=="5:36")?.root;
            if(root==null)return;
            AddInput(root,"5:64","5:66",false);
            var password=AddInput(root,"5:69","5:72",true);
            if(password!=null){password.onSubmit.RemoveAllListeners();password.onSubmit.AddListener(_=>Login());}
        }
        bool signingIn;
        public void SubmitLogin(){Login();}
        void Login()
        {
            if (!Application.isPlaying || signingIn) return;
            var nodes = lookup["5:36"].GetComponentsInChildren<FigmaNode>(true);
            var code = Array.Find(nodes,n=>n.nodeId=="5:64")?.GetComponent<TMP_InputField>();
            var password = Array.Find(nodes,n=>n.nodeId=="5:69")?.GetComponent<TMP_InputField>();
            if (code == null || password == null || string.IsNullOrWhiteSpace(code.text) || string.IsNullOrEmpty(password.text))
            { Notice("Nhập mã nhân viên và mật khẩu để đăng nhập."); return; }
            if (!OmsMobileSession.TryLoadEndpoint(out string endpoint))
            { Notice("Chưa cấu hình địa chỉ backend OMS. Không thể đăng nhập hoặc tra dữ liệu kho thật."); return; }
            if(!System.Text.RegularExpressions.Regex.IsMatch(code.text.Trim(),@"^NV-\d{5}$") && !string.Equals(code.text.Trim(),"admin",StringComparison.OrdinalIgnoreCase))
            { Notice("Nhập mã nhân viên NV-00000 hoặc tài khoản admin."); return; }
            StartCoroutine(SignIn(endpoint,code.text.Trim(),password));
        }
        System.Collections.IEnumerator SignIn(string endpoint,string code,TMP_InputField password)
        {
            signingIn=true; Notice("Đang đăng nhập…",false,0);
            OmsMobileApi.LoginReply reply=null;string error=null;
            yield return OmsMobileApi.Login(endpoint,code,password.text,value=>reply=value,value=>error=value);
            signingIn=false;password.text="";
            if(CurrentScreen!="5:36")yield break;
            if(reply==null)
            {
                string message=error!=null&&error.StartsWith("OMS network request failed (0)",StringComparison.Ordinal)?"Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.":"Không đăng nhập được. Kiểm tra tài khoản và thử lại.";
                Notice(message,true);yield break;
            }
            bool valid=true;
            try { OmsMobileSession.Set(endpoint,reply.accessToken,reply.user.employeeCode,reply.expiresAt); }
            catch(ArgumentException){valid=false;}
            if(!valid){Notice("Phiên đăng nhập không hợp lệ. Vui lòng thử lại.");yield break;}
            history.Clear();Show("85:14",false);Presence?.Restore();
        }
        void SelectTaskFilter(GameObject root, TMP_Text selected)
        {
            var motion=root.GetComponent<ImportedTaskFilterMotion>();if(motion==null){motion=root.AddComponent<ImportedTaskFilterMotion>();motion.Configure();}motion.Select(selected);
        }
        void Bind(GameObject target, UnityEngine.Events.UnityAction action)
        {
            for (Transform parent = target.transform.parent; parent != null; parent = parent.parent) if (bound.Contains(parent.gameObject)) return;
            if (!bound.Add(target)) return;
            Graphic hit = target.GetComponent<Graphic>();
            if (hit == null) { var image = target.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(1,1,1,.001f); hit = image; }
            if (hit is UnityEngine.UI.Image hitImage && hitImage.sprite == null && hitImage.GetComponent<UniformUiSurface>()==null) hit.color = new Color(1,1,1,.001f);
            if (hit.color.a == 0) { var color = hit.color; color.a = .001f; hit.color = color; }
            hit.enabled = true;
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
            var button = target.GetComponent<UnityEngine.UI.Button>() ?? target.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.interactable = true;
            button.onClick.AddListener(() => { LastClickedButton = target; action(); });
            if(Application.isPlaying&&target.GetComponent<ImportedPressFeedback>()==null)target.AddComponent<ImportedPressFeedback>();
            BoundButtonCount++;
        }
        TMP_InputField AddInput(GameObject root, string containerId, string textId, bool password)
        {
            FigmaNode container = null, source = null;
            foreach (var node in root.GetComponentsInChildren<FigmaNode>(true))
            {
                if(node.nodeId==containerId)container=node;
                if(node.nodeId==textId && node.gameObject.name!="Editable text")source=node;
            }
            if (container == null || source == null) return null;
            var field = container.GetComponent<TMP_InputField>();
            if (field == null)
            {
                var placeholder = source.GetComponent<TMP_Text>();
                var textObject = new GameObject("Editable text",typeof(RectTransform),typeof(TextMeshProUGUI));
                textObject.transform.SetParent(container.transform,false);
                var text = textObject.GetComponent<TMP_Text>(); text.font=placeholder.font; text.color=placeholder.color; text.text = ""; text.raycastTarget = false;
                var rect = text.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(34,0); rect.offsetMax = new Vector2(-40,0);
                field = container.gameObject.AddComponent<TMP_InputField>(); field.textComponent = text; field.textViewport = container.rectTransform; field.placeholder = placeholder;
                field.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
                field.characterLimit = password ? 64 : 32; field.text = "";
            }
            var copiedNode=field.textComponent.GetComponent<FigmaNode>();
            if(copiedNode!=null)
            {
                if(Application.isPlaying)Destroy(copiedNode);else DestroyImmediate(copiedNode);
            }
            // TMP needs its own text viewport. The imported placeholder may be nested beside an icon.
            var viewport=container.transform.Find("Input text viewport") as RectTransform;
            if(viewport==null)
            {
                viewport=new GameObject("Input text viewport",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();
                viewport.SetParent(container.transform,false);
            }
            viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;viewport.offsetMin=new Vector2(46,0);viewport.offsetMax=new Vector2(-44,0);
            field.textViewport=viewport;field.lineType=TMP_InputField.LineType.SingleLine;
            foreach(var text in new[]{field.textComponent,source.GetComponent<TMP_Text>()})
            {
                text.transform.SetParent(viewport,false);
                text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
                text.rectTransform.offsetMin=text.rectTransform.offsetMax=Vector2.zero;
                text.alignment=TextAlignmentOptions.Left;text.margin=Vector4.zero;text.fontSize=15;
                text.characterSpacing=0;text.wordSpacing=0;text.lineSpacing=0;text.fontStyle=FontStyles.Normal;
                text.transform.localScale=Vector3.one;text.rectTransform.pivot=new Vector2(.5f,.5f);
                text.richText=false;text.overflowMode=TextOverflowModes.Overflow;
                text.enableAutoSizing=false;text.textWrappingMode=TextWrappingModes.NoWrap;text.raycastTarget=false;
            }
            var placeholderText=source.GetComponent<TMP_Text>();
            placeholderText.text=password?"Nhập mật khẩu":"NV-00000 hoặc admin";
            field.placeholder=placeholderText;
            foreach(var node in container.GetComponentsInChildren<FigmaNode>(true))
            {
                bool left=node.nodeName=="user" || node.nodeName=="input-left";
                bool eye=node.nodeId=="5:564";
                if(!left && !eye)continue;
                var icon=node.rectTransform;
                icon.anchorMin=icon.anchorMax=icon.pivot=new Vector2(eye?1:0,.5f);
                icon.anchoredPosition=new Vector2(eye?-16:16,0);icon.sizeDelta=new Vector2(eye?20:18,eye?20:18);
            }
            var inputHit=viewport.GetComponent<UnityEngine.UI.Image>() ?? viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            inputHit.color=new Color(1,1,1,.001f);inputHit.enabled=true;inputHit.raycastTarget=true;
            inputHit.canvasRenderer.cullTransparentMesh=false;
            field.targetGraphic=inputHit;
            field.enabled=true;field.interactable=true;field.readOnly=false;
            field.customCaretColor=true;field.caretColor=new Color(.08f,.25f,.52f);field.caretWidth=2;
            field.selectionColor=new Color(.17f,.43f,.9f,.22f);
            field.resetOnDeActivation=false;field.restoreOriginalTextOnEscape=false;
            field.shouldHideMobileInput=false;
            var caretRepair=container.GetComponent<ImportedInputCaretRepair>()??container.gameObject.AddComponent<ImportedInputCaretRepair>();caretRepair.Configure(field);
            return field;
        }
        public void Notice(string message,bool error=false,float duration=6)
        {
            snackbar=snackbar??gameObject.AddComponent<ImportedSnackbar>();
            var root=lookup[CurrentScreen];var sample=root.GetComponentInChildren<TMP_Text>(true);
            snackbar.Show(root.transform.parent,sample.font,message,error,BottomNavigation!=null&&BottomNavigation.activeSelf,duration);
        }
    }
}




