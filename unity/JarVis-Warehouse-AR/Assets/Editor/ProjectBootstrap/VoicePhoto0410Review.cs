#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBootstrap {
public static class VoicePhoto0410Review {
 const string Out=@"C:\Users\Windows\Documents\ChatGPT\jarvis\voice-photo-20261123";
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Debug.Log("[VoicePhoto0410] PASS "+name);}
 static byte[] Wav(){using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
  const int frames=16000;w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+frames*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(16000);w.Write(32000);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(frames*2);for(int i=0;i<frames;i++)w.Write((short)(Math.Sin(i*.13)*4000));return stream.ToArray();}}
 static string Hash(string path)=>Convert.ToBase64String(System.Security.Cryptography.SHA256.Create().ComputeHash(File.ReadAllBytes(path)));
 static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Flags).SetValue(obj,value);
 static void Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Flags|BindingFlags.Public).Invoke(obj,args);
 public static void Run(){
  Directory.CreateDirectory(Out);
  var protectedFiles=new[]{"Assets/Scenes/Roompresent_VPS.unity","Assets/Resources/RoompresentCheckedRegistration.json","Assets/JarVis/Scripts/LabTesting/SmallRoomAR.cs","Assets/JarVis/Scripts/Navigation/RoompresentDeliveryVerification.cs"};
  protectedFiles=protectedFiles.Select(p=>File.Exists(p)?p:Directory.GetFiles("Assets",Path.GetFileName(p),SearchOption.AllDirectories).Single()).ToArray();
  var hashes=protectedFiles.Select(Hash).ToArray();
  var mediaSource=File.ReadAllText("Assets/JarVis/Scripts/UI/ImportedChatMedia.cs");
  Check(mediaSource.Contains("message.Id,message.AttachedPhoto)"),"Upload and retry use recording-owned image");
  Check(!mediaSource.Contains("if(reply!=null){ClearPhoto();"),"Late completion cannot clear newer composer photo");
  var retry=mediaSource.Substring(mediaSource.IndexOf("public void RetryVoice"),mediaSource.IndexOf("void StartUpload")-mediaSource.IndexOf("public void RetryVoice"));
  Check(!retry.Contains("AddVoiceMessage")&&!retry.Contains("CapturePhoto")&&!retry.Contains("ClearPhoto"),"Retry reuses same row and attachment");
  using(var owned=VoiceMessage.Create(Wav())){var original=new byte[]{2,4,6};owned.CapturePhoto(original);original[0]=9;Check(owned.AttachedPhoto[0]==2,"Attachment copied before composer can change");bool rejected=false;try{owned.CapturePhoto(new byte[]{8});}catch(InvalidOperationException){rejected=true;}Check(rejected,"Attachment cannot be rebound on retry");owned.Dispose();Check(owned.AttachedPhoto==null&&owned.Disposed,"Dispose releases attachment and WAV");}
  string theme=PlayerPrefs.GetString("JarVis.Theme","light");var language=JarvisLocalization.CurrentLanguage;
  try{
   foreach(bool dark in new[]{false,true})foreach(bool en in new[]{false,true}){
    EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
    var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>(FindObjectsInactive.Include);nav.Initialize();
    var settings=nav.gameObject.AddComponent<ImportedSettings>();Set(settings,"nav",nav);Set(settings,"font",nav.GetComponentInChildren<TMP_Text>(true).font);Set(nav,"<Settings>k__BackingField",settings);
    PlayerPrefs.SetString("JarVis.Theme",dark?"dark":"light");JarvisLocalization.SetLanguage(en?SupportedLanguage.En:SupportedLanguage.Vi);
    nav.Show("5:1216",false);var root=nav.screens.Single(s=>s.id=="5:1216").root;
    var chat=root.GetComponent<ImportedJarvisChat>()??root.AddComponent<ImportedJarvisChat>();chat.Configure(root);
    var media=chat.Media;
    byte[] jpeg=File.ReadAllBytes("Assets/Resources/"+JarvisLocalCatalogue.Items.Single(i=>i.code=="A1").image+".png");
    Set(media,"<Photo>k__BackingField",jpeg);
    var message=VoiceMessage.Create(Wav());
    Invoke(media,"CommitVoiceMessage",message);
    Check(media.Photo==null&&message.AttachedPhoto.SequenceEqual(jpeg),"Composer moved into recording-owned image");
    Check(chat.MessageCount==1,"Photo and voice are one retained conversation item");
    var rows=(List<GameObject>)typeof(ImportedJarvisChat).GetField("rows",Flags).GetValue(chat);var combined=rows.Single();
    Check(combined.GetComponent<VerticalLayoutGroup>()!=null&&combined.GetComponentsInChildren<VoiceMessageView>().Length==1&&combined.GetComponentsInChildren<RawImage>().Length==1,"Actual image and voice share layout/lifetime");
    var texture=combined.GetComponentInChildren<RawImage>().texture;Check(texture.width>100&&texture.height>100,"Actual photo pixels decoded");
    settings.ApplyTheme();
    string tag=(dark?"dark":"light")+(en?"-en":"-vi");
    foreach(var state in new[]{VoiceMessageStatus.Sending,VoiceMessageStatus.Failed,VoiceMessageStatus.Completed}){
     message.Status=state;message.Retryable=state==VoiceMessageStatus.Failed;message.Error=state==VoiceMessageStatus.Failed?"NETWORK_ERROR":null;
     if(state==VoiceMessageStatus.Completed)message.Transcript=en?"Where should I put notebook A1?":"TĂ´i cáº§n giao quyá»ƒn vá»Ÿ A1 Ä‘áº¿n Ä‘Ă¢u?";
     Invoke(combined.GetComponentInChildren<VoiceMessageView>(),"Refresh");
     Capture(nav.GetComponentInParent<Canvas>(),Out+"/photo-voice-"+tag+"-"+state+".png",2400);
     Check(combined.GetComponentInChildren<RawImage>().texture==texture,"Photo remains visible: "+state);
     Check(((RectTransform)combined.transform).rect.height>=300,"Nested photo and voice reserve height without overlap");
    }
    Capture(nav.GetComponentInParent<Canvas>(),Out+"/photo-voice-"+tag+"-1920.png",1920);
    Set(media,"<Photo>k__BackingField",new byte[]{7,8,9});Check(message.AttachedPhoto.SequenceEqual(jpeg),"New draft cannot replace sent attachment");media.ClearPhoto();
    var voiceOnly=VoiceMessage.Create(Wav());Invoke(media,"CommitVoiceMessage",voiceOnly);Check(voiceOnly.AttachedPhoto==null&&chat.MessageCount==2,"Voice-only send remains one row");
    // Exercise existing retention removal: the whole visual row and audio dispose together.
    string wavPath=message.AudioPath;Invoke(chat,"RemoveRow",combined);
    Check(message.Disposed&&message.AttachedPhoto==null&&!File.Exists(wavPath),"Removing combined row releases photo bytes and local WAV");
    Check(!((Dictionary<GameObject,Texture2D>)typeof(ImportedJarvisChat).GetField("imageTextures",Flags).GetValue(chat)).ContainsKey(combined),"Photo texture ownership removed with row");
   }
  }finally{PlayerPrefs.SetString("JarVis.Theme",theme);JarvisLocalization.SetLanguage(language);}
  for(int i=0;i<protectedFiles.Length;i++)Check(Hash(protectedFiles[i])==hashes[i],"Untouched "+Path.GetFileName(protectedFiles[i]));
  File.WriteAllText(Out+"/result.txt","PASS: recording attachment ownership, single grouped row, pending/failed/completed UI, VI/EN light/dark, 1080x2400 and 1080x1920, voice-only and cleanup. Synthetic audio, no provider/microphone call.\n");Debug.Log("[VoicePhoto0410] PASS ALL");
 }
 static void Capture(Canvas canvas,string file,int h){
  var capture=typeof(RoomFloorplan049Review).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);capture.Invoke(null,new object[]{canvas,1080,h,file});
 }
 public static void Build(){Run();AndroidBuild.BuildApk();}
}}
#endif
