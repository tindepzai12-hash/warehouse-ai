using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class ImportedUiReview
    {
        const string Request="Library/JarVisReviewAllUi.request";
        static ImportedUiReview()
        {
            EditorApplication.delayCall+=CheckRequest;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("JarVisReviewAllUi",false))
                {SessionState.SetBool("JarVisReviewAllUi",false);new GameObject("Imported UI review").AddComponent<ImportedUiReviewRunner>();}
            };
        }
        static void CheckRequest()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
                EditorApplication.delayCall+=CheckRequest;return;
            }
            File.Delete(Request);Run();
        }
        [MenuItem("Tools/JarVis/Review All Imported UI")]
        public static void Run()
        {
            if(UnityEngine.Object.FindAnyObjectByType<ImportedFigmaNavigation>()==null)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("JarVisReviewAllUi",true);EditorApplication.isPlaying=true;
        }
    }
    public sealed class ImportedUiReviewRunner:MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            var nav=UnityEngine.Object.FindAnyObjectByType<ImportedFigmaNavigation>();
            var canvas=nav.GetComponent<Canvas>();var camera=UnityEngine.Object.FindAnyObjectByType<Camera>();
            var mode=canvas.renderMode;var previousCamera=canvas.worldCamera;var previousTarget=camera.targetTexture;
            var target=new RenderTexture(824,1830,24);var image=new Texture2D(824,1830,TextureFormat.RGBA32,false);
            var errors=new System.Collections.Generic.List<string>();int fieldsChecked=0;
            string footerSignature=null;
            Directory.CreateDirectory("Logs/UiReview");
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;
            foreach(var screen in nav.screens)
            {
                nav.Show(screen.id,false);yield return null;
                nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();Canvas.ForceUpdateCanvases();camera.Render();
                if(nav.BottomNavigation!=null&&nav.BottomNavigation.activeInHierarchy)
                {
                    var coordinates=new System.Collections.Generic.List<string>();
                    foreach(var node in nav.BottomNavigation.GetComponentsInChildren<FigmaNode>(false))
                        if((node.nodeName??"").StartsWith("tab-")||node.nodeName=="center-scan-btn")
                        {
                            var corners=new Vector3[4];node.rectTransform.GetWorldCorners(corners);
                            coordinates.Add(node.nodeName+":"+string.Join("/",corners.Select(c=>RectTransformUtility.WorldToScreenPoint(camera,c).ToString("F3"))));
                            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,node.rectTransform.TransformPoint(node.rectTransform.rect.center))};
                            var hits=new System.Collections.Generic.List<RaycastResult>();canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                            if(hits.Count==0||ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=node.gameObject)
                                errors.Add(screen.id+": blocked footer action "+node.nodeName);
                        }
                    string signature=string.Join(";",coordinates);
                    if(footerSignature==null)footerSignature=signature;
                    else if(signature!=footerSignature)errors.Add(screen.id+": bottom tab position changed");
                }
                foreach(var rect in screen.root.GetComponentsInChildren<RectTransform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(rect.gameObject)>0)errors.Add(screen.id+": missing script "+rect.name);
                foreach(var field in screen.root.GetComponentsInChildren<TMP_InputField>(false))
                {
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,field.textViewport.TransformPoint(field.textViewport.rect.center))};
                    var hits=new System.Collections.Generic.List<RaycastResult>();canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    if(hits.Count==0 || hits[0].gameObject.GetComponentInParent<TMP_InputField>()!=field){errors.Add(screen.id+": blocked input "+field.name);continue;}
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    yield return null;yield return null;
                    string before=field.text;
                    field.text="";
                    foreach(char ch in "NV-01234")field.ProcessEvent(new Event{type=EventType.KeyDown,character=ch});
                    if(!field.isFocused || field.text!="NV-01234")errors.Add(screen.id+": input typing failed "+field.name);
                    else if(field.textComponent.characterSpacing!=0 || field.textComponent.wordSpacing!=0 || field.textComponent.transform.localScale!=Vector3.one)
                        errors.Add(screen.id+": distorted input text "+field.name);
                    else fieldsChecked++;
                    field.text=before;field.DeactivateInputField();
                }
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();
                File.WriteAllBytes("Logs/UiReview/"+screen.id.Replace(':','-')+".png",image.EncodeToPNG());
                if(screen.id=="5:36")
                {
                    var loginField=screen.root.GetComponentsInChildren<TMP_InputField>(false).First(f=>f.contentType!=TMP_InputField.ContentType.Password);
                    loginField.text="NV-01234";loginField.textComponent.ForceMeshUpdate();
                    float width=loginField.textComponent.preferredWidth;
                    if(width<30 || width>140)errors.Add("Login glyph spacing is invalid: "+width);
                    yield return null;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();File.WriteAllBytes("Logs/UiReview/Login-Typed.png",image.EncodeToPNG());
                    loginField.text="";
                }
                if(screen.id=="5:1216")
                {
                    var chat=screen.root.GetComponent<ImportedJarvisChat>();
                    if(chat==null)errors.Add("AI chat not configured");
                    else
                    {
                        chat.AddMessage("5S trong kho là gì vậy JarVis?",true);
                        string liveFile=@"E:\warehouse-ai\logs\mobile-ai-live.json";
                        if(File.Exists(liveFile))
                        {
                            var reply=JsonUtility.FromJson<JarVis.Warehouse.Backend.OmsMobileApi.JarvisReply>(File.ReadAllText(liveFile));
                            chat.AddMessage(reply.message,false);
                            yield return null;yield return null;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                            image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();File.WriteAllBytes("Logs/UiReview/AI-RealReply.png",image.EncodeToPNG());
                        }
                        chat.AddMessage(string.Join("\n",Enumerable.Repeat("Kiểm tra cuộn nội dung dài: Sàng lọc, Sắp xếp, Sạch sẽ, Săn sóc, Sẵn sàng.",24)),false);
                        yield return null;yield return null;Canvas.ForceUpdateCanvases();
                        var scroller=screen.root.GetComponentsInChildren<ScrollRect>().FirstOrDefault(s=>s.content!=null&&s.content.name=="Messages");
                        if(scroller==null||scroller.content.rect.height<=scroller.viewport.rect.height)errors.Add("AI long reply is not scrollable");
                        camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();
                        File.WriteAllBytes("Logs/UiReview/AI-LongReply.png",image.EncodeToPNG());
                    }
                }
            }
            bool liveChatVerified=false;
            if(File.Exists("Library/JarVisVerifyLiveChat.request"))
            {
                File.Delete("Library/JarVisVerifyLiveChat.request");
                nav.Show("5:36",false);yield return null;
                string credentials=File.ReadAllText(@"E:\warehouse-ai\data\oms-admin-credentials.txt");
                string username=System.Text.RegularExpressions.Regex.Match(credentials,@"(?m)^Username: (.+)\r?$").Groups[1].Value.Trim();
                string password=System.Text.RegularExpressions.Regex.Match(credentials,@"(?m)^Password: (.+)\r?$").Groups[1].Value.Trim();
                var loginRoot=nav.screens.First(s=>s.id=="5:36").root;
                var nodes=loginRoot.GetComponentsInChildren<FigmaNode>(true);
                nodes.First(n=>n.nodeId=="5:64").GetComponent<TMP_InputField>().text=username;
                nodes.First(n=>n.nodeId=="5:69").GetComponent<TMP_InputField>().text=password;
                var loginButton=nodes.First(n=>n.nodeId=="5:74").gameObject;
                ExecuteEvents.Execute(loginButton,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                float deadline=Time.realtimeSinceStartup+12;
                while(nav.CurrentScreen!="5:1216"&&Time.realtimeSinceStartup<deadline)yield return null;
                if(nav.CurrentScreen!="5:1216")errors.Add("Real admin login did not open JarVis chat");
                else
                {
                    var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();
                    chat.Input.text="5S trong kho là gì vậy JarVis? Trả lời ngắn gọn 5 ý bằng tiếng Việt.";
                    yield return null;Canvas.ForceUpdateCanvases();
                    var rect=(RectTransform)chat.SendButton.transform;
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center))};
                    var hits=new System.Collections.Generic.List<RaycastResult>();canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    if(hits.Count==0||ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)!=chat.SendButton.gameObject)errors.Add("Send button raycast is blocked");
                    else ExecuteEvents.Execute(chat.SendButton.gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    if(!chat.IsSending)errors.Add("Send click did not start request");
                    yield return new WaitForSecondsRealtime(.3f);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();File.WriteAllBytes("Logs/UiReview/AI-Thinking.png",image.EncodeToPNG());
                    deadline=Time.realtimeSinceStartup+70;
                    while(chat.IsSending&&Time.realtimeSinceStartup<deadline)yield return null;
                    if(chat.IsSending||chat.StatusText!="OpenAI • Đã kết nối")errors.Add("Live OpenAI chat failed: "+chat.StatusText);
                    else liveChatVerified=true;
                    Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                    image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();File.WriteAllBytes("Logs/UiReview/AI-Live-Unity.png",image.EncodeToPNG());
                }
            }
            camera.targetTexture=previousTarget;canvas.renderMode=mode;canvas.worldCamera=previousCamera;RenderTexture.active=null;
            Destroy(image);Destroy(target);nav.Show("5:36",false);
            File.WriteAllText("Logs/UiReview/result.txt",(errors.Count==0?"PASS":"FAIL")+": "+nav.screens.Length+" screens captured; "+fieldsChecked+" inputs; bottom tabs keep identical pixel coordinates.\nLive admin login + send button + OpenAI reply: "+liveChatVerified+"\n"+string.Join("\n",errors)+"\nPhysical Android keyboard/camera not tested.\n");
            Destroy(gameObject);
            if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);
        }
    }
}
