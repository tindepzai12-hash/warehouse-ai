using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class JarvisFigmaMascotSync
    {
        private const string SpritePath = "Assets/Resources/Figma/JarVisMascotLogin.png";
        private const string SplashPath = "Assets/Scenes/SplashScene.unity";
        private const string LoginPath = "Assets/Scenes/LoginScene.unity";

        [MenuItem("Tools/JarVis/Apply Figma Mascot to Splash and Login")]
        public static void Apply()
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            Sprite mascot = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (mascot == null)
                throw new InvalidOperationException("The Figma mascot PNG is missing or is not a sprite: " + SpritePath);

            string previouslyOpenScene = SceneManager.GetActiveScene().path;
            UpdateScene(SplashPath, mascot, UpdateSplash);
            UpdateScene(LoginPath, mascot, UpdateLogin);
            if (!string.IsNullOrEmpty(previouslyOpenScene) && previouslyOpenScene != LoginPath)
                EditorSceneManager.OpenScene(previouslyOpenScene, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("[JarVis] Original Figma mascot applied to Splash and Login scenes.");
        }

        private static void UpdateScene(string path, Sprite mascot, Action<Scene, Sprite> update)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            update(scene, mascot);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpdateSplash(Scene scene, Sprite mascot)
        {
            Transform canvas = RequiredRoot(scene, "Splash Canvas");
            Transform safeArea = RequiredChild(canvas, "Safe Area");
            UnityEngine.UI.Image background = RequiredChild(canvas, "Background")
                .GetComponent<UnityEngine.UI.Image>();
            background.color = Color.white;
            Transform glow = canvas.Find("Glow");
            if (glow != null) glow.gameObject.SetActive(false);
            Camera camera = RequiredRoot(scene, "Splash Camera").GetComponent<Camera>();
            camera.backgroundColor = Color.white;

            var title = RequiredChild(safeArea, "JarVis Label").GetComponent<UnityEngine.UI.Text>();
            title.text = "JarVis";
            title.color = ColorFromHex("1762C4");
            var subtitle = RequiredChild(safeArea, "Subtitle").GetComponent<UnityEngine.UI.Text>();
            subtitle.text = "Trợ lý kho thông minh";
            subtitle.color = ColorFromHex("52627C");
            var footer = RequiredChild(safeArea, "Status").GetComponent<UnityEngine.UI.Text>();
            footer.text = "Được phát triển bởi Justin Nguyễn";
            footer.color = ColorFromHex("8893A6");

            UnityEngine.UI.Image image = GetOrCreateImage(safeArea, "JarVis Figma Mascot");
            image.sprite = mascot;
            RectTransform rect = (RectTransform)image.transform;
            SetAnchors(rect, new Vector2(.39f, .59f), new Vector2(.61f, .69f));
        }

        private static void UpdateLogin(Scene scene, Sprite mascot)
        {
            Transform canvas = RequiredRoot(scene, "Login Canvas");
            Transform safeArea = RequiredChild(canvas, "Safe Area");
            Transform oldMark = RequiredChild(safeArea, "JarVis Robot Mark");
            var placeholder = oldMark.GetComponent<UnityEngine.UI.Text>();
            if (placeholder != null) placeholder.enabled = false;
            UnityEngine.UI.Image image = GetOrCreateImage(safeArea, "JarVis Figma Mascot");
            image.sprite = mascot;
            RectTransform rect = (RectTransform)image.transform;
            SetAnchors(rect, new Vector2(.39f, .66f), new Vector2(.61f, .76f));
        }

        private static UnityEngine.UI.Image GetOrCreateImage(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            GameObject child = existing == null
                ? new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image))
                : existing.gameObject;
            if (existing == null) child.transform.SetParent(parent, false);
            var image = child.GetComponent<UnityEngine.UI.Image>();
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Transform RequiredRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root.transform;
            throw new InvalidOperationException(scene.path + " is missing " + name);
        }

        private static Transform RequiredChild(Transform parent, string path)
        {
            Transform child = parent.Find(path);
            if (child == null)
                throw new InvalidOperationException(parent.name + " is missing " + path);
            return child;
        }

        private static Color ColorFromHex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color color);
            return color;
        }
    }
}
