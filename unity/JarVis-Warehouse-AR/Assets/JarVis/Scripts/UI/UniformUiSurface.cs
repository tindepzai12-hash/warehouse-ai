using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
[RequireComponent(typeof(Image))] public sealed class UniformUiSurface:MonoBehaviour {
 public bool Glass=>requestedGlass&&!PrototypeUi.ReducedGlass;
 public static void Gradient(Image image,Color end){var surface=image.GetComponent<UniformUiSurface>();if(surface?.material==null)return;surface.material.SetColor("_FillEnd",end);surface.material.SetFloat("_Gradient",1);image.SetMaterialDirty();}
 Image image; Material material; Color fill,stroke,lastFill,lastStroke; Vector4 lastSize,lastCenter; float radius,width; ImportedSettings settings; bool adaptTheme=true,initialized,requestedGlass,lastGlass;
 public static void Use(Image target,float r,Color fill,Color stroke,float width=1,bool glass=false,bool adaptTheme=true){var c=target.GetComponent<UniformUiSurface>();if(c==null)c=target.gameObject.AddComponent<UniformUiSurface>();if(target.GetComponent<UniformSurfaceUv>()==null)target.gameObject.AddComponent<UniformSurfaceUv>();c.image=target;c.fill=fill;c.stroke=stroke;c.radius=r;c.width=width;c.adaptTheme=adaptTheme;c.settings=target.GetComponentInParent<ImportedFigmaNavigation>()?.Settings;if(c.material==null)c.material=new Material(Shader.Find("JarVis/Uniform UI Surface"));c.material.SetFloat("_Gradient",0);c.requestedGlass=glass;c.material.SetFloat("_Glass",glass&&!PrototypeUi.ReducedGlass?1:0);target.material=c.material;target.sprite=null;target.type=Image.Type.Simple;c.initialized=false;c.Refresh();}
 void LateUpdate(){Refresh();}
 void Refresh(){if(image==null||material==null)return;bool glass=requestedGlass&&!PrototypeUi.ReducedGlass;if(!initialized||glass!=lastGlass){material.SetFloat("_Glass",glass?1:0);lastGlass=glass;image.SetMaterialDirty();}if(settings==null)settings=GetComponentInParent<ImportedFigmaNavigation>()?.Settings;var rect=image.rectTransform.rect;var f=fill;var s=stroke;if(adaptTheme&&settings!=null&&settings.Dark&&f.r>.75f&&f.g>.75f&&f.b>.75f){f=new Color(.1f,.15f,.23f,f.a);s=new Color(.28f,.37f,.5f,s.a);}var size=new Vector4(rect.width,rect.height,radius,width);var center=new Vector4(rect.center.x,rect.center.y,0,0);if(!initialized||size!=lastSize||center!=lastCenter||f!=lastFill||s!=lastStroke){material.SetVector("_Size",size);material.SetVector("_RectCenter",center);material.SetColor("_Fill",f);material.SetColor("_Stroke",s);lastSize=size;lastCenter=center;lastFill=f;lastStroke=s;initialized=true;image.SetMaterialDirty();}image.type=Image.Type.Simple;if(image.sprite!=null)image.sprite=null;}
 void OnDestroy(){if(material!=null)Destroy(material);}
} }


