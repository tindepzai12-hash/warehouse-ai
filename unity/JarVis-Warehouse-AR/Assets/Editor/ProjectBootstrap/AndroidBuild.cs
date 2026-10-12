using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class AndroidBuild
    {
        private const string DefaultOutputPath = "Builds/Android/JarVis-Warehouse.apk";

        [MenuItem("Tools/JarVis/Build Android APK")]
        public static void BuildApk()
        {
            AndroidProjectConfigurator.ApplySettings();
            JarvisProjectValidator.Validate();

            string outputPath = ResolveOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.StrictMode | BuildOptions.CompressWithLz4HC
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log(
                $"[AndroidBuild] Result={summary.result}, Size={summary.totalSize}, "
                + $"Duration={summary.totalTime}, Output={outputPath}"
            );

            if (summary.result != BuildResult.Succeeded)
                throw new UnityEditor.Build.BuildFailedException($"Android APK build failed: {summary.result}");

            FileInfo apk = new FileInfo(outputPath);
            if (!apk.Exists || apk.Length == 0)
                throw new UnityEditor.Build.BuildFailedException("Android build reported success but APK is missing.");

            Debug.Log($"[AndroidBuild] APK ready: {apk.FullName} ({apk.Length} bytes)");
        }

        private static string ResolveOutputPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], "-buildOutput", StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(arguments[index + 1]);
            }

            return Path.GetFullPath(DefaultOutputPath);
        }
    }
}
