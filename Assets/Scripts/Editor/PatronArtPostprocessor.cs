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
            // The market app's furniture (2026-08-07). Every border is MEASURED off its own
            // art by Tools/market_borders.py and pasted here — a 9-slice whose border is
            // guessed either eats the corner detail or stretches it. The tablet's frame
            // probed at exactly 20px on all four sides.
            // The distributor's white/green kit (2026-08-07), each border probed off its
            // own art: the tablet's bezel measured at 40px on every side.
            else if (file == "sh_tablet") ti.spriteBorder = new Vector4(40, 40, 40, 40);
            else if (file == "sh_card") ti.spriteBorder = new Vector4(10, 8, 10, 8);
            else if (file == "sh_bar") ti.spriteBorder = new Vector4(10, 8, 10, 8);
            else if (file == "sh_tab_on" || file == "sh_tab_off") ti.spriteBorder = new Vector4(8, 6, 8, 6);
            else if (file == "sh_btn") ti.spriteBorder = new Vector4(6, 6, 6, 6);
            else if (file == "sh_balance") ti.spriteBorder = new Vector4(10, 5, 10, 5);
            else if (file == "sh_panel") ti.spriteBorder = new Vector4(12, 9, 12, 9);
            else if (file == "mk_tablet") ti.spriteBorder = new Vector4(20, 20, 20, 20);
            else if (file == "mk_tab_on" || file == "mk_tab_off") ti.spriteBorder = new Vector4(10, 6, 10, 6);
            else if (file == "mk_card") ti.spriteBorder = new Vector4(10, 9, 10, 9);
            else if (file == "mk_appbar") ti.spriteBorder = new Vector4(10, 5, 10, 5);
        }
    }
}
