#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Demo2026;
using JarVis.Warehouse.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class JarVisDemoPlayReview
    {
        static JarVisDemoPlayReview(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisSystemPlayReview",false)){SessionState.SetBool("JarVisSystemPlayReview",false);new GameObject("Editor-only demo interaction review").AddComponent<JarVisDemoPlayRunner>();}};}
        public static void Run(){EditorSceneManager.OpenScene(JarVisSystemDemoReview.ScenePath);SetPortrait();SessionState.SetBool("JarVisSystemPlayReview",true);EditorApplication.isPlaying=true;}
        public static void SetPortrait()
        {
            var a=typeof(Editor).Assembly;var f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
            var t=a.GetType("UnityEditor.GameViewSizes");var o=t.GetProperty("instance",f|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);var group=t.GetMethod("GetGroup",f).Invoke(o,new[]{t.GetProperty("currentGroupType",f).GetValue(o)});var gt=group.GetType();int count=(int)gt.GetMethod("GetTotalCount",f).Invoke(group,null),selected=-1;
            for(int i=0;i<count;i++){var s=gt.GetMethod("GetGameViewSize",f).Invoke(group,new object[]{i});if((int)s.GetType().GetProperty("width",f).GetValue(s)==432&&(int)s.GetType().GetProperty("height",f).GetValue(s)==960){selected=i;break;}}
            if(selected<0){var s=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),f,null,new object[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),432,960,"JarVis demo 432x960"},null);gt.GetMethod("AddCustomSize",f).Invoke(group,new[]{s});selected=count;}
            var game=a.GetType("UnityEditor.GameView");game.GetProperty("selectedSizeIndex",f).SetValue(EditorWindow.GetWindow(game),selected);
        }
        public static void Export()
        {
            AssetDatabase.ExportPackage(new[]{"Assets/JarVis/Demos/SystemReview","Assets/Resources/JarVisReview","Assets/Editor/ProjectBootstrap/JarVisSystemDemoReview.cs","Assets/Editor/ProjectBootstrap/JarVisDemoPlayReview.cs"},Path.Combine(JarVisSystemDemoReview.Output,"JarVis-UI-Demo.unitypackage"),ExportPackageOptions.Recurse|ExportPackageOptions.IncludeDependencies);
            File.WriteAllText(Path.Combine(JarVisSystemDemoReview.Output,"EXPORT.txt"),"PASS "+DateTime.UtcNow.ToString("O"));EditorApplication.Exit(0);
        }
    }
    public sealed class JarVisDemoPlayRunner : MonoBehaviour
    {
        const string Dir=JarVisSystemDemoReview.Output;
        readonly List<string> checks=new List<string>(); readonly List<string> errors=new List<string>();JarVisSystemDemo view;
        bool failed;
        void Log(string m,string trace,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(m);}
        void Check(bool value,string message){checks.Add((value?"PASS ":"FAIL ")+message);if(!value)failed=true;}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Log;yield return null;yield return null;
            view=FindFirstObjectByType<JarVisSystemDemo>();Check(view!=null&&view.Rehearsal,"Actual demo prefab starts in Play Mode");
            view.StartMission();yield return new WaitForSecondsRealtime(.1f);
            var sweep=view.GetComponentInChildren<JarVisScanSweep>();var line=sweep.transform.Find("JarVis sweep clip/Scan line").GetComponent<RectTransform>();var before=line.anchoredPosition;
            yield return new WaitForSecondsRealtime(.35f);Check(Vector2.Distance(before,line.anchoredPosition)>2,"Barcode scan line moves with actual unscaled runtime frames");Check(!view.Record.Loaded,"Moving sweep never fabricates recognition progress");
            yield return Motion("barcode",72);
            Check(view.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",false,0),"A1 scan creates real popup in Play");var popup=view.ModalGroup;float alpha=popup.alpha;
            yield return Motion("popup",36);Check(popup.alpha>.99f&&popup.alpha>=alpha,"Popup entrance settles opacity");
            Click("OK · Tiếp tục");yield return null;Check(view.ScreenId=="scan-products"&&!view.AwaitingAck,"Popup OK allows next scan");Check(view.SubmitScan(RoompresentDeliveryRecord.A2,"RearCamera",false,0),"A2 accepted in Play");yield return new WaitForSecondsRealtime(.35f);Click("OK · Tiếp tục");yield return null;Check(view.ScreenId=="scan-A","Both products open QR A in Play");
            yield return Motion("qr",72);
            Check(view.SubmitScan("JWM:MAIN:E","RearCamera",true,.5f),"Explicit QR A pose fixture opens ack");yield return new WaitForSecondsRealtime(.35f);Click("Mở chỉ dẫn AR");yield return null;
            view.Assistant("voice");yield return Motion("voice",72);Check(view.ScreenId=="ar"&&view.TrackingTrusted,"Voice overlay retains AR view state");view.CloseModal();view.BeginDelivery("B");view.SubmitScan("JWM:MAIN:B","RearCamera",true,.5f);yield return new WaitForSecondsRealtime(.35f);Click("OK · Quét barcode");yield return null;view.SubmitScan(RoompresentDeliveryRecord.A1,"RearCamera",true,.5f);yield return null;Check(view.Record.deliveredB&&!view.Record.deliveredC&&view.TrackingTrusted,"B-to-C handoff preserves tracking state in Play");
            Check(errors.Count==0,"No new runtime exceptions/errors");checks.AddRange(errors.Select(e=>"ERROR "+e));
            File.WriteAllLines(Path.Combine(Dir,"PLAY-RESULT.txt"),new[]{(failed?"FAIL ":"PASS ")+DateTime.UtcNow.ToString("O")}.Concat(checks).Concat(new[]{"LIMIT: explicit Editor inputs and pose fixtures; audio waveform is demo amplitude, not microphone evidence."}));Application.logMessageReceived-=Log;EditorApplication.Exit(failed?1:0);
        }
        void Click(string text)
        {
            Transform p=view.ModalOpen?view.overlayRoot:view.transform;var matches=p.GetComponentsInChildren<Button>().Where(b=>b.interactable&&b.GetComponentInChildren<TMPro.TMP_Text>()?.text==text).ToArray();Check(matches.Length==1,"Button responds "+text);if(matches.Length==1)matches[0].onClick.Invoke();
        }
        IEnumerator Motion(string name,int frames)
        {
            string dir=Path.Combine(Dir,"motion",name);Directory.CreateDirectory(dir);var c=view.GetComponentInParent<Canvas>();
            var field=typeof(JarVisSystemDemoReview).GetField("canvas",BindingFlags.Static|BindingFlags.NonPublic);var method=typeof(JarVisSystemDemoReview).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic);
            field.SetValue(null,c);
            for(int i=0;i<frames;i++)
            {
                // Unscaled sampling preserves native UGUI animation rather than interpolating screenshots.
                var mode=c.renderMode;var cam=c.worldCamera;method.Invoke(null,new object[]{new Vector2Int(432,960),Path.Combine(dir,i.ToString("000")+".png")});c.renderMode=mode;c.worldCamera=cam;
                yield return new WaitForSecondsRealtime(1f/24);
            }
        }
    }
}
#endif
