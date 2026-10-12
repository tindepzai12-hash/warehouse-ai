using System;
using System.IO;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ZXing;

namespace ProjectBootstrap
{
    public static class ScannerUiVerification
    {
        [MenuItem("Tools/JarVis/Verify Scanner and Settings")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before running scene verification.");
            if(!Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save the current scene before running verification.");
            foreach(var format in new[]{BarcodeFormat.QR_CODE,BarcodeFormat.CODE_128,BarcodeFormat.EAN_13})
            {
                string value=format==BarcodeFormat.EAN_13?"5901234123457":"JARVIS-REAL-SCAN-001";
                var matrix=new MultiFormatWriter().encode(value,format,640,360);
                var colors=new Color32[640*360];
                for(int y=0;y<360;y++)for(int x=0;x<640;x++)colors[y*640+x]=matrix[x,y]?new Color32(0,0,0,255):new Color32(255,255,255,255);
                var result=ImportedCameraScanner.Decode(colors,640,360);
                if(result==null || result.Text!=value)throw new InvalidOperationException("Decoder failed: "+format);
            }
            // Fast verification uses the saved scene; never regenerate 22 screens for a decoder change.
            if(EditorSceneManager.GetActiveScene().path!="Assets/Scenes/JarVisFigmaApp.unity")
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity",OpenSceneMode.Single);
            var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>();
            nav.Initialize();
            var canvas=nav.GetComponent<Canvas>();var camera=UnityEngine.Object.FindFirstObjectByType<Camera>();
            var originalMode=canvas.renderMode;var originalCamera=canvas.worldCamera;float originalDistance=canvas.planeDistance;
            var originalTarget=camera.targetTexture;var originalActive=RenderTexture.active;string originalScreen=nav.CurrentScreen;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var target=new RenderTexture(412,915,24);var texture=new Texture2D(412,915,TextureFormat.RGBA32,false);
            try
            {
                camera.targetTexture=target;nav.Show("5:1308",false);
                var root=nav.screens.First(s=>s.id=="5:1308").root;
                var nodes=root.GetComponentsInChildren<FigmaNode>(true);
                var navigation=nav.BottomNavigation;
                var scan=navigation.GetComponentsInChildren<FigmaNode>(true).First(n=>n.nodeName=="center-scan-btn");
                var scroll=root.GetComponentInChildren<ScrollRect>(true);
                if(navigation.transform.GetSiblingIndex()!=navigation.transform.parent.childCount-1)throw new InvalidOperationException("Navigation isn't foreground.");
                foreach(bool bottom in new[]{false,true})
                {
                    scroll.verticalNormalizedPosition=bottom?0:1;Canvas.ForceUpdateCanvases();
                    nav.GetComponentInChildren<ImportedFigmaViewport>(true).Fit();Canvas.ForceUpdateCanvases();camera.Render();
                    var bounds=OwnBounds(root.transform,scan.rectTransform);
                    var viewport=OwnBounds(root.transform,scroll.viewport);
                    RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,412,915),0,0);texture.Apply();
                    File.WriteAllBytes("Logs/Settings-"+(bottom?"Bottom":"Top")+".png",texture.EncodeToPNG());
                    if(viewport.min.y<bounds.max.y+7)throw new InvalidOperationException("Settings viewport overlaps scan action. viewport="+viewport+" scan="+bounds+" root="+root.GetComponent<RectTransform>().rect);
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,scan.rectTransform.TransformPoint(scan.rectTransform.rect.center))};
                    var hits=new System.Collections.Generic.List<RaycastResult>();
                    canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
                    if(hits.Count==0 || hits[0].gameObject!=scan.gameObject)throw new InvalidOperationException("Scan button is blocked in Settings.");
                    ExecuteEvents.Execute(hits[0].gameObject,pointer,ExecuteEvents.pointerClickHandler);
                    if(nav.CurrentScreen!="5:331")throw new InvalidOperationException("Scan click did not open scanner.");
                    nav.Show("5:1308",false);
                }
                File.WriteAllText("Logs/ScannerUiVerification.txt","PASS: QR_CODE, CODE_128, EAN_13 decode; Settings top/bottom QR clearance, foreground navigation and raycast click to scanner. Physical Android camera and authenticated backend lookup not tested.\n");
            }
            finally
            {
                camera.targetTexture=originalTarget;RenderTexture.active=originalActive;
                canvas.renderMode=originalMode;canvas.worldCamera=originalCamera;canvas.planeDistance=originalDistance;
                nav.Show(originalScreen,false);
                UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(target);
            }
        }
        public static void RebuildAndRun(){CdmFigmaAppBuilder.Build();Run();}
        static Bounds OwnBounds(Transform root,RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            var result=new Bounds(root.InverseTransformPoint(corners[0]),Vector3.zero);
            for(int i=1;i<4;i++)result.Encapsulate(root.InverseTransformPoint(corners[i]));
            return result;
        }
    }
}
