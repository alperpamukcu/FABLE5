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
        /// <summary>
        /// SOLID FIGURES (2026-09-21, the author: "karakterlerin bel kısımları zeminin üstünde şeffaflaşıyor ...
        /// katı olması gerekiyor, içerisini gösteremez"): a figure's enclosed transparent pockets — the wedge
        /// between a hanging arm and the body, the gap between the legs — read as a fade at the waist where the
        /// floor showed through them (measured, r73/r74: the body itself is opaque, the pockets are in the art).
        /// At import every transparent pixel the picture's outside cannot reach is filled from its opaque
        /// neighbours, pass by pass, so the silhouette closes and the author's PNGs stay as they were drawn.
        /// </summary>
        private void OnPostprocessTexture(Texture2D tex)
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("Resources/Patron/") || tex == null) return;
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            var outside = new bool[w * h];
            var stack = new System.Collections.Generic.Stack<int>();
            for (int x = 0; x < w; x++) { Seed(px, outside, stack, x); Seed(px, outside, stack, x + (h - 1) * w); }
            for (int y = 0; y < h; y++) { Seed(px, outside, stack, y * w); Seed(px, outside, stack, y * w + w - 1); }
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % w, y = i / w;
                if (x > 0) Seed(px, outside, stack, i - 1);
                if (x < w - 1) Seed(px, outside, stack, i + 1);
                if (y > 0) Seed(px, outside, stack, i - w);
                if (y < h - 1) Seed(px, outside, stack, i + w);
            }
            int filled = 0;
            bool changed = true;
            while (changed)
            {
                changed = false;
                var next = (Color32[])px.Clone();
                for (int i = 0; i < px.Length; i++)
                {
                    if (px[i].a != 0 || outside[i]) continue;
                    int x = i % w, y = i / w, r = 0, g = 0, b = 0, n = 0;
                    if (x > 0 && px[i - 1].a != 0) { r += px[i - 1].r; g += px[i - 1].g; b += px[i - 1].b; n++; }
                    if (x < w - 1 && px[i + 1].a != 0) { r += px[i + 1].r; g += px[i + 1].g; b += px[i + 1].b; n++; }
                    if (y > 0 && px[i - w].a != 0) { r += px[i - w].r; g += px[i - w].g; b += px[i - w].b; n++; }
                    if (y < h - 1 && px[i + w].a != 0) { r += px[i + w].r; g += px[i + w].g; b += px[i + w].b; n++; }
                    if (n == 0) continue;
                    next[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                    changed = true; filled++;
                }
                px = next;
            }
            if (filled > 0) { tex.SetPixels32(px); tex.Apply(false, false); }
        }

        /// <summary>Marks a transparent pixel as reachable from the outside and queues it for the flood.</summary>
        private static void Seed(Color32[] px, bool[] outside, System.Collections.Generic.Stack<int> stack, int i)
        {
            if (outside[i] || px[i].a != 0) return;
            outside[i] = true;
            stack.Push(i);
        }

        private void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            // Fixtures and the scene plates both stand in the WORLD and share the world's
            // import settings; everything else here is UI. Resources/Scene/ carries the
            // window plates (14 v3 §7), which are loaded by shift name at runtime rather
            // than wired into the scene, because one serialized slot cannot hold three.
            bool world = p.Contains("Resources/Fixtures/") || p.Contains("Resources/Scene/")
                      || p.Contains("Art/Backgrounds/");
            // Resources/Menu and Resources/Keys (2026-09-15): the author's button pack and key caps, sliced at run
            // time (MenuPack, KeyCaps) — UI sprites at PPU 100 like the items, point-filtered or the 16px cells blur.
            if (!p.Contains("Resources/Patron/") && !p.Contains("Resources/Items/") && !p.Contains("Resources/Emotes/")
                && !p.Contains("Resources/Menu/") && !p.Contains("Resources/Keys/") && !world) return;

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
            // THE MAT IS TILED (2026-09-06, it grows with the garnish rail), and a tiled sprite
            // wants a Full Rect mesh or Unity draws the run wrong and says so in the console
            // (2026-09-07). Same rule the counter has, for the same reason.
            // ...and the author's own bench counter (2026-09-16), tiled at 4x across the benches.
            if (file == "fx_prep_mat" || file == "bench_counter")
            {
                var matSettings = new TextureImporterSettings();
                ti.ReadTextureSettings(matSettings);
                matSettings.spriteMeshType = SpriteMeshType.FullRect;
                ti.SetTextureSettings(matSettings);
            }
            // The key's corner arc is 14px and its standing lip 11px, so a 16px ring keeps both
            // out of the stretched centre (2026-07-27, art regenerated at 128x80).
            // The 2026-08-09 kit, DRAWN rather than generated, so this border is the
            // drawing's own construction line: sh_ipad2 is 274x175 (exactly the 1096x700
            // it renders at) with a 28px ring, so a sliced Image draws the bezel at 1:1.
            if (file == "sh_ipad2") ti.spriteBorder = new Vector4(28, 28, 28, 28);
            // THE MATS REPEAT, THEY DO NOT STRETCH (2026-09-06). Both of the author's mats
            // are a ribbed body between two drawn ends; a rail that grows must repeat the
            // ribs, so the ends are the border and the middle is the tile. Six pixels is
            // the drawn end on both plates, measured off them.
            if (file == "prep_mat" || file == "fx_beer_mat")
                ti.spriteBorder = new Vector4(6, 0, 6, 0);
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
