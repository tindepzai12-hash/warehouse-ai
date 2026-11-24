using System.Collections.Generic;
using Immersal.Samples.Navigation;
using UnityEngine;

namespace AtlasAR
{
    public sealed class AtlasImmersalBridge:MonoBehaviour
    {
        public AtlasSession session;
        public GameObject navigationUiPrefab,targetPrefab;
        NavigationManager manager;
        Canvas[] sampleCanvases;
        public GameObject SampleUi=>manager!=null?manager.gameObject:null;
        readonly Dictionary<string,GameObject> targets=new Dictionary<string,GameObject>();
        public void Initialize()
        {
            session.Navigation.ExternalVisualization=true;
            if(navigationUiPrefab==null||targetPrefab==null)throw new System.InvalidOperationException("Immersal sample prefabs not assigned");
            var ui=Instantiate(navigationUiPrefab);ui.name="Navigation UI — Immersal official sample (QR A adapter)";
            manager=ui.GetComponent<NavigationManager>();
            manager.ConfigureAtlas(target=>{var saved=target.GetComponent<AtlasNavigationTarget>();if(saved!=null){session.SelectTarget(saved.markerId);session.StartNavigation();}},()=>session.StopNavigation());
            // Atlas owns the tools and minimap; the official sample owns its target list and path.
            sampleCanvases=ui.GetComponentsInChildren<Canvas>(true);foreach(var canvas in sampleCanvases){canvas.sortingOrder=12;canvas.enabled=false;}
        }
        public void Bind(Transform mapRoot)
        {
            targets.Clear();if(mapRoot==null){manager?.ShowAtlasRoute(null,null,false);return;}
            foreach(var marker in session.SavedMap.markers)
            {
                var target=Instantiate(targetPrefab,mapRoot,false);target.name="Navigation Target "+marker.id;
                // The Navigation Target is the saved marker XYZ; routing has a separate floor approach.
                target.transform.localPosition=marker.position;
                target.transform.localScale=Vector3.one;
                AtlasTargetAppearance.PlaceOnMarker(target.transform,marker.id);
                var nav=target.GetComponent<IsNavigationTarget>();nav.targetName=marker.id+" · "+marker.label;
                var saved=target.AddComponent<AtlasNavigationTarget>();saved.markerId=marker.id;saved.savedMapPosition=marker.position;saved.arrivalMapPosition=session.Navigation.Arrival(marker.id);
                var pose=new GameObject("MarkerPose "+marker.id+" — exact saved XYZ").transform;pose.SetParent(target.transform,false);
                var approach=new GameObject("Arrival floor "+marker.id+" — routing only").transform;approach.SetParent(target.transform,false);approach.localPosition=saved.arrivalMapPosition-marker.position;saved.arrivalPose=approach;
                targets[marker.id]=target;
            }
        }
        public void Tick(Transform mapRoot,bool valid,bool cameraMode)
        {
            if(sampleCanvases!=null)foreach(var canvas in sampleCanvases)if(canvas!=null)canvas.enabled=valid&&cameraMode;
            if(manager!=null)manager.ShowAtlasRoute(mapRoot,session.Navigation.Points,valid&&cameraMode&&session.Navigation.Active&&session.Navigation.Remaining>.65f,session.Navigation.Progress);
        }
        void OnDestroy(){if(manager!=null)Destroy(manager.gameObject);}
    }
}
