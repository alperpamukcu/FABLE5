// Auto-applies PIXEL sprite import settings to every PNG under Assets/Art/ and
// Assets/Resources/Scene/ (v2 pixel
// pivot, PATCH_15 §C / 15_asset_pipeline §4): point filtering, no compression, no
// mipmaps, PPU 1. Pairs with the project's Pixel Perfect Camera (640×360, integer scale).
using UnityEditor;
using UnityEngine;

public class LastCallImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        var path = assetPath.Replace("\\", "/").ToLower();
        // Resources/Scene is the room's art in all but name — the window's view, its glass,
        // the palms, the back bar. Its five plates had each been hand-set to these very
        // settings one file at a time, and a sixth arriving as a blurry Default texture is
        // then a silent bug (see memory: a new PNG whose rule is not compiled yet keeps
        // Default and has to be force-reimported). The rule says it instead.
        // ...AND Resources/Fixtures, for exactly the reason written above (2026-08-25).
        // The room's own furniture lives there - plants, lamps, tables, the sink, the beer
        // fonts - as PPU 1 world sprites, and every one of its .meta files had been set by
        // hand to these settings. Five new plants arrived with no .meta at all and would
        // have imported as bilinear PPU 100: blurry, and a hundred times too small in a
        // room measured in art pixels. The rule the comment above promised now actually
        // covers the folder it was describing.
        //
        // Resources/ITEMS is deliberately NOT here: those are canvas sprites at PPU 100
        // (see the meta open_sign_gen ships), and sweeping them to PPU 1 would resize
        // every prop the HUD draws.
        if (!path.Contains("/art/") && !path.Contains("/resources/scene/")
            && !path.Contains("/resources/fixtures/")) return;

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
