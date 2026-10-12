using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class SettingsControlsReview
    {
        static SettingsControlsReview(){EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("SettingsControlsReview",false)){SessionState.SetBool("SettingsControlsReview",false);new GameObject("Settings controls review").AddComponent<SettingsControlsRunner>();}};}
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and preserve scene edits first.");
            SessionState.SetBool("SettingsControlsReview",true);EditorApplication.isPlaying=true;
        }
    }
    public sealed class SettingsControlsRunner:MonoBehaviour
    {
        readonly List<string> checks=new List<string>();
        void Check(bool pass,string name){checks.Add((pass?"PASS ":"FAIL ")+name);}
        IEnumerator Start()
        {
            yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();
            if(nav==null||OmsMobileSession.IsReady){EditorApplication.isPlaying=false;yield break;}
            var settings=nav.Settings;var locale=nav.GetComponent<ImportedAppLocalization>();
            var language=JarvisLocalization.CurrentLanguage;string theme=settings.Theme;
            int size=PlayerPrefs.GetInt("JarVis.TextSize",1),glass=PlayerPrefs.GetInt("JarVis.ReduceGlass",0),motion=PlayerPrefs.GetInt("JarVis.ReduceMotion",0);
            locale.Select(SupportedLanguage.En);nav.Show("5:1308",false);
            foreach(bool active in new[]{false,true})
            {
                PlayerPrefs.SetInt("JarVis.ReduceGlass",active?1:0);PlayerPrefs.SetInt("JarVis.ReduceMotion",active?1:0);
                settings.SelectSize(active?2:1);settings.SelectTheme(active?"dark":"light");settings.BuildPrototypeMain();yield return null;yield return null;
                var screen=nav.screens.First(item=>item.id=="5:1308").root;
                var switches=screen.GetComponentsInChildren<ThemeSwitchMotion>();
                Check(switches.Length==3,"Three shared switch components "+active);
                Check(switches.All(item=>((RectTransform)item.transform).sizeDelta==new Vector2(58,48)),"Identical 58x48 touch targets "+active);
                var tracks=switches.Select(item=>item.transform.Find("Switch track").GetComponent<Image>()).ToArray();
                Check(tracks.All(item=>item.rectTransform.sizeDelta==new Vector2(58,32)),"Identical 58x32 visible tracks "+active);
                Check(tracks.All(item=>Mathf.Abs(item.transform.position.x-tracks[0].transform.position.x)<.01f),"All switches aligned on one horizontal coordinate "+active);
                Check(tracks.All(item=>item.color==tracks[0].color),"Shared on/off palette "+active);
                var labels=screen.GetComponentsInChildren<TMP_Text>();
                var headings=labels.Where(item=>new[]{"Text size","Dark mode","Display & motion"}.Contains(item.text)).ToArray();
                Check(headings.Length==3&&headings.All(item=>Mathf.Abs(item.fontSize-headings[0].fontSize)<.01f),"Three headings use the same text size "+active);
                var card=screen.transform.GetComponentsInChildren<RectTransform>().First(item=>item.name=="Appearance and account");
                Check(!card.GetComponentsInChildren<TMP_Text>().Any(item=>new[]{"Small","Medium","Large"}.Contains(item.text)&&-item.rectTransform.anchoredPosition.y<48),"No duplicate size value beside Text size "+active);
                string filename="Settings-unified-"+(active?"dark-large":"light")+".png";
                PrototypeParityRunner.Capture(nav,filename,active?390:412,active?844:915);
                Directory.CreateDirectory("Docs/UiUxAudit/screenshots/settings");File.Copy("Logs/PrototypeParity/"+filename,"Docs/UiUxAudit/screenshots/settings/"+filename,true);
                if(!active)
                {
                    foreach(string name in switches.Select(item=>item.name).ToArray())
                    {
                        var control=screen.GetComponentsInChildren<ThemeSwitchMotion>().First(item=>item.name==name);control.GetComponent<Button>().onClick.Invoke();yield return null;yield return null;
                        Check(name.Contains("Dark")?settings.Dark:name.Contains("transparency")?PlayerPrefs.GetInt("JarVis.ReduceGlass")==1:PlayerPrefs.GetInt("JarVis.ReduceMotion")==1,"Switch callback persists correct preference: "+name);
                        settings.SelectTheme("light");PlayerPrefs.SetInt("JarVis.ReduceGlass",0);PlayerPrefs.SetInt("JarVis.ReduceMotion",0);settings.BuildPrototypeMain();yield return null;yield return null;
                    }
                }
            }
            PlayerPrefs.SetInt("JarVis.ReduceGlass",glass);PlayerPrefs.SetInt("JarVis.ReduceMotion",motion);settings.SelectSize(size);settings.SelectTheme(theme);locale.Select(language);PlayerPrefs.Save();
            Directory.CreateDirectory("Logs/UiUxAudit");File.WriteAllText("Logs/UiUxAudit/settings.txt",(checks.Any(item=>item.StartsWith("FAIL"))?"FAIL":"PASS")+"\n"+DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",checks));
            EditorApplication.isPlaying=false;
        }
    }
}
