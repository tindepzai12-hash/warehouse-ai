#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using Immersal.Samples.Navigation;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoompresentQuickBuild
    {
        public const string ScenePath="Assets/Scenes/Roompresent_AR_QuickTest.unity";
        const string Request="Library/RoompresentQuickBuild.request";
        const string Logs="Logs/RoompresentQuick/";
        const string Generated="Assets/Map Data/Roompresent/Generated/QuickTest/";
        const string Samples="Assets/Samples/Immersal SDK/2.5.0/Core Samples/Prefabs/Navigation/";
        const string Main="Assets/Scenes/JarVisFigmaApp.unity";
        public const string Output="Builds/Android/JarVis-Warehouse-0.3.1.apk";
        static bool busy;
        static RoompresentQuickBuild(){EditorApplication.update+=Tick;}
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Tick()
        {
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            if((DateTime.UtcNow-File.GetLastWriteTimeUtc(Request)).TotalSeconds<8)return;
            string command=File.ReadAllText(Request).Trim();File.Move(Request,Request+".running-"+DateTime.UtcNow.Ticks);busy=true;
            try{if(command=="setup")Setup();else if(command=="review")Review();else if(command=="build")Build();else throw new InvalidOperationException("Unknown Roompresent command");}
            catch(Exception e){Directory.CreateDirectory(Logs);File.WriteAllText(Logs+"failure.txt",e.ToString());Debug.LogError("[RoompresentQuickBuild] "+e);}
            finally{busy=false;}
        }
        static T[] All<T>()where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        static void PreserveDirty()
        {Directory.CreateDirectory("Library/RoompresentQuickBackups");for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isDirty){string backup="Library/RoompresentQuickBackups/"+scene.name+"-"+DateTime.UtcNow.Ticks+".unity";Check(EditorSceneManager.SaveScene(scene,backup,true)&&File.Exists(backup),"Could not preserve scene edits");}}}
        static GameObject Child(string name,Transform parent=null){var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);return go;}
        static GameObject Prefab(string name,Transform parent){var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Samples+name+".prefab");Check(asset!=null,"Missing navigation prefab "+name);return(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);}
        static Vector3 Floor(Vector3 point){point.y=.015f;return point;}
        static Vector3 Access(NavMeshSurface surface,Vector3 objectPoint)
        {var filter=new NavMeshQueryFilter{agentTypeID=surface.agentTypeID,areaMask=NavMesh.AllAreas};Check(NavMesh.SamplePosition(Floor(objectPoint),out var hit,.95f,filter),"No observed floor beside "+objectPoint);return hit.position;}
        [MenuItem("Tools/JarVis/Roompresent Test/01 Set Up Presentation Test")]
        public static void Setup()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorUtility.scriptCompilationFailed,"Stop Play and compile first");
            Check(!File.Exists(ScenePath),"Presentation test scene already exists; Review preserves edits");PreserveDirty();Directory.CreateDirectory(Generated);Directory.CreateDirectory(Logs);
            var source=EditorSceneManager.OpenScene(ImmersalVideoSetup.ScenePath,OpenSceneMode.Single);var venue=All<RoompresentVenue>().Single();
            var targets=venue.targets.GetComponentsInChildren<IsNavigationTarget>(true).ToDictionary(t=>t.targetName,t=>venue.transform.InverseTransformPoint(t.transform.position));
            Check(new[]{"A","B","C"}.All(targets.ContainsKey),"Roompresent targets missing");
            var observedFloor=venue.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Walkable Floor From Scan");Check(observedFloor.sharedMesh!=null,"Roompresent observed floor missing");
            var arRoots=source.GetRootGameObjects().Where(r=>r.GetComponent<ARSession>()!=null||r.GetComponent<XROrigin>()!=null||r.GetComponent<EventSystem>()!=null||r.GetComponent<Light>()!=null).ToArray();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            foreach(var root in arRoots){var clone=UnityEngine.Object.Instantiate(root);clone.name=root.name;SceneManager.MoveGameObjectToScene(clone,scene);clone.SetActive(true);}
            var guide=Child("Roompresent Presentation - automatic two-book test").AddComponent<SmallRoomAR>();guide.presentationRoom=true;
            var map=Child("Roompresent - native AR anchor");guide.mapRoot=map.transform;
            Check(venue.referenceScan!=null,"Original Roompresent GLB reference missing");var reference=UnityEngine.Object.Instantiate(venue.referenceScan,map.transform,false);reference.name="Roompresent Scaniverse Reference - Editor Only";reference.tag="EditorOnly";reference.SetActive(true);
            var floor=Child("Roompresent observed clear floor",map.transform);floor.AddComponent<MeshFilter>().sharedMesh=observedFloor.sharedMesh;var renderer=floor.AddComponent<MeshRenderer>();
            var surface=floor.AddComponent<NavMeshSurface>();guide.surface=surface;surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.RenderMeshes;
            int agent=Enumerable.Range(0,NavMesh.GetSettingsCount()).Select(i=>NavMesh.GetSettingsByIndex(i).agentTypeID).Single(id=>NavMesh.GetSettingsNameFromID(id)=="JarVis SmallRoom Test");surface.agentTypeID=agent;surface.overrideVoxelSize=true;surface.voxelSize=.035f;
            EditorSceneManager.CloseScene(source,true);All<XROrigin>().Single().transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            surface.BuildNavMesh();Check(surface.navMeshData!=null,"Roompresent NavMesh bake failed");if(!AssetDatabase.Contains(surface.navMeshData))AssetDatabase.CreateAsset(surface.navMeshData,Generated+"RoompresentQuickNavMesh.asset");renderer.enabled=false;
            guide.startLocal=Access(surface,targets["A"]);guide.basinLocal=targets["B"];guide.bagLocal=targets["C"];guide.basinAccess=Access(surface,targets["B"]);guide.bagAccess=Access(surface,targets["C"]);
            var pins=Child("Navigation Targets A B C",map.transform);guide.pins=pins.transform;
            foreach(var pair in targets){if(!new[]{"A","B","C"}.Contains(pair.Key))continue;var pin=Prefab("Navigation Target",pins.transform);pin.name="Navigation Target "+pair.Key;pin.transform.localPosition=pair.Value+Vector3.up*.12f;pin.transform.localScale=Vector3.one*.45f;pin.GetComponent<IsNavigationTarget>().targetName=pair.Key;PrefabUtility.RecordPrefabInstancePropertyModifications(pin.GetComponent<IsNavigationTarget>());PrefabUtility.RecordPrefabInstancePropertyModifications(pin.transform);}
            var arrows=Prefab("Navigation Path",guide.transform);arrows.name="Live Roompresent AR arrows";arrows.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);guide.path=arrows.GetComponent<NavigationPath>();guide.path.pathWidth=.22f;PrefabUtility.RecordPrefabInstancePropertyModifications(guide.path);
            Check(EditorSceneManager.SaveScene(scene,ScenePath),"Roompresent test scene save failed");var buildScenes=EditorBuildSettings.scenes.ToList();buildScenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=buildScenes.ToArray();AssetDatabase.SaveAssets();Review();Preview(guide);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Logs+"placement.json",JsonUtility.ToJson(new Placement{start=guide.startLocal,A=targets["A"],B=guide.basinLocal,C=guide.bagLocal,accessB=guide.basinAccess,accessC=guide.bagAccess},true));Debug.Log("[RoompresentQuickBuild] SETUP PASS");
        }
        [Serializable]sealed class Placement{public Vector3 start,A,B,C,accessB,accessC;}
        static NavMeshPath Route(SmallRoomAR guide,Vector3 from,Vector3 to)
        {var filter=new NavMeshQueryFilter{agentTypeID=guide.surface.agentTypeID,areaMask=NavMesh.AllAreas};var path=new NavMeshPath();Check(NavMesh.SamplePosition(from,out var a,.35f,filter)&&NavMesh.SamplePosition(to,out var b,.35f,filter)&&NavMesh.CalculatePath(a.position,b.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete,"Incomplete presentation route "+from+" -> "+to);for(int i=1;i<path.corners.Length;i++){int steps=Math.Max(1,Mathf.CeilToInt(Vector3.Distance(path.corners[i-1],path.corners[i])/.05f));for(int j=0;j<=steps;j++)Check(NavMesh.SamplePosition(Vector3.Lerp(path.corners[i-1],path.corners[i],(float)j/steps),out _,.025f,filter),"Route leaves observed floor");}return path;}
        [MenuItem("Tools/JarVis/Roompresent Test/02 Review Presentation Test")]
        public static void Review()
        {
            Directory.CreateDirectory(Logs);Check(SceneManager.GetActiveScene().path==ScenePath,"Open Roompresent quick test");var guide=All<SmallRoomAR>().Single();
            Check(guide.presentationRoom&&guide.surface!=null&&guide.path!=null&&guide.pins!=null,"Wrong mission or missing route references");Check(All<ARSession>().Length==1&&All<XROrigin>().Length==1&&All<ARCameraManager>().Length==1&&All<ARCameraBackground>().Length==1&&All<EventSystem>().Length==1,"Unique AR ownership required");Check(All<Immersal.ImmersalSDK>().Length==0&&All<Immersal.XR.XRMap>().Length==0&&All<RoompresentVenue>().Length==0,"Quick test must not inherit old registration dialogs or competing map movement");
            var points=new[]{guide.startLocal,guide.basinAccess,guide.bagAccess};var rows=new List<string>();
            try{foreach(bool moved in new[]{false,true}){guide.mapRoot.SetPositionAndRotation(moved?new Vector3(2,.4f,-1):Vector3.zero,moved?Quaternion.Euler(0,37,0):Quaternion.identity);guide.surface.RemoveData();guide.surface.AddData();for(int a=0;a<3;a++)for(int b=0;b<3;b++)if(a!=b){var path=Route(guide,guide.mapRoot.TransformPoint(points[a]),guide.mapRoot.TransformPoint(points[b]));float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);rows.Add("PASS "+(moved?"moved ":"local ")+"ABC"[a]+" -> "+"ABC"[b]+" corners="+path.corners.Length+" meters="+length.ToString("F3"));}}}
            finally{guide.mapRoot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);guide.surface.RemoveData();guide.surface.AddData();}
            File.WriteAllLines(Logs+"paths.txt",rows);VerifyMission();Debug.Log("[RoompresentQuickBuild] REVIEW PASS");
        }
        static void VerifyMission()
        {
            var m=new RoompresentDeliveryRecord();Check(!m.ScanProduct(RoompresentDeliveryRecord.A1),"Unaccepted book");m.accepted=true;Check(!m.ScanProduct("4710043004342"),"Wrong item");Check(m.ScanProduct(RoompresentDeliveryRecord.A1)&&!m.Loaded&&m.ScanProduct(RoompresentDeliveryRecord.A1)&&!m.Loaded,"Duplicate A1 unlocked route");Check(!m.Confirm("JWM:MAIN:B","B",true,.2f),"Missing A2 accepted");Check(m.ScanProduct(RoompresentDeliveryRecord.A2)&&m.Loaded,"A2 failed");Check(!m.Confirm("JWM:MAIN:C","B",true,.2f)&&!m.Confirm("JWM:MAIN:B","B",false,.2f),"Wrong destination/tracking");foreach(float d in new[]{float.NaN,float.PositiveInfinity,-1f,1.21f})Check(!m.Confirm("JWM:MAIN:B","B",true,d),"Invalid distance");Check(m.Confirm("JWM:ROOMPRESENT:153613:B","B",true,.2f)&&!m.Confirm("JWM:MAIN:B","B",true,.2f)&&m.Confirm("JWM:ROOMPRESENT:153613:C","C",true,.2f)&&m.Complete,"B/C delivery sequence");var r=new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};Check(r.Confirm("JWM:MAIN:C","C",true,.2f)&&r.Confirm("JWM:MAIN:B","B",true,.2f)&&r.Complete,"Reverse sequence");
            Check(SmallRoomDeliveryRecord.Destination("JWM:SMALLROOM:225317:B")=="B"&&RoompresentDeliveryRecord.Destination("JWM:SMALLROOM:225317:B")==null,"Wrong venue-specific label accepted in Roompresent");File.WriteAllText(Logs+"mission.txt","PASS exact books, duplicate/missing scans, both delivery orders, checkpoint aliases, wrong venue QR, tracking/distance guards. Roompresent and small-room progress remain separate.\nPhysical walking/camera/AI tests pending.\n");
        }
        static void Preview(SmallRoomAR guide)
        {
            var preview=Child("Editor route preview only");preview.tag="EditorOnly";foreach(var pair in new[]{(guide.startLocal,guide.basinAccess),(guide.basinAccess,guide.bagAccess)}){var visual=Prefab("Navigation Path",preview.transform).GetComponent<NavigationPath>();visual.pathWidth=.22f;visual.GenerateValidatedPolyline(Route(guide,pair.Item1,pair.Item2).corners.ToList(),Vector3.up);}
            Selection.activeGameObject=guide.mapRoot.gameObject;var view=SceneView.lastActiveSceneView;if(view!=null){view.LookAt(Vector3.zero,Quaternion.LookRotation(new Vector3(.15f,-1,.15f)),7,true);view.Repaint();}
        }
        [MenuItem("Tools/JarVis/Roompresent Test/03 Build Full JarVis APK")]
        public static void Build()
        {
            PreserveDirty();if(!File.Exists(ScenePath))Setup();else EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Review();
            // The shared scanner/guide also serve the small room: check that saved scene too.
            EditorSceneManager.OpenScene(SmallRoomSceneBuild.ScenePath,OpenSceneMode.Single);SmallRoomSceneBuild.Review();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var required=new[]{Main,"Assets/Scenes/SplashScene.unity","Assets/Scenes/LoginScene.unity","Assets/Scenes/WarehouseScene.unity","Assets/Scenes/AtlasAR.unity",ImmersalVideoSetup.ScenePath,SmallRoomSceneBuild.ScenePath,ScenePath};foreach(var path in required)Check(File.Exists(path),"Missing full app scene "+path);Check(!EditorBuildSettings.scenes.Any(s=>s.enabled&&!required.Contains(s.path)),"Unexpected enabled scene; preserve before replacing build list");EditorBuildSettings.scenes=required.Select(path=>new EditorBuildSettingsScene(path,true)).ToArray();AndroidProjectConfigurator.ApplySettings();JarvisProjectValidator.Validate();AssetDatabase.SaveAssets();Directory.CreateDirectory(Path.GetDirectoryName(Output));EditorUserBuildSettings.buildAppBundle=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=required,target=BuildTarget.Android,locationPathName=Path.GetFullPath(Output),options=BuildOptions.StrictMode|BuildOptions.CompressWithLz4HC});File.WriteAllText(Logs+"build.txt","result="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nseconds="+report.summary.totalTime.TotalSeconds+"\nFull JarVis 0.3.1/code15; both room tests and original Immersal scene. Physical/AI acceptance pending.\n");Check(report.summary.result==BuildResult.Succeeded&&File.Exists(Output),"Full JarVis build failed");EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Debug.Log("[RoompresentQuickBuild] APK READY "+Path.GetFullPath(Output));
        }
    }
}
#endif
