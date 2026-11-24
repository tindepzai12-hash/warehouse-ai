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
using ZXing;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class SmallRoomSceneBuild
    {
        public const string ScenePath="Assets/Scenes/SmallRoom_AR_225317.unity";
        const string Data="Assets/Map Data/SmallRoom225317/";
        const string Generated=Data+"Generated/";
        const string Logs="Logs/SmallRoom225317/";
        const string Request="Library/SmallRoomBuild.request";
        const string Samples="Assets/Samples/Immersal SDK/2.5.0/Core Samples/Prefabs/Navigation/";
        const string Main="Assets/Scenes/JarVisFigmaApp.unity";
        public const string Output="Builds/Android/JarVis-Warehouse-0.3.1.apk";
        static bool busy;
        static readonly Vector3 A=new Vector3(1.485522f,0,-1.901156f), B=new Vector3(-.869063f,.082853f,2.343723f), C=new Vector3(-2.434365f,.234324f,1.090140f);
        static readonly Vector3 BA=new Vector3(-.882329f,0,1.833017f), CA=new Vector3(-1.625184f,0,.824857f);
        static SmallRoomSceneBuild(){EditorApplication.update+=Tick;}
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static void Tick()
        {
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request))return;
            string command=File.ReadAllText(Request).Trim(); File.Move(Request,Request+".running-"+DateTime.UtcNow.Ticks);busy=true;
            try { if(command=="setup")Setup();else if(command=="build")Build();else if(command=="review")Review();else throw new InvalidOperationException("Unknown small room command"); }
            catch(Exception e){Directory.CreateDirectory(Logs);File.WriteAllText(Logs+"failure.txt",e.ToString());Debug.LogError("[SmallRoomBuild] "+e);}
            finally{busy=false;}
        }
        static T[] All<T>()where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        static GameObject Child(string name,Transform parent=null){var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);return go;}
        static GameObject Prefab(string path,Transform parent)
        {var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);Check(source!=null,"Missing official prefab "+path);return (GameObject)PrefabUtility.InstantiatePrefab(source,parent);}
        static T Save<T>(T value,string name)where T:UnityEngine.Object
        {string path=Generated+name+".asset";var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}EditorUtility.CopySerialized(value,old);UnityEngine.Object.DestroyImmediate(value);return old;}
        static void PreserveDirtyScenes()
        {
            Directory.CreateDirectory("Library/SmallRoomBackups");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {var s=SceneManager.GetSceneAt(i);if(s.isDirty){string backup="Library/SmallRoomBackups/"+s.name+"-"+DateTime.UtcNow.Ticks+".unity";Check(EditorSceneManager.SaveScene(s,backup,true)&&File.Exists(backup),"Could not preserve dirty scene");}}
        }
        [MenuItem("Tools/JarVis/Small Room/01 Set Up and Verify")]
        public static void Setup()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorUtility.scriptCompilationFailed,"Stop Play and compile before setup");
            PreserveDirtyScenes();Directory.CreateDirectory(Generated);Directory.CreateDirectory(Logs);
            Check(!File.Exists(ScenePath),"Small room scene already exists; use Review instead of replacing edits");
            var source=EditorSceneManager.OpenScene(ImmersalVideoSetup.ScenePath,OpenSceneMode.Single);
            var sourceRoots=source.GetRootGameObjects();
            var keep=sourceRoots.Where(r=>r.GetComponent<ARSession>()!=null||r.GetComponent<XROrigin>()!=null||r.GetComponent<EventSystem>()!=null||r.GetComponent<Light>()!=null).ToArray();
            Check(keep.Count(r=>r.GetComponent<XROrigin>()!=null)==1&&keep.Count(r=>r.GetComponent<ARSession>()!=null)==1,"AR camera/session source missing");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            foreach(var root in keep){var clone=UnityEngine.Object.Instantiate(root);clone.name=root.name;SceneManager.MoveGameObjectToScene(clone,scene);clone.SetActive(true);}
            EditorSceneManager.CloseScene(source,true);
            var origin=All<XROrigin>().Single();origin.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var controller=Child("Small Room Delivery - A1 B and A2 C").AddComponent<SmallRoomAR>();
            var map=Child("SmallRoom225317 - native AR anchor",null);controller.mapRoot=map.transform;
            controller.startLocal=A;controller.basinLocal=B;controller.bagLocal=C;controller.basinAccess=BA;controller.bagAccess=CA;
            Mesh reference=ReadMesh();reference=Save(reference,"ScanReference");
            var scan=Child("Scaniverse Reference - Editor Only",map.transform);scan.tag="EditorOnly";scan.AddComponent<MeshFilter>().sharedMesh=reference;
            string texturePath=Data+"Scaniverse_2026_11_19_225317.jpg";
            var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);importer.maxTextureSize=2048;importer.mipmapEnabled=false;importer.SaveAndReimport();
            var material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));material.name="Original Scaniverse texture";material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);material.SetFloat("_Cull",0);material=Save(material,"ScanMaterial");
            scan.AddComponent<MeshRenderer>().sharedMaterial=material;
            var floor=Child("Observed clear floor",map.transform);floor.AddComponent<MeshFilter>().sharedMesh=Save(Floor(reference),"ObservedFloor");
            var floorRenderer=floor.AddComponent<MeshRenderer>();floorRenderer.sharedMaterial=material;
            var surface=floor.AddComponent<NavMeshSurface>();controller.surface=surface;surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.RenderMeshes;
            int agent=-1;for(int i=0;i<NavMesh.GetSettingsCount();i++){var settings=NavMesh.GetSettingsByIndex(i);if(NavMesh.GetSettingsNameFromID(settings.agentTypeID)=="JarVis SmallRoom Test")agent=settings.agentTypeID;}
            if(agent<0){agent=NavMesh.CreateSettings().agentTypeID;var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);var entries=settings.FindProperty("m_Settings");var names=settings.FindProperty("m_SettingNames");for(int i=0;i<entries.arraySize;i++){var entry=entries.GetArrayElementAtIndex(i);if(entry.FindPropertyRelative("agentTypeID").intValue!=agent)continue;entry.FindPropertyRelative("agentRadius").floatValue=.22f;entry.FindPropertyRelative("agentHeight").floatValue=1.7f;entry.FindPropertyRelative("agentClimb").floatValue=.08f;entry.FindPropertyRelative("agentSlope").floatValue=15;entry.FindPropertyRelative("minRegionArea").floatValue=.03f;names.GetArrayElementAtIndex(i).stringValue="JarVis SmallRoom Test";}settings.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();}
            surface.agentTypeID=agent;surface.overrideVoxelSize=true;surface.voxelSize=.035f;surface.BuildNavMesh();Check(surface.navMeshData!=null,"No floor NavMesh baked");
            if(!AssetDatabase.Contains(surface.navMeshData))AssetDatabase.CreateAsset(surface.navMeshData,Generated+"WalkableNavMesh.asset");floorRenderer.enabled=false;
            var pins=Child("Navigation Targets A B C",map.transform);controller.pins=pins.transform;
            foreach(var point in new[]{("A",A),("B",B),("C",C)})
            {var pin=Prefab(Samples+"Navigation Target.prefab",pins.transform);pin.name="Navigation Target "+point.Item1;pin.transform.localPosition=point.Item2+Vector3.up*.12f;pin.transform.localScale=Vector3.one*.45f;pin.GetComponent<IsNavigationTarget>().targetName=point.Item1;PrefabUtility.RecordPrefabInstancePropertyModifications(pin.GetComponent<IsNavigationTarget>());PrefabUtility.RecordPrefabInstancePropertyModifications(pin.transform);}
            var path=Prefab(Samples+"Navigation Path.prefab",controller.transform);path.name="Live AR arrows";path.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);controller.path=path.GetComponent<NavigationPath>();controller.path.pathWidth=.22f;PrefabUtility.RecordPrefabInstancePropertyModifications(controller.path);
            Check(EditorSceneManager.SaveScene(scene,ScenePath),"Could not save small room scene");
            var enabled=EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath).ToList();enabled.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=enabled.ToArray();AssetDatabase.SaveAssets();Review();Preview(controller);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText(Logs+"setup.txt","PASS new room scene, original scan texture, observed floor NavMesh, A door / B basin / C black tote. No Roompresent cloud localization in this scene.\nPhysical accuracy unverified; automatic alignment assumes standing at A facing B.\n");Debug.Log("[SmallRoomBuild] SETUP PASS");
        }
        static Mesh ReadMesh()
        {
            using(var reader=new BinaryReader(File.OpenRead(Data+"SmallRoom225317.meshbin")))
            {Check(reader.ReadUInt32()==0x534D4150,"Unexpected scan binary");int count=checked((int)reader.ReadUInt32());Check(count>0&&count%3==0&&count<2000000,"Invalid scan vertex count");var vertices=new Vector3[count];var uv=new Vector2[count];for(int i=0;i<count;i++){vertices[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());uv[i]=new Vector2(reader.ReadSingle(),reader.ReadSingle());}Check(reader.BaseStream.Position==reader.BaseStream.Length,"Trailing scan binary data");var mesh=new Mesh{name="Original Scaniverse225317",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=Enumerable.Range(0,count).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }
        static Mesh Floor(Mesh source)
        {
            var v=source.vertices;var result=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<v.Length;i+=3)
            {var a=v[i];var b=v[i+1];var c=v[i+2];var normal=Vector3.Cross(b-a,c-a).normalized;if(Mathf.Max(a.y,b.y,c.y)>.065f||Mathf.Min(a.y,b.y,c.y)<-.04f||Mathf.Abs(normal.y)<.93f||Mathf.Max((a-b).magnitude,Mathf.Max((b-c).magnitude,(c-a).magnitude))>.7f)continue;a.y=b.y=c.y=0;int n=result.Count;result.Add(a);result.Add(b);result.Add(c);indices.Add(n);indices.Add(n+(normal.y>0?1:2));indices.Add(n+(normal.y>0?2:1));}
            Check(indices.Count>300,"Too little observed floor");var mesh=new Mesh{name="Observed floor only",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(result);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static NavMeshPath Route(SmallRoomAR room,Vector3 a,Vector3 b)
        {var filter=new NavMeshQueryFilter{agentTypeID=room.surface.agentTypeID,areaMask=NavMesh.AllAreas};var path=new NavMeshPath();Check(NavMesh.SamplePosition(a,out var x,.35f,filter)&&NavMesh.SamplePosition(b,out var y,.35f,filter)&&NavMesh.CalculatePath(x.position,y.position,filter,path)&&path.status==NavMeshPathStatus.PathComplete,"Disconnected floor route: "+a+" -> "+b);return path;}
        [MenuItem("Tools/JarVis/Small Room/02 Review Current Setup")]
        public static void Review()
        {
            Directory.CreateDirectory(Logs);Check(SceneManager.GetActiveScene().path==ScenePath,"Open small room scene for review");
            var room=All<SmallRoomAR>().Single();Check(All<ARSession>().Length==1&&All<XROrigin>().Length==1&&All<ARCameraManager>().Length==1&&All<ARCameraBackground>().Length==1&&All<EventSystem>().Length==1,"AR ownership must be unique");
            Check(All<Immersal.ImmersalSDK>().Length==0&&All<Immersal.XR.XRMap>().Length==0,"Old map localization must not be used for new room");
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var tr in root.GetComponentsInChildren<Transform>(true))Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)==0,"Missing script: "+tr.name);
            var points=new[]{A,BA,CA};var report=new List<string>();
            foreach(bool moved in new[]{false,true})
            {
                room.mapRoot.SetPositionAndRotation(moved?new Vector3(3,.3f,-2):Vector3.zero,moved?Quaternion.Euler(0,37,0):Quaternion.identity);room.surface.RemoveData();room.surface.AddData();
                for(int a=0;a<3;a++)for(int b=0;b<3;b++)if(a!=b){var route=Route(room,room.mapRoot.TransformPoint(points[a]),room.mapRoot.TransformPoint(points[b]));float distance=0;for(int i=1;i<route.corners.Length;i++)distance+=Vector3.Distance(route.corners[i-1],route.corners[i]);report.Add("PASS "+(moved?"moved ":"local ")+"ABC"[a]+" -> "+"ABC"[b]+" corners="+route.corners.Length+" meters="+distance.ToString("F3"));}
            }
            room.mapRoot.SetPositionAndRotation(Vector3.zero,Quaternion.identity);room.surface.RemoveData();room.surface.AddData();File.WriteAllLines(Logs+"paths.txt",report);
            VerifyCore();WriteQr();VerifyPhotos();Debug.Log("[SmallRoomBuild] REVIEW PASS");
        }
        static void VerifyCore()
        {
            Check(SmallRoomDeliveryRecord.Destination("JWM:ROOMPRESENT:153613:B")=="B"&&SmallRoomDeliveryRecord.Destination("JWM:ROOMPRESENT:153613:C")=="C","Existing printed B/C labels");
            var m=new SmallRoomDeliveryRecord();Check(!m.ScanProduct(SmallRoomDeliveryRecord.A1),"Unaccepted scan");m.accepted=true;Check(!m.ScanProduct("4710043004342"),"Milk matched book");Check(m.ScanProduct(SmallRoomDeliveryRecord.A1)&&!m.Loaded,"A1 guard");Check(m.ScanProduct(SmallRoomDeliveryRecord.A1)&&!m.Loaded,"Duplicate A1 guard");Check(!m.Confirm("JWM:MAIN:B","B",true,.2f),"Incomplete load");Check(m.ScanProduct(SmallRoomDeliveryRecord.A2)&&m.Loaded,"A2 guard");Check(!m.Confirm("JWM:MAIN:C","B",true,.2f)&&!m.Confirm("JWM:MAIN:B","B",false,.2f),"Wrong QR or lost tracking");foreach(float d in new[]{float.NaN,float.PositiveInfinity,-1f,1.21f})Check(!m.Confirm("JWM:MAIN:B","B",true,d),"Distance guard");Check(m.Confirm("JWM:MAIN:B","B",true,.2f)&&!m.Confirm("JWM:MAIN:B","B",true,.2f)&&m.Confirm("JWM:MAIN:C","C",true,.2f)&&m.Complete,"B then C");
            var reverse=new SmallRoomDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true};Check(reverse.Confirm("JWM:SMALLROOM:225317:C","C",true,.2f)&&reverse.Confirm("JWM:SMALLROOM:225317:B","B",true,.2f)&&reverse.Complete,"C then B");
            var rotation=Quaternion.Euler(0,37,0);var offset=new Vector3(3,-.2f,4);var foot=rotation*A+offset;Check(SmallRoomAR.TryStartPose(A,B,foot,rotation*(B-A).normalized,out var pose)&&Quaternion.Angle(rotation,pose.rotation)<.001f&&(pose.position-offset).magnitude<.001f,"Automatic start pose transform");Check(!SmallRoomAR.TryStartPose(A,B,foot,Vector3.up,out _),"Vertical camera guard");File.WriteAllText(Logs+"mission-core.txt","PASS duplicate/wrong barcode, both scan requirement, wrong QR, lost tracking, invalid distance, both delivery orders, automatic A/facing-B rigid transform. No live stock mutation.\n");
        }
        static void VerifyPhotos()
        {
            var report=new List<string>();var photos=new[]{("codex-clipboard-bb401bec-2b87-4198-9835-08959fb23516.png",SmallRoomDeliveryRecord.A1),("codex-clipboard-eb65edc0-483c-4dc4-bbfc-9deb6817e6aa.png",SmallRoomDeliveryRecord.A2)};
            foreach(var photo in photos){var texture=new Texture2D(2,2);try{Check(texture.LoadImage(File.ReadAllBytes("C:/Users/Windows/AppData/Local/Temp/"+photo.Item1)),"Book photo missing");var pixels=texture.GetPixels32();Result decoded=null;foreach(int divisor in new[]{2,1}){int inputHeight=divisor==1?texture.height/2:texture.height;int w=texture.width/divisor,h=inputHeight/divisor;var down=new Color32[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)down[y*w+x]=pixels[y*divisor*texture.width+x*divisor];decoded=ImportedCameraScanner.Decode(down,w,h,true);if(decoded!=null)break;}Check(decoded!=null&&decoded.Text==photo.Item2&&decoded.BarcodeFormat==BarcodeFormat.EAN_13,"Book photo failed "+photo.Item2);report.Add("PASS supplied actual book photo "+decoded.Text);}finally{UnityEngine.Object.DestroyImmediate(texture);}}
            File.WriteAllLines(Logs+"book-photos.txt",report);
        }
        static void WriteQr()
        {
            Directory.CreateDirectory("Docs/SmallRoomQR");foreach(string id in new[]{"B","C"}){string payload="JWM:SMALLROOM:225317:"+id;var matrix=new MultiFormatWriter().encode(payload,BarcodeFormat.QR_CODE,800,800);var pixels=new Color32[800*800];for(int y=0;y<800;y++)for(int x=0;x<800;x++)pixels[y*800+x]=matrix[x,799-y]?new Color32(0,0,0,255):new Color32(255,255,255,255);Check(ImportedCameraScanner.Decode(pixels,800,800)?.Text==payload,"QR roundtrip failed");var tex=new Texture2D(800,800,TextureFormat.RGB24,false);tex.SetPixels32(pixels);tex.Apply();File.WriteAllBytes("Docs/SmallRoomQR/SmallRoom-"+id+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);}
        }
        static void Preview(SmallRoomAR room)
        {
            var preview=Child("Editor preview only - not camera accuracy proof");preview.tag="EditorOnly";
            foreach(var pair in new[]{(A,BA),(BA,CA)}){var obj=Prefab(Samples+"Navigation Path.prefab",preview.transform);obj.GetComponent<NavigationPath>().pathWidth=.22f;obj.GetComponent<NavigationPath>().GenerateValidatedPolyline(Route(room,pair.Item1,pair.Item2).corners.ToList(),Vector3.up);}
            Selection.activeGameObject=room.mapRoot.gameObject;var view=SceneView.lastActiveSceneView;if(view!=null){view.LookAt(new Vector3(0,.2f,0),Quaternion.LookRotation(new Vector3(.2f,-1f,.2f)),6f,true);view.Repaint();}
        }
        [DrawGizmo(GizmoType.Selected|GizmoType.NonSelected)]
        static void Labels(SmallRoomAR room,GizmoType kind)
        {if(room.pins==null)return;var style=new GUIStyle(EditorStyles.boldLabel){fontSize=24};style.normal.textColor=new Color(1,.25f,.05f);foreach(var t in room.pins.GetComponentsInChildren<IsNavigationTarget>())Handles.Label(t.transform.position+Vector3.up*.15f,t.targetName,style);}
        [MenuItem("Tools/JarVis/Small Room/03 Build Full JarVis APK")]
        public static void Build()
        {
            if(!File.Exists(ScenePath))Setup();else{PreserveDirtyScenes();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Review();}
            var required=new[]{Main,"Assets/Scenes/SplashScene.unity","Assets/Scenes/LoginScene.unity","Assets/Scenes/WarehouseScene.unity","Assets/Scenes/AtlasAR.unity",ImmersalVideoSetup.ScenePath,ScenePath}.Concat(File.Exists(RoompresentQuickBuild.ScenePath)?new[]{RoompresentQuickBuild.ScenePath}:Array.Empty<string>()).ToArray();foreach(string s in required)Check(File.Exists(s),"Missing app scene "+s);Check(!EditorBuildSettings.scenes.Any(s=>s.enabled&&!required.Contains(s.path)),"Unexpected enabled scene; preserve before build");EditorBuildSettings.scenes=required.Select(s=>new EditorBuildSettingsScene(s,true)).ToArray();
            AndroidProjectConfigurator.ApplySettings();JarvisProjectValidator.Validate();AssetDatabase.SaveAssets();Directory.CreateDirectory(Path.GetDirectoryName(Output));EditorUserBuildSettings.buildAppBundle=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=required,target=BuildTarget.Android,locationPathName=Path.GetFullPath(Output),options=BuildOptions.StrictMode|BuildOptions.CompressWithLz4HC});File.WriteAllText(Logs+"build.txt","result="+report.summary.result+"\nbytes="+report.summary.totalSize+"\nerrors="+report.summary.totalErrors+"\nseconds="+report.summary.totalTime.TotalSeconds+"\nFull app 0.3.1/code15; physical camera and tracking acceptance pending.\n");Check(report.summary.result==BuildResult.Succeeded&&File.Exists(Output),"Full JarVis build failed");Debug.Log("[SmallRoomBuild] APK READY "+Path.GetFullPath(Output));EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        }
    }
}
#endif
