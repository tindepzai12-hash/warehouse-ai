using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class ImportedHomeReview
    {
        static ImportedHomeReview()
        {
            EditorApplication.update+=()=>{
                const string flag="Library/JarVisReviewHome.request";
                if(!File.Exists(flag)||EditorApplication.isCompiling||EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isPlaying)return;
                if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
                bool live=File.ReadAllText(flag).Contains("live");File.Delete(flag);if(live)RunLive();else Run();
            };
            EditorApplication.playModeStateChanged+=s=>{
                if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisHomeReview",false))
                {SessionState.SetBool("JarVisHomeReview",false);new GameObject("Home layout review").AddComponent<HomeReviewRunner>().live=SessionState.GetBool("JarVisHomeLive",false);}
            };
        }
        [MenuItem("Tools/JarVis/Review Approved Home")]
        public static void Run()
        {
            SessionState.SetBool("JarVisHomeLive",false);
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("JarVisHomeReview",true);EditorApplication.isPlaying=true;
        }
        public static void RunLive(){Run();SessionState.SetBool("JarVisHomeLive",true);}
        public static void OpenForUser(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");EditorApplication.isPlaying=true;}
    }
    public sealed class HomeReviewRunner:MonoBehaviour
    {
        public bool live;
        IEnumerator Start()
        {
            yield return null;yield return null;
            var errors=new List<string>();var nav=FindAnyObjectByType<ImportedFigmaNavigation>();
            string dir="Logs/HomeReview";Directory.CreateDirectory(dir);Directory.CreateDirectory("Logs/ComposerLocalizationReview");
            var loc=nav.GetComponent<ImportedAppLocalization>();
            var oldLanguage=JarvisLocalization.CurrentLanguage;string oldTheme=PlayerPrefs.GetString("JarVis.Theme","light");
            int oldSize=PlayerPrefs.GetInt("JarVis.TextSize",1);
            if(live)
            {
                string credentials=File.ReadAllText(@"E:\warehouse-ai\data\oms-admin-credentials.txt");
                string Field(string name)=>System.Text.RegularExpressions.Regex.Match(credentials,"(?m)^"+name+": (.+)\\r?$").Groups[1].Value.Trim();
                if(!JarVis.Warehouse.Backend.OmsMobileSession.TryLoadEndpoint(out var endpoint))errors.Add("Local backend not configured");
                else
                {
                    JarVis.Warehouse.Backend.OmsMobileApi.LoginReply login=null;
                    yield return JarVis.Warehouse.Backend.OmsMobileApi.Login(endpoint,Field("Username"),Field("Password"),r=>login=r,e=>errors.Add("Local login failed"));
                    if(login!=null)
                    {
                        JarVis.Warehouse.Backend.OmsMobileSession.Set(endpoint,login.accessToken,login.user.employeeCode,login.expiresAt);
                        yield return JarVis.Warehouse.Backend.OmsMobileApi.SetPresence(true,r=>{},e=>errors.Add("Online activation failed"));
                        nav.Presence.Restore();float until=Time.realtimeSinceStartup+15;
                        while(nav.Presence.State==PresenceState.Loading&&Time.realtimeSinceStartup<until)yield return null;
                        if(!nav.Presence.CanWork||nav.Presence.Snapshot?.streak==null)errors.Add("Real API streak missing");
                    }
                }
            }
            var footer=nav.BottomNavigation.GetComponentsInChildren<RectTransform>(true).ToDictionary(r=>r.GetInstanceID(),r=>r.anchoredPosition);
            foreach(var language in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
            foreach(var theme in new[]{"light","dark"})
            foreach(int size in new[]{1,2})
            {
                loc.Select(language);nav.Settings.SelectTheme(theme);PlayerPrefs.SetInt("JarVis.TextSize",size);
                nav.Show("126:8",false);yield return new WaitForSecondsRealtime(.5f);Canvas.ForceUpdateCanvases();
                var home=nav.screens.First(s=>s.id=="126:8").root;
                home.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition=1;yield return null;Canvas.ForceUpdateCanvases();
                foreach(var t in home.GetComponentsInChildren<TMP_Text>(false))
                {t.ForceMeshUpdate();if(t.isTextTruncated)errors.Add(language+" "+theme+" "+size+" clipped: "+t.text);if(t.font.boldSpacing!=0||t.characterSpacing!=0)errors.Add("Unexpected text tracking: "+t.text);}
                var actions=home.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("Quick action")).ToArray();
                if(actions.Length!=4)errors.Add("Expected four quick actions");
                foreach(var b in actions){var r=(RectTransform)b.transform;if(r.rect.width<48||r.rect.height<48)errors.Add("Small action target "+b.name);}
                foreach(var r in nav.BottomNavigation.GetComponentsInChildren<RectTransform>(true))
                    if(footer.TryGetValue(r.GetInstanceID(),out var position)&&r.anchoredPosition!=position)errors.Add("Footer moved "+r.name);
                ComposerLocalizationRunner.Capture(nav,"Home-"+language+"-"+theme+"-"+size+".png");
                var scroll=home.GetComponentInChildren<ScrollRect>();scroll.verticalNormalizedPosition=0;yield return null;yield return null;
                ComposerLocalizationRunner.Capture(nav,"Home-Bottom-"+language+"-"+theme+"-"+size+".png");
            }
            nav.Show("85:14",false);yield return new WaitForSecondsRealtime(.5f);ComposerLocalizationRunner.Capture(nav,"Home-Offline.png");
            if(live){yield return JarVis.Warehouse.Backend.OmsMobileApi.Logout(r=>{},e=>errors.Add("Review logout failed"));JarVis.Warehouse.Backend.OmsMobileSession.Clear();}
            nav.Settings.SelectTheme(oldTheme);PlayerPrefs.SetInt("JarVis.TextSize",oldSize);loc.Select(oldLanguage);nav.Show("5:36",false);
            File.WriteAllText(dir+"/Result.txt",(errors.Count==0?"PASS":"FAIL")+" "+DateTime.UtcNow.ToString("O")+"\nVI/EN, light/dark, medium/large, scroll, footer stability. Real presence API: "+live+". No model calls.\n"+string.Join("\n",errors));
            if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);else Destroy(gameObject);
        }
    }
}
