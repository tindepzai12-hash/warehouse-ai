using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class PackageInstaller
    {
        static readonly string[] PackagesToAdd =
        {
            "com.unity.xr.arfoundation",
            "com.unity.xr.arcore",
            "com.unity.adaptiveperformance",
            "com.unity.adaptiveperformance.google.android"
        };

        static readonly string[] PackagesToRemove =
        {
            "com.unity.adaptiveperformance.samsung.android"
        };

        const double TimeoutSeconds = 900;
        static AddAndRemoveRequest _request;
        static double _deadline;

        public static void Install()
        {
            Debug.Log($"[PackageInstaller] Adding: {string.Join(", ", PackagesToAdd)}");
            _request = Client.AddAndRemove(PackagesToAdd, PackagesToRemove);
            _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (_request == null) return;
            if (!_request.IsCompleted)
            {
                if (EditorApplication.timeSinceStartup > _deadline)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[PackageInstaller] Timed out waiting for UPM.");
                    EditorApplication.Exit(2);
                }
                return;
            }

            EditorApplication.update -= Poll;
            if (_request.Status == StatusCode.Success)
            {
                var names = _request.Result.Select(package => $"{package.name}@{package.version}");
                Debug.Log($"[PackageInstaller] Resolved: {string.Join(", ", names)}");
                EditorApplication.Exit(0);
                return;
            }

            Debug.LogError($"[PackageInstaller] Failed: {_request.Error?.message}");
            EditorApplication.Exit(1);
        }
    }
}
