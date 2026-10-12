using System;
using System.Linq;
using JarVis.Warehouse;
using JarVis.Warehouse.AR;
using JarVis.Warehouse.Auth;
using JarVis.Warehouse.Data;
using JarVis.Warehouse.Labels;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.Providers;
using JarVis.Warehouse.UI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    public static class JarvisProjectValidator
    {
        private const string SplashScenePath = "Assets/Scenes/SplashScene.unity";
        private const string ImportedScenePath = "Assets/Scenes/JarVisFigmaApp.unity";
        private const string LoginScenePath = "Assets/Scenes/LoginScene.unity";
        private const string WarehouseScenePath = "Assets/Scenes/WarehouseScene.unity";
        private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private const string FigmaMascotPath = "Assets/Resources/Figma/JarVisMascotLogin.png";

        [MenuItem("Tools/JarVis/Validate Android Prototype")]
        public static void Validate()
        {
            ValidateBuildSettings();
            if(EditorBuildSettings.scenes.First(scene=>scene.enabled).path==ImportedScenePath)
                ValidateImportedScene();
            else
            {
                ValidateSplashScene();
                ValidateLoginScene();
                ValidateWarehouseScene();
                ValidateRuntimeModels();
                ValidateRendererFeature();
            }
            ValidateAndroidSettings();
            Debug.Log("[JarvisProjectValidator] Android prototype validation passed.");
        }

        private static void ValidateBuildSettings()
        {
            string[] scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if(scenePaths.Length==4&&scenePaths[0]==ImportedScenePath
                &&scenePaths[1]==SplashScenePath&&scenePaths[2]==LoginScenePath&&scenePaths[3]==WarehouseScenePath)
                return;

            if (scenePaths.Length != 3
                || scenePaths[0] != SplashScenePath
                || scenePaths[1] != LoginScenePath
                || scenePaths[2] != WarehouseScenePath)
            {
                throw new InvalidOperationException(
                    "Enabled build scenes must be SplashScene, LoginScene, then WarehouseScene."
                );
            }
        }

        private static void ValidateImportedScene()
        {
            EditorSceneManager.OpenScene(ImportedScenePath,OpenSceneMode.Single);
            ImportedFigmaNavigation navigation=RequireComponent<ImportedFigmaNavigation>();
            RequireComponent<ImportedFigmaViewport>();
            ValidateSingleEventSystem("JarVisFigmaApp");
            if(navigation.screens==null||navigation.screens.Length!=22
                ||navigation.screens.Any(screen=>screen==null||screen.root==null||string.IsNullOrWhiteSpace(screen.id))
                ||navigation.screens.Select(screen=>screen.id).Distinct().Count()!=22
                ||!navigation.screens.Any(screen=>screen.id==navigation.initialScreen))
                throw new InvalidOperationException("Imported app screen routes are incomplete.");
            Canvas canvas=navigation.GetComponent<Canvas>();
            if(canvas==null||canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)
                throw new InvalidOperationException("Imported app Canvas is missing its raycaster.");
            foreach(var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                    if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)>0)
                        throw new InvalidOperationException("Imported app has a missing script: "+transform.name);
            Debug.Log("[JarvisProjectValidator] Imported app entry and 22 screen routes passed.");
        }

        private static void ValidateSplashScene()
        {
            EditorSceneManager.OpenScene(SplashScenePath, OpenSceneMode.Single);
            RequireComponent<SafeAreaPanel>();
            ValidateFigmaMascot();
            JarvisSplashController controller = UnityEngine.Object.FindAnyObjectByType<JarvisSplashController>(
                FindObjectsInactive.Include
            );
            if (controller == null)
                throw new InvalidOperationException("SplashScene is missing JarvisSplashController.");

            SerializedObject serializedController = new SerializedObject(controller);
            RequireReference(serializedController, "splashGroup");
            RequireReference(serializedController, "jarvisLabel");
            if (serializedController.FindProperty("nextScene").stringValue != "LoginScene")
                throw new InvalidOperationException("SplashScene must load LoginScene.");

            float totalSeconds = serializedController.FindProperty("totalSeconds").floatValue;
            if (Mathf.Abs(totalSeconds - 1.8f) > 0.05f)
                throw new InvalidOperationException("SplashScene duration must be approximately 1.8 seconds.");
        }

        private static void ValidateLoginScene()
        {
            EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
            RequireComponent<SafeAreaPanel>();
            ValidateFigmaMascot();
            LoginController controller = RequireComponent<LoginController>();
            SerializedObject serializedController = new SerializedObject(controller);
            string[] requiredReferences =
            {
                "titleLabel",
                "subtitleLabel",
                "employeeLabel",
                "accessCodeLabel",
                "roleLabel",
                "languageLabel",
                "employeeInput",
                "accessCodeInput",
                "roleDropdown",
                "languageDropdown",
                "loginButton",
                "loginButtonLabel",
                "statusLabel",
                "loadingIndicator"
            };
            foreach (string propertyName in requiredReferences)
                RequireReference(serializedController, propertyName);

            if (serializedController.FindProperty("nextScene").stringValue != "WarehouseScene")
                throw new InvalidOperationException("LoginScene must load WarehouseScene.");

            ValidateSingleEventSystem("LoginScene");
        }

        private static void ValidateWarehouseScene()
        {
            EditorSceneManager.OpenScene(WarehouseScenePath, OpenSceneMode.Single);
            RequireComponent<ARSession>();
            RequireComponent<XROrigin>();
            RequireComponent<ARCameraManager>();
            RequireComponent<ARCameraBackground>();
            RequireComponent<SafeAreaPanel>();
            RequireComponent<ArSessionStatusPresenter>();

            WarehouseWorkspaceController workspace = RequireComponent<WarehouseWorkspaceController>();
            SerializedObject serializedWorkspace = new SerializedObject(workspace);
            RequireReference(serializedWorkspace, "safeAreaRoot");

            WarehouseTaskController dashboard = RequireComponent<WarehouseTaskController>();
            SerializedObject serializedDashboard = new SerializedObject(dashboard);
            string[] requiredReferences =
            {
                "shiftLabel",
                "taskTitleLabel",
                "taskDetailLabel",
                "taskStateLabel",
                "progressLabel",
                "activityLabel",
                "progressFill",
                "previousButton",
                "scanButton",
                "completeButton",
                "nextButton"
            };
            foreach (string propertyName in requiredReferences)
                RequireReference(serializedDashboard, propertyName);

            ValidateSingleEventSystem("WarehouseScene");
        }

        private static void ValidateRendererFeature()
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
                MobileRendererPath
            );
            if (rendererData == null)
                throw new InvalidOperationException("Mobile renderer data is missing.");

            if (!rendererData.rendererFeatures.OfType<ARBackgroundRendererFeature>().Any())
                throw new InvalidOperationException("Mobile renderer is missing ARBackgroundRendererFeature.");
        }

        private static void ValidateRuntimeModels()
        {
            if (Enum.GetValues(typeof(SupportedLanguage)).Length != 2)
                throw new InvalidOperationException("Only Vietnamese and English languages are allowed.");
            if (Enum.GetValues(typeof(WarehouseTaskStatus)).Length != 7)
                throw new InvalidOperationException("Warehouse task status coverage is incomplete.");

            MockWarehouseRepository repository = new MockWarehouseRepository();
            WarehouseItem item = repository.FindByBarcode("8931234567890");
            if (item == null || item.sku != "ICE-CREAM-001" || !item.isMock)
                throw new InvalidOperationException("Mock barcode lookup did not return the expected item.");

            foreach (WarehouseTask task in repository.GetAssignedTasks("VALIDATION"))
            {
                WarehouseItem taskItem = repository.FindByBarcode(task.barcode);
                if (taskItem == null
                    || taskItem.taskId != task.taskId
                    || taskItem.sku != task.sku
                    || taskItem.location != task.location
                    || !taskItem.isMock)
                {
                    throw new InvalidOperationException(
                        $"Mock task {task.taskId} does not have a matching barcode item."
                    );
                }
            }

            InventoryResult inventory = repository.GetInventory("ICE-CREAM-001");
            if (inventory == null || inventory.inventoryVerified || !inventory.isMock)
                throw new InvalidOperationException("Mock inventory must remain unverified.");

            WarehouseMapService mapService = new WarehouseMapService();
            Route route = mapService.GetRoute("ENTRY-01", "B-12-03");
            if (!route.IsValid
                || route.waypoints[0].locationId != "ENTRY-01"
                || route.waypoints[^1].locationId != "B-12-03"
                || route.approved)
            {
                throw new InvalidOperationException("Mock waypoint route validation failed.");
            }

            string[] taskLocations = { "IN-A-03", "C-07-02" };
            foreach (string taskLocation in taskLocations)
            {
                Route taskRoute = mapService.GetRoute("ENTRY-01", taskLocation);
                if (!taskRoute.IsValid
                    || taskRoute.waypoints[^1].locationId != taskLocation
                    || taskRoute.approved)
                {
                    throw new InvalidOperationException(
                        $"Mock waypoint route to {taskLocation} validation failed."
                    );
                }
            }

            WarehouseLabelService labelService = new WarehouseLabelService();
            if (labelService.HasScannableEncoder
                || !labelService.BuildPreview(item).Contains(item.itemId))
            {
                throw new InvalidOperationException("Label preview must contain the trace item ID and remain non-scannable.");
            }

            Debug.Log("[JarvisProjectValidator] Runtime model self-tests passed.");
        }

        private static void ValidateAndroidSettings()
        {
            if (PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android)
                != "com.teep.jarviswarehousear")
            {
                throw new InvalidOperationException("Android application identifier is incorrect.");
            }

            if (PlayerSettings.Android.minSdkVersion != AndroidSdkVersions.AndroidApiLevel29)
                throw new InvalidOperationException("Android minimum SDK must be API level 29.");

            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
                throw new InvalidOperationException("Android target architecture must be ARM64 only.");

            if (PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android)
                != ScriptingImplementation.IL2CPP)
            {
                throw new InvalidOperationException("Android scripting backend must be IL2CPP.");
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                throw new InvalidOperationException("Default interface orientation must be portrait.");

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android)
                || !PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)
                    .SequenceEqual(new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 }))
                throw new InvalidOperationException("Android must use OpenGLES3 without Vulkan.");
        }

        private static T RequireComponent<T>() where T : Component
        {
            T component = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name} is missing {typeof(T).Name}."
                );
            }

            return component;
        }

        private static void ValidateFigmaMascot()
        {
            Sprite source = AssetDatabase.LoadAssetAtPath<Sprite>(FigmaMascotPath);
            if (source == null)
                throw new InvalidOperationException("Figma mascot sprite is missing: " + FigmaMascotPath);

            UnityEngine.UI.Image image = UnityEngine.Object
                .FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.name == "JarVis Figma Mascot");
            if (image == null || image.sprite != source || !image.preserveAspect)
                throw new InvalidOperationException(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                    + " must use the original Figma mascot sprite without stretching.");
        }

        private static void ValidateSingleEventSystem(string sceneName)
        {
            EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include
            );
            if (eventSystems.Length != 1)
                throw new InvalidOperationException($"{sceneName} must contain exactly one EventSystem.");
        }

        private static void RequireReference(SerializedObject serializedObject, string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == null)
            {
                throw new InvalidOperationException(
                    $"{serializedObject.targetObject.name}.{propertyName} is not assigned."
                );
            }
        }
    }
}
