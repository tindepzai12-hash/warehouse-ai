using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.VisualMaps
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VisualMapFloorGraphic : MaskableGraphic
    {
        public VisualMapSession Owner;
        private float nextRefresh;
        public static Vector2 Project(Vector3 point,Vector2 minimum,Vector2 maximum,Rect rect)
        {
            Vector2 extent = maximum - minimum;
            float scale = Mathf.Min((rect.width - 24) / Mathf.Max(.1f,extent.x),(rect.height - 24) / Mathf.Max(.1f,extent.y));
            return rect.center + (new Vector2(point.x,point.z) - (minimum + maximum) * .5f) * scale;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear(); if (Owner == null) return;
            VisualMapGeometry geometry = Owner.Geometry;
            VisualMapModel map = Owner.SelectedMap;
            if (map == null) return;
            bool hasGeometry = geometry != null && geometry.cells != null;
            Vector2 minimum = hasGeometry ? new Vector2(geometry.origin[0],geometry.origin[1]) : new Vector2(-1,-1);
            Vector2 maximum = hasGeometry ? minimum + new Vector2(geometry.width,geometry.height) * geometry.resolution : new Vector2(1,1);
            if (!hasGeometry && map.points != null) foreach (VisualMapPoint point in map.points)
            {
                minimum = Vector2.Min(minimum,new Vector2(point.x - .7f,point.z - .7f));
                maximum = Vector2.Max(maximum,new Vector2(point.x + .7f,point.z + .7f));
            }
            Rect rect = rectTransform.rect;
            Vector2 Convert(Vector3 point) => Project(point,minimum,maximum,rect);
            if (hasGeometry)
            {
                int stride = Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(geometry.width,geometry.height) / 64f));
                for (int row = 0; row < geometry.height; row += stride) for (int column = 0; column < geometry.width; column += stride)
                {
                    int value = 0;
                    for (int scanRow = row; scanRow < Mathf.Min(row + stride,geometry.height); scanRow++) for (int scanColumn = column; scanColumn < Mathf.Min(column + stride,geometry.width); scanColumn++) value = Mathf.Max(value,geometry.cells[scanRow * geometry.width + scanColumn]);
                    if (value == 0) continue;
                    Vector2 first = Convert(new Vector3(minimum.x + column * geometry.resolution,0,minimum.y + row * geometry.resolution));
                    Vector2 second = Convert(new Vector3(minimum.x + Mathf.Min(column + stride,geometry.width) * geometry.resolution,0,minimum.y + Mathf.Min(row + stride,geometry.height) * geometry.resolution));
                    Quad(helper,first,new Vector2(first.x,second.y),second,new Vector2(second.x,first.y),value == 2 ? new Color(.85f,.35f,.27f) : new Color(.82f,.88f,.92f));
                }
            }
            if (map.points != null && map.points.Length > 0)
            {
                for (int index = 0; index + 1 < map.points.Length; index++) Line(helper,Convert(map.points[index].Position),Convert(map.points[index + 1].Position),3,new Color(.22f,.65f,1));
                Dot(helper,Convert(map.points[0].Position),4,Color.white);
                Dot(helper,Convert(map.points[map.points.Length - 1].Position),6,new Color(1,.55f,.08f));
            }
            if (Owner.MapPoseAvailable)
            {
                Vector2 position = Convert(Owner.MapPosition);
                if (!rect.Contains(position)) return;
                Vector3 forward = Owner.MapForward;
                Vector2 direction = new Vector2(forward.x,forward.z).normalized,side = new Vector2(-direction.y,direction.x);
                Dot(helper,position,8,Color.white); Dot(helper,position,5,new Color(.04f,.3f,.98f));
                int start = helper.currentVertCount;
                helper.AddVert(position + direction * 16,new Color(.15f,.6f,1),Vector2.zero);
                helper.AddVert(position + direction * 7 + side * 5,new Color(.15f,.6f,1),Vector2.zero);
                helper.AddVert(position + direction * 7 - side * 5,new Color(.15f,.6f,1),Vector2.zero);
                helper.AddTriangle(start,start + 1,start + 2);
            }
        }

        public static void Quad(VertexHelper helper,Vector2 first,Vector2 second,Vector2 third,Vector2 fourth,Color tint)
        {
            int start = helper.currentVertCount;
            helper.AddVert(first,tint,Vector2.zero); helper.AddVert(second,tint,Vector2.zero); helper.AddVert(third,tint,Vector2.zero); helper.AddVert(fourth,tint,Vector2.zero);
            helper.AddTriangle(start,start + 1,start + 2); helper.AddTriangle(start,start + 2,start + 3);
        }

        public static void Line(VertexHelper helper,Vector2 first,Vector2 second,float width,Color tint)
        {
            Vector2 along = (second - first).normalized,side = new Vector2(-along.y,along.x) * width * .5f;
            Quad(helper,first - side,first + side,second + side,second - side,tint);
        }

        private static void Dot(VertexHelper helper,Vector2 position,float radius,Color tint)
        {
            for (int index = 0; index < 12; index++)
            {
                float first = index * Mathf.PI / 6,second = (index + 1) * Mathf.PI / 6;
                Quad(helper,position,position + new Vector2(Mathf.Cos(first),Mathf.Sin(first)) * radius,position + new Vector2(Mathf.Cos(second),Mathf.Sin(second)) * radius,position,tint);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f; SetVerticesDirty();
        }
    }
}
