using UnityEditor;
using UnityEngine;

namespace LastCall.EditorTools
{
    /// <summary>
    /// The customer animation frames (Assets/Resources/Patron/&lt;clip&gt;/*.png, 2026-07-23)
    /// are hi-bit pixel art loaded at runtime via <c>Resources.LoadAll</c>. They must import as
    /// point-filtered, uncompressed sprites or they blur and fringe when scaled up in the HUD.
    /// This applies those settings automatically on import, so dropping in a new frame set needs
    /// no manual inspector fiddling.
    /// </summary>
    public sealed class PatronArtPostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("Resources/Patron/")) return;

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
