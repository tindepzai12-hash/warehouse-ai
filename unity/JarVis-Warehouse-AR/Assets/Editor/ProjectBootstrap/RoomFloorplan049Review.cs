#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
namespace ProjectBootstrap {
public static class RoomFloorplan049Review {
 const string Out=@"C:\Users\Windows\Documents\ChatGPT\jarvis\room-floorplan-20261123";
 [Serializable] public class Route {public string id;public Vector3[] points;}
 [Serializable] public class Geometry {public Vector3 a,b,c;public Vector3[] floor;public Route[] routes;public Vector3 meshMin,meshMax;}
 public static void ExportGeometry(){
  EditorSceneManager.OpenScene("Assets/Scenes/Roompresent_VPS.unity");
  var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
  guide.surface.AddData();var mesh=NavMesh.CalculateTriangulation();
  var geometry=new Geometry{a=guide.startLocal,b=guide.basinAccess,c=guide.bagAccess,floor=mesh.vertices.Select(v=>guide.mapRoot.InverseTransformPoint(v)).ToArray()};
  var routes=new List<Route>();
  foreach(var tuple in new[]{("AB",geometry.a,geometry.b),("AC",geometry.a,geometry.c),("BC",geometry.b,geometry.c)}){
   if(!NavMesh.SamplePosition(guide.mapRoot.TransformPoint(tuple.Item2),out var a,.85f,NavMesh.AllAreas)||!NavMesh.SamplePosition(guide.mapRoot.TransformPoint(tuple.Item3),out var b,.85f,NavMesh.AllAreas))throw new Exception("Missing access");
   var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new Exception("Incomplete "+tuple.Item1);
   routes.Add(new Route{id=tuple.Item1,points=path.corners.Select(v=>guide.mapRoot.InverseTransformPoint(v)).ToArray()});
  }geometry.routes=routes.ToArray();
  // Export imported mesh in the SAME map-local coordinates as the navigation, not an assumed glTF handedness.
  var venue=UnityEngine.Object.FindFirstObjectByType<RoompresentVenue>();
  var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(f=>f.sharedMesh!=null && f.sharedMesh.vertexCount==62930).ToArray(); if(filters.Length!=1)throw new Exception("Expected original scan mesh, got "+filters.Length);
  using(var writer=new BinaryWriter(File.Create(Out+"/unity-scan.bin"))){writer.Write(filters.Length);foreach(var f in filters){var vertices=f.sharedMesh.vertices;var faces=f.sharedMesh.triangles;writer.Write(vertices.Length);writer.Write(faces.Length);foreach(var v in vertices){var p=guide.mapRoot.InverseTransformPoint(f.transform.TransformPoint(v));writer.Write(p.x);writer.Write(p.y);writer.Write(p.z);}foreach(int i in faces)writer.Write(i);}}
  File.WriteAllText(Out+"/navigation-geometry.json",JsonUtility.ToJson(geometry,true));Debug.Log("[RoomFloorplan049] GEOMETRY EXPORTED");
 }
 static string Hash(string p)=>BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-","");
 static void Check(bool ok,string s){if(!ok)throw new Exception(s);Debug.Log("[RoomFloorplan049] PASS "+s);}
 public static void Run(){
  var guards=new[]{"Assets/Resources/RoompresentCheckedRegistration.json","Assets/Scenes/Roompresent_VPS.unity","Assets/Scenes/JarVisFigmaApp.unity","Assets/JarVis/Scripts/LabTesting/SmallRoomAR.cs","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryVerification.cs"};
  // SmallRoomAR lives under LabTesting in this project; resolve if source moved without writing it.
  guards=guards.Select(p=>File.Exists(p)?p:Directory.GetFiles("Assets","SmallRoomAR.cs",SearchOption.AllDirectories).Single()).ToArray();
  var hashes=guards.Select(Hash).ToArray();var projection=RoompresentMapProjection.Load();Check(projection!=null,"Projection loads");
  Check(projection.width==projection.height&&projection.width==1024,"Square 1024 map with uniform metric scale");
  var tex=RoompresentMap2D.LoadReference();Check(tex!=null&&tex.width==1024&&tex.height==1024,"Scan-derived square map loads");UnityEngine.Object.DestroyImmediate(tex);
  foreach(var point in new[]{projection.a,projection.b,projection.c}){var p=projection.Project(point);Check(p.x>40&&p.x<984&&p.y>40&&p.y<984,"Saved waypoint inside floorplan");}
  var language=JarvisLocalization.CurrentLanguage;string theme=PlayerPrefs.GetString("JarVis.Theme","light");
  try{
   EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);
   nav.Initialize(); var settings=nav.gameObject.AddComponent<ImportedSettings>(); var flags=BindingFlags.Instance|BindingFlags.NonPublic; typeof(ImportedSettings).GetField("nav",flags).SetValue(settings,nav); typeof(ImportedSettings).GetField("font",flags).SetValue(settings,nav.GetComponentInChildren<TMP_Text>(true).font);typeof(ImportedFigmaNavigation).GetField("<Settings>k__BackingField",flags).SetValue(nav,settings);
   foreach(bool dark in new[]{false,true}){
    PlayerPrefs.SetString("JarVis.Theme",dark?"dark":"light");
    JarvisLocalization.SetLanguage(SupportedLanguage.Vi);nav.Show("215:2",false);
    var root=nav.screens.Single(s=>s.id=="215:2").root;var screen=root.GetComponent<WarehouseMapScreen>()??root.AddComponent<WarehouseMapScreen>();screen.Configure(nav);
    typeof(WarehouseMapScreen).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(screen,null);
    var texts=screen.GetComponentsInChildren<TMP_Text>();Check(!texts.Any(t=>t.text.Contains("AR GUIDANCE")||t.text.Contains("HƯỚNG DẪN AR")||t.text.Contains("Map unavailable")||t.text.Contains("Roompresent")),"Map screen has no removed sections");
    Check(screen.GetComponentsInChildren<RawImage>().Single().rectTransform.sizeDelta==new Vector2(330,330),"Square view, no stretch");
    var overlay=screen.GetComponentInChildren<RoompresentMapPositionOverlay>();overlay.RefreshPosition();Check(overlay.PositionState=="DEFAULT_A","No invented tracking pose");
    foreach(int h in new[]{2400,1920})Capture(nav.GetComponentInParent<Canvas>(),1080,h,Out+"/map-"+(dark?"dark":"light")+"-"+h+".png");
   }
   foreach(bool dark in new[]{false,true})foreach(bool en in new[]{false,true}){
    PlayerPrefs.SetString("JarVis.Theme",dark?"dark":"light");JarvisLocalization.SetLanguage(en?SupportedLanguage.En:SupportedLanguage.Vi);
    string tag=(dark?"dark":"light")+(en?"-en":"-vi");
    var stockRoot=nav.screens.Single(s=>s.id=="5:780").root;nav.Show("5:780",false);
    var inventory=stockRoot.GetComponent<ImportedWarehouseLists>()??stockRoot.AddComponent<ImportedWarehouseLists>();inventory.Configure(nav,false);
    typeof(ImportedWarehouseLists).GetMethod("Render",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(inventory,null);
    Capture(nav.GetComponentInParent<Canvas>(),1080,2400,Out+"/inventory-"+tag+".png");
    var tasksRoot=nav.screens.Single(s=>s.id=="48:244").root;
    foreach(var product in JarvisLocalCatalogue.Items){
     var popup=JarvisLocalCatalogue.Show(nav,product);Capture(nav.GetComponentInParent<Canvas>(),1080,2400,Out+"/detail-"+product.code+"-"+tag+".png");UnityEngine.Object.DestroyImmediate(popup);
    }

    nav.Show("48:244",false);var tasks=tasksRoot.GetComponent<ImportedWarehouseLists>()??tasksRoot.AddComponent<ImportedWarehouseLists>();tasks.Configure(nav,true);
    var flags2=BindingFlags.Instance|BindingFlags.NonPublic;
    typeof(ImportedWarehouseLists).GetField("selected",flags2).SetValue(tasks,1);
    typeof(ImportedWarehouseLists).GetMethod("Render",flags2).Invoke(tasks,null);
    Capture(nav.GetComponentInParent<Canvas>(),1080,2400,Out+"/tasks-working-"+tag+".png");
    var filters=(RectTransform)typeof(ImportedWarehouseLists).GetField("filters",flags2).GetValue(tasks);
    filters.GetComponentInParent<ScrollRect>().horizontalNormalizedPosition=1;
    Capture(nav.GetComponentInParent<Canvas>(),1080,2400,Out+"/tasks-end-"+tag+".png");
   }  }finally{PlayerPrefs.SetString("JarVis.Theme",theme);JarvisLocalization.SetLanguage(language);}
  for(int i=0;i<guards.Length;i++)Check(Hash(guards[i])==hashes[i],"Untouched "+Path.GetFileName(guards[i]));
  var chat=typeof(JarVis.Warehouse.Backend.OmsMobileApi).GetMethod("ChatJson",BindingFlags.Static|BindingFlags.NonPublic);
  foreach(bool en in new[]{false,true})foreach(var product in JarvisLocalCatalogue.Items){
   var question=en?"Where is this product and how many are in stock?":"Sản phẩm này ở đâu và số lượng tồn kho là bao nhiêu?";
   var payload=(string)chat.Invoke(null,new object[]{question,null,product.barcode});
   var body=JsonUtility.FromJson<ChatCheck>(payload);Check(body.message.Length<=1000,"AI context within contract "+product.code+" "+en);
   Check(body.message.Contains("stockQuantity=1")&&body.message.Contains(en?"No generic confirmation":"không kết bằng yêu cầu xác nhận"),"AI knows exact product and answer style "+product.code+" "+en);
  }
  Debug.Log("[RoomFloorplan049] PASS ALL");
 }
 [Serializable] class ChatCheck{public string message;}
 static void Capture(Canvas canvas,int w,int h,string file){
  var go=new GameObject("Map capture",typeof(Camera));var cam=go.GetComponent<Camera>();var rt=new RenderTexture(w,h,24);rt.Create();var old=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
  try{foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");cam.cullingMask=1<<LayerMask.NameToLayer("UI");cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=ImportedSettings.CurrentDark?PrototypeUi.Hex("101a29"):Color.white;cam.orthographic=true;cam.nearClipPlane=.1f;cam.farClipPlane=10;cam.transform.position=new Vector3(0,0,-5);cam.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=1;
   Canvas.ForceUpdateCanvases();foreach(var v in canvas.GetComponentsInChildren<ImportedFigmaViewport>(true))v.Fit();Canvas.ForceUpdateCanvases();
   foreach(var t in canvas.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();if(t.transform.parent?.name=="Roompresent 2D map" || (t.transform.parent?.name??"").StartsWith("Catalogue ") || t.GetComponentInParent<PrototypeDialog>()!=null)Check(!t.isTextOverflowing,"Changed text fits "+h+" "+t.name);}
   cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());
  }finally{RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);cam.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
 }
 public static void Build(){Run();AndroidBuild.BuildApk();}
}}
#endif
