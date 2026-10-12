using System.IO;
using System.Linq;
using JarVis.Warehouse;
using JarVis.Warehouse.AR;
using JarVis.Warehouse.Auth;
using JarVis.Warehouse.UI;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class JarvisSceneBuilder
    {
        private const string SplashScenePath = "Assets/Scenes/SplashScene.unity";
        private const string LoginScenePath = "Assets/Scenes/LoginScene.unity";
        private const string WarehouseScenePath = "Assets/Scenes/WarehouseScene.unity";
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private const string LoginRebuildRequestPath = "Library/JarvisRebuildLogin.request";

        static JarvisSceneBuilder()
        {
            EditorApplication.delayCall += EnsurePrototypeScenes;
            EditorApplication.delayCall += ProcessLoginRebuildRequest;
        }

        private static void ProcessLoginRebuildRequest()
        {
            if (!File.Exists(LoginRebuildRequestPath) || EditorApplication.isCompiling)
                return;

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += ProcessLoginRebuildRequest;
                return;
            }

            File.Delete(LoginRebuildRequestPath);
            if (SceneManager.GetActiveScene().path == LoginScenePath)
                EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            RebuildLoginScene();
            EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
            EditorApplication.delayCall += () =>
            {
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                EditorApplication.isPlaying = true;
            };
            Debug.Log("[JarvisSceneBuilder] Login rebuild request completed and scene opened.");
        }

        [MenuItem("Tools/JarVis/Rebuild Splash Scene")]
        public static void RebuildSplashScene()
        {
            CreateSplashScene();
            JarvisFigmaMascotSync.Apply();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[JarvisSceneBuilder] Splash scene rebuilt.");
        }

        [MenuItem("Tools/JarVis/Rebuild Android Prototype Scenes")]
        public static void RebuildPrototypeScenes()
        {
            CreateSplashScene();
            CreateLoginScene();
            CreateWarehouseScene();
            JarvisFigmaMascotSync.Apply();
            EnsureArBackgroundRendererFeature();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[JarvisSceneBuilder] Android prototype scenes rebuilt.");
        }

        [MenuItem("Tools/JarVis/Rebuild Login Scene")]
        public static void RebuildLoginScene()
        {
            CreateLoginScene();
            JarvisFigmaMascotSync.Apply();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[JarvisSceneBuilder] Login scene rebuilt.");
        }

        private static void EnsurePrototypeScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            Scene activeScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path) && activeScene.isDirty)
            {
                Debug.LogWarning(
                    "[JarvisSceneBuilder] Scene generation skipped because an untitled scene has unsaved changes."
                );
                return;
            }

            if (!File.Exists(SplashScenePath))
                CreateSplashScene();
            if (!File.Exists(LoginScenePath))
                CreateLoginScene();
            if (!File.Exists(WarehouseScenePath))
                CreateWarehouseScene();

            EnsureArBackgroundRendererFeature();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
        }

        private static void CreateSplashScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SplashScenePath));

            Scene activeScene = SceneManager.GetActiveScene();
            NewSceneMode mode = string.IsNullOrEmpty(activeScene.path)
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);

            GameObject cameraObject = new GameObject("Splash Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorFromHex("07111F");
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject canvasObject = new GameObject(
                "Splash Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(JarvisSplashController)
            );
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2400f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = canvasObject.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            CreateImage("Background", canvasObject.transform, ColorFromHex("07111F"), Vector2.zero, Vector2.one);
            CreateImage(
                "Glow",
                canvasObject.transform,
                new Color(0.04f, 0.78f, 0.94f, 0.12f),
                new Vector2(0.12f, 0.34f),
                new Vector2(0.88f, 0.66f)
            );

            GameObject safeAreaObject = new GameObject(
                "Safe Area",
                typeof(RectTransform),
                typeof(SafeAreaPanel)
            );
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            RectTransform safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            UnityEngine.UI.Text title = CreateText(
                "JarVis Label",
                safeAreaObject.transform,
                "JarVis",
                112,
                FontStyle.Bold,
                ColorFromHex("E9FBFF"),
                new Vector2(0.08f, 0.47f),
                new Vector2(0.92f, 0.58f)
            );
            title.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text subtitle = CreateText(
                "Subtitle",
                safeAreaObject.transform,
                "WAREHOUSE ASSISTANT",
                30,
                FontStyle.Normal,
                ColorFromHex("55D6F4"),
                new Vector2(0.12f, 0.41f),
                new Vector2(0.88f, 0.47f)
            );
            subtitle.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text status = CreateText(
                "Status",
                safeAreaObject.transform,
                "Preparing Android workspace...",
                25,
                FontStyle.Normal,
                new Color(0.75f, 0.83f, 0.88f, 1f),
                new Vector2(0.12f, 0.10f),
                new Vector2(0.88f, 0.16f)
            );
            status.alignment = TextAnchor.MiddleCenter;

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            SerializedObject controller = new SerializedObject(canvasObject.GetComponent<JarvisSplashController>());
            controller.FindProperty("splashGroup").objectReferenceValue = canvasGroup;
            controller.FindProperty("jarvisLabel").objectReferenceValue = title;
            controller.FindProperty("nextScene").stringValue = "LoginScene";
            controller.FindProperty("totalSeconds").floatValue = 1.8f;
            controller.FindProperty("fadeSeconds").floatValue = 0.35f;
            controller.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, SplashScenePath);
            if (mode == NewSceneMode.Additive)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void CreateLoginScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LoginScenePath));

            Scene activeScene = SceneManager.GetActiveScene();
            NewSceneMode mode = string.IsNullOrEmpty(activeScene.path)
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);

            GameObject cameraObject = new GameObject("Login Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ColorFromHex("F8F9FA");
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            GameObject canvasObject = new GameObject(
                "Login Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster),
                typeof(LoginController)
            );
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;

            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2400f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            CreateImage("Background", canvasObject.transform, ColorFromHex("F8F9FA"), Vector2.zero, Vector2.one);

            GameObject safeAreaObject = new GameObject(
                "Safe Area",
                typeof(RectTransform),
                typeof(SafeAreaPanel)
            );
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            RectTransform safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            UnityEngine.UI.Text robotMark = CreateText(
                "JarVis Robot Mark",
                safeAreaObject.transform,
                "J",
                118,
                FontStyle.Bold,
                ColorFromHex("202124"),
                new Vector2(0.35f, 0.80f),
                new Vector2(0.65f, 0.94f)
            );
            robotMark.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text brandLabel = CreateText(
                "Brand Label",
                safeAreaObject.transform,
                "JarVis Warehouse\nAssistant",
                68,
                FontStyle.Bold,
                ColorFromHex("202124"),
                new Vector2(0.08f, 0.69f),
                new Vector2(0.92f, 0.80f)
            );
            brandLabel.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text prototypeLabel = CreateText(
                "Prototype Label",
                safeAreaObject.transform,
                "Fulfillment Center Prototype",
                27,
                FontStyle.Normal,
                ColorFromHex("6B7075"),
                new Vector2(0.08f, 0.655f),
                new Vector2(0.92f, 0.70f)
            );
            prototypeLabel.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Image card = CreateImage(
                "Login Card",
                safeAreaObject.transform,
                ColorFromHex("FFFFFF"),
                new Vector2(0.055f, 0.045f),
                new Vector2(0.945f, 0.64f)
            );
            UnityEngine.UI.Outline cardOutline = card.gameObject.AddComponent<UnityEngine.UI.Outline>();
            cardOutline.effectColor = new Color32(218, 224, 229, 255);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            UnityEngine.UI.Text titleLabel = CreateText(
                "Login Title",
                card.transform,
                string.Empty,
                1,
                FontStyle.Normal,
                Color.clear,
                Vector2.zero,
                Vector2.zero
            );
            titleLabel.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text subtitleLabel = CreateText(
                "Demo Notice",
                card.transform,
                string.Empty,
                1,
                FontStyle.Normal,
                Color.clear,
                Vector2.zero,
                Vector2.zero
            );
            subtitleLabel.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text employeeLabel = CreateText(
                "Employee Label",
                card.transform,
                "Employee ID",
                24,
                FontStyle.Normal,
                ColorFromHex("30343A"),
                new Vector2(0.07f, 0.855f),
                new Vector2(0.93f, 0.91f)
            );
            employeeLabel.alignment = TextAnchor.MiddleLeft;
            UnityEngine.UI.InputField employeeInput = CreateInputField(
                "Employee Input",
                card.transform,
                "e.g. JW-9502",
                new Vector2(0.07f, 0.735f),
                new Vector2(0.93f, 0.85f),
                false
            );
            StyleLightInput(employeeInput);

            UnityEngine.UI.Text accessCodeLabel = CreateText(
                "Access Code Label",
                card.transform,
                "Access Code",
                24,
                FontStyle.Normal,
                ColorFromHex("30343A"),
                new Vector2(0.07f, 0.655f),
                new Vector2(0.93f, 0.71f)
            );
            accessCodeLabel.alignment = TextAnchor.MiddleLeft;
            UnityEngine.UI.InputField accessCodeInput = CreateInputField(
                "Access Code Input",
                card.transform,
                "Enter 6-digit code",
                new Vector2(0.07f, 0.535f),
                new Vector2(0.93f, 0.65f),
                true
            );
            StyleLightInput(accessCodeInput);

            UnityEngine.UI.Text roleLabel = CreateText(
                "Role Label",
                card.transform,
                "Select Role",
                22,
                FontStyle.Bold,
                ColorFromHex("666A70"),
                new Vector2(0.07f, 0.47f),
                new Vector2(0.93f, 0.525f)
            );
            roleLabel.alignment = TextAnchor.MiddleCenter;
            UnityEngine.UI.Dropdown roleDropdown = CreateDropdown(
                "Role Dropdown",
                card.transform,
                Vector2.zero,
                Vector2.zero
            );
            roleDropdown.gameObject.SetActive(false);

            UnityEngine.UI.Button[] roleButtons = new UnityEngine.UI.Button[3];
            UnityEngine.UI.Text[] roleButtonLabels = new UnityEngine.UI.Text[3];
            UnityEngine.UI.Image[] roleButtonBackgrounds = new UnityEngine.UI.Image[3];
            string[] roleNames = { "Associate", "Supervisor", "Inventory" };
            string[] roleMarks = { "A", "S", "I" };
            for (int i = 0; i < roleButtons.Length; i++)
            {
                float left = 0.07f + (i * 0.295f);
                roleButtons[i] = CreateButton(
                    $"{roleNames[i]} Role Button",
                    card.transform,
                    roleNames[i],
                    new Vector2(left, 0.29f),
                    new Vector2(left + 0.255f, 0.465f),
                    ColorFromHex("08BAD0")
                );
                roleButtonBackgrounds[i] = roleButtons[i].GetComponent<UnityEngine.UI.Image>();
                roleButtonLabels[i] = roleButtons[i].transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
                RectTransform roleTextRect = roleButtonLabels[i].rectTransform;
                roleTextRect.anchorMin = new Vector2(0.05f, 0.04f);
                roleTextRect.anchorMax = new Vector2(0.95f, 0.43f);
                roleButtonLabels[i].fontSize = 22;
                roleButtonLabels[i].color = Color.white;

                UnityEngine.UI.Text icon = CreateText(
                    "Icon",
                    roleButtons[i].transform,
                    roleMarks[i],
                    42,
                    FontStyle.Bold,
                    Color.white,
                    new Vector2(0.20f, 0.43f),
                    new Vector2(0.80f, 0.92f)
                );
                icon.alignment = TextAnchor.MiddleCenter;
            }

            UnityEngine.UI.Text languageLabel = CreateText(
                "Language Label",
                card.transform,
                "Language",
                22,
                FontStyle.Bold,
                ColorFromHex("666A70"),
                new Vector2(0.07f, 0.225f),
                new Vector2(0.93f, 0.285f)
            );
            languageLabel.alignment = TextAnchor.MiddleCenter;
            UnityEngine.UI.Dropdown languageDropdown = CreateDropdown(
                "Language Dropdown",
                card.transform,
                Vector2.zero,
                Vector2.zero
            );
            languageDropdown.gameObject.SetActive(false);

            UnityEngine.UI.Button[] languageButtons = new UnityEngine.UI.Button[2];
            UnityEngine.UI.Text[] languageButtonLabels = new UnityEngine.UI.Text[2];
            UnityEngine.UI.Image[] languageButtonBackgrounds = new UnityEngine.UI.Image[2];
            string[] languageNames = { "English", "Tiếng Việt" };
            for (int i = 0; i < languageButtons.Length; i++)
            {
                float left = i == 0 ? 0.22f : 0.52f;
                languageButtons[i] = CreateButton(
                    $"{languageNames[i]} Language Button",
                    card.transform,
                    languageNames[i],
                    new Vector2(left, 0.155f),
                    new Vector2(left + 0.26f, 0.225f),
                    ColorFromHex("F7FAFC")
                );
                languageButtonBackgrounds[i] = languageButtons[i].GetComponent<UnityEngine.UI.Image>();
                UnityEngine.UI.Outline languageOutline = languageButtons[i].gameObject.AddComponent<UnityEngine.UI.Outline>();
                languageOutline.effectColor = ColorFromHex("58646C");
                languageOutline.effectDistance = new Vector2(1.5f, -1.5f);
                languageButtonLabels[i] = languageButtons[i].transform.Find("Label").GetComponent<UnityEngine.UI.Text>();
                languageButtonLabels[i].fontSize = 19;
                languageButtonLabels[i].color = ColorFromHex("30343A");
            }

            UnityEngine.UI.Button loginButton = CreateButton(
                "Login Button",
                card.transform,
                "Sign In",
                new Vector2(0.07f, 0.045f),
                new Vector2(0.93f, 0.13f),
                ColorFromHex("08BAD0")
            );
            UnityEngine.UI.Text loginButtonLabel = loginButton.transform.Find("Label")
                .GetComponent<UnityEngine.UI.Text>();

            UnityEngine.UI.Text statusLabel = CreateText(
                "Status Label",
                card.transform,
                string.Empty,
                21,
                FontStyle.Normal,
                ColorFromHex("C44747"),
                new Vector2(0.07f, 0.002f),
                new Vector2(0.93f, 0.043f)
            );
            statusLabel.alignment = TextAnchor.MiddleCenter;

            UnityEngine.UI.Text loadingLabel = CreateText(
                "Loading Indicator",
                card.transform,
                "Đang tải...",
                21,
                FontStyle.Bold,
                ColorFromHex("08BAD0"),
                new Vector2(0.30f, 0.002f),
                new Vector2(0.70f, 0.043f)
            );
            loadingLabel.alignment = TextAnchor.MiddleCenter;
            loadingLabel.gameObject.SetActive(false);

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            SerializedObject controller = new SerializedObject(canvasObject.GetComponent<LoginController>());
            controller.FindProperty("titleLabel").objectReferenceValue = titleLabel;
            controller.FindProperty("subtitleLabel").objectReferenceValue = subtitleLabel;
            controller.FindProperty("employeeLabel").objectReferenceValue = employeeLabel;
            controller.FindProperty("accessCodeLabel").objectReferenceValue = accessCodeLabel;
            controller.FindProperty("roleLabel").objectReferenceValue = roleLabel;
            controller.FindProperty("languageLabel").objectReferenceValue = languageLabel;
            controller.FindProperty("employeeInput").objectReferenceValue = employeeInput;
            controller.FindProperty("accessCodeInput").objectReferenceValue = accessCodeInput;
            controller.FindProperty("roleDropdown").objectReferenceValue = roleDropdown;
            controller.FindProperty("languageDropdown").objectReferenceValue = languageDropdown;
            SetObjectArray(controller.FindProperty("roleButtons"), roleButtons);
            SetObjectArray(controller.FindProperty("roleButtonLabels"), roleButtonLabels);
            SetObjectArray(controller.FindProperty("roleButtonBackgrounds"), roleButtonBackgrounds);
            SetObjectArray(controller.FindProperty("languageButtons"), languageButtons);
            SetObjectArray(controller.FindProperty("languageButtonLabels"), languageButtonLabels);
            SetObjectArray(controller.FindProperty("languageButtonBackgrounds"), languageButtonBackgrounds);
            controller.FindProperty("loginButton").objectReferenceValue = loginButton;
            controller.FindProperty("loginButtonLabel").objectReferenceValue = loginButtonLabel;
            controller.FindProperty("statusLabel").objectReferenceValue = statusLabel;
            controller.FindProperty("loadingIndicator").objectReferenceValue = loadingLabel.gameObject;
            controller.FindProperty("nextScene").stringValue = "WarehouseScene";
            controller.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, LoginScenePath);
            if (mode == NewSceneMode.Additive)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void CreateWarehouseScene()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WarehouseScenePath));

            Scene activeScene = SceneManager.GetActiveScene();
            NewSceneMode mode = string.IsNullOrEmpty(activeScene.path)
                ? NewSceneMode.Single
                : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);

            GameObject sessionObject = new GameObject(
                "AR Session",
                typeof(ARSession),
                typeof(ARInputManager)
            );
            SceneManager.MoveGameObjectToScene(sessionObject, scene);

            GameObject originObject = new GameObject(
                "XR Origin (Mobile AR)",
                typeof(XROrigin),
                typeof(ARPlaneManager),
                typeof(ARRaycastManager)
            );
            SceneManager.MoveGameObjectToScene(originObject, scene);
            originObject.GetComponent<ARPlaneManager>().requestedDetectionMode =
                PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;

            GameObject cameraOffsetObject = new GameObject("Camera Offset");
            cameraOffsetObject.transform.SetParent(originObject.transform, false);

            GameObject cameraObject = new GameObject(
                "AR Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(ARCameraManager),
                typeof(ARCameraBackground),
                typeof(TrackedPoseDriver)
            );
            cameraObject.transform.SetParent(cameraOffsetObject.transform, false);
            cameraObject.tag = "MainCamera";

            Camera arCamera = cameraObject.GetComponent<Camera>();
            arCamera.clearFlags = CameraClearFlags.SolidColor;
            arCamera.backgroundColor = ColorFromHex("07111F");
            arCamera.nearClipPlane = 0.01f;
            arCamera.farClipPlane = 50f;

            XROrigin xrOrigin = originObject.GetComponent<XROrigin>();
            xrOrigin.Origin = originObject;
            xrOrigin.CameraFloorOffsetObject = cameraOffsetObject;
            xrOrigin.Camera = arCamera;
            xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            xrOrigin.CameraYOffset = 0f;

            GameObject canvasObject = new GameObject(
                "Warehouse Canvas",
                typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler),
                typeof(UnityEngine.UI.GraphicRaycaster),
                typeof(WarehouseTaskController),
                typeof(ArSessionStatusPresenter),
                typeof(WarehouseWorkspaceController)
            );
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            UnityEngine.UI.CanvasScaler scaler = canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2400f);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject safeAreaObject = new GameObject(
                "Safe Area",
                typeof(RectTransform),
                typeof(SafeAreaPanel)
            );
            safeAreaObject.transform.SetParent(canvasObject.transform, false);
            RectTransform safeAreaRect = safeAreaObject.GetComponent<RectTransform>();
            safeAreaRect.anchorMin = Vector2.zero;
            safeAreaRect.anchorMax = Vector2.one;
            safeAreaRect.offsetMin = Vector2.zero;
            safeAreaRect.offsetMax = Vector2.zero;

            UnityEngine.UI.Image header = CreateImage(
                "Header Panel",
                safeAreaObject.transform,
                new Color(0.03f, 0.08f, 0.14f, 0.94f),
                new Vector2(0.03f, 0.82f),
                new Vector2(0.97f, 0.97f)
            );

            UnityEngine.UI.Text appTitle = CreateText(
                "App Title",
                header.transform,
                "JarVis Warehouse",
                48,
                FontStyle.Bold,
                ColorFromHex("E9FBFF"),
                new Vector2(0.05f, 0.42f),
                new Vector2(0.72f, 0.92f)
            );
            appTitle.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Text shiftLabel = CreateText(
                "Shift Label",
                header.transform,
                "SHIFT A",
                22,
                FontStyle.Bold,
                ColorFromHex("8FA8B5"),
                new Vector2(0.05f, 0.08f),
                new Vector2(0.60f, 0.43f)
            );
            shiftLabel.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Text arStatusLabel = CreateText(
                "AR Status",
                header.transform,
                "AR STARTING",
                22,
                FontStyle.Bold,
                ColorFromHex("55D6F4"),
                new Vector2(0.62f, 0.08f),
                new Vector2(0.95f, 0.43f)
            );
            arStatusLabel.alignment = TextAnchor.MiddleRight;

            UnityEngine.UI.Image taskCard = CreateImage(
                "Task Card",
                safeAreaObject.transform,
                new Color(0.03f, 0.08f, 0.14f, 0.94f),
                new Vector2(0.03f, 0.34f),
                new Vector2(0.97f, 0.78f)
            );

            UnityEngine.UI.Text mockBadge = CreateText(
                "Mock Badge",
                taskCard.transform,
                "MOCK TASK QUEUE",
                20,
                FontStyle.Bold,
                ColorFromHex("F7C66B"),
                new Vector2(0.06f, 0.84f),
                new Vector2(0.55f, 0.95f)
            );
            mockBadge.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Text taskStateLabel = CreateText(
                "Task State",
                taskCard.transform,
                "READY",
                22,
                FontStyle.Bold,
                ColorFromHex("55D6F4"),
                new Vector2(0.62f, 0.84f),
                new Vector2(0.94f, 0.95f)
            );
            taskStateLabel.alignment = TextAnchor.MiddleRight;

            UnityEngine.UI.Text taskTitleLabel = CreateText(
                "Task Title",
                taskCard.transform,
                "Receive inbound pallet",
                42,
                FontStyle.Bold,
                ColorFromHex("E9FBFF"),
                new Vector2(0.06f, 0.60f),
                new Vector2(0.94f, 0.82f)
            );
            taskTitleLabel.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Text taskDetailLabel = CreateText(
                "Task Detail",
                taskCard.transform,
                "IN-2048\nDock A3\n12 cartons",
                28,
                FontStyle.Normal,
                ColorFromHex("B8C9D1"),
                new Vector2(0.06f, 0.25f),
                new Vector2(0.94f, 0.60f)
            );
            taskDetailLabel.alignment = TextAnchor.UpperLeft;

            UnityEngine.UI.Text progressLabel = CreateText(
                "Progress Label",
                taskCard.transform,
                "0 / 3 TASKS COMPLETE",
                20,
                FontStyle.Bold,
                ColorFromHex("8FA8B5"),
                new Vector2(0.06f, 0.13f),
                new Vector2(0.94f, 0.22f)
            );
            progressLabel.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Image progressBackground = CreateImage(
                "Progress Background",
                taskCard.transform,
                new Color(0.25f, 0.34f, 0.39f, 0.75f),
                new Vector2(0.06f, 0.06f),
                new Vector2(0.94f, 0.11f)
            );
            UnityEngine.UI.Image progressFill = CreateImage(
                "Progress Fill",
                progressBackground.transform,
                ColorFromHex("55D6F4"),
                Vector2.zero,
                Vector2.one
            );
            progressFill.type = UnityEngine.UI.Image.Type.Filled;
            progressFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;

            UnityEngine.UI.Image controlsPanel = CreateImage(
                "Controls Panel",
                safeAreaObject.transform,
                new Color(0.03f, 0.08f, 0.14f, 0.90f),
                new Vector2(0.03f, 0.15f),
                new Vector2(0.97f, 0.30f)
            );

            UnityEngine.UI.Button previousButton = CreateButton(
                "Previous Button",
                controlsPanel.transform,
                "PREV",
                new Vector2(0.03f, 0.18f),
                new Vector2(0.24f, 0.82f),
                ColorFromHex("29404C")
            );
            UnityEngine.UI.Button scanButton = CreateButton(
                "Scan Button",
                controlsPanel.transform,
                "MOCK SCAN",
                new Vector2(0.27f, 0.18f),
                new Vector2(0.49f, 0.82f),
                ColorFromHex("155E75")
            );
            UnityEngine.UI.Button completeButton = CreateButton(
                "Complete Button",
                controlsPanel.transform,
                "COMPLETE",
                new Vector2(0.52f, 0.18f),
                new Vector2(0.75f, 0.82f),
                ColorFromHex("176B4D")
            );
            UnityEngine.UI.Button nextButton = CreateButton(
                "Next Button",
                controlsPanel.transform,
                "NEXT",
                new Vector2(0.78f, 0.18f),
                new Vector2(0.97f, 0.82f),
                ColorFromHex("29404C")
            );

            UnityEngine.UI.Text activityLabel = CreateText(
                "Activity Label",
                safeAreaObject.transform,
                "MOCK DATA - Ready for operator review",
                22,
                FontStyle.Normal,
                ColorFromHex("B8C9D1"),
                new Vector2(0.05f, 0.06f),
                new Vector2(0.95f, 0.12f)
            );
            activityLabel.alignment = TextAnchor.MiddleCenter;

            GameObject eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule)
            );
            SceneManager.MoveGameObjectToScene(eventSystem, scene);

            SerializedObject dashboard = new SerializedObject(
                canvasObject.GetComponent<WarehouseTaskController>()
            );
            dashboard.FindProperty("shiftLabel").objectReferenceValue = shiftLabel;
            dashboard.FindProperty("taskTitleLabel").objectReferenceValue = taskTitleLabel;
            dashboard.FindProperty("taskDetailLabel").objectReferenceValue = taskDetailLabel;
            dashboard.FindProperty("taskStateLabel").objectReferenceValue = taskStateLabel;
            dashboard.FindProperty("progressLabel").objectReferenceValue = progressLabel;
            dashboard.FindProperty("activityLabel").objectReferenceValue = activityLabel;
            dashboard.FindProperty("progressFill").objectReferenceValue = progressFill;
            dashboard.FindProperty("previousButton").objectReferenceValue = previousButton;
            dashboard.FindProperty("scanButton").objectReferenceValue = scanButton;
            dashboard.FindProperty("completeButton").objectReferenceValue = completeButton;
            dashboard.FindProperty("nextButton").objectReferenceValue = nextButton;
            dashboard.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject arStatus = new SerializedObject(
                canvasObject.GetComponent<ArSessionStatusPresenter>()
            );
            arStatus.FindProperty("statusLabel").objectReferenceValue = arStatusLabel;
            arStatus.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject workspace = new SerializedObject(
                canvasObject.GetComponent<WarehouseWorkspaceController>()
            );
            workspace.FindProperty("safeAreaRoot").objectReferenceValue = safeAreaRect;
            workspace.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, WarehouseScenePath);
            if (mode == NewSceneMode.Additive)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void EnsureArBackgroundRendererFeature()
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(
                MobileRendererPath
            );
            if (rendererData == null)
            {
                Debug.LogError($"[JarvisSceneBuilder] Renderer data not found at {MobileRendererPath}.");
                return;
            }

            if (rendererData.rendererFeatures.OfType<ARBackgroundRendererFeature>().Any())
                return;

            ARBackgroundRendererFeature feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "AR Background Renderer Feature";
            feature.Create();
            AssetDatabase.AddObjectToAsset(feature, rendererData);
            rendererData.rendererFeatures.Add(feature);

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
            {
                SerializedObject renderer = new SerializedObject(rendererData);
                SerializedProperty featureMap = renderer.FindProperty("m_RendererFeatureMap");
                featureMap.arraySize = rendererData.rendererFeatures.Count;
                featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
                renderer.ApplyModifiedPropertiesWithoutUndo();
            }

            rendererData.SetDirty();
            EditorUtility.SetDirty(rendererData);
        }

        private static void ConfigureBuildSettings()
        {
            const string importedFigmaScene = "Assets/Scenes/JarVisFigmaApp.unity";
            string[] desiredScenes = File.Exists(importedFigmaScene)
                ? new[] { importedFigmaScene, SplashScenePath, LoginScenePath, WarehouseScenePath }
                : new[] { SplashScenePath, LoginScenePath, WarehouseScenePath };
            EditorBuildSettings.scenes = desiredScenes
                .Where(File.Exists)
                .Select(path => new EditorBuildSettingsScene(path, true))
                .ToArray();
            if (File.Exists(importedFigmaScene))
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(importedFigmaScene);
        }

        private static UnityEngine.UI.Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image)
            );
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static UnityEngine.UI.Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Color color
        )
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button)
            );
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnityEngine.UI.Image image = buttonObject.GetComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = true;

            UnityEngine.UI.Button button = buttonObject.GetComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            UnityEngine.UI.ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.86f, 0.94f, 0.98f, 1f);
            colors.pressedColor = new Color(0.68f, 0.82f, 0.88f, 1f);
            colors.disabledColor = new Color(0.45f, 0.50f, 0.53f, 0.55f);
            colors.colorMultiplier = 1f;
            button.colors = colors;

            UnityEngine.UI.Text buttonLabel = CreateText(
                "Label",
                buttonObject.transform,
                label,
                21,
                FontStyle.Bold,
                ColorFromHex("E9FBFF"),
                Vector2.zero,
                Vector2.one
            );
            buttonLabel.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void StyleLightInput(UnityEngine.UI.InputField input)
        {
            UnityEngine.UI.Image background = input.GetComponent<UnityEngine.UI.Image>();
            background.color = ColorFromHex("FFFFFF");

            UnityEngine.UI.Outline outline = input.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = ColorFromHex("D8DEE4");
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            if (input.placeholder is UnityEngine.UI.Text placeholder)
            {
                placeholder.color = ColorFromHex("9AA0A6");
                placeholder.fontStyle = FontStyle.Normal;
            }

            if (input.textComponent != null)
                input.textComponent.color = ColorFromHex("202124");
        }

        private static void SetObjectArray(
            SerializedProperty property,
            UnityEngine.Object[] values
        )
        {
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static UnityEngine.UI.InputField CreateInputField(
            string name,
            Transform parent,
            string placeholderValue,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool password
        )
        {
            GameObject inputObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.InputField)
            );
            inputObject.transform.SetParent(parent, false);
            RectTransform rect = inputObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnityEngine.UI.Image background = inputObject.GetComponent<UnityEngine.UI.Image>();
            background.color = ColorFromHex("132633");

            UnityEngine.UI.Text placeholder = CreateText(
                "Placeholder",
                inputObject.transform,
                placeholderValue,
                24,
                FontStyle.Italic,
                ColorFromHex("758D99"),
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f)
            );
            placeholder.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Text valueText = CreateText(
                "Text",
                inputObject.transform,
                string.Empty,
                25,
                FontStyle.Normal,
                ColorFromHex("E9FBFF"),
                new Vector2(0.04f, 0.08f),
                new Vector2(0.96f, 0.92f)
            );
            valueText.alignment = TextAnchor.MiddleLeft;
            valueText.supportRichText = false;

            UnityEngine.UI.InputField input = inputObject.GetComponent<UnityEngine.UI.InputField>();
            input.targetGraphic = background;
            input.textComponent = valueText;
            input.placeholder = placeholder;
            input.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
            input.contentType = password
                ? UnityEngine.UI.InputField.ContentType.Password
                : UnityEngine.UI.InputField.ContentType.Standard;
            return input;
        }

        private static UnityEngine.UI.Dropdown CreateDropdown(
            string name,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject dropdownObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Dropdown)
            );
            dropdownObject.transform.SetParent(parent, false);
            RectTransform rect = dropdownObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnityEngine.UI.Image background = dropdownObject.GetComponent<UnityEngine.UI.Image>();
            background.color = ColorFromHex("132633");

            UnityEngine.UI.Text caption = CreateText(
                "Label",
                dropdownObject.transform,
                string.Empty,
                22,
                FontStyle.Normal,
                ColorFromHex("E9FBFF"),
                new Vector2(0.06f, 0.08f),
                new Vector2(0.82f, 0.92f)
            );
            caption.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Text arrow = CreateText(
                "Arrow",
                dropdownObject.transform,
                "▼",
                20,
                FontStyle.Bold,
                ColorFromHex("55D6F4"),
                new Vector2(0.82f, 0.08f),
                new Vector2(0.96f, 0.92f)
            );
            arrow.alignment = TextAnchor.MiddleCenter;

            GameObject templateObject = new GameObject(
                "Template",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.ScrollRect)
            );
            templateObject.transform.SetParent(dropdownObject.transform, false);
            RectTransform templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 240f);
            templateObject.GetComponent<UnityEngine.UI.Image>().color = ColorFromHex("132633");

            GameObject viewportObject = new GameObject(
                "Viewport",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Mask)
            );
            viewportObject.transform.SetParent(templateObject.transform, false);
            RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(4f, 4f);
            viewportRect.offsetMax = new Vector2(-4f, -4f);
            UnityEngine.UI.Image viewportImage = viewportObject.GetComponent<UnityEngine.UI.Image>();
            viewportImage.color = Color.white;
            viewportObject.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            RectTransform contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 72f);

            GameObject itemObject = new GameObject(
                "Item",
                typeof(RectTransform),
                typeof(UnityEngine.UI.Toggle)
            );
            itemObject.transform.SetParent(contentObject.transform, false);
            RectTransform itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 72f);

            UnityEngine.UI.Image itemBackground = CreateImage(
                "Item Background",
                itemObject.transform,
                ColorFromHex("1A3444"),
                Vector2.zero,
                Vector2.one
            );
            itemBackground.raycastTarget = true;

            UnityEngine.UI.Image checkmark = CreateImage(
                "Item Checkmark",
                itemObject.transform,
                ColorFromHex("55D6F4"),
                new Vector2(0.03f, 0.30f),
                new Vector2(0.08f, 0.70f)
            );

            UnityEngine.UI.Text itemLabel = CreateText(
                "Item Label",
                itemObject.transform,
                "Option",
                22,
                FontStyle.Normal,
                ColorFromHex("E9FBFF"),
                new Vector2(0.11f, 0.08f),
                new Vector2(0.96f, 0.92f)
            );
            itemLabel.alignment = TextAnchor.MiddleLeft;

            UnityEngine.UI.Toggle itemToggle = itemObject.GetComponent<UnityEngine.UI.Toggle>();
            itemToggle.targetGraphic = itemBackground;
            itemToggle.graphic = checkmark;
            itemToggle.isOn = true;

            UnityEngine.UI.ScrollRect scrollRect = templateObject.GetComponent<UnityEngine.UI.ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

            UnityEngine.UI.Dropdown dropdown = dropdownObject.GetComponent<UnityEngine.UI.Dropdown>();
            dropdown.targetGraphic = background;
            dropdown.template = templateRect;
            dropdown.captionText = caption;
            dropdown.itemText = itemLabel;
            templateObject.SetActive(false);
            return dropdown;
        }

        private static UnityEngine.UI.Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax
        )
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(UnityEngine.UI.Text)
            );
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            UnityEngine.UI.Text text = textObject.GetComponent<UnityEngine.UI.Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static Color ColorFromHex(string hex)
        {
            return ColorUtility.TryParseHtmlString($"#{hex}", out Color color) ? color : Color.white;
        }
    }
}
