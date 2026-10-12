using System.IO;
using JarVis.Warehouse.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    // Produces editable Unity assets from the lower-row Figma inventory layout.
    [InitializeOnLoad]
    public static class JarvisInventoryPrefabBuilder
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string SpritePath = ResourcesFolder + "/JarVisRoundedPanel.png";
        private const string PrefabPath = ResourcesFolder + "/JarVisInventoryPanel.prefab";
        private const string PreviewPath = "Assets/Scenes/JarVisInventoryPreview.unity";

        static JarvisInventoryPrefabBuilder()
        {
            EditorApplication.delayCall += RepairExistingPrefabReferences;
        }

        [MenuItem("Tools/JarVis/Build Inventory Prefab and Preview")]
        public static void Build()
        {
            Directory.CreateDirectory(ResourcesFolder);
            CreateRoundedSprite();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var root = new GameObject("Inventory Panel", typeof(RectTransform),
                    typeof(WarehouseInventoryScreen));
                RectTransform rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                root.GetComponent<WarehouseInventoryScreen>().Build(null, null, null, null,
                    null, null, null);
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Object.DestroyImmediate(root);
            }
            if (prefab == null)
                throw new IOException("Failed to create Unity inventory prefab");
            RepairExistingPrefabReferences();

            if (File.Exists(PreviewPath))
            {
                Debug.Log("[JarVis] Inventory prefab and preview already exist; user edits were preserved.");
                return;
            }
            Scene activeScene = SceneManager.GetActiveScene();
            NewSceneMode mode = string.IsNullOrEmpty(activeScene.path)
                ? NewSceneMode.Single : NewSceneMode.Additive;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            var canvasObject = new GameObject("Inventory Preview Canvas", typeof(Canvas),
                typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.UI.CanvasScaler scaler =
                canvasObject.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(412, 915);
            scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(canvasObject.transform, false);
            RectTransform screen = instance.GetComponent<RectTransform>();
            screen.anchorMin = Vector2.zero;
            screen.anchorMax = Vector2.one;
            screen.offsetMin = screen.offsetMax = Vector2.zero;

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            EditorSceneManager.SaveScene(scene, PreviewPath);
            if (mode == NewSceneMode.Additive)
                EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();
            Debug.Log("[JarVis] Inventory prefab and Unity preview scene are ready.");
        }

        [MenuItem("Tools/JarVis/Open Figma Inventory Preview")]
        public static void OpenPreview()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                Build();
            EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Debug.Log("[JarVis] Opened editable inventory preview. Runtime uses the same prefab with OMS data.");
        }

        private static void CreateRoundedSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath) != null) return;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 7.5f);
                float dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 7.5f);
                texture.SetPixel(x, y, new Color(1, 1, 1,
                    Mathf.Clamp01(8.5f - Mathf.Sqrt(dx * dx + dy * dy))));
            }
            texture.Apply();
            File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteBorder = new Vector4(9, 9, 9, 9);
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void RepairExistingPrefabReferences()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                return;
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                WarehouseInventoryScreen view = contents.GetComponent<WarehouseInventoryScreen>();
                if (view == null) return;
                SerializedObject serialized = new SerializedObject(view);
                if (serialized.FindProperty("search").objectReferenceValue != null) return;
                view.RepairReferences();
                EditorUtility.SetDirty(view);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                Debug.Log("[JarVis] Inventory prefab references repaired without changing its layout.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
