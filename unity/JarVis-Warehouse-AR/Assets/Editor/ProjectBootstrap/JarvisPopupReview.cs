using System.Collections;
using System.IO;
using System.Linq;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class JarvisPopupReview
    {
        static JarvisPopupReview(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("PopupReview",false)){SessionState.SetBool("PopupReview",false);new GameObject("Popup visual review").AddComponent<JarvisPopupRunner>();}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("PopupReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class JarvisPopupRunner:MonoBehaviour
    {
        const string Dir="Logs/PopupBullets";
        IEnumerator Start()
        {
            Directory.CreateDirectory(Dir);yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var loc=nav.GetComponent<ImportedAppLocalization>();
            var oldLanguage=JarvisLocalization.CurrentLanguage;string oldTheme=PlayerPrefs.GetString("JarVis.Theme","light");bool ok=true;
            foreach(var lang in new[]{SupportedLanguage.Vi,SupportedLanguage.En})foreach(string theme in new[]{"dark","light"})
            {
                loc.Select(lang);nav.Settings.SelectTheme(theme);nav.Show("48:244",false);yield return null;
                JarvisLabMission.ShowProduct(nav);yield return null;Canvas.ForceUpdateCanvases();
                var popup=nav.GetComponentsInChildren<PrototypeDialog>().Last();
                foreach(var text in popup.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();if(text.isTextTruncated){ok=false;Debug.LogError("Popup truncation: "+text.text);}}
                var buttons=popup.GetComponentsInChildren<Button>();ok&=buttons.Length==2;
                Capture(nav,lang+"-"+theme,1080,2400);
                if(lang==SupportedLanguage.Vi&&theme=="dark")Capture(nav,"Vi-dark-alternate",1080,1920);
                buttons.First(b=>b.GetComponentInChildren<TMP_Text>()!=null).onClick.Invoke();yield return null;ok&=!popup;
            }
            // Optional presentation is off by default for all existing detail callers.
            var standard=PrototypeDialog.Details(nav,"Detail","Summary",new[]{"Original detail"},null,null);yield return null;
            ok&=!standard.GetComponentsInChildren<RectTransform>().Any(r=>r.name=="Fact bullet");Destroy(standard);
            nav.Settings.SelectTheme(oldTheme);loc.Select(oldLanguage);
            File.WriteAllText(Dir+"/result.txt",ok?"PASS: VI/EN light/dark; primary and alternate aspect; no text truncation; close works; no transfer action; standard detail preserved.":"FAIL: inspect popup review.");
            EditorApplication.Exit(ok?0:1);
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
