using System;

namespace JarVis.Warehouse.Lab
{
    // Canonical printed-card coordinates, not screen orientation or the normal of a flat sheet.
    public static class LabScanWarp
    {
        public static byte[] QrCrop(byte[] source,int width,int height,float[] quad,int size=320)
        {
            if(source==null||width<1||height<1||(long)width*height!=source.Length||quad==null||quad.Length!=8||size<32||size>640)return null;
            for(int i=0;i<8;i++)if(float.IsNaN(quad[i])||float.IsInfinity(quad[i]))return null;
            double x0=quad[0],y0=quad[1],x1=quad[2],y1=quad[3],x2=quad[4],y2=quad[5],x3=quad[6],y3=quad[7];
            double sign=0,area=0;
            for(int i=0;i<4;i++)
            {
                int j=(i+1)%4,k=(i+2)%4;double cross=(quad[j*2]-quad[i*2])*(quad[k*2+1]-quad[j*2+1])-(quad[j*2+1]-quad[i*2+1])*(quad[k*2]-quad[j*2]);
                if(Math.Abs(cross)<1e-4||sign*cross<0)return null;sign=cross;area+=quad[i*2]*quad[j*2+1]-quad[j*2]*quad[i*2+1];
            }
            if(Math.Abs(area)<512)return null;
            double dx1=x1-x2,dx2=x3-x2,dx3=x0-x1+x2-x3,dy1=y1-y2,dy2=y3-y2,dy3=y0-y1+y2-y3;
            double g=0,h=0;
            if(Math.Abs(dx3)+Math.Abs(dy3)>1e-7)
            {
                double determinant=dx1*dy2-dx2*dy1;if(Math.Abs(determinant)<1e-7)return null;
                g=(dx3*dy2-dx2*dy3)/determinant;h=(dx1*dy3-dx3*dy1)/determinant;
            }
            double a=x1-x0+g*x1,b=x3-x0+h*x3,c=x0,d=y1-y0+g*y1,e=y3-y0+h*y3,f=y0;
            var result=new byte[size*size];int outside=0;
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                // The QR is x=247..753/y=397..903 on the 1000x1200 original.
                // Include a white quiet zone and exclude the patterned outside border.
                double u=.18+.64*(x+.5)/size,v=.275+(.808333333333-.275)*(y+.5)/size,denominator=g*u+h*v+1;
                if(Math.Abs(denominator)<1e-7)return null;
                int sx=(int)Math.Round((a*u+b*v+c)/denominator),sy=(int)Math.Round((d*u+e*v+f)/denominator);
                if(sx<0||sx>=width||sy<0||sy>=height){result[y*size+x]=255;outside++;}else result[y*size+x]=source[sy*width+sx];
            }
            return outside>size*size/20?null:result;
        }
    }
}
