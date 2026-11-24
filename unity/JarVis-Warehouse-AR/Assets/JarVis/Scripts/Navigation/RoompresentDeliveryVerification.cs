using JarVis.Warehouse.UI;
using ZXing;
using ZXing.Common;

namespace JarVis.Warehouse.Navigation
{
    public enum DeliveryScanOutcome { Unavailable, WrongDestination, WrongProduct, DestinationVerified, Completed }

    // Ephemeral proof: cancellation, pause and tracking loss discard unfinished scans.
    public sealed class RoompresentDeliveryVerification
    {
        public string Stop {get;private set;}
        public bool WaitingForProduct {get;private set;}
        string qr;
        public bool Active=>Stop!=null;
        public string ProductName=>Stop=="B"?"A1":"A2";
        public bool Begin(RoompresentDeliveryRecord record,string stop)
        {
            Cancel();
            if(!record.accepted||!record.Loaded||record.Complete||(stop!="B"&&stop!="C")||stop=="B"&&record.deliveredB||stop=="C"&&record.deliveredC)return false;
            Stop=stop;return true;
        }
        public void Cancel(){Stop=null;qr=null;WaitingForProduct=false;}
        public DeliveryScanOutcome Observe(RoompresentDeliveryRecord record,string code,string source,bool tracking,float distance)
        {
            if(!Active||source!="RearCamera")return DeliveryScanOutcome.Unavailable;
            if(!tracking||!float.IsFinite(distance)||distance<0||distance>1.2f){qr=null;WaitingForProduct=false;return DeliveryScanOutcome.Unavailable;}
            if(!WaitingForProduct)
            {
                if(RoompresentDeliveryRecord.Destination(code)!=Stop)return DeliveryScanOutcome.WrongDestination;
                qr=code;WaitingForProduct=true;return DeliveryScanOutcome.DestinationVerified;
            }
            if(code!=(Stop=="B"?RoompresentDeliveryRecord.A1:RoompresentDeliveryRecord.A2))return DeliveryScanOutcome.WrongProduct;
            if(!record.Confirm(qr,Stop,tracking,distance,code)){Cancel();return DeliveryScanOutcome.Unavailable;}
            Cancel();return DeliveryScanOutcome.Completed;
        }
        public static Result DecodeProduct(byte[] gray,int width,int height)
        {
            var reader=new BarcodeReaderGeneric{AutoRotate=true,Options=new DecodingOptions{TryHarder=true,PossibleFormats=new[]{BarcodeFormat.EAN_13,BarcodeFormat.CODE_128,BarcodeFormat.CODE_39}}};
            // Scan central horizontal bands first. A small code near the centre must not
            // be missed because the full-screen decoder samples a different scanline.
            foreach(float fraction in new[]{.24f,.48f,1f})
            {
                int h=System.Math.Max(32,(int)(height*fraction)),top=(height-h)/2;
                var pixels=new byte[width*h];System.Array.Copy(gray,top*width,pixels,0,pixels.Length);
                var result=reader.Decode(new RGBLuminanceSource(pixels,width,h,RGBLuminanceSource.BitmapFormat.Gray8));
                if(result!=null)return result;
            }
            return null;
        }
    }
}
