using System;
using System.IO;
using TMPro;
using UnityEngine;
using JarVis.Warehouse.Localization;

namespace JarVis.Warehouse.UI
{
    // Isolated owner trial. No OMS task or inventory transaction is posted.
    [Serializable] public sealed class RoompresentDeliveryRecord
    {
        public const string A1 = "4714551570610", A2 = "4714551570603";
        public int schema = 1, mapId = 153613;
        public bool accepted, scannedA1, scannedA2, deliveredB, deliveredC;
        public string route = "AUTO", preferredStop;
        public bool Loaded => scannedA1 && scannedA2;
        public bool Complete => deliveredB && deliveredC;
        public bool ScanProduct(string code)
        {
            if (!accepted || Loaded) return false;
            if (code == A1) { scannedA1 = true; return true; }
            if (code == A2) { scannedA2 = true; return true; }
            return false;
        }
        public static string Destination(string payload)
        {
            if (payload == "JWM:ROOMPRESENT:153613:B" || payload == "JWM:MAIN:B") return "B";
            if (payload == "JWM:ROOMPRESENT:153613:C" || payload == "JWM:MAIN:C") return "C";
            return null;
        }
        public bool Confirm(string payload, string activeStop, bool tracking, float distance, string product = null)
        {
            string stop = Destination(payload);
            if (product != (stop == "B" ? A1 : stop == "C" ? A2 : "") || !accepted || !Loaded || Complete || !tracking || float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0 || distance > 1.2f || stop != activeStop) return false;
            if (stop == "B" && !deliveredB) { deliveredB = true; preferredStop = null; return true; }
            if (stop == "C" && !deliveredC) { deliveredC = true; preferredStop = null; return true; }
            return false;
        }
    }

    // Deliberately not serialized: saved task progress is never scan proof for a new visit.
    public sealed class RoompresentBarcodeGate
    {
        bool a1,a2,acknowledged;
        public bool Complete=>a1&&a2;
        public bool Ready=>Complete&&acknowledged;
        public void Acknowledge(){acknowledged=true;}
        public bool Contains(string code)=>code==RoompresentDeliveryRecord.A1?a1:code==RoompresentDeliveryRecord.A2&&a2;
        public bool Observe(string code,string source)
        {
            if(source!="RearCamera")return false;
            if(code==RoompresentDeliveryRecord.A1){a1=true;acknowledged=false;return true;}
            if(code==RoompresentDeliveryRecord.A2){a2=true;acknowledged=false;return true;}
            return false;
        }
        public void Reset(){a1=a2=acknowledged=false;}
    }

    public static class RoompresentDeliveryMission
    {
        static RoompresentDeliveryRecord current;
        static readonly RoompresentBarcodeGate barcodeGate=new RoompresentBarcodeGate();
        public static bool AwaitingAcknowledgement {get;private set;}
        public static bool FreshScannedA1=>barcodeGate.Contains(RoompresentDeliveryRecord.A1);
        public static bool FreshScannedA2=>barcodeGate.Contains(RoompresentDeliveryRecord.A2);
        public static string ScanReminder(bool english)
        {
            if(!Armed||!FreshScannedA1&&!FreshScannedA2)return null;
            if(FreshScannedA1&&FreshScannedA2)return english?"A1 and A2 scanned · 2/2 books\nTap OK to scan QR A":"Đã quét quyển vở A1 và A2 · 2/2\nBấm OK để quét QR A";
            string done=FreshScannedA1?"A1":"A2",next=FreshScannedA1?"A2":"A1";
            return english?"Book "+done+" scanned · 1/2\nNext: scan book "+next:"Đã quét quyển vở "+done+" · 1/2\nTiếp theo: quét quyển vở "+next;
        }
        public static bool CanOpenGuidance=>!AwaitingAcknowledgement&&barcodeGate.Ready&&Current.Loaded&&Current.accepted;
        public static void EndGuidance(){barcodeGate.Reset();AwaitingAcknowledgement=false;Armed=false;}
        public static bool Armed { get; private set; }
        static string FilePath => Path.Combine(Application.persistentDataPath, "jarvis-roompresent-quick-delivery-v1.json");
        public static RoompresentDeliveryRecord Current
        {
            get
            {
                if (current != null) return current;
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var saved = JsonUtility.FromJson<RoompresentDeliveryRecord>(File.ReadAllText(FilePath));
                        if (saved != null && saved.schema == 1 && saved.mapId == 153613 &&
                            (!saved.deliveredB && !saved.deliveredC || saved.Loaded) &&
                            (saved.route == "AUTO" || saved.route == "B" || saved.route == "C")) current = saved;
                    }
                }
                catch (Exception) { Debug.LogWarning("[Roompresent] Test progress could not be read."); }
                return current ?? (current = new RoompresentDeliveryRecord());
            }
        }
        public static string Summary => Current.Complete ? "A1 → B ✓   A2 → C ✓" : Current.Loaded ? "A1 → B  •  A2 → C" : $"A1 {(Current.scannedA1 ? "✓" : "○")}   A2 {(Current.scannedA2 ? "✓" : "○")}";
        public static void Save()
        {
            try { string temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonUtility.ToJson(Current)); if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath); }
            catch (Exception) { Debug.LogWarning("[Roompresent] Test progress was not saved. Stock unchanged."); }
        }
        public static void Open(ImportedFigmaNavigation nav)
        {
            SmallRoomDeliveryMission.Disarm();
            if (Current.Complete) current = new RoompresentDeliveryRecord();
            Begin(nav);
        }
        public static float CardHeight => 200;
        public static void Restart(ImportedFigmaNavigation nav)
        {
            // Explicit restart of the isolated presentation trial only.
            current=new RoompresentDeliveryRecord();
            SmallRoomDeliveryMission.Disarm();
            Debug.Log("[Roompresent] User restarted trial; camera verification of both books required.");
            Begin(nav);
        }
        static void Begin(ImportedFigmaNavigation nav)
        {
            Current.accepted = true; Current.route="AUTO"; Current.preferredStop=null; Save();
            barcodeGate.Reset();AwaitingAcknowledgement=false;
            nav.Show("5:331"); Armed = true;
            nav.Notice(ImportedAppLocalization.IsEnglish ? "Scan A1 and A2 at the start table. Then scan QR A to align the room." : "Quét A1 và A2 tại bàn xuất phát. Sau đó quét QR A để căn phòng.", false, 10);
        }
        public static void Disarm() { Armed=false; AwaitingAcknowledgement=false; }
        public static bool HandleScan(string code, string source, ImportedFigmaNavigation nav, out string vi, out string en, Action resume = null)
        {
            vi = en = null;
            if (!Armed) return false;
            if(AwaitingAcknowledgement)return true;
            if (source != "RearCamera") { vi = "Nhiệm vụ thử yêu cầu quét bằng camera."; en = "Use the camera to verify the test products."; return true; }
            if (code != RoompresentDeliveryRecord.A1 && code != RoompresentDeliveryRecord.A2)
            { vi = "Sai sản phẩm. Cần A1: CPNB-1004 hoặc A2: CPNB-1003."; en = "Wrong product. Scan A1: CPNB-1004 or A2: CPNB-1003."; return true; }
            if(barcodeGate.Contains(code)){vi="Sản phẩm này đã được quét. Quét quyển còn lại.";en="This item was already scanned. Scan the other book.";return true;}
            barcodeGate.Observe(code,source);Current.ScanProduct(code); Save();
            AwaitingAcknowledgement=true;
            bool both=barcodeGate.Complete;string book=code==RoompresentDeliveryRecord.A1?"A1":"A2";
            Debug.Log("[Roompresent] Book verified "+book+"; freshBoth="+both+"; awaiting OK");
            RoompresentScanPopup.Show(nav,code,both,()=>{
                AwaitingAcknowledgement=false;
                if(!Armed)return;
                barcodeGate.Acknowledge();
                if(CanOpenGuidance){Armed=false;RoompresentQuickMapLauncher.Open(nav);}
                else resume?.Invoke();
            });
            vi="Quét thành công "+book+". Bấm OK để tiếp tục.";
            en="Scanned "+book+". Tap OK to continue.";
            return true;
        }
        public static void Card(Transform parent, TMP_FontAsset font, ImportedFigmaNavigation nav, float y)
        {
            bool en = ImportedAppLocalization.IsEnglish, dark = nav.Settings.Dark;
            var card = PrototypeUi.Panel(parent, "Roompresent two deliveries", 0, y, 366, CardHeight, dark, 20);
            PrototypeUi.Text(card, font, en ? "MISSION #00001 PUTAWAY" : "NHIỆM VỤ #00001 CẤT HÀNG", 18, 12, 282, 24, 13, dark, true);
            var reset=JarVisUi.Rect(card,"Reset mission",304,4,44,44);
            PrototypeUi.Icon(reset,"refresh",13,13,18,PrototypeUi.Muted(dark));
            PrototypeUi.Button(reset,()=>PrototypeDialog.Show(nav,en?"Reset mission?":"Đặt lại nhiệm vụ?",en?"Clear delivery progress for A1/A2. Saved map registration stays unchanged.":"Xóa tiến độ giao A1/A2 để làm lại. Giữ nguyên tọa độ map đã lưu.",en?"Reset":"Đặt lại",en?"Cancel":"Hủy",()=>Restart(nav)));
            PrototypeUi.Text(card, font, "A1 → B  •  A2 → C", 18, 39, 330, 29, 21, dark, true);
            PrototypeUi.Text(card, font, en ? "• Scan the A1 and A2 product barcodes\n• Scan QR A to align your current location\n• Deliver A1 to B and A2 to C; verify each pair" : "• Quét barcode hai sản phẩm A1 và A2\n• Quét QR A để căn vị trí hiện tại\n• Giao A1 tới B, A2 tới C và xác thực từng cặp", 18, 73, 330, 67, 12, dark);
            var start=PrototypeUi.Action(card, font, Current.Complete ? (en ? "Mission completed" : "Đã hoàn thành nhiệm vụ") : (en ? "Start mission" : "Bắt đầu nhiệm vụ"), 18, 148, 330, 36, dark, () => Open(nav), true);
            start.interactable=!Current.Complete;

        }
    }
}
