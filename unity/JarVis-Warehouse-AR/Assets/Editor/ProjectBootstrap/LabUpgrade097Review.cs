using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class LabUpgrade097Review
    {
        const string Output="Logs/LabTest/Upgrade097";
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        static int checks;
        static void Check(bool pass,string message){checks++;if(!pass)throw new InvalidOperationException(message);}
        static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
        static void Prop(object o,string name,object value)=>o.GetType().GetProperty(name,Flags).SetValue(o,value);
        static object Field(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
        static void Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags).Invoke(o,args);
        static LabFloorPlan Floor()=>JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text);
        static LabFixedMap Trial()=>JsonUtility.FromJson<LabTrialPreset>(Resources.Load<TextAsset>("LabTest/LabTrialNavigation").text).map;
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            TestLuminance();TestQrBinding();TestJournal();TestLowMemory();
            foreach(var size in new[]{new Vector2Int(360,640),new Vector2Int(412,915)})Capture(size);
            // Same input/save/export flows changed in this release; retain the existing regression gate.
            LabUpgrade096Review.Run();
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" focused assertions (CPU transforms, QR identity/pose binding, bounded diagnostics/export, managed lifecycle and diagnostics UI). 096 regression also passed. Native camera/AI lifecycle, Android exit bridge and crash-free operation on Samsung NOT verified.");
            Debug.Log("JARVIS_097_PASS "+checks);
        }
        static void TestLuminance()
        {
            var source=new NativeArray<byte>(new byte[]{1,2,3,99,4,5,6,99},Allocator.Temp);
            try
            {
                var output=new byte[6];Check(LabCameraLuminance.Copy(source,3,2,4,1,Matrix4x4.identity,output,3,2),"Padded row supported");
                Check(output.SequenceEqual(new byte[]{1,2,3,4,5,6}),"Identity respects row stride");
                var m=Matrix4x4.identity;m.m00=-1;m.m02=1;
                Check(LabCameraLuminance.Copy(source,3,2,4,1,m.transpose,output,3,2)&&output.SequenceEqual(new byte[]{3,2,1,6,5,4}),"Horizontal reflection");
                m=Matrix4x4.identity;m.m11=-1;m.m12=1;
                Check(LabCameraLuminance.Copy(source,3,2,4,1,m.transpose,output,3,2)&&output.SequenceEqual(new byte[]{4,5,6,1,2,3}),"Vertical reflection");
                m=Matrix4x4.identity;m.m00=0;m.m01=1;m.m10=-1;m.m11=0;m.m12=1;
                Check(LabCameraLuminance.Copy(source,3,2,4,1,m.transpose,output,2,3)&&output.SequenceEqual(new byte[]{4,1,5,2,6,3}),"Portrait rotation keeps pixel correspondence");
                Check(!LabCameraLuminance.Copy(source,3,3,4,1,Matrix4x4.identity,output,3,2),"Reject short CPU plane");
                Check(!LabCameraLuminance.Copy(source,3,2,4,1,Matrix4x4.zero,output,3,2),"Reject missing display transform");
                m=Matrix4x4.identity;m.m00=float.NaN;Check(!LabCameraLuminance.Copy(source,3,2,4,1,m,output,3,2),"Reject nonfinite UV");
                m=Matrix4x4.identity;m.m02=2;Check(LabCameraLuminance.Copy(source,3,2,4,1,m.transpose,output,3,2)&&output.All(x=>x==255),"Outside crop is white, never invalid memory");
            }
            finally{source.Dispose();}
            var packed=new NativeArray<byte>(new byte[]{10,99,20,99,30,99,40,99},Allocator.Temp);
            try{var output=new byte[4];Check(LabCameraLuminance.Copy(packed,2,2,4,2,Matrix4x4.identity,output,2,2)&&output.SequenceEqual(new byte[]{10,20,30,40}),"Interleaved pixel stride");}
            finally{packed.Dispose();}
        }
        static void TestQrBinding()
        {
            var texture=Resources.Load<Texture2D>("LabTest/Lab-A");var color=texture.GetPixels32();var bytes=new NativeArray<byte>(color.Length,Allocator.Temp);
            try
            {
                for(int i=0;i<color.Length;i++)bytes[i]=(byte)((color[i].r*77+color[i].g*150+color[i].b*29)>>8);
                int width=640,height=768;var pixels=new byte[width*height];
                Check(LabCameraLuminance.Copy(bytes,texture.width,texture.height,texture.width,1,Matrix4x4.identity,pixels,width,height),"Card CPU sampling");
                var session=typeof(LabTestSession);var jobType=session.GetNestedType("MarkerPoseJob",BindingFlags.NonPublic);
                var obsType=session.GetNestedType("Observation",BindingFlags.NonPublic);var obs=Activator.CreateInstance(obsType);
                Set(obs,"pose",new Pose(new Vector3(1,2,3),Quaternion.identity));Set(obs,"tracking",true);
                var job=Activator.CreateInstance(jobType);Set(job,"reference","A");Set(job,"sample",obs);
                Set(job,"quad",new float[]{0,height,width,height,width,0,0,0});
                var jobs=Array.CreateInstance(jobType,1);jobs.SetValue(job,0);
                var decode=session.GetMethod("DecodeBoundMarkerFrame",BindingFlags.NonPublic|BindingFlags.Static);
                var bound=decode.Invoke(null,new object[]{pixels,width,height,jobs});
                Check((string)Field(bound,"id")=="A"&&(int)Field(bound,"count")==1&&ReferenceEquals(Field(bound,"observation"),obs),"QR A binds to the same card pose after CPU read");
                Set(job,"reference","B");
                var wrong=decode.Invoke(null,new object[]{pixels,width,height,jobs});
                Check(Field(wrong,"observation")==null,"QR A cannot acquire the similar B pose");
                Set(job,"reference","A");var rotated=new byte[height*width];
                var matrix=Matrix4x4.identity;matrix.m00=0;matrix.m01=1;matrix.m10=-1;matrix.m11=0;matrix.m12=1;
                Check(LabCameraLuminance.Copy(bytes,texture.width,texture.height,texture.width,1,matrix.transpose,rotated,height,width),"Rotated card CPU sampling");
                // Map the four original card corners through the inverse UV rotation.
                Set(job,"quad",new float[]{0,0,0,width,height,width,height,0});
                var result=decode.Invoke(null,new object[]{rotated,height,width,jobs});
                Check((string)Field(result,"id")=="A"&&ReferenceEquals(Field(result,"observation"),obs),"Rotated QR keeps pose binding");
            }
            finally{bytes.Dispose();}
        }
        static void TestJournal()
        {
            string folder=Path.GetFullPath(Output+"/journal-"+Guid.NewGuid().ToString("N"));var log=new LabStabilityJournal(folder);
            for(int i=0;i<600;i++)log.Write("ROTATION",new string('a',1800)+"\nnot-a-new-row");
            Check(log.Error==null,"Journal writes");
            Check(Directory.GetFiles(folder).All(f=>new FileInfo(f).Length<=LabStabilityJournal.LimitBytes),"Diagnostic retention bounded");
            var snapshot=log.Snapshot();Check(snapshot.Count==2,"One previous part retained");
            int before=snapshot.Values.Sum(t=>t.Length);log.Write("PAUSE","1");Check(snapshot.Values.Sum(t=>t.Length)==before,"Frozen export is immutable");
            var export=new LabNavigationJournal.Snapshot{id="stability-test"};foreach(var item in snapshot)export.text[item.Key]=item.Value;
            string zip=LabNavigationJournal.WriteZip(export,Path.GetFullPath(Output+"/exports"));
            using(var archive=ZipFile.OpenRead(zip))foreach(var entry in archive.Entries)
            using(var reader=new StreamReader(entry.Open()))Check(reader.ReadToEnd()==snapshot[entry.Name],"ZIP preserves exact captured log");
        }
        static void TestLowMemory()
        {
            var go=new GameObject("097 lifecycle fixture");
            try
            {
                var owner=go.AddComponent<LabTestSession>();Prop(owner,"Floor",Floor());var trial=Trial();Prop(owner,"Map",trial.survey);Set(owner,"fixedActive",trial);Set(owner,"fixedDraft",trial);
                Prop(owner,"Exercise",new LabExercise("C"));string map=JsonUtility.ToJson(owner.Map),active=JsonUtility.ToJson(trial);
                var ai=go.AddComponent<LabCameraAi>();Set(ai,"owner",owner);Set(owner,"cameraAi",ai);Set(ai,"rgbaBuffer",new byte[16384]);Set(owner,"markerLuminance",new byte[8192]);
                Call(owner,"LowMemory");
                Check(Field(ai,"rgbaBuffer")==null&&Field(owner,"markerLuminance")==null,"Memory fallback releases owned managed frames");
                Check(owner.Exercise.Target=="C"&&JsonUtility.ToJson(owner.Map)==map&&JsonUtility.ToJson(trial)==active,"Low-memory fallback retains target, raw observations and active map");
                Check((double)Field(ai,"cooldownUntil")>Time.realtimeSinceStartupAsDouble,"AI retry is delayed after memory warning");
                Call(ai,"OnApplicationPause",true);Call(ai,"OnApplicationPause",false);Call(ai,"OnDisable");Call(ai,"OnDisable");
                Check(owner.Exercise.Target=="C"&&JsonUtility.ToJson(owner.Map)==map,"Managed pause/repeated cleanup preserves data");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Capture(Vector2Int size)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);var rt=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("097 diagnostics UI");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();Prop(owner,"Floor",Floor());Prop(owner,"Map",Trial().survey);
                owner.CameraOwner=root.AddComponent<VisualMapSession>();var camera=new GameObject("UI camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.23f,.27f);owner.CameraOwner.arCamera=camera;
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Set(owner,"ui",ui);Call(ui,"Start");Set(ui,"showingNavigationLog",true);
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.3f;
                for(int i=0;i<4;i++){Call(ui,"LateUpdate");Canvas.ForceUpdateCanvases();}
                foreach(var text in root.GetComponentsInChildren<Text>().Where(t=>t.gameObject.activeInHierarchy&&t.text.Length>0))
                {text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);float h=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;Check(h<=text.rectTransform.rect.height+1,"Diagnostics text clips: "+text.text);}
                Check(root.GetComponentsInChildren<Button>().Count(b=>b.name=="Xuất nhật ký lỗi / văng app"&&b.interactable)==1,"Crash export usable before navigation");
                foreach(var b in root.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy))
                {var corners=new Vector3[4];((RectTransform)b.transform).GetWorldCorners(corners);foreach(var c in corners){var p=camera.WorldToViewportPoint(c);Check(p.x>=-.001&&p.x<=1.001&&p.y>=-.001&&p.y<=1.001,"Diagnostics button clipped");}}
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Output+"/diagnostics-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{foreach(var root in scene.GetRootGameObjects())foreach(var c in root.GetComponentsInChildren<Camera>())c.targetTexture=null;RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);EditorSceneManager.CloseScene(scene,true);}
        }
    }
}
