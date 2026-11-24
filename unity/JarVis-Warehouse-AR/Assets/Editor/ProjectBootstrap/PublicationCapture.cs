#if UNITY_EDITOR
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
    [InitializeOnLoad] public static class PublicationCapture
    {
        static PublicationCapture(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarvisPublicationCapture",false)){SessionState.SetBool("JarvisPublicationCapture",false);new GameObject("Publication capture only").AddComponent<PublicationCaptureRunner>();}};}
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("JarvisPublicationCapture",true);EditorApplication.isPlaying=true;}
    }
    public sealed class PublicationCaptureRunner:MonoBehaviour
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\linkedin-jarvis-unity-20261121";
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        ImportedFigmaNavigation nav;
        readonly List<string> errors=new List<string>();
        readonly List<string> manifest=new List<string>{"file\tmode\tvisible_text"};
        readonly Dictionary<string,string> prefs=new Dictionary<string,string>();
        readonly string[] keys={"JarVis.Theme","JarVis.Language"};
        readonly Dictionary<string,int?> ints=new Dictionary<string,int?>();
        Dictionary<string,byte[]> guards;
        object originalRecord;
        FieldInfo current;
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
        static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,F).Invoke(o,a);
        void Error(string m,string t,LogType ty){if(ty==LogType.Exception||ty==LogType.Error)errors.Add(m.Split('\n')[0]);}
        IEnumerator Wait(){yield return null;yield return null;yield return new WaitForSecondsRealtime(.15f);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(Path.Combine(Dir,"02-anh-goc-unity"));Application.logMessageReceived+=Error;
            foreach(var k in keys)prefs[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetString(k):null;
            foreach(var k in new[]{"JarVis.TextSize","JarVis.ReduceGlass","JarVis.ReduceMotion"})ints[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetInt(k):(int?)null;
            string disk=Path.Combine(Application.persistentDataPath,"jarvis-roompresent-quick-delivery-v1.json");
            guards=new[]{"Assets/Scenes/JarVisFigmaApp.unity","Assets/Scenes/Roompresent_VPS.unity","Assets/Resources/RoompresentCheckedRegistration.json",disk}.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            current=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.NonPublic|BindingFlags.Static);originalRecord=current.GetValue(null);
            yield return null;yield return null;
            nav=FindAnyObjectByType<ImportedFigmaNavigation>();
            if(nav==null||OmsMobileSession.IsReady){errors.Add("Authenticated session or missing app: capture cancelled");Finish();yield break;}
            nav.Presence.StopAllCoroutines();nav.Presence.enabled=false;
            // UI-only fixture; no warehouse quantities, API task or inventory records are invented.
            Call(nav.Presence,"Accept",new OmsMobileApi.PresenceReply{status="ONLINE",todaySeconds=0,sessionSeconds=0,serverNow=DateTimeOffset.UtcNow.ToString("O"),nextResetAt=DateTimeOffset.UtcNow.AddHours(8).ToString("O"),leaseUntil=DateTimeOffset.UtcNow.AddHours(1).ToString("O")});
            current.SetValue(null,new RoompresentDeliveryRecord{accepted=true,preferredStop="B"});
            var locale=nav.GetComponent<ImportedAppLocalization>();locale.Select(SupportedLanguage.Vi);
            foreach(var scanner in nav.GetComponentsInChildren<ImportedCameraScanner>(true))scanner.enabled=false;
            foreach(var mode in new[]{"light","dark"})
            {
                nav.Settings.SelectTheme(mode);nav.Settings.SelectSize(1);
                foreach(var pair in new[]{new[]{"5:11","splash"},new[]{"5:36","login"},new[]{"126:8","home"},new[]{"48:244","mission"},new[]{"5:780","stock"},new[]{"5:1308","settings"},new[]{"prototype:activity","activity"},new[]{"5:1216","chat-main"}})
                {
                    nav.Show(pair[0],false);yield return Wait();Capture(pair[1],mode);
                }
                nav.Show("5:331",false);yield return Wait();
                foreach(var code in new[]{RoompresentDeliveryRecord.A1,RoompresentDeliveryRecord.A2})
                {
                    RoompresentScanPopup.Show(nav,code,code==RoompresentDeliveryRecord.A2,()=>{});yield return Wait();Capture(code==RoompresentDeliveryRecord.A1?"book-A1-popup":"book-A2-popup",mode);
                    foreach(var dialog in nav.GetComponentsInChildren<PrototypeDialog>(true)){dialog.gameObject.SetActive(false);Destroy(dialog.gameObject);}yield return Wait();
                }
            }
            Finish();
        }
        void Capture(string name,string mode)
        {
            string file=name+"-"+mode+".png";PrototypeParityRunner.Capture(nav,"__publication.png",1080,2400);File.Copy("Logs/PrototypeParity/__publication.png",Path.Combine(Dir,"02-anh-goc-unity",file),true);
            var labels=nav.GetComponentsInChildren<TMP_Text>().Where(t=>t.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(t.text)).Select(t=>t.text.Replace('\t',' ').Replace('\n',' '));manifest.Add(file+"\t"+mode+"\t"+string.Join(" | ",labels));
        }
        void Finish()
        {
            if(current!=null)current.SetValue(null,originalRecord);
            foreach(var k in keys){if(prefs.TryGetValue(k,out var v)&&v!=null)PlayerPrefs.SetString(k,v);else PlayerPrefs.DeleteKey(k);}
            foreach(var kv in ints){if(kv.Value.HasValue)PlayerPrefs.SetInt(kv.Key,kv.Value.Value);else PlayerPrefs.DeleteKey(kv.Key);}
            foreach(var pair in guards)if(pair.Value==null?File.Exists(pair.Key):!File.Exists(pair.Key)||!pair.Value.SequenceEqual(File.ReadAllBytes(pair.Key)))errors.Add("Changed protected source/data: "+Path.GetFileName(pair.Key));
            Application.logMessageReceived-=Error;
            File.WriteAllLines(Path.Combine(Dir,"evidence","main-capture-result.txt"),new[]{errors.Count==0?"PASS":"FAIL","Current Unity runtime UI; local Editor state only; no warehouse/task writes, provider/model requests, APK install or device capture. No claim of live positioning."}.Concat(errors));
            File.WriteAllLines(Path.Combine(Dir,"evidence","main-manifest.tsv"),manifest);EditorApplication.Exit(errors.Count==0?0:1);
        }
    }
}
#endif
