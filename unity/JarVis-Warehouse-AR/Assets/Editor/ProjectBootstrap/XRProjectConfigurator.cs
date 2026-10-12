using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace ProjectBootstrap
{
    public static class XRProjectConfigurator
    {
        const string SettingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
        const string ARCoreLoaderType = "UnityEngine.XR.ARCore.ARCoreLoader";

        public static void Configure()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(
                BuildTargetGroup.Android
            );

            if (settings == null)
            {
                var perBuildTarget = LoadOrCreateSettingsAsset();
                if (!perBuildTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                    perBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                settings = perBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            }

            settings.InitManagerOnStart = true;
            var manager = settings.AssignedSettings;
            if (manager == null)
                throw new System.InvalidOperationException("Android XR manager settings were not created.");

            if (!XRPackageMetadataStore.IsLoaderAssigned(ARCoreLoaderType, BuildTargetGroup.Android))
            {
                if (!XRPackageMetadataStore.AssignLoader(
                    manager,
                    ARCoreLoaderType,
                    BuildTargetGroup.Android
                ))
                {
                    throw new System.InvalidOperationException("Could not assign ARCore loader for Android.");
                }
            }

            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
            Debug.Log("[XRProjectConfigurator] ARCore loader enabled for Android.");
            EditorApplication.Exit(0);
        }

        static XRGeneralSettingsPerBuildTarget LoadOrCreateSettingsAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsPath);
            if (existing != null)
            {
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, existing, true);
                return existing;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            var created = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(created, SettingsPath);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, created, true);
            AssetDatabase.SaveAssets();
            return created;
        }
    }
}
