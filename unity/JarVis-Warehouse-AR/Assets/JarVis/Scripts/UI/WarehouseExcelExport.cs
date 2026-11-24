using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JarVis.Warehouse.Localization;
namespace JarVis.Warehouse.UI
{
    public sealed class WarehouseExcelExport:MonoBehaviour
    {
        ImportedFigmaNavigation nav;bool inbound;string appearance;RectTransform button;
        public void Configure(ImportedFigmaNavigation owner,bool isInbound){nav=owner;inbound=isInbound;Build();}
        void Update(){if(nav!=null&&appearance!=nav.Settings.Dark+ImportedAppLocalization.IsEnglish.ToString())Build();}
        void Build()
        {
            if(button!=null){button.gameObject.SetActive(false);Destroy(button.gameObject);}appearance=nav.Settings.Dark+ImportedAppLocalization.IsEnglish.ToString();bool dark=nav.Settings.Dark;
            var font=GetComponentInChildren<TMP_Text>(true).font;
            var action=PrototypeUi.Action(transform,font,ImportedAppLocalization.IsEnglish?"Export to Excel":"Xuất sang Excel",23,758,366,44,dark,Export);
            button=(RectTransform)action.transform;button.name="Export Excel";
            var icon=JarVisUi.Rect(button,"Excel icon",92,12,20,20).gameObject.AddComponent<ExcelFileIcon>();icon.raycastTarget=false;
            var label=button.GetComponentInChildren<TMP_Text>();label.rectTransform.anchoredPosition=new Vector2(120,0);label.rectTransform.sizeDelta=new Vector2(220,44);label.alignment=TextAlignmentOptions.MidlineLeft;
        }
        public static void Attach(ImportedFigmaNavigation nav)
        {
            foreach(var s in nav.screens.Where(s=>s.id=="5:935"||s.id=="14:387"))
                (s.root.GetComponent<WarehouseExcelExport>()??s.root.AddComponent<WarehouseExcelExport>()).Configure(nav,s.id=="5:935");
        }
        public void Export()
        {
            try
            {
                var fields=GetComponentsInChildren<TMP_InputField>().Select(f=>new[]{f.name,f.text}).ToArray();
                string filename="JarVis-"+(inbound?"Inbound":"Outbound")+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".xlsx";
                string path=Path.Combine(Application.persistentDataPath,filename);File.WriteAllBytes(path,CreateWorkbook(inbound,fields));
#if UNITY_ANDROID && !UNITY_EDITOR
                using(var player=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=player.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var exporter=new AndroidJavaClass("com.jarvis.warehouse.JarvisExcelDownloads"))
                {
                    string result=exporter.CallStatic<string>("save",activity,path);
                    if(!result.StartsWith("OK:"))throw new IOException(result);
                    path="Download/JarVis/"+filename;
                }
#endif
                nav.Notice((ImportedAppLocalization.IsEnglish?"Excel saved: ":"Đã lưu Excel: ")+path,false,10);
            }
            catch(Exception e){nav.Notice((ImportedAppLocalization.IsEnglish?"Excel export failed: ":"Chưa xuất được Excel: ")+e.GetType().Name,true,8);}
        }
        public static byte[] CreateWorkbook(bool inbound,string[][] fields)
        {
            string[][] products=new[]{new[]{"Mã sản phẩm","Tên sản phẩm","Barcode","Màu sắc","Số lượng tồn kho","Vị trí","Lô mẫu","Hạn sử dụng","Nguồn"}}
                .Concat(JarvisLocalCatalogue.Items.Select(p=>new[]{p.sku,p.name,p.barcode,p.color,p.quantity.ToString(),JarvisLocalCatalogue.Location(p,false),p.lot,p.expiry??"Không áp dụng","Danh mục mẫu do người dùng cung cấp"})).ToArray();
            string[][] form=new[]{new[]{"Trường","Nội dung"},new[]{"Màn hình",inbound?"Nhập kho":"Xuất kho"},new[]{"Trạng thái","Bản xuất biểu mẫu và danh mục; không xác nhận giao dịch kho"},new[]{"Thời điểm",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}}.Concat(fields??Array.Empty<string[]>()).ToArray();
            using(var stream=new MemoryStream())
            {
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
                {
                    void Write(string name,string data){using(var writer=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))writer.Write(data);}
                    Write("[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/worksheets/sheet2.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>");
                    Write("_rels/.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    Write("xl/workbook.xml","<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Sản phẩm\" sheetId=\"1\" r:id=\"rId1\"/><sheet name=\"Biểu mẫu\" sheetId=\"2\" r:id=\"rId2\"/></sheets></workbook>");
                    Write("xl/_rels/workbook.xml.rels","<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet2.xml\"/></Relationships>");
                    Write("xl/worksheets/sheet1.xml",Sheet(products));Write("xl/worksheets/sheet2.xml",Sheet(form));
                }
                return stream.ToArray();
            }
        }
        static string Sheet(string[][] rows)
        {
            var text=new StringBuilder();using(var x=XmlWriter.Create(text,new XmlWriterSettings{OmitXmlDeclaration=true,CheckCharacters=true}))
            {
                x.WriteStartElement("worksheet","http://schemas.openxmlformats.org/spreadsheetml/2006/main");
                x.WriteStartElement("sheetViews");x.WriteStartElement("sheetView");x.WriteAttributeString("workbookViewId","0");x.WriteStartElement("pane");x.WriteAttributeString("ySplit","1");x.WriteAttributeString("topLeftCell","A2");x.WriteAttributeString("state","frozen");x.WriteEndElement();x.WriteEndElement();x.WriteEndElement();
                x.WriteStartElement("cols");x.WriteStartElement("col");x.WriteAttributeString("min","1");x.WriteAttributeString("max","9");x.WriteAttributeString("width","27");x.WriteAttributeString("customWidth","1");x.WriteEndElement();x.WriteEndElement();
                x.WriteStartElement("sheetData");for(int r=0;r<rows.Length;r++){x.WriteStartElement("row");x.WriteAttributeString("r",(r+1).ToString());for(int c=0;c<rows[r].Length;c++){x.WriteStartElement("c");x.WriteAttributeString("r",((char)('A'+c)).ToString()+(r+1));x.WriteAttributeString("t","inlineStr");x.WriteStartElement("is");x.WriteElementString("t",rows[r][c]??"");x.WriteEndElement();x.WriteEndElement();}x.WriteEndElement();}x.WriteEndElement();x.WriteEndElement();
            }return text.ToString();
        }
    }
    [RequireComponent(typeof(CanvasRenderer))] public sealed class ExcelFileIcon:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper v)
        {
            v.Clear();var r=rectTransform.rect;var green=new Color32(16,124,65,255);
            void Quad(float x,float y,float w,float h,Color32 color){int n=v.currentVertCount;v.AddVert(new Vector2(r.x+x*r.width,r.y+y*r.height),color,Vector2.zero);v.AddVert(new Vector2(r.x+(x+w)*r.width,r.y+y*r.height),color,Vector2.zero);v.AddVert(new Vector2(r.x+(x+w)*r.width,r.y+(y+h)*r.height),color,Vector2.zero);v.AddVert(new Vector2(r.x+x*r.width,r.y+(y+h)*r.height),color,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);}
            Quad(.28f,.02f,.68f,.96f,green);for(int i=0;i<3;i++)Quad(.58f,.22f+i*.24f,.28f,.07f,new Color32(187,230,205,255));Quad(0,.18f,.6f,.64f,new Color32(11,96,51,255));
            void Stroke(Vector2 a,Vector2 b){var d=(b-a).normalized;var s=new Vector2(-d.y,d.x)*.05f;int n=v.currentVertCount;foreach(var p in new[]{a+s,b+s,b-s,a-s})v.AddVert(new Vector2(r.x+p.x*r.width,r.y+p.y*r.height),Color.white,Vector2.zero);v.AddTriangle(n,n+1,n+2);v.AddTriangle(n,n+2,n+3);}
            Stroke(new Vector2(.15f,.34f),new Vector2(.45f,.66f));Stroke(new Vector2(.15f,.66f),new Vector2(.45f,.34f));
        }
    }
}
