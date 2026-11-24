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
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    public static class Assistant043Review
    {
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
        static void Check(bool test,string message){if(!test)throw new InvalidOperationException(message);}
        public static void Run()
        {
            DeliveryWorkflowReview.RunSaved();
            EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
            var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();guide.surface.RemoveData();guide.surface.AddData();
            string root="Logs/RoompresentVPS/Assistant043/";Directory.CreateDirectory(root);
            var map=RoompresentMap2D.Create(guide);Check(map!=null&&map.width==384&&map.height==512,"Actual floor schematic unavailable");File.WriteAllBytes(root+"Roompresent-2D.png",map.EncodeToPNG());UnityEngine.Object.DestroyImmediate(map);
            var task=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true};
            string context=RoompresentArAssistant.TaskContext(task,"C",false);
            Check(context.Contains("B=done,C=pending")&&context.Contains("LOST; position unknown")&&context.Contains(RoompresentDeliveryRecord.A1)&&context.Contains(RoompresentDeliveryRecord.A2),"Wrong mission/tracking context");
            Check(RoompresentArAssistant.WantsMap("I lost tracking, what should I do?",false)&&RoompresentArAssistant.WantsMap("Tôi bị mất tracking",false)&&!RoompresentArAssistant.WantsMap("What book goes to B?",true),"Map intent wrong");
            var fontPath=AssetDatabase.FindAssets("t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.Contains("BeVietnamPro Figma SDF")&&!p.Contains("Fallback"));
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            var language=JarvisLocalization.CurrentLanguage;
            try
            {
                foreach(var lang in new[]{SupportedLanguage.Vi,SupportedLanguage.En})
                {
                    JarvisLocalization.SetLanguage(lang);Call(guide,"MakeUi",font);
                    var canvas=guide.gameObject.scene.GetRootGameObjects().Single(o=>o.name=="Small room guidance").GetComponent<Canvas>();
                    var assistant=canvas.GetComponentInChildren<RoompresentArAssistant>();
                    var hud=canvas.GetComponentInChildren<RoompresentDeliveryHud>();
                    // Synthetic UI states only; never claim an acquired camera position in this review.
                    foreach(var r in new[]{"start","dock","proof","success","scanFrame"})Field<RectTransform>(hud,r).gameObject.SetActive(false);
                    var transformBefore=guide.mapRoot.localToWorldMatrix;var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();bool sessionEnabled=session.enabled;
                    string dir=root+(lang==SupportedLanguage.Vi?"VI/":"EN/");Directory.CreateDirectory(dir);
                    foreach(var size in new[]{new Vector2Int(1080,2400),new Vector2Int(1080,1920)})
                    {
                        Render(canvas,size,dir+"collapsed-"+size.y+".png");assistant.OpenWidget();Render(canvas,size,dir+"voice-"+size.y+".png");
                        assistant.OpenChat();Call(assistant,"AddMessage",lang==SupportedLanguage.Vi?"Tôi đã giao A1 tại B nhưng mất tracking khi đi tới C. JarVis có thể giúp tìm đường và nhắc lại bước xác nhận không?":"I've delivered A1 at B, but lost tracking on the way to C. Can JarVis help me find the route and explain the verification steps?",true);
                        Render(canvas,size,dir+"chat-"+size.y+".png");
                        assistant.AddMap();Render(canvas,size,dir+"map-message-"+size.y+".png");
                        Field<RawImage>(assistant,"mapFull").texture=Field<Texture2D>(assistant,"map");Field<RectTransform>(assistant,"mapView").gameObject.SetActive(true);Field<RectTransform>(assistant,"mapView").SetAsLastSibling();Render(canvas,size,dir+"map-expanded-"+size.y+".png");
                        assistant.OpenReport();Field<TMP_InputField>(assistant,"incident").text=lang==SupportedLanguage.Vi?"Camera bị mờ khi tôi đến kệ C. Tôi vẫn giữ sách A2 và chưa xác nhận giao.":"The camera is blurred at cart C. I still have A2 and haven't confirmed delivery.";Render(canvas,size,dir+"report-"+size.y+".png");
                        assistant.Close();Check(!assistant.MediaBusy,"Close retained recording/request state");
                    }
                    Check(guide.mapRoot.localToWorldMatrix==transformBefore&&session.enabled==sessionEnabled,"Assistant changed AR session/map transform");
                    UnityEngine.Object.DestroyImmediate(canvas.gameObject);
                }
            }
            finally{JarvisLocalization.SetLanguage(language);}
            File.WriteAllText(root+"result.txt","PASS existing ordered delivery, real registration and B/C handoff regressions. PASS task context includes current completion and unknown position when tracking lost; map intent. PASS schematic generated from actual baked floor and saved access points. PASS text bounds and VI/EN UI renders 1080x2400 / 1080x1920: launcher, voice card, chat, map attachment, enlarged map, report. Panel open/close retains ARSession enabled state and map transform. No real speech, provider call, keyboard or walking simulated as success; these require Samsung acceptance.\n");
            Debug.Log("[Assistant043Review] PASS");
        }
        static void Render(Canvas canvas,Vector2Int size,string output)
        {
            var go=new GameObject("Assistant review camera",typeof(Camera));var camera=go.GetComponent<Camera>();
            var rt=new RenderTexture(size.x,size.y,24);rt.Create();var previous=RenderTexture.active;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");
                camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.17f,.21f);camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();
                var assistant=canvas.GetComponentInChildren<RoompresentArAssistant>();Call(assistant,"LayoutChat");Canvas.ForceUpdateCanvases();
                foreach(var effect in canvas.GetComponentsInChildren<PrototypeGlassShadow>(true))typeof(PrototypeGlassShadow).GetMethod("LateUpdate",Flags).Invoke(effect,null);
                foreach(var surface in canvas.GetComponentsInChildren<UniformUiSurface>(true))typeof(UniformUiSurface).GetMethod("LateUpdate",Flags).Invoke(surface,null);
                Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Check(!text.isTextOverflowing,"Assistant UI text overflows: "+text.text);}
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(output,image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
#endif
