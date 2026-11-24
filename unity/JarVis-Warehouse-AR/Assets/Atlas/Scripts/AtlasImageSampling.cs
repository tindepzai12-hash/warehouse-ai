using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace AtlasAR
{
    public static class AtlasImageSampling
    {
        public static bool Copy(byte[] raw,int rawWidth,int rawHeight,ref byte[] pixels,out int width,out int height)
        {
            width=height=0;
            if(raw==null||rawWidth<100||rawHeight<100||(long)rawWidth*rawHeight>raw.Length)return false;
            float scale=Mathf.Min(1,800f/Mathf.Max(rawWidth,rawHeight));
            width=Mathf.RoundToInt(rawWidth*scale);height=Mathf.RoundToInt(rawHeight*scale);
            if(pixels==null||pixels.Length!=width*height)pixels=new byte[width*height];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int sy=Mathf.Min(rawHeight-1,(int)((y+.5)*rawHeight/height));
                int sx=Mathf.Min(rawWidth-1,(int)((x+.5)*rawWidth/width));
                pixels[y*width+x]=raw[sy*rawWidth+sx];
            }
            return true;
        }
        // Pixel centres remain in the raw optical axes, independent of screen orientation.
        public static Vector4 ScaleIntrinsics(Vector4 k,int width,int height,int targetWidth,int targetHeight)
        {
            float sx=(float)targetWidth/width,sy=(float)targetHeight/height;
            return new Vector4(k.x*sx,k.y*sy,(k.z+.5f)*sx-.5f,(k.w+.5f)*sy-.5f);
        }
        public static bool Copy(XRCpuImage image,ref byte[] pixels,out int width,out int height)
        {
            width=height=0;if(!image.valid||image.planeCount<1||image.width<100||image.height<100)return false;
            float scale=Mathf.Min(1,800f/Mathf.Max(image.width,image.height));
            width=Mathf.RoundToInt(image.width*scale);height=Mathf.RoundToInt(image.height*scale);
            var plane=image.GetPlane(0);long row=(long)(image.width-1)*plane.pixelStride+1;
            long last=(long)(image.height-1)*plane.rowStride+row;
            if(plane.pixelStride<1||plane.rowStride<row||last>plane.data.Length)return false;
            if(pixels==null||pixels.Length!=width*height)pixels=new byte[width*height];
            for(int y=0;y<height;y++)
            {
                int sy=Mathf.Min(image.height-1,(int)((y+.5)*image.height/height));int offset=sy*plane.rowStride;
                for(int x=0;x<width;x++)
                {
                    int sx=Mathf.Min(image.width-1,(int)((x+.5)*image.width/width));pixels[y*width+x]=plane.data[offset+sx*plane.pixelStride];
                }
            }
            return true;
        }
    }
}
