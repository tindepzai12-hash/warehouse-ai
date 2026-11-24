using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace AtlasAR
{
    public static class AtlasCameraWarp
    {
        // The same display UV transform as ARCameraBackground, but output rows are TOP to BOTTOM for OpenCV.
        public static bool Copy(XRCpuImage image,Matrix4x4 display,byte[] dest,int width,int height)
        {
            if(dest.Length!=width*height||!image.valid)return false;var plane=image.GetPlane(0);var m=display.transpose;
            if(Mathf.Abs(m.m00*m.m11-m.m01*m.m10)<1e-7f)return false;
            for(int y=0;y<height;y++)
            {
                float v=1-(y+.5f)/height;
                for(int x=0;x<width;x++)
                {
                    float u=(x+.5f)/width,ix=m.m00*u+m.m01*v+m.m02,iy=m.m10*u+m.m11*v+m.m12;
                    if(ix<0||ix>=1||iy<0||iy>=1){dest[y*width+x]=255;continue;}
                    int at=(int)(iy*image.height)*plane.rowStride+(int)(ix*image.width)*plane.pixelStride;
                    if(at<0||at>=plane.data.Length)return false;dest[y*width+x]=plane.data[at];
                }
            }
            return true;
        }
        public static Vector4 Intrinsics(Matrix4x4 projection,int width,int height)=>new Vector4(Mathf.Abs(projection.m00)*width*.5f,Mathf.Abs(projection.m11)*height*.5f,(1-projection.m02)*width*.5f,(1+projection.m12)*height*.5f);
    }
}
