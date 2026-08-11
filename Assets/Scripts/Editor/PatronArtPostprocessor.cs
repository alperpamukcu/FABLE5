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
            bool fixture = p.Contains("Resources/Fixtures/");
            if (!p.Contains("Resources/Patron/") && !p.Contains("Resources/Items/") && !fixture) return;

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
            // Fixtures stand in the WORLD, where the stage runs at one unit per art pixel
            // (PixelPerfectCamera assetsPPU = 1): PPU 1 means a fixture drawn at scale 1 is
            // pixel-for-pixel on the room's own grid, nothing resampled. UI sprites keep
            // the canvas-era 100.
            ti.spritePixelsPerUnit = fixture ? 1 : 100;

            // Frames and plates are stretched to fit their UI rect, so give them 9-slice
            // borders — otherwise the brass caps and rivets smear as the rect grows.
            // (The rule chain held sixteen more entries for the two superseded market
            // kits, plus one for a file that never existed — deleted with their sprites,
            // audit 2026-08-11. A border rule without its PNG is how ghosts accumulate.)
            string file = System.IO.Path.GetFileNameWithoutExtension(p);
            // The key's corner arc is 14px and its standing lip 11px, so a 16px ring keeps both
            // out of the stretched centre (2026-07-27, art regenerated at 128x80).
            if (file == "plate" || file == "plate_down") ti.spriteBorder = new Vector4(16, 16, 16, 16);
            // The 2026-08-09 kit, DRAWN rather than generated, so this border is the
            // drawing's own construction line: sh_ipad2 is 274x175 (exactly the 1096x700
            // it renders at) with a 28px ring, so a sliced Image draws the bezel at 1:1.
            else if (file == "sh_ipad2") ti.spriteBorder = new Vector4(28, 28, 28, 28);
        }
    }
}
