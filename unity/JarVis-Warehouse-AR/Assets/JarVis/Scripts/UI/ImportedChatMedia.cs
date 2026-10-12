using System;
using System.Collections;
using System.IO;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;

namespace JarVis.Warehouse.UI
{
    // Media stays in memory; microphone/photo access happens only after an explicit tap.
    public sealed class ImportedChatMedia : MonoBehaviour
    {
        public byte[] Photo { get; private set; }
        public bool IsRecording => Recorder != null && Recorder.State == VoiceRecorderState.Recording;
        public bool VoiceMode => Recorder != null && Recorder.State != VoiceRecorderState.Idle;
        public bool IsBusy => VoiceMode || transcribing || picking || permissionPending;
        public VoiceRecorder Recorder { get; private set; }
        public VoicePlayback Playback { get; private set; }
        public VoiceRecordingBar RecordingBar { get; private set; }
        public enum VoiceState { Idle, Recording, Stopped, Processing, Uploading, WaitingForAI, Completed, Error }
        public VoiceState State { get; private set; }=VoiceState.Idle;
        public bool SpeakerEnabled { get; private set; }
        public UnityEngine.UI.Button PhotoButton { get; private set; }
        public UnityEngine.UI.Button MicButton { get; private set; }
        public UnityEngine.UI.Button ListenButton { get; private set; }
        public UnityEngine.UI.Button ExpandButton { get; private set; }
        ChatActionIcon micIcon,listenIcon;
        ImportedJarvisChat chat;
        TMP_Text micLabel, listenLabel;
        UnityEngine.UI.Button remove;
        UnityEngine.UI.RawImage thumbnail;
        Texture2D selectedTexture;
        AudioSource speaker;
        Coroutine transcription, speech;
        OmsMobileApi.VoiceUpload voiceUpload;
        OmsMobileApi.SpeechDownload speechDownload;
        int generation;
        string lastAnswer;
        VoiceMessage activeVoice;
        bool transcribing, picking, permissionPending;
        GameObject plusMenu;
        UnityEngine.UI.Button[] menuButtons;
        ChatActionIcon[] menuIcons;
        float composerY=738;
        public bool PlusMenuOpen=>plusMenu!=null&&plusMenu.activeSelf;
        public void TogglePlusMenu()
        {
            if(PlusMenuOpen){ClosePlusMenu();return;}
            if(plusMenu!=null){Destroy(plusMenu);plusMenu=null;}
            var navigation=chat.GetComponentInParent<ImportedFigmaNavigation>();bool dark=navigation.Settings.Dark;
            var menu=PrototypeUi.Panel(ExpandButton.transform.parent.parent,"JarVis plus menu",16,composerY-92,208,80,dark,20,null,null,true);plusMenu=menu.gameObject;
            menuButtons=new UnityEngine.UI.Button[3];menuIcons=new ChatActionIcon[3];
            void Item(string name,int index,ChatActionIcon.Symbol symbol,Action clicked)
            {
                var tile=PrototypeUi.Panel(menu,name,12+index*64,12,56,56,dark,14,dark?"293a51":"f1f6fc","00000000");
                var icon=Rect("Action icon",tile,16,16,24,24).gameObject.AddComponent<ChatActionIcon>();icon.symbol=symbol;icon.raycastTarget=false;icon.gameObject.AddComponent<UiColourOwner>();
                var button=PrototypeUi.Button(tile,()=>{ClosePlusMenu();clicked();});
                menuButtons[index]=button;menuIcons[index]=icon;
            }
            Item("Choose photo",0,ChatActionIcon.Symbol.Photo,SelectPhoto);Item("Record voice",1,ChatActionIcon.Symbol.Microphone,ToggleRecording);Item("Listen to reply",2,ChatActionIcon.Symbol.Speaker,ToggleSpeech);
            plusMenu.transform.SetAsLastSibling();Refresh();chat.RefreshComposer();
        }
        public void ClosePlusMenu(){if(plusMenu!=null)plusMenu.SetActive(false);chat?.RefreshComposer();}

        public void Configure(ImportedJarvisChat owner, Transform root, TMP_FontAsset font)
        {
            chat=owner;
            PhotoButton=IconButton(root,font,"Chọn ảnh",0,ChatActionIcon.Symbol.Photo,out _);PhotoButton.onClick.AddListener(SelectPhoto);
            MicButton=IconButton(root,font,"Ghi âm",52,ChatActionIcon.Symbol.Microphone,out micLabel);MicButton.onClick.AddListener(ToggleRecording);
            ListenButton=IconButton(root,font,"Nghe",104,ChatActionIcon.Symbol.Speaker,out listenLabel);ListenButton.onClick.AddListener(ToggleSpeech);
            micIcon=MicButton.GetComponentInChildren<ChatActionIcon>();listenIcon=ListenButton.GetComponentInChildren<ChatActionIcon>();
            ExpandButton=IconButton(root,font,"Mở chức năng chat",0,ChatActionIcon.Symbol.Plus,out _);
            ExpandButton.onClick.AddListener(()=>chat.ExpandMediaActions());ExpandButton.gameObject.SetActive(false);
            remove=IconButton(root,font,"Bỏ ảnh",0,ChatActionIcon.Symbol.Close,out var removeLabel);remove.onClick.AddListener(ClearPhoto);
            remove.transform.SetParent(root.parent,false);var removeRect=(RectTransform)remove.transform;removeRect.anchoredPosition=new Vector2(16,-682);removeRect.sizeDelta=new Vector2(200,48);
            var removeBackground=remove.GetComponent<UnityEngine.UI.Image>();removeBackground.sprite=ImportedUiQuality.RoundedCard(12);removeBackground.type=UnityEngine.UI.Image.Type.Sliced;removeBackground.color=new Color(.91f,.95f,1);
            remove.GetComponentInChildren<ChatActionIcon>().rectTransform.anchoredPosition=new Vector2(168,-12);
            var preview=Rect("Attached photo preview",remove.transform,4,4,36,36);
            thumbnail=preview.gameObject.AddComponent<UnityEngine.UI.RawImage>();thumbnail.raycastTarget=false;
            var label=removeLabel;label.gameObject.SetActive(true);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=new Vector2(46,0);label.rectTransform.offsetMax=new Vector2(-40,0);label.fontSize=12;label.alignment=TextAlignmentOptions.MidlineLeft;
            remove.gameObject.SetActive(false);
            speaker=gameObject.AddComponent<AudioSource>();speaker.playOnAwake=false;Refresh();
            SpeakerEnabled=PlayerPrefs.GetInt("JarVis.SpeakerEnabled",0)==1;
            Recorder=gameObject.AddComponent<VoiceRecorder>();
            Recorder.Failed+=message=>SetState(VoiceState.Error,message);
            Playback=gameObject.AddComponent<VoicePlayback>();Playback.Starting+=StopSpeech;Playback.Failed+=message=>chat.MediaStatus(message);
            RecordingBar=new GameObject("Voice recording composer",typeof(RectTransform)).AddComponent<VoiceRecordingBar>();
            RecordingBar.Configure(root,font,Recorder,CancelRecording,StopRecording,SendRecording);
        }
        static RectTransform Rect(string name,Transform parent,float x,float y,float width,float height)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(width,height);return r;
        }
        static UnityEngine.UI.Button IconButton(Transform root,TMP_FontAsset font,string text,float x,ChatActionIcon.Symbol symbol,out TMP_Text label)
        {
            var r=Rect(text,root,x,0,48,48);
            var hit=r.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=new Color(1,1,1,.001f);
            var iconRect=Rect("Vector icon",r,12,12,24,24);
            var icon=iconRect.gameObject.AddComponent<ChatActionIcon>();icon.symbol=symbol;icon.raycastTarget=false;
            icon.gameObject.AddComponent<UiColourOwner>();iconRect.anchoredPosition=new Vector2(10,-12);
            var button=r.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=icon;
            var colors=button.colors;colors.normalColor=colors.highlightedColor=new Color(.07f,.3f,.65f);colors.pressedColor=new Color(.02f,.16f,.38f);colors.disabledColor=new Color(.56f,.64f,.74f);button.colors=colors;
            var t=Rect("Label",r,0,0,48,48);label=t.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=13;label.color=new Color(.07f,.3f,.65f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.richText=false;label.gameObject.SetActive(false);
            return button;
        }
        public void ShowCollapsed(bool collapsed)
        {
            PhotoButton.gameObject.SetActive(!VoiceMode&&!collapsed);MicButton.gameObject.SetActive(!VoiceMode&&!collapsed);ListenButton.gameObject.SetActive(!VoiceMode&&!collapsed);ExpandButton.gameObject.SetActive(!VoiceMode&&collapsed);
            if(RecordingBar!=null)RecordingBar.gameObject.SetActive(VoiceMode);
        }
        public void PositionAttachment(float y){composerY=y;((RectTransform)remove.transform).anchoredPosition=new Vector2(16,-(y-56));if(plusMenu!=null)((RectTransform)plusMenu.transform).anchoredPosition=new Vector2(16,-Mathf.Max(130,y-92));}
        void Update()
        {
            if(IsRecording)
            {
                if(Recorder.Duration>=25)chat.MediaStatus(ImportedAppLocalization.IsEnglish?"Almost 30s · Stop or send your recording":"Sắp đủ 30 giây · Dừng hoặc gửi bản ghi");
            }
            if(Recorder!=null&&Recorder.State==VoiceRecorderState.Stopped&&State==VoiceState.Recording){SetState(VoiceState.Stopped,ImportedAppLocalization.IsEnglish?"Recording stopped · Send or delete":"Đã dừng ghi âm · Gửi hoặc xóa bản ghi");}
            if(speaker!=null&&speech==null&&!speaker.isPlaying&&speaker.clip!=null){Destroy(speaker.clip);speaker.clip=null;}
            if(micIcon!=null)micIcon.Set(IsRecording?ChatActionIcon.Symbol.Stop:ChatActionIcon.Symbol.Microphone);
            if(listenIcon!=null)listenIcon.Set(SpeakerEnabled?ChatActionIcon.Symbol.Speaker:ChatActionIcon.Symbol.SpeakerOff);
            if(IsRecording&&micIcon!=null)micIcon.color=Color.Lerp(new Color(.85f,.16f,.22f),new Color(1,.4f,.4f),.5f+.5f*Mathf.Sin(Time.unscaledTime*5));
            if(PlusMenuOpen)RefreshMenu();
        }
        public void Refresh()
        {
            if(chat==null||ExpandButton==null)return;
            var plusColors=ExpandButton.colors;
            bool dark=chat.GetComponentInParent<ImportedFigmaNavigation>()?.Settings?.Dark==true;
            plusColors.normalColor=plusColors.highlightedColor=dark?PrototypeUi.Hex("85b4ff"):PrototypeUi.Accent;
            ExpandButton.colors=plusColors;
            bool ready=OmsMobileSession.IsReady&&!chat.IsSending;
            PhotoButton.interactable=ready&&!IsBusy;
            MicButton.interactable=ready&&!IsBusy;
            // Preference remains controllable during a request, including before the first reply.
            ListenButton.interactable=OmsMobileSession.IsReady&&!IsRecording;
            bool darkTheme=dark;
            foreach(var button in new[]{PhotoButton,MicButton,ListenButton}){var colors=button.colors;colors.normalColor=colors.highlightedColor=darkTheme?PrototypeUi.Hex("85b4ff"):PrototypeUi.Accent;button.colors=colors;}
            remove.interactable=!chat.IsSending&&!IsBusy;
            if(!IsRecording)micLabel.text=transcribing?"Đang nhận…":"Ghi âm";
            RefreshMenu();
        }
        void RefreshMenu()
        {
            if(!PlusMenuOpen||menuButtons==null)return;
            bool dark=chat.GetComponentInParent<ImportedFigmaNavigation>().Settings.Dark;
            bool[] enabled={PhotoButton.interactable,MicButton.interactable,ListenButton.interactable};
            for(int i=0;i<3;i++){menuButtons[i].interactable=enabled[i];menuIcons[i].color=enabled[i]?(dark?PrototypeUi.Hex("85b4ff"):PrototypeUi.Accent):PrototypeUi.Muted(dark);}
            menuIcons[1].Set(IsRecording?ChatActionIcon.Symbol.Stop:ChatActionIcon.Symbol.Microphone);
            menuIcons[2].Set(SpeakerEnabled?ChatActionIcon.Symbol.Speaker:ChatActionIcon.Symbol.SpeakerOff);
        }
        public void SetAnswer(string text){lastAnswer=text;Refresh();if(SpeakerEnabled&&!string.IsNullOrWhiteSpace(text)){StopSpeech();speech=StartCoroutine(ReadAnswer());}}
        public void ClearPhoto()
        {
            bool hadPhoto=Photo!=null;
            Photo=null;thumbnail.texture=null;
            if(selectedTexture!=null)Destroy(selectedTexture);selectedTexture=null;
            remove.gameObject.SetActive(false);if(hadPhoto&&!chat.IsSending)chat.MediaStatus("Sẵn sàng nhận câu hỏi");chat.RefreshComposer();
        }
        public void SelectPhoto()
        {
            if(IsBusy||chat.IsSending||!OmsMobileSession.IsReady)return;
#if UNITY_EDITOR
            string path=UnityEditor.EditorUtility.OpenFilePanel("Chọn ảnh gửi JarVis (JPG/PNG)","","jpg,jpeg,png");
            if(!string.IsNullOrEmpty(path))LoadPhoto(path);
#elif UNITY_ANDROID
            picking=true;Refresh();chat.RefreshComposer();
            try {using(var bridge=new AndroidJavaClass("com.jarvis.warehouse.ChatPhotoPicker"))bridge.CallStatic("pick",gameObject.name,"OnPhotoPicked",Application.temporaryCachePath);}
            catch(Exception){picking=false;chat.MediaStatus("Không mở được trình chọn ảnh Android");Refresh();}
#else
            chat.MediaStatus("Chọn ảnh hiện hỗ trợ Android và Unity Editor");
#endif
        }
        // Android callback contains only a temporary, downscaled JPEG file path, never UI instructions.
        public void OnPhotoPicked(string path)
        {
            picking=false;
            if(!isActiveAndEnabled){if(path.StartsWith(Application.temporaryCachePath,StringComparison.Ordinal)&&File.Exists(path))File.Delete(path);return;}
            if(path=="ERROR")chat.MediaStatus("Không đọc được ảnh. Hãy chọn JPG/PNG khác.");
            else if(!string.IsNullOrEmpty(path))
            {
                // Native plugin produces only a file in this app's cache, not an arbitrary path.
                if(path.StartsWith(Application.temporaryCachePath+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                {LoadPhoto(path);if(File.Exists(path))File.Delete(path);}
                else chat.MediaStatus("Đường dẫn ảnh không hợp lệ");
            }
            Refresh();chat.RefreshComposer();
        }
        public void LoadPhoto(string path)
        {
            Texture2D source=null, scaled=null;RenderTexture target=null;
            var previous=RenderTexture.active;
            try
            {
                if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException();
                source=new Texture2D(2,2,TextureFormat.RGB24,false);
                if(!ImageConversion.LoadImage(source,File.ReadAllBytes(path)))throw new InvalidDataException();
                float factor=Mathf.Min(1,1024f/Mathf.Max(source.width,source.height));
                int width=Mathf.Max(1,Mathf.RoundToInt(source.width*factor)),height=Mathf.Max(1,Mathf.RoundToInt(source.height*factor));
                target=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
                Graphics.Blit(source,target);RenderTexture.active=target;
                scaled=new Texture2D(width,height,TextureFormat.RGB24,false);scaled.ReadPixels(new Rect(0,0,width,height),0,0);scaled.Apply();
                var bytes=scaled.EncodeToJPG(82);if(bytes.Length>2*1024*1024)throw new InvalidDataException();
                ClearPhoto();Photo=bytes;selectedTexture=scaled;scaled=null;thumbnail.texture=selectedTexture;
                // Use a fitted rect rather than stretching the selected photo.
                var r=thumbnail.rectTransform;float aspect=(float)width/height;r.sizeDelta=aspect>=1?new Vector2(36,36/aspect):new Vector2(36*aspect,36);
                remove.gameObject.SetActive(true);chat.MediaStatus("Ảnh đã đính kèm — nhấn Gửi để hỏi JarVis");
            }
            catch(Exception){chat.MediaStatus("Không đọc được ảnh. Chọn JPG/PNG dưới 8 MB.");}
            finally {RenderTexture.active=previous;if(target!=null)RenderTexture.ReleaseTemporary(target);if(source!=null)Destroy(source);if(scaled!=null)Destroy(scaled);}
            Refresh();chat.RefreshComposer();
        }
        public void ToggleRecording()
        {
            if(IsRecording){StopRecording();return;}
            if(IsBusy||chat.IsSending||!OmsMobileSession.IsReady)return;
            if(Photo!=null){chat.MediaStatus("Gửi hoặc bỏ ảnh đính kèm trước khi hỏi bằng giọng nói");return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                permissionPending=true;Refresh();chat.RefreshComposer();var callbacks=new UnityEngine.Android.PermissionCallbacks();int ticket=generation;
                callbacks.PermissionGranted+=_=>{if(ticket!=generation)return;permissionPending=false;if(isActiveAndEnabled)BeginRecording();};
                callbacks.PermissionDenied+=_=>{if(ticket!=generation)return;permissionPending=false;SetState(VoiceState.Error,"Cần quyền micro để ghi âm");};
                callbacks.PermissionDeniedAndDontAskAgain+=_=>{if(ticket!=generation)return;permissionPending=false;SetState(VoiceState.Error,"Bật quyền micro trong Cài đặt ứng dụng để ghi âm");};
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);return;
            }
#endif
            BeginRecording();
        }
        void BeginRecording()
        {
            if(!isActiveAndEnabled||chat.IsSending||!OmsMobileSession.IsReady)return;
            StopSpeech();Playback.Stop();chat.Input.DeactivateInputField();chat.SetKeyboardMode(false);
            if(Recorder.Begin())SetState(VoiceState.Recording,ImportedAppLocalization.IsEnglish?"Recording · Stop, send or delete":"Đang ghi âm · Dừng, gửi hoặc xóa");
        }
        public void StopRecording()
        {
            if(!IsRecording)return;
            Recorder.Stop();SetState(VoiceState.Stopped,ImportedAppLocalization.IsEnglish?"Recording stopped · Send or delete":"Đã dừng ghi âm · Gửi hoặc xóa bản ghi");
        }
        public void SendRecording()
        {
            if(!isActiveAndEnabled||!VoiceMode||transcribing||chat.IsSending||!OmsMobileSession.IsReady)return;
            var message=Recorder.FinalizeMessage();
            if(message!=null)SubmitVoice(message);
            chat.RefreshComposer();
        }
        public void SubmitRecording(byte[] wav)
        {
            if(!isActiveAndEnabled||IsBusy||chat.IsSending||!OmsMobileSession.IsReady)return;
            if(wav==null||wav.Length>2048044||!HasAudibleSamples(wav)){SetState(VoiceState.Error,"NO_AUDIO: Chưa nghe thấy giọng nói. Hãy thử lại.");return;}
            try{SubmitVoice(VoiceMessage.Create(wav));}catch(Exception){SetState(VoiceState.Error,"Không xử lý được bản ghi âm.");}
        }
        void SubmitVoice(VoiceMessage message)
        {
            StopSpeech();Playback.Stop();chat.AddVoiceMessage(message,Playback,RetryVoice);
            StartUpload(message);
        }
        public void RetryVoice(VoiceMessage message)
        {
            if(!isActiveAndEnabled||IsBusy||chat.IsSending||!OmsMobileSession.IsReady||message==null||message.Disposed||message.Status!=VoiceMessageStatus.Failed||!message.Retryable)return;
            if(DateTime.UtcNow-message.CreatedAt>TimeSpan.FromHours(23)){message.Retryable=false;chat.MediaStatus("Bản ghi đã quá hạn thử lại. Hãy ghi âm mới.");return;}
            StartUpload(message);
        }
        void StartUpload(VoiceMessage message)
        {
            activeVoice=message;message.Status=VoiceMessageStatus.Sending;message.Error=null;message.Retryable=false;
            transcribing=true;chat.SetVoicePending(true);SetState(VoiceState.Uploading,"Đang gửi giọng nói…");
            transcription=StartCoroutine(UploadVoice(message));
        }
        IEnumerator UploadVoice(VoiceMessage message)
        {
            byte[] wav=null;string readError=null;
            try{wav=File.ReadAllBytes(message.AudioPath);}catch(Exception){readError="LOCAL_AUDIO: Không đọc được bản ghi âm";}
            OmsMobileApi.VoiceReply reply=null;string error=null;voiceUpload=new OmsMobileApi.VoiceUpload();
            if(readError==null)yield return voiceUpload.Send(wav,value=>reply=value,value=>error=value,()=>{message.Status=VoiceMessageStatus.Processing;SetState(VoiceState.WaitingForAI,"JarVis đang nghe và trả lời…");},message.Id);
            else error=readError;
            wav=null;
            bool retryAllowed=voiceUpload.RetryAllowed;voiceUpload.Dispose();voiceUpload=null;
            transcribing=false;transcription=null;activeVoice=null;chat.SetVoicePending(false);
            if(reply!=null){message.Status=VoiceMessageStatus.Completed;message.Transcript=reply.transcript;State=VoiceState.Completed;chat.AcceptVoice(reply);}
            else{FailVoice(message,error??"NETWORK_ERROR");message.Retryable&=retryAllowed;SetState(VoiceState.Error,error?.StartsWith("SERVER_UPDATE_REQUIRED",StringComparison.Ordinal)==true?(ImportedAppLocalization.IsEnglish?"Server needs the voice update. Audio has not been sent.":"Cần cập nhật máy chủ voice. Audio chưa được gửi."):(ImportedAppLocalization.IsEnglish?"Voice failed. Recording kept; check connection or server.":"Gửi giọng nói lỗi. Bản ghi được giữ; kiểm tra mạng hoặc máy chủ."));}
            Refresh();chat.RefreshComposer();
        }
        static void FailVoice(VoiceMessage message,string error)
        {
            message.Status=VoiceMessageStatus.Failed;message.Error=error;
            string code=error.Split(':')[0];message.Retryable=code=="NETWORK_ERROR"||code=="TIMEOUT"||code=="BUSY"||code=="RATE_LIMIT"||code=="VOICE_PENDING"||code=="SERVER_UPDATE_REQUIRED";
        }
        public static byte[] EncodeWav(AudioClip clip,int frames)
        {
            frames=Mathf.Clamp(frames,0,clip.samples);var samples=new float[frames*clip.channels];if(!clip.GetData(samples,0))throw new InvalidDataException("Audio samples unavailable");
            using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+frames*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(clip.frequency);writer.Write(clip.frequency*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(frames*2);
                for(int i=0;i<frames;i++){float mono=0;for(int c=0;c<clip.channels;c++)mono+=samples[i*clip.channels+c];writer.Write((short)Mathf.RoundToInt(Mathf.Clamp(mono/clip.channels,-1,1)*32767));}
                return stream.ToArray();
            }
        }
        public static bool HasAudibleSamples(byte[] wav)
        {
            double energy=0;float peak=0;if(wav==null||wav.Length<=44)return false;
            for(int i=44;i+1<wav.Length;i+=2){float sample=BitConverter.ToInt16(wav,i)/32768f;peak=Mathf.Max(peak,Mathf.Abs(sample));energy+=sample*sample;}
            return peak>=.008f&&Math.Sqrt(energy/((wav.Length-44)/2))>=.001;
        }
        void SetState(VoiceState state,string message){State=state;chat?.MediaStatus(message);Refresh();chat?.RefreshComposer();}
        void ToggleSpeech()
        {
            SpeakerEnabled=!SpeakerEnabled;PlayerPrefs.SetInt("JarVis.SpeakerEnabled",SpeakerEnabled?1:0);PlayerPrefs.Save();
            if(!SpeakerEnabled)StopSpeech();
            else if(!IsBusy&&!chat.IsSending&&!string.IsNullOrEmpty(lastAnswer)){StopSpeech();speech=StartCoroutine(ReadAnswer());}
            chat.MediaStatus(SpeakerEnabled?"Loa bật · JarVis sẽ đọc câu trả lời bằng giọng AI":"Loa tắt · chỉ hiển thị câu trả lời chữ");Refresh();
        }
        IEnumerator ReadAnswer()
        {
            Playback?.Stop();
            string error=null;AudioClip clip=null;speechDownload=new OmsMobileApi.SpeechDownload();
            yield return OmsMobileApi.Speak(lastAnswer.Substring(0,Mathf.Min(4000,lastAnswer.Length)),value=>clip=value,value=>error=value,speechDownload);
            speechDownload.Dispose();speechDownload=null;
            speech=null;
                if(clip==null){chat.MediaStatus(error??"Không tải được giọng đọc");listenLabel.text="Nghe";yield break;}
            speaker.clip=clip;speaker.Play();chat.MediaStatus("Đang phát giọng AI của JarVis");
        }
        public void StopSpeech()
        {
            speechDownload?.Dispose();speechDownload=null;
            if(speech!=null){StopCoroutine(speech);speech=null;}
            if(speaker!=null){speaker.Stop();if(speaker.clip!=null)Destroy(speaker.clip);speaker.clip=null;}
            if(listenLabel!=null)listenLabel.text="Nghe";
        }
        public void CancelRecording(){Recorder?.Cancel();if(State==VoiceState.Recording||State==VoiceState.Stopped)State=VoiceState.Idle;Refresh();chat?.RefreshComposer();}
        public void ResetSession(){StopAllMedia();ClearPhoto();lastAnswer=null;Refresh();}
        void StopAllMedia(){generation++;CancelRecording();StopSpeech();Playback?.Stop();voiceUpload?.Dispose();voiceUpload=null;if(transcription!=null){StopCoroutine(transcription);transcription=null;}if(activeVoice!=null){FailVoice(activeVoice,"NETWORK_ERROR: Request interrupted");activeVoice=null;}transcribing=false;chat?.SetVoicePending(false);permissionPending=false;State=VoiceState.Idle;}
        void OnDisable(){if(plusMenu!=null)plusMenu.SetActive(false);StopAllMedia();picking=false;if(chat!=null)chat.RefreshComposer();}
        void OnApplicationPause(bool paused){if(paused){StopAllMedia();chat?.MediaStatus("Đã dừng giọng nói khi ứng dụng tạm nghỉ");chat?.RefreshComposer();}}
        void OnDestroy(){StopAllMedia();if(selectedTexture!=null)Destroy(selectedTexture);}
    }
}
