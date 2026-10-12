using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JarVis.Warehouse.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class FigmaPointerVerification
    {
        static FigmaPointerVerification() { EditorApplication.playModeStateChanged += Changed; }
        public static void Run()
        {
            CdmFigmaAppBuilder.Build();
            SessionState.SetBool("JarVisFigmaPointerTest",true);
            EditorApplication.isPlaying = true;
        }
        static void Changed(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("JarVisFigmaPointerTest",false)) return;
            SessionState.SetBool("JarVisFigmaPointerTest",false);
            new GameObject("Figma pointer verification").AddComponent<FigmaPointerTestRunner>();
        }
    }
    public sealed class FigmaPointerTestRunner : MonoBehaviour
    {
        IEnumerator Start()
        {
            yield return null; yield return null;
            var nav = UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>();
            var testSettings = UnityEngine.Object.Instantiate(InputSystem.settings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testSettings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings = testSettings;
            Application.runInBackground = true;
            foreach (var device in InputSystem.devices.ToArray()) if (device is Mouse) InputSystem.DisableDevice(device);
            var mouse = InputSystem.AddDevice<Mouse>("JarVisPointerTestMouse");
            var failures = new List<string>(); var checkedButtons = new List<string>(); var skipped = new List<string>();
            var canvas = nav.GetComponent<Canvas>(); var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            var target = new RenderTexture(412,915,24); camera.targetTexture = target; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            foreach (var screen in nav.screens)
            {
                nav.Show(screen.id,false); yield return null;
                var buttons = screen.root.GetComponentsInChildren<UnityEngine.UI.Button>(false).ToArray();
                foreach (var button in buttons)
                {
                    nav.Show(screen.id,false); yield return null;
                    nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit(); Canvas.ForceUpdateCanvases(); camera.Render();
                    var rect = button.transform as RectTransform;
                    var point = RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
                    var scroll = button.GetComponentInParent<ScrollRect>();
                    if (scroll != null)
                    {
                        var viewCorners = new Vector3[4]; scroll.viewport.GetWorldCorners(viewCorners);
                        var worldCenter = rect.TransformPoint(rect.rect.center);
                        if (worldCenter.x>viewCorners[2].x || worldCenter.y<viewCorners[0].y)
                        {
                            if (scroll.horizontal) scroll.horizontalNormalizedPosition = 1;
                            if (scroll.vertical) scroll.verticalNormalizedPosition = 0;
                            yield return null; Canvas.ForceUpdateCanvases(); camera.Render();
                            point = RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
                        }
                    }
                    if (point.x<0 || point.x>412 || point.y<0 || point.y>915) { skipped.Add(screen.id+"/"+button.name); continue; }
                    var pointer = new PointerEventData(EventSystem.current) { position = point };
                    var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
                    var hit = hits.Count>0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                    if (hit != button.gameObject) { failures.Add(screen.id+"/"+button.name+": hit="+(hit!=null?hit.name:"none")+" depth="+button.targetGraphic.depth+" cull="+button.targetGraphic.canvasRenderer.cull+" alpha="+button.targetGraphic.color.a+" size="+rect.rect.size); continue; }
                    // Send real mouse state to the configured InputSystemUIInputModule.
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = point }.WithButton(MouseButton.Left,true));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState { position = point });
                    yield return null; yield return null;
                    if (nav.LastClickedButton != button.gameObject) failures.Add(screen.id+"/"+button.name+": input event not received");
                    else checkedButtons.Add(screen.id+"/"+button.name);
                    if (screen.id=="5:36" && button.name=="btn-primary" && nav.CurrentScreen!="85:14") failures.Add("Login did not navigate");
                }
            }
            camera.targetTexture = null; Destroy(target);
            var report = "POINTER "+(failures.Count==0?"PASS":"FAIL")+"; clicked="+checkedButtons.Count+"; offscreen="+skipped.Count+"\n"+string.Join("\n",failures)+"\nOFFSCREEN:\n"+string.Join("\n",skipped);
            File.WriteAllText("Logs/FigmaPointerVerification.txt",report); Debug.Log(report);
            EditorApplication.Exit(failures.Count==0?0:1);
        }
    }
}
