#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Immersal.XR;
using JarVis.Warehouse.Navigation;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    // Three explicitly corresponding landmarks, not guessed coordinate offsets.
    public sealed class RoompresentRegistrationWindow : EditorWindow
    {
        Vector3[] mapPoints = new Vector3[3];
        Vector3[] scanPoints = new Vector3[3];
        bool[] mapPicked = new bool[3], scanPicked = new bool[3];
        int picking=-1;
        bool pickingMap;
        Vector3[] cloud;
        string result="Pick the same three fixed physical landmarks in both scans. Use widely spaced corners, not movable book carts.";
        [MenuItem("Tools/JarVis/Immersal/08 Register Roompresent Scans")]
        public static void Open(){GetWindow<RoompresentRegistrationWindow>("Roompresent registration");}
        void OnEnable(){SceneView.duringSceneGui+=SceneGui;}
        void OnDisable(){SceneView.duringSceneGui-=SceneGui;}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("Scaniverse and Immersal are separate coordinate systems. A/B/C are already placed in the room scan. This step aligns the localization map to that room.",MessageType.Info);
            for(int i=0;i<3;i++)
            {
                EditorGUILayout.LabelField("Matching landmark "+(i+1),EditorStyles.boldLabel);
                mapPoints[i]=EditorGUILayout.Vector3Field("Immersal map local",mapPoints[i]);
                scanPoints[i]=EditorGUILayout.Vector3Field("Room / XRSpace local",scanPoints[i]);
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button((mapPicked[i]?"Re-pick":"Pick")+" Immersal "+(i+1))){picking=i;pickingMap=true;SceneView.RepaintAll();}
                if(GUILayout.Button((scanPicked[i]?"Re-pick":"Pick")+" Scaniverse "+(i+1))){picking=i;pickingMap=false;SceneView.RepaintAll();}
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox(result,MessageType.None);
            if(GUILayout.Button("Fit and report residuals (keeps AR unverified)"))Fit();
            if(GUILayout.Button("Show room and navigation preview"))RoompresentSceneBuilder.Preview();
            EditorGUILayout.HelpBox("After fitting, check independent landmarks and real phone tracking. The Alignment Verified field stays off until those checks are completed. Fitting three points alone is not an accuracy guarantee.",MessageType.Warning);
        }
        void SceneGui(SceneView view)
        {
            var map=FindFirstObjectByType<XRMap>();var venue=FindFirstObjectByType<RoompresentVenue>();
            if(map==null||venue==null)return;
            for(int i=0;i<3;i++)
            {
                Handles.color=Color.yellow;if(mapPicked[i])Handles.Label(map.transform.TransformPoint(mapPoints[i]),"Map "+(i+1));
                Handles.color=Color.cyan;if(scanPicked[i])Handles.Label(venue.transform.TransformPoint(scanPoints[i]),"Room "+(i+1));
            }
            if(picking<0)return;
            Handles.BeginGUI();GUI.Box(new Rect(10,38,510,34),"Click "+(pickingMap?"an Immersal point":"the Scaniverse surface")+" for landmark "+(picking+1)+". Esc cancels.");Handles.EndGUI();
            var e=Event.current;if(e.type==EventType.KeyDown&&e.keyCode==KeyCode.Escape){picking=-1;e.Use();return;}
            if(e.type!=EventType.MouseDown||e.button!=0||e.alt)return;
            if(pickingMap)
            {
                if(cloud==null)cloud=ReadCloud();float best=18f;int index=-1;
                for(int i=0;i<cloud.Length;i++)
                {
                    var world=map.transform.TransformPoint(cloud[i]);if(view.camera.WorldToViewportPoint(world).z<0)continue;
                    float distance=Vector2.Distance(HandleUtility.WorldToGUIPoint(world),e.mousePosition);if(distance<best){best=distance;index=i;}
                }
                if(index<0){result="No point within 18 pixels. Zoom in and click a visible cloud point.";Repaint();return;}
                mapPoints[picking]=cloud[index];mapPicked[picking]=true;
            }
            else
            {
                var ray=HandleUtility.GUIPointToWorldRay(e.mousePosition);bool found=false;RaycastHit best=default;
                foreach(var filter in venue.referenceScan.GetComponentsInChildren<MeshFilter>())
                {
                    var collider=filter.GetComponent<MeshCollider>();if(collider==null){collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;}
                    if(collider.Raycast(ray,out var hit,1000)&&(!found||hit.distance<best.distance)){best=hit;found=true;}
                }
                if(!found){result="No Scaniverse surface hit. Orbit to expose the landmark and try again.";Repaint();return;}
                scanPoints[picking]=venue.transform.InverseTransformPoint(best.point);scanPicked[picking]=true;
            }
            picking=-1;e.Use();Repaint();SceneView.RepaintAll();
        }
        static Vector3[] ReadCloud()
        {
            byte[] bytes=File.ReadAllBytes(RoompresentSceneBuilder.Data+"153613-Roompresent-sparse.ply");
            using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream))
            {
                int count=0;string line="";
                while(stream.Position<Math.Min(bytes.Length,4096))
                {
                    char ch=(char)reader.ReadByte();if(ch!='\n'){if(ch!='\r')line+=ch;continue;}
                    if(line.StartsWith("element vertex "))count=int.Parse(line.Substring(15));if(line=="end_header")break;line="";
                }
                var points=new Vector3[count];for(int i=0;i<count;i++){points[i]=new Vector3(-reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());reader.ReadBytes(3);}return points;
            }
        }
        void Fit()
        {
            try
            {
                if(mapPicked.Any(p=>!p)||scanPicked.Any(p=>!p))throw new InvalidOperationException("Pick all three matching pairs first; zero/default coordinates will not be used.");
                Vector3 u=mapPoints[1]-mapPoints[0],v=mapPoints[2]-mapPoints[0],x=scanPoints[1]-scanPoints[0],y=scanPoints[2]-scanPoints[0];
                if(Mathf.Min(u.magnitude,v.magnitude,x.magnitude,y.magnitude)<1f||Vector3.Cross(u.normalized,v.normalized).magnitude<.15f||Vector3.Cross(x.normalized,y.normalized).magnitude<.15f)throw new InvalidOperationException("Use three widely spaced, non-collinear landmarks at least one metre apart.");
                float scale=(x.magnitude/u.magnitude+y.magnitude/v.magnitude)*.5f;
                if(scale<.85f||scale>1.15f)throw new InvalidOperationException("Scale differs by more than 15%; check units and landmark correspondence.");
                var first=Quaternion.FromToRotation(u,x);var normal1=first*Vector3.Cross(u,v).normalized;var normal2=Vector3.Cross(x,y).normalized;
                var rotation=Quaternion.AngleAxis(Vector3.SignedAngle(normal1,normal2,x.normalized),x.normalized)*first;
                var offset=scanPoints[0]-rotation*(mapPoints[0]*scale);
                float[] errors=Enumerable.Range(0,3).Select(i=>Vector3.Distance(rotation*(mapPoints[i]*scale)+offset,scanPoints[i])).ToArray();
                if(errors.Max()>.15f)throw new InvalidOperationException("Landmark residual exceeds 15 cm; do not apply this fit. Residuals: "+string.Join(", ",errors.Select(n=>n.ToString("F3"))));
                var map=FindFirstObjectByType<XRMap>();var venue=FindFirstObjectByType<RoompresentVenue>();Undo.RecordObject(map.transform,"Register Roompresent scans");
                map.transform.localPosition=offset;map.transform.localRotation=rotation;map.transform.localScale=Vector3.one*scale;venue.alignmentVerified=false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(map.transform);RoompresentSceneBuilder.SaveWithoutCredential();
                result="Fit applied; AR remains UNVERIFIED. Residuals (m): "+string.Join(", ",errors.Select(n=>n.ToString("F3")))+". Scale: "+scale.ToString("F4");
                Directory.CreateDirectory("Logs/ImmersalVideoSetup");File.WriteAllText("Logs/ImmersalVideoSetup/roompresent-registration.txt",result);
            }
            catch(Exception e){result=e.Message;}
            Repaint();SceneView.RepaintAll();
        }
    }
}
#endif
