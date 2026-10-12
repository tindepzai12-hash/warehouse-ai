using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    // UI-only regression: no provider request, microphone permission or warehouse writes.
    [InitializeOnLoad]
    public static class ImportedComposerLocalizationReview
    {
        static ImportedComposerLocalizationReview()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarVisComposerReview",false))
                {SessionState.SetBool("JarVisComposerReview",false);new GameObject("Composer localization review").AddComponent<ComposerLocalizationRunner>();}
            };
        }
        [MenuItem("Tools/JarVis/Review Composer and Languages")]
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("JarVisComposerReview",true);EditorApplication.isPlaying=true;
        }
    }
    public sealed class ComposerLocalizationRunner:MonoBehaviour
    {
        readonly List<string> errors=new List<string>();
        void Check(bool value,string message){if(!value)errors.Add(message);}
        IEnumerator Start()
        {
            yield return null;yield return null;
            Directory.CreateDirectory("Logs/ComposerLocalizationReview");
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();
            var localization=nav.GetComponent<ImportedAppLocalization>();
            var login=nav.screens.First(s=>s.id=="5:36").root;
            var inputs=login.GetComponentsInChildren<TMP_InputField>(true);
            inputs[0].text="NV-98765";inputs[1].text="test-password";
            var english=login.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b=>b.name=="Choose EN");
            ExecuteEvents.Execute(english.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
            yield return null;yield return null;
            Check(ImportedAppLocalization.IsEnglish,"Login EN did not change language");
            Check(inputs[0].text=="NV-98765"&&inputs[1].text=="test-password","Language changed input values");
            Capture(nav,"Login-EN.png");
            int checkedLabels=0;
            foreach(var screen in nav.screens)
            {
                nav.Show(screen.id,false);yield return null;yield return null;
                foreach(var text in screen.root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if(inputs.Any(f=>f.textComponent==text))continue;
                    string source=ImportedAppLocalization.Source(text),translated=ImportedAppLocalization.Text(source);
                    if(translated!=source){checkedLabels++;Check(text.text==translated,screen.id+" label not translated: "+source);}
                }
                if(screen.id=="48:244"||screen.id=="5:1308")Capture(nav,screen.id.Replace(':','-')+"-EN.png");
            }
            nav.Show("5:1216",false);yield return null;
            var chat=nav.screens.First(s=>s.id=="5:1216").root.GetComponent<ImportedJarvisChat>();
            chat.Input.text="Nhập kho";
            chat.AddMessage("Nhập kho",true);chat.AddMessage("Sàng lọc, Sắp xếp, Sạch sẽ, Săn sóc, Sẵn sàng.",false);
            yield return null;yield return null;
            Check(chat.Input.text=="Nhập kho","Localized the user's draft");
            Check(chat.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.name=="Message"&&t.text=="Nhập kho"),"Localized a user message");
            Capture(nav,"Chat-Expanded-EN.png");
            chat.SetKeyboardMode(true);Canvas.ForceUpdateCanvases();
            Check(chat.ActionsCollapsed&&!chat.Media.PhotoButton.gameObject.activeSelf&&chat.Media.ExpandButton.gameObject.activeSelf,"Keyboard did not collapse actions");
            Check(!nav.BottomNavigation.activeSelf,"Footer still visible in keyboard mode");
            Check(((RectTransform)chat.Input.transform).rect.width==268,"Collapsed input did not grow");
            Capture(nav,"Chat-Collapsed-EN.png");
            chat.ExpandMediaActions();yield return null;yield return null;
            Check(!chat.ActionsCollapsed&&chat.Media.PhotoButton.gameObject.activeSelf,"Arrow did not reopen actions");
            Check(chat.Input.text=="Nhập kho","Arrow cleared draft");
            chat.Input.DeactivateInputField();chat.SetKeyboardMode(false);yield return null;
            foreach(var button in new[]{chat.Media.PhotoButton,chat.Media.MicButton,chat.Media.ListenButton,chat.SendButton})
            {
                var rect=(RectTransform)button.transform;Check(rect.rect.width>=48&&rect.rect.height>=48,"Small touch target: "+button.name);
                var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                Check(hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"Blocked composer target: "+button.name);
                var icon=button.GetComponentInChildren<ChatActionIcon>();
                if(icon!=null){var mesh=icon.canvasRenderer.GetMesh();Check(mesh!=null&&mesh.vertexCount>4,"Missing vector icon mesh: "+button.name);}
            }
            chat.Media.LoadPhoto("Assets/Resources/Figma/JarVisMascotLogin.png");yield return null;
            Check(chat.Media.Photo!=null,"Attachment failed after toolbar refactor");Capture(nav,"Chat-Photo-EN.png");chat.Media.ClearPhoto();
            localization.Select(SupportedLanguage.Vi);yield return null;yield return null;
            Check(chat.StatusText=="Sẵn sàng nhận câu hỏi","Language change restored stale attachment status");
            Check(chat.Input.text=="Nhập kho","Switching VI changed draft");Capture(nav,"Chat-VI.png");
            nav.Show("5:36",false);inputs[0].text="";inputs[1].text="";yield return null;Capture(nav,"Login-VI.png");
            File.WriteAllText("Logs/ComposerLocalizationReview/result.txt",(errors.Count==0?"PASS":"FAIL")+" — "+DateTime.UtcNow.ToString("o")+"\n"+nav.screens.Length+" screens, "+checkedLabels+" translated labels; Login selection, input preservation, collapsed/expanded composer, tap targets and attachment checked.\n"+string.Join("\n",errors)+"\nPhysical Samsung keyboard/IME and microphone/photo picker are not tested. No OpenAI requests.\n");
            if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);else Destroy(gameObject);
        }
        public static void Capture(ImportedFigmaNavigation nav,string filename)
        {
            var canvas=nav.GetComponent<Canvas>();var camera=FindAnyObjectByType<Camera>();var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var target=new RenderTexture(824,1830,24);var image=new Texture2D(824,1830,TextureFormat.RGBA32,false);
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;Canvas.ForceUpdateCanvases();nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();Canvas.ForceUpdateCanvases();foreach(var label in nav.GetComponentsInChildren<TMP_Text>(false)){label.UpdateMeshPadding();label.ForceMeshUpdate();}camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,824,1830),0,0);image.Apply();File.WriteAllBytes("Logs/ComposerLocalizationReview/"+filename,image.EncodeToPNG());}
            finally{camera.targetTexture=oldTarget;canvas.renderMode=mode;canvas.worldCamera=oldCamera;RenderTexture.active=oldActive;Canvas.ForceUpdateCanvases();nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();Destroy(image);Destroy(target);}
        }
    }
}
