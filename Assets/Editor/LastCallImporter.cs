// Auto-applies PIXEL sprite import settings to every PNG under Assets/Art/ (v2 pixel
// pivot, PATCH_15 §C / 15_asset_pipeline §4): point filtering, no compression, no
// mipmaps, PPU 1. Pairs with the project's Pixel Perfect Camera (640×360, integer scale).
using UnityEditor;
using UnityEngine;

public class LastCallImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath.Replace("\\", "/").ToLower();
        if (!path.Contains("/art/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 1;                 // 1 sprite pixel = 1 world unit
        ti.filterMode = FilterMode.Point;           // crisp pixels, no bilinear blur
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.maxTextureSize = 2048;
    }
}
