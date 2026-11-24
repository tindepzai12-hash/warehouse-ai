using System.Collections;
using System.Collections.Generic;
using Immersal.XR;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // Keep the app session alive while the genuine Immersal sample owns AR/camera input.
    public sealed class ImmersalMapLauncher : MonoBehaviour
    {
        public const string SceneName = "ARNavigation_Immersal_Video";
        static ImmersalMapLauncher instance;
        readonly List<GameObject> suspended = new List<GameObject>();
        Scene home;
        int frameRate;
        bool closing;
        ImportedFigmaNavigation nav;
        bool English => ImportedAppLocalization.IsEnglish;

        public static void Open(ImportedFigmaNavigation owner)
        {
            if (instance != null || SceneManager.GetSceneByName(SceneName).isLoaded) return;
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                owner.Notice(ImportedAppLocalization.IsEnglish ? "Immersal AR is not included in this build yet." : "Bản này chưa có scene Immersal AR.");
                return;
            }
            instance = new GameObject("JarVis Immersal map bridge").AddComponent<ImmersalMapLauncher>();
            instance.nav = owner;
            instance.StartCoroutine(instance.Launch());
        }

        IEnumerator Launch()
        {
            home = SceneManager.GetActiveScene();
            frameRate = Application.targetFrameRate;
            foreach (var root in home.GetRootGameObjects())
                if (root != gameObject && root.activeSelf) { suspended.Add(root); root.SetActive(false); }
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive); }
            catch (System.Exception) { }
            if (operation == null)
            {
                Restore();
                nav.Notice(English ? "Unable to open Immersal AR. Please try again." : "Không mở được Immersal AR. Hãy thử lại.");
                Destroy(gameObject);
                yield break;
            }
            yield return operation;
            var scene = SceneManager.GetSceneByName(SceneName);
            if (!scene.IsValid() || !scene.isLoaded) { Close(); yield break; }
            SceneManager.SetActiveScene(scene);
            AddControls(scene);
        }

        void AddControls(Scene scene)
        {
            TMP_FontAsset font = nav.GetComponentInChildren<TMP_Text>(true)?.font;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var venue in root.GetComponentsInChildren<JarVis.Warehouse.Navigation.RoompresentVenue>(true))
                {
                    var delivery = venue.GetComponent<JarVis.Warehouse.Navigation.RoompresentDeliveryAR>();
                    if (delivery == null) delivery = venue.gameObject.AddComponent<JarVis.Warehouse.Navigation.RoompresentDeliveryAR>();
                    delivery.Configure(font);
                    return;
                }
            }
            bool mapConfigured = false;
            bool alignmentPending = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var map in root.GetComponentsInChildren<XRMap>(true)) mapConfigured |= map.IsConfigured;
                foreach (var venue in root.GetComponentsInChildren<JarVis.Warehouse.Navigation.RoompresentVenue>(true)) alignmentPending |= !venue.alignmentVerified;
            }
            var overlay = new GameObject("JarVis Immersal controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(overlay, scene);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(412, 915);
            scaler.matchWidthOrHeight = .5f;
            var safe = new GameObject("Safe area", typeof(RectTransform)).GetComponent<RectTransform>();
            safe.SetParent(overlay.transform, false);
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            // Bottom controls leave the sample's localization counter visible at the top.
            var dock = new GameObject("Map controls", typeof(RectTransform)).GetComponent<RectTransform>();
            dock.SetParent(safe, false);
            dock.anchorMin = dock.anchorMax = new Vector2(.5f, 0);
            dock.pivot = new Vector2(.5f, 0);
            dock.anchoredPosition = new Vector2(0, 16);
            dock.sizeDelta = new Vector2(366, 142);
            var panel = PrototypeUi.Panel(dock, "Immersal status", 0, 0, 366, 142, true);
            PrototypeUi.Text(panel, font, "Immersal AR", 16, 10, 334, 25, 18, true, true);
            PrototypeUi.Text(panel, font, alignmentPending
                ? (English ? "Room map imported. Alignment must be checked before AR guidance." : "Đã nhập map phòng. Cần kiểm tra căn map trước khi dẫn đường AR.")
                : mapConfigured
                ? (English ? "Awaiting localization. Follow the map status above." : "Đang chờ định vị. Theo dõi trạng thái bản đồ phía trên.")
                : (English ? "No localization map yet. Add your map in Unity to continue." : "Chưa có bản đồ định vị. Thêm map trong Unity để tiếp tục."),
                16, 39, 334, 42, 13, true);
            PrototypeUi.Action(panel, font, English ? "Back to warehouse map" : "Về bản đồ kho", 16, 88, 334, 44, true, Close, true);
        }

        void Update()
        {
            if (!closing && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true) Close();
        }
        public static void Close()
        {
            if (instance == null || instance.closing) return;
            instance.closing = true;
            instance.StartCoroutine(instance.Return());
        }
        IEnumerator Return()
        {
            if (home.IsValid() && home.isLoaded) SceneManager.SetActiveScene(home);
            var scene = SceneManager.GetSceneByName(SceneName);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            Restore();
            Destroy(gameObject);
        }
        void Restore()
        {
            foreach (var root in suspended) if (root != null) root.SetActive(true);
            suspended.Clear();
            Application.targetFrameRate = frameRate;
        }
        void OnDestroy() { Restore(); if (instance == this) instance = null; }
    }
}
