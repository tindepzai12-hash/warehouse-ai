using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.VisualMaps
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VisualMapObjectGraphic : MaskableGraphic
    {
        public VisualMapSession Owner;
        private VisualObjectDetection[] previous;
        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (Owner == null || Owner.ObjectImage == null) return;
            Rect rect = rectTransform.rect;
            int count = 0;
            foreach (VisualObjectDetection detection in Owner.ObjectDetections)
            {
                if (detection?.box == null || detection.box.Length != 4 || ++count > 30) continue;
                bool valid = true;
                foreach (float value in detection.box) valid &= VisualMapModel.Finite(value) && value >= 0 && value <= 1;
                if (!valid || detection.box[2] <= detection.box[0] || detection.box[3] <= detection.box[1]) continue;
                Vector2 first = new Vector2(rect.xMin + detection.box[0] * rect.width,rect.yMax - detection.box[1] * rect.height);
                Vector2 second = new Vector2(rect.xMin + detection.box[2] * rect.width,rect.yMax - detection.box[3] * rect.height);
                Color tint = new Color(.12f,.85f,1);
                VisualMapFloorGraphic.Line(helper,first,new Vector2(second.x,first.y),2,tint);
                VisualMapFloorGraphic.Line(helper,new Vector2(second.x,first.y),second,2,tint);
                VisualMapFloorGraphic.Line(helper,second,new Vector2(first.x,second.y),2,tint);
                VisualMapFloorGraphic.Line(helper,new Vector2(first.x,second.y),first,2,tint);
            }
        }
        private void Update()
        {
            if (Owner != null && previous != Owner.ObjectDetections) { previous = Owner.ObjectDetections; SetVerticesDirty(); }
        }
    }
}
