#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace ProjectBootstrap {
public static class InventoryBadgeReview {
 const string Out=@"C:\Users\Windows\Documents\ChatGPT\jarvis\inventory-polish-20261123";
 const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
 public static void Run(){
  var oldLanguage=JarvisLocalization.CurrentLanguage;string oldTheme=PlayerPrefs.GetString("JarVis.Theme","light");int oldSize=PlayerPrefs.GetInt("JarVis.TextSize",1);
  try{
   foreach(bool dark in new[]{false,true})foreach(bool en in new[]{false,true}){
    EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
    var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);nav.Initialize();
    var settings=nav.gameObject.AddComponent<ImportedSettings>();typeof(ImportedSettings).GetField("nav",Flags).SetValue(settings,nav);typeof(ImportedSettings).GetField("font",Flags).SetValue(settings,nav.GetComponentInChildren<TMP_Text>(true).font);typeof(ImportedFigmaNavigation).GetField("<Settings>k__BackingField",Flags).SetValue(nav,settings);
    PlayerPrefs.SetString("JarVis.Theme",dark?"dark":"light");JarvisLocalization.SetLanguage(en?SupportedLanguage.En:SupportedLanguage.Vi);
    foreach(int size in en?new[]{1,2}:new[]{1}){
     PlayerPrefs.SetInt("JarVis.TextSize",size);nav.Show("5:780",false);
     var root=nav.screens.Single(s=>s.id=="5:780").root;
     var screen=root.GetComponent<ImportedWarehouseLists>()??root.AddComponent<ImportedWarehouseLists>();screen.Configure(nav,false);
     typeof(ImportedWarehouseLists).GetMethod("Render",Flags).Invoke(screen,null);
     var canvas=nav.GetComponentInParent<Canvas>();string tag=(dark?"dark":"light")+(en?"-en":"-vi")+"-size"+size;
     Capture(canvas,Out+"/inventory-"+tag+".png",2400);
     foreach(var title in root.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Product title"))Debug.Log("[InventoryBadge] title="+title.text+" size="+title.fontSize+" lines="+title.textInfo.lineCount);
     if(size==1){
      Capture(canvas,Out+"/inventory-"+tag+"-1920.png",1920);
      foreach(var p in JarvisLocalCatalogue.Items){var popup=JarvisLocalCatalogue.Show(nav,p);Capture(canvas,Out+"/detail-"+p.code+"-"+tag+".png",2400);UnityEngine.Object.DestroyImmediate(popup);}
     }
    }
   }
  }finally{PlayerPrefs.SetString("JarVis.Theme",oldTheme);PlayerPrefs.SetInt("JarVis.TextSize",oldSize);JarvisLocalization.SetLanguage(oldLanguage);}
  Debug.Log("[InventoryBadge] CAPTURE COMPLETE");
 }
 public static void Build(){Run();AndroidBuild.BuildApk();}
 static void Capture(Canvas canvas,string file,int h){
  typeof(RoomFloorplan049Review).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{canvas,1080,h,file});
  foreach(var title in canvas.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Product title"||t.name=="Stock quantity")){
   title.ForceMeshUpdate();if(title.isTextOverflowing)throw new Exception("Inventory text overflow: "+title.text);
  }
 }
}}
#endif
