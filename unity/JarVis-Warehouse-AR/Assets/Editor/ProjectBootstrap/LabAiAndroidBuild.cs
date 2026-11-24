using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;

namespace ProjectBootstrap
{
    // Dependency is pinned and added only to the dedicated Lab application build.
    public sealed class LabAiAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder=>100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if(PlayerSettings.applicationIdentifier!="com.teep.jarvislab")
            {
                // Unity reuses this Gradle folder across application IDs. Remove only
                // our byte-identical generated Lab sources from a previous Lab build.
                foreach(string name in new[]{"LabObjectDetector.java","LabRuntimeDiagnostics.java"})
                {
                    string generated=Path.Combine(path,"src/main/java/com/jarvis/warehouse",name);
                    string template=Path.Combine(UnityEngine.Application.dataPath,"Editor/ProjectBootstrap",name+".txt");
                    if(File.Exists(generated)&&File.Exists(template)&&File.ReadAllText(generated)==File.ReadAllText(template))File.Delete(generated);
                }
                return;
            }
            string file=Path.Combine(path,"build.gradle");
            string source=File.ReadAllText(file);
            const string dependency="implementation 'com.google.mediapipe:tasks-vision:0.10.32'";
            if(!source.Contains(dependency))source+="\ndependencies { "+dependency+" }\n";
            File.WriteAllText(file,source);
            string javaDir=Path.Combine(path,"src/main/java/com/jarvis/warehouse");Directory.CreateDirectory(javaDir);
            File.Copy(Path.Combine(UnityEngine.Application.dataPath,"Editor/ProjectBootstrap/LabObjectDetector.java.txt"),Path.Combine(javaDir,"LabObjectDetector.java"),true);
            File.Copy(Path.Combine(UnityEngine.Application.dataPath,"Editor/ProjectBootstrap/LabRuntimeDiagnostics.java.txt"),Path.Combine(javaDir,"LabRuntimeDiagnostics.java"),true);
            // Offline detector does not need the network-state permission inherited from its dependency.
            string manifestPath=Path.Combine(path,"src/main/AndroidManifest.xml");
            var manifest=new XmlDocument();manifest.Load(manifestPath);
            var root=manifest.DocumentElement;root.SetAttribute("xmlns:tools","http://schemas.android.com/tools");
            var permission=manifest.CreateElement("uses-permission");permission.SetAttribute("name","http://schemas.android.com/apk/res/android","android.permission.ACCESS_NETWORK_STATE");
            permission.SetAttribute("node","http://schemas.android.com/tools","remove");root.AppendChild(permission);manifest.Save(manifestPath);
        }
    }
}

