using UnityEditor;
using UnityEngine;
namespace ProjectBootstrap
{
    public sealed class PrototypeAssetImporter:AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Prototype/"))return;
            var texture=(TextureImporter)assetImporter;texture.textureType=TextureImporterType.Sprite;texture.spriteImportMode=SpriteImportMode.Single;
            texture.alphaIsTransparency=true;texture.mipmapEnabled=false;texture.textureCompression=TextureImporterCompression.Uncompressed;texture.maxTextureSize=2048;texture.filterMode=FilterMode.Bilinear;
        }
    }
}
