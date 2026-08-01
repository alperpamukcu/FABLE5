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

            /// <summary>The modular faces (the author, 2026-08-02): the glass is a
            /// cylinder, so the BACK face and base render under the liquid and the FRONT
            /// face over it — the front's interior fully clear, only its walls read.
            /// Null for the procedural set, whose single sprite stays the old way.</summary>
            public readonly Sprite Front, Back;

            public Piece(Sprite sprite, Sprite fill, float interiorHalf, float floorY, float rimY,
                float[] profile, float aspect, float density,
                Sprite front = null, Sprite back = null)
            {
                Sprite = sprite; Fill = fill; InteriorHalf = interiorHalf;
                FloorY = floorY; RimY = rimY; Profile = profile; Aspect = aspect;
                Density = density; Front = front; Back = back;
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

        /// <summary>The same glass at an upgrade tier (1–6, Core's ladder: tier 1 is the
        /// 0★ base set, tier 6 the 5★ legendary): richer dress, same geometry.</summary>
        public static Piece For(GlasswareDefinition glass, int tier)
        {
            if (tier < 1) tier = 1;
            if (tier > TycoonRun.MaxGlassTier) tier = TycoonRun.MaxGlassTier;
            string key = (glass?.Id ?? "") + "_t" + tier;
            if (Cache.TryGetValue(key, out var cached)) return cached;
            // The generated 3D set wins when installed (the author, 2026-08-02: the
            // procedural glasses read flat). Each tier wears its own dressed sprites
            // (tier_glasses.py — same glass, richer metal); the geometry table applies
            // to all of them because dress never moves the cavity.
            var gen = ItemArt.Load($"glass3d_{glass?.Id}");
            var piece = gen != null ? FromGenerated(glass, gen, tier)
                : Draw(glass, Mathf.Min(tier, 3));   // the procedural dress only knows 1–3
            Cache[key] = piece;
            return piece;
        }

        /// <summary>Interior geometry of each generated sprite, measured at install by
        /// install_glasses.py and finished by finish_glasses.py (the author, 2026-08-02:
        /// keep the approved design — but mirror it pixel-true at the FINAL resolution,
        /// and round the flat bottoms into the ellipse a cylinder actually shows).
        /// Fractions of the sprite rect; density starts at the procedural vessel's
        /// measured value until SurfaceY is re-read in play.</summary>
        private readonly struct Gen3D
        {
            public readonly float FloorY, RimY, InteriorHalf, Density;
            public Gen3D(float floorY, float rimY, float interiorHalf, float density)
            { FloorY = floorY; RimY = rimY; InteriorHalf = interiorHalf; Density = density; }
        }

        // Interiors are the widest wall-to-wall gap measured off the FINAL sprites —
        // the TRUE cavity, which is what the decor (rim crusts, wedges, ice) wants.
        // The liquid pools deliberately do NOT use this for their width: a metaball
        // surface stops a pixel or two short of its box, so the pools overshoot to
        // the full drawn width at their call sites and let the wall band cover the
        // margin (the author: boundary = contact point + a few px, so no seams).
        private static readonly Dictionary<string, Gen3D> Gen3DTable = new Dictionary<string, Gen3D>
        {
            // RimY is the CAVITY's top row (the fill mask's first row), not the mouth
            // band above it — a rim up in the mouth let a full glass draw its surface
            // over the lip (the author, 2026-08-02: "şimdi de taşıyor").
            ["pint"] = new Gen3D(0.083f, 0.958f, 0.905f, 0.97f),       // 42x72, gap 38px @ row 8
            ["highball"] = new Gen3D(0.079f, 0.952f, 0.875f, 0.97f),   // 32x63, gap 28px @ row 7
            ["rocks"] = new Gen3D(0.214f, 0.946f, 0.913f, 0.94f),      // 46x56, gap 42px, floor on the ledge
            ["martini"] = new Gen3D(0.576f, 0.955f, 0.917f, 0.95f),    // 48x66, gap 44px @ row 8
            ["coupe"] = new Gen3D(0.552f, 0.955f, 0.913f, 0.94f),      // 46x67, gap 42px @ row 8
        };

        private static Piece FromGenerated(GlasswareDefinition glass, Sprite sprite, int tier)
        {
            // Tier 1 is the undecorated base set; higher tiers load their dressed
            // sprites and fall back to the base if a dress file is missing, so a
            // half-installed set degrades to plain glass instead of a white square.
            string dress = tier > 1 ? $"_t{tier}" : "";
            if (dress.Length > 0)
            {
                var dressed = ItemArt.Load($"glass3d_{glass.Id}{dress}");
                if (dressed != null) sprite = dressed; else dress = "";
            }
            var fill = ItemArt.Load($"glass3d_{glass.Id}_fill");
            Gen3D g = Gen3DTable.TryGetValue(glass.Id, out var t)
                ? t : new Gen3D(0.08f, 0.94f, 0.8f, 0.95f);
            var solverProfile = new float[glass.Profile?.Count ?? 2];
            if (glass.Profile != null && glass.Profile.Count > 0)
                for (int i = 0; i < glass.Profile.Count; i++) solverProfile[i] = (float)glass.Profile[i];
            else solverProfile = new[] { 1f, 1f };
            return new Piece(sprite, fill, g.InteriorHalf, g.FloorY, g.RimY, solverProfile,
                sprite.rect.width / sprite.rect.height, g.Density,
                ItemArt.Load($"glass3d_{glass.Id}{dress}_front"),
                ItemArt.Load($"glass3d_{glass.Id}{dress}_back"));
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
