using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.VisualMaps
{
    public static class VisualMapSensor
    {
        public static VisualMapImageQuality Quality(ARCameraManager manager)
        {
            if (manager == null || !manager.TryAcquireLatestCpuImage(out XRCpuImage image)) return default;
            using (image)
            {
                const int width = 64,height = 48;
                byte[] samples = new byte[width * height];
                XRCpuImage.Plane plane = image.GetPlane(0);
                for (int row = 0; row < height; row++)
                    for (int column = 0; column < width; column++)
                        samples[row * width + column] = plane.data[(row * image.height / height) * plane.rowStride + (column * image.width / width) * plane.pixelStride];
                return VisualMapCoach.Measure(samples,width,height);
            }
        }

        public static byte[] ColorJpeg(ARCameraManager manager,VisualMapFrame frame)
        {
            if (!manager.TryAcquireLatestCpuImage(out XRCpuImage image)) return null;
            using (image)
            {
                if (Math.Abs(image.timestamp - frame.cameraTime) > .001) return null;
                XRCpuImage.ConversionParams conversion = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0,0,image.width,image.height),outputDimensions = new Vector2Int(frame.width,frame.height),
                    outputFormat = TextureFormat.RGB24,transformation = XRCpuImage.Transformation.None
                };
                int size = image.GetConvertedDataSize(conversion);
                IntPtr pointer = Marshal.AllocHGlobal(size);
                Texture2D texture = null;
                try
                {
                    image.Convert(conversion,pointer,size);
                    byte[] native = new byte[size],flipped = new byte[size];
                    Marshal.Copy(pointer,native,0,size);
                    int rowSize = frame.width * 3;
                    for (int row = 0; row < frame.height; row++) Buffer.BlockCopy(native,row * rowSize,flipped,(frame.height - row - 1) * rowSize,rowSize);
                    texture = new Texture2D(frame.width,frame.height,TextureFormat.RGB24,false);
                    texture.LoadRawTextureData(flipped); texture.Apply();
                    return texture.EncodeToJPG(90);
                }
                finally { Marshal.FreeHGlobal(pointer); if (texture != null) UnityEngine.Object.Destroy(texture); }
            }
        }

        public static bool Depth(AROcclusionManager manager,VisualMapFrame frame,string folder)
        {
            if (manager == null || !manager.TryAcquireRawEnvironmentDepthCpuImage(out XRCpuImage depth)) return false;
            using (depth)
            {
                if (depth.width < 8 || depth.height < 8 || depth.width > 640 || depth.height > 640 || Math.Abs(depth.timestamp - frame.cameraTime) > .05 || !manager.TryAcquireEnvironmentDepthConfidenceCpuImage(out XRCpuImage confidence)) return false;
                using (confidence)
                {
                    if (confidence.width != depth.width || confidence.height != depth.height || Math.Abs(confidence.timestamp - depth.timestamp) > .001 || confidence.format != XRCpuImage.Format.OneComponent8) return false;
                    if (depth.format != XRCpuImage.Format.DepthFloat32 && depth.format != XRCpuImage.Format.DepthUint16) return false;
                    XRCpuImage.Plane plane = depth.GetPlane(0),quality = confidence.GetPlane(0);
                    float[] metres = new float[depth.width * depth.height];
                    byte[] scores = new byte[metres.Length],sample = new byte[4];
                    int accepted = 0;
                    for (int row = 0; row < depth.height; row++)
                        for (int column = 0; column < depth.width; column++)
                        {
                            int input = row * plane.rowStride + column * plane.pixelStride,index = row * depth.width + column;
                            if (depth.format == XRCpuImage.Format.DepthUint16) metres[index] = (plane.data[input] | plane.data[input + 1] << 8) * .001f;
                            else
                            {
                                for (int part = 0; part < 4; part++) sample[part] = plane.data[input + part];
                                metres[index] = BitConverter.ToSingle(sample,0);
                            }
                            if (!VisualMapModel.Finite(metres[index]) || metres[index] < 0) metres[index] = 0;
                            scores[index] = quality.data[row * quality.rowStride + column * quality.pixelStride];
                            if (scores[index] >= 200 && metres[index] >= .2f && metres[index] <= 5) accepted++;
                        }
                    if (accepted < 30) return false;
                    string target = Path.Combine(folder,"depth"); Directory.CreateDirectory(target);
                    byte[] values = new byte[metres.Length * 4]; Buffer.BlockCopy(metres,0,values,0,values.Length);
                    string depthName = frame.name + ".f32",confidenceName = frame.name + ".u8";
                    File.WriteAllBytes(Path.Combine(target,depthName),values); File.WriteAllBytes(Path.Combine(target,confidenceName),scores);
                    float sx = depth.width / (float)frame.width,sy = depth.height / (float)frame.height;
                    frame.depth = new VisualMapDepth { file = depthName,confidence = confidenceName,width = depth.width,height = depth.height,
                        fx = frame.fx * sx,fy = frame.fy * sy,cx = frame.cx * sx,cy = frame.cy * sy,time = depth.timestamp };
                    return true;
                }
            }
        }
    }
}
