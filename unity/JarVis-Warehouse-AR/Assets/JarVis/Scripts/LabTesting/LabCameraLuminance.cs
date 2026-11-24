using Unity.Collections;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    public static class LabCameraLuminance
    {
        // Same screen-UV -> native image-UV contract as ARCameraBackground/DepthUv.
        // Output rows start at viewport bottom, matching WorldToViewportPoint and LabScanWarp.
        public static bool Copy(NativeArray<byte> plane,int sourceWidth,int sourceHeight,int rowStride,int pixelStride,Matrix4x4 display,byte[] output,int width,int height)
        {
            if(!plane.IsCreated||sourceWidth<1||sourceHeight<1||sourceWidth>8192||sourceHeight>8192||rowStride<1||pixelStride<1||width<1||height<1||width>2048||height>2048||(long)width*height>4194304||output==null||(long)width*height!=output.Length)return false;
            if((long)(sourceHeight-1)*rowStride+(long)(sourceWidth-1)*pixelStride>=plane.Length||rowStride<(long)(sourceWidth-1)*pixelStride+1)return false;
            var m=display.transpose;
            foreach(float n in new[]{m.m00,m.m01,m.m02,m.m10,m.m11,m.m12})if(float.IsNaN(n)||float.IsInfinity(n))return false;
            // Reject a missing transform instead of reporting a frame of one repeated pixel.
            if(Mathf.Abs(m.m00*m.m11-m.m01*m.m10)<1e-7f)return false;
            float dx=m.m00/width,dy=m.m10/width;
            for(int y=0;y<height;y++)
            {
                float v=(y+.5f)/height,u=m.m00*(.5f/width)+m.m01*v+m.m02,t=m.m10*(.5f/width)+m.m11*v+m.m12;
                for(int x=0;x<width;x++,u+=dx,t+=dy)
                {
                    int at=y*width+x;
                    output[at]=u<0||u>=1||t<0||t>=1?(byte)255:plane[(int)(t*sourceHeight)*rowStride+(int)(u*sourceWidth)*pixelStride];
                }
            }
            return true;
        }
    }
}
