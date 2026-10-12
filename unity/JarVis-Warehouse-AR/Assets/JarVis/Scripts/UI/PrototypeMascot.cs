using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI
{
    public sealed class PrototypeMascot:MonoBehaviour
    {
        RectTransform actor;Image[] lids;Graphic[] smiles,sparks;
        public float PreviewTime=-1;
        public void Configure()
        {
            actor=JarVisUi.Rect(transform,"Transparent JarVis actor",0,0,84,82);
            var image=actor.gameObject.AddComponent<Image>();image.sprite=Resources.Load<Sprite>("Prototype/JarVis");image.preserveAspect=true;image.raycastTarget=false;
            lids=new Image[2];smiles=new Graphic[2];sparks=new Graphic[2];
            for(int i=0;i<2;i++)
            {
                var lid=JarVisUi.Rect(actor,"Eyelid "+i,31.5f+i*11.6f,47,8,8).gameObject.AddComponent<Image>();UniformUiSurface.Use(lid,4,PrototypeUi.Hex("092758"),Color.clear,0,false,false);lid.raycastTarget=false;lids[i]=lid;
                var smile=PrototypeUi.Icon(actor,"back",32+i*11.6f,49,6,PrototypeUi.Hex("00d2e9"));smile.rectTransform.localRotation=Quaternion.Euler(0,0,-90);smiles[i]=smile;
                var spark=PrototypeUi.Icon(transform,"plus",i==0?76:-3,i==0?6:24,i==0?10:7,PrototypeUi.Hex("f59a13"));sparks[i]=spark;
            }
        }
        void Update()
        {
            if(actor==null)return;float t=(PreviewTime>=0?PreviewTime:Time.unscaledTime)%5;bool reduced=PrototypeUi.ReducedMotion;
            float hop=t<1&&!reduced?Mathf.Sin(t*Mathf.PI)*10:0;actor.anchoredPosition=new Vector2(0,hop);actor.localRotation=Quaternion.Euler(0,0,t<1&&!reduced?Mathf.Sin(t*Mathf.PI*2)*5:0);
            bool happy=t>.2f&&t<.9f&&!reduced,blink=t>3.3f&&t<3.5f&&!reduced;
            for(int i=0;i<2;i++){lids[i].enabled=happy||blink;smiles[i].enabled=happy;sparks[i].enabled=happy;}
        }
    }
}
