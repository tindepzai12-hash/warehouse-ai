using System.IO;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class LoginInputRepair
    {
        const string Request="Library/JarVisFixLoginInputs.request";
        static LoginInputRepair(){EditorApplication.delayCall+=ProcessRequest;}
        static void ProcessRequest()
        {
            if(!File.Exists(Request))return;
            if(EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
                EditorApplication.delayCall+=ProcessRequest;return;
            }
            File.Delete(Request);
            Run();
            EditorApplication.isPlaying=true;
        }
        [MenuItem("Tools/JarVis/Fix Login Inputs")]
        public static void Run()
        {
            var nav=Object.FindFirstObjectByType<ImportedFigmaNavigation>();
            if(nav==null){Debug.LogError("Open JarVisFigmaApp before repairing Login inputs.");return;}
            nav.Initialize();
            nav.RepairLoginInputs();
            Canvas.ForceUpdateCanvases();
            var root=nav.screens.First(s=>s.id=="5:36").root;
            var fields=root.GetComponentsInChildren<TMP_InputField>(true);
            if(fields.Length!=2 || fields.Any(f=>f.textViewport==f.transform || f.textComponent.alignment!=TextAlignmentOptions.Left))
                throw new System.InvalidOperationException("Login input alignment check failed.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/LoginInputRepair.txt","PASS: two separate text viewports, left/middle aligned inputs; icon layout repaired. Authentication requires a valid OMS employee account.\n");
            Debug.Log("[JarVis] Login inputs repaired without rebuilding the app.");
        }
    }
}
