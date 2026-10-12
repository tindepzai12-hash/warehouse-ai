using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class CdmFigmaTmpSetup
    {
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private static double deadline;

        public static void ImportEssentials()
        {
            if (File.Exists(SettingsPath))
            {
                Debug.Log("[JarVis Figma] TMP Essentials already installed.");
                EditorApplication.Exit(0);
                return;
            }

            deadline = EditorApplication.timeSinceStartup + 120;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            EditorApplication.update += Poll;
        }

        private static void Poll()
        {
            if (File.Exists(SettingsPath))
            {
                EditorApplication.update -= Poll;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[JarVis Figma] TMP Essentials installed.");
                EditorApplication.Exit(0);
            }
            else if (EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= Poll;
                Debug.LogError("[JarVis Figma] TMP Essentials import timed out.");
                EditorApplication.Exit(1);
            }
        }
    }
}
