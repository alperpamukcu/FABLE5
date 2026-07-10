// Auto-applies sprite import settings to every PNG under Assets/Art/
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
        ti.spritePixelsPerUnit = 100;
        ti.filterMode = FilterMode.Bilinear;      // painterly, not pixel art
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.maxTextureSize = path.Contains("/backgrounds/") ? 2048 : 1024;
    }
}
