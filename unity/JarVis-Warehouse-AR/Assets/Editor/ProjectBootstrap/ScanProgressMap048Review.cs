#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
namespace ProjectBootstrap
{
    public static class ScanProgressMap048Review
    {
        const string Out=@"C:\Users\Windows\Documents\ChatGPT\jarvis\scan-progress-map-20261123";
        const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
        const BindingFlags Static=BindingFlags.NonPublic|BindingFlags.Static;
        static readonly List<string> log=new List<string>();
        static void Check(bool ok,string text){log.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
        static void Set(object o,string n,object value)=>o.GetType().GetField(n,Private).SetValue(o,value);
        static object Call(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Private).Invoke(o,a);
        static string Hash(string p)=>BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(p))).Replace("-","");
        public static void Run()
        {
            log.Clear();Directory.CreateDirectory(Out);
            var guards=new[]{"Assets/Resources/RoompresentCheckedRegistration.json","Assets/Scenes/Roompresent_VPS.unity","Assets/Scenes/JarVisFigmaApp.unity","Assets/JarVis/Scripts/Navigation/SmallRoomAR.cs","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryVerification.cs"};
            var hashes=guards.ToDictionary(p=>p,Hash);
            string theme=PlayerPrefs.HasKey("JarVis.Theme")?PlayerPrefs.GetString("JarVis.Theme"):null;
            var language=JarvisLocalization.CurrentLanguage;
            var gate=(RoompresentBarcodeGate)typeof(RoompresentDeliveryMission).GetField("barcodeGate",Static).GetValue(null);
            var gateValues=new[]{"a1","a2","acknowledged"}.ToDictionary(n=>n,n=>(bool)gate.GetType().GetField(n,Private).GetValue(gate));
            var armed=typeof(RoompresentDeliveryMission).GetField("<Armed>k__BackingField",Static);bool originalArmed=(bool)armed.GetValue(null);
            try
            {
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();guide.surface.RemoveData();guide.surface.AddData();
                var mesh=NavMesh.CalculateTriangulation();Check(mesh.vertices.Length>0,"Saved Roompresent navigation geometry exists");
                Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
                foreach(var v in mesh.vertices){var p=guide.mapRoot.InverseTransformPoint(v);min=Vector2.Min(min,new Vector2(p.x,p.z));max=Vector2.Max(max,new Vector2(p.x,p.z));}
                var projection=new RoompresentMapProjection{center=(min+max)*.5f,scale=Mathf.Min(304/Mathf.Max(.1f,max.x-min.x),400/Mathf.Max(.1f,max.y-min.y)),a=guide.startLocal,b=guide.basinAccess,c=guide.bagAccess,registrationSha256=hashes[guards[0]]};
                var map=RoompresentMap2D.Create(guide);Check(map!=null,"Create actual saved-route map, not fabricated routes");
                File.WriteAllBytes("Assets/Resources/RoompresentRoute2D.bytes",map.EncodeToPNG());File.WriteAllBytes(Out+"/map-2d.png",map.EncodeToPNG());UnityEngine.Object.DestroyImmediate(map);
                File.WriteAllText("Assets/Resources/RoompresentMapProjection.json",JsonUtility.ToJson(projection,true));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach(var pos in new[]{projection.a,projection.b,projection.c}){var p=projection.Project(pos);Check(p.x>16&&p.x<368&&p.y>16&&p.y<496,"Saved waypoint projects inside packaged schematic: "+pos);}
                gate.Reset();armed.SetValue(null,true);Check(RoompresentDeliveryMission.ScanReminder(false)==null,"No reminder invented from saved progress");
                Check(!gate.Observe(RoompresentDeliveryRecord.A1,"Manual"),"Manual input does not create fresh book proof");
                Check(RoompresentDeliveryMission.ScanReminder(false)==null,"Manual input does not create a scanned reminder");
                gate.Observe(RoompresentDeliveryRecord.A1,"RearCamera");Check(RoompresentDeliveryMission.ScanReminder(false).Contains("A1 · 1/2")&&RoompresentDeliveryMission.ScanReminder(false).Contains("A2"),"A1 reminder and remaining A2");
                gate.Reset();gate.Observe(RoompresentDeliveryRecord.A2,"RearCamera");Check(RoompresentDeliveryMission.ScanReminder(false).Contains("A2 · 1/2"),"A2-first reminder works");
                gate.Observe(RoompresentDeliveryRecord.A1,"RearCamera");Check(RoompresentDeliveryMission.ScanReminder(false).Contains("2/2")&&!gate.Ready,"Two scanned still require popup OK");gate.Acknowledge();Check(gate.Ready,"Existing popup acknowledgement gate retained");
                var chat=typeof(OmsMobileApi).GetMethod("ChatJson",Static);
                foreach(bool en in new[]{false,true})
                {
                    string q=en?"Where is this product and how many are in stock?":"Sản phẩm này ở đâu và số lượng tồn kho là bao nhiêu?";
                    string json=(string)chat.Invoke(null,new object[]{q,new byte[]{1,2,3},"4710043004342"});var body=JsonUtility.FromJson<Request>(json);
                    Check(body.message.Contains("stockQuantity=1")&&body.message.Contains("location=B")&&body.message.Contains("FreshDelight"),"Photo request carries verified product, location B and declared stock 1 "+en);
                    Check(body.message.Length<=1000&&body.images.Length==1,"Product context fits server contract and retains photo "+en);
                    body=JsonUtility.FromJson<Request>((string)chat.Invoke(null,new object[]{q,null,"4710043004342"}));Check(body.message.Contains("stockQuantity=1"),"Follow-up without photo retains product record "+en);
                    body=JsonUtility.FromJson<Request>((string)chat.Invoke(null,new object[]{q,null,"9999999999999"}));Check(!body.message.Contains("FreshDelight")&&!body.message.Contains("stockQuantity"),"Unknown code cannot inherit the milk record "+en);
                }
                Check(JarvisTestProduct.Milk.stockQuantity==1&&JarvisTestProduct.Describe(false).Contains("Số lượng tồn kho: 1"),"Single owner-authorized stock value for detail and AI");
                var current=typeof(RoompresentDeliveryMission).GetField("current",Static);var original=current.GetValue(null);
                try
                {
                    Check(JarvisLocalCatalogue.Items.Length==3&&JarvisLocalCatalogue.Items.Select(p=>p.barcode).Distinct().Count()==3,"Three distinct catalogue barcodes");
                    var a1=JarvisLocalCatalogue.Find(RoompresentDeliveryRecord.A1);var a2=JarvisLocalCatalogue.Find(RoompresentDeliveryRecord.A2);var milk=JarvisLocalCatalogue.Find("4710043004342");
                    Check(a1.name.Contains("Vũ trụ")&&a2.color.Contains("Cam"),"Owner confirmed 610 = space A1, 603 = orange hotpot A2");
                    foreach(var p in JarvisLocalCatalogue.Items)Check(p.quantity==1&&p.lot.StartsWith("DEMO-")&&Resources.Load<Texture2D>(p.image)!=null,"Quantity 1, sample lot and actual supplied photo: "+p.code);
                    foreach(var record in new[]{new RoompresentDeliveryRecord(),new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true}})
                    {current.SetValue(null,record);Check(JarvisLocalCatalogue.Location(a1,false)=="Chưa cập nhật"&&JarvisLocalCatalogue.Location(a2,false)=="Chưa cập nhật","Incomplete mission does not publish book shelf locations");Check(JarvisLocalCatalogue.Location(milk,false)=="B","Milk remains at B");}
                    current.SetValue(null,new RoompresentDeliveryRecord{accepted=true,scannedA1=true,scannedA2=true,deliveredB=true,deliveredC=true});
                    Check(JarvisLocalCatalogue.Location(a1,false)=="B"&&JarvisLocalCatalogue.Location(a2,false)=="C","Completed mission publishes A1 B and A2 C");
                    current.SetValue(null,new RoompresentDeliveryRecord());Check(JarvisLocalCatalogue.Location(a1,false)=="Chưa cập nhật","Reset fixture clears published book position");
                    foreach(bool incoming in new[]{true,false})File.WriteAllBytes(Out+(incoming?"/inbound.xlsx":"/outbound.xlsx"),WarehouseExcelExport.CreateWorkbook(incoming,new[]{new[]{"Ghi chú","=1+1"}}));
                    Check(true,"Inbound/outbound XLSX generated; formula-looking note is encoded as text");
                }
                finally{current.SetValue(null,original);}
                foreach(string mode in new[]{"light","dark"})
                {
                    PlayerPrefs.SetString("JarVis.Theme",mode);JarvisLocalization.SetLanguage(SupportedLanguage.Vi);
                    EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
                    var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);nav.Initialize();var font=nav.GetComponentInChildren<TMP_Text>(true).font;
                    var settings=nav.gameObject.AddComponent<ImportedSettings>();Set(settings,"nav",nav);Set(settings,"font",font);typeof(ImportedFigmaNavigation).GetField("<Settings>k__BackingField",Private).SetValue(nav,settings);
                    var canvas=nav.GetComponentInParent<Canvas>();
                    WarehouseExcelExport.Attach(nav);
                    nav.Show("5:780",false);var stock=nav.screens.Single(s=>s.id=="5:780").root;var inventory=stock.AddComponent<ImportedWarehouseLists>();inventory.Configure(nav,false);Call(inventory,"Render");
                    Check(stock.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Vũ trụ"))&&stock.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("Lẩu Capybara")),"Actual supplied products appear on inventory screen "+mode);
                    Render(canvas,1080,2400,Out+"/inventory-"+mode+".png");
                    var mapRoot=nav.screens.Single(s=>s.id=="215:2").root;var screen=mapRoot.AddComponent<WarehouseMapScreen>();screen.Configure(nav);Call(screen,"Render");nav.Show("215:2",false);
                    var overlay=mapRoot.GetComponentInChildren<RoompresentMapPositionOverlay>();Check(overlay!=null&&overlay.PositionState=="DEFAULT_A"&&overlay.LocalPosition==projection.a,"Map tab displays explicit default A without claiming native tracking "+mode);
                    foreach(int h in new[]{2400,1920})Render(canvas,1080,h,Out+"/map-"+mode+"-"+h+".png");
                    nav.Show("5:331",false);var root=nav.screens.Single(s=>s.id=="5:331").root;
                    var scanner=root.GetComponent<ImportedCameraScanner>()??root.AddComponent<ImportedCameraScanner>();scanner.Configure(root);
                    var presentation=root.AddComponent<PrototypeScannerPresentation>();presentation.Configure(nav);gate.Reset();gate.Observe(RoompresentDeliveryRecord.A1,"RearCamera");armed.SetValue(null,true);presentation.RefreshScanReminder();
                    var reminder=root.GetComponentsInChildren<TMP_Text>().Single(t=>t.name=="Scanned books reminder");Check(reminder.text.Contains("Đã quét quyển vở A1")&&reminder.fontStyle==FontStyles.Bold,"Persistent scanned A1 label uses existing font in "+mode);
                    foreach(int h in new[]{2400,1920})Render(canvas,1080,h,Out+"/scanner-"+mode+"-"+h+".png");
                    if(mode=="light")
                    {
                        JarvisLabMission.ShowProduct(nav);Render(canvas,1080,2400,Out+"/product-stock-1.png");
                        foreach(var id in new[]{"5:935","14:387"})
                        {
                            nav.Show(id,false);var form=nav.screens.Single(s=>s.id==id).root;
                            Check(form.GetComponent<WarehouseExcelExport>()!=null&&form.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="Xuất sang Excel"),"Existing transaction form has Excel action "+id);
                            File.WriteAllLines(Out+"/form-"+id.Replace(":","-")+".txt",form.GetComponentsInChildren<TMP_Text>().Select(t=>t.name+"|"+t.text+"|"+t.rectTransform.anchoredPosition+"|"+t.rectTransform.sizeDelta+"|parent="+t.transform.parent.name));
                            Render(canvas,1080,2400,Out+"/form-"+id.Replace(":","-")+".png");
                        }
                    }
                }
                foreach(var p in guards)Check(Hash(p)==hashes[p],"Preserved "+Path.GetFileName(p));
                log.Add("LIMIT: Editor UI, map projection and request payload tests. Actual Samsung QR A alignment/live 2D pose and generated AI reply require device acceptance. No OMS stock write.");
                File.WriteAllLines(Out+"/verification.txt",log);Debug.Log("[ScanProgressMap048Review] PASS");
            }
            catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines(Out+"/verification.txt",log);throw;}
            finally{armed.SetValue(null,originalArmed);foreach(var p in gateValues)Set(gate,p.Key,p.Value);JarvisLocalization.SetLanguage(language);if(theme==null)PlayerPrefs.DeleteKey("JarVis.Theme");else PlayerPrefs.SetString("JarVis.Theme",theme);}
        }
        [Serializable] sealed class Request{public string message;public OmsMobileApi.ChatImage[] images;}
        static void Render(Canvas canvas,int w,int h,string path)
        {
            var go=new GameObject("Editor UI capture camera",typeof(Camera));var camera=go.GetComponent<Camera>();var rt=new RenderTexture(w,h,24);rt.Create();var previous=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
            try
            {
                foreach(var t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=LayerMask.NameToLayer("UI");camera.cullingMask=1<<LayerMask.NameToLayer("UI");camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=ImportedSettings.CurrentDark?PrototypeUi.Hex("101a29"):Color.white;camera.orthographic=true;camera.nearClipPlane=.1f;camera.farClipPlane=10;camera.transform.position=new Vector3(0,0,-5);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                Canvas.ForceUpdateCanvases();foreach(var v in canvas.GetComponentsInChildren<ImportedFigmaViewport>(true))v.Fit();Canvas.ForceUpdateCanvases();foreach(var s in canvas.GetComponentsInChildren<PrototypeGlassShadow>(true))Call(s,"LateUpdate");foreach(var s in canvas.GetComponentsInChildren<UniformUiSurface>(true))Call(s,"LateUpdate");Canvas.ForceUpdateCanvases();
                foreach(var t in canvas.GetComponentsInChildren<TMP_Text>().Where(t=>t.name=="Scanned books reminder"||t.transform.parent?.name=="Roompresent 2D map"||(t.transform.parent?.name??"").StartsWith("Catalogue "))){t.ForceMeshUpdate();Check(!t.isTextOverflowing,"New label fits "+h+": "+t.text);}
                camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;canvas.worldCamera=null;UnityEngine.Object.DestroyImmediate(tex);camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        }
        public static void Build(){Run();AndroidBuild.BuildApk();}
    }
}
#endif
