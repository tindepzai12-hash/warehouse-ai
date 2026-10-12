using System;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    public sealed class VoiceRecordingBar : MonoBehaviour
    {
        public UnityEngine.UI.Button CancelButton { get; private set; }
        public UnityEngine.UI.Button StopButton { get; private set; }
        public UnityEngine.UI.Button SendButton { get; private set; }
        VoiceRecorder recorder;
        VoiceWaveform waveform;
        TMP_Text timer;
        ChatActionIcon cancelIcon,stopIcon,sendIcon;
        UnityEngine.UI.Image sendFace;
        float nextRefresh;
        int lastSecond = -1;
        public void Configure(Transform composer, TMP_FontAsset font, VoiceRecorder source, Action cancel, Action stop, Action send)
        {
            recorder = source;
            var root = (RectTransform)transform;
            root.SetParent(composer, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var surface = gameObject.AddComponent<UnityEngine.UI.Image>(); surface.raycastTarget = true;
            UniformUiSurface.Use(surface,24,Color.white,PrototypeUi.Hex("d9e5f5"),.5f);
            var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4,4,0,0); layout.spacing = 4;
            layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            CancelButton = VoiceUi.Action(root, "Delete recording", ChatActionIcon.Symbol.Trash, cancel);
            StopButton = VoiceUi.Action(root, "Stop recording", ChatActionIcon.Symbol.Stop, stop);
            var waveRect = VoiceUi.Rect("Microphone amplitude", root, 0, 0, 80, 22);
            waveform = waveRect.gameObject.AddComponent<VoiceWaveform>();
            var waveSize = waveRect.gameObject.AddComponent<LayoutElement>(); waveSize.minWidth = 40; waveSize.flexibleWidth = 1;
            timer = VoiceUi.Label(root, font, "Duration", "00:00", 13);
            timer.rectTransform.sizeDelta = new Vector2(48,48); timer.alignment = TextAlignmentOptions.Center;
            var timerSize = timer.gameObject.AddComponent<LayoutElement>(); timerSize.preferredWidth = 48;
            SendButton = VoiceUi.Action(root, "Send voice", ChatActionIcon.Symbol.Send, send);
            cancelIcon=CancelButton.GetComponentInChildren<ChatActionIcon>();stopIcon=StopButton.GetComponentInChildren<ChatActionIcon>();sendIcon=SendButton.GetComponentInChildren<ChatActionIcon>();
            foreach(var icon in new[]{cancelIcon,stopIcon,sendIcon}){icon.rectTransform.anchoredPosition=new Vector2(12,-14);icon.rectTransform.sizeDelta=new Vector2(20,20);}
            sendFace=VoiceUi.CircularFace(SendButton,PrototypeUi.Accent);
            gameObject.SetActive(false);
        }
        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            bool dark = GetComponentInParent<ImportedFigmaNavigation>()?.Settings?.Dark == true;
            var ink = dark ? PrototypeUi.Hex("85b4ff") : PrototypeUi.Accent;
            var muted=dark?PrototypeUi.Hex("9eafc9"):PrototypeUi.Hex("526987");
            waveform.color = ink; waveform.Show(recorder.Levels, 1);
            timer.color = recorder.Duration >= 25 ? (dark?PrototypeUi.Hex("ffba80"):PrototypeUi.Hex("a74b12")) : (dark?PrototypeUi.Hex("dce8fa"):PrototypeUi.Hex("243c5c"));
            cancelIcon.color=muted;stopIcon.color=recorder.State==VoiceRecorderState.Recording?muted:new Color(muted.r,muted.g,muted.b,.45f);
            sendIcon.color=Color.white;sendFace.color=PrototypeUi.Accent;
            StopButton.interactable = recorder.State == VoiceRecorderState.Recording;
            SendButton.interactable = recorder.Duration >= .25f && recorder.State != VoiceRecorderState.Finalizing;
            if(!SendButton.interactable)sendFace.color=new Color(PrototypeUi.Accent.r,PrototypeUi.Accent.g,PrototypeUi.Accent.b,.45f);
            int second = Mathf.FloorToInt(recorder.Duration);
            if (second != lastSecond) { lastSecond = second; timer.text = VoiceAudio.TimeLabel(recorder.Duration); }
            timer.fontSize=13*ImportedSettings.TextScale;
        }
        void OnEnable() { lastSecond = -1; nextRefresh = 0; }
    }

    public static class VoiceUi
    {
        public static UnityEngine.UI.Image CircularFace(UnityEngine.UI.Button button,Color color)
        {
            var rect=Rect("Voice control face",button.transform,4,4,36,36);rect.SetAsFirstSibling();
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
            var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=ImportedUiQuality.RoundedCard(18);image.type=UnityEngine.UI.Image.Type.Sliced;image.color=color;image.raycastTarget=false;
            image.gameObject.AddComponent<UiColourOwner>();return image;
        }
        public static RectTransform Rect(string name, Transform parent, float left, float top, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(left,-top); rect.sizeDelta = new Vector2(width,height);
            return rect;
        }
        public static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string value, float size)
        {
            var rect = Rect(name,parent,0,0,288,24);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.text = value; label.fontSize = size; label.richText = false;
            label.textWrappingMode = TextWrappingModes.Normal; label.raycastTarget = false;
            label.margin = Vector4.zero; label.characterSpacing = label.wordSpacing = 0;
            return label;
        }
        public static UnityEngine.UI.Button Action(Transform parent, string name, ChatActionIcon.Symbol symbol, Action action)
        {
            var rect = Rect(name,parent,0,0,44,48);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = new Color(1,1,1,.001f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.transition = Selectable.Transition.None; button.onClick.AddListener(() => action());
            var size = rect.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = size.minWidth = 44;
            var icon = Rect("Voice action",rect,10,12,24,24).gameObject.AddComponent<ChatActionIcon>();
            icon.symbol = symbol; icon.raycastTarget = false; icon.color = PrototypeUi.Accent;
            icon.gameObject.AddComponent<UiColourOwner>();
            return button;
        }
    }
}
