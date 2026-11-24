#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class PassiveGuidanceReview
    {
        const string Logs="Logs/RoompresentVPS/Passive039Final/";
        static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException(msg);}
        public static void Run(SmallRoomAR guide,RoompresentVpsLocalization vps)
        {
            DeliveryWorkflowReview.Run(guide,vps);
        }
        static TMP_Text Text(SmallRoomAR g,string name)=>(TMP_Text)typeof(SmallRoomAR).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);
        static void Render(SmallRoomAR guide,Canvas canvas,TMP_FontAsset font,Vector2Int size,bool english)
        {
            Text(guide,"heading").text=english?"Go to B · A1":"Tới B · A1";
            Text(guide,"status").text=english?"Follow the arrows · 5.4 m":"Đi theo mũi tên · 5.4 m";
            Text(guide,"detail").text=english?"Next: C · A2":"Tiếp theo: C · A2";
            var cameraObject=new GameObject("Passive UI review camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
            var rt=new RenderTexture(size.x,size.y,24);rt.Create();
            var previous=RenderTexture.active;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
            try
            {
                camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.17f,.21f);camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();foreach(var t in canvas.GetComponentsInChildren<TMP_Text>())t.ForceMeshUpdate();
                Check(!Text(guide,"heading").gameObject.activeInHierarchy&&!Text(guide,"status").gameObject.activeInHierarchy,"Overlay must remain hidden in both languages");
                camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();
                File.WriteAllBytes(Logs+(english?"EN-":"VI-")+size.x+"x"+size.y+".png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(image);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
    }
}
#endif
