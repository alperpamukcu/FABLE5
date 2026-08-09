using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The serving-preference pictograms (the author, 2026-08-01: the licence should SHOW
    /// what the customer wants, not list it). Six fixed asks exist — the four garnishes,
    /// the hard shake, the brim-full glass — plus the read's fill band, so the whole set
    /// is drawn here in code the way <see cref="BackBarArt"/> draws the wall: deterministic
    /// pixels, palette tokens, no generator. 24×24, black-ringed like the game's item art.
    /// </summary>
    public static class PrefArt
    {
        private const int S = 24;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        private static readonly Color Ring = new Color(0.07f, 0.06f, 0.09f, 1f);
        private static readonly Color Glass = new Color(0.72f, 0.84f, 0.94f, 1f);
        private static readonly Color GlassDim = new Color(0.58f, 0.70f, 0.82f, 1f);

        public static Sprite ForPreparation(string id)
        {
            switch (id)
            {
                case "ice": return Ice();
                case "lemon_twist": return Lemon();
                case "salt_rim": return SaltRim();
                case "sugar_rim": return SugarRim();
                default: return null;
            }
        }

        public static Sprite Ice() => Cached("ice", p =>
        {
            var face = new Color(0.80f, 0.90f, 0.97f);
            var shade = new Color(0.64f, 0.78f, 0.90f);
            for (int y = 5; y <= 18; y++)
                for (int x = 5; x <= 18; x++)
                    Px(p, x, y, y < 9 ? face : (x > 14 ? shade : face));
            // the two glints every cube in this game wears
            Px(p, 8, 15, Color.white); Px(p, 9, 14, Color.white);
            Px(p, 12, 11, Color.white);
        });

        public static Sprite Lemon() => Cached("lemon", p =>
        {
            var rind = new Color(0.93f, 0.76f, 0.18f);
            var flesh = new Color(0.99f, 0.93f, 0.55f);
            var pith = new Color(0.99f, 0.97f, 0.85f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = x - 11.5f, dy = y - 11.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 9.5f) Px(p, x, y, d > 7.5f ? rind : flesh);
                }
            // segment cuts, the way the shelf's lemon wheel is cut
            for (int i = -6; i <= 6; i++)
            {
                Px(p, 11 + i, 11, pith); Px(p, 12 + i, 12, pith);
                if (Math.Abs(i) <= 4) { Px(p, 11 + i, 11 + i, pith); Px(p, 12 + i, 11 - i, pith); }
            }
        });

        public static Sprite SaltRim() => Cached("salt_rim", p =>
        {
            CoupeWithRim(p, Color.white);
        });

        public static Sprite SugarRim() => Cached("sugar_rim", p =>
        {
            CoupeWithRim(p, new Color(0.97f, 0.78f, 0.85f));
        });

        public static Sprite Shaker() => Cached("shaker", p =>
        {
            var tin = new Color(0.74f, 0.76f, 0.80f);
            var tinShade = new Color(0.56f, 0.59f, 0.64f);
            // body, a hair narrower at the top, cap above
            for (int y = 3; y <= 14; y++)
            {
                int inset = y > 10 ? 1 : 0;
                for (int x = 6 + inset; x <= 17 - inset; x++)
                    Px(p, x, y, x > 14 ? tinShade : tin);
            }
            for (int y = 15; y <= 17; y++)
                for (int x = 8; x <= 15; x++) Px(p, x, y, tinShade);
            for (int y = 18; y <= 19; y++)
                for (int x = 10; x <= 13; x++) Px(p, x, y, tin);
            // the shake, said with speed marks
            var mark = new Color(0.97f, 0.93f, 0.80f);
            Px(p, 2, 8, mark); Px(p, 3, 10, mark); Px(p, 2, 12, mark);
            Px(p, 21, 8, mark); Px(p, 20, 10, mark); Px(p, 21, 12, mark);
        });

        private static void CoupeWithRim(Color[] p, Color grains)
        {
            // bowl
            for (int y = 10; y <= 16; y++)
            {
                int inset = 16 - y;
                for (int x = 4 + inset; x <= 19 - inset; x++)
                    Px(p, x, y, Glass);
            }
            // stem and foot
            for (int y = 4; y <= 9; y++) { Px(p, 11, y, GlassDim); Px(p, 12, y, GlassDim); }
            for (int x = 8; x <= 15; x++) Px(p, x, 3, GlassDim);
            // the crusted rim, dotted along the top edge
            for (int x = 4; x <= 19; x++)
                if ((x & 1) == 0) Px(p, x, 18, grains); else Px(p, x, 17, grains);
        }

        // ── plumbing ─────────────────────────────────────────────────────────────

        private static Sprite Cached(string key, Action<Color[]> draw)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var p = new Color[S * S];
            var clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < p.Length; i++) p[i] = clear;
            draw(p);
            Outline(p);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(p);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            Cache[key] = s;
            return s;
        }

        /// <summary>The one-pixel black ring the item art wears, closed on all four sides.</summary>
        private static void Outline(Color[] p)
        {
            var src = (Color[])p.Clone();
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    if (src[y * S + x].a >= 0.5f) continue;
                    bool edge =
                        (x > 0 && src[y * S + x - 1].a >= 0.5f) ||
                        (x < S - 1 && src[y * S + x + 1].a >= 0.5f) ||
                        (y > 0 && src[(y - 1) * S + x].a >= 0.5f) ||
                        (y < S - 1 && src[(y + 1) * S + x].a >= 0.5f);
                    if (edge) p[y * S + x] = Ring;
                }
        }

        private static void Px(Color[] p, int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= S || y >= S) return;
            p[y * S + x] = c;
        }
    }
}
