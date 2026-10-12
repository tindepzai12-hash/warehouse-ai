using System.IO;
using Cdm.Figma.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // Creates a local-only downloader configured for the JarVis Figma file.
    // The owner enters a Figma token in Unity; no token belongs in source control.
    public static class CdmFigmaConnectionSetup
    {
        private const string Folder = "Assets/JarVis/LocalFigmaConnection";
        private const string AssetPath = Folder + "/PROJECT-KHO-Downloader.asset";

        [MenuItem("Tools/JarVis/Prepare Figma Connection")]
        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);
            FigmaDownloaderAsset downloader =
                AssetDatabase.LoadAssetAtPath<FigmaDownloaderAsset>(AssetPath);
            if (downloader == null)
            {
                downloader = ScriptableObject.CreateInstance<FigmaDownloaderAsset>();
                AssetDatabase.CreateAsset(downloader, AssetPath);
            }

            downloader.fileId = "GGBvTqIQRqLP4cZv1LHAfT";
            downloader.fileName = "PROJECT-KHO";
            downloader.assetPath = "JarVis/DesignImports";
            downloader.assetExtension = "figma";
            downloader.downloadImages = true;
            downloader.downloadDependencies = true;
            EditorUtility.SetDirty(downloader);
            AssetDatabase.SaveAssets();
            Selection.activeObject = downloader;
            Debug.Log("[JarVis Figma] Local-only connection asset ready at " + AssetPath);
        }
    }
}
