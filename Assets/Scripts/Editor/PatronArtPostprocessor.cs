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
            // Fixtures and the scene plates both stand in the WORLD and share the world's
            // import settings; everything else here is UI. Resources/Scene/ carries the
            // window plates (14 v3 §7), which are loaded by shift name at runtime rather
            // than wired into the scene, because one serialized slot cannot hold three.
            bool world = p.Contains("Resources/Fixtures/") || p.Contains("Resources/Scene/")
                      || p.Contains("Art/Backgrounds/");
            if (!p.Contains("Resources/Patron/") && !p.Contains("Resources/Items/") && !world) return;

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
            ti.spritePixelsPerUnit = world ? 1 : 100;

            // Frames and plates are stretched to fit their UI rect, so give them 9-slice
            // borders — otherwise the brass caps and rivets smear as the rect grows.
            // (The rule chain held sixteen more entries for the two superseded market
            // kits, plus one for a file that never existed — deleted with their sprites,
            // audit 2026-08-11. A border rule without its PNG is how ghosts accumulate.)
            string file = System.IO.Path.GetFileNameWithoutExtension(p);
            // The key's corner arc is 14px and its standing lip 11px, so a 16px ring keeps both
            // out of the stretched centre (2026-07-27, art regenerated at 128x80).
            // The 2026-08-09 kit, DRAWN rather than generated, so this border is the
            // drawing's own construction line: sh_ipad2 is 274x175 (exactly the 1096x700
            // it renders at) with a 28px ring, so a sliced Image draws the bezel at 1:1.
            if (file == "sh_ipad2") ti.spriteBorder = new Vector4(28, 28, 28, 28);
            // THE COUNTER IS NINE-SLICED SO IT CAN BE WIDENED BY REPEATING, NEVER BY
            // STRETCHING (2026-08-19, the author: "sağa ve sola doğru genişlet ... kenarlara
            // uzattıkça sündüren değil görüntüyü üreten metodla"). The stage draws it with
            // SpriteDrawMode.Tiled, which keeps the four border bands at 1:1 and REPEATS
            // the centre - so a window wider than 16:9 grows more cabinet run instead of a
            // taller, smeared bar. The numbers are the drawing's own cabinet dividers,
            // measured off counter.png: the run's verticals sit at x 160-168 and 335, so
            // the left cap is the two doors up to the divider at 168, the repeating tile is
            // the single glass panel 168..335, and the right cap is everything past 335 -
            // the second panel and the drawer unit. Divider to divider, so a repeat reads
            // as one more cabinet rather than as a seam. Vertical borders stay 0: the stage
            // sets size.y to the art's own height, so there is exactly one tile down.
            else if (file == "counter")
            {
                // RE-MEASURED for the 2026-08-21 drawer counter (638x241 after its 112
                // transparent rows were cropped off): the blue posts now scan at x 7-32,
                // 209-226, 412-429 and 605-630, so the front is THREE bays, not the old
                // eight. Divider to divider again - left cap up to 217, the repeating
                // tile is the middle bay 217..420, right cap is the rest.
                ti.spriteBorder = new Vector4(217, 0, 218, 0);
                // Tiling reads the sprite's MESH, and the default tight mesh throws away the
                // transparent margin the tile is measured against - Unity says so out loud
                // ("Sprite Tiling might not appear correctly ... not generated with Full Rect")
                // and then draws the run wrong. Full Rect is part of the border rule, not a
                // separate preference.
                // (It lives on TextureImporterSettings, not on the importer itself.)
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(settings);
            }
        }
    }
}
