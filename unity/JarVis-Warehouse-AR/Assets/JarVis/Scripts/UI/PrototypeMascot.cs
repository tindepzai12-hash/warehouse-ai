using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace JarVis.Warehouse.UI
{
    public sealed class PrototypeMascot:MonoBehaviour,IPointerClickHandler
    {
        RectTransform actor;Image[] lids;Graphic[] smiles,sparks;
        public float PreviewTime=-1;
        float happyUntil,tapStarted;
        public bool IsHappy { get; private set; }
        public void Configure()
        {
            var target=GetComponent<Image>();if(target==null)target=gameObject.AddComponent<Image>();target.color=Color.clear;target.raycastTarget=true;
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
            if(actor==null)return;bool tapped=Time.unscaledTime<happyUntil;float t=tapped?Time.unscaledTime-tapStarted:(PreviewTime>=0?PreviewTime:Time.unscaledTime)%5;bool reduced=PrototypeUi.ReducedMotion;
            float hop=t<1&&!reduced?Mathf.Sin(t*Mathf.PI)*10:0;actor.anchoredPosition=new Vector2(0,hop);actor.localRotation=Quaternion.Euler(0,0,t<1&&!reduced?Mathf.Sin(t*Mathf.PI*2)*5:0);
            bool happy=tapped||t>.2f&&t<.9f&&!reduced,blink=!tapped&&t>3.3f&&t<3.5f&&!reduced;IsHappy=happy;
            for(int index=0;index<2;index++){lids[index].enabled=happy||blink;smiles[index].enabled=happy;sparks[index].enabled=happy&&!reduced;}
        }
    }
}
