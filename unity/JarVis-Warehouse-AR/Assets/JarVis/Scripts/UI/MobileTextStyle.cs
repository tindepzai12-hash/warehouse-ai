using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    // The warehouse Canvas is authored at 1080x2400. Small legacy labels otherwise
    // render at only a few density-independent pixels on a high-density phone.
    internal static class MobileTextStyle
    {
        private const int MinimumFontSize = 34;
        private const int MinimumBestFitSize = 28;

        public static void Apply(Transform root)
        {
            if (root == null) return;
            Font vietnameseFont = Resources.Load<Font>("BeVietnamPro-Regular");
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (label == null || label.fontSize <= 1 || label.color.a < 0.01f)
                    continue;
                if (vietnameseFont != null) label.font = vietnameseFont;
                if (label.fontSize < MinimumFontSize)
                    label.fontSize = MinimumFontSize;
                if (label.resizeTextForBestFit)
                {
                    label.resizeTextMinSize = Mathf.Max(label.resizeTextMinSize,
                        MinimumBestFitSize);
                    label.resizeTextMaxSize = Mathf.Max(label.resizeTextMaxSize,
                        label.fontSize);
                }
            }
        }
    }
}
