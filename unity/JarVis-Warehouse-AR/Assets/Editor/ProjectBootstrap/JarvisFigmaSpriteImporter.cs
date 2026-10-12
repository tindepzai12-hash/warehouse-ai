using UnityEditor;

namespace ProjectBootstrap
{
    // Keep independently exported Figma artwork editable as Unity sprites.
    public sealed class JarvisFigmaSpriteImporter : AssetPostprocessor
    {
        private const string FigmaArtPath = "Assets/Resources/Figma/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(FigmaArtPath, System.StringComparison.Ordinal))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
