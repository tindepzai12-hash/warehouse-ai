using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 public sealed class UiGlassBackdrop:MonoBehaviour {
  static RenderTexture backdrop;static int captured=-1;
  void OnEnable(){StartCoroutine(CaptureWhenReady());}
  IEnumerator CaptureWhenReady(){yield return null;yield return null;if(captured==Time.frameCount)yield break;captured=Time.frameCount;var nav=GetComponentInParent<ImportedFigmaNavigation>();if(nav==null)yield break;
   // Sample the background beneath glass, never the control's own text or a previous page.
   var hidden=new Dictionary<Graphic,bool>();foreach(var surface in nav.GetComponentsInChildren<UniformUiSurface>(false))if(surface.Glass){var shadow=surface.GetComponent<PrototypeGlassShadow>()?.Graphic;if(shadow!=null&&!hidden.ContainsKey(shadow)){hidden.Add(shadow,shadow.enabled);shadow.enabled=false;}foreach(var graphic in surface.GetComponentsInChildren<Graphic>(false))if(!hidden.ContainsKey(graphic)){hidden.Add(graphic,graphic.enabled);graphic.enabled=false;}}
   RenderTexture fresh=null;try{Shader.SetGlobalFloat("_JarVisHasBackdrop",0);fresh=UiBackdropCapture.Capture(nav,1);}finally{foreach(var pair in hidden)if(pair.Key!=null)pair.Key.enabled=pair.Value;}
   if(fresh==null)yield break;if(backdrop!=null){backdrop.Release();Destroy(backdrop);}backdrop=fresh;Shader.SetGlobalTexture("_JarVisBackdrop",backdrop);Shader.SetGlobalVector("_JarVisBackdrop_TexelSize",new Vector4(1f/backdrop.width,1f/backdrop.height,backdrop.width,backdrop.height));Shader.SetGlobalFloat("_JarVisHasBackdrop",1);}
 }
 public static class UiBackdropCapture {
  public static RenderTexture Capture(ImportedFigmaNavigation nav,int passes) {
   var canvas=nav.GetComponentInParent<Canvas>();var camera=Camera.main;if(canvas==null||camera==null)return null;
   int h=915,w=412;
   var output=new RenderTexture(w,h,0){name="JarVis blurred UI backdrop"};var target=RenderTexture.GetTemporary(w,h,24);var scratch=RenderTexture.GetTemporary(w,h,0);var old=camera.targetTexture;var mode=canvas.renderMode;var oldCamera=canvas.worldCamera;float distance=canvas.planeDistance;
   var blur=new Material(Shader.Find("JarVis/Backdrop Blur"));
   try {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;camera.targetTexture=target;Canvas.ForceUpdateCanvases();nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();foreach(var text in nav.GetComponentsInChildren<TMPro.TMP_Text>(false)){text.UpdateMeshPadding();text.ForceMeshUpdate();}camera.Render();Graphics.Blit(target,output);
    for(int i=0;i<passes;i++){blur.SetVector("_Direction",new Vector4(1,0,0,0));Graphics.Blit(output,scratch,blur);blur.SetVector("_Direction",new Vector4(0,1,0,0));Graphics.Blit(scratch,output,blur);}return output;
   } finally {camera.targetTexture=old;canvas.renderMode=mode;canvas.worldCamera=oldCamera;canvas.planeDistance=distance;Canvas.ForceUpdateCanvases();nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();RenderTexture.ReleaseTemporary(target);RenderTexture.ReleaseTemporary(scratch);Object.Destroy(blur);}
  }
 }
}


