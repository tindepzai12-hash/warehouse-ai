using System;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Demo2026
{
    /// <summary>Flat UGUI building blocks. Coordinates are logical units from the top left.</summary>
    public static class JarVisVisuals
    {
        public static TMP_FontAsset Regular, Bold;
        private static bool fontsAttempted;

        public static Color Hex(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A hex color is required.", nameof(value));
            string hex = value.Trim();
            if (hex[0] != '#') hex = "#" + hex;
            Color result;
            if (!ColorUtility.TryParseHtmlString(hex, out result))
                throw new ArgumentException("Invalid hex color: " + value, nameof(value));
            return result;
        }

        public static void EnsureFonts()
        {
            if (fontsAttempted) return;
            fontsAttempted = true;
            if (Regular == null) Regular = Resources.Load<TMP_FontAsset>("BeVietnamPro Figma SDF");
            if (Bold == null) Bold = Resources.Load<TMP_FontAsset>("BeVietnamPro-Bold SDF");
            if (Regular == null || Bold == null)
                Debug.LogWarning("JarVis demo: place BeVietnamPro Figma SDF and BeVietnamPro-Bold SDF in Resources, or assign JarVisVisuals.Regular/Bold before building UI.");
            if (Regular == null) Regular = TMP_Settings.defaultFontAsset;
            if (Bold == null) Bold = Regular;
        }

        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name ?? "Rect", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, w), Mathf.Max(0f, h));
            return rect;
        }

        public static RectTransform Panel(Transform parent, string name, float x, float y, float w, float h,
            Color fill, float radius = 20f)
        {
            var rect = Rect(parent, name, x, y, w, h);
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(w, h) * .5f);
            if (Shader.Find("JarVis/Uniform UI Surface") != null)
            {
                var image = rect.gameObject.AddComponent<Image>();
                image.color = Color.white;
                image.raycastTarget = false;
                UniformUiSurface.Use(image, radius, fill, Color.clear, 0f, false, false);
            }
            else
            {
                // A flat rounded mesh also works when the project's surface shader is stripped.
                var surface = rect.gameObject.AddComponent<JarVisFlatSurface>();
                surface.Radius = radius;
                surface.color = fill;
                surface.raycastTarget = false;
            }
            return rect;
        }

        public static TMP_Text Text(Transform parent, string text, float x, float y, float w, float h,
            float size, Color color, bool bold = false)
        {
            EnsureFonts();
            var label = Rect(parent, "Text", x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = bold ? Bold : Regular;
            label.text = text ?? string.Empty;
            label.fontSize = size;
            // Bold is supplied by its own font asset, without synthetic bold styling.
            label.fontStyle = FontStyles.Normal;
            label.fontWeight = FontWeight.Regular;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = Vector4.zero;
            label.enableAutoSizing = false;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(Transform parent, string text, float x, float y, float w, float h,
            Color fill, Color ink, Action clicked, string icon = null)
        {
            var rect = Panel(parent, string.IsNullOrEmpty(text) ? "Icon button " + icon : text,
                x, y, w, h, fill, Mathf.Min(16f, Mathf.Min(w, h) * .5f));
            var graphic = rect.GetComponent<Graphic>();
            graphic.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            if (clicked != null) button.onClick.AddListener(() => clicked());
            JarVisMotion.Attach(rect);

            var sprite = string.IsNullOrEmpty(icon) ? null : Resources.Load<Sprite>("Prototype/Icons/" + icon);
            bool hasText = !string.IsNullOrEmpty(text);
            float padding = Mathf.Min(12f, w * .15f);
            float available = Mathf.Max(0f, w - padding * 2f);
            float iconSize = Mathf.Min(22f, Mathf.Min(h - 12f, available));
            iconSize = Mathf.Max(0f, iconSize);
            TMP_Text label = null;
            float labelWidth = 0f;
            float gap = sprite != null && hasText ? 8f : 0f;
            if (hasText)
            {
                label = Text(rect, text, padding, 0f, available, h, 16f, ink, true);
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                labelWidth = Mathf.Min(label.GetPreferredValues(text).x,
                    Mathf.Max(0f, available - (sprite != null ? iconSize + gap : 0f)));
            }
            float groupWidth = labelWidth + (sprite != null ? iconSize + gap : 0f);
            float start = (w - groupWidth) * .5f;
            if (sprite != null)
            {
                var image = Rect(rect, icon, start, (h - iconSize) * .5f, iconSize, iconSize)
                    .gameObject.AddComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.color = ink;
                image.raycastTarget = false;
                start += iconSize + gap;
            }
            if (label != null)
            {
                label.rectTransform.anchoredPosition = new Vector2(start, 0f);
                label.rectTransform.sizeDelta = new Vector2(labelWidth, h);
            }
            return button;
        }
    }

    // Shared convex geometry; no textures, generated materials, shadows or per-frame allocations.
    internal static class JarVisMesh
    {
        internal static void RoundedRect(VertexHelper mesh, Rect rect, float radius, Color tint)
        {
            if (rect.width <= 0f || rect.height <= 0f || tint.a <= 0f) return;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(rect.width, rect.height) * .5f);
            const int segments = 6;
            int first = mesh.currentVertCount;
            mesh.AddVert(new Vector3(rect.center.x, rect.center.y), tint, Vector2.zero);
            for (int corner = 0; corner < 4; corner++)
            {
                float cx = corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius;
                float cy = corner < 2 ? rect.yMax - radius : rect.yMin + radius;
                for (int step = 0; step <= segments; step++)
                {
                    float angle = (corner * 90f + step * 90f / segments) * Mathf.Deg2Rad;
                    mesh.AddVert(new Vector3(cx + Mathf.Cos(angle) * radius, cy + Mathf.Sin(angle) * radius),
                        tint, Vector2.zero);
                }
            }
            int count = 4 * (segments + 1);
            for (int i = 0; i < count; i++) mesh.AddTriangle(first, first + 1 + i, first + 1 + (i + 1) % count);
        }

        internal static float Unit(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
        }
    }

    [AddComponentMenu("")]
    internal sealed class JarVisFlatSurface : MaskableGraphic
    {
        internal float Radius = 20f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            JarVisMesh.RoundedRect(vh, rectTransform.rect, Radius, color);
        }
    }

    /// <summary>Original procedural robot and waveform. Feed measured audio through SetAmplitude.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
    public sealed class JarVisFace : MaskableGraphic
    {
        [Range(0f, 1f)] public float Amplitude;
        public bool Speaking;
        [Tooltip("Negative uses live motion; 0..1 selects a deterministic animation cycle.")]
        public float PreviewPhase = -1f;
        public bool DemoAmplitude = false;

        private float phase, energy;
        private bool lastReduced;
        private float lastPreview = -2f;
        private static readonly Color Head = new Color(.055f, .14f, .28f, 1f);
        private static readonly Color Faceplate = new Color(.035f, .10f, .21f, 1f);
        private static readonly Color Cyan = new Color(.23f, .87f, .96f, 1f);

        protected override void OnEnable()
        {
            base.OnEnable();
            raycastTarget = false;
            RefreshVisual();
        }

        public void SetAmplitude(float value)
        {
            Amplitude = JarVisMesh.Unit(value);
            if (!Application.isPlaying || PreviewPhase >= 0f) energy = TargetEnergy(CurrentPhase());
            SetVerticesDirty();
        }

        public void RefreshVisual()
        {
            Amplitude = JarVisMesh.Unit(Amplitude);
            if (!Application.isPlaying || PreviewPhase >= 0f) energy = TargetEnergy(CurrentPhase());
            SetVerticesDirty();
        }

        private float CurrentPhase()
        {
            return PreviewPhase >= 0f ? JarVisMesh.Unit(PreviewPhase) : phase;
        }

        private float TargetEnergy(float cycle)
        {
            if (!Speaking) return 0f;
            if (!DemoAmplitude) return JarVisMesh.Unit(Amplitude);
            float t = cycle * Mathf.PI * 2f;
            return Mathf.Clamp01(.38f + .22f * Mathf.Sin(t * 3f) + .15f * Mathf.Sin(t * 7f + .8f));
        }

        private void Update()
        {
            bool reduced = PrototypeUi.ReducedMotion;
            bool preview = PreviewPhase >= 0f;
            if (!preview && !reduced) phase = Mathf.Repeat(phase + Time.unscaledDeltaTime / 5.2f, 1f);
            float target = TargetEnergy(CurrentPhase());
            float next = preview ? target : Mathf.Lerp(energy, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 15f));
            if (!Mathf.Approximately(next, energy) || !reduced || lastReduced != reduced || lastPreview != PreviewPhase)
            {
                energy = next;
                SetVerticesDirty();
            }
            lastReduced = reduced;
            lastPreview = PreviewPhase;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect bounds = rectTransform.rect;
            float side = Mathf.Min(bounds.height * .82f, bounds.width * .36f);
            if (side <= 0f) return;
            bool reduced = PrototypeUi.ReducedMotion;
            float cycle = reduced ? 0f : CurrentPhase();
            float level = PreviewPhase >= 0f || !Application.isPlaying ? TargetEnergy(cycle) : energy;
            float t = cycle * Mathf.PI * 2f;
            var head = new Rect(bounds.xMin + side * .07f, bounds.center.y - side * .5f, side, side);
            Draw(vh, new Rect(head.center.x - side * .025f, head.yMax - side * .01f, side * .05f, side * .12f), side * .025f, Head);
            Draw(vh, new Rect(head.center.x - side * .065f, head.yMax + side * .075f, side * .13f, side * .075f), side * .038f, Cyan);
            Draw(vh, new Rect(head.xMin - side * .065f, head.center.y - side * .12f, side * 1.13f, side * .24f), side * .05f, Head);
            Draw(vh, head, side * .25f, Head);
            Draw(vh, new Rect(head.x + side * .12f, head.y + side * .20f, side * .76f, side * .55f), side * .17f, Faceplate);

            float blink = 1f;
            if (!reduced && cycle > .94f && cycle < .98f)
                blink = 1f - .9f * Mathf.Sin((cycle - .94f) / .04f * Mathf.PI);
            float eyeHeight = side * (.21f + .035f * level) * blink;
            float eyeY = head.center.y + side * .015f + (reduced ? 0f : side * .018f * level * Mathf.Sin(t * 3f));
            float eyeX = reduced ? 0f : side * .022f * level * Mathf.Sin(t * 2f);
            for (int i = 0; i < 2; i++)
            {
                float x = head.x + side * (i == 0 ? .29f : .58f) + eyeX;
                Draw(vh, new Rect(x, eyeY - eyeHeight * .5f, side * .13f, eyeHeight), side * .065f, Cyan);
            }
            Draw(vh, new Rect(head.center.x - side * .1f, head.y + side * .22f, side * .2f, side * .035f), side * .0175f, Cyan * new Color(1f, 1f, 1f, .65f));

            float left = head.xMax + side * .27f;
            float available = bounds.xMax - side * .06f - left;
            if (available <= 0f) return;
            const int bars = 11;
            float pitch = available / bars;
            float barWidth = Mathf.Min(side * .075f, pitch * .55f);
            for (int i = 0; i < bars; i++)
            {
                float envelope = .4f + .6f * Mathf.Sin((i + 1f) / (bars + 1f) * Mathf.PI);
                float wave = .45f + .55f * Mathf.Pow(Mathf.Sin(i * .82f - t * 4f), 2f);
                float height = side * (.065f + .68f * level * envelope * wave);
                Draw(vh, new Rect(left + pitch * (i + .5f) - barWidth * .5f,
                    bounds.center.y - height * .5f, barWidth, height), barWidth * .5f,
                    Cyan * new Color(1f, 1f, 1f, .55f + .45f * level));
            }
        }

        private void Draw(VertexHelper vh, Rect rect, float radius, Color tint)
        {
            JarVisMesh.RoundedRect(vh, rect, radius, tint * color);
        }
    }
}
