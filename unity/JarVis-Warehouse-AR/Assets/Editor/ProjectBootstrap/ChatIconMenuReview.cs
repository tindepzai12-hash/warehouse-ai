using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class ChatIconMenuReview
    {
        static ChatIconMenuReview(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("ChatIconMenuReview",false)){SessionState.SetBool("ChatIconMenuReview",false);new GameObject("Chat icon menu QA").AddComponent<ChatIconMenuRunner>();}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("ChatIconMenuReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class ChatIconMenuRunner:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();readonly List<string> failures=new List<string>();
        void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)failures.Add(name);}
        void Error(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)failures.Add(message.Split('\n')[0]);}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Error;yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();string oldTheme=nav.Settings.Theme;var oldLanguage=JarvisLocalization.CurrentLanguage;
            var language=nav.GetComponent<ImportedAppLocalization>();
            var root=nav.screens.First(s=>s.id=="5:1216").root;var chat=root.GetComponent<ImportedJarvisChat>();
            foreach(string theme in new[]{"light","dark"})foreach(var locale in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
            {
                nav.Settings.SelectTheme(theme);language.Select(locale);nav.Show("5:1216",false);yield return null;yield return null;
                chat.Media.ExpandButton.onClick.Invoke();yield return null;
                var menu=root.transform.Find("JarVis plus menu");
                Check(menu!=null&&menu.gameObject.activeSelf,theme+" "+locale+" plus opens icon menu");
                if(menu==null)continue;
                Check(menu.GetComponentsInChildren<TMP_Text>().Length==0,"Menu is icon-only");
                var icons=menu.GetComponentsInChildren<ChatActionIcon>();
                Check(icons.Length==3&&icons.Any(i=>i.symbol==ChatActionIcon.Symbol.Photo)&&icons.Any(i=>i.symbol==ChatActionIcon.Symbol.Microphone)&&icons.Any(i=>i.symbol==ChatActionIcon.Symbol.Speaker||i.symbol==ChatActionIcon.Symbol.SpeakerOff),"Three correct vector symbols");
                var buttons=menu.GetComponentsInChildren<Button>();Check(buttons.Length==3&&buttons.All(b=>((RectTransform)b.transform).rect.width>=44&&((RectTransform)b.transform).rect.height>=44),"All actions have at least 44px hit areas");
                Check(buttons[0].interactable==chat.Media.PhotoButton.interactable&&buttons[1].interactable==chat.Media.MicButton.interactable&&buttons[2].interactable==chat.Media.ListenButton.interactable,"Existing media availability rules preserved");
                var suggestions=(RectTransform)typeof(ImportedJarvisChat).GetField("quickActions",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(chat);
                Check(!suggestions.gameObject.activeSelf,"Suggestions hidden under open menu");
                var rect=(RectTransform)menu;var composer=(RectTransform)chat.Media.ExpandButton.transform.parent;
                Check(-rect.anchoredPosition.y+rect.rect.height<=-composer.anchoredPosition.y,"Menu clears composer");
                var hint=root.GetComponentsInChildren<TMP_Text>().First(t=>t.name=="Empty conversation");
                Check(hint.rectTransform.rect.width<=380.1f&&hint.textWrappingMode==TextWrappingModes.Normal,"English hint wraps within conversation");
                Check(chat.SendButton.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Arrow ")).All(i=>i.color==Color.white),"Send arrow stays white in both themes");
                Directory.CreateDirectory("Logs/PrototypeParity");PrototypeParityRunner.Capture(nav,"Chat-icons-"+theme+"-"+locale+".png",824,1830);
                chat.Media.ExpandButton.onClick.Invoke();yield return null;Check(!chat.Media.PlusMenuOpen,"Plus tap closes menu");Check(suggestions.gameObject.activeSelf,"Suggestions restored after closing");
                chat.Media.TogglePlusMenu();nav.Show("5:36",false);yield return null;Check(!chat.Media.PlusMenuOpen,"Leaving chat closes menu");
            }
            nav.Settings.SelectTheme(oldTheme);language.Select(oldLanguage);nav.Show("5:36",false);
            Application.logMessageReceived-=Error;File.WriteAllText("Logs/PrototypeParity/ChatIconMenu.txt",(failures.Count==0?"PASS ":"FAIL ")+DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",checks)+"\nNo picker, microphone, playback or API calls.\n"+string.Join("\n",failures));
            EditorApplication.Exit(failures.Count==0?0:1);
        }
    }
}
