using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Cdm.Figma;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // One-time, token-free source file for importing the Login frame through cdmvision.
    public static class CdmFigmaLoginDownload
    {
        private const string FileId = "GGBvTqIQRqLP4cZv1LHAfT";
        private const string Output = "Assets/JarVis/DesignImports/PROJECT-KHO.figma";

        public static async void DownloadFromClipboard()
        {
            string token = GUIUtility.systemCopyBuffer?.Trim();
            if (string.IsNullOrWhiteSpace(token) || token.Length < 30)
            {
                Debug.LogError("[JarVis Figma] No Figma token was found in the OS clipboard.");
                EditorApplication.Exit(2);
                return;
            }

            GUIUtility.systemCopyBuffer = string.Empty;
            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                var downloader = new FigmaDownloader
                {
                    downloadDependencies = false,
                    downloadImages = true
                };
                var file = await downloader.DownloadFileAsync(token, FileId, "", null,
                    cancellation.Token);
                if (file == null) throw new InvalidOperationException("Figma returned no file.");

                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                byte[] json = Encoding.UTF8.GetBytes(file.ToString("N"));
                using (var output = File.Create(Output))
                using (var gzip = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal))
                    gzip.Write(json, 0, json.Length);

                AssetDatabase.ImportAsset(Output, ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[JarVis Figma] Design imported from Figma at " + Output);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogError("[JarVis Figma] Download/import failed: " + error.GetType().Name
                    + ": " + error.Message);
                EditorApplication.Exit(1);
            }
            finally
            {
                token = null;
            }
        }
    }
}
