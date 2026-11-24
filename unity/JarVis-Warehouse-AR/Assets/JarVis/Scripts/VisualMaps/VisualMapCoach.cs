using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace JarVis.Warehouse.VisualMaps
{
    public struct VisualMapImageQuality
    {
        public bool Available;
        public float Mean,Variance,DarkFraction,BrightFraction;
        public bool Unusable => Available && (Mean < 8 || Mean > 247 || Variance < 4);
    }

    public static class VisualMapCoach
    {
        public static string TrackingHint(NotTrackingReason reason)
        {
            if (reason == NotTrackingReason.InsufficientLight) return "Khu vực không đủ ánh sáng để theo dõi AR. Bật thêm đèn; ảnh đã lưu vẫn còn.";
            if (reason == NotTrackingReason.ExcessiveMotion) return "Camera đang chuyển động quá nhanh. Chậm lại và giữ máy ổn định một chút.";
            if (reason == NotTrackingReason.InsufficientFeatures) return "AR cần thêm chi tiết. Hướng camera vào đồ vật hoặc góc phòng, tránh chỉ nhìn một vùng trống.";
            return "Đang tìm lại vị trí. Giữ máy chậm, nhìn đồ vật có chi tiết; ảnh đã lưu vẫn còn.";
        }

        public static VisualMapImageQuality Measure(byte[] gray,int width,int height)
        {
            if (gray == null || width < 1 || height < 1 || (long)width * height != gray.Length) return default;
            double total = 0,squared = 0;
            int count = 0,dark = 0,bright = 0,step = Mathf.Max(1,gray.Length / 4096);
            for (int index = 0; index < gray.Length; index += step)
            {
                int value = gray[index]; total += value; squared += value * value; count++;
                if (value < 25) dark++;
                if (value > 235) bright++;
            }
            float mean = (float)(total / count);
            return new VisualMapImageQuality { Available = true,Mean = mean,Variance = Mathf.Max(0,(float)(squared / count - mean * mean)),DarkFraction = dark / (float)count,BrightFraction = bright / (float)count };
        }

        public static string Hint(bool tracking,VisualMapImageQuality quality,float speed,float turnSpeed,Pose camera,List<VisualMapFrame> frames,double stillSeconds)
        {
            if (!tracking) return "Đang tìm lại vị trí. Giữ máy chậm, nhìn đồ vật có chi tiết; ảnh đã lưu vẫn còn.";
            if (!quality.Available) return "Đang đọc camera. Giữ máy ổn định một chút; không cần bắt đầu lại lượt quét.";
            if (quality.Mean < 40 && quality.DarkFraction > .45f) return "Khu vực không đủ ánh sáng. Bật thêm đèn hoặc hướng camera về vùng sáng hơn.";
            if (quality.Mean > 215 && quality.BrightFraction > .45f) return "Camera đang bị chói. Hướng lệch khỏi nguồn sáng để thấy rõ đồ vật.";
            if (speed > .8f || turnSpeed > 55) return "Bạn đang di chuyển máy nhanh. Chậm lại một chút để ảnh rõ và nối được với ảnh trước.";
            if (quality.Variance < 120) return "Vùng này ít chi tiết. Hướng thêm vào góc phòng hoặc đồ vật; không cần đổi phòng.";
            if (frames == null || frames.Count < 3) return "Đang tự lưu ảnh. Đi chậm và hướng camera vào đồ vật, tường và các góc phòng.";
            if (stillSeconds < 1.5) return "Đang tự lưu ảnh. Giữ các ảnh có phần cảnh chồng nhau khi di chuyển.";
            Vector3 origin = new Vector3(frames[0].position[0],frames[0].position[1],frames[0].position[2]);
            float baseline = 0;
            foreach (VisualMapFrame frame in frames) baseline = Mathf.Max(baseline,Vector3.Distance(origin,new Vector3(frame.position[0],frame.position[1],frame.position[2])));
            if (baseline < .35f) return "Đã có ảnh quanh điểm đứng. Nếu lối đi trống, dịch máy sang bên một chút để thu chiều sâu.";
            int left = 0,right = 0,nearby = 0;
            foreach (VisualMapFrame frame in frames)
            {
                Vector3 position = new Vector3(frame.position[0],frame.position[1],frame.position[2]);
                if (Vector3.Distance(position,camera.position) > .8f) continue;
                Quaternion rotation = new Quaternion(frame.rotation[0],frame.rotation[1],frame.rotation[2],frame.rotation[3]);
                float difference = Mathf.DeltaAngle(camera.rotation.eulerAngles.y,rotation.eulerAngles.y);
                if (difference < -12 && difference >= -50) left++;
                if (difference > 12 && difference <= 50) right++;
                nearby++;
            }
            if (nearby > 0 && left == 0 && right > 0) return "Lia chậm camera sang trái một chút để bổ sung góc chưa có ảnh ở vị trí này.";
            if (nearby > 0 && right == 0 && left > 0) return "Lia chậm camera sang phải một chút để bổ sung góc chưa có ảnh ở vị trí này.";
            return "Ảnh đã được giữ. Tiếp tục quét vùng bạn cần hoặc bấm Lưu lượt quét để kết thúc.";
        }
    }
}
