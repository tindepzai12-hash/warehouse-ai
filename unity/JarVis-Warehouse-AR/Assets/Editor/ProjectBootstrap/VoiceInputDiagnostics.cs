using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.UI;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    [InitializeOnLoad]
    public static class VoiceInputDiagnostics
    {
        const string Request = "Library/JarVisVoiceInput.request";
        static double nextCheck;
        static VoiceInputDiagnostics()
        {
            EditorApplication.delayCall += CheckRequest;
            EditorApplication.update += CheckRequest;
        }
        static void CheckRequest()
        {
            if(EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextCheck)return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            if(!File.Exists(Request))return;
            File.Delete(Request);
            WriteReport();
        }
        [MenuItem("Tools/JarVis/Voice/Input and Playback Diagnostics")]
        public static void WriteReport()
        {
            var report = new StringBuilder();
            report.AppendLine("UTC=" + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Play=" + EditorApplication.isPlaying);
            var mute = typeof(EditorUtility).GetProperty("audioMasterMute",BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            report.AppendLine("Editor audio mute=" + (mute == null ? "unavailable" : mute.GetValue(null)));
            report.AppendLine("Listener pause=" + AudioListener.pause + "; volume=" + AudioListener.volume);
            var devices = Microphone.devices;
            report.AppendLine("Microphone count=" + devices.Length);
            foreach(var device in devices)report.AppendLine("Input=" + device);
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            report.AppendLine("Active listeners=" + listeners.Count(listener=>listener.isActiveAndEnabled));
            report.AppendLine("Authenticated=" + OmsMobileSession.IsReady);
            if(OmsMobileSession.IsReady)report.AppendLine("Backend=" + OmsMobileSession.BaseUrl);
            foreach(var media in UnityEngine.Object.FindObjectsByType<ImportedChatMedia>(FindObjectsSortMode.None))
            {
                report.AppendLine("Media=" + media.State + "; busy=" + media.IsBusy + "; recorder=" + media.Recorder?.State + "; seconds=" + media.Recorder?.Duration);
                report.AppendLine("Playback=" + media.Playback?.Playing + "; position=" + media.Playback?.Position);
                foreach(var view in media.GetComponentsInChildren<VoiceMessageView>())
                    report.AppendLine("Voice=" + view.Message.Status + "; seconds=" + view.Message.Duration + "; error=" + (view.Message.Error ?? "").Split(':')[0]);
            }
            Directory.CreateDirectory("Logs/VoiceInputDiagnosis");
            File.WriteAllText("Logs/VoiceInputDiagnosis/Current.txt",report.ToString());
            Debug.Log("[JarVis] Voice diagnostics saved; no microphone capture or audio upload performed.");
        }
    }
}
