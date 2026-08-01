using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The serving glasses, drawn from their own silhouette (v5 P14 / C9).
    ///
    /// Drawn rather than generated for a harder reason than the drink icons. A serving glass
    /// is **hollow**: the liquid pools behind it and shows through, so the art is an outline
    /// and the fluid has to land exactly inside it. With a picture, that means measuring the
    /// interior off the image by hand — which is what the old single tumbler did, and why the
    /// serve stage carried three tuned constants (0.66 of the half-width, 0.14 up from the
    /// floor, 0.6 of the height) that meant nothing except "this is where the drink goes in
    /// THAT drawing". Five glasses would have been fifteen such numbers.
    ///
    /// Drawing from <see cref="GlasswareDefinition.Profile"/> — the same array the fluid
    /// solver fills — means the glass and the drink inside it come from one source, and the
    /// interior is reported rather than guessed. A glass added to glassware.json is drawn,
    /// filled and measured on the day it is added.
    /// </summary>
    public static class GlassArt
    {
        private const int W = 128, H = 176;
        private const int Wall = 3;       // wall thickness in texture pixels
        private const int Base = 6;       // the floor slab of a tumbler

        private static readonly Color Outline = new Color32(0x14, 0x10, 0x18, 0xFF);
        private static readonly Color Body = new Color32(0xBF, 0xD6, 0xE4, 0xFF);
        private static readonly Color Shade = new Color32(0x8D, 0xA5, 0xB6, 0xFF);
        private static readonly Color Shine = new Color32(0xFF, 0xFF, 0xFF, 0xE6);
        // The upgrade dress (per-glass tiers, 2026-08-02): tier 2 wears etched rings,
        // tier 3 a gold rim and foot — geometry untouched, so every measured interior
        // and fluid profile survives the promotion.
        private static readonly Color Etch = new Color32(0xE4, 0xF1, 0xF9, 0xFF);
        private static readonly Color Gold = new Color32(0xE6, 0xBE, 0x66, 0xFF);
        private static readonly Color GoldDim = new Color32(0xB8, 0x8A, 0x3C, 0xFF);

        /// <summary>A drawn glass and the hole in it: where the drink actually goes, as
        /// fractions of the sprite's rect so it survives whatever size the UI draws it at.</summary>
        public readonly struct Piece
        {
            public readonly Sprite Sprite;
            /// <summary>
            /// The interior as a solid silhouette, pixel-aligned with <see cref="Sprite"/>.
            /// Drawn behind the glass and clipped with a vertical <see cref="Image.fillAmount"/>,
            /// it gives a filled glass anywhere the full fluid solver would be overkill — the
            /// drink carried across the counter, a shop listing — and it is clipped to the real
            /// silhouette, so a martini's liquid narrows into the cone instead of being a
            /// rectangle poking through the walls.
            /// </summary>
            public readonly Sprite Fill;
            /// <summary>Half-width of the interior at its widest, as a fraction of the rect's
            /// half-width.</summary>
            public readonly float InteriorHalf;
            /// <summary>Floor and rim of the interior, 0 = bottom of the rect, 1 = top.</summary>
            public readonly float FloorY, RimY;
            /// <summary>The silhouette to hand <see cref="MetaballFluid.SetProfile"/>.</summary>
            public readonly float[] Profile;
            /// <summary>The rect's aspect (width / height) at the sprite's own proportions.</summary>
            public readonly float Aspect;
            /// <summary>Particle-count correction for <see cref="MetaballFluid.SetDensity"/>,
            /// measured per vessel — see <see cref="Densities"/>.</summary>
            public readonly float Density;

            public Piece(Sprite sprite, Sprite fill, float interiorHalf, float floorY, float rimY,
                float[] profile, float aspect, float density)
            {
                Sprite = sprite; Fill = fill; InteriorHalf = interiorHalf;
                FloorY = floorY; RimY = rimY; Profile = profile; Aspect = aspect;
                Density = density;
            }

            /// <summary>The <see cref="Image.fillAmount"/> that draws the interior filled to
            /// <paramref name="fraction"/>. The mask is transparent below the floor, so this is
            /// simply where that level sits up the whole sprite.</summary>
            public float FillAmount(float fraction) =>
                FloorY + (RimY - FloorY) * Mathf.Clamp01(fraction);
        }

        private readonly struct Shape
        {
            public readonly int Half, Floor, Rim;
            public readonly bool Stem;
            public Shape(int half, int floor, int rim, bool stem)
            { Half = half; Floor = floor; Rim = rim; Stem = stem; }
        }

        // Proportion is presentation, so it lives here rather than in the data — the profile
        // says a martini is a cone and a highball a tube, not that the highball is the tall
        // one. Same table as the order icons, at eight times the pixels.
        private static readonly Dictionary<string, Shape> Shapes = new Dictionary<string, Shape>
        {
            ["pint"] = new Shape(46, 12, 168, false),
            ["highball"] = new Shape(34, 12, 168, false),
            ["rocks"] = new Shape(50, 12, 106, false),
            ["martini"] = new Shape(56, 88, 166, true),
            ["coupe"] = new Shape(52, 94, 158, true),
        };

        private static readonly Shape DefaultShape = new Shape(40, 12, 164, false);

        /// <summary>
        /// Per-vessel particle-count correction, MEASURED rather than reasoned about — the
        /// solver estimates its count from the silhouette's area and the estimate is not exact
        /// for every shape (see CLAUDE.md: correct the vessel, never scale the fill fraction,
        /// which only clamps). Each glass was filled to a quarter, a half, three quarters and
        /// the brim, let settle, and its drawn surface read back with `SurfaceY`.
        ///
        /// Every one of them drew SHORT, because the one global 0.90 here was calibrated
        /// against the old single tumbler and the five silhouettes are not it — a full glass
        /// was landing at 0.90 of its rim, which is the reading a player sees most, since a
        /// full glass is the thing they are aiming for.
        /// </summary>
        private static readonly Dictionary<string, float> Densities = new Dictionary<string, float>
        {
            ["pint"] = 0.97f,
            ["highball"] = 0.97f,
            ["rocks"] = 0.94f,
            ["martini"] = 0.95f,
            ["coupe"] = 0.94f,
        };

        private static readonly Dictionary<string, Piece> Cache = new Dictionary<string, Piece>();

        /// <summary>The drawn glass for a glassware definition, built once and kept.</summary>
        public static Piece For(GlasswareDefinition glass) => For(glass, 1);

        /// <summary>The same glass at an upgrade tier (1–3): richer dress, same geometry.</summary>
        public static Piece For(GlasswareDefinition glass, int tier)
        {
            if (tier < 1) tier = 1;
            if (tier > 3) tier = 3;
            string key = (glass?.Id ?? "") + "_t" + tier;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            // The generated 3D set wins when installed (the author, 2026-08-02: the
            // procedural glasses read flat). Tier dress for the generated set is its own
            // upcoming art pass, so for now every tier wears the same base sprite.
            var gen = ItemArt.Load($"glass3d_{glass?.Id}");
            var piece = gen != null ? FromGenerated(glass, gen) : Draw(glass, tier);
            Cache[key] = piece;
            return piece;
        }

        /// <summary>Interior geometry of each generated sprite, measured at install by
        /// install_glasses.py (the BottleArt bargain, offline: the script erodes the
        /// silhouette, bakes the cavity to translucency, writes the companion fill mask,
        /// and prints this table). Fractions of the sprite rect; density starts at the
        /// procedural vessel's measured value until SurfaceY is re-read in play.</summary>
        private readonly struct Gen3D
        {
            public readonly float FloorY, RimY, InteriorHalf, Density;
            public Gen3D(float floorY, float rimY, float interiorHalf, float density)
            { FloorY = floorY; RimY = rimY; InteriorHalf = interiorHalf; Density = density; }
        }

        // Quarter-res pass (the author, 2026-08-02: the glasses out-resolved the
        // customers): the hi-res masters were dropped to the patron grain with NEAREST
        // and re-measured. Masters live in the scratchpad as glass3d_*_hi.png.
        private static readonly Dictionary<string, Gen3D> Gen3DTable = new Dictionary<string, Gen3D>
        {
            ["pint"] = new Gen3D(0.083f, 0.972f, 0.905f, 0.97f),       // 42x72
            ["highball"] = new Gen3D(0.079f, 0.968f, 0.875f, 0.97f),   // 32x63
            ["rocks"] = new Gen3D(0.214f, 0.964f, 0.913f, 0.94f),      // 46x56, floor on the ledge
            ["martini"] = new Gen3D(0.576f, 0.970f, 0.917f, 0.95f),    // 48x66
            ["coupe"] = new Gen3D(0.552f, 0.970f, 0.913f, 0.94f),      // 46x67
        };

        private static Piece FromGenerated(GlasswareDefinition glass, Sprite sprite)
        {
            var fill = ItemArt.Load($"glass3d_{glass.Id}_fill");
            Gen3D g = Gen3DTable.TryGetValue(glass.Id, out var t)
                ? t : new Gen3D(0.08f, 0.94f, 0.8f, 0.95f);
            var solverProfile = new float[glass.Profile?.Count ?? 2];
            if (glass.Profile != null && glass.Profile.Count > 0)
                for (int i = 0; i < glass.Profile.Count; i++) solverProfile[i] = (float)glass.Profile[i];
            else solverProfile = new[] { 1f, 1f };
            return new Piece(sprite, fill, g.InteriorHalf, g.FloorY, g.RimY, solverProfile,
                sprite.rect.width / sprite.rect.height, g.Density);
        }

        private static Piece Draw(GlasswareDefinition glass, int tier)
        {
            var shape = glass != null && Shapes.TryGetValue(glass.Id, out var s) ? s : DefaultShape;
            IReadOnlyList<double> profile = glass?.Profile;

            var px = new Color[W * H];
            var hole = new Color[W * H];      // the interior, drawn alongside so the two agree
            int span = Mathf.Max(1, shape.Rim - shape.Floor);

            // The stalk and the foot, for a glass that stands on one. Both are solid: they are
            // the part of the glass you look THROUGH the least.
            if (shape.Stem)
            {
                for (int y = 0; y < 7; y++)
                {
                    int half = Mathf.RoundToInt(shape.Half * (y < 3 ? 0.52f : 0.44f));
                    Span(px, y, -half, half, y < 2 ? Shade : Body);
                }
                for (int y = 7; y < shape.Floor; y++) Span(px, y, -4, 4, Body);
                for (int y = 7; y < shape.Floor; y++) { Put(px, -5, y, Shade); Put(px, 4, y, Shine); }
            }

            for (int y = shape.Floor - (shape.Stem ? 0 : Base); y <= shape.Rim; y++)
            {
                float t = Mathf.Clamp01((y - shape.Floor) / (float)span);
                int half = HalfWidth(profile, t, shape.Half);

                // Below the interior floor the glass is solid — the base a tumbler stands on.
                if (y < shape.Floor)
                {
                    Span(px, y, -half, half - 1, y < shape.Floor - Base + 2 ? Shade : Body);
                    continue;
                }

                // At and above the floor only the WALLS are drawn. The middle is left
                // transparent on purpose: that hole is where the drink is rendered — and it is
                // recorded into the fill mask on the same pass, so the two cannot disagree.
                Span(hole, y, -half + Wall, half - Wall - 1, Color.white);
                bool etched = tier >= 2 &&
                    (y == shape.Floor + span * 38 / 100 || y == shape.Floor + span * 52 / 100);
                Span(px, y, -half, -half + Wall - 1, etched ? Etch : Body);
                Span(px, y, half - Wall, half - 1, etched ? Etch : Body);
                Put(px, -half, y, Shade);
                Put(px, half - 1, y, Shade);

                // One lit column down the inside of the left wall.
                if (y > shape.Floor + 6 && y < shape.Rim - 5) Put(px, -half + Wall, y, Shine);
            }

            // The mouth: the far lip of the rim, drawn right across, which is what tells the
            // eye it is looking into an open vessel rather than at a flat cut-out.
            int rimHalf = HalfWidth(profile, 1f, shape.Half);
            for (int y = shape.Rim; y < shape.Rim + Wall && y < H; y++)
                Span(px, y, -rimHalf, rimHalf - 1,
                    tier >= 3 ? (y == shape.Rim + Wall - 1 ? GoldDim : Gold)
                    : y == shape.Rim + Wall - 1 ? Shade : Body);
            // ...and a gold foot for the finest line: the base band of a tumbler, the
            // bottom of a stem's foot.
            if (tier >= 3)
            {
                int footY = shape.Stem ? 0 : shape.Floor - Base;
                int footHalf = shape.Stem
                    ? Mathf.RoundToInt(shape.Half * 0.52f)
                    : HalfWidth(profile, 0f, shape.Half);
                Span(px, footY, -footHalf, footHalf - 1, GoldDim);
            }

            Trace(px);

            // The 3D read (the author: the outlines drew flat): an open MOUTH you look
            // into, and soft light down the interior walls. All translucent, all laid
            // over pixels that are still empty — the drink behind shows through, and the
            // outline pass has already run so nothing re-traces these.
            int innerHalf = rimHalf - Wall;
            if (innerHalf > 4)
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -innerHalf; dx < innerHalf; dx++)
                    {
                        float e = (dx + 0.5f) * (dx + 0.5f) / (float)(innerHalf * innerHalf)
                                + dy * dy / 9f;
                        int y = shape.Rim + dy;
                        if (e > 1f || y < 0 || y >= H) continue;
                        int xI = W / 2 + dx;
                        if (px[y * W + xI].a > 0f) continue;
                        px[y * W + xI] = e > 0.62f
                            ? new Color(0.08f, 0.10f, 0.16f, 0.42f)
                            : new Color(0.05f, 0.06f, 0.10f, 0.20f);
                    }
            for (int y = shape.Floor + 1; y < shape.Rim - 3; y++)
            {
                float t = Mathf.Clamp01((y - shape.Floor) / (float)span);
                int half = HalfWidth(profile, t, shape.Half) - Wall;
                for (int i2 = 0; i2 < 5 && i2 < half; i2++)
                {
                    int xL = W / 2 - half + i2;
                    if (xL < 0 || xL >= W || px[y * W + xL].a > 0f) continue;
                    px[y * W + xL] = new Color(1f, 1f, 1f, 0.12f - 0.02f * i2);
                }
                int xR = W / 2 + half - 1;
                if (xR >= 0 && xR < W && px[y * W + xR].a <= 0f)
                    px[y * W + xR] = new Color(0.04f, 0.07f, 0.11f, 0.12f);
            }

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
                name = $"glass_{glass?.Id ?? "default"}_t{tier}",
            };
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.DontSave;
            sprite.name = tex.name;

            var fillTex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
                name = $"fill_{glass?.Id ?? "default"}",
            };
            fillTex.SetPixels(hole);
            fillTex.Apply();
            var fill = Sprite.Create(fillTex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
            fill.hideFlags = HideFlags.DontSave;
            fill.name = fillTex.name;

            // What the fluid needs, measured off what was just drawn rather than guessed:
            // the widest interior, and the floor and rim it sits between.
            float interiorHalf = (rimHalf - Wall) / (W * 0.5f);
            var solverProfile = new float[profile?.Count ?? 2];
            if (profile != null && profile.Count > 0)
                for (int i = 0; i < profile.Count; i++) solverProfile[i] = (float)profile[i];
            else { solverProfile = new[] { 1f, 1f }; }

            float density = glass != null && Densities.TryGetValue(glass.Id, out var d) ? d : 0.95f;
            return new Piece(sprite, fill, interiorHalf,
                shape.Floor / (float)H, shape.Rim / (float)H, solverProfile, W / (float)H, density);
        }

        // ── the liquid's SURFACE (the author, 2026-08-02: the pour must read 3D) ──
        // The glasses look down into their mouths now, so a flat liquid line breaks the
        // illusion: the fill wears a meniscus ELLIPSE, as wide as the interior at that
        // height, tinted a shade lighter than the drink.

        private static Sprite _surfaceSprite;

        public static Sprite SurfaceEllipse()
        {
            if (_surfaceSprite != null) return _surfaceSprite;
            const int EW = 64, EH = 16;
            var px = new Color[EW * EH];
            float ecx = (EW - 1) / 2f, ecy = (EH - 1) / 2f;
            for (int y = 0; y < EH; y++)
                for (int x = 0; x < EW; x++)
                {
                    float dx = (x - ecx) / ecx, dy = (y - ecy) / ecy;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    px[y * EW + x] = new Color(1f, 1f, 1f, d > 0.70f ? 0.95f : 0.78f);
                }
            var tex = new Texture2D(EW, EH, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            tex.SetPixels(px);
            tex.Apply();
            _surfaceSprite = Sprite.Create(tex, new Rect(0, 0, EW, EH), new Vector2(0.5f, 0.5f), 1f);
            _surfaceSprite.hideFlags = HideFlags.DontSave;
            return _surfaceSprite;
        }

        public static float ProfileAt(float[] profile, float t)
        {
            if (profile == null || profile.Length == 0) return 1f;
            if (profile.Length == 1) return profile[0];
            float at = Mathf.Clamp01(t) * (profile.Length - 1);
            int i = Mathf.Min((int)at, profile.Length - 2);
            return Mathf.Lerp(profile[i], profile[i + 1], at - i);
        }

        /// <summary>Builds the meniscus as a child of the glass rect. Callers order it over
        /// the liquid; a glass drawn as the parent's own Image stays under its walls' paint
        /// only where the ellipse is narrower than the mouth — which it is, by measure.</summary>
        public static Image MakeSurface(RectTransform glassRect)
        {
            var go = new GameObject("Surface", typeof(RectTransform));
            var srt = (RectTransform)go.transform;
            srt.SetParent(glassRect, false);
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0);
            srt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.AddComponent<Image>();
            img.sprite = SurfaceEllipse();
            img.raycastTarget = false;
            go.SetActive(false);
            return img;
        }

        /// <summary>Seats the meniscus at the live fill and colours it off the drink.</summary>
        public static void PlaceSurface(Image surface, Piece piece, float fill,
            RectTransform glassRect, Color liquid)
        {
            if (surface == null) return;
            bool show = fill > 0.02f;
            if (surface.gameObject.activeSelf != show) surface.gameObject.SetActive(show);
            if (!show) return;
            float t = Mathf.Clamp01(fill);
            var rect = glassRect.rect;
            float w = rect.width * piece.InteriorHalf * ProfileAt(piece.Profile, t);
            surface.rectTransform.sizeDelta = new Vector2(w, Mathf.Max(3f, w * 0.18f));
            surface.rectTransform.anchoredPosition = new Vector2(0, piece.FillAmount(t) * rect.height);
            surface.color = new Color(
                Mathf.Lerp(liquid.r, 1f, 0.35f),
                Mathf.Lerp(liquid.g, 1f, 0.35f),
                Mathf.Lerp(liquid.b, 1f, 0.35f), 0.95f);
        }

        private static int HalfWidth(IReadOnlyList<double> profile, float t, int maxHalf)
        {
            if (profile == null || profile.Count == 0) return maxHalf;
            float at = Mathf.Clamp01(t) * (profile.Count - 1);
            int i = Mathf.Min((int)at, profile.Count - 2);
            float w = Mathf.Lerp((float)profile[i], (float)profile[i + 1], at - i);
            return Mathf.Max(Wall + 1, Mathf.RoundToInt(w * maxHalf));
        }

        /// <summary>Fills a row between two offsets from the centre column.</summary>
        private static void Span(Color[] px, int y, int fromDx, int toDx, Color c)
        {
            for (int dx = fromDx; dx <= toDx; dx++) Put(px, dx, y, c);
        }

        private static void Put(Color[] px, int dx, int y, Color c)
        {
            int x = W / 2 + dx;
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            px[y * W + x] = c;
        }

        /// <summary>Wraps the drawn glass in the one-pixel dark line the rest of the set
        /// carries, so it sits in the same world as the bottles and the keg.</summary>
        private static void Trace(Color[] px)
        {
            var outlined = new List<int>();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (px[y * W + x].a > 0f) continue;
                    if (Solid(px, x - 1, y) || Solid(px, x + 1, y) ||
                        Solid(px, x, y - 1) || Solid(px, x, y + 1))
                        outlined.Add(y * W + x);
                }
            foreach (int i in outlined) px[i] = Outline;
        }

        private static bool Solid(Color[] px, int x, int y) =>
            x >= 0 && x < W && y >= 0 && y < H && px[y * W + x].a > 0f;
    }
}
