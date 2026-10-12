using System;
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
using UnityEngine.EventSystems;
namespace ProjectBootstrap
{
 [InitializeOnLoad] public static class ImportedSettingsReview
 {
  static double next;
  static ImportedSettingsReview(){EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+2;if(!File.Exists("Library/JarVisReviewSettings.request")||EditorApplication.isCompiling)return;if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}if(EditorApplication.isPlayingOrWillChangePlaymode)return;File.Delete("Library/JarVisReviewSettings.request");EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("JarVisSettingsReview",true);EditorApplication.isPlaying=true;};EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisSettingsReview",false)){SessionState.SetBool("JarVisSettingsReview",false);new GameObject("Settings review").AddComponent<SettingsReviewRunner>();}};}
 }
 public sealed class SettingsReviewRunner:MonoBehaviour
 {
  IEnumerator Start()
  {
   yield return null;yield return null;
   var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var loc=nav.GetComponent<ImportedAppLocalization>();var settings=nav.Settings;
   string theme=PlayerPrefs.GetString("JarVis.Theme","light");int size=PlayerPrefs.GetInt("JarVis.TextSize",1);var language=JarvisLocalization.CurrentLanguage;
   var errors=new System.Collections.Generic.List<string>();
   Application.LogCallback log=(message,trace,type)=>{if(type==LogType.Exception||type==LogType.Error)errors.Add(message);};Application.logMessageReceived+=log;
   nav.Show("5:1308",false);settings.SelectTheme("light");settings.SelectSize(1);loc.Select(SupportedLanguage.Vi);
   yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Settings-Light-Vi-Fixed.png");
   foreach(var kind in new[]{"password","language","size","theme"})
   {
    settings.Open(kind);yield return null;yield return null;Canvas.ForceUpdateCanvases();
    if(settings.Panel==null||nav.BottomNavigation.activeSelf)errors.Add(kind+" overlay/footer");
    foreach(var label in settings.Panel.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();if(label.isTextTruncated)errors.Add(kind+" clipped "+label.text);}
    if(kind=="language"){var names=settings.Panel.GetComponentsInChildren<TMP_Text>().Select(t=>t.text).ToArray();if(!names.Contains("English (EN)")||!names.Contains("Tiếng Việt (VI)"))errors.Add("Language identities");}
    if(kind=="size"){var slider=settings.Panel.GetComponentInChildren<Slider>();if(slider==null||!slider.wholeNumbers||slider.minValue!=0||slider.maxValue!=2||Mathf.Abs(slider.handleRect.rect.height-24)>1)errors.Add("Three-detent slider geometry");else {foreach(int stop in new[]{0,1,2}){var area=(RectTransform)slider.handleRect.parent;var point=area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin,area.rect.xMax,stop*.5f),area.rect.center.y,0));var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,point)};slider.OnPointerDown(pointer);slider.OnDrag(pointer);slider.OnPointerUp(pointer);if(PlayerPrefs.GetInt("JarVis.TextSize")!=stop)errors.Add("Slider drag detent "+stop);}slider.value=1;}}
    ComposerLocalizationRunner.Capture(nav,"Settings-"+kind+"-Vi.png");
    if(kind=="password")
    {var fields=settings.Panel.GetComponentsInChildren<TMP_InputField>();if(fields.Length!=3||fields.Any(f=>f.contentType!=TMP_InputField.ContentType.Password))errors.Add("Password fields");fields[0].text="not-a-real-password";settings.Close();yield return null;if(settings.CurrentPassword!=null)errors.Add("Password retained after close");}
   }
   settings.Close();settings.SelectTheme("dark");settings.SelectSize(2);loc.Select(SupportedLanguage.En);
   nav.Show("5:1308",false);yield return null;yield return null;settings.ApplyTheme();ComposerLocalizationRunner.Capture(nav,"Settings-Dark-En.png");
   
   foreach(var kind in new[]{"password","language","size","theme"})
   {settings.Open(kind);yield return null;yield return null;foreach(var label in settings.Panel.GetComponentsInChildren<TMP_Text>()){label.ForceMeshUpdate();if(label.isTextTruncated)errors.Add("Dark EN "+kind+" clipped "+label.text);}ComposerLocalizationRunner.Capture(nav,"Settings-"+kind+"-Dark-En.png");}
   settings.Close();foreach(var screen in nav.screens){nav.Show(screen.id,false);yield return null;settings.ApplyTheme();}
   nav.Show("85:14",false);yield return null;yield return null;yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Settings-Offline-Fixed.png");
   nav.Show("5:36",false);yield return null;yield return null;settings.ApplyTheme();ComposerLocalizationRunner.Capture(nav,"Settings-Login-Dark-Fixed.png");
   nav.Show("5:1308",false);yield return null;var settingsScroll=nav.screens.First(s=>s.id=="5:1308").root.GetComponentInChildren<ScrollRect>();settingsScroll.verticalNormalizedPosition=0;yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Settings-Signout-Fixed.png");settingsScroll.verticalNormalizedPosition=1;
   nav.Show("5:1216",false);yield return null;yield return null;settings.ApplyTheme();ComposerLocalizationRunner.Capture(nav,"Settings-Chat-Dark-En.png");
   settings.SelectTheme("light");settings.SelectSize(1);loc.Select(SupportedLanguage.Vi);
   nav.Show("126:8",false);yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Home-Justin-Footer.png");
   var home=nav.screens.First(s=>s.id=="126:8").root;if(home.GetComponentsInChildren<UserPortraitGraphic>(true).Length==0)errors.Add("Home user portrait missing");
   nav.Show("5:1308",false);yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Settings-Justin-Avatar.png");
   nav.Show("48:244",false);yield return null;yield return null;yield return new WaitForSecondsRealtime(.3f);ComposerLocalizationRunner.Capture(nav,"Tasks-All-Fixed.png");
   var task=nav.screens.First(s=>s.id=="48:244").root;var filters=task.GetComponentsInChildren<TMP_Text>(true).Where(t=>ImportedAppLocalization.Source(t).StartsWith("Đang làm (")).ToArray();
   if(filters.Length>0){task.GetComponent<ImportedTaskFilterMotion>().Select(filters[0]);yield return null;ComposerLocalizationRunner.Capture(nav,"Tasks-Selection-Moving.png");yield return new WaitForSecondsRealtime(.35f);ComposerLocalizationRunner.Capture(nav,"Tasks-Working-Fixed.png");var chips=task.GetComponentsInChildren<TaskChipSurface>(true);if(chips.Count(c=>c.GetComponent<Image>().color.b>c.GetComponent<Image>().color.r*1.5f)!=1)errors.Add("Task filter selection duplicated");}else errors.Add("Task filters missing");
   settings.SelectTheme(theme);settings.SelectSize(size);loc.Select(language);settings.Close();nav.Show("5:1308",false);
   Application.logMessageReceived-=log;
   File.WriteAllText("Logs/ComposerLocalizationReview/Settings.txt",(errors.Count==0?"PASS":"REVIEW REQUIRED")+" "+DateTime.UtcNow.ToString("O")+"\nPassword form, VI/EN choices, saved text/theme controls, 22-screen theme traversal; no API calls or account changes.\n"+string.Join("\n",errors)+"\nAndroid keyboard/device appearance not tested.");Destroy(gameObject);
  }
 }
}






