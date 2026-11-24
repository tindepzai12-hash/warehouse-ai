using System;
using UnityEngine;

namespace JarVis.Warehouse.Backend
{
    // Explicit owner-authorized test catalogue. It is not a production stock record.
    public static class JarvisTestProduct
    {
        [Serializable] public sealed class Record
        {
            public string barcode, name, location, manufacturedOn, receivedOn, expiresOn;
            public string provenance;
            public int volumeMl, kcal, calciumMg, stockQuantity;
        }
        static Record cached;
        public static Record Milk => cached ?? (cached = JsonUtility.FromJson<Record>(Resources.Load<TextAsset>("JarvisTestProduct").text));
        public static bool Matches(string code) => string.Equals(code, Milk.barcode, StringComparison.Ordinal);
        public static string Describe(bool english)
        {
            var p = Milk;
            return english
                ? "OWNER TEST RECORD · not live warehouse stock\n" + "FreshDelight chocolate milk" + " · " + p.volumeMl + " ml\nBarcode: " + p.barcode + "\nLocation: " + p.location + " (lab map)\nManufactured: 14/06/2027\nReceived: 10/11/2027\nExpires: 12/12/2027\n128 kcal · calcium 120 mg per carton\nStock quantity: " + p.stockQuantity + ""
                : "MẪU KIỂM THỬ · chưa phải tồn kho thật\n" + "Sữa sô-cô-la FreshDelight" + " · " + p.volumeMl + " ml\nBarcode: " + p.barcode + "\nVị trí: " + p.location + " (map lab)\nNSX: 14/06/2027\nNhập kho: 10/11/2027\nHSD: 12/12/2027\n128 kcal · canxi 120 mg/hộp\nSố lượng tồn kho: " + p.stockQuantity + "";
        }
        public static string VerifiedContext(string question,string verifiedCode,bool english)
        {
            if(!Matches(verifiedCode))return question??"";
            var p=Milk;
            string record="barcode="+p.barcode+"; name="+p.name+"; location="+p.location+"; stockQuantity="+p.stockQuantity+"; volumeMl="+p.volumeMl+"; manufacturedOn="+p.manufacturedOn+"; receivedOn="+p.receivedOn+"; expiresOn="+p.expiresOn;
            return (question??"")+ (english?
                "\n[App catalogue record matched by exact barcode; owner test catalogue, not live OMS. Answer product/stock/location questions from this record. stockQuantity is the supplied stock count, not unknown. Do not claim an OMS stock transaction. Record: ":
                "\n[Bản ghi danh mục app khớp chính xác barcode; danh mục mẫu do chủ app cung cấp, chưa đồng bộ OMS. Dùng bản ghi để trả lời tên, vị trí, tồn kho. stockQuantity là số lượng đã khai báo, không phải chưa biết. Không nói đã ghi giao dịch OMS. Bản ghi: ")+record+"]";
        }
        public static string PhotoContext(string question,bool english=false)
        {
            // Keep within the server's 1000-character message contract.
            string q = string.IsNullOrWhiteSpace(question)?(english?"Describe the product and read its label in English. State what is unclear.":"Hãy đọc chữ và mô tả sản phẩm nhìn thấy trong ảnh, bằng tiếng Việt. Nêu điều chưa đọc rõ."):question;
            if (q.Length > 420) return q; // Preserve the full user question; never silently truncate it.
            if(english)return q+"\n[Owner test data, not live stock: FreshDelight chocolate milk 200ml; barcode 4710043004342. Manufactured 14/06/2027; received 10/11/2027; expires 12/12/2027; location B on lab map. Use only if the image matches this product. These dates/location are owner-supplied, not OCR. Analyze other products as shown. Do not claim to read a barcode from a front-only photo. Answer directly, no generic confirmation request or disclaimer.]";
            return q + "\n[Thông tin mẫu kiểm thử do chủ app cung cấp: FreshDelight 福樂 巧克力牛乳 sữa sô-cô-la 200ml, barcode 4710043004342. NSX 14/06/2027; nhập kho 10/11/2027; HSD 12/12/2027; vị trí B map lab. Chỉ dùng metadata này nếu nội dung ảnh thực sự khớp sản phẩm, không đoán khi chưa rõ. Các ngày/vị trí do người dùng khai báo, không phải OCR, không phải tồn kho thật. Ảnh sản phẩm khác phải phân tích đúng ảnh khác. Nếu ảnh chỉ có mặt trước, không nói đã đọc barcode từ ảnh. Trả lời thẳng, không kết bằng yêu cầu xác nhận hay chú thích chung.]";
        }
    }
}
