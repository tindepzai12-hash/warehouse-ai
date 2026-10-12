using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class VoiceWorkflowReview
    {
        static VoiceWorkflowReview(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("VoiceWorkflowReview",false)){SessionState.SetBool("VoiceWorkflowReview",false);new GameObject("Voice workflow QA").AddComponent<VoiceWorkflowRunner>();}};}
        public static void Run(){JarvisProjectValidator.Validate();EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("VoiceWorkflowReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class VoiceWorkflowRunner:MonoBehaviour
    {
        readonly List<string> checks=new List<string>(),errors=new List<string>();
        void Check(bool value,string name){checks.Add((value?"PASS ":"FAIL ")+name);if(!value)errors.Add(name);}
        void Log(string text,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(text.Split('\n')[0]);}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var locale=nav.GetComponent<ImportedAppLocalization>();var oldLanguage=JarvisLocalization.CurrentLanguage;string oldTheme=nav.Settings.Theme;
            int preference=PlayerPrefs.GetInt("JarVis.SpeakerEnabled",0);
            var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();var media=chat.Media;
            // UI-only navigation fixture: do not log employee hours or alter the live attendance record.
            nav.Presence.enabled=false;nav.Presence.StopAllCoroutines();typeof(ImportedPresence).GetProperty("State").SetValue(nav.Presence,PresenceState.Online);
            checks.Add("INFO Online navigation gate uses Editor fixture; no presence/time data mutation.");
            OmsMobileSession.Clear();string endpoint;OmsMobileSession.TryLoadEndpoint(out endpoint);
            var credentials=File.ReadAllText("E:/warehouse-ai/data/oms-admin-credentials.txt");string Field(string key)=>Regex.Match(credentials,"(?m)^"+key+":\\s*(.+)$").Groups[1].Value.Trim();
            OmsMobileApi.LoginReply session=null;string error=null;
            yield return OmsMobileApi.Login(endpoint,Field("Username"),Field("Password"),s=>session=s,e=>error=e);
            Check(session!=null,"Actual UnityWebRequest login on unchanged live database");if(session==null){Finish();yield break;}
            OmsMobileSession.Set(endpoint,session.accessToken,session.user.employeeCode,session.expiresAt);
            foreach(string theme in new[]{"light","dark"})foreach(var lang in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
            {
                nav.Settings.SelectTheme(theme);locale.Select(lang);nav.Show("5:1216",false);yield return null;yield return null;Canvas.ForceUpdateCanvases();Check(nav.CurrentScreen=="5:1216","Actual chat screen active");
                var buttons=new[]{media.PhotoButton,media.MicButton,media.ListenButton,chat.SendButton};
                var row=media.PhotoButton.transform.parent.GetComponent<HorizontalLayoutGroup>();Check(row!=null,theme+lang+" composer owns horizontal layout");
                float last=-10000;
                foreach(var control in new Transform[]{media.PhotoButton.transform,media.MicButton.transform,media.ListenButton.transform,chat.Input.transform,chat.SendButton.transform})
                {
                    var r=(RectTransform)control;Check(r.rect.width>=44&&r.rect.height>=44,"44px target "+control.name);Check(r.anchoredPosition.x>last,"No overlapping composer children "+control.name);last=r.anchoredPosition.x;
                }
                Check(!media.ExpandButton.gameObject.activeSelf&&buttons.All(b=>b.gameObject.activeSelf),"Compact inline icons without large menu");
                Check(chat.SendButton.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Arrow ")).All(i=>i.color==Color.white),"Readable white send arrow");
                if(media.SpeakerEnabled)media.ListenButton.onClick.Invoke();media.ListenButton.onClick.Invoke();Check(media.SpeakerEnabled,"Speaker ON before first reply");media.ListenButton.onClick.Invoke();Check(!media.SpeakerEnabled,"Speaker OFF remains off");
                Directory.CreateDirectory("Logs/PrototypeParity");PrototypeParityRunner.Capture(nav,"Voice-composer-"+theme+"-"+lang+".png",824,1830);PrototypeParityRunner.Capture(nav,"Voice-composer-"+theme+"-"+lang+"-alternate.png",780,1688);
            }
            locale.Select(SupportedLanguage.Vi);nav.Settings.SelectTheme("light");nav.Show("5:1216",false);yield return null;
            var stereo=AudioClip.Create("Trimmed stereo QA fixture",32000,2,16000,false);var samples=new float[64000];for(int i=0;i<32000;i++){samples[i*2]=Mathf.Sin(i*.1f)*.4f;samples[i*2+1]=samples[i*2];}stereo.SetData(samples,0);
            var wav=ImportedChatMedia.EncodeWav(stereo,16000);Check(wav.Length==32044&&BitConverter.ToInt16(wav,22)==1&&BitConverter.ToInt16(wav,34)==16&&BitConverter.ToInt32(wav,40)==32000,"Trimmed mono PCM16 WAV header and length");Destroy(stereo);
            var silence=AudioClip.Create("Silence fixture",16000,1,16000,false);var silent=ImportedChatMedia.EncodeWav(silence,16000);Check(!ImportedChatMedia.HasAudibleSamples(silent),"Silence detection prevents AI calls");Destroy(silence);
            chat.Input.text="Draft stays";int before=chat.MessageCount;media.SubmitRecording(silent);Check(media.State==ImportedChatMedia.VoiceState.Error&&!media.IsBusy&&chat.MessageCount==before&&chat.Input.text=="Draft stays","Silence error preserves draft and feed");
            media.LoadPhoto("Assets/Resources/Prototype/JarVis.png");Check(media.Photo!=null&&chat.SendButton.interactable,"Image attachment and image-only send remain available");media.ClearPhoto();chat.Input.text="";
            // Real transport cancellation with a deliberately invalid RIFF signature:
            // server byte validation cannot forward it to a billable provider even if abort races.
            var cancelledWav=(byte[])wav.Clone();cancelledWav[0]=0;
            media.SubmitRecording(cancelledWav);Check(media.IsBusy&&!chat.SendButton.interactable,"Voice blocks duplicate text and media submissions");nav.Show("5:36",false);yield return null;Check(!media.IsBusy&&media.State==ImportedChatMedia.VoiceState.Idle,"Leaving screen aborts voice request");nav.Show("5:1216",false);yield return null;
            Check(chat.CanChat&&OmsMobileSession.AccessToken==session.accessToken,"Chat and login retain one session");
            var upload=new OmsMobileApi.VoiceUpload();OmsMobileApi.VoiceReply failedReply=null;error=null;yield return upload.Send(new byte[0],r=>failedReply=r,e=>error=e,()=>{});Check(failedReply==null&&error!=null&&error.StartsWith("NO_AUDIO"),"Empty upload is a recoverable client error");
            error=null;yield return upload.Send(new byte[44],r=>failedReply=r,e=>error=e,()=>{});Check(failedReply==null&&error!=null&&error.StartsWith("INVALID_AUDIO"),"Unity parses sanitized server voice error");
            bool live=File.Exists("Library/JarVisVerifyLiveVoice.request");
            if(live)
            {
                File.Delete("Library/JarVisVerifyLiveVoice.request");
                chat.Input.text="Xin chào JarVis, tôi tên Justin. Trả lời bằng một câu ngắn.";chat.Input.onSubmit.Invoke(chat.Input.text);
                float deadline=Time.realtimeSinceStartup+80;while(chat.IsSending&&Time.realtimeSinceStartup<deadline)yield return null;
                Check(!chat.IsSending&&!string.IsNullOrEmpty(chat.LastReplyText)&&chat.LastReplyLanguage=="vi","Actual typed Enter send through OpenAI");
                foreach(string question in new[]{"What is five S in a warehouse? Answer briefly.","Tên tôi là gì? Trả lời thật ngắn."})
                {
                    AudioClip audio=null;error=null;yield return OmsMobileApi.Speak(question,c=>audio=c,e=>error=e);
                    Check(audio!=null,"Actual OpenAI TTS audio fixture generated");if(audio==null)continue;
                    var raw=new float[audio.samples*audio.channels];audio.GetData(raw,0);int frames=Mathf.RoundToInt(audio.length*16000);var mono=new float[frames];for(int i=0;i<frames;i++){int source=Mathf.Min(audio.samples-1,Mathf.FloorToInt(i*audio.frequency/16000f));for(int c=0;c<audio.channels;c++)mono[i]+=raw[source*audio.channels+c]/audio.channels;}
                    var clip=AudioClip.Create("Real provider audio fixture, not microphone",frames,1,16000,false);clip.SetData(mono,0);var voice=ImportedChatMedia.EncodeWav(clip,frames);Destroy(clip);Destroy(audio);
                    before=chat.MessageCount;chat.Input.text="Unsent draft";media.SubmitRecording(voice);media.SubmitRecording(voice);
                    deadline=Time.realtimeSinceStartup+115;while(media.IsBusy&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(media.State==ImportedChatMedia.VoiceState.Completed&&chat.MessageCount==before+2,"Actual WAV → STT → same AI → exactly two chat messages");Check(chat.Input.text=="Unsent draft","Voice preserves independent typed draft");
                    Check(chat.LastReplyLanguage==(question.StartsWith("What")?"en":"vi"),"Voice reply follows question language");if(!question.StartsWith("What"))Check(chat.LastReplyText!=null&&chat.LastReplyText.Contains("Justin"),"Voice recalls prior typed context");
                    yield return null;yield return null;yield return new WaitForSecondsRealtime(.2f);
                    var scroll=chat.GetComponentInChildren<ScrollRect>();Check(scroll!=null&&scroll.verticalNormalizedPosition<.01f,"New voice reply scrolls to bottom after layout settles");
                    PrototypeParityRunner.Capture(nav,"Voice-live-"+chat.LastReplyLanguage+".png",824,1830);
                }
                media.ListenButton.onClick.Invoke();Check(media.SpeakerEnabled,"Speaker ON starts current reply download");yield return null;media.ListenButton.onClick.Invoke();Check(!media.SpeakerEnabled,"Speaker OFF cancels in-flight speech");
            }
            checks.Add("INFO Physical microphone device count: "+Microphone.devices.Length+"; no hardware recording or Android permission claimed.");
            media.StopSpeech();chat.Input.text="";yield return OmsMobileApi.Logout(_=>{},_=>{});OmsMobileSession.Clear();nav.Show("5:36",false);yield return null;Check(!nav.BottomNavigation.activeSelf,"Login still hides bottom navigation");
            PlayerPrefs.SetInt("JarVis.SpeakerEnabled",preference);PlayerPrefs.Save();locale.Select(oldLanguage);nav.Settings.SelectTheme(oldTheme);Finish();
        }
        void Finish(){Application.logMessageReceived-=Log;Directory.CreateDirectory("Logs/PrototypeParity");File.WriteAllText("Logs/PrototypeParity/VoiceWorkflow.txt",(errors.Count==0?"PASS ":"FAIL ")+DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",checks)+"\n"+string.Join("\n",errors));EditorApplication.Exit(errors.Count==0?0:1);}
    }
}
