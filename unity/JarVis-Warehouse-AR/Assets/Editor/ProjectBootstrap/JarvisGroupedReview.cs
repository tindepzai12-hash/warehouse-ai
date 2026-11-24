using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class JarvisGroupedReview
    {
        static JarvisGroupedReview(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarvisGroupedQA",false)){SessionState.SetBool("JarvisGroupedQA",false);new GameObject("Grouped regression").AddComponent<JarvisGroupedRunner>();}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("JarvisGroupedQA",true);EditorApplication.isPlaying=true;}
    }
    public sealed class JarvisGroupedRunner:MonoBehaviour
    {
        readonly List<string> lines=new List<string>();bool failed;float deadline;
        const string Dir="Logs/GroupedRepair20261110";
        void Check(bool ok,string detail){lines.Add((ok?"PASS ":"FAIL ")+detail);if(!ok)failed=true;}
        void Update(){if(deadline>0&&Time.realtimeSinceStartup>deadline){Check(false,"watchdog");Finish();}}
        void Finish(){deadline=0;Directory.CreateDirectory(Dir);File.WriteAllLines(Dir+"/result.txt",lines);OmsMobileSession.Clear();EditorApplication.Exit(failed?1:0);}
        IEnumerator Start()
        {
            deadline=Time.realtimeSinceStartup+110;Directory.CreateDirectory(Dir);yield return null;yield return null;
            foreach(string mode in new[]{"cap401","capexpired","post401","normal"})
            {
                OmsMobileSession.Set("http://127.0.0.1:18775/"+mode,"FIXTURE","QA",DateTime.UtcNow.AddHours(1).ToString("O"));
                OmsMobileApi.VoiceReply reply=null;string error=null;
                using(var upload=new OmsMobileApi.VoiceUpload())
                {
                    yield return upload.Send(new byte[32044],v=>reply=v,e=>error=e,()=>{},"qa");
                    if(mode=="cap401"||mode=="normal")Check(reply?.reply=="fixture answer"&&error==null&&!upload.RetryAllowed,mode+": one authenticated legacy voice fallback, no replay");
                    else if(mode=="capexpired")Check(reply==null&&!OmsMobileSession.IsReady,mode+": expired session stops before upload");
                    else Check(reply==null&&error!=null&&OmsMobileSession.IsReady,mode+": transcription POST rejection is never bypassed");
                }
            }
            OmsMobileSession.Clear();
            string source=@"C:\Users\Windows\AppData\Local\Temp\codex-clipboard-b2c27665-2fe3-424c-b54f-dc91dcac0c53.png";
            var original=new Texture2D(2,2);bool loaded=original.LoadImage(File.ReadAllBytes(source));
            var rt=RenderTexture.GetTemporary(768,1024,0);var previous=RenderTexture.active;Graphics.Blit(original,rt);RenderTexture.active=rt;
            var scaled=new Texture2D(768,1024,TextureFormat.RGB24,false);scaled.ReadPixels(new Rect(0,0,768,1024),0,0);scaled.Apply();
            var decoded=ImportedCameraScanner.Decode(scaled.GetPixels32(),768,1024);
            Check(loaded&&decoded!=null&&decoded.Text=="4710043004342","actual supplied milk photo decodes exact EAN at chat upload resolution");
            RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(original);Destroy(scaled);
            Check(!JarvisTestProduct.Matches("05841479"),"previous wrong barcode cannot match milk");
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var loc=nav.GetComponent<ImportedAppLocalization>();
            string oldTheme=PlayerPrefs.GetString("JarVis.Theme","light");int oldSize=PlayerPrefs.GetInt("JarVis.TextSize",1);var oldLanguage=JarvisLocalization.CurrentLanguage;
            foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})foreach(string theme in new[]{"light","dark"})
            {
                loc.Select(language);nav.Settings.SelectTheme(theme);PlayerPrefs.SetInt("JarVis.TextSize",1);
                nav.Show("5:1216",false);yield return null;
                var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();
                chat.AddMessage(language==SupportedLanguage.Vi?"Hộp sữa được lưu tại B. Bản ghi giọng nói vẫn được giữ khi gửi lỗi.":"Milk is stored at B. The voice recording is kept if sending fails.",false);
                yield return null;Canvas.ForceUpdateCanvases();
                var message=chat.GetComponentsInChildren<TMP_Text>().Last(t=>t.name=="Message");
                float lum=(message.color.r+message.color.g+message.color.b)/3;
                Check(theme=="dark"?lum>.75f:lum<.4f,language+" "+theme+": new message contrast applied without navigation");
                Capture(nav,language+"-"+theme+"-chat",824,1830);
                chat.SetKeyboardMode(true);chat.SetKeyboardMode(false);Check(nav.BottomNavigation.activeSelf,"composer restores shared navigation");
                nav.Show("126:8",false);yield return new WaitForSecondsRealtime(.4f);
                var home=nav.screens.First(s=>s.id=="126:8").root;
                Check(home.GetComponentsInChildren<Button>(true).Any(b=>b.transform.parent?.name=="Milk A to B trial"),"Home exposes explicit local mission");
                Capture(nav,language+"-"+theme+"-home",824,1830);
                nav.Show("48:244",false);yield return null;yield return null;
                Check(nav.screens.First(s=>s.id=="48:244").root.GetComponentsInChildren<RectTransform>().Any(t=>t.name=="Milk A to B trial"),"Tasks exposes trial independently of OMS task data");
                JarvisLabMission.ShowProduct(nav);yield return null;Canvas.ForceUpdateCanvases();
                foreach(var t in nav.GetComponentsInChildren<PrototypeDialog>().SelectMany(d=>d.GetComponentsInChildren<TMP_Text>())){t.ForceMeshUpdate();Check(!t.isTextTruncated,language+" "+theme+": popup fits "+t.text);}
                Capture(nav,language+"-"+theme+"-popup",824,1830);
                if(language==SupportedLanguage.Vi&&theme=="dark")Capture(nav,"Vi-dark-popup-alternate",1080,1920);
            }
            nav.Settings.SelectTheme(oldTheme);PlayerPrefs.SetInt("JarVis.TextSize",oldSize);loc.Select(oldLanguage);
            Finish();
        }
        static void Capture(ImportedFigmaNavigation nav,string name,int width,int height)
        {
            var canvas=nav.GetComponent<Canvas>();var camera=FindAnyObjectByType<Camera>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGBA32,false);
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;camera.targetTexture=target;Canvas.ForceUpdateCanvases();
            foreach(var view in nav.GetComponentsInChildren<ImportedFigmaViewport>(true))view.Fit();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Dir+"/"+name+".png",image.EncodeToPNG());
            RenderTexture.active=oldActive;camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;Destroy(image);Destroy(target);
        }
    }
}
