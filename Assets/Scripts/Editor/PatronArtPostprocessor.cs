using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// Hi-bit pixel art loaded at runtime from Resources — the customer animation frames
    /// (Assets/Resources/Patron/&lt;clip&gt;/*.png) and the drink item assets
    /// (Assets/Resources/Items/*.png), both 2026-07-23. They must import as point-filtered,
    /// uncompressed sprites or they blur and fringe when scaled in the HUD. This applies those
    /// settings automatically on import, so dropping in a new asset needs no inspector fiddling.
    /// </summary>
    public sealed class PatronArtPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("Resources/Patron/") && !p.Contains("Resources/Items/")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spritePixelsPerUnit = 100;
        }
    }
}
