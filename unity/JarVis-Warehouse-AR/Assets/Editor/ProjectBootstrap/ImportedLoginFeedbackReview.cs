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

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class ImportedLoginFeedbackReview
    {
        static double nextCheck;
        static ImportedLoginFeedbackReview()
        {
            EditorApplication.update+=()=>{
                if(EditorApplication.timeSinceStartup<nextCheck)return;nextCheck=EditorApplication.timeSinceStartup+2;
                if(!File.Exists("Library/JarVisReviewLoginFeedback.request")||EditorApplication.isCompiling)return;
                if(EditorApplication.isPlaying){EditorApplication.isPlaying=false;return;}
                if(EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete("Library/JarVisReviewLoginFeedback.request");Run();
            };
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisFeedbackReview",false))
                {SessionState.SetBool("JarVisFeedbackReview",false);new GameObject("Login feedback review").AddComponent<LoginFeedbackRunner>();}
            };
        }
        [MenuItem("Tools/JarVis/Review Login Caret and Notifications")]
        public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");SessionState.SetBool("JarVisFeedbackReview",true);EditorApplication.isPlaying=true;}
    }
    public sealed class LoginFeedbackRunner:MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null;yield return null;
            Directory.CreateDirectory("Logs/ComposerLocalizationReview");var errors=new List<string>();
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();nav.Show("5:36",false);yield return null;
            var root=nav.screens.First(s=>s.id=="5:36").root;
            var fields=root.GetComponentsInChildren<TMP_InputField>(true);
            var input=fields.First(f=>f.contentType!=TMP_InputField.ContentType.Password);
            input.text="NV-00000";input.caretBlinkRate=0;input.ActivateInputField();yield return null;yield return null;yield return null;
            input.caretPosition=input.text.Length;input.ForceLabelUpdate();yield return null;Canvas.ForceUpdateCanvases();
            foreach(var field in fields)
            {
                var caret=field.GetComponentInChildren<TMP_SelectionCaret>(true);
                if(caret==null||caret.transform.parent!=field.textComponent.transform.parent)errors.Add("Caret is outside text viewport: "+field.name);
            }
            ComposerLocalizationRunner.Capture(nav,"Login-Caret-Fixed.png");input.DeactivateInputField();
            nav.Notice("Không kết nối được máy chủ. Kiểm tra mạng rồi thử lại.",true);yield return new WaitForSecondsRealtime(.25f);
            var snackbar=nav.GetComponent<ImportedSnackbar>();
            if(snackbar.Panel==null||snackbar.Panel.transform.parent!=root.transform.parent)errors.Add("Snackbar coordinate space is incorrect");
            var rect=(RectTransform)snackbar.Panel.transform;
            if(rect.rect.height>132||rect.rect.width!=380)errors.Add("Snackbar is not compact");
            foreach(var field in fields)
            {
                var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=new Rect(corners[0],corners[2]-corners[0]);
                ((RectTransform)field.transform).GetWorldCorners(corners);var b=new Rect(corners[0],corners[2]-corners[0]);
                if(a.Overlaps(b))errors.Add("Snackbar covers login input");
            }
            ComposerLocalizationRunner.Capture(nav,"Login-Error-Snackbar.png");
            nav.Notice("Chưa có thông báo.",false,.1f);yield return new WaitForSecondsRealtime(.5f);
            if(snackbar.Panel!=null)errors.Add("Snackbar did not auto-dismiss");
            nav.Notice("Chưa có thông báo.");nav.Show("126:8",false);
            if(snackbar.Panel!=null)errors.Add("Snackbar survives a route change");
            nav.Show("5:36",false);input.text="";
            File.WriteAllText("Logs/ComposerLocalizationReview/LoginFeedback.txt",(errors.Count==0?"PASS":"FAIL")+" — "+DateTime.UtcNow.ToString("o")+"\nCaret parent, compact notification, no form overlap, auto-dismiss and route cleanup checked.\n"+string.Join("\n",errors)+"\nPhysical Android keyboard not tested. No OpenAI request.\n");
            if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);else Destroy(gameObject);
        }
    }
}
