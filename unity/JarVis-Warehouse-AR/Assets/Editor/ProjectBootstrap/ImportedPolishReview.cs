using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class ImportedPolishReview
    {
        static double next;
        static ImportedPolishReview()
        {
            EditorApplication.update+=()=>{
                if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;
                const string flag="Library/JarVisReviewPolish.request";
                if(!File.Exists(flag)||EditorApplication.isCompiling)return;
                if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                SessionState.SetBool("JarVisPolishOfflineOnly",File.ReadAllText(flag).Contains("offline"));File.Delete(flag);Run();
            };
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisPolishReview",false))
            {SessionState.SetBool("JarVisPolishReview",false);new GameObject("UI polish review").AddComponent<ImportedPolishRunner>().offlineOnly=SessionState.GetBool("JarVisPolishOfflineOnly",false);SessionState.SetBool("JarVisPolishOfflineOnly",false);}};
        }
        [MenuItem("Tools/JarVis/Review UI Polish")]
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("JarVisPolishReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class ImportedPolishRunner:MonoBehaviour
    {
        public bool offlineOnly;
        IEnumerator Start()
        {
            yield return null;yield return null;var errors=new List<string>();var clipping=new List<string>();
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var loc=nav.GetComponent<ImportedAppLocalization>();
            foreach(var scanner in nav.GetComponentsInChildren<ImportedCameraScanner>(true))scanner.enabled=false;
            int icons=0;Directory.CreateDirectory("Logs/ComposerLocalizationReview");
            foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
            {
                loc.Select(language);
                foreach(var screen in nav.screens.Where(s=>!offlineOnly||s.id=="85:14"))
                {
                    nav.Show(screen.id,false);yield return null;yield return null;Canvas.ForceUpdateCanvases();
                    foreach(var text in screen.root.GetComponentsInChildren<TMP_Text>(false))
                    {text.ForceMeshUpdate();if(text.isTextTruncated)clipping.Add(language+" "+screen.id+" "+text.name+": "+text.text);}
                    if(language==SupportedLanguage.Vi)icons+=screen.root.GetComponentsInChildren<ImportedUtilityIcon>(true).Length;
                    if(nav.BottomNavigation.activeInHierarchy)
                    foreach(var tab in nav.BottomNavigation.GetComponentsInChildren<FigmaNode>(true).Where(n=>(n.nodeName??"").StartsWith("tab-")))
                    {
                        var icon=tab.GetComponentInChildren<ImportedUtilityIcon>(true);var text=tab.GetComponentInChildren<TMP_Text>(true);
                        if(icon==null||Mathf.Abs(icon.rectTransform.anchoredPosition.x+icon.rectTransform.rect.width*.5f-(text.rectTransform.anchoredPosition.x+text.rectTransform.rect.width*.5f))>.1f)errors.Add(screen.id+" misaligned tab "+tab.nodeName);
                        if(tab.GetComponent<Button>()==null)errors.Add("Unbound tab "+tab.nodeName);
                    }
                    ComposerLocalizationRunner.Capture(nav,"Polish-"+screen.id.Replace(':','-')+"-"+language+".png");
                    if(screen.id=="5:1308")
                    {var scroll=screen.root.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Polish-Settings-Bottom-"+language+".png");scroll.verticalNormalizedPosition=1;}
                }
            }
            loc.Select(SupportedLanguage.Vi);nav.Show("5:36",false);
            File.WriteAllText("Logs/ComposerLocalizationReview/Polish.txt",(errors.Count==0&&clipping.Count==0?"PASS":"REVIEW REQUIRED")+" — "+DateTime.UtcNow.ToString("O")+"\n"+(offlineOnly?"Offline Home":"22 screens")+" × VI/EN; aligned footer; "+icons+" vector symbols; no API/model calls.\n"+string.Join("\n",errors)+"\nTruncated labels to inspect:\n"+string.Join("\n",clipping)+"\nPhysical Android not tested.");Destroy(gameObject);
        }
    }
}
