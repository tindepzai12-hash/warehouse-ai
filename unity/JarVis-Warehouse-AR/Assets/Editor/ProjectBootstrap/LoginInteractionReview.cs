using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class LoginInteractionReview
    {
        const string Request="Library/JarVisReviewLoginInteraction.request";
        static double next;
        static LoginInteractionReview()
        {
            EditorApplication.update+=()=>{
                if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+1;
                if(!File.Exists(Request)||EditorApplication.isCompiling)return;
                if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(Request);Run();
            };
            EditorApplication.playModeStateChanged+=s=>{
                if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("LoginInteractionReview",false))
                {SessionState.SetBool("LoginInteractionReview",false);new GameObject("Login interaction QA").AddComponent<LoginInteractionRunner>();}
            };
        }
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("LoginInteractionReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class LoginInteractionRunner:MonoBehaviour
    {
        readonly List<string> errors=new List<string>();
        readonly List<string> checks=new List<string>();
        void Check(bool ok,string name){checks.Add((ok?"PASS ":"FAIL ")+name);if(!ok)errors.Add(name);}
        IEnumerator Start()
        {
            yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();nav.Show("5:36",false);yield return null;
            var root=nav.screens.First(s=>s.id=="5:36").root;
            var fields=root.GetComponentsInChildren<TMP_InputField>();Check(fields.Length==2,"Two live login fields");
            foreach(var field in fields)
            {
                Canvas.ForceUpdateCanvases();
                var canvas=field.GetComponentInParent<Canvas>();
                var rect=(RectTransform)field.transform;
                var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
                var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                bool hit=hits.Count>0&&hits[0].gameObject.GetComponentInParent<TMP_InputField>()==field;
                Check(hit,"Pointer reaches "+field.name);
                if(hit)
                {
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    yield return null;yield return null;
                    Check(field.isFocused,"Pointer focuses "+field.name);
                    field.text="";
                    foreach(char character in "NV-Test12")
                    {
                        field.ProcessEvent(new Event{type=EventType.KeyDown,character=character,keyCode=KeyCode.None});
                        field.ForceLabelUpdate();field.textComponent.ForceMeshUpdate();yield return null;
                    }
                    field.ForceLabelUpdate();field.textComponent.ForceMeshUpdate();
                    Check(field.text=="NV-Test12","Keyboard characters accepted by "+field.name);
                    Check(field.contentType!=TMP_InputField.ContentType.Password||!field.textComponent.text.Contains("NV-Test12"),"Password remains masked "+field.name);
                    field.caretPosition=field.text.Length;
                    field.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Backspace});
                    Check(field.text=="NV-Test1","Backspace edits "+field.name);
                    field.text="";field.DeactivateInputField();
                }
            }
            EventSystem.current.SetSelectedGameObject(null);
            Directory.CreateDirectory("Logs/PrototypeParity");
            string oldTheme=nav.Settings.Theme;
            foreach(string theme in new[]{"light","dark"})
            {
                nav.Settings.SelectTheme(theme);
                nav.Show("5:1216",false);yield return null;
                nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>().SetKeyboardMode(true);
                nav.Show("5:36",false);yield return null;yield return null;
                Check(!nav.BottomNavigation.activeSelf,theme+" chat to login hides dock");
                nav.Back();Check(nav.CurrentScreen=="5:36",theme+" login Back stays on login");
                nav.Show("5:1308",false);yield return null;
                nav.Settings.Open("language");yield return null;
                nav.Show("5:36",false);yield return null;yield return null;
                Check(!nav.BottomNavigation.activeSelf,theme+" settings detail to login hides dock");
                Check(nav.Settings.Panel==null,theme+" settings detail closes after navigation");
                PrototypeParityRunner.Capture(nav,"Login-"+theme+"-navigation-fixed.png",824,1830);
                nav.Show("48:244",false);yield return null;
                Check(nav.BottomNavigation.activeSelf,theme+" tasks shows dock");
                foreach(var image in nav.BottomNavigation.GetComponentsInChildren<UnityEngine.UI.Image>())
                    if(image.name=="home"||image.name=="tasks"||image.name=="map")
                        Check(theme!="dark"||image.color.r>.4f,theme+" dock icon readable "+image.name);
            }
            nav.Settings.SelectTheme(oldTheme);nav.Show("5:36",false);yield return null;
            if(File.Exists("Library/JarVisVerifyUnifiedBackend.request"))
            {
                File.Delete("Library/JarVisVerifyUnifiedBackend.request");
                var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();
                JarVis.Warehouse.Backend.OmsMobileSession.Clear();
                Check(!chat.CanChat,"Unauthenticated chat cannot use preview fallback");
                string endpoint;Check(JarVis.Warehouse.Backend.OmsMobileSession.TryLoadEndpoint(out endpoint)&&endpoint=="http://127.0.0.1:8000","Login and AI share live endpoint");
                var credentials=File.ReadAllText("E:/warehouse-ai/data/oms-admin-credentials.txt");
                string employee=System.Text.RegularExpressions.Regex.Match(credentials,@"(?m)^Username:\s*(.+)$").Groups[1].Value.Trim();
                string password=System.Text.RegularExpressions.Regex.Match(credentials,@"(?m)^Password:\s*(.+)$").Groups[1].Value.Trim();
                JarVis.Warehouse.Backend.OmsMobileApi.LoginReply session=null;string networkError=null;
                yield return JarVis.Warehouse.Backend.OmsMobileApi.Login(endpoint,employee,password,value=>session=value,error=>networkError=error);
                Check(session!=null,"UnityWebRequest live admin login");
                if(session!=null)
                {
                    JarVis.Warehouse.Backend.OmsMobileSession.Set(endpoint,session.accessToken,session.user.employeeCode,session.expiresAt);
                    Check(chat.CanChat,"Authenticated chat uses same mobile session");
                    JarVis.Warehouse.Backend.OmsMobileApi.JarvisReply answer=null;
                    yield return JarVis.Warehouse.Backend.OmsMobileApi.AskJarvis("Chào JarVis, hãy chào tôi bằng một câu tiếng Việt.",null,value=>answer=value,error=>networkError=error);
                    Check(answer!=null&&answer.mode=="openai"&&!answer.synthetic&&!string.IsNullOrWhiteSpace(answer.message),"UnityWebRequest actual OpenAI on live backend");
                    yield return JarVis.Warehouse.Backend.OmsMobileApi.Logout(_=>{},_=>{});
                    JarVis.Warehouse.Backend.OmsMobileSession.Clear();
                }
                nav.Show("5:36",false);yield return null;
            }
            PrototypeParityRunner.Capture(nav,"Login-input-fixed.png",824,1830);
            File.WriteAllText("Logs/PrototypeParity/LoginInteraction.txt",(errors.Count==0?"PASS ":"FAIL ")+DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",checks)+"\nLive login/OpenAI only when explicit integration flag is present; see checks above. No credentials saved or printed. Physical Android keyboard not tested.\n");
            if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);else Destroy(gameObject);
        }
    }
}
