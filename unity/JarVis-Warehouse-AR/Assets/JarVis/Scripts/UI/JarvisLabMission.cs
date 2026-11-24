using System;
using System.IO;
using JarVis.Warehouse.Backend;
using UnityEngine;
using TMPro;
using JarVis.Warehouse.Localization;

namespace JarVis.Warehouse.UI
{
    // A local, explicitly labelled trial. It never posts an OMS stock transaction.
    public static class JarvisLabMission
    {
        [Serializable] public sealed class Record
        {
            public string state="await_product", barcode="4710043004342", origin="A", target="B", mapId, updatedUtc;
            public int mapRevision;
        }
        static Record record;
        static string PathName=>Path.Combine(Application.persistentDataPath,"jarvis-milk-trial.json");
        public static Record Current
        {
            get
            {
                if(record!=null)return record;
                try{if(File.Exists(PathName)){var v=JsonUtility.FromJson<Record>(File.ReadAllText(PathName));if(v!=null&&v.barcode=="4710043004342"&&v.origin=="A"&&v.target=="B"&&(v.state=="await_product"||v.state=="carrying"||v.state=="complete"))record=v;}}
                catch(Exception){Debug.LogWarning("[JarVis Lab] Trial record unavailable; no prior AR pose will be restored.");}
                return record??(record=new Record());
            }
        }
        public static bool Armed {get;private set;}
        public static string Status=>Current.state=="complete"?"Đã xác nhận giao tại B · MẪU THỬ":Current.state=="carrying"?"Đã đọc sữa tại A · Đích B · MẪU THỬ":"Quét barcode sữa tại A · MẪU THỬ";
        public static void Open(ImportedFigmaNavigation navigation)
        {
            if(Current.state=="carrying"){JarvisLabLauncher.Open();return;}
            record=new Record();Save();Armed=true;
            navigation.Show("5:331");
            navigation.Notice("Nhiệm vụ thử A → B: quét barcode 4710043004342 của hộp sữa tại A. Không dùng QR quảng cáo trên hộp.",false,10);
        }
        public static bool AcceptProduct(string code)
        {
            if(!Armed||!JarvisTestProduct.Matches(code))return false;
            Current.state="carrying";Armed=false;Save();return true;
        }
        public static bool BindMap(string id,int revision)
        {
            if(!string.IsNullOrEmpty(Current.mapId)&&(Current.mapId!=id||Current.mapRevision!=revision))return false;
            Current.mapId=id;Current.mapRevision=revision;Save();return true;
        }
        public static bool Complete(bool aligned,float distance,string target)
        {
            if(Current.state!="carrying"||!aligned||target!="B"||distance>.8f||float.IsNaN(distance))return false;
            Current.state="complete";Save();return true;
        }
        public static RectTransform Card(Transform parent,TMP_FontAsset font,ImportedFigmaNavigation nav,float y)
        {
            bool en=ImportedAppLocalization.IsEnglish,dark=nav.Settings.Dark;
            var card=PrototypeUi.Panel(parent,"Milk A to B trial",0,y,366,174,dark,20);
            PrototypeUi.Text(card,font,en?"LOCAL TEST MISSION":"NHIỆM VỤ THỬ",18,14,330,22,11,dark,true);
            PrototypeUi.Text(card,font,en?"Move milk · A → B":"Chuyển hộp sữa · A → B",18,41,330,28,17,dark,true);
            PrototypeUi.Text(card,font,en?"Scan the milk barcode at A. Dates and B are your test data.":"Quét barcode hộp sữa tại A. Ngày và vị trí B là dữ liệu bạn đặt để thử.",18,76,330,44,12,dark,false,true);
            PrototypeUi.Action(card,font,Current.state=="carrying"?(en?"Resume route to B":"Tiếp tục tới B"):(en?"Start test mission":"Bắt đầu nhiệm vụ thử"),18,126,330,36,dark,()=>Open(nav),true);
            return card;
        }
        public static void ShowProduct(ImportedFigmaNavigation nav)
        { if(nav!=null) JarvisLocalCatalogue.Show(nav,JarvisLocalCatalogue.Find("4710043004342")); }
        static void Save()
        {
            Current.updatedUtc=DateTime.UtcNow.ToString("O");
            try{string tmp=PathName+".tmp";File.WriteAllText(tmp,JsonUtility.ToJson(Current,true));if(File.Exists(PathName))File.Replace(tmp,PathName,null);else File.Move(tmp,PathName);}
            catch(Exception){Debug.LogWarning("[JarVis Lab] Trial progress could not be saved. No stock was changed.");}
        }
    }
}
