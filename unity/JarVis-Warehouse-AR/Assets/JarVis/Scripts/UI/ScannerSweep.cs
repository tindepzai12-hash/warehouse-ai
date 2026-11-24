using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 public sealed class ScannerSweep:MonoBehaviour {
 ScannerSweepGraphic beam;
 void Awake(){var r=JarVisUi.Rect(transform,"Red scanning sweep",0,0,0,0);r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;beam=r.gameObject.AddComponent<ScannerSweepGraphic>();beam.color=new Color(1,.3f,.38f);beam.raycastTarget=false;}
 void Update(){var scanner=GetComponentInParent<ImportedCameraScanner>();bool show=scanner!=null&&(scanner.State==ScannerState.Scanning||scanner.State==ScannerState.Decoding);beam.gameObject.SetActive(show);if(show){beam.Phase=PrototypeUi.ReducedMotion?.5f:.5f-.5f*Mathf.Cos(Time.unscaledTime*Mathf.PI/.95f);beam.SetVerticesDirty();beam.transform.SetAsLastSibling();}}
 }
 [RequireComponent(typeof(CanvasRenderer))]
 public sealed class ScannerSweepGraphic:MaskableGraphic {
 public float Phase=.5f;
 protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();RoompresentCodeFrame.DrawSweep(vh,rectTransform.rect,color,Phase,PrototypeUi.ReducedMotion);}
 }
}
