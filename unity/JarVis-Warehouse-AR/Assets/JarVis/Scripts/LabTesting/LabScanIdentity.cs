using System;

namespace JarVis.Warehouse.Lab
{
    public static class LabScanIdentity
    {
        public static string PayloadId(string payload)
        {
            return payload!=null&&payload.Length==10&&payload.StartsWith("JWM:MAIN:",StringComparison.Ordinal)&&payload[9]>='A'&&payload[9]<='E'?payload.Substring(9,1):null;
        }
        // Reference-image detections must never be used as the count of physical QR cards.
        public static bool Accepts(string wanted,string decoded,int physicalQrIds,double now,double capturedAt)
        {
            return LabPrintedCard.Id(wanted)&&decoded==wanted&&physicalQrIds==1&&now>=capturedAt&&now-capturedAt<=.85;
        }
    }
}
