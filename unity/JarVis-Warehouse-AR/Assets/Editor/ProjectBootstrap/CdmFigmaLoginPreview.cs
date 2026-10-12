using System;
using System.IO;
using Cdm.Figma.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    // Creates a preview from the imported Figma hierarchy without touching LoginScene.
    public static class CdmFigmaLoginPreview
    {
        private const string ImportPath = "Assets/JarVis/DesignImports/JarVis-Login-5-36.figma";
        private const string PreviewPath = "Assets/Scenes/JarVisFigmaLoginPreview.unity";

        public static void Capture()
        {
            Scene scene = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            Camera camera = null;
            Canvas canvas = null;
            foreach (GameObject item in scene.GetRootGameObjects())
            {
                if (item.name == "Figma Login Preview Camera") camera = item.GetComponent<Camera>();
                if (item.name == "Figma Login Preview Canvas") canvas = item.GetComponent<Canvas>();
            }
            if (camera == null || canvas == null)
                throw new InvalidOperationException("Preview camera or Canvas is missing.");
            foreach (FigmaNode node in canvas.GetComponentsInChildren<FigmaNode>(true))
            {
                if (node.nodeId != "5:66" && node.nodeId != "5:564" && node.nodeId != "5:565") continue;
                RectTransform rect = node.GetComponent<RectTransform>();
                string types = string.Join(",", Array.ConvertAll(node.GetComponents<Component>(), c => c.GetType().Name));
                Debug.Log("[JarVis Figma] Node " + node.nodeId + " " + types
                    + " size=" + (rect != null ? rect.rect.size.ToString() : "none")
                    + " scale=" + node.transform.localScale);
                UnityEngine.UI.Image image = node.GetComponent<UnityEngine.UI.Image>();
                if (image != null)
                    Debug.Log("[JarVis Figma] Image " + node.nodeId + " mesh=" + image.useSpriteMesh
                        + " sprite=" + (image.sprite != null ? image.sprite.rect.ToString() : "none")
                        + " worldScale=" + node.transform.lossyScale
                        + " parentScale=" + node.transform.parent.localScale);
            }

            const int width = 412;
            const int height = 915;
            RenderTexture target = new RenderTexture(width, height, 24);
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderMode previousMode = canvas.renderMode;
            Camera previousWorldCamera = canvas.worldCamera;
            try
            {
                canvas.transform.localScale = Vector3.one;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply();
                Directory.CreateDirectory("Logs");
                File.WriteAllBytes("Logs/FigmaLoginPreview-412x915.png", output.EncodeToPNG());
                Debug.Log("[JarVis Figma] Captured 412x915 imported Login preview.");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousWorldCamera;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(output);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [MenuItem("Tools/JarVis/Open Imported Figma Login")]
        public static void Open()
        {
            Scene scene = EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            FigmaNode login = null;
            Canvas canvas = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Figma Login Preview Canvas") canvas = root.GetComponent<Canvas>();
            foreach (FigmaNode node in root.GetComponentsInChildren<FigmaNode>(true))
                if (node.nodeId == "5:36") login = node;
            }
            if (login == null)
                throw new InvalidOperationException("Imported Login 5:36 is missing from preview scene.");
            if (canvas == null)
                throw new InvalidOperationException("Preview Canvas is missing.");
            canvas.transform.localScale = Vector3.one;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Debug.Log("[JarVis Figma] Opened Login 5:36 from Figma; hierarchy is present.");
        }

        public static void Build()
        {
            GameObject designAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ImportPath);
            if (designAsset == null)
                throw new InvalidOperationException("Figma Login asset did not import: " + ImportPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Figma Login Preview Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            cameraObject.tag = "MainCamera";

            var canvasObject = new GameObject("Figma Login Preview Canvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.transform.localScale = Vector3.one;
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            // The preview is authored at exactly 412x915. Constant scale also
            // avoids persisting a zero scale when a batch Editor has no Game View.
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referenceResolution = new Vector2(412, 915);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            FigmaDesign design = designAsset.GetComponent<FigmaDesign>();
            if (design == null || design.document == null || design.document.pages.Count != 1)
                throw new InvalidOperationException("Login import must contain exactly one Figma page.");
            FigmaPage page = design.document.pages[0];
            FigmaNode loginNode = null;
            foreach (FigmaNode candidate in page.GetComponentsInChildren<FigmaNode>(true))
            {
                if (candidate.nodeId != "5:36") continue;
                loginNode = candidate;
                break;
            }
            if (loginNode == null)
                throw new InvalidOperationException("Figma Login node 5:36 is missing.");
            GameObject imported = UnityEngine.Object.Instantiate(
                loginNode.gameObject, canvasObject.transform, false);
            imported.name = "Figma Login 5-36";
            imported.transform.localScale = Vector3.one;
            if (imported.transform.parent != canvasObject.transform)
                throw new InvalidOperationException("Imported Login is not under preview Canvas.");
            var root = imported.GetComponent<RectTransform>();
            if (root == null)
                throw new InvalidOperationException("Imported Login has no RectTransform.");
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            // cdmvision wraps this 65px placeholder after substituting the
            // project's Vietnamese font. Keep the original Figma text on one line.
            foreach (FigmaNode node in imported.GetComponentsInChildren<FigmaNode>(true))
            {
                if (node.nodeId == "5:66")
                {
                    TMP_Text placeholder = node.GetComponent<TMP_Text>();
                    if (placeholder != null)
                    {
                        placeholder.enableWordWrapping = false;
                        placeholder.overflowMode = TextOverflowModes.Overflow;
                    }
                }
                else if (node.nodeId == "5:565")
                {
                    // The importer's SVG raster for Figma's 17px eye vector
                    // overdraws its RectTransform by roughly 5x in Unity 6.
                    node.transform.localScale = new Vector3(.2f, .2f, 1f);
                }
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            canvasObject.transform.localScale = Vector3.one;
            EditorSceneManager.SaveScene(scene, PreviewPath);
            Debug.Log("[JarVis Figma] Login preview scene created from imported design: " + PreviewPath);
        }
    }
}
