#if UNITY_EDITOR
using System;
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
using UnityEngine.AI;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class ReturnA044Review
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags).Invoke(o,a);
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        public static void Run()
        {
            Assistant043Review.Run();
            EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
            var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();guide.surface.RemoveData();guide.surface.AddData();
            string output="Logs/RoompresentVPS/ReturnA044/";Directory.CreateDirectory(output);
            foreach(string q in new[]{"gửi lại ảnh 2D","cho tôi bản đồ","gui lai so do","show the 2-D map","Please send the 2 D image again"})
                Check(RoompresentMap2D.IsRequest(q),"Missing map intent: "+q);
            Check(RoompresentMap2D.IsRequest("gui lai anh",true)&&!RoompresentMap2D.IsRequest("gui lai anh")&&!RoompresentMap2D.IsRequest("How do I confirm book A2?"),"Resend context/ordinary question routing");
            var bundled=RoompresentMap2D.LoadReference();Check(bundled!=null&&bundled.width==384&&bundled.height==512,"Bundled offline map missing");UnityEngine.Object.DestroyImmediate(bundled);
            var current=typeof(RoompresentDeliveryMission).GetField("current",BindingFlags.NonPublic|BindingFlags.Static);
            var previous=current.GetValue(null);
            var file=(string)typeof(RoompresentDeliveryMission).GetProperty("FilePath",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            var bytes=File.Exists(file)?File.ReadAllBytes(file):null;
            var transform=guide.mapRoot.localToWorldMatrix;var frame=guide.qrLocalization?.Frame;
            var savedStop=guide.SelectedStop;
            var language=JarvisLocalization.CurrentLanguage;
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.Contains("BeVietnamPro Figma SDF")&&!p.Contains("Fallback")));
            try
            {
                foreach(bool complete in new[]{false,true})
                {
                    var record=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true,deliveredC=complete,preferredStop="C"};current.SetValue(null,record);
                    string before=JsonUtility.ToJson(record);guide.ReturnToStart();
                    Check(guide.ReturningToStart&&guide.SelectedStop=="A"&&!guide.Verification.Active,"Return A cannot be selected after delivery");
                    Check(JsonUtility.ToJson(record)==before,"Return changed saved progress/preferred pending delivery");
                    Check(guide.mapRoot.localToWorldMatrix==transform&&guide.qrLocalization?.Frame==frame,"Return changed spatial reference");
                    foreach(var from in new[]{guide.basinAccess,guide.bagAccess})
                    {
                        var target=(Vector3)Call(guide,"Access","A");Check(Vector3.Distance(target,guide.mapRoot.TransformPoint(guide.startLocal))<.001f,"A resolved to C");
                        object[] args={guide.mapRoot.TransformPoint(from),target,null,0f};
                        Check((bool)Call(guide,"Route",args)&&((NavMeshPath)args[2]).status==NavMeshPathStatus.PathComplete&&(float)args[3]>1,"No actual B/C to A floor route");
                    }
                    if(!complete){guide.SelectDelivery("C");Check(!guide.ReturningToStart&&guide.SelectedStop=="C"&&!record.deliveredC,"Cannot resume pending C");}
                }
                guide.surface.RemoveData(); // Offline resend must work with no active floor mesh or tracking.
                foreach(var lang in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
                {
                    JarvisLocalization.SetLanguage(lang);guide.ReturnToStart();Call(guide,"MakeUi",font);
                    var canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
                    try
                    {
                        var hud=canvas.GetComponentInChildren<RoompresentDeliveryHud>();hud.Refresh();
                        foreach(string field in new[]{"start","proof","success","scanFrame"})Get<RectTransform>(hud,field).gameObject.SetActive(false);
                        Get<RectTransform>(hud,"dock").gameObject.SetActive(true);
                        Check(canvas.GetComponentsInChildren<Button>(true).Count(b=>b.name=="Return to A")==1,"Missing or duplicate Return A");
                        var assistant=canvas.GetComponentInChildren<RoompresentArAssistant>();assistant.OpenChat();
                        Set(assistant,"busy",true);
                        for(int i=0;i<2;i++)
                        {
                            Get<TMP_InputField>(assistant,"input").text=lang==SupportedLanguage.Vi?"gửi lại ảnh 2D":"Send the 2-D map again";
                            Call(assistant,"SendText");
                        }
                        Check(Get<RectTransform>(assistant,"content").Cast<Transform>().Count(t=>t.name=="2D map attachment")==2,"Map resend blocked by missing tracking/provider wait");
                        Check(Get<bool>(assistant,"busy"),"Local map canceled the active request");
                        Set(assistant,"busy",false);Get<TMP_InputField>(assistant,"input").text="";
                        var recorder=Get<VoiceRecorder>(assistant,"recorder");
                        typeof(VoiceRecorder).GetField("<State>k__BackingField",Flags).SetValue(recorder,VoiceRecorderState.Stopped);
                        // No audio fixture: dispatch must finalize/cancel the short recording, never call a provider.
                        Call(assistant,"SendText");Check(recorder.State==VoiceRecorderState.Idle,"Empty text Send did not submit recorded voice");
                        Check(guide.mapRoot.localToWorldMatrix==transform&&guide.qrLocalization?.Frame==frame,"Map resend changed tracking reference");
                        foreach(var size in new[]{new Vector2Int(1080,2400),new Vector2Int(1080,1920)})
                        {
                            string suffix=(lang==SupportedLanguage.Vi?"VI":"EN")+"-"+size.y+".png";
                            assistant.Close();Render(canvas,size,output+"return-A-"+suffix);
                            assistant.OpenChat();Render(canvas,size,output+"map-resend-"+suffix);
                        }
                    }
                    finally{UnityEngine.Object.DestroyImmediate(canvas.gameObject);}
                }
                VerifyMainChat(font);
                File.WriteAllText(output+"result.txt","PASS actual complete NavMesh routes B→A / C→A; return A selectable after partial and full delivery, pending C resumable; progress and spatial references retained. PASS packaged map loaded without active NavMesh; repeated typed requests bypass provider busy state and work with no tracking/session. PASS VI/EN map intent, contextual resend. PASS main-chat map attachments twice and preview close. PASS rendered return controls/chat at 1080x2400 and 1080x1920. Physical Samsung walking, camera and voice acceptance still required.\n");
                Debug.Log("[ReturnA044Review] PASS");
            }
            finally
            {
                guide.Verification.Cancel();Set(guide,"activeStop",savedStop);current.SetValue(null,previous);JarvisLocalization.SetLanguage(language);
                if(bytes!=null)File.WriteAllBytes(file,bytes);else if(File.Exists(file))File.Delete(file);
                guide.surface.AddData();
            }
        }
        static void VerifyMainChat(TMP_FontAsset font)
        {
            var go=new GameObject("Isolated main chat attachment review",typeof(RectTransform));go.SetActive(false);
            try
            {
                var chat=go.AddComponent<ImportedJarvisChat>();Set(chat,"font",font);Set(chat,"content",go.GetComponent<RectTransform>());Set(chat,"screenRect",go.GetComponent<RectTransform>());
                foreach(string field in new[]{"empty","status"}){var label=new GameObject(field,typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);label.GetComponent<TMP_Text>().font=font;Set(chat,field,label.GetComponent<TMP_Text>());}
                Call(chat,"SendReferenceMap",true);Call(chat,"SendReferenceMap",false);
                Check(go.GetComponentsInChildren<RawImage>(true).Count()==2,"Main chat did not resend image");
                Call(chat,"ShowMapPreview");var preview=go.transform.Find("Roompresent map preview");Check(preview!=null,"Main chat image cannot enlarge");
                preview.GetComponentInChildren<Button>(true).onClick.Invoke();
                Check(go.transform.Find("Roompresent map preview")==null&&Get<Texture2D>(chat,"mapPreviewTexture")==null,"Map preview close retained its overlay/texture");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Render(Canvas canvas,Vector2Int size,string output)=>typeof(Assistant043Review).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{canvas,size,output});
    }
}
#endif
