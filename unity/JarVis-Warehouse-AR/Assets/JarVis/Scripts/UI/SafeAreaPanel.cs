using UnityEngine;

namespace JarVis.Warehouse.UI
{
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaPanel : MonoBehaviour
    {
        private RectTransform panel;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void OnEnable()
        {
            if (panel == null)
                panel = GetComponent<RectTransform>();

            ApplySafeArea();
        }

        private void Update()
        {
            Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.safeArea != appliedSafeArea || screenSize != appliedScreenSize)
                ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (panel == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safeArea = Screen.safeArea;
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            panel.anchorMin = safeArea.position / screenSize;
            panel.anchorMax = (safeArea.position + safeArea.size) / screenSize;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            appliedSafeArea = safeArea;
            appliedScreenSize = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
