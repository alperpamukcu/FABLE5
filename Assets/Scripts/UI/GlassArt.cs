using System.Collections.Generic;
using LastCall.Core;
using UnityEngine;

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

        /// <summary>A drawn glass and the hole in it: where the drink actually goes, as
        /// fractions of the sprite's rect so it survives whatever size the UI draws it at.</summary>
        public readonly struct Piece
        {
            public readonly Sprite Sprite;
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

            public Piece(Sprite sprite, float interiorHalf, float floorY, float rimY,
                float[] profile, float aspect, float density)
            {
                Sprite = sprite; InteriorHalf = interiorHalf;
                FloorY = floorY; RimY = rimY; Profile = profile; Aspect = aspect;
                Density = density;
            }
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
        public static Piece For(GlasswareDefinition glass)
        {
            string key = glass?.Id ?? "";
            if (Cache.TryGetValue(key, out var cached)) return cached;
            var piece = Draw(glass);
            Cache[key] = piece;
            return piece;
        }

        private static Piece Draw(GlasswareDefinition glass)
        {
            var shape = glass != null && Shapes.TryGetValue(glass.Id, out var s) ? s : DefaultShape;
            IReadOnlyList<double> profile = glass?.Profile;

            var px = new Color[W * H];
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
                // transparent on purpose: that hole is where the drink is rendered.
                Span(px, y, -half, -half + Wall - 1, Body);
                Span(px, y, half - Wall, half - 1, Body);
                Put(px, -half, y, Shade);
                Put(px, half - 1, y, Shade);

                // One lit column down the inside of the left wall.
                if (y > shape.Floor + 6 && y < shape.Rim - 5) Put(px, -half + Wall, y, Shine);
            }

            // The mouth: the far lip of the rim, drawn right across, which is what tells the
            // eye it is looking into an open vessel rather than at a flat cut-out.
            int rimHalf = HalfWidth(profile, 1f, shape.Half);
            for (int y = shape.Rim; y < shape.Rim + Wall && y < H; y++)
                Span(px, y, -rimHalf, rimHalf - 1, y == shape.Rim + Wall - 1 ? Shade : Body);

            Trace(px);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
                name = $"glass_{glass?.Id ?? "default"}",
            };
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.DontSave;
            sprite.name = tex.name;

            // What the fluid needs, measured off what was just drawn rather than guessed:
            // the widest interior, and the floor and rim it sits between.
            float interiorHalf = (rimHalf - Wall) / (W * 0.5f);
            var solverProfile = new float[profile?.Count ?? 2];
            if (profile != null && profile.Count > 0)
                for (int i = 0; i < profile.Count; i++) solverProfile[i] = (float)profile[i];
            else { solverProfile = new[] { 1f, 1f }; }

            float density = glass != null && Densities.TryGetValue(glass.Id, out var d) ? d : 0.95f;
            return new Piece(sprite, interiorHalf,
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
