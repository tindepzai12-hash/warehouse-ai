#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using JarVis.Warehouse.Preview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class PickupPreviewPlay
    {
        const string Dir = @"C:\Users\Windows\Documents\ChatGPT\jarvis\pickup-demo-20261121";
        static PickupPreviewPlay()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("JarVisPickupDemoPlay", false))
                { SessionState.SetBool("JarVisPickupDemoPlay", false); new GameObject("Pickup demo play check").AddComponent<PickupPreviewPlayRunner>(); }
            };
        }
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/JarVis/Demos/PickupReview/PickupReview.unity");
            SessionState.SetBool("JarVisPickupDemoPlay", true); EditorApplication.isPlaying = true;
        }
        public sealed class PickupPreviewPlayRunner : MonoBehaviour
        {
            void Click(PickupFlowPreview ui, string name) { ui.GetComponentsInChildren<Button>().Single(b => b.name == name).onClick.Invoke(); }
            IEnumerator Start()
            {
                yield return null; yield return null;
                try
                {
                    var ui = UnityEngine.Object.FindFirstObjectByType<PickupFlowPreview>();
                    if (ui == null || ui.transform.Cast<Transform>().Count(c => c.gameObject.activeSelf && c.name == "Pickup UI demo") != 1) throw new Exception("Demo has duplicate or missing page after Play");
                    Click(ui, "Accept mission"); Click(ui, "Demo scan valid"); Click(ui, "Open AR"); Click(ui, "Begin pickup"); Click(ui, "Demo scan valid"); Click(ui, "Continue barcode"); Click(ui, "Demo scan valid"); Click(ui, "Confirm book");
                    if (!ui.PickedB || ui.PickedC || ui.Stop != "C") throw new Exception("Play-mode A1 handoff failed");
                    File.WriteAllText(Dir + "/PLAY-RESULT.txt", "PASS " + DateTime.UtcNow.ToString("O") + " - Isolated demo scene opened in Unity Play; one active page, actual button callbacks reach A1 confirmed and C selected. No camera feed or production mission used.");
                    EditorApplication.Exit(0);
                }
                catch (Exception e) { File.WriteAllText(Dir + "/PLAY-RESULT.txt", "FAIL " + e); Debug.LogException(e); EditorApplication.Exit(1); }
            }
        }
    }
}
#endif
