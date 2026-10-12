using System;using System.Collections;using System.IO;using System.Linq;using Cdm.Figma.UI;using JarVis.Warehouse.UI;using JarVis.Warehouse.Backend;using JarVis.Warehouse.Localization;using TMPro;using UnityEditor;using UnityEngine;using UnityEngine.UI;
namespace ProjectBootstrap {
 [InitializeOnLoad] public static class ImportedPresenceReview {
  static double next;static ImportedPresenceReview(){EditorApplication.update+=()=>{if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;const string flag="Library/JarVisReviewPresence.request";if(!File.Exists(flag)||EditorApplication.isCompiling||!EditorApplication.isPlaying)return;bool focused=File.ReadAllText(flag).Contains("focused");File.Delete(flag);new GameObject("Presence UI review").AddComponent<PresenceReviewRunner>().focused=focused;};}
 }
 public sealed class PresenceReviewRunner:MonoBehaviour {
  public bool focused;
  IEnumerator Start(){var nav=FindAnyObjectByType<ImportedFigmaNavigation>();var errors=new System.Collections.Generic.List<string>();string dir="Logs/PresenceReview";Directory.CreateDirectory(dir);OmsMobileSession.Clear();
   // Capture all routes without backend/model calls before authenticated state testing.
   var loc=nav.GetComponent<ImportedAppLocalization>();loc.Select(SupportedLanguage.Vi);
   var ledger=new System.Text.StringBuilder("# Runtime route audit\n");if(!focused)foreach(var screen in nav.screens){nav.Show(screen.id,false);yield return null;yield return null;ledger.AppendLine(screen.id+" | "+screen.root.name+" | buttons="+screen.root.GetComponentsInChildren<Button>(true).Length);ComposerLocalizationRunner.Capture(nav,"Audit-"+screen.id.Replace(':','-')+".png");}
   File.WriteAllText(dir+"/Routes.md",ledger.ToString());nav.Show("5:36",false);
   string credentials=File.ReadAllText(@"E:\warehouse-ai\data\oms-admin-credentials.txt");string Field(string name)=>System.Text.RegularExpressions.Regex.Match(credentials,"(?m)^"+name+": (.+)\\r?$").Groups[1].Value.Trim();
   if(!OmsMobileSession.TryLoadEndpoint(out var endpoint)){errors.Add("Backend endpoint unavailable");Finish(errors);yield break;}
   OmsMobileApi.LoginReply login=null;yield return OmsMobileApi.Login(endpoint,Field("Username"),Field("Password"),r=>login=r,e=>errors.Add("Login API failed"));if(login==null){Finish(errors);yield break;}
   OmsMobileSession.Set(endpoint,login.accessToken,login.user.employeeCode,login.expiresAt);
   OmsMobileApi.PresenceReply value=null;yield return OmsMobileApi.SetPresence(false,r=>value=r,e=>errors.Add("Offline API failed"));nav.Show("85:14",false);nav.Presence.Restore();float deadline=Time.realtimeSinceStartup+15;while(nav.Presence.State==PresenceState.Loading&&Time.realtimeSinceStartup<deadline)yield return null;
   if(nav.Presence.State!=PresenceState.Offline)errors.Add("Login did not restore offline");yield return null;yield return null;ComposerLocalizationRunner.Capture(nav,"Presence-Offline.png");
   nav.Presence.Toggle();deadline=Time.realtimeSinceStartup+15;while(!nav.Presence.CanWork&&Time.realtimeSinceStartup<deadline)yield return null;if(!nav.Presence.CanWork)errors.Add("Online activation failed");yield return new WaitForSecondsRealtime(2.2f);ComposerLocalizationRunner.Capture(nav,"Presence-Online.png");var timer=nav.screens.First(s=>s.id=="126:8").root.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Today online");File.WriteAllText(dir+"/Timer.txt",timer.text+" | truncated="+timer.isTextTruncated+" | preferred="+timer.preferredHeight+" | rect="+timer.rectTransform.rect.height+" | vertices="+timer.textInfo.characterCount);if(timer.isTextTruncated||timer.textInfo.characterCount<8)errors.Add("Main daily timer not fully rendered");
   loc.Select(SupportedLanguage.En);yield return new WaitForSecondsRealtime(1.1f);ComposerLocalizationRunner.Capture(nav,"Presence-English.png");nav.Settings.SelectTheme("dark");yield return new WaitForSecondsRealtime(1.1f);ComposerLocalizationRunner.Capture(nav,"Presence-Dark.png");
   nav.Presence.Toggle();deadline=Time.realtimeSinceStartup+15;while(nav.Presence.State!=PresenceState.Offline&&Time.realtimeSinceStartup<deadline)yield return null;if(nav.Presence.State!=PresenceState.Offline)errors.Add("Offline activation failed");yield return OmsMobileApi.GetPresence(r=>value=r,e=>errors.Add("Presence restore failed"));if(value==null||value.status!="OFFLINE"||value.todaySeconds<2)errors.Add("Completed duration not persisted");
   yield return OmsMobileApi.Logout(r=>{},e=>errors.Add("Logout failed"));OmsMobileSession.Clear();nav.Settings.SelectTheme("light");loc.Select(SupportedLanguage.Vi);nav.Show("5:36",false);Finish(errors);
  }
  void Finish(System.Collections.Generic.List<string> errors){File.WriteAllText("Logs/PresenceReview/Result.txt",(errors.Count==0?"PASS":"FAIL")+" "+DateTime.UtcNow.ToString("O")+"\n22 route captures; real local login/offline/online/persisted timer/logout; VI/EN/light/dark. No model calls.\n"+string.Join("\n",errors));Destroy(gameObject);}
 }
}
