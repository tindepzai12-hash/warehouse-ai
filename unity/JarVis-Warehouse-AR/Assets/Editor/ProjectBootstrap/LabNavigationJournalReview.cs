using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Lab;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectBootstrap
{
    public static class LabNavigationJournalReview
    {
        const string Output="Logs/LabTest/Journal091";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static int checks;
        static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
        static void Field(object target,string name,object value)=>target.GetType().GetField(name,Flags).SetValue(target,value);
        static void Prop(object target,string name,object value)=>target.GetType().GetProperty(name).SetValue(target,value);
        static void Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
        static string Read(ZipArchive archive,string name){using(var reader=new StreamReader(archive.GetEntry(name).Open()))return reader.ReadToEnd();}
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            EditorSceneManager.OpenScene(LabTestBuild.ScenePath,OpenSceneMode.Single);
            Directory.CreateDirectory(Output);checks=0;File.WriteAllText(Output+"/result.txt","RUNNING");
            string root=Path.GetFullPath(Path.Combine(Output,"runs-"+Guid.NewGuid().ToString("N")));
            var context=new Dictionary<string,string>{{"map.json","{\"map\":\"fixture\"}"}};
            var journal=new LabNavigationJournal(root);journal.Begin("0.9.1",0,context);
            for(int i=0;i<20;i++)journal.Append("{\"target\":\"B\",\"tracking\":"+(i<10?"true":"false")+"}",i+",row\n",i*.2,i<5,i<10);
            journal.Flush();string first=journal.Folder;
            Check(journal.Current.samples==20&&journal.Current.invalidPoseSamples==15&&journal.Current.trackingUnavailableSamples==10,"Counts retain tracking failures");
            var snapshot=journal.Capture(context);
            journal.Append("{\"target\":\"C\"}","20,row\n",4,true,true);journal.Flush();
            string zip=System.Threading.Tasks.Task.Run(()=>LabNavigationJournal.WriteZip(snapshot,Output)).GetAwaiter().GetResult();
            using(var archive=ZipFile.OpenRead(zip))
            {
                string log=Read(archive,"trace-0001.jsonl");
                Check(log.Split('\n').Count(x=>x.Length>0)==20,"Export snapshot excludes subsequent appends");
                Check(!log.Contains("\"target\":\"C\""),"Later destination cannot contaminate export");
                Check(Read(archive,"positions-0001.csv").Split('\n').Count(x=>x.Length>0)==21,"CSV header and all rows present");
                Check(archive.GetEntry("map.json")!=null&&archive.GetEntry("README.txt")!=null,"Self-contained map and instructions");
                Check(JsonUtility.FromJson<LabNavigationJournal.Manifest>(Read(archive,"manifest.json")).samples==20,"Snapshot manifest matches log boundary");
            }
            Check(zip!=LabNavigationJournal.WriteZip(snapshot,Output),"Repeated exports have unique names");
            journal.Pause(true,5);Check(journal.Current.state=="paused","Pause flushes without ending journey");journal.Pause(false,6);
            // Simulate a full/unwritable location. A retry must not lose or duplicate pending rows.
            string moved=first+"-moved";Directory.Move(first,moved);File.WriteAllText(first,"blocked directory");
            bool failed=false;try{journal.Append("{\"retry\":true}","21,row\n",8,false,false);}catch(IOException){failed=true;}
            Check(failed,"Write failure is surfaced");File.Delete(first);Directory.Move(moved,first);journal.Flush();
            Check(File.ReadAllLines(Path.Combine(first,"trace-0001.jsonl")).Length==22,"Retry preserves pending sample once");
            journal.End("USER_STOP",9);Check(!journal.Active,"Stopping finalizes run");
            var reopened=new LabNavigationJournal(root);Check(reopened.LatestFolder()==first,"Previous run found after app restart");
            string recovered=LabNavigationJournal.WriteZip(reopened.Capture(new Dictionary<string,string>()),Output);
            using(var archive=ZipFile.OpenRead(recovered))Check(JsonUtility.FromJson<LabNavigationJournal.Manifest>(Read(archive,"manifest.json")).endReason=="USER_STOP","Stopped run exports after restart");
            journal.Begin("0.9.1",10,context);Check(journal.Folder!=first&&File.Exists(Path.Combine(first,"manifest.json")),"Starting new journey retains previous run");
            string large="{\"reason\":\""+new string('x',12000)+"\"}";
            for(int i=0;i<100;i++)journal.Append(large,i+",large\n",11+i*.2,true,true);
            journal.End("ROLLOVER",35);var rollover=journal.Capture(context);
            Check(rollover.files.Count(x=>x.Key.EndsWith(".jsonl"))>=2,"Large runs retain multiple log parts");
            string rolloverZip=LabNavigationJournal.WriteZip(rollover,Output);
            using(var archive=ZipFile.OpenRead(rolloverZip))
            {int rows=archive.Entries.Where(x=>x.Name.EndsWith(".jsonl")).Sum(e=>Read(archive,e.Name).Split('\n').Count(x=>x.Length>0));Check(rows==100,"All rollover samples exported");}
            foreach(var size in new[]{new Vector2Int(360,800),new Vector2Int(412,915)})foreach(string state in new[]{"walking","recover","log","reopened"})Render(size,state,root);
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" filesystem, snapshot, recovery and native UI checks. Android MediaStore, camera and physical navigation NOT VERIFIED.\n");
        }
        static void Render(Vector2Int size,string state,string rootPath)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            var rt=new RenderTexture(size.x,size.y,24);var previous=RenderTexture.active;
            try
            {
                var root=new GameObject("Journal UI fixture");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<LabTestSession>();
                Prop(owner,"Floor",JsonUtility.FromJson<LabFloorPlan>(Resources.Load<TextAsset>("LabTest/LabFloorPlanNew").text));
                Prop(owner,"Map",JsonUtility.FromJson<LabSurvey>(Resources.Load<TextAsset>("LabTest/LabNavigationPresetNew").text));Field(owner,"ready",true);
                Field(owner,"journal",new LabNavigationJournal(rootPath));Field(owner,"journalAvailable",true);
                if(state=="recover")owner.EditorReviewRecovery(false);else if(state!="reopened")owner.EditorReviewNavigation("RẼ PHẢI",true,false,false);
                var ui=root.AddComponent<LabTestUi>();ui.Owner=owner;Call(ui,"Start");
                if(state=="log"||state=="reopened")Field(ui,"showingNavigationLog",true);
                var camera=new GameObject("Journal camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.targetTexture=rt;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.24f);
                var canvas=root.GetComponentInChildren<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=5;
                for(int i=0;i<4;i++){Call(ui,"LateUpdate");foreach(var text in root.GetComponentsInChildren<Text>())text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);Canvas.ForceUpdateCanvases();}
                var buttons=root.GetComponentsInChildren<Button>();
                Check(buttons.Any(b=>b.name.Contains(state=="log"||state=="reopened"?"Xuất ZIP lượt chỉ đường":"Lưu / xuất")),"Export reachable: "+state);
                foreach(var button in buttons)Check(button.GetComponent<RectTransform>().rect.height>=44,"Touch size "+button.name);
                foreach(var text in root.GetComponentsInChildren<Text>())
                {
                    float need=text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,text.GetGenerationSettings(text.rectTransform.rect.size))/text.pixelsPerUnit;
                    Check(need<=text.rectTransform.rect.height+1,"Clipped "+state+" "+size+" "+text.text);
                }
                camera.Render();RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(Output+"/"+state+"-"+size.x+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally{RenderTexture.active=previous;EditorSceneManager.CloseScene(scene,true);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
    }
}
