using JarVis.Warehouse.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JarVis.Warehouse.Demo2026
{
    /// <summary>Unscaled press and overlay motion. Attach to the rect that should move.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class JarVisMotion : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler
    {
        private const float PressDuration = .12f, EnterDuration = .28f;
        private RectTransform rect;
        private Selectable selectable;
        private CanvasGroup group;
        private Vector3 restScale;
        private Vector2 restPosition;
        private float restAlpha = 1f, press = 1f, pressFrom = 1f, pressTo = 1f, pressTime;
        private float enterTime;
        private bool entering, initialized, ownsPointer, overlayOwned;
        private int pointerId;

        public static JarVisMotion Attach(RectTransform target)
        {
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            var motion = target.GetComponent<JarVisMotion>();
            return motion != null ? motion : target.gameObject.AddComponent<JarVisMotion>();
        }

        public static JarVisMotion OverlayEnter(RectTransform target)
        {
            var motion = Attach(target);
            motion.OverlayEnter();
            return motion;
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized) return;
            rect = (RectTransform)transform;
            selectable = GetComponent<Selectable>();
            restScale = rect.localScale;
            restPosition = rect.anchoredPosition;
            initialized = true;
        }

        /// <summary>Call after activating/positioning the overlay. Does not take input or camera ownership.</summary>
        public void OverlayEnter()
        {
            Initialize();
            if (!entering)
            {
                restPosition = rect.anchoredPosition;
                restScale = rect.localScale / Mathf.Max(.001f, press);
                group = GetComponent<CanvasGroup>();
                if (group == null) group = gameObject.AddComponent<CanvasGroup>();
                restAlpha = group.alpha;
            }
            enterTime = 0f;
            overlayOwned = true;
            entering = isActiveAndEnabled && !PrototypeUi.ReducedMotion;
            Apply();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || eventData.button != PointerEventData.InputButton.Left || ownsPointer) return;
            if (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable())) return;
            Initialize();
            if (!entering && Mathf.Approximately(press, 1f)) restScale = rect.localScale;
            pointerId = eventData.pointerId;
            ownsPointer = true;
            TweenPress(.98f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (ownsPointer && eventData.pointerId == pointerId) Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (ownsPointer && eventData.pointerId == pointerId) Release();
        }

        public void OnCancel(BaseEventData eventData) { Release(); }

        private void Release()
        {
            ownsPointer = false;
            TweenPress(1f);
        }

        private void TweenPress(float target)
        {
            Initialize();
            pressFrom = press;
            pressTo = PrototypeUi.ReducedMotion ? 1f : target;
            pressTime = 0f;
            if (PrototypeUi.ReducedMotion) { press = 1f; Apply(); }
        }

        private void Update()
        {
            if (PrototypeUi.ReducedMotion)
            {
                if (entering || press != 1f || pressTo != 1f)
                {
                    entering = false;
                    press = pressTo = 1f;
                    Apply();
                }
                return;
            }
            if (ownsPointer && selectable != null && !selectable.IsInteractable()) Release();
            bool changed = false;
            if (!Mathf.Approximately(press, pressTo))
            {
                pressTime += Time.unscaledDeltaTime;
                press = Mathf.Lerp(pressFrom, pressTo, EaseOut(pressTime / PressDuration));
                if (pressTime >= PressDuration) press = pressTo;
                changed = true;
            }
            if (entering)
            {
                enterTime += Time.unscaledDeltaTime;
                if (enterTime >= EnterDuration) entering = false;
                changed = true;
            }
            if (changed) Apply();
        }

        private static float EaseOut(float value)
        {
            float remaining = 1f - Mathf.Clamp01(value);
            return 1f - remaining * remaining * remaining;
        }

        private void Apply()
        {
            float t = entering ? EaseOut(enterTime / EnterDuration) : 1f;
            rect.localScale = restScale * (press * Mathf.Lerp(.985f, 1f, t));
            if (overlayOwned && group != null)
            {
                // Positive logical y is downward, hence a negative anchored offset.
                rect.anchoredPosition = restPosition + Vector2.down * (12f * (1f - t));
                group.alpha = restAlpha * t;
                if (!entering) overlayOwned = false;
            }
        }

        private void OnDisable()
        {
            if (!initialized) return;
            bool restore = entering || ownsPointer || press != 1f;
            ownsPointer = entering = false;
            press = pressTo = 1f;
            if (restore) Apply();
            // Capture a fresh externally assigned layout when the object is reused.
            initialized = false;
        }
    }

    /// <summary>Decorative scan sweep only. Completion always comes from SetProgress, never a timer.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class JarVisScanSweep : MonoBehaviour
    {
        public bool IsBarcode;
        public bool Scanning = true;
        [Tooltip("Negative uses live motion; 0..1 selects a deterministic sweep cycle.")]
        public float PreviewPhase = -1f;
        public float Progress { get; private set; }

        private RectTransform viewport, line, band;
        private Image lineImage, bandImage;
        private float phase;
        private bool lastScanning, lastBarcode, lastReduced;
        private float lastPreview = -2f;
        private static readonly Color SweepInk = new Color(.22f, .88f, 1f, 1f);

        private void OnEnable() { RefreshVisual(); }

        public void SetProgress(float value)
        {
            Progress = JarVisMesh.Unit(value);
            RefreshVisual();
        }

        private void EnsureVisuals()
        {
            if (viewport != null) return;
            viewport = JarVisVisuals.Rect(transform, "JarVis sweep clip", 0f, 0f, 0f, 0f);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(.5f, .5f);
            viewport.anchoredPosition = Vector2.zero;
            viewport.sizeDelta = new Vector2(-8f, -8f);
            viewport.gameObject.AddComponent<RectMask2D>();
            bandImage = MakeImage(viewport, "Faint scan band");
            band = bandImage.rectTransform;
            lineImage = MakeImage(viewport, "Scan line");
            line = lineImage.rectTransform;
        }

        private static Image MakeImage(Transform parent, string name)
        {
            var rect = JarVisVisuals.Rect(parent, name, 0f, 0f, 0f, 0f);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public void RefreshVisual()
        {
            // Do not construct children during an inactive setup; OnEnable will build them.
            if (!isActiveAndEnabled) { if (viewport != null) viewport.gameObject.SetActive(false); return; }
            EnsureVisuals();
            bool reduced = PrototypeUi.ReducedMotion;
            bool active = Scanning && Progress < 1f;
            viewport.gameObject.SetActive(active);
            lastScanning = Scanning;
            lastBarcode = IsBarcode;
            lastPreview = PreviewPhase;
            lastReduced = reduced;
            if (!active) return;
            float cycle = reduced ? .25f : PreviewPhase >= 0f ? JarVisMesh.Unit(PreviewPhase) : phase;
            float travel = .5f - .5f * Mathf.Cos(cycle * Mathf.PI * 2f);
            // Read the frame rect directly so the first refresh does not depend on a layout pass.
            Vector2 frameSize = ((RectTransform)transform).rect.size;
            float width = Mathf.Max(0f, frameSize.x - 8f), height = Mathf.Max(0f, frameSize.y - 8f);
            float direction = Mathf.Sin(cycle * Mathf.PI * 2f) >= 0f ? 1f : -1f;
            float thickness = 2f, bandSize = Mathf.Min(24f, (IsBarcode ? width : height) * .12f);
            if (IsBarcode)
            {
                float x = Mathf.Lerp(-width * .5f, width * .5f, travel);
                line.sizeDelta = new Vector2(thickness, height);
                band.sizeDelta = new Vector2(bandSize, height);
                line.anchoredPosition = new Vector2(x, 0f);
                band.anchoredPosition = new Vector2(x - direction * bandSize * .5f, 0f);
            }
            else
            {
                float y = Mathf.Lerp(height * .5f, -height * .5f, travel);
                line.sizeDelta = new Vector2(width, thickness);
                band.sizeDelta = new Vector2(width, bandSize);
                line.anchoredPosition = new Vector2(0f, y);
                band.anchoredPosition = new Vector2(0f, y + direction * bandSize * .5f);
            }
            lineImage.color = SweepInk * new Color(1f, 1f, 1f, .85f);
            bandImage.color = SweepInk * new Color(1f, 1f, 1f, .075f);
        }

        private void Update()
        {
            bool reduced = PrototypeUi.ReducedMotion;
            bool changed = lastScanning != Scanning || lastBarcode != IsBarcode || lastReduced != reduced || lastPreview != PreviewPhase;
            if (Scanning && Progress < 1f && !reduced && PreviewPhase < 0f)
            {
                phase = Mathf.Repeat(phase + Time.unscaledDeltaTime / 2.8f, 1f);
                changed = true;
            }
            if (changed) RefreshVisual();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (viewport != null && isActiveAndEnabled) RefreshVisual();
        }

        private void OnDisable()
        {
            if (viewport != null) viewport.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (viewport == null) return;
            if (Application.isPlaying) Destroy(viewport.gameObject);
            else DestroyImmediate(viewport.gameObject);
        }
    }
}
