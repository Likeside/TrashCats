using UnityEditor;
using UnityEngine;

namespace TrashCats.Editor
{
    /// <summary>
    /// Import settings for character and environment art under Assets/_Project/Art: single sprites, 100 PPU,
    /// bottom-centre pivot (frames share a ground line), no mipmaps.
    /// </summary>
    public sealed class SpriteImportPostprocessor : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/_Project/Art/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtRoot))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
