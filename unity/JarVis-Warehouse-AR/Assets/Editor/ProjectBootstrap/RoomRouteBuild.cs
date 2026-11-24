using System;
using System.IO;
using JarVis.Warehouse.RouteRecording;
using UnityEditor;
using UnityEditor.XR.ARSubsystems;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace ProjectBootstrap
{
    public static class RoomRouteBuild
    {
        public static void Prepare()
        {
            const string libraryPath = "Assets/Resources/Routes/RoomMarkers.asset";
            XRReferenceImageLibrary library = AssetDatabase.LoadAssetAtPath<XRReferenceImageLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<XRReferenceImageLibrary>();
                AssetDatabase.CreateAsset(library,libraryPath);
                for (int index = 0; index < 2; index++)
                {
                    string name = index == 0 ? "Room-A" : "Room-B";
                    string path = "Assets/Resources/Routes/" + name + ".png";
                    TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.isReadable = true;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.mipmapEnabled = false;
                    importer.SaveAndReimport();
                    library.Add();
                    library.SetName(index,name);
                    library.SetTexture(index,AssetDatabase.LoadAssetAtPath<Texture2D>(path),true);
                    library.SetSpecifySize(index,true);
                    library.SetSize(index,new Vector2(0.18f,0.18f));
                }
                EditorUtility.SetDirty(library);
            }
            const string glowPath = "Assets/Resources/Routes/RoomGlow.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(glowPath) == null)
            {
                Shader shader = Shader.Find("JarVis/RoomGlow");
                if (shader == null) throw new InvalidOperationException("Missing room glow shader");
                Material material = new Material(shader);
                material.SetColor("_BaseColor",new Color(0,0.9f,1,0.08f));
                AssetDatabase.CreateAsset(material,glowPath);
            }
            AssetDatabase.SaveAssets();
            if (ShaderUtil.ShaderHasError(Shader.Find("JarVis/RoomGlow"))) throw new InvalidOperationException("Room glow shader failed compilation");
        }

        public static void Reviews()
        {
            Prepare();
            RoomRouteReview.Run();
            RoomRouteRegressionReview.Run();
            RoomRouteReadinessReview.Run();
            RoomRoutePresentationReview.Run();
            RoomRouteUiReview.Run();
            RoomRoute035Review.Run();
        }

        public static void Run()
        {
            Reviews();
            WalkingRouteBuild.Build();
            File.WriteAllText("Logs/RoomRoutes/build.txt","PASS Android APK generated after room logic/UI checks. Camera and image tracking on Samsung still require real test.\nUTC=" + DateTime.UtcNow.ToString("O"));
        }
    }
}
