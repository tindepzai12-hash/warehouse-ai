#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
namespace ProjectBootstrap
{
    public static class AssistantPopupProof
    {
        const string Dir=@"C:\Users\Windows\Documents\ChatGPT\jarvis\popup-tracking-test-20261121";
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,F).GetValue(o);
        static void Set(object o,string field,object value)=>o.GetType().GetField(field,F).SetValue(o,value);
        static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,F).Invoke(o,args);
        public static void Run()
        {
            Directory.CreateDirectory(Dir);var checks=new List<string>();
            void Check(bool value,string name){checks.Add((value?"PASS ":"FAIL ")+name);if(!value)throw new InvalidOperationException(name);}
            var recordField=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
            var oldRecord=recordField.GetValue(null);var oldLanguage=JarvisLocalization.CurrentLanguage;
            Canvas canvas=null;GameObject fixture=null;
            try
            {
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);JarvisLocalization.SetLanguage(SupportedLanguage.Vi);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
                var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();
                var origin=UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                var camera=origin.Camera;var cm=camera.GetComponent<ARCameraManager>();
                var qr=guide.qrLocalization;
                if(qr==null){qr=guide.gameObject.AddComponent<RoompresentQrLocalization>();qr.enabled=false;guide.qrLocalization=qr;}
                // Inactive reference fixture makes this a non-null retention check, not a claim of native localization.
                fixture=new GameObject("Editor-only reference fixture - no native tracking");fixture.SetActive(false);
                fixture.transform.SetPositionAndRotation(new Vector3(.7f,.1f,-1.2f),Quaternion.Euler(0,17,0));
                var anchor=fixture.AddComponent<ARAnchor>();Set(qr,"anchor",anchor);Set(qr,"referenceFrame",fixture.transform);
                recordField.SetValue(null,new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true,preferredStop="C"});Set(guide,"activeStop","C");
                var fontPath=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.Contains("BeVietnamPro Figma SDF")&&!p.Contains("Fallback"));
                Call(guide,"MakeUi",AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath));
                canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
                var hud=canvas.GetComponentInChildren<RoompresentDeliveryHud>();hud.Refresh();
                foreach(string field in new[]{"start","proof","success","scanFrame"})Get<RectTransform>(hud,field).gameObject.SetActive(false);
                Get<RectTransform>(hud,"dock").gameObject.SetActive(true);
                var assistant=canvas.GetComponentInChildren<RoompresentArAssistant>();
                var mapMatrix=guide.mapRoot.localToWorldMatrix;var mapParent=guide.mapRoot.parent;var frame=qr.Frame;var refMatrix=frame.localToWorldMatrix;
                var originMatrix=origin.transform.localToWorldMatrix;var cameraMatrix=camera.transform.localToWorldMatrix;
                bool sessionOn=session.enabled,cmOn=cm.enabled,originOn=origin.gameObject.activeSelf;
                string progress=JsonUtility.ToJson(RoompresentDeliveryMission.Current);int scene=guide.gameObject.scene.handle;
                var registration=File.ReadAllBytes("Assets/Resources/RoompresentCheckedRegistration.json");
                void Retained(string step)
                {
                    Check(session==UnityEngine.Object.FindFirstObjectByType<ARSession>()&&session.enabled==sessionOn,step+": same ARSession and enabled state");
                    Check(cm.enabled==cmOn&&camera.transform.localToWorldMatrix==cameraMatrix&&origin.transform.localToWorldMatrix==originMatrix&&origin.gameObject.activeSelf==originOn,step+": same camera and XR origin");
                    Check(guide.qrLocalization==qr&&qr.Frame==frame&&frame.localToWorldMatrix==refMatrix&&Get<ARAnchor>(qr,"anchor")==anchor,step+": non-null reference and anchor unchanged (fixture)");
                    Check(guide.mapRoot.parent==mapParent&&guide.mapRoot.localToWorldMatrix==mapMatrix,step+": map pose unchanged");
                    Check(JsonUtility.ToJson(RoompresentDeliveryMission.Current)==progress&&guide.SelectedStop=="C"&&guide.gameObject.scene.handle==scene,step+": mission, selected C and scene unchanged");
                }
                void Shot(string name)
                {
                    Retained(name);
                    foreach(var size in new[]{new Vector2Int(1080,2400),new Vector2Int(1080,1920)})
                        typeof(Assistant043Review).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{canvas,size,Path.Combine(Dir,name+"-"+size.y+".png")});
                }
                Shot("01-guidance");
                Get<RectTransform>(assistant,"pill").GetComponent<Button>().onClick.Invoke();
                Check(Get<RectTransform>(assistant,"widget").gameObject.activeSelf,"Launcher button opens actual widget");Shot("02-widget");
                Get<RectTransform>(assistant,"widget").GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>()?.text=="Nhắn tin").onClick.Invoke();
                Check(Get<RectTransform>(assistant,"chat").gameObject.activeSelf,"Chat button opens actual overlay");Shot("03-chat");
                var input=Get<TMP_InputField>(assistant,"input");input.text="Gửi lại ảnh bản đồ 2D";Get<Button>(assistant,"send").onClick.Invoke();
                var content=Get<RectTransform>(assistant,"content");
                int Attachments()=>content.Cast<Transform>().Count(c=>c.name=="2D map attachment");
                Check(Attachments()==1&&content.GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null),"Actual Send button attaches packaged 2D image without provider");Shot("04-map-message");
                content.Cast<Transform>().First(c=>c.name=="2D map attachment").GetComponent<Button>().onClick.Invoke();
                Check(Get<RectTransform>(assistant,"mapView").gameObject.activeSelf,"Tapping message image enlarges map");Shot("05-map-expanded");
                Get<RectTransform>(assistant,"mapView").GetComponentInChildren<Button>().onClick.Invoke();
                Check(!Get<RectTransform>(assistant,"mapView").gameObject.activeSelf&&Get<RectTransform>(assistant,"chat").gameObject.activeSelf,"Closing enlarged map keeps chat");
                Set(assistant,"busy",true);input.text="Gửi lại ảnh 2D";Get<Button>(assistant,"send").onClick.Invoke();
                Check(Attachments()==2&&Get<bool>(assistant,"busy"),"Resend appends second image and preserves active request");Set(assistant,"busy",false);Shot("06-map-resent");
                assistant.OpenWidget();Get<RectTransform>(assistant,"widget").GetComponentsInChildren<Button>().Single(b=>b.name=="Report incident").onClick.Invoke();Shot("07-report");
                assistant.Close();Check(!Get<RectTransform>(assistant,"chat").gameObject.activeSelf&&Get<RectTransform>(assistant,"pill").gameObject.activeSelf,"Close returns to collapsed launcher");Shot("08-closed");
                Check(registration.SequenceEqual(File.ReadAllBytes("Assets/Resources/RoompresentCheckedRegistration.json")),"Saved A/B/C registration unchanged");
                checks.Add("LIMIT: Editor UI test; inactive reference fixture has no native Tracking state. No camera pose, walking, microphone, speech API or physical tracking stability is certified. No scene save or mission disk write.");
                File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),new[]{"PASS · "+DateTime.UtcNow.ToString("O")}.Concat(checks));
                Debug.Log("[AssistantPopupProof] PASS actual UI button flow, map send/resend and spatial ownership checks");
            }
            catch(Exception error){File.WriteAllLines(Path.Combine(Dir,"RESULT.txt"),checks.Concat(new[]{"FAIL "+error}));Debug.LogException(error);EditorApplication.Exit(1);return;}
            finally
            {
                recordField.SetValue(null,oldRecord);JarvisLocalization.SetLanguage(oldLanguage);
                if(canvas!=null)UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                if(fixture!=null)UnityEngine.Object.DestroyImmediate(fixture);
            }
            EditorApplication.Exit(0);
        }
    }
}
#endif
