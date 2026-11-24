#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JarVis.Warehouse.Lab;
using JarVis.Warehouse.Navigation;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class QrCpu047Review
    {
        static readonly StringBuilder Log=new StringBuilder();
        const string Out="Logs/QrCpu047";
        static void Check(bool ok,string text){Log.AppendLine((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
        public static void Run()
        {
            Directory.CreateDirectory(Out);Log.Clear();Log.AppendLine(DateTime.UtcNow.ToString("O"));
            try
            {
                typeof(LabUpgrade097Review).GetMethod("TestLuminance",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
                Check(true,"Existing identity/rotation/reflection, padded/interleaved strides, invalid plane/UV/bounds cases");
                int decoded=0;bool portrait=false,landscape=false;
                foreach(int frame in new[]{1,2,3,4})
                {
                    var tex=new Texture2D(2,2);
                    try
                    {
                        Check(tex.LoadImage(File.ReadAllBytes(@"C:\Users\Windows\Documents\ChatGPT\jarvis\qr-a-video-20261123\frame-"+frame.ToString("D2")+".png")),"Load actual QR A video frame "+frame);
                        var colors=tex.GetPixels32();int stride=tex.width*2+16;
                        var data=new NativeArray<byte>(stride*tex.height,Allocator.Temp);
                        try
                        {
                            // A padded, interleaved CPU luminance plane. GetPixels32 rows
                            // and identity viewport UVs both begin at the viewport bottom.
                            for(int y=0;y<tex.height;y++)for(int x=0;x<tex.width;x++)
                            {
                                var p=colors[y*tex.width+x];data[y*stride+x*2]=(byte)((p.r*77+p.g*150+p.b*29)>>8);
                            }
                            foreach(int orientation in new[]{0,90})
                            {
                                int w=orientation==0?648:1440,h=orientation==0?1440:648;
                                var display=Matrix4x4.identity;
                                if(orientation==90){display.m00=0;display.m01=1;display.m10=-1;display.m11=0;display.m12=1;}
                                var gray=new byte[w*h];
                                Check(LabCameraLuminance.Copy(data,tex.width,tex.height,stride,2,display.transpose,gray,w,h),"1440px CPU image accepted frame="+frame+" orientation="+orientation);
                                for(int y=0;y<h/2;y++)for(int x=0;x<w;x++){int a=y*w+x,b=(h-y-1)*w+x;byte p=gray[a];gray[a]=gray[b];gray[b]=p;}
                                var result=RoompresentQrLocalization.DecodeQrFrame(gray,w,h);
                                bool read=result?.Text=="JWM:MAIN:E";
                                Log.AppendLine("VIDEO frame="+frame+" orientation="+orientation+" decoded="+(result?.Text??"unreadable (recorded scan beam/blur retained)"));
                                if(read){decoded++;portrait|=orientation==0;landscape|=orientation==90;}
                            }
                            Check(!LabCameraLuminance.Copy(data,tex.width,tex.height,stride,2,Matrix4x4.identity,new byte[1],648,1440),"Reject mismatched destination buffer");
                            Check(!LabCameraLuminance.Copy(data,tex.width,tex.height,stride,2,Matrix4x4.identity,new byte[2050],2050,1),"Reject excessive destination dimension");
                        }
                        finally{data.Dispose();}
                    }
                    finally{UnityEngine.Object.DestroyImmediate(tex);}
                }
                Check(decoded>=2&&portrait&&landscape,"Full CPU plane → display transform → row flip → actual video QR A decoded in portrait and landscape (readable frames="+decoded+"/8; unreadable frames not counted as success)");
                Recovery046Review.Run();
                Check(true,"108 QR A/B/C perspective cases, real B/C photos, product barcodes, ordered delivery, retained map/session handoff regressions");
                File.WriteAllText(Out+"/result.txt",Log.ToString());Debug.Log("[QrCpu047Review] PASS");
            }
            catch(Exception e){File.WriteAllText(Out+"/result.txt",Log+"FAIL "+e);throw;}
        }
        public static void Build(){Run();AndroidBuild.BuildApk();}
    }
}
#endif
