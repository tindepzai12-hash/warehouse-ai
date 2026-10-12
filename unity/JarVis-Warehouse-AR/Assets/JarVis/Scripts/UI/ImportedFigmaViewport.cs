using UnityEngine;

namespace JarVis.Warehouse.UI
{
    [ExecuteAlways]
    public sealed class ImportedFigmaViewport : MonoBehaviour
    {
        public Vector2 referenceSize = new Vector2(412,915);
        void OnEnable() { Fit(); }
        void LateUpdate() { Fit(); }
        public void Fit()
        {
            var rect = transform as RectTransform;
            var parent = transform.parent as RectTransform;
            if (rect == null || parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = referenceSize;
            Vector2 available=parent.rect.size;
            if(Application.isPlaying&&!Application.isEditor&&Screen.width>0&&Screen.height>0){var safe=Screen.safeArea;available=Vector2.Scale(parent.rect.size,new Vector2(safe.width/Screen.width,safe.height/Screen.height));rect.anchoredPosition=Vector2.Scale(parent.rect.size,new Vector2(safe.center.x/Screen.width-.5f,safe.center.y/Screen.height-.5f));}
            float scale = Mathf.Min(available.x/referenceSize.x,available.y/referenceSize.y);
            rect.localScale = new Vector3(scale,scale,1);
        }
    }
}
