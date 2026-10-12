using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class VoiceMessageReview
    {
        static double deadline;
        static VoiceMessageReview()
        {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("VoiceMessageReview",false))
                { SessionState.SetBool("VoiceMessageReview",false);var runner=new GameObject("Voice message fixture review").AddComponent<VoiceMessageReviewRunner>();runner.transport=SessionState.GetBool("VoiceTransportReview",false);SessionState.SetBool("VoiceTransportReview",false); }
            };
            EditorApplication.update += () => {
                if (deadline > 0 && EditorApplication.timeSinceStartup > deadline)
                { File.WriteAllText("Logs/VoiceMessageReview/Result.txt","FAIL: watchdog timeout"); EditorApplication.Exit(1); }
            };
        }
        public static void Run()
        {
            Directory.CreateDirectory("Logs/VoiceMessageReview"); deadline = EditorApplication.timeSinceStartup + 150;
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("VoiceMessageReview",true); EditorApplication.isPlaying = true;
        }
        public static void RunTransport(){SessionState.SetBool("VoiceTransportReview",true);Run();}
    }

    public sealed class VoiceMessageReviewRunner : MonoBehaviour
    {
        public bool transport;
        [Serializable] sealed class FixtureEndpoint { public bool fixture; public string baseUrl; }
        [Serializable] sealed class FixtureMetrics { public bool fixture; public int transcriptions,answers,voicePosts,droppedResponses; public bool contextPreserved; public float[] audioDurations; public string[] ids; }
        readonly List<string> checks = new List<string>();
        readonly List<string> failures = new List<string>();
        void Check(bool value,string name) { checks.Add((value ? "PASS " : "FAIL ") + name); if (!value) failures.Add(name); }
        void Error(string message,string stack,LogType type) { if (type == LogType.Exception || type == LogType.Error) failures.Add(message.Split('\n')[0]); }
        static AudioClip Fixture(int seconds)
        {
            var clip = AudioClip.Create("QA generated PCM, not microphone speech",seconds*16000,1,16000,false);
            var samples = new float[clip.samples];
            for (int frame=0;frame<samples.Length;frame++) samples[frame]=Mathf.Sin(frame*.12f)*(.012f+.028f*(frame/(float)samples.Length));
            clip.SetData(samples,0); return clip;
        }
        static void InstallStoppedFixture(VoiceRecorder recorder,int seconds)
        {
            recorder.Cancel();
            typeof(VoiceRecorder).GetField("clip",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(recorder,Fixture(seconds));
            typeof(VoiceRecorder).GetField("frames",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(recorder,seconds*16000);
            typeof(VoiceRecorder).GetProperty("State").SetValue(recorder,VoiceRecorderState.Stopped);
            for(int index=0;index<recorder.Levels.Length;index++) recorder.Levels[index]=.05f+index*.01f;
        }
        IEnumerator Start()
        {
            Application.logMessageReceived += Error; yield return null; yield return null;
            if(transport){yield return Transport();yield break;}
            var navigation = FindAnyObjectByType<ImportedFigmaNavigation>();
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener=>listener.isActiveAndEnabled)==1,"Existing app has exactly one active audio listener for voice and TTS");
            var localization = navigation.GetComponent<ImportedAppLocalization>();
            string oldTheme=navigation.Settings.Theme; var oldLanguage=JarvisLocalization.CurrentLanguage;
            navigation.Presence.enabled=false;navigation.Presence.StopAllCoroutines();
            typeof(ImportedPresence).GetProperty("State").SetValue(navigation.Presence,PresenceState.Online);
            OmsMobileSession.Set("http://127.0.0.1:1","UI-FIXTURE-NOT-AUTHENTICATED","QA",DateTime.UtcNow.AddHours(1).ToString("O"));
            navigation.Show("5:1216",false);yield return null;yield return null;
            var chat=navigation.screens.First(screen=>screen.id=="5:1216").root.GetComponent<ImportedJarvisChat>();
            var media=chat.Media;chat.Input.text="Draft stays independent";
            Check(!media.VoiceMode&&!media.RecordingBar.gameObject.activeSelf,"Idle composer has no permanent recording panel");
            InstallStoppedFixture(media.Recorder,20);media.Recorder.Cancel();
            Check(chat.MessageCount==0&&!media.VoiceMode&&chat.Input.text=="Draft stays independent","Cancel fixture discards all audio without a message or upload");
            InstallStoppedFixture(media.Recorder,20);
            var longMessage=media.Recorder.FinalizeMessage();
            Check(longMessage!=null&&Mathf.Abs(longMessage.Duration-20)<.001f,"Entire 20-second fixture survives finalization");
            Check(media.Recorder.FinalizeMessage()==null,"One recording finalizes only once, repeated Send cannot create another message");
            var bytes=File.ReadAllBytes(longMessage.AudioPath);
            Check(bytes.Length==640044&&BitConverter.ToInt32(bytes,40)==640000,"20s PCM16 WAV exact header/data lengths");
            Check(longMessage.Waveform.Length==64&&longMessage.Waveform.Max()>longMessage.Waveform.Min(),"64 bars derive from the actual PCM envelope");
            var longView=chat.AddVoiceMessage(longMessage,media.Playback,media.RetryVoice);
            InstallStoppedFixture(media.Recorder,3);var shortMessage=media.Recorder.FinalizeMessage();
            var shortView=chat.AddVoiceMessage(shortMessage,media.Playback,media.RetryVoice);
            Check(Mathf.Abs(shortMessage.Duration-3)<.001f&&chat.MessageCount==2,"3s optimistic audio row remains a voice bubble");
            shortView.PlayButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.25f);
            Check(media.Playback.Current==shortMessage&&media.Playback.Playing&&media.Playback.Position>0,"Actual AudioSource plays original fixture WAV and advances");
            shortView.PlayButton.onClick.Invoke();float paused=media.Playback.Position;yield return new WaitForSecondsRealtime(.15f);
            Check(!media.Playback.Playing&&Mathf.Abs(media.Playback.Position-paused)<.05f,"Pause retains playback position");
            longView.PlayButton.onClick.Invoke();yield return null;
            Check(media.Playback.Current==longMessage&&media.Playback.Playing,"Playing B replaces A, only one playback source");
            media.Playback.Stop();shortView.PlayButton.onClick.Invoke();
            yield return new WaitForSecondsRealtime(3.4f);
            Check(media.Playback.Current==null&&media.Playback.Position==0,"End of original recording resets playback to start");
            shortMessage.Status=VoiceMessageStatus.Failed;shortMessage.Retryable=true;shortMessage.Error="NETWORK_ERROR";
            shortView.Refresh();Check(File.Exists(shortMessage.AudioPath)&&shortView.RetryButton.gameObject.activeSelf,"Network failure keeps the recording and offers same-message retry");
            shortMessage.Retryable=false;shortMessage.Error="AI_REQUEST_FAILED";shortView.Refresh();
            Check(!shortView.RetryButton.gameObject.activeSelf&&File.Exists(shortMessage.AudioPath),"Ambiguous provider failure keeps recording without repeated provider cost");
            shortMessage.Status=VoiceMessageStatus.Completed;shortMessage.Transcript="Fixture transcript only, not recognized speech";
            shortView.Refresh();
            Check(shortView.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight<=80&&longView.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight<=80,"Default audio bubbles are compact, no dedicated transcript row");
            Check(shortView.GetComponentsInChildren<TMP_Text>(true).Any(label=>label.name=="Optional voice transcript"&&!label.gameObject.activeSelf),"Transcript defaults collapsed; audio row is never replaced");
            shortView.TranscriptButton.onClick.Invoke();
            Check(shortView.GetComponentsInChildren<TMP_Text>(true).Any(label=>label.name=="Optional voice transcript"&&label.gameObject.activeSelf&&label.text==shortMessage.Transcript),"Compact transcript button expands original transcript inline");
            shortView.TranscriptButton.onClick.Invoke();
            Check(shortView.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight<=80,"Transcript collapse restores compact bubble height");
            foreach(string theme in new[]{"light","dark"})foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
            {
                navigation.Settings.SelectTheme(theme);localization.Select(language);InstallStoppedFixture(media.Recorder,3);chat.RefreshComposer();
                yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();
                Check(media.RecordingBar.gameObject.activeSelf&&!chat.Input.gameObject.activeSelf&&chat.Input.text=="Draft stays independent",theme+language+" recording replaces composer, preserves typed draft");
                foreach(var button in new[]{media.RecordingBar.CancelButton,media.RecordingBar.StopButton,media.RecordingBar.SendButton})
                { var rect=(RectTransform)button.transform;Check(rect.rect.width>=44&&rect.rect.height>=44,theme+language+" 44px recording target "+button.name); }
                Check(shortView.GetComponentsInChildren<TMP_Text>(true).Any(label=>label.name=="Voice delivery status"&&label.text.Contains(language==SupportedLanguage.En?"Sent":"Đã gửi")),theme+language+" voice status uses UI language");
                foreach(var control in new[]{shortView.PlayButton,shortView.TranscriptButton}){var rect=(RectTransform)control.transform;Check(rect.rect.width>=44&&rect.rect.height>=44,theme+language+" compact bubble keeps 44px "+control.name);}
                PrototypeParityRunner.Capture(navigation,"Voice-message-"+theme+"-"+language+".png",824,1830);
                media.CancelRecording();chat.RefreshComposer();yield return null;
                Check(!media.RecordingBar.gameObject.activeSelf&&chat.Input.gameObject.activeSelf,theme+language+" cancel restores normal composer");
            }
            string path=shortMessage.AudioPath;shortMessage.Dispose();Check(!File.Exists(path),"Audio file ownership cleans up after message disposal");
            media.Playback.Stop();media.ResetSession();OmsMobileSession.Clear();navigation.Show("5:36",false);
            navigation.Settings.SelectTheme(oldTheme);localization.Select(oldLanguage);
            checks.Add("INFO Generated PCM/UI fixtures only; no real microphone, Android permissions, STT or AI call verified.");
            Application.logMessageReceived-=Error;
            File.WriteAllText("Logs/VoiceMessageReview/Result.txt",(failures.Count==0?"PASS":"FAIL")+"\n"+string.Join("\n",checks)+"\n"+string.Join("\n",failures));
            EditorApplication.Exit(failures.Count==0?0:1);
        }
        IEnumerator Transport()
        {
            var endpoint=JsonUtility.FromJson<FixtureEndpoint>(File.ReadAllText("E:/warehouse-ai/logs/voice-message-fixture-endpoint.json"));
            if(endpoint?.fixture!=true||!Uri.TryCreate(endpoint.baseUrl,UriKind.Absolute,out var address)||!address.IsLoopback||address.Scheme!="http")throw new InvalidOperationException("Transport review requires a local isolated fixture");
            var navigation=FindAnyObjectByType<ImportedFigmaNavigation>();
            navigation.Presence.enabled=false;navigation.Presence.StopAllCoroutines();typeof(ImportedPresence).GetProperty("State").SetValue(navigation.Presence,PresenceState.Online);
            OmsMobileApi.LoginReply session=null;string error=null;
            yield return OmsMobileApi.Login(endpoint.baseUrl,"NV-00101","test-voice-only",reply=>session=reply,value=>error=value);
            Check(session!=null,"HTTP login on isolated temporary fixture database");
            if(session==null){FinishTransport();yield break;}
            OmsMobileSession.Set(endpoint.baseUrl,session.accessToken,session.user.employeeCode,session.expiresAt);
            navigation.Show("5:1216",false);yield return null;yield return null;
            var chat=navigation.screens.First(screen=>screen.id=="5:1216").root.GetComponent<ImportedJarvisChat>();var media=chat.Media;
            chat.Input.text="Unsent typed draft";
            InstallStoppedFixture(media.Recorder,3);media.SendRecording();media.SendRecording();media.SendRecording();
            Check(chat.MessageCount==1&&media.IsBusy&&chat.IsSending,"Triple Send creates exactly one optimistic voice row before HTTP completes");
            float until=Time.realtimeSinceStartup+30;
            while(media.IsBusy&&Time.realtimeSinceStartup<until)yield return null;
            var first=chat.GetComponentsInChildren<VoiceMessageView>().Single();
            Check(first.Message.Status==VoiceMessageStatus.Failed&&first.Message.Retryable&&File.Exists(first.Message.AudioPath),"Dropped successful response preserves original audio and safe retry");
            string firstId=first.Message.Id;
            media.RetryVoice(first.Message);media.RetryVoice(first.Message);
            until=Time.realtimeSinceStartup+30;while(media.IsBusy&&Time.realtimeSinceStartup<until)yield return null;
            Check(first.Message.Status==VoiceMessageStatus.Completed&&first.Message.Id==firstId&&chat.MessageCount==2,"Manual Retry recovers cached answer with same ID and no extra voice bubble");
            Check(chat.LastReplyText=="QA fixture response: WH-VOICE-42"&&chat.Input.text=="Unsent typed draft","Actual Unity HTTP response enters shared reply renderer without consuming typed draft");
            InstallStoppedFixture(media.Recorder,20);media.SendRecording();
            until=Time.realtimeSinceStartup+30;while(media.IsBusy&&Time.realtimeSinceStartup<until)yield return null;
            var voices=chat.GetComponentsInChildren<VoiceMessageView>();
            Check(voices.Length==2&&voices.All(view=>view.Message.Status==VoiceMessageStatus.Completed)&&chat.MessageCount==4,"Second whole utterance coexists with first voice and both replies");
            voices[1].PlayButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.15f);
            Check(media.Playback.Current==voices[1].Message&&media.Playback.Playing&&media.Playback.Position>0,"Uploaded original WAV is still playable after success");media.Playback.Stop();
            var request=UnityEngine.Networking.UnityWebRequest.Get(endpoint.baseUrl+"/fixture/metrics");
            yield return request.SendWebRequest();
            var metrics=JsonUtility.FromJson<FixtureMetrics>(request.downloadHandler.text);request.Dispose();
            Check(metrics.fixture&&metrics.transcriptions==2&&metrics.answers==2,"Three audio POSTs including retry trigger only two STT and two answer executions");
            Check(metrics.voicePosts==3&&metrics.ids[0]==metrics.ids[1]&&metrics.ids[2]!=metrics.ids[0]&&metrics.droppedResponses==1,"One recording ID is preserved across lost-response retry");
            Check(metrics.audioDurations.Length==2&&Mathf.Abs(metrics.audioDurations[0]-3)<.001f&&Mathf.Abs(metrics.audioDurations[1]-20)<.001f,"Backend receives complete original 3s and 20s PCM recordings");
            Check(metrics.contextPreserved,"Second voice reaches server answer with prior user and assistant context");
            Check(chat.Input.text=="Unsent typed draft","Typed draft remains independent through both voices and retry");
            yield return OmsMobileApi.Logout(_=>{},_=>{});OmsMobileSession.Clear();navigation.Show("5:36",false);
            FinishTransport();
        }
        void FinishTransport()
        {
            Application.logMessageReceived-=Error;
            checks.Add("INFO HTTP transport and generated PCM verified with injected providers; not human microphone/STT accuracy or live AI.");
            File.WriteAllText("Logs/VoiceMessageReview/Transport.txt",(failures.Count==0?"PASS":"FAIL")+"\n"+string.Join("\n",checks)+"\n"+string.Join("\n",failures));
            EditorApplication.Exit(failures.Count==0?0:1);
        }
    }
}
