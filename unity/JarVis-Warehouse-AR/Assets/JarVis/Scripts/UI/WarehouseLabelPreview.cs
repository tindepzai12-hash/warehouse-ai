using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ZXing;
using ZXing.Common;

namespace JarVis.Warehouse.UI
{
    public sealed class WarehouseLabelPreview : MonoBehaviour
    {
        private Texture2D texture;
        public static bool TryEncode(string code,out Texture2D result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(code) || code.Length > 80) return false;
            foreach (char character in code) if (character < 32 || character > 126) return false;
            try
            {
                BitMatrix matrix = new MultiFormatWriter().encode(code,BarcodeFormat.CODE_128,640,160,
                    new Dictionary<EncodeHintType,object> { { EncodeHintType.MARGIN,20 } });
                Color32[] pixels = new Color32[matrix.Width * matrix.Height];
                for (int row = 0; row < matrix.Height; row++)
                    for (int column = 0; column < matrix.Width; column++)
                        pixels[row * matrix.Width + column] = matrix[column,row] ? new Color32(0,0,0,255) : new Color32(255,255,255,255);
                result = new Texture2D(matrix.Width,matrix.Height,TextureFormat.RGBA32,false);
                result.filterMode = FilterMode.Point;
                result.wrapMode = TextureWrapMode.Clamp;
                result.SetPixels32(pixels);
                result.Apply();
                return true;
            }
            catch (Exception) { if (result != null) Destroy(result); result = null; return false; }
        }
        public bool SetCode(string code)
        {
            if (texture != null) Destroy(texture);
            if (!TryEncode(code,out texture)) return false;
            RawImage image = GetComponent<RawImage>();
            if (image == null) image = gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            return true;
        }
        private void OnDestroy() { if (texture != null) Destroy(texture); }
    }
}
