#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JarVis.Warehouse.Navigation;
using JarVis.Warehouse.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ZXing;

namespace ProjectBootstrap
{
    public static class Recovery046Review
    {
        const BindingFlags Flags=BindingFlags.NonPublic|BindingFlags.Instance;
        static readonly StringBuilder Log=new StringBuilder();
        static void Check(bool ok,string s){if(!ok)throw new Exception(s);Log.AppendLine("PASS "+s);}
        static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
        static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
        static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
        public static void Run()
        {
            const string folder="Logs/ScannerRecovery046";Directory.CreateDirectory(folder);
            Log.Clear();Log.AppendLine(DateTime.UtcNow.ToString("O"));
            try
            {
                Scan045Review.Run();
                Check(true,"Existing ordered QR/product proof, B/C handoff, Return A, AI map intent and mission state regressions");
                foreach(string code in new[]{"JWM:MAIN:E","JWM:MAIN:A","JWM:MAIN:B","JWM:MAIN:C"})
                {
                    var bits=new MultiFormatWriter().encode(code,BarcodeFormat.QR_CODE,512,512);
                    var gray=new byte[512*512];
                    for(int y=0;y<512;y++)for(int x=0;x<512;x++)gray[y*512+x]=bits[x,y]?(byte)0:(byte)255;
                    for(int turn=0;turn<4;turn++)
                    {
                        Check(RoompresentQrLocalization.DecodeQrFrame(gray,512,512)?.Text==code,"QR payload "+code+" rotated "+(turn*90));
                        var next=new byte[gray.Length];for(int y=0;y<512;y++)for(int x=0;x<512;x++)next[x*512+511-y]=gray[y*512+x];gray=next;
                    }
                }
                foreach(string code in new[]{RoompresentDeliveryRecord.A1,RoompresentDeliveryRecord.A2})
                {
                    var bits=new MultiFormatWriter().encode(code,BarcodeFormat.EAN_13,640,360);
                    var pixels=new Color32[640*360];for(int y=0;y<360;y++)for(int x=0;x<640;x++)pixels[y*640+x]=bits[x,y]?new Color32(0,0,0,255):new Color32(255,255,255,255);
                    Check(ImportedCameraScanner.Decode(pixels,640,360,true)?.Text==code,"Product camera decoder "+code);
                }
                foreach(var sample in new[]{
                    ("B",@"C:\Users\Windows\AppData\Local\Temp\codex-clipboard-909829eb-980f-4b1a-aad0-abc4e3be5a34.png"),
                    ("C",@"C:\Users\Windows\AppData\Local\Temp\codex-clipboard-9c657d00-d091-402e-9546-0f75359efbbb.png")})
                {
                    var tex=new Texture2D(2,2);
                    try
                    {
                        Check(tex.LoadImage(File.ReadAllBytes(sample.Item2)),"Load user photo QR "+sample.Item1);
                        int h=1440,w=Mathf.RoundToInt((float)tex.width/tex.height*h);var gray=new byte[w*h];var colors=tex.GetPixels32();
                        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                        {
                            var pixel=colors[(tex.height-1-y*tex.height/h)*tex.width+x*tex.width/w];
                            gray[y*w+x]=(byte)((pixel.r*77+pixel.g*150+pixel.b*29)>>8);
                        }
                        Check(RoompresentQrLocalization.DecodeQrFrame(gray,w,h)?.Text=="JWM:MAIN:"+sample.Item1,"Decode real photographed QR "+sample.Item1+" at camera resolution");
                    }
                    finally{UnityEngine.Object.DestroyImmediate(tex);}
                }
                CheckAngles();
                EditorSceneManager.OpenScene(RoompresentVpsBuild.ScenePath);
                var guide=UnityEngine.Object.FindFirstObjectByType<SmallRoomAR>();
                var vps=guide.localization;var manager=vps.cameraManager;
                var session=UnityEngine.Object.FindFirstObjectByType<ARSession>();
                var map=guide.mapRoot.localToWorldMatrix;var camera=manager.transform.localToWorldMatrix;
                var sessionEnabled=session.enabled;var record=JsonUtility.ToJson(RoompresentDeliveryMission.Current);
                var enumerator=(IEnumerator)Call(vps,"Refocus");
                Check(enumerator.MoveNext()&&!manager.enabled&&vps.FocusRefreshing,"Refresh stops camera image manager only");
                Check(enumerator.MoveNext()&&!manager.enabled,"Camera remains off for two frames");
                enumerator.MoveNext();
                Check(manager.enabled,"Refresh restarts camera image manager");
                Call(vps,"CancelCameraRefresh");(enumerator as IDisposable)?.Dispose();
                Check(!vps.FocusRefreshing&&manager.enabled,"Interrupted refresh clears busy flag and restores camera");
                Check(session.enabled==sessionEnabled&&guide.mapRoot.localToWorldMatrix==map&&manager.transform.localToWorldMatrix==camera&&JsonUtility.ToJson(RoompresentDeliveryMission.Current)==record,"Refresh preserves ARSession, map/camera transforms and delivery progress");
                Call(guide.qrLocalization??guide.gameObject.AddComponent<RoompresentQrLocalization>(),"OnApplicationPause",false);
                EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
                var nav=UnityEngine.Object.FindFirstObjectByType<ImportedFigmaNavigation>();nav.Initialize();nav.Show("5:331",false);
                var root=nav.screens.First(s=>s.id=="5:331").root;
                var scanner=root.GetComponent<ImportedCameraScanner>()??root.AddComponent<ImportedCameraScanner>();scanner.Configure(root);
                Check(scanner!=null,"Scanner available in saved full app");
                Set(scanner,"scanLocked",true);Set(scanner,"session",42);
                Call(scanner,"ContinueMissionScan");
                Check(!scanner.ScanLocked&&(int)Get(scanner,"session")>=44,"OK after paused success popup reacquires camera instead of only unlocking decoding");
                Check(scanner.State!=ScannerState.Scanning,"Editor reopen enters startup/idle; does not falsely report a working camera");
                Check(true,"Physical QR focus/long idle recovery and metric tracking still require Samsung acceptance");
                File.WriteAllText(folder+"/result.txt",Log.ToString());
                Debug.Log("[Recovery046Review] PASS");
            }
            catch(Exception e){File.WriteAllText(folder+"/result.txt",Log+"FAIL "+e);throw;}
        }
        public static void Build()
        {
            Run();
            AndroidBuild.BuildApk();
        }
        static void CheckAngles()
        {
            var views=new[]{Vector2.zero,new Vector2(0,35),new Vector2(0,-35),new Vector2(35,0),new Vector2(-35,0),new Vector2(25,25),new Vector2(-25,-25),new Vector2(-25,25),new Vector2(25,-25)};
            foreach(string code in new[]{"JWM:MAIN:E","JWM:MAIN:B","JWM:MAIN:C"})
            {
                var qr=new MultiFormatWriter().encode(code,BarcodeFormat.QR_CODE,256,256);
                foreach(var angle in views)foreach(int roll in new[]{0,90,180,270})
                {
                    var rotation=Quaternion.Euler(angle.x,angle.y,roll);
                    Vector2 Project(float x,float y){var p=rotation*new Vector3(x,y,0);return new Vector2(320+700*p.x/(1+p.z),480-700*p.y/(1+p.z));}
                    var tl=Project(-.22f,.22f);var tr=Project(.22f,.22f);var br=Project(.22f,-.22f);var bl=Project(-.22f,-.22f);
                    var warp=ZXing.Common.PerspectiveTransform.quadrilateralToQuadrilateral(tl.x,tl.y,tr.x,tr.y,br.x,br.y,bl.x,bl.y,0,0,256,0,256,256,0,256);
                    var image=Enumerable.Repeat((byte)255,640*960).ToArray();var point=new float[2];
                    int left=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(tl.x,tr.x,br.x,bl.x))),right=Mathf.Min(639,Mathf.CeilToInt(Mathf.Max(tl.x,tr.x,br.x,bl.x)));
                    int top=Mathf.Max(0,Mathf.FloorToInt(Mathf.Min(tl.y,tr.y,br.y,bl.y))),bottom=Mathf.Min(959,Mathf.CeilToInt(Mathf.Max(tl.y,tr.y,br.y,bl.y)));
                    for(int y=top;y<=bottom;y++)for(int x=left;x<=right;x++)
                    {
                        point[0]=x+.5f;point[1]=y+.5f;warp.transformPoints(point);
                        int px=(int)point[0],py=(int)point[1];
                        if(point[0]>=0&&point[1]>=0&&px<256&&py<256&&qr[px,py])image[y*640+x]=0;
                    }
                    Check(RoompresentQrLocalization.DecodeQrFrame(image,640,960)?.Text==code,"Perspective "+code+" pitch="+angle.x+" yaw="+angle.y+" roll="+roll);
                }
            }
        }
    }
}
#endif
