using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using ZXing;

namespace ProjectBootstrap
{
    [InitializeOnLoad] public static class AndroidScannerReview
    {
        public const string MilkCode = "MILK-180ML-CASE24-TEST";
        public const string Folder = "Logs/AndroidScanner";
        static AndroidScannerReview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("AndroidScannerReview", false)) return;
                SessionState.SetBool("AndroidScannerReview", false);
                new GameObject("Offline scanner review").AddComponent<AndroidScannerReviewRunner>();
            };
        }
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !Application.isBatchMode && EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save scene and stop Play first.");
            Directory.CreateDirectory(Folder); Directory.CreateDirectory("Docs/ScannerSamples");
            var checks = new List<string>();
            foreach (var sample in new[] {
                (BarcodeFormat.QR_CODE, MilkCode), (BarcodeFormat.CODE_128,"000123456789"), (BarcodeFormat.CODE_39,"CASE-MILK-01"),
                (BarcodeFormat.EAN_13,"5901234123457"), (BarcodeFormat.EAN_8,"96385074"), (BarcodeFormat.UPC_A,"012345678905"),
                (BarcodeFormat.UPC_E,"04210007"), (BarcodeFormat.ITF,"00123456789012") })
            {
                var matrix = new MultiFormatWriter().encode(sample.Item2, sample.Item1, 640, 640);
                var pixels = new Color32[640 * 640];
                for (int row = 0; row < 640; row++) for (int column = 0; column < 640; column++) pixels[row * 640 + column] = matrix[column,row] ? new Color32(0,0,0,255) : new Color32(255,255,255,255);
                var decoded = ImportedCameraScanner.Decode(pixels,640,640);
                bool same = decoded != null && decoded.Text == sample.Item2;
                if (!same && sample.Item1 == BarcodeFormat.UPC_A) same = decoded != null && decoded.BarcodeFormat == BarcodeFormat.EAN_13 && decoded.Text == "0" + sample.Item2;
                if (!same) throw new InvalidOperationException("Decode mismatch " + sample.Item1 + " => " + decoded?.BarcodeFormat + " / " + decoded?.Text);
                checks.Add("PASS Generated image " + sample.Item1 + " -> " + decoded.BarcodeFormat + " = " + decoded.Text);
                for (int angle = 1; angle <= 3; angle++)
                {
                    var rotated = new Color32[pixels.Length];
                    for (int row = 0; row < 640; row++) for (int column = 0; column < 640; column++) rotated[column*640+639-row] = pixels[row*640+column];
                    pixels = rotated;
                    var result = ImportedCameraScanner.Decode(pixels,640,640);
                    if (result == null || result.Text != decoded.Text) throw new InvalidOperationException("Rotation failed " + sample.Item1 + " / " + angle);
                    checks.Add("PASS " + sample.Item1 + " rotated " + angle*90);
                }
                if (sample.Item1 == BarcodeFormat.QR_CODE)
                {
                    var image = new Texture2D(640,640,TextureFormat.RGBA32,false);
                    var upright = new Color32[pixels.Length];
                    for (int row = 0; row < 640; row++) for (int column = 0; column < 640; column++) upright[(639-row)*640+column] = matrix[column,row] ? new Color32(0,0,0,255) : new Color32(255,255,255,255);
                    image.SetPixels32(upright);image.Apply();File.WriteAllBytes("Docs/ScannerSamples/Milk-Case-QR.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
                }
            }
            foreach (string code in new[] { "", new string('X',161), " leading", "line\nbreak", "https://example.com", "javascript:alert(1)", "{\"sku\":\"X\"}" })
            { if (ScannerValidation.Validate(code,"QR_CODE","Editor").ValidationStatus != "Invalid") throw new InvalidOperationException("Unsafe input accepted"); checks.Add("PASS Reject invalid input"); }
            var valid = ScannerValidation.Validate("000123456789","CODE_128","Editor");
            if(valid.NormalizedIdentifier!="000123456789")throw new InvalidOperationException("Leading zero lost");
            checks.Add("PASS Leading zeros preserved");
            if(ImportedCameraScanner.Decode(null,640,640)!=null || ImportedCameraScanner.Decode(new Color32[1],640,640)!=null)throw new InvalidOperationException("Invalid frame accepted");
            checks.Add("PASS Invalid frame dimensions rejected");
            var matches = ScannerValidation.ExactMatches(new OmsMobileApi.InventoryList{items=new[]{new OmsMobileApi.InventoryRow{sku="A",lotCode="000123456789"},new OmsMobileApi.InventoryRow{sku="000123456789",lotCode="OTHER"}}},"000123456789",out bool lotOnly);
            if(matches.Length!=1||lotOnly)throw new InvalidOperationException("Product/lot confusion");checks.Add("PASS Exact product match takes precedence over lot code");
            File.WriteAllText(Folder+"/Decoder.txt",DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",checks));
            EditorSceneManager.OpenScene("Assets/Scenes/JarVisFigmaApp.unity");
            SessionState.SetBool("AndroidScannerReview",true);EditorApplication.isPlaying=true;
        }
        public static void Build()
        {
            CheckConnectionPolicy();
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!image.LoadImage(File.ReadAllBytes("Docs/ScannerSamples/Milk-Case-Label.png")))throw new InvalidOperationException("QR label PNG is invalid");
                var result=ImportedCameraScanner.Decode(image.GetPixels32(),image.width,image.height);
                if(result==null||result.Text!=MilkCode)throw new InvalidOperationException("Final QR label does not decode correctly");
                File.WriteAllText(Folder+"/Label.txt","PASS final Milk-Case-Label.png decoded as "+MilkCode+"\n"+DateTime.UtcNow.ToString("O"));
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
            PlayerSettings.bundleVersion="0.1.2";PlayerSettings.Android.bundleVersionCode=3;
            ProductWorkspaceReview.BuildAndroid();
        }
        public static void CheckConnectionPolicy()
        {
            Directory.CreateDirectory(Folder);var checks=new List<string>();
            void Check(bool pass,string text){if(!pass)throw new InvalidOperationException(text);checks.Add("PASS "+text);}
            Check(OmsMobileApi.RequestTimeoutSeconds("/api/oms/mobile/login")==90,"Login timeout covers a documented free Render cold start without automatic retry");
            Check(OmsMobileApi.RequestTimeoutSeconds("/api/oms/mobile/inventory/search")==20,"Inventory timeout unchanged");
            Check(OmsMobileApi.RequestTimeoutSeconds("/api/oms/mobile/jarvis/chat")==70,"AI request timeout unchanged");
            foreach(var sample in new[]{("SSL CA certificate error","TLS"),("Cannot resolve destination host","DNS"),("Request timeout","TIMEOUT"),("Failed to connect","CONNECTION")})
                Check(OmsMobileApi.NetworkFailureKind(sample.Item1)==sample.Item2,"Transport classification "+sample.Item2);
            Check(ImportedFigmaNavigation.LoginFailureMessage("OMS network request failed (0) [TLS]").Contains("bảo mật"),"TLS failure never prompts certificate bypass");
            Check(ImportedFigmaNavigation.LoginFailureMessage("OMS network request failed (0) [TIMEOUT]").Contains("phản hồi chậm"),"Timeout not misreported as wrong password");
            Check(ImportedFigmaNavigation.LoginFailureMessage("OMS network request failed (0) [DNS]").Contains("DNS"),"DNS failure has actionable feedback");
            Check(ImportedFigmaNavigation.LoginFailureMessage("OMS network request failed (503) [CONNECTION]").Contains("chưa sẵn sàng"),"503 not misreported as wrong password");
            Check(ImportedFigmaNavigation.LoginFailureMessage("OMS login response is invalid").Contains("phản hồi không hợp lệ"),"Malformed login response is distinguished from credentials");
            File.WriteAllText(Folder+"/ConnectionPolicy.txt",DateTime.UtcNow.ToString("O")+"\n"+string.Join("\n",checks)+"\nNot an actual Samsung network/login test. No credentials or external requests used.");
        }
    }
    public sealed class AndroidScannerReviewRunner : MonoBehaviour
    {
        readonly List<string> checks=new List<string>(), errors=new List<string>();
        void Check(bool pass,string text){checks.Add((pass?"PASS ":"FAIL ")+text);if(!pass)errors.Add(text);}
        void Error(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)errors.Add(message);}
        IEnumerator Start()
        {
            Application.logMessageReceived+=Error;yield return null;yield return null;
            var nav=FindAnyObjectByType<ImportedFigmaNavigation>();
            if(nav==null||OmsMobileSession.IsReady)throw new InvalidOperationException("Review requires unauthenticated offline Editor.");
            var language=nav.GetComponent<ImportedAppLocalization>();var oldLanguage=JarvisLocalization.CurrentLanguage;string oldTheme=nav.Settings.Theme;int oldSize=PlayerPrefs.GetInt("JarVis.TextSize",1);
            nav.Show("5:331",false);yield return null;yield return null;
            var root=nav.screens.First(screen=>screen.id=="5:331").root;var scanner=root.GetComponent<ImportedCameraScanner>();
            Check(scanner.State==ScannerState.Idle,"Editor never opens physical camera");
            scanner.Submit(AndroidScannerReview.MilkCode);yield return null;
            Check(scanner.ScanLocked&&scanner.LastScan.ErrorCode=="SIGN_IN_REQUIRED","One scan locks recognition and never invents a warehouse product");
            Check(scanner.LastScan.RawValue==AndroidScannerReview.MilkCode,"Raw QR payload is unchanged");
            var accept=typeof(ImportedCameraScanner).GetMethod("Accept",BindingFlags.Instance|BindingFlags.NonPublic);
            accept.Invoke(scanner,new object[]{"DIFFERENT","QR_CODE","RearCamera"});
            Check(scanner.LastScan.RawValue==AndroidScannerReview.MilkCode,"Repeated camera frames cannot trigger another lookup");
            scanner.RestartScan();yield return null;Check(!scanner.ScanLocked&&scanner.LastScan==null,"Scan next explicitly unlocks and clears previous result");
            scanner.Submit("https://example.com");Check(scanner.LastScan.ErrorCode=="UNSUPPORTED_CONTENT","URL is rejected without opening browser");
            scanner.Submit(AndroidScannerReview.MilkCode);
            var unknown=scanner.LastScan;scanner.PresentReply(unknown,new OmsMobileApi.InventoryList{items=Array.Empty<OmsMobileApi.InventoryRow>()});Check(unknown.ErrorCode=="NOT_FOUND","Empty backend stock yields not found");
            var sample=new OmsMobileApi.InventoryList{synthetic=true,items=new[]{new OmsMobileApi.InventoryRow{productId="QA-MILK",sku=AndroidScannerReview.MilkCode,name="Thùng sữa 24 × 180 ml — QA fixture",unit="thùng",barcode=AndroidScannerReview.MilkCode,lotCode="QA-LOT",expiryDate="2027-02-01",locationCode="QA-A01",available=7}}};
            scanner.PresentReply(unknown,sample);Check(unknown.MatchedProductId=="QA-MILK","Fixture exact match shows actual reply fields");
            foreach(var locale in new[]{SupportedLanguage.Vi,SupportedLanguage.En})foreach(string theme in new[]{"light","dark"})
            {
                language.Select(locale);nav.Settings.SelectTheme(theme);PlayerPrefs.SetInt("JarVis.TextSize",2);yield return null;yield return null;
                PrototypeParityRunner.Capture(nav,"Scanner-"+locale+"-"+theme+".png",390,844);
                File.Copy("Logs/PrototypeParity/Scanner-"+locale+"-"+theme+".png",AndroidScannerReview.Folder+"/Scanner-"+locale+"-"+theme+".png",true);
                var text=root.GetComponentsInChildren<TMP_Text>().First(label=>label.name=="Camera status");
                Check(text.text.Contains(locale==SupportedLanguage.En?"TEST BACKEND DATA":"DỮ LIỆU THỬ NGHIỆM"),locale+" "+theme+" fixture disclosure translated");
                Check(!text.isTextOverflowing,locale+" "+theme+" result not clipped at large text");
                foreach(var button in root.GetComponentsInChildren<Button>())Check(((RectTransform)button.transform).rect.height>=44,"Touch target "+button.name);
                var next=root.GetComponentsInChildren<Button>().First(button=>button.name=="Scan again").GetComponentInChildren<TMP_Text>();
                Check(next!=null&&next.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(next.text),locale+" "+theme+" Scan next label is visible");
            }
            var ambiguous=ScannerValidation.Validate(AndroidScannerReview.MilkCode,"QR_CODE","Editor");
            scanner.PresentReply(ambiguous,new OmsMobileApi.InventoryList{items=new[]{sample.items[0],new OmsMobileApi.InventoryRow{productId="QA-OTHER",sku=AndroidScannerReview.MilkCode}}});Check(ambiguous.ErrorCode=="AMBIGUOUS_MATCH"&&ambiguous.MatchedProductId==null,"Duplicate product identifiers are not silently selected");
            for(int index=0;index<10;index++){nav.Show("126:8",false);yield return null;Check(scanner.State==ScannerState.Released&&scanner.LastScan==null,"Close releases scanner and sensitive result "+index);nav.Show("5:331",false);yield return null;Check(!scanner.ScanLocked,"Reopen scanner unlocks "+index);}
            PlayerPrefs.SetInt("JarVis.TextSize",oldSize);language.Select(oldLanguage);nav.Settings.SelectTheme(oldTheme);
            Application.logMessageReceived-=Error;
            File.WriteAllText(AndroidScannerReview.Folder+"/UI.txt",(errors.Count==0?"PASS":"FAIL")+"\n"+DateTime.UtcNow.ToString("O")+"\nOffline Editor fixtures only; no camera, network, real account, stock mutation or device acceptance.\n"+string.Join("\n",checks)+"\nErrors:\n"+string.Join("\n",errors));
            if(Application.isBatchMode)EditorApplication.Exit(errors.Count==0?0:1);else EditorApplication.isPlaying=false;
        }
    }
}
