#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Immersal;
using Immersal.XR;
using Immersal.Samples.Navigation;
using JarVis.Warehouse.Navigation;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoompresentSceneBuilder
    {
        public const string Data = "Assets/Map Data/Roompresent/";
        public const string Generated = Data + "Generated/";
        const string Samples = "Assets/Samples/Immersal SDK/2.5.0/Core Samples/Prefabs/";
        const string RequestPath = "Library/RoompresentSceneBuilder.request";
        const string LocalToken = "Library/ImmersalVideoSetup/UserToken.local";
        static bool working;
        static RoompresentSceneBuilder() { EditorApplication.update += Tick; EditorApplication.delayCall += () => Debug.Log("[RoompresentSceneBuilder] VERSION 4 ready"); }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void TargetLabels(RoompresentVenue state,GizmoType kind)
        {
            if(state.targets==null)return;
            var style=new GUIStyle(EditorStyles.boldLabel){fontSize=22};style.normal.textColor=new Color(1f,.26f,.08f);
            foreach(var target in state.targets.GetComponentsInChildren<IsNavigationTarget>(true))Handles.Label(target.transform.position+Vector3.up*.2f,target.targetName,style);
        }
        [MenuItem("Tools/JarVis/Immersal/05 Set Up Roompresent Navigation")]
        public static void RequestSetup() { File.WriteAllText(RequestPath, "setup"); }
        [MenuItem("Tools/JarVis/Immersal/06 Review Roompresent Paths")]
        public static void Review() { ValidatePaths(); }
        [MenuItem("Tools/JarVis/Immersal/07 Show Roompresent Scene Preview")]
        public static void Preview()
        {
            RequireVenueScene();
            var state=All<RoompresentVenue>().Single();
            var root=Child("Editor Route Preview - NOT phone tracking",state.transform);root.tag="EditorOnly";
            var targets=state.targets.GetComponentsInChildren<IsNavigationTarget>(true).ToDictionary(t=>t.targetName,t=>t);
            foreach(var pair in new[]{new[]{"A","B"},new[]{"B","C"}})
            {
                var start=targets[pair[0]].transform.position;var end=targets[pair[1]].transform.position;start.y=end.y=.015f;
                if(!NavMesh.SamplePosition(start,out var a,.95f,NavMesh.AllAreas)||!NavMesh.SamplePosition(end,out var b,.95f,NavMesh.AllAreas))continue;
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var child=root.transform.Find(pair[0]+" to "+pair[1]);
                var obj=child!=null?child.gameObject:Prefab(Samples+"Navigation/Navigation Path.prefab",root.transform);obj.name=pair[0]+" to "+pair[1];
                var visual=obj.GetComponent<NavigationPath>();visual.pathWidth=.22f;visual.GenerateValidatedPolyline(path.corners.ToList(),Vector3.up);
            }
            Selection.activeGameObject=state.gameObject;
            var view=SceneView.lastActiveSceneView;
            if(view!=null){view.LookAt(new Vector3(-.4f,.5f,-.3f),Quaternion.LookRotation(new Vector3(-.9f,-.85f,.5f),Vector3.up),8f,true);view.Repaint();}
            SaveWithoutCredential();
            Debug.Log("[RoompresentSceneBuilder] Editor route preview generated; this is not a physical AR tracking result.");
        }

        static void Tick()
        {
            if (working || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            working = true;
            string command = File.ReadAllText(RequestPath).Trim();
            File.Move(RequestPath, RequestPath + ".running-" + DateTime.UtcNow.Ticks);
            try
            {
                if (command == "restart")
                {
                    RequireVenueScene();
                    SaveWithoutCredential();
                    Debug.Log("[RoompresentSceneBuilder] Saved scene before restarting only the Unity Editor.");
                    EditorApplication.delayCall += () => EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                }
                else if (command == "setup") Setup();
                else if (command == "review") ValidatePaths();
                else if (command == "preview") Preview();
                else throw new InvalidOperationException("Unknown Roompresent command.");
            }
            catch (Exception e) { Report("FAIL " + e); Debug.LogError("[RoompresentSceneBuilder] " + e.Message); }
            finally { working = false; }
        }

        static void RequireVenueScene()
        {
            if (SceneManager.GetActiveScene().path != ImmersalVideoSetup.ScenePath)
                throw new InvalidOperationException("Open the Immersal video scene; no other scene will be replaced.");
        }
        static T[] All<T>() where T:Component => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();
        static T Ensure<T>(GameObject go) where T:Component { var value=go.GetComponent<T>(); if(value==null)value=go.AddComponent<T>(); return value; }
        static GameObject Child(string name, Transform parent)
        {
            var existing = parent.Find(name); if (existing != null) return existing.gameObject;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Roompresent setup"); go.transform.SetParent(parent, false); return go;
        }
        static GameObject Prefab(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        public static void Setup()
        {
            RequireVenueScene();
            Directory.CreateDirectory(Generated);
            var scene = SceneManager.GetActiveScene();
            Directory.CreateDirectory("Library/ImmersalVideoSetup");
            EditorSceneManager.SaveScene(scene, "Library/ImmersalVideoSetup/BeforeRoompresentSetup.unity", true);
            var space = All<XRSpace>().Single();
            var map = All<XRMap>().Single();
            Undo.RecordObject(map, "Configure genuine Roompresent map");
            map.Configure(AssetDatabase.LoadAssetAtPath<TextAsset>(Data + "153613-Roompresent.bytes"));
            if (map.mapId != 153613 || !map.IsConfigured) throw new InvalidOperationException("Roompresent map configuration failed.");
            map.name = "XR Map 153613 - Roompresent";
            map.CreateVisualization(XRMapVisualization.RenderMode.EditorOnly);
            map.Visualization.LoadPly(Path.GetFullPath(Data + "153613-Roompresent-sparse.ply"));

            string glb = Data + "Scaniverse 2026-11-19 153704.glb";
            AssetDatabase.ImportAsset(glb, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(glb) == null)
                throw new InvalidOperationException("glTFast has not imported the room GLB yet; restart Editor after the package update and run step 05.");
            var content = Child("Roompresent Content", space.transform);
            var state = Ensure<RoompresentVenue>(content);
            state.alignmentVerified = false;
            var scan = content.transform.Find("Scaniverse Reference - Editor Only");
            if (scan == null)
            {
                var model = Prefab(glb, content.transform); model.name = "Scaniverse Reference - Editor Only"; model.tag = "EditorOnly"; scan = model.transform;
            }
            state.referenceScan = scan.gameObject;

            var targets = Child("Navigation Targets A B C", content.transform);
            PlaceTarget(targets.transform,"A",new Vector3(1.6321806f,.8264267f,-3.2412338f));
            PlaceTarget(targets.transform,"B",new Vector3(-2.0507436f,.8546972f,1.8392259f));
            PlaceTarget(targets.transform,"C",new Vector3(-.7009451f,.6700959f,2.8860228f));
            state.targets = targets;

            var navigation = All<NavigationManager>().FirstOrDefault();
            if (navigation == null) navigation = Prefab(Samples+"Navigation/Navigation UI.prefab",null).GetComponent<NavigationManager>();
            navigation.useAtlasValidatedRoute = false;
            navigation.requireCompleteNavMeshPath = true;
            var navProperties = new SerializedObject(navigation);
            navProperties.FindProperty("m_heightOffset").floatValue = .06f;
            navProperties.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(navigation);
            state.navigationUi = navigation.gameObject;
            if (!All<Immersal.Samples.Util.NotificationManager>().Any()) Prefab(Samples+"Util/Notification Manager.prefab",null);

            var surfaceObject = Child("GenerateNavMesh",content.transform);
            var floor = Child("Walkable Floor From Scan",surfaceObject.transform);
            int layer = EnsureNavigationLayer(); floor.layer = layer;
            var mesh = MakeFloor(scan);
            string meshPath = Generated+"Roompresent-WalkableFloor.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { AssetDatabase.CreateAsset(mesh,meshPath); saved = mesh; }
            else { EditorUtility.CopySerialized(mesh,saved); UnityEngine.Object.DestroyImmediate(mesh); }
            Ensure<MeshFilter>(floor).sharedMesh = saved;
            var renderer = Ensure<MeshRenderer>(floor);
            renderer.enabled = true;
            var surface = Ensure<NavMeshSurface>(surfaceObject);
            surface.collectObjects = CollectObjects.Children;
            surface.layerMask = 1 << layer;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.overrideVoxelSize = true; surface.voxelSize = .05f;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("NavMesh bake returned no data.");
            if (!AssetDatabase.Contains(surface.navMeshData))
                AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath(Generated+"Roompresent-NavMesh.asset"));
            renderer.enabled = false; state.floorRenderers = new Renderer[]{renderer};
            EditorUtility.SetDirty(state);
            SaveWithoutCredential();
            AssetDatabase.SaveAssets();
            ValidatePaths();
            Selection.activeGameObject = content;
            var view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.LookAt(new Vector3(-.4f,.5f,-.3f),Quaternion.LookRotation(new Vector3(-.9f,-.85f,.5f),Vector3.up),8f,true);
                view.Repaint();
            }
            Report("PASS scene created using official SDK prefabs, original map bytes and GLB.\nA is presentation-table code (old E). B is left cart; C is right cart in owner reference.\nNavMesh baked from observed low horizontal scan faces.\nREGISTRATION NOT VERIFIED: independent Scaniverse/Immersal coordinates need matched landmarks. Runtime guidance remains gated.\nSee roompresent-paths.txt for actual path checks.\n");
            Debug.Log("[RoompresentSceneBuilder] PASS setup saved; scan-to-Immersal registration still unverified, runtime guidance gated.");
        }

        static void PlaceTarget(Transform root,string id,Vector3 position)
        {
            var t = root.Find("Navigation Target "+id);
            if (t == null) { var obj = Prefab(Samples+"Navigation/Navigation Target.prefab",root);obj.name="Navigation Target "+id;t=obj.transform;t.localPosition=position; }
            var target=t.GetComponent<IsNavigationTarget>(); target.targetName=id;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }

        static Mesh MakeFloor(Transform scan)
        {
            var vertices = new List<Vector3>(); var indices = new List<int>();
            foreach(var filter in scan.GetComponentsInChildren<MeshFilter>())
            {
                var source=filter.sharedMesh; var points=source.vertices; var faces=source.triangles;
                for(int i=0;i<faces.Length;i+=3)
                {
                    var a=scan.parent.InverseTransformPoint(filter.transform.TransformPoint(points[faces[i]]));
                    var b=scan.parent.InverseTransformPoint(filter.transform.TransformPoint(points[faces[i+1]]));
                    var c=scan.parent.InverseTransformPoint(filter.transform.TransformPoint(points[faces[i+2]]));
                    var normal=Vector3.Cross(b-a,c-a).normalized;
                    if(Mathf.Max(a.y,b.y,c.y)>.18f || Mathf.Min(a.y,b.y,c.y)<-.12f || Mathf.Abs(normal.y)<.90f)continue;
                    if(Mathf.Max((a-b).magnitude,Mathf.Max((b-c).magnitude,(a-c).magnitude))>1.5f)continue;
                    a.y=b.y=c.y=.015f; int first=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);
                    if(normal.y>0){indices.Add(first);indices.Add(first+1);indices.Add(first+2);}else{indices.Add(first);indices.Add(first+2);indices.Add(first+1);}
                }
            }
            if(indices.Count<3)throw new InvalidOperationException("No supported floor faces; do not invent a whole-room floor.");
            var mesh=new Mesh{name="Roompresent observed walkable floor",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        static int EnsureNavigationLayer()
        {
            int layer=LayerMask.NameToLayer("Navigation");if(layer>=0)return layer;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=settings.FindProperty("layers");
            for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue="Navigation";settings.ApplyModifiedPropertiesWithoutUndo();return i;}
            throw new InvalidOperationException("No free layer for Navigation.");
        }

        public static void ValidatePaths()
        {
            RequireVenueScene();
            var targets=All<IsNavigationTarget>().Where(t=>t.transform.IsChildOf(All<RoompresentVenue>().Single().transform)).OrderBy(t=>t.targetName).ToArray();
            var lines=new List<string>{"EDITOR NAVMESH CHECK ONLY. No phone tracking or scan-to-Immersal registration claim."};
            foreach(var start in targets)foreach(var end in targets)
            {
                if(start==end)continue;
                var p=start.transform.position;var q=end.transform.position;p.y=q.y=.015f;
                bool startOk=NavMesh.SamplePosition(p,out var from,.95f,NavMesh.AllAreas);bool endOk=NavMesh.SamplePosition(q,out var to,.95f,NavMesh.AllAreas);
                var path=new NavMeshPath();bool complete=startOk&&endOk&&NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                bool sampledInside=complete;float largestDeviation=0;
                // NavMesh.Raycast can hit the exact boundary vertex of an otherwise valid corner.
                // Independently sample the rendered centreline every <=5cm with a stated 2cm tolerance.
                for(int i=1;i<path.corners.Length;i++)
                {
                    int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(path.corners[i-1],path.corners[i])/.05f));
                    for(int k=0;k<=steps;k++)
                    {
                        var point=Vector3.Lerp(path.corners[i-1],path.corners[i],(float)k/steps);
                        if(!NavMesh.SamplePosition(point,out var sample,.02f,NavMesh.AllAreas)){sampledInside=false;continue;}
                        largestDeviation=Mathf.Max(largestDeviation,Vector3.Distance(point,sample.position));
                    }
                }
                lines.Add(start.targetName+" -> "+end.targetName+": "+(complete?"COMPLETE":"FAIL")+" corners="+path.corners.Length+" centrelineSampledInside2cm="+sampledInside+" maxDeviation="+largestDeviation.ToString("F5")+" startSnap="+(startOk?Vector3.Distance(p,from.position).ToString("F3"):"none")+" endSnap="+(endOk?Vector3.Distance(q,to.position).ToString("F3"):"none"));
            }
            Directory.CreateDirectory("Logs/ImmersalVideoSetup");File.WriteAllLines("Logs/ImmersalVideoSetup/roompresent-paths.txt",lines);
        }

        public static void SaveWithoutCredential()
        {
            var sdk=All<ImmersalSDK>().Single();Directory.CreateDirectory("Library/ImmersalVideoSetup");
            if(!string.IsNullOrEmpty(sdk.developerToken))File.WriteAllText(LocalToken,sdk.developerToken);
            sdk.developerToken="";PrefabUtility.RecordPrefabInstancePropertyModifications(sdk);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new IOException("Scene save failed.");
        }
        static void Report(string text){Directory.CreateDirectory("Logs/ImmersalVideoSetup");File.WriteAllText("Logs/ImmersalVideoSetup/roompresent-setup.txt",text);}
    }
}
#endif
