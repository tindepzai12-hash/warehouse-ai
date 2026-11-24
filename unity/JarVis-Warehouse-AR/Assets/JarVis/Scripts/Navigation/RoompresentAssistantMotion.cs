using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Navigation
{
    // Drawn bars use real recorder/playback levels; silence stays still.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoompresentAssistantMotion:MaskableGraphic
    {
        public float[] Levels;
        public Vector2 EyeOffset {get;private set;}
        float strength;
        protected override void Awake(){base.Awake();useLegacyMeshGeneration=false;raycastTarget=false;}
        void Update()
        {
            float peak=0,weight=0,sum=0;
            if(Levels!=null)for(int i=0;i<Levels.Length;i++){float value=Mathf.Clamp01(Levels[i]);peak=Mathf.Max(peak,value);sum+=value;weight+=value*i/Mathf.Max(1,Levels.Length-1);}
            strength=Mathf.Lerp(strength,peak,1-Mathf.Exp(-Time.unscaledDeltaTime*12));
            var target=new Vector2(sum>.01f?1+weight/sum*2:0,(weight/Mathf.Max(.01f,sum)-.5f)*3*strength);
            EyeOffset=Vector2.Lerp(EyeOffset,target,1-Mathf.Exp(-Time.unscaledDeltaTime*8));SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;int count=32;
            for(int i=0;i<count;i++)
            {
                int index=Levels==null?0:Mathf.Min(Levels.Length-1,i*Levels.Length/count);
                float level=Levels==null||Levels.Length==0?0:Mathf.Clamp01(Levels[index]);float h=3+level*(r.height-4);
                float x=r.xMin+(i+.5f)*r.width/count,w=Mathf.Min(3,r.width/count-2),cy=r.center.y;
                int start=vh.currentVertCount;var c=color;c.a*=.35f+.65f*Mathf.Max(level,strength*.3f);
                vh.AddVert(new Vector3(x-w*.5f,cy-h*.5f),c,Vector2.zero);vh.AddVert(new Vector3(x-w*.5f,cy+h*.5f),c,Vector2.zero);
                vh.AddVert(new Vector3(x+w*.5f,cy+h*.5f),c,Vector2.zero);vh.AddVert(new Vector3(x+w*.5f,cy-h*.5f),c,Vector2.zero);
                vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
