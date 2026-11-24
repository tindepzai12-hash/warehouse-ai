#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Immersal;
using Immersal.XR;
using Immersal.Samples.Navigation;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using Unity.AI.Navigation;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class RoompresentVpsBuild
    {
        public const string ScenePath="Assets/Scenes/Roompresent_VPS.unity";
        const string Request="Library/RoompresentVps.request";
        const string Logs="Logs/RoompresentVPS/";
        public const string Output="Builds/Android/JarVis-Warehouse-0.4.5.apk";
        static bool busy;
        static RoompresentVpsBuild(){EditorApplication.update+=Tick;EditorApplication.delayCall+=()=>Debug.Log("[RoompresentVPSBuild] CODE 045 READY");}
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static T[] All<T>()where T:Component=>SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        static void Tick()
        {
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Request)||(DateTime.UtcNow-File.GetLastWriteTimeUtc(Request)).TotalSeconds<8)return;
            string command=File.ReadAllText(Request).Trim();File.Move(Request,Request+".running-"+DateTime.UtcNow.Ticks);busy=true;
            try{Directory.CreateDirectory(Logs);if(command=="prepare")Prepare();else if(command=="review")Review();else if(command=="binary")VerifyMapBinary();else if(command=="build"||command=="build-035")Build();else throw new InvalidOperationException("Unknown VPS command");}
            catch(Exception e){File.WriteAllText(Logs+"failure.txt",e.ToString());Debug.LogError("[RoompresentVPSBuild] "+e);}
            finally{busy=false;}
        }
        static void PreserveDirty()
        {
            Directory.CreateDirectory("Library/RoompresentVpsBackups");
            for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isDirty)Check(EditorSceneManager.SaveScene(scene,"Library/RoompresentVpsBackups/"+scene.name+"-"+DateTime.UtcNow.Ticks+".unity",true),"Could not preserve scene edits");}
        }
        static string Hash(string path){using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static void EnsureScenes()
        {
            var required=new[]{"Assets/Scenes/JarVisFigmaApp.unity","Assets/Scenes/SplashScene.unity","Assets/Scenes/LoginScene.unity","Assets/Scenes/WarehouseScene.unity","Assets/Scenes/AtlasAR.unity",ImmersalVideoSetup.ScenePath,SmallRoomSceneBuild.ScenePath,RoompresentQuickBuild.ScenePath,ScenePath};
            foreach(var path in required)Check(File.Exists(path),"Missing full JarVis scene "+path);
            var extra=EditorBuildSettings.scenes.Where(s=>!required.Contains(s.path)).ToArray();
            EditorBuildSettings.scenes=required.Select(path=>new EditorBuildSettingsScene(path,true)).Concat(extra).ToArray();
        }
        static void ConfigureSession()
        {
            var session=All<ImmersalSession>().Single();
            var serialized=new SerializedObject(session);
            serialized.FindProperty("m_SessionUpdateInterval").floatValue=.7f;
            serialized.FindProperty("m_BurstMode").boolValue=false;
            serialized.FindProperty("m_ResetOnPause").boolValue=false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(session);
            var camera=All<ARCameraManager>().Single();camera.autoFocusRequested=false;EditorUtility.SetDirty(camera);
            var sdk=All<ImmersalSDK>().Single();var sdkState=new SerializedObject(sdk);sdkState.FindProperty("m_Downsample").boolValue=false;sdkState.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(sdk);EditorUtility.SetDirty(sdk);
        }
        [MenuItem("Tools/JarVis/Roompresent VPS/01 Prepare Real Localization")]
        public static void Prepare()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorUtility.scriptCompilationFailed,"Compile and stop Play first");PreserveDirty();Directory.CreateDirectory(Logs);
            if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);ConfigureSession();Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()),"Could not save session settings");EnsureScenes();Review();return;}
            var original=EditorSceneManager.OpenScene(RoompresentQuickBuild.ScenePath,OpenSceneMode.Single);
            var template=All<SmallRoomAR>().Single();
            var positions=new[]{template.startLocal,template.basinLocal,template.bagLocal,template.basinAccess,template.bagAccess};
            var tempMap=UnityEngine.Object.Instantiate(template.mapRoot.gameObject);tempMap.hideFlags=HideFlags.HideAndDontSave;
            var pinPoints=template.pins.GetComponentsInChildren<IsNavigationTarget>(true).ToDictionary(t=>t.targetName,t=>template.mapRoot.InverseTransformPoint(t.transform.position)-Vector3.up*.12f);
            // A separate scene keeps all original sample and quick-test assets intact.
            Check(AssetDatabase.CopyAsset(ImmersalVideoSetup.ScenePath,ScenePath),"Could not copy existing SDK scene");
            // Move the temporary copy out before closing its source scene.
            var staging=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.MoveGameObjectToScene(tempMap,staging);
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);EditorSceneManager.CloseScene(original,true);SceneManager.SetActiveScene(scene);
            var sdk=All<ImmersalSDK>().Single();sdk.developerToken="";sdk.targetFrameRate=30;
            var xr=All<XRSpace>().Single();xr.ProcessPoses=false;
            foreach(var old in All<RoompresentVenue>())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(var canvas in All<Canvas>())UnityEngine.Object.DestroyImmediate(canvas.gameObject);
            foreach(var nav in All<NavigationManager>())UnityEngine.Object.DestroyImmediate(nav.gameObject);
            var map=All<XRMap>().Single();map.transform.localPosition=Vector3.zero;map.transform.localRotation=Quaternion.identity;map.transform.localScale=Vector3.one;
            var method=All<DeviceLocalization>().Single();map.LocalizationMethod=method;
            map.MapOptions.Clear();map.MapOptions.Add(new MapLoadingOption{m_SerializedDataSource=0,DownloadVisualizationAtRuntime=false});map.SerializeMapOptions();
            var localizer=All<Localizer>().Single();var localizerState=new SerializedObject(localizer);var methods=localizerState.FindProperty("m_LocalizationMethodObjects");methods.arraySize=1;methods.GetArrayElementAtIndex(0).objectReferenceValue=method;localizerState.ApplyModifiedPropertiesWithoutUndo();
            ConfigureSession();
            var camera=All<ARCameraManager>().Single();
            var vps=sdk.gameObject.AddComponent<RoompresentVpsLocalization>();vps.sdk=sdk;vps.nativeUpdater=All<SceneUpdater>().Single();vps.cameraManager=camera;vps.mapFile=map.mapFile;
            vps.mapFingerprint=Hash(RoompresentSceneBuilder.Data+"153613-Roompresent.bytes");vps.scanFingerprint=Hash(RoompresentSceneBuilder.Data+"Scaniverse 2026-11-19 153704.glb");
            var sdkState=new SerializedObject(sdk);sdkState.FindProperty("m_SceneUpdater").objectReferenceValue=vps;sdkState.ApplyModifiedPropertiesWithoutUndo();
            var mapRoot=UnityEngine.Object.Instantiate(tempMap,xr.transform,false);mapRoot.name="Roompresent content in VPS coordinates";mapRoot.hideFlags=HideFlags.None;mapRoot.transform.localPosition=Vector3.zero;mapRoot.transform.localRotation=Quaternion.identity;mapRoot.transform.localScale=Vector3.one;
            UnityEngine.Object.DestroyImmediate(tempMap);EditorSceneManager.CloseScene(staging,true);SceneManager.SetActiveScene(scene);
            var guideObject=new GameObject("Roompresent VPS guidance");var guide=guideObject.AddComponent<SmallRoomAR>();guide.presentationRoom=true;guide.localization=vps;guide.mapRoot=mapRoot.transform;
            guide.startLocal=positions[0];guide.basinLocal=positions[1];guide.bagLocal=positions[2];guide.basinAccess=positions[3];guide.bagAccess=positions[4];guide.registrationLandmarks=new[]{pinPoints["A"],pinPoints["B"],pinPoints["C"]};
            guide.pins=mapRoot.transform.Find("Navigation Targets A B C");guide.surface=mapRoot.GetComponentInChildren<NavMeshSurface>(true);
            var pathPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/Immersal SDK/2.5.0/Core Samples/Prefabs/Navigation/Navigation Path.prefab");var arrows=(GameObject)PrefabUtility.InstantiatePrefab(pathPrefab,guideObject.transform);guide.path=arrows.GetComponent<NavigationPath>();guide.path.pathWidth=.22f;PrefabUtility.RecordPrefabInstancePropertyModifications(guide.path);
            Check(EditorSceneManager.SaveScene(scene,ScenePath),"Could not save VPS scene");
            EnsureScenes();AssetDatabase.SaveAssets();Review();
            Debug.Log("[RoompresentVPSBuild] PREPARED embedded map 153613, device localization, no start-at-A pose");
        }
        [MenuItem("Tools/JarVis/Roompresent VPS/02 Review Localization")]
        public static void Review()
        {
            Directory.CreateDirectory(Logs);Check(SceneManager.GetActiveScene().path==ScenePath,"Open Roompresent VPS scene");
            var sdk=All<ImmersalSDK>().Single();var vps=All<RoompresentVpsLocalization>().Single();var map=All<XRMap>().Single();var guide=All<SmallRoomAR>().Single();
            var session=All<ImmersalSession>().Single();Check(Mathf.Abs(session.SessionUpdateInterval-.7f)<.001f&&!session.BurstMode,"Session cadence was not persisted");
            Check(!All<ARCameraManager>().Single().autoFocusRequested,"Use ARCore fixed focus for room tracking");
            Check(!sdk.downsample,"Preserve full camera detail for room recognition");
            Check(sdk.SceneUpdater==vps&&vps.nativeUpdater!=null&&vps.cameraManager!=null&&guide.localization==vps&&guide.presentationRoom,"Incorrect pose ownership");
            Check(map.mapId==153613&&map.IsConfigured&&map.mapFile!=null&&map.LocalizationMethod is DeviceLocalization,"Real embedded Roompresent map required");
            Check(vps.mapFingerprint==Hash(AssetDatabase.GetAssetPath(map.mapFile)),"Map content fingerprint mismatch");
            var loading=map.MapOptions.OfType<MapLoadingOption>().Single();Check(loading.m_SerializedDataSource==0&&!loading.DownloadVisualizationAtRuntime,"Localization must use embedded bytes, not a network download");
            Check(sdk.Localizer.AvailableLocalizationMethods.Length==1&&sdk.Localizer.AvailableLocalizationMethods[0] is DeviceLocalization,"Only on-device localization allowed for this scene");
            Check(All<ARCameraManager>().Length==1&&All<ARCameraBackground>().Length==1&&All<XROrigin>().Length==1&&All<ARSession>().Length==1&&All<XRSpace>().Length==1,"Duplicate AR/camera owners");
            Check(guide.mapRoot.IsChildOf(All<XRSpace>().Single().transform)&&!guide.mapRoot.IsChildOf(vps.cameraManager.transform)&&All<RoompresentVenue>().Length==0,"Content must belong to spatial map, not camera or old calibration owner");
            Check(guide.registrationLandmarks.Length==3&&guide.surface!=null&&guide.path!=null&&guide.pins!=null,"Missing registration/path references");
            Check(guide.mapRoot.GetComponentInParent<XRSpace>()!=null&&vps.cameraManager.GetComponentInParent<XROrigin>()!=null,"Installer cannot resolve spatial/camera owners");
            VerifyGate();VerifyRegistration(guide,vps);VerifyFixedA(guide,vps);DeliveryWorkflowReview.Run(guide,vps);
            File.WriteAllText(Logs+"configuration.txt","PASS genuine embedded Roompresent 153613; exact bytes fingerprint; only DeviceLocalization; ARCore camera intrinsics via official platform; three consistent map-to-tracker poses; unique camera/space; no automatic A pose; on-device recovery; registration gate remains closed without checked landmarks.\nPhysical Android localization and shelf alignment pending.\n");Debug.Log("[RoompresentVPSBuild] REVIEW PASS");
        }
        static void VerifyGate()
        {
            var gate=new RoompresentPoseGate();var q=Quaternion.Euler(0,37,0);var p=new Vector3(3,-1,2);
            Check(!gate.Observe(153613,p,q,0)&&!gate.Observe(153613,p,q,1)&&gate.Observe(153613,p,q,2)&&gate.Fresh(3),"Stable map pose not acquired");
            Check(!gate.Observe(153613,p+Vector3.right,q,3)&&!gate.Fresh(3),"Jump kept stale guidance");
            Check(!gate.Observe(153613,p+Vector3.right,q,4)&&gate.Observe(153613,p+Vector3.right,q,5),"Relocalization did not recover");
            Check(!gate.Fresh(14),"Expired pose accepted");gate.Invalidate();Check(!gate.Fresh(5),"Pause retained trusted pose");
            Check(!gate.Observe(153613,p,q,20)&&!gate.Observe(153613,p,q,34)&&gate.Observe(153613,p,q,48),"Sparse consistent fixes failed acquisition");
            Check(gate.Acquired&&!gate.Fresh(60)&&gate.Observe(153613,p,q,100),"Continuous VIO lost its map frame merely because VPS was sparse");
            gate.Invalidate();Check(!gate.Acquired,"Tracking interruption retained map trust");
            // Replay the measured strong first view and weak 180-degree lookalikes.
            var fieldGate=new RoompresentPoseGate();var first=new Vector3(-1.768f,1.170f,.775f);var facing=Quaternion.Euler(0,102,0);
            Check(!fieldGate.Observe(153613,first,facing,0,23,.874)&&!fieldGate.Observe(153613,first+new Vector3(-.049f,-.012f,.107f),facing,4,21,1.103),"Field acquisition started incorrectly");
            foreach(float time in new[]{35f,37f,39f})Check(!fieldGate.Observe(153613,new Vector3(2.1f,1.1f,3.9f),Quaternion.Euler(0,283,0),time,13,1.5),"Weak opposite-facing match accepted");
            Check(fieldGate.Consistent==2&&fieldGate.Observe(153613,first,facing,55,23,1.1),"Weak lookalike replaced the strong map candidate");
            Check(!fieldGate.Observe(153613,first,facing,56,30,5.2)&&fieldGate.Acquired,"Poor reprojection invalidated the valid map frame");
            Check(!gate.Observe(1,p,q,6)&&!gate.Observe(153613,new Vector3(float.NaN,0,0),q,7)&&!gate.Observe(153613,p,new Quaternion(0,0,0,0),8),"Invalid map/pose accepted");
            File.WriteAllText(Logs+"pose-gate.txt","PASS stable and sparse acquisition, jump rejection, automatic reacquisition, freshness expiry, continuous-VIO map retention, tracking/pause invalidation, wrong map, NaN and invalid quaternion rejection.\nThis tests pose policy, not physical localization accuracy.\n");
        }
        static void VerifyMapBinary()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Native map check is Editor-only");
            bool wasLoaded=MapHandleMapping.TryGetHandle(153613,out _);
            try
            {
                int handle=Core.LoadMap(153613,File.ReadAllBytes(RoompresentSceneBuilder.Data+"153613-Roompresent.bytes"));
                Check(handle>=0,"Native SDK rejected the actual Roompresent binary");int points=Core.GetPointCloudSize(153613);Check(points>100,"Native map has insufficient decoded points");
                File.WriteAllText(Logs+"map-binary.txt","PASS actual map binary loaded by installed native Immersal plugin; points="+points+"\nThis verifies file compatibility, not Samsung camera localization.\n");Debug.Log("[RoompresentVPSBuild] NATIVE MAP PASS points="+points);
            }
            finally{if(!wasLoaded)Core.FreeMap(153613);}
        }
        static void VerifyFixedA(SmallRoomAR guide,RoompresentVpsLocalization vps)
        {
            foreach(float yaw in new[]{0f,90f,179f,-120f})
            {
                var q=Quaternion.Euler(0,yaw,0);var offset=new Vector3(2,.8f,-1);
                var bl=offset+q*new Vector3(-.05f,0,-.05f);var tl=offset+q*new Vector3(-.05f,0,.05f);var tr=offset+q*new Vector3(.05f,0,.05f);
                Check(RoompresentQrLocalization.TryPose(bl,tl,tr,out var pose)&&Vector3.Distance(pose.position,offset)<.001f&&Quaternion.Angle(pose.rotation,q)<.01f,"Fixed A metric pose/yaw");
                Check(!RoompresentQrLocalization.TryPose(tr,tl,bl,out _),"Mirrored A pose accepted");
            }
            Check(!RoompresentQrLocalization.TryPose(Vector3.zero,Vector3.up,Vector3.one,out _)&&!RoompresentQrLocalization.IsStartQr("4714551570610"),"Product or invalid plane used as A");
            Check(RoompresentQrLocalization.MatchesPoint("JWM:MAIN:B","B")&&RoompresentQrLocalization.MatchesPoint("JWM:MAIN:C","C"),"Owner B/C cards must scan during setup");
            Check(!RoompresentQrLocalization.MatchesPoint("JWM:MAIN:C","B")&&!RoompresentQrLocalization.MatchesPoint("JWM:MAIN:B","A")&&!RoompresentQrLocalization.MatchesPoint(RoompresentDeliveryRecord.A1,"B"),"Wrong point or product advanced QR setup");
            foreach(string payload in new[]{"JWM:MAIN:E","JWM:MAIN:A","JWM:ROOMPRESENT:153613:A","JWM:MAIN:B","JWM:MAIN:C","JWM:ROOMPRESENT:153613:B","JWM:ROOMPRESENT:153613:C"})
            {
                var bits=new ZXing.QrCode.QRCodeWriter().encode(payload,ZXing.BarcodeFormat.QR_CODE,320,320);
                var gray=new byte[320*320];for(int y=0;y<320;y++)for(int x=0;x<320;x++)gray[y*320+x]=bits[x,y]?(byte)0:(byte)255;
                for(int turn=0;turn<4;turn++)
                {
                    var r=RoompresentQrLocalization.DecodeQrFrame(gray,320,320);
                    Check(r!=null&&r.Text==payload&&r.ResultPoints.Length>=3,"Fixed A QR decoder rotation");
                    Vector3 Point(ZXing.ResultPoint p)=>new Vector3(p.X*.001f,.8f,-p.Y*.001f);
                    Check(RoompresentQrLocalization.TryPose(Point(r.ResultPoints[0]),Point(r.ResultPoints[1]),Point(r.ResultPoints[2]),out _),"QR finder ordering does not match pose geometry");
                    var rotated=new byte[gray.Length];for(int y=0;y<320;y++)for(int x=0;x<320;x++)rotated[x*320+319-y]=gray[y*320+x];gray=rotated;
                }
            }
            var source=guide.registrationLandmarks;var rotation=Quaternion.Euler(0,-71,0);var translation=new Vector3(1,.2f,3);
            var record=new RoompresentQrRegistration{qr="JWM:MAIN:E",fit=new RoompresentMapRegistration{source=source.ToArray(),observed=source.Select(p=>rotation*p+translation).ToArray(),mapFingerprint=vps.mapFingerprint,scanFingerprint=vps.scanFingerprint,operatorChecked=true}};
            Check(record.Validate(vps,source,out var t,out var yaw2)&&Vector3.Distance(t,translation)<.001f&&Mathf.Abs(Mathf.DeltaAngle(yaw2,-71))<.01f,"Fixed A stored registration");
            record.fit.operatorChecked=false;Check(!record.Validate(vps,source,out _,out _),"Unverified B/C registration accepted");
            VerifyOwnerQrPhotos();
            File.WriteAllText(Logs+"fixed-a.txt","PASS metric QR pose at four headings, mirrored/non-horizontal/product rejection, strict A/B/C identity and actual QR decode/finder order at 28 rotations, saved registration and operator check. AR plane/anchor capture and physical B/C alignment remain device tests.\n");
        }
        [Serializable] sealed class QrPhoto { public string path,payload; public float[] projection; }
        [Serializable] sealed class QrPhotos { public QrPhoto[] photos; }
        static void VerifyOwnerQrPhotos()
        {
            const string config="Library/RoompresentQrPhotos.local.json";
            if(!File.Exists(config))return;
            var photos=JsonUtility.FromJson<QrPhotos>(File.ReadAllText(config));Check(photos?.photos?.Length==2,"Expected both owner B/C photos");
            foreach(var photo in photos.photos)
            {
                var texture=new Texture2D(2,2);
                try
                {
                    Check(texture.LoadImage(File.ReadAllBytes(photo.path)),"Cannot load owner QR photo");
                    var pixels=texture.GetPixels32();int step=Math.Max(1,(texture.height+1599)/1600),w=texture.width/step,h=texture.height/step;
                    var gray=new byte[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=pixels[(texture.height-1-y*step)*texture.width+x*step];gray[y*w+x]=(byte)((p.r*77+p.g*150+p.b*29)>>8);}
                    var frame=RoompresentQrLocalization.InspectFrame(gray,w,h);var decoded=frame.result;
                    if(decoded==null&&photo.projection?.Length==9)
                    {
                        var m=photo.projection;
                        decoded=RoompresentQrLocalization.DecodeProjected(frame,(x,y)=>{float d=m[6]*x+m[7]*y+m[8];return new Vector2((m[0]*x+m[1]*y+m[2])/d/step,(m[3]*x+m[4]*y+m[5])/d/step);});
                    }
                    Check(decoded!=null&&decoded.Text==photo.payload,"Owner B/C photo QR must decode exactly");
                    Debug.Log("[RoompresentQRReview] Owner photo decoded "+decoded.Text);
                }
                finally{UnityEngine.Object.DestroyImmediate(texture);}
            }
            File.WriteAllText(Logs+"owner-photo-qr.txt","PASS actual owner photographs decode as JWM:MAIN:B and JWM:MAIN:C using the runtime detector/decoder and planar resampling. Photo tests use an independently measured image quadrilateral; runtime uses AR plane geometry instead. Real Samsung plane sampling and physical room positions remain device tests.\n");
        }
        static void VerifyRegistration(SmallRoomAR guide,RoompresentVpsLocalization vps)
        {
            var source=guide.registrationLandmarks;var rotation=Quaternion.Euler(0,37,0);var translation=new Vector3(2,-.8f,3);
            var r=new RoompresentMapRegistration{source=source.ToArray(),observed=source.Select(p=>rotation*p+translation).ToArray(),mapFingerprint=vps.mapFingerprint,scanFingerprint=vps.scanFingerprint,operatorChecked=true};
            Check(r.Validate(vps.mapFingerprint,vps.scanFingerprint,source,out var t,out float yaw)&&(t-translation).magnitude<.001f&&Mathf.Abs(Mathf.DeltaAngle(yaw,37))<.001f,"Valid registration rejected");
            Check(!r.Validate("different-map",vps.scanFingerprint,source,out _,out _)&&!r.Validate(vps.mapFingerprint,"different-scan",source,out _,out _),"Stale map registration accepted");
            r.operatorChecked=false;Check(!r.Validate(vps.mapFingerprint,vps.scanFingerprint,source,out _,out _),"Unchecked registration accepted");r.operatorChecked=true;r.observed[2]+=Vector3.up;Check(!r.Validate(vps.mapFingerprint,vps.scanFingerprint,source,out _,out _),"Inconsistent landmarks accepted");
            File.WriteAllText(Logs+"registration.txt","PASS rigid scan-to-map transform, stale map and scan fingerprints, operator-check gate and inconsistent landmarks. Old v1 start QR registration is not silently imported.\nNo camera pose or book scan is used as a localization substitute.\n");
        }
        [MenuItem("Tools/JarVis/Roompresent VPS/03 Build Full JarVis")]
        public static void Build()
        {
            Scan045Review.Run();Prepare();DeliveryWorkflowReview.RunSaved();PreserveDirty();var enabled=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();Check(enabled[0]=="Assets/Scenes/JarVisFigmaApp.unity"&&enabled.Contains(ScenePath),"Full JarVis entry and VPS required");
            AndroidProjectConfigurator.ApplySettings();JarvisProjectValidator.Validate();AssetDatabase.SaveAssets();Directory.CreateDirectory(Path.GetDirectoryName(Output));EditorUserBuildSettings.buildAppBundle=false;
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=enabled,target=BuildTarget.Android,locationPathName=Path.GetFullPath(Output),options=BuildOptions.StrictMode|BuildOptions.CompressWithLz4HC});
            File.WriteAllText(Logs+"build.txt","result="+report.summary.result+"\nerrors="+report.summary.totalErrors+"\nseconds="+report.summary.totalTime.TotalSeconds+"\nFull JarVis 0.4.5/code29 with metric QR A and measured destination reference transfer, QR frame/progress/success OK, focus refresh without session reset, remaining-stop handoff and ordered QR/product proof. Bilingual compact UI and math/workflow regression checks passed. Physical recovery and walking remain device tests.\n");Check(report.summary.result==BuildResult.Succeeded,"VPS full-app build failed");EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);Debug.Log("[RoompresentVPSBuild] APK READY "+Path.GetFullPath(Output));
        }
    }
}
#endif
