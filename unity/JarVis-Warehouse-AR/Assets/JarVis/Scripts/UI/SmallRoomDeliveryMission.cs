using System;
using System.IO;
using TMPro;
using UnityEngine;
using JarVis.Warehouse.Localization;

namespace JarVis.Warehouse.UI
{
    [Serializable]
    public sealed class SmallRoomDeliveryRecord
    {
        public const string A1 = "4714551570610", A2 = "4714551570603";
        public const string Venue = "scaniverse-20261119-225317";
        public int schema = 1;
        public string venue = Venue;
        public bool accepted, scannedA1, scannedA2, deliveredB, deliveredC;
        public bool Loaded => scannedA1 && scannedA2;
        public bool Complete => deliveredB && deliveredC;
        public bool ScanProduct(string code)
        {
            if (!accepted || Complete) return false;
            if (code == A1) { scannedA1 = true; return true; }
            if (code == A2) { scannedA2 = true; return true; }
            return false;
        }
        public static string Destination(string code)
        {
            if (code == "JWM:SMALLROOM:225317:B" || code == "JWM:MAIN:B" || code == "JWM:ROOMPRESENT:153613:B") return "B";
            if (code == "JWM:SMALLROOM:225317:C" || code == "JWM:MAIN:C" || code == "JWM:ROOMPRESENT:153613:C") return "C";
            return null;
        }
        public bool Confirm(string code, string active, bool tracking, float distance)
        {
            string stop = Destination(code);
            if (!accepted || !Loaded || Complete || !tracking || !float.IsFinite(distance) || distance < 0 || distance > 1.2f || stop == null || stop != active) return false;
            if (stop == "B" && !deliveredB) { deliveredB = true; return true; }
            if (stop == "C" && !deliveredC) { deliveredC = true; return true; }
            return false;
        }
    }

    // Separate venue/progress from Roompresent and real OMS stock.
    public static class SmallRoomDeliveryMission
    {
        static SmallRoomDeliveryRecord current;
        public static bool Armed { get; private set; }
        static string FilePath => Path.Combine(Application.persistentDataPath, "jarvis-smallroom-225317-delivery-v1.json");
        public static SmallRoomDeliveryRecord Current
        {
            get
            {
                if (current != null) return current;
                try
                {
                    if (File.Exists(FilePath))
                    {
                        var saved = JsonUtility.FromJson<SmallRoomDeliveryRecord>(File.ReadAllText(FilePath));
                        if (saved != null && saved.schema == 1 && saved.venue == SmallRoomDeliveryRecord.Venue &&
                            (!saved.deliveredB && !saved.deliveredC || saved.accepted && saved.Loaded)) current = saved;
                    }
                }
                catch (Exception) { Debug.LogWarning("[SmallRoom] Local task could not be restored."); }
                return current ?? (current = new SmallRoomDeliveryRecord());
            }
        }
        public static string Summary => Current.Complete ? "A1 → B ✓   A2 → C ✓" : $"A1 {(Current.scannedA1 ? "✓" : "○")}  A2 {(Current.scannedA2 ? "✓" : "○")}";
        public static void Save()
        {
            try { string temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonUtility.ToJson(Current)); if (File.Exists(FilePath)) File.Replace(temp, FilePath, null); else File.Move(temp, FilePath); }
            catch (Exception) { Debug.LogWarning("[SmallRoom] Progress could not be saved; no stock changed."); }
        }
        public static void Disarm() { Armed=false; }
        public static void Open(ImportedFigmaNavigation nav)
        {
            RoompresentDeliveryMission.Disarm();
            if (Current.Complete) current = new SmallRoomDeliveryRecord();
            Current.accepted = true; Save();
            if (Current.Loaded) { Armed = false; SmallRoomMapLauncher.Open(nav); return; }
            nav.Show("5:331"); Armed = true;
            nav.Notice(ImportedAppLocalization.IsEnglish ? "At door A: scan A1 and A2. AR opens automatically; face basin B." : "Tại cửa A: quét A1 và A2. AR tự mở; hướng máy về chậu B.", false, 8);
        }
        public static bool HandleScan(string code, string source, ImportedFigmaNavigation nav, out string vi, out string en)
        {
            vi = en = null;
            if (!Armed) return false;
            if (source != "RearCamera") { vi = "Quét barcode sách bằng camera."; en = "Scan the books with the camera."; return true; }
            if (!Current.ScanProduct(code)) { vi = "Sai sách: cần A1 / CPNB-1004 và A2 / CPNB-1003."; en = "Wrong book: use A1 / CPNB-1004 and A2 / CPNB-1003."; return true; }
            Save(); Debug.Log("[SmallRoom] Book verified " + (code == SmallRoomDeliveryRecord.A1 ? "A1" : "A2") + "; both=" + Current.Loaded);
            vi = Current.Loaded ? "Đủ hai sách · đang mở AR." : "Đã nhận sách · quét quyển còn lại.";
            en = Current.Loaded ? "Both books ready · opening AR." : "Book verified · scan the other book.";
            if (Current.Loaded) { Armed = false; SmallRoomMapLauncher.Open(nav); }
            return true;
        }
        public static void Card(Transform parent, TMP_FontAsset font, ImportedFigmaNavigation nav, float y)
        {
            bool en = ImportedAppLocalization.IsEnglish, dark = nav.Settings.Dark;
            var card = PrototypeUi.Panel(parent, "Small room book delivery", 0, y, 366, 190, dark, 20);
            PrototypeUi.Text(card, font, en ? "SMALL ROOM · TEST MISSION" : "MAP PHÒNG NHỎ · NHIỆM VỤ THỬ", 18, 12, 330, 22, 11, dark, true);
            PrototypeUi.Text(card, font, "A1 → B  •  A2 → C", 18, 39, 330, 29, 21, dark, true);
            PrototypeUi.Text(card, font, en ? "• A: at the door, facing basin B\n• Scan both books; AR opens automatically\n• B: basin · C: black bag" : "• A: trước cửa, hướng về chậu B\n• Quét hai sách, AR tự mở và dẫn đường\n• B: chậu đỏ/xanh · C: túi đen", 18, 73, 330, 67, 12, dark);
            PrototypeUi.Action(card, font, Current.Complete ? (en ? "Repeat test" : "Thử lại nhiệm vụ") : Current.accepted ? (en ? "Continue · " : "Tiếp tục · ") + Summary : (en ? "Scan A1 + A2 to start" : "Quét A1 + A2 để bắt đầu"), 18, 143, 330, 36, dark, () => Open(nav), true);
        }
    }
}
