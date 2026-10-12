using System;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    public sealed class VoiceMessageView : MonoBehaviour
    {
        public VoiceMessage Message { get; private set; }
        public UnityEngine.UI.Button PlayButton { get; private set; }
        public UnityEngine.UI.Button RetryButton { get; private set; }
        public UnityEngine.UI.Button TranscriptButton { get; private set; }
        VoicePlayback playback;
        VoiceWaveform waveform;
        ChatActionIcon playIcon, detailsIcon, retryIcon;
        UnityEngine.UI.Image playFace;
        TMP_Text duration, state, transcript;
        RectTransform bubble;
        LayoutElement sizing;
        bool expanded;
        float nextRefresh;
        string lastCaption,lastTranscript;
        float lastScale=-1;
        bool lastExpanded;
        int lastSecond = -1;

        public void Configure(VoiceMessage message, VoicePlayback player, TMP_FontAsset font, Action<VoiceMessage> retry)
        {
            Message = message; playback = player;
            sizing = gameObject.AddComponent<LayoutElement>();
            bubble = VoiceUi.Rect("Voice bubble",transform,0,0,288,72);
            bubble.anchorMin = bubble.anchorMax = bubble.pivot = new Vector2(1,1);
            var surface = bubble.gameObject.AddComponent<UnityEngine.UI.Image>(); surface.raycastTarget = false;
            UniformUiSurface.Use(surface,18,PrototypeUi.Hex("f2f7ff"),PrototypeUi.Hex("d9e5f5"),.5f);
            PlayButton = VoiceUi.Action(bubble,"Play or pause recording",ChatActionIcon.Symbol.Play,()=>playback.Toggle(Message));
            PlaceAction(PlayButton,8);
            playIcon = PlayButton.GetComponentInChildren<ChatActionIcon>();
            playFace = VoiceUi.CircularFace(PlayButton,PrototypeUi.Accent);
            waveform = VoiceUi.Rect("Recorded waveform",bubble,62,17,126,24).gameObject.AddComponent<VoiceWaveform>();
            duration = VoiceUi.Label(bubble,font,"Playback duration","",12);
            duration.rectTransform.anchoredPosition = new Vector2(198,-19); duration.rectTransform.sizeDelta = new Vector2(44,22);
            duration.alignment = TextAlignmentOptions.MidlineLeft;
            state = VoiceUi.Label(bubble,font,"Voice delivery status","",11);
            state.rectTransform.anchoredPosition = new Vector2(62,-47); state.rectTransform.sizeDelta = new Vector2(208,20);
            RetryButton = VoiceUi.Action(bubble,"Retry this voice message",ChatActionIcon.Symbol.Retry,()=>retry(Message));
            PlaceAction(RetryButton,240); retryIcon = RetryButton.GetComponentInChildren<ChatActionIcon>();
            TranscriptButton = VoiceUi.Action(bubble,"Show or hide transcript",ChatActionIcon.Symbol.Transcript,()=>{expanded=!expanded;Refresh();});
            PlaceAction(TranscriptButton,240); detailsIcon = TranscriptButton.GetComponentInChildren<ChatActionIcon>();
            transcript = VoiceUi.Label(bubble,font,"Optional voice transcript","",15);
            transcript.rectTransform.anchoredPosition = new Vector2(14,-76);
            Refresh();
        }
        static void PlaceAction(UnityEngine.UI.Button button,float left)
        {
            var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(left,-6);rect.sizeDelta=new Vector2(44,44);
            var icon=button.GetComponentInChildren<ChatActionIcon>();icon.rectTransform.anchoredPosition=new Vector2(13,-13);icon.rectTransform.sizeDelta=new Vector2(18,18);
        }
        void Update()
        {
            if(Message==null||Time.unscaledTime<nextRefresh)return;
            nextRefresh=Time.unscaledTime+.1f;Refresh();
        }
        public void Refresh()
        {
            if(Message==null)return;
            bool english=ImportedAppLocalization.IsEnglish;
            bool dark=GetComponentInParent<ImportedFigmaNavigation>()?.Settings?.Dark==true;
            var accent=dark?PrototypeUi.Hex("85b4ff"):PrototypeUi.Accent;
            var ink=dark?PrototypeUi.Hex("dce8fa"):PrototypeUi.Hex("243c5c");
            var muted=dark?PrototypeUi.Hex("9eafc9"):PrototypeUi.Hex("526987");
            waveform.color=accent;duration.color=ink;transcript.color=ink;
            playFace.color=PrototypeUi.Accent;playIcon.color=Color.white;
            detailsIcon.color=expanded?accent:muted;retryIcon.color=accent;
            state.color=Message.Status==VoiceMessageStatus.Failed?(dark?PrototypeUi.Hex("ffb09b"):PrototypeUi.Hex("a73e28")):muted;
            bool active=playback.Current==Message;
            float position=active?playback.Position:0;
            waveform.Show(Message.Waveform,position/Message.Duration);
            playIcon.Set(active&&playback.Playing?ChatActionIcon.Symbol.Pause:ChatActionIcon.Symbol.Play);
            int second=Mathf.FloorToInt(active?position:Message.Duration);
            if(second!=lastSecond){lastSecond=second;duration.text=VoiceAudio.TimeLabel(active?position:Message.Duration);}
            bool retry=Message.Status==VoiceMessageStatus.Failed&&Message.Retryable&&!Message.Disposed;
            RetryButton.gameObject.SetActive(retry);
            var owner=GetComponentInParent<ImportedJarvisChat>();RetryButton.interactable=owner!=null&&!owner.IsSending&&!owner.Media.IsBusy;
            TranscriptButton.gameObject.SetActive(!retry&&!string.IsNullOrWhiteSpace(Message.Transcript));
            transcript.gameObject.SetActive(expanded&&TranscriptButton.gameObject.activeSelf);
            state.text=Message.Status==VoiceMessageStatus.Sending?(english?"Sending…":"Đang gửi…"):
                Message.Status==VoiceMessageStatus.Processing?(english?"JarVis is listening…":"JarVis đang nghe…"):
                Message.Status==VoiceMessageStatus.Completed?(english?"Sent":"Đã gửi"):FailureLabel(Message.Error,english,retry);
            float scale=ImportedSettings.TextScale;
            if(lastCaption==state.text&&lastTranscript==Message.Transcript&&Mathf.Approximately(lastScale,scale)&&lastExpanded==transcript.gameObject.activeSelf)return;
            lastCaption=state.text;lastTranscript=Message.Transcript;lastScale=scale;lastExpanded=transcript.gameObject.activeSelf;
            duration.fontSize=12*scale;state.fontSize=11*scale;transcript.fontSize=15*scale;
            float stateHeight=Mathf.Max(18,state.GetPreferredValues(state.text,208,0).y);
            state.rectTransform.sizeDelta=new Vector2(208,stateHeight);
            float height=Mathf.Max(72,47+stateHeight+8);
            if(transcript.gameObject.activeSelf)
            {
                transcript.text=Message.Transcript;
                float textHeight=transcript.GetPreferredValues(transcript.text,260,0).y;
                transcript.rectTransform.anchoredPosition=new Vector2(14,-(height+4));transcript.rectTransform.sizeDelta=new Vector2(260,textHeight);
                height+=textHeight+18;
            }
            if(!Mathf.Approximately(sizing.preferredHeight,height)){sizing.preferredHeight=sizing.minHeight=height;bubble.sizeDelta=new Vector2(288,height);}
        }
        static string FailureLabel(string error,bool english,bool retry)
        {
            string code=(error??"").Split(':')[0];
            if(code=="SERVER_UPDATE_REQUIRED")return english?"Update server · tap retry":"Cập nhật máy chủ · thử lại";
            if(code=="VOICE_PENDING")return english?"Checking result · tap retry":"Chưa rõ kết quả · thử lại";
            if(code=="TRANSCRIPTION_FAILED"||code=="EMPTY_TRANSCRIPT"||code=="NO_AUDIO")return english?"Speech not recognized · audio kept":"Không nhận dạng được · đã giữ audio";
            if(code=="AI_REQUEST_FAILED"||code=="OPENAI_CONFIGURATION_ERROR")return english?"JarVis error · audio kept":"JarVis gặp lỗi · đã giữ audio";
            if(code=="BUSY"||code=="RATE_LIMIT")return english?"JarVis busy · retry later":"JarVis đang bận · thử lại sau";
            return english?(retry?"Not sent · tap retry":"Not sent · audio kept"):(retry?"Chưa gửi · thử lại":"Chưa gửi · đã giữ bản ghi");
        }
        void OnDestroy(){if(playback!=null&&playback.Current==Message)playback.Stop();Message?.Dispose();}
    }
}
