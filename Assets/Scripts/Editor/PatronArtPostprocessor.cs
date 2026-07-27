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
            // Readable, so a UI Image can hit-test against the sprite's alpha — that is what
            // lets an icon like the waste bin be clickable on the object itself, not on a box.
            ti.isReadable = true;
            ti.spritePixelsPerUnit = 100;

            // Frames and plates are stretched to fit their UI rect, so give them 9-slice
            // borders — otherwise the brass caps and rivets smear as the rect grows.
            string file = System.IO.Path.GetFileNameWithoutExtension(p);
            // The key's corner arc is 14px and its standing lip 11px, so a 16px ring keeps both
            // out of the stretched centre (2026-07-27, art regenerated at 128x80).
            if (file == "plate" || file == "plate_down") ti.spriteBorder = new Vector4(16, 16, 16, 16);
            else if (file == "bar_frame") ti.spriteBorder = new Vector4(26, 10, 26, 10);
            else if (file == "tab_btn" || file == "tab_btn_down") ti.spriteBorder = new Vector4(18, 16, 18, 16);
        }
    }
}
