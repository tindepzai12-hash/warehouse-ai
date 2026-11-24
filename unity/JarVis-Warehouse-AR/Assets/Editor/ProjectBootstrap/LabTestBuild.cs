using System;
using System.IO;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.VisualMaps;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class LabTestBuild
    {
        public const string ScenePath="Assets/Scenes/JarVisLabTest.unity";
        public const string ApkPath="Builds/Android/JarVis-Lab-Test-0.9.8.apk";
        static LabTestBuild()
        {
            EditorApplication.update+=()=>
            {
                if(!SessionState.GetBool("JarVis.LabBuildPending",false)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
                SessionState.SetBool("JarVis.LabBuildPending",false);
                try{Build();}catch(Exception error){File.WriteAllText("Logs/LabTest/build-result.txt","FAILED "+LabTestSession.Version+"\n"+error);Debug.LogException(error);}
            };
        }
        public static void Queue()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Stop Play and preserve unsaved scene edits first.");
            Directory.CreateDirectory("Logs/LabTest");File.WriteAllText("Logs/LabTest/build-result.txt","RUNNING "+LabTestSession.Version+"\n");
            SessionState.SetBool("JarVis.LabBuildPending",true);
        }
        public static void Prepare()
        {
            if(EditorApplication.isPlaying||EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Stop Play and fix compilation first.");
            Directory.CreateDirectory("Assets/Resources/LabTest");
            // A serialized transparent material retains the Android alpha shader variant.
            const string cueMaterialPath="Assets/Resources/LabTest/LiveCueTransparent.mat";
            var cueMaterial=AssetDatabase.LoadAssetAtPath<Material>(cueMaterialPath);
            if(cueMaterial==null)
            {
                cueMaterial=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/Routes/LiveViewUnlit.mat")){name="LiveCueTransparent",renderQueue=3005};
                cueMaterial.SetOverrideTag("RenderType","Transparent");cueMaterial.SetFloat("_Surface",1);cueMaterial.SetFloat("_ZWrite",0);cueMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);cueMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);cueMaterial.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);cueMaterial.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);cueMaterial.SetFloat("_Cull",(float)CullMode.Off);cueMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                AssetDatabase.CreateAsset(cueMaterial,cueMaterialPath);AssetDatabase.SaveAssets();
            }
            if(!cueMaterial.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")||cueMaterial.GetFloat("_ZWrite")!=0)throw new InvalidOperationException("Cue alpha variant must remain serialized for Android.");
            const string libraryPath="Assets/Resources/LabTest/LabMarkers.asset";
            var library=AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(libraryPath);
            if(library==null)
            {
                library=ScriptableObject.CreateInstance<XRReferenceImageLibrary>();AssetDatabase.CreateAsset(library,libraryPath);
                for(int i=0;i<5;i++)
                {
                    string id=((char)('A'+i)).ToString(),path="Assets/Resources/LabTest/Lab-"+id+".png";
                    var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)throw new InvalidOperationException("Missing marker "+path);
                    importer.isReadable=true;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                    library.Add();library.SetName(i,id);library.SetTexture(i,AssetDatabase.LoadAssetAtPath<Texture2D>(path),true);library.SetSpecifySize(i,true);library.SetSize(i,new Vector2(.2f,.24f));
                }
                EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
            }
            if(library.count!=5)throw new InvalidOperationException("Exactly 5 marker references required.");
            const string phonePath="Assets/Resources/LabTest/LabPhoneMarker.asset";
            var phone=AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(phonePath);
            if(phone==null){phone=ScriptableObject.CreateInstance<XRReferenceImageLibrary>();AssetDatabase.CreateAsset(phone,phonePath);phone.Add();phone.SetName(0,"A");phone.SetTexture(0,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/LabTest/Lab-A.png"),true);phone.SetSpecifySize(0,false);EditorUtility.SetDirty(phone);AssetDatabase.SaveAssets();}
            if(phone.count!=1||phone[0].specifySize)throw new InvalidOperationException("Phone A must estimate physical image dimensions.");
            const string calibratedPath="Assets/Resources/LabTest/LabCalibratedMarkers.asset";
            var calibrated=AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(calibratedPath);
            if(calibrated==null)
            {
                calibrated=ScriptableObject.CreateInstance<XRReferenceImageLibrary>();AssetDatabase.CreateAsset(calibrated,calibratedPath);
                for(int i=0;i<3;i++){string id=((char)('A'+i)).ToString();calibrated.Add();calibrated.SetName(i,id);calibrated.SetTexture(i,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/LabTest/Lab-"+id+".png"),true);calibrated.SetSpecifySize(i,false);}
                EditorUtility.SetDirty(calibrated);AssetDatabase.SaveAssets();
            }
            if(calibrated.count!=3)throw new InvalidOperationException("A/B/C reference library missing");
            for(int i=0;i<3;i++)if(calibrated[i].specifySize)throw new InvalidOperationException("Do not assume unmeasured physical marker dimensions");
            const string fixedPath="Assets/Resources/LabTest/LabFixedMarkers.asset";
            var fixedLibrary=AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(fixedPath);
            if(fixedLibrary==null){fixedLibrary=ScriptableObject.CreateInstance<XRReferenceImageLibrary>();AssetDatabase.CreateAsset(fixedLibrary,fixedPath);}
            while(fixedLibrary.count<5)fixedLibrary.Add();
            if(fixedLibrary.count!=5)throw new InvalidOperationException("Fixed library must contain exactly A-E");
            for(int i=0;i<5;i++)
            {
                string id=((char)('A'+i)).ToString();fixedLibrary.SetName(i,id);
                fixedLibrary.SetTexture(i,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/LabTest/Lab-"+id+".png"),true);
                fixedLibrary.SetSpecifySize(i,true);fixedLibrary.SetSize(i,new Vector2(LabPrintedCard.ImageWidthMetres,LabPrintedCard.ImageHeightMetres));
            }
            EditorUtility.SetDirty(fixedLibrary);AssetDatabase.SaveAssets();
            for(int i=0;i<5;i++)
            {
                string id=((char)('A'+i)).ToString(),singlePath="Assets/Resources/LabTest/LabFixedMarker-"+id+".asset";
                var single=AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(singlePath);
                if(single==null){single=ScriptableObject.CreateInstance<XRReferenceImageLibrary>();AssetDatabase.CreateAsset(single,singlePath);single.Add();}
                if(single.count!=1)throw new InvalidOperationException("Single-card library must contain exactly one image: "+id);
                single.SetName(0,id);single.SetTexture(0,AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/LabTest/Lab-"+id+".png"),true);
                single.SetSpecifySize(0,true);single.SetSize(0,new Vector2(LabPrintedCard.ImageWidthMetres,LabPrintedCard.ImageHeightMetres));EditorUtility.SetDirty(single);
            }
            AssetDatabase.SaveAssets();
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)
            {
                VisualMapBuild.EnsureScene();
                var scene=EditorSceneManager.OpenScene(VisualMapBuild.ScenePath,OpenSceneMode.Additive);
                try
                {
                    VisualMapSession session=null;
                    foreach(var root in scene.GetRootGameObjects()){session=root.GetComponentInChildren<VisualMapSession>(true);if(session!=null)break;}
                    if(session==null)throw new InvalidOperationException("Existing AR camera scene missing.");
                    session.gameObject.AddComponent<LabTestSession>();
                    EditorSceneManager.SaveScene(scene,ScenePath,true);
                }
                finally{EditorSceneManager.CloseScene(scene,true);}
            }
        }
        public static void Build()
        {
            Prepare();
            string name=PlayerSettings.productName,identifier=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),version=PlayerSettings.bundleVersion;
            int code=PlayerSettings.Android.bundleVersionCode;
            bool keystore=PlayerSettings.Android.useCustomKeystore,bundle=EditorUserBuildSettings.buildAppBundle;
            var sdk=PlayerSettings.Android.minSdkVersion;var arch=PlayerSettings.Android.targetArchitectures;var scripting=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            var orientation=PlayerSettings.defaultInterfaceOrientation;var graphics=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            var core=ARCoreSettings.currentSettings;var depth=core!=null?core.depth:ARCoreSettings.Requirement.Optional;
            try
            {
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android&&!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))throw new BuildFailedException("Cannot switch to Android.");
                PlayerSettings.productName="JarVis Lab Test";PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.teep.jarvislab");
                PlayerSettings.bundleVersion=LabTestSession.Version;PlayerSettings.Android.bundleVersionCode=25;
                PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel29;PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
                PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;PlayerSettings.Android.useCustomKeystore=false;EditorUserBuildSettings.buildAppBundle=false;
                GraphicsSettings.defaultRenderPipeline=QualitySettings.renderPipeline=AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
                if(core!=null)core.depth=ARCoreSettings.Requirement.Optional;
                Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=BuildTarget.Android,locationPathName=Path.GetFullPath(ApkPath),options=BuildOptions.StrictMode|BuildOptions.CompressWithLz4HC});
                Directory.CreateDirectory("Logs/LabTest");
                File.WriteAllText("Logs/LabTest/build-result.txt","result="+result.summary.result+"\nerrors="+result.summary.totalErrors+"\nbytes="+result.summary.totalSize+"\nseconds="+result.summary.totalTime.TotalSeconds+"\nutc="+DateTime.UtcNow.ToString("O")+"\nPhysical camera/navigation and device install are NOT verified by this build.\n");
                if(result.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("Lab test APK failed: "+result.summary.result);
            }
            finally
            {
                PlayerSettings.productName=name;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,identifier);PlayerSettings.bundleVersion=version;PlayerSettings.Android.bundleVersionCode=code;
                PlayerSettings.Android.useCustomKeystore=keystore;EditorUserBuildSettings.buildAppBundle=bundle;PlayerSettings.Android.minSdkVersion=sdk;PlayerSettings.Android.targetArchitectures=arch;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,scripting);PlayerSettings.defaultInterfaceOrientation=orientation;
                GraphicsSettings.defaultRenderPipeline=graphics;QualitySettings.renderPipeline=quality;if(core!=null)core.depth=depth;AssetDatabase.SaveAssets();
            }
        }
    }
}
