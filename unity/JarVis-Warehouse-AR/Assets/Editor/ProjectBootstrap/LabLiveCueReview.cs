using System;
using System.IO;
using JarVis.Warehouse.Lab;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    public static class LabLiveCueReview
    {
        public static void Preview()
        {
            if(Application.isBatchMode)EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            Directory.CreateDirectory("Logs/LabTest/Live070/G1");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var previous=RenderTexture.active;
            try
            {
                var camera=new GameObject("Live cue preview camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);
                camera.transform.position=new Vector3(0,1.35f,0);camera.transform.rotation=Quaternion.Euler(18,0,0);
                camera.fieldOfView=60;camera.aspect=412f/915;camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.05f;
                var target=new RenderTexture(412,915,24);camera.targetTexture=target;
                var frame=new GameObject("Fixed metric frame").transform;SceneManager.MoveGameObjectToScene(frame.gameObject,scene);
                var rig=new GameObject("Three pooled chevrons").AddComponent<LabRouteCueRig>();SceneManager.MoveGameObjectToScene(rig.gameObject,scene);
                rig.Initialize(Resources.Load<Material>("Routes/LiveViewUnlit"));
                var tokens=new[]{new LabCueToken(3,1.65f,0,1.65f,0,1),new LabCueToken(4,2.2f,0,2.2f,0,1),new LabCueToken(5,2.75f,0,2.75f,0,1)};
                var motion=new LabCueChoreography();motion.Context(1,1,null,LabCueMode.Path,0);motion.Sync(tokens,3,0);
                foreach(var bg in new[]{"light","dark"})
                {
                    camera.backgroundColor=bg=="light"?new Color(.88f,.91f,.94f):new Color(.055f,.075f,.10f);
                    foreach(double time in new[]{0d,.16,.32,.54,1.4})
                    {
                        rig.PlacePath(frame,tokens,3,.08f);
                        for(int i=0;i<3;i++)rig.Paint(i,motion.Alpha(tokens[i].cell,time,false),LabCueChoreography.Light(time,i,false));
                        Save(camera,target,"Logs/LabTest/Live070/G1/path-"+bg+"-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png");
                    }
                    foreach(float bearing in new[]{-90f,0f,90f,180f})
                    {
                        rig.PlaceBoard(camera,new Vector3(0,.8f,2.3f),bearing);
                        for(int i=0;i<3;i++)rig.Paint(i,1,1);
                        Save(camera,target,"Logs/LabTest/Live070/G1/board-"+bg+"-"+bearing+".png");
                    }
                }
                camera.backgroundColor=new Color(.10f,.14f,.20f);
                for(int k=0;k<45;k++)
                {
                    double time=k/15d;rig.PlacePath(frame,tokens,3,.08f);
                    for(int i=0;i<3;i++)rig.Paint(i,motion.Alpha(tokens[i].cell,time,false),LabCueChoreography.Light(time,i,false));
                    Save(camera,target,"Logs/LabTest/Live070/G1/motion-"+k.ToString("D3")+".png");
                }
                rig.Hide();
                if(rig.VisibleCount!=0)throw new InvalidOperationException("Hiding the rig must remove all world markers.");
                camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);
                File.WriteAllText("Logs/LabTest/Live070/G1/result.txt","RENDERED 18 static frames + 45 animation frames through LabRouteCueRig. Visual review remains required. No physical AR acceptance.\n");
            }
            finally{RenderTexture.active=previous;EditorSceneManager.CloseScene(scene,true);}
        }
        static void Save(Camera camera,RenderTexture target,string file)
        {
            camera.Render();RenderTexture.active=target;
            var texture=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,target.width,target.height),0,0);texture.Apply();
            File.WriteAllBytes(file,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}

