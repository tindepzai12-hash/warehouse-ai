using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoiceWaveform : MaskableGraphic
    {
        public float Progress { get; private set; }
        float[] levels;
        protected override void Awake() { base.Awake(); useLegacyMeshGeneration = false; raycastTarget = false; }
        public void Show(float[] values, float progress = 0)
        {
            levels = values;
            Progress = Mathf.Clamp01(progress);
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (levels == null || levels.Length == 0) return;
            var bounds = rectTransform.rect;
            float slot = bounds.width / levels.Length;
            for (int index = 0; index < levels.Length; index++)
            {
                float radius=Mathf.Min(1.4f,slot*.28f);
                float halfHeight=Mathf.Max(radius,bounds.height*.5f*levels[index]);
                float center=bounds.xMin+slot*(index+.5f);
                var tint = color;
                if ((index + .5f) / levels.Length > Progress) tint.a *= .5f;
                int vertex = helper.currentVertCount;
                helper.AddVert(new Vector3(center,bounds.center.y),tint,Vector2.zero);
                for(int point=0;point<12;point++)
                {
                    float angle=(point<6?point*36:180+(point-6)*36)*Mathf.Deg2Rad;
                    float capCenter=bounds.center.y+(point<6?halfHeight-radius:-halfHeight+radius);
                    helper.AddVert(new Vector3(center+Mathf.Cos(angle)*radius,capCenter+Mathf.Sin(angle)*radius),tint,Vector2.zero);
                }
                for(int point=0;point<12;point++)helper.AddTriangle(vertex,vertex+1+point,vertex+1+(point+1)%12);
            }
        }
    }
}
