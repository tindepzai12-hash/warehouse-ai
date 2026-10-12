using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectBootstrap
{
    // Focused test: existing scene only. No Figma import, rebuilding or warehouse seed.
    [InitializeOnLoad]
    public static class ImportedChatMediaReview
    {
        const string Request="Library/JarVisReviewChatMedia.request";
        static double nextCheck;
        static ImportedChatMediaReview()
        {
            EditorApplication.delayCall+=Check;
            EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;if(File.Exists(Request))Check();};
            EditorApplication.playModeStateChanged+=s=>{
                if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisReviewChatMedia",false))
                {SessionState.SetBool("JarVisReviewChatMedia",false);new GameObject("Chat media verification").AddComponent<ImportedChatMediaReviewRunner>();}
            };
        }
        static void Check()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode){if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;EditorApplication.delayCall+=Check;return;}
            File.Delete(Request);SessionState.SetBool("JarVisReviewChatMedia",true);EditorApplication.isPlaying=true;
        }
    }
    public sealed class ImportedChatMediaReviewRunner:MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            var errors=new List<string>();Directory.CreateDirectory("Logs/ChatMediaReview");
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();nav.Show("5:36",false);yield return null;
            string credentials=File.ReadAllText(@"E:\warehouse-ai\data\oms-admin-credentials.txt");
            string Field(string name)=>System.Text.RegularExpressions.Regex.Match(credentials,"(?m)^"+name+": (.+)\\r?$").Groups[1].Value.Trim();
            var nodes=nav.screens.First(s=>s.id=="5:36").root.GetComponentsInChildren<FigmaNode>(true);
            nodes.First(n=>n.nodeId=="5:64").GetComponent<TMP_InputField>().text=Field("Username");
            nodes.First(n=>n.nodeId=="5:69").GetComponent<TMP_InputField>().text=Field("Password");
            Click(nodes.First(n=>n.nodeId=="5:74").gameObject);
            float deadline=Time.realtimeSinceStartup+20;while(nav.CurrentScreen!="5:1216"&&Time.realtimeSinceStartup<deadline)yield return null;
            if(nav.CurrentScreen!="5:1216"){errors.Add("Admin login failed");Finish(errors);yield break;}
            var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();var media=chat.Media;
            if(media==null){errors.Add("Media component missing");Finish(errors);yield break;}
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            foreach(var button in new[]{media.PhotoButton,media.MicButton,media.ListenButton,chat.SendButton})
            {
                var rect=(RectTransform)button.transform;
                if(rect.rect.height<44)errors.Add(button.name+" hit area too short");
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                if(hits.Count==0||ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=button.gameObject)errors.Add(button.name+" raycast blocked: "+(hits.Count==0?"no hit":hits[0].gameObject.name)+"; pointer "+pointer.position+"; interactable "+button.IsInteractable());
            }
            var testClip=AudioClip.Create("WAV contract",16000,1,16000,false);var wav=ImportedChatMedia.EncodeWav(testClip,16000);Destroy(testClip);
            if(wav.Length!=32044||System.Text.Encoding.ASCII.GetString(wav,0,4)!="RIFF")errors.Add("WAV encoding contract failed");
            media.LoadPhoto("Assets/Resources/Figma/JarVisMascotLogin.png");
            if(media.Photo==null||!chat.SendButton.interactable)errors.Add("Photo preview or image-only send disabled");
            Capture("Composer.png");
            bool live=File.Exists("Library/JarVisVerifyLiveMedia.request");
            if(live)
            {
                File.Delete("Library/JarVisVerifyLiveMedia.request");
                chat.Input.text="What does this image show? Answer briefly in English.";Click(chat.SendButton.gameObject);
                deadline=Time.realtimeSinceStartup+75;while(chat.IsSending&&Time.realtimeSinceStartup<deadline)yield return null;
                if(chat.IsSending||chat.LastReplyLanguage!="en"||string.IsNullOrEmpty(chat.LastReplyText)||media.Photo!=null)errors.Add("Live English photo chat failed");
                Capture("English-Photo-Reply.png");
                chat.Input.text="5S trong kho là gì vậy JarVis? Trả lời ngắn gọn 5 ý.";Click(chat.SendButton.gameObject);
                deadline=Time.realtimeSinceStartup+75;while(chat.IsSending&&Time.realtimeSinceStartup<deadline)yield return null;
                if(chat.IsSending||chat.LastReplyLanguage!="vi"||!chat.LastReplyText.Contains("Sàng lọc"))errors.Add("Live Vietnamese language switch failed");
                Capture("Vietnamese-Reply.png");
                AudioClip speech=null;string error=null;
                yield return OmsMobileApi.Speak("What is 5S in a warehouse?",value=>speech=value,value=>error=value);
                if(speech==null)errors.Add("Live voice output failed: "+error);
                else
                {
                    // OpenAI MP3 playback and mono PCM encoding are verified without opening the physical mic.
                    var samples=new float[speech.samples*speech.channels];speech.GetData(samples,0);
                    int frames=Mathf.RoundToInt((float)speech.samples/speech.frequency*16000);
                    var mono=new float[frames];
                    for(int i=0;i<frames;i++){int source=Mathf.Min(speech.samples-1,Mathf.FloorToInt((float)i*speech.frequency/16000));for(int c=0;c<speech.channels;c++)mono[i]+=samples[source*speech.channels+c]/speech.channels;}
                    var clip=AudioClip.Create("Voice round trip",frames,1,16000,false);clip.SetData(mono,0);
                    OmsMobileApi.Transcript transcript=null;error=null;
                    yield return OmsMobileApi.Transcribe(ImportedChatMedia.EncodeWav(clip,frames),value=>transcript=value,value=>error=value);
                    if(transcript==null||transcript.language!="en"||!transcript.text.Contains("5"))errors.Add("Live voice transcription failed: "+error);
                    else File.WriteAllText("Logs/ChatMediaReview/Transcript.txt",transcript.text);
                    Destroy(speech);Destroy(clip);
                }
            }
            media.ClearPhoto();Finish(errors,live);
        }
        static void Click(GameObject target){ExecuteEvents.Execute(target,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);}
        static void Capture(string file)
        {
            Canvas.ForceUpdateCanvases();var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var canvas=nav.GetComponent<Canvas>();var camera=FindAnyObjectByType<Camera>();
            var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(824,1830,24);var image=new Texture2D(824,1830,TextureFormat.RGBA32,false);
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();File.WriteAllBytes("Logs/ChatMediaReview/"+file,image.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;RenderTexture.active=oldActive;Destroy(image);Destroy(target);}
        }
        void Finish(List<string> errors,bool live=false)
        {
            File.WriteAllText("Logs/ChatMediaReview/result.txt",(errors.Count==0?"PASS":"FAIL")+" — "+DateTime.UtcNow.ToString("o")+"\nAdmin login, media tap targets, attachment and WAV: checked.\nLive English image + Vietnamese chat + OpenAI voice round trip: "+live+"\n"+string.Join("\n",errors)+"\nPhysical Samsung microphone, permission and Android photo picker not tested.\n");Destroy(gameObject);
        }
    }
}
