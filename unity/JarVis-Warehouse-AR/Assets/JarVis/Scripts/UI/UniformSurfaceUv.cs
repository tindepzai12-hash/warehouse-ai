using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 // Write normalized rect coordinates before Canvas batching transforms the vertices.
 [RequireComponent(typeof(Image))] public sealed class UniformSurfaceUv : BaseMeshEffect {
 public override void ModifyMesh(VertexHelper mesh){if(!IsActive())return;var rect=graphic.rectTransform.rect;var vertex=new UIVertex();for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);vertex.uv0=new Vector2((vertex.position.x-rect.xMin)/Mathf.Max(1,rect.width),(vertex.position.y-rect.yMin)/Mathf.Max(1,rect.height));mesh.SetUIVertex(vertex,i);}}
 }
}
