using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 [RequireComponent(typeof(CanvasRenderer))] public sealed class UserPortraitGraphic:MaskableGraphic {
 Texture2D photo;public override Texture mainTexture=>photo!=null?photo:Texture2D.whiteTexture;
 protected override void Awake(){base.Awake();photo=Resources.Load<Texture2D>("JarVisUserJustin");raycastTarget=false;SetAllDirty();}
 protected override void OnPopulateMesh(VertexHelper mesh){mesh.Clear();var r=rectTransform.rect;float radius=Mathf.Min(r.width,r.height)*.5f;float vertical=photo!=null?(float)photo.width/photo.height:1;var center=new Vector2(.51f,.58f);mesh.AddVert(r.center,Color.white,center);for(int i=0;i<=64;i++){float a=i*Mathf.PI*2/64;var direction=new Vector2(Mathf.Cos(a),Mathf.Sin(a));mesh.AddVert(r.center+direction*radius,Color.white,center+new Vector2(direction.x*.5f,direction.y*vertical*.5f));if(i>0)mesh.AddTriangle(0,i,i+1);}}
 }
}

