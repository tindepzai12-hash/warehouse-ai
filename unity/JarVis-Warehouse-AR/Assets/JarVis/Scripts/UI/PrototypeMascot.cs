using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace JarVis.Warehouse.UI
{
    public sealed class PrototypeMascot:MonoBehaviour,IPointerClickHandler
    {
        [SerializeField] RectTransform actor;
        [SerializeField] Image face;
        [SerializeField] Sprite openFace,expressionFace;
        [SerializeField] Graphic[] smiles,sparks;
        public float PreviewTime=-1;
        float happyUntil,tapStarted;
        public bool IsHappy { get; private set; }
        public void Configure()
        {
            var target=GetComponent<Image>();if(target==null)target=gameObject.AddComponent<Image>();target.color=Color.clear;target.raycastTarget=true;
            actor=JarVisUi.Rect(transform,"Transparent JarVis actor",0,0,84,82);
            actor.anchorMin=actor.anchorMax=actor.pivot=new Vector2(.5f,.5f);actor.anchoredPosition=Vector2.zero;
            face=actor.gameObject.AddComponent<Image>();openFace=Resources.Load<Sprite>("Prototype/JarVis");expressionFace=Resources.Load<Sprite>("Prototype/JarVisExpressionBase");face.sprite=openFace;face.preserveAspect=true;face.raycastTarget=false;
            smiles=new Graphic[2];sparks=new Graphic[2];
            for(int i=0;i<2;i++)
            {
                var smile=JarVisUi.Rect(actor,"Smile arc "+i,30.6f+i*11.1f,45.2f,8.4f,8.4f).gameObject.AddComponent<MascotSmileArc>();smile.color=PrototypeUi.Hex("00d2e9");smile.raycastTarget=false;smiles[i]=smile;
                var spark=PrototypeUi.Icon(transform,"plus",i==0?76:-3,i==0?6:24,i==0?10:7,PrototypeUi.Hex("f59a13"));sparks[i]=spark;
            }
        }
        void OnEnable(){RecoverVisualReferences();}
        void RecoverVisualReferences()
        {
            if(actor==null)actor=transform.Find("Transparent JarVis actor") as RectTransform;
            if(actor==null)return;
            if(face==null)face=actor.GetComponent<Image>();
            if(openFace==null)openFace=Resources.Load<Sprite>("Prototype/JarVis");
            if(expressionFace==null)expressionFace=Resources.Load<Sprite>("Prototype/JarVisExpressionBase");
            if(smiles==null||smiles.Length!=2)
            {
                var curves=actor.GetComponentsInChildren<MascotSmileArc>(true);smiles=new Graphic[curves.Length];
                for(int i=0;i<curves.Length;i++)smiles[i]=curves[i];
            }
            if(sparks==null||sparks.Length!=2)
            {
                var marks=new System.Collections.Generic.List<Graphic>();
                foreach(var image in GetComponentsInChildren<Image>(true))if(image.name=="plus")marks.Add(image);
                sparks=marks.ToArray();
            }
        }
        void Update()
        {
            Paint();
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if(eventData.button==PointerEventData.InputButton.Left)Celebrate();
        }
        public void Celebrate()
        {
            tapStarted=Time.unscaledTime;happyUntil=tapStarted+1.6f;Paint();
        }
        void OnDisable(){happyUntil=0;IsHappy=false;}
        void Paint()
        {
            if(actor==null||face==null||smiles==null||sparks==null)RecoverVisualReferences();
            if(actor==null||face==null)return;
            bool tapped=Time.unscaledTime<happyUntil;float t=tapped?Time.unscaledTime-tapStarted:(PreviewTime>=0?PreviewTime:Time.unscaledTime)%5;bool reduced=PrototypeUi.ReducedMotion;
            float hop=t<1&&!reduced?Mathf.Sin(t*Mathf.PI)*10:0;actor.anchoredPosition=new Vector2(0,hop);actor.localRotation=Quaternion.Euler(0,0,t<1&&!reduced?Mathf.Sin(t*Mathf.PI*2)*5:0);
            bool happy=tapped||t>.2f&&t<.9f&&!reduced,blink=!tapped&&t>3.3f&&t<3.5f&&!reduced;IsHappy=happy;
            face.sprite=(happy||blink)&&expressionFace!=null?expressionFace:openFace;
            if(smiles!=null)foreach(var smile in smiles)if(smile!=null)smile.enabled=happy;
            if(sparks!=null)foreach(var spark in sparks)if(spark!=null)spark.enabled=happy&&!reduced;
        }
    }
    // Curved cyan eyes are drawn over the clean expression sprite.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MascotSmileArc : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var bounds=rectTransform.rect;
            const int segments=12;
            for(int i=0;i<segments;i++)
            {
                float a=(float)i/segments,b=(float)(i+1)/segments;
                Vector2 p=new Vector2(Mathf.Lerp(bounds.xMin+1.2f,bounds.xMax-1.2f,a),bounds.center.y-1+Mathf.Sin(a*Mathf.PI)*2.3f);
                Vector2 q=new Vector2(Mathf.Lerp(bounds.xMin+1.2f,bounds.xMax-1.2f,b),bounds.center.y-1+Mathf.Sin(b*Mathf.PI)*2.3f);
                var normal=new Vector2(-(q-p).y,(q-p).x).normalized*.6f;
                int start=mesh.currentVertCount;
                mesh.AddVert(p-normal,color,Vector2.zero);mesh.AddVert(p+normal,color,Vector2.zero);
                mesh.AddVert(q+normal,color,Vector2.zero);mesh.AddVert(q-normal,color,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
