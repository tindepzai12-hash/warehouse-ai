#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using JarVis.Warehouse.Navigation;
using Immersal.Samples.Navigation;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZXing;
using ZXing.Common;
namespace ProjectBootstrap
{
    public static class Scan045Review
    {
        public static void Run()
        {
            ReturnA044Review.Run();
            EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
            var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
            var stop=typeof(SmallRoomAR).GetField("activeStop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var matrix=guide.mapRoot.localToWorldMatrix;var before=stop.GetValue(guide);
            try
            {
                var a=guide.pins.GetComponentsInChildren<IsNavigationTarget>(true).Single(p=>p.targetName=="A");
                stop.SetValue(guide,"A");guide.UpdateStartPin();if(!a.gameObject.activeSelf)throw new Exception("Return A marker hidden");
                stop.SetValue(guide,"C");guide.UpdateStartPin();if(a.gameObject.activeSelf)throw new Exception("A marker obscures delivery mode");
                if(guide.mapRoot.localToWorldMatrix!=matrix)throw new Exception("Pin changed registered transform");
            }
            finally{stop.SetValue(guide,before);guide.UpdateStartPin();}
            var log=new StringBuilder("PASS existing ordered proof, Return A, saved registration, handoff, repeated map and bilingual UI regressions. PASS A marker selected/hidden without moving map.\n");
            foreach(string path in Directory.GetFiles("Logs/RoompresentVPS/Scan045/VideoFrames","*.png"))
            {
                var tex=new Texture2D(2,2);ImageConversion.LoadImage(tex,File.ReadAllBytes(path));int w=tex.width,h=tex.height;
                var pixels=tex.GetPixels32();var gray=new byte[w*h];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++){var p=pixels[(h-1-y)*w+x];gray[y*w+x]=(byte)((p.r*77+p.g*150+p.b*29)>>8);}
                int cropH=h/2,top=h/5;var crop=new byte[w*cropH];Array.Copy(gray,top*w,crop,0,crop.Length);
                var product=RoompresentDeliveryVerification.DecodeProduct(crop,w,cropH);
                var qr=RoompresentQrLocalization.DecodeQrFrame(crop,w,cropH);
                log.AppendLine(Path.GetFileName(path)+" product="+(product?.Text??"unreadable")+" QR="+(qr?.Text??"unreadable"));
                UnityEngine.Object.DestroyImmediate(tex);
            }
            foreach(string code in new[]{JarVis.Warehouse.UI.RoompresentDeliveryRecord.A1,JarVis.Warehouse.UI.RoompresentDeliveryRecord.A2})
            {
                var bits=new MultiFormatWriter().encode(code,BarcodeFormat.EAN_13,360,100);
                var gray=Enumerable.Repeat((byte)255,810*1440).ToArray();
                for(int y=0;y<100;y++)for(int x=0;x<360;x++)gray[(630+y)*810+225+x]=bits[x,y]?(byte)0:(byte)255;
                if(RoompresentDeliveryVerification.DecodeProduct(gray,810,1440)?.Text!=code)throw new Exception("Small central barcode missed");
            }
            log.AppendLine("PASS small product code in portrait frame. Video unreadable frames remain reported, never treated as a successful camera scan. Physical focus/recovery/QR proximity still require Samsung acceptance.");
            File.WriteAllText("Logs/RoompresentVPS/Scan045/result.txt",log.ToString());Debug.Log("[Scan045Review] PASS\n"+log);
        }
    }
}
#endif
