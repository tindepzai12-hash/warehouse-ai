using UnityEngine;
using UnityEngine.EventSystems;

namespace JarVis.Warehouse.UI
{
    // Press feedback changes opacity only, keeping imported geometry stable.
    public sealed class ImportedPressFeedback:MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        CanvasGroup group;float resting=1,target=1;
        void Awake(){group=GetComponent<CanvasGroup>();if(group==null)group=gameObject.AddComponent<CanvasGroup>();resting=group.alpha;target=resting;}
        void Update(){if(group!=null&&!Mathf.Approximately(group.alpha,target))group.alpha=Mathf.MoveTowards(group.alpha,target,Time.unscaledDeltaTime*4);}
        public void OnPointerDown(PointerEventData data){target=resting*.78f;}
        public void OnPointerUp(PointerEventData data){target=resting;}
        public void OnPointerExit(PointerEventData data){target=resting;}
        void OnDisable(){target=resting;if(group!=null)group.alpha=resting;}
    }
}
