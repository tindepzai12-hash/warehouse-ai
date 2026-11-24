using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AtlasAR;
using JarVis.Warehouse.Backend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class JarvisRepairReview
    {
        static JarvisRepairReview()
        {
            EditorApplication.playModeStateChanged+=s=>{
                if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("JarvisRepairQA",false))
                {SessionState.SetBool("JarvisRepairQA",false);new GameObject("Isolated repair QA").AddComponent<JarvisRepairRunner>();}
            };
        }
        public static void Tests()
        {
            Directory.CreateDirectory("Logs/Repair20261110");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            SessionState.SetBool("JarvisRepairQA",true);EditorApplication.isPlaying=true;
        }
        public static void Build()
        {
            var enabled=EditorBuildSettings.scenes.ToList();
            if(!enabled.Any(s=>s.path=="Assets/Scenes/AtlasAR.unity"))enabled.Add(new EditorBuildSettingsScene("Assets/Scenes/AtlasAR.unity",true));
            EditorBuildSettings.scenes=enabled.ToArray();AssetDatabase.SaveAssets();
            AndroidBuild.BuildApk();
        }
    }
    public sealed class JarvisRepairRunner:MonoBehaviour
    {
        readonly List<string> lines=new List<string>(); bool failed; float deadline;
        void Check(bool ok,string detail){lines.Add((ok?"PASS ":"FAIL ")+detail);if(!ok)failed=true;}
        void Session(string route,string token="FIXTURE"){OmsMobileSession.Set("http://127.0.0.1:18774/"+route,token,"QA",DateTime.UtcNow.AddHours(1).ToString("O"));}
        void Update(){if(deadline>0&&Time.realtimeSinceStartup>deadline){Check(false,"watchdog");Finish();}}
        void Finish(){deadline=0;File.WriteAllLines("Logs/Repair20261110/result.txt",lines);OmsMobileSession.Clear();EditorApplication.Exit(failed?1:0);}
        IEnumerator Start()
        {
            deadline=Time.realtimeSinceStartup+90;yield return null;
            Session("normal");string error=null;OmsMobileApi.JarvisReply reply=null;
            yield return OmsMobileApi.AskJarvis("fixture",v=>reply=v,v=>error=v);
            Check(reply?.message=="fixture answer"&&error==null,"text response accepted from isolated fixture");
            Session("upstream");error=null;yield return OmsMobileApi.AskJarvis("fixture",_=>{},v=>error=v);
            Check(OmsMobileSession.IsReady&&error!=null&&error.StartsWith("AI_AUTH_ERROR"),"upstream 401 with /me 200 preserves mobile session");
            Session("expired");error=null;yield return OmsMobileApi.AskJarvis("fixture",_=>{},v=>error=v);
            Check(!OmsMobileSession.IsReady&&error!=null,"authoritative /me 401 clears exact expired session");
            foreach(string mode in new[]{"malformed","empty"})
            {Session(mode);error=null;reply=null;yield return OmsMobileApi.AskJarvis("fixture",v=>reply=v,v=>error=v);Check(reply==null&&error!=null&&OmsMobileSession.IsReady,mode+" response fails gracefully without logging out");}
            Session("stale","OLD_FIXTURE");bool done=false;error=null;
            StartCoroutine(Stale(v=>{error=v;done=true;}));yield return new WaitForSeconds(.1f);Session("normal","NEW_FIXTURE");
            while(!done)yield return null;
            Check(error=="SESSION_CHANGED"&&OmsMobileSession.AccessToken=="NEW_FIXTURE","late request cannot overwrite or clear a newer session");
            Session("normal");error=null;OmsMobileApi.VoiceReply voice=null;
            var photo=new Texture2D(4,4);byte[] jpeg=photo.EncodeToJPG();Destroy(photo);
            using(var upload=new OmsMobileApi.VoiceUpload())
            {
                yield return upload.Send(new byte[32044],v=>voice=v,v=>error=v,()=>{},"fixture-voice",jpeg);
                Check(voice?.reply=="fixture answer"&&voice.transcript=="fixture spoken question"&&error==null,"legacy capability 404 falls back to one transcription + photo question");
                Check(!upload.RetryAllowed,"legacy ambiguous requests cannot be replayed automatically");
            }
            Check(JarvisTestProduct.Matches("4710043004342")&&!JarvisTestProduct.Matches("4710043004343"),"milk barcode exact matching preserves identifier");
            var p=JarvisTestProduct.Milk;Check(p.location=="B"&&p.manufacturedOn=="2027-06-14"&&p.receivedOn=="2027-11-10"&&p.expiresOn=="2027-12-12","owner test dates and lab B preserved");
            var q=new string('x',800);Check(JarvisTestProduct.PhotoContext(q)==q,"long photo question never silently truncated");
            try
            {
                var map=AtlasMap.Load();Check(map.markers.Length==5&&map.id=="lab-owner-approved-20261106","approved lab A-E map reused, not bedroom");
                using(var nav=new AtlasNavMeshService(map))
                {
                    Check(nav.Ready,"saved lab NavMesh loaded");
                    foreach(var from in map.markers)foreach(var to in map.markers)if(from.id!=to.id)
                    {var route=nav.Plan(nav.Arrival(from.id),to.id);Check(route.valid,from.id+" -> "+to.id+" complete route: "+route.reason);}
                }
                var frame=new Pose(new Vector3(2,0,-3),Quaternion.Euler(0,123,0));
                var mp=new Pose(new Vector3(1,1.4f,2),Quaternion.Euler(10,32,0));
                var wp=new Pose(frame.position+frame.rotation*mp.position,frame.rotation*mp.rotation);
                Check(AtlasPoseMath.TryMapFrame(wp,mp,out var fitted)&&Vector3.Distance(fitted.position,frame.position)<.001f&&Quaternion.Angle(fitted.rotation,frame.rotation)<.01f,"map-camera frame conversion preserves rotation and translation");
            }
            catch(Exception e){Check(false,"map QA: "+e.GetType().Name+": "+e.Message);}
            Finish();
        }
        IEnumerator Stale(Action<string> done){yield return OmsMobileApi.AskJarvis("fixture",_=>done("UNEXPECTED_SUCCESS"),done);}
    }
}

