using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The drink INSIDE a bottle, drawn behind the glass and driven by what the rules say is
    /// left in it. Rebuilt 2026-08-10 against the pilot bottle art, which is the condition
    /// <see cref="BottleArt"/> set when the old layered machinery was swept: "when that art
    /// lands, the liquid layer gets rebuilt against it — not resurrected from here."
    ///
    /// Almost nothing of the old thousand lines comes back, because the new art removes the
    /// reason they existed. The generated glass is genuinely SEMI-TRANSPARENT, so there is no
    /// back plate to paint, no film to thin and no label to protect: the liquid is simply
    /// rendered BEHIND the bottle sprite and the glass covers it for free. What is left is the
    /// part that was always the point — the shape of the cavity, and the level moving in it.
    ///
    /// The camera is 2.5D (high top-down), so the surface is an ELLIPSE, not a line. Scanning
    /// down the glass you meet the surface's FAR rim first, which means the waterline stands
    /// HIGHEST at the centre and falls away to the walls. Bowing it the other way — the sign
    /// that looks right until you see it — reads as a liquid sagging in the middle, which is
    /// the one thing a level never does.
    ///
    /// One Graphic per bottle, one strip of triangles, no particles: a shelf carries a dozen
    /// bottles and <see cref="MetaballFluid"/>'s solver is for the one vessel being poured.
    /// </summary>
    public sealed class BottleFluid : MaskableGraphic
    {
        // ── the cavity ──────────────────────────────────────────────────────────────────
        /// <summary>
        /// The inside of one bottle, measured off its own art: for each row, how far the
        /// liquid may reach left and right, in 0..1 of the sprite's rect.
        ///
        /// Measured rather than authored because the pilot pairs are generated per bottle and
        /// no two silhouettes match — and because a bottle that is measured cannot drift out
        /// of sync with the art the way a hand-written table does.
        /// </summary>
        public sealed class Cavity
        {
            public readonly float[] Left;      // per row, 0 = rect's left edge
            public readonly float[] Right;
            public readonly int Bottom;        // first row that holds liquid
            public readonly int Top;           // last row: the fill line at 100%

            /// <summary>Can anything behind this bottle actually be seen through it?
            ///
            /// The whole approach rests on it. Measured 2026-08-10 across the shipped set:
            /// every one of the flat-era sprites is alpha 255 across its body, so a drink
            /// drawn behind one is perfectly correct and perfectly invisible. The pilot art
            /// is see-through, which is what makes the simple route possible at all — so a
            /// bottle that is not gets SAID rather than silently drawn behind.</summary>
            public readonly float SeeThrough;

            private Cavity(float[] left, float[] right, int bottom, int top, float seeThrough)
            {
                Left = left; Right = right; Bottom = bottom; Top = top; SeeThrough = seeThrough;
            }

            public bool Valid => Top > Bottom;

            /// <summary>Row index (0 = bottom of the rect) for a 0..1 fill.</summary>
            public float RowFor(float fill) => Bottom + Mathf.Clamp01(fill) * (Top - Bottom);

            private const float Alpha = 0.10f;   // the glass is see-through: catch its edge low
            private const int Wall = 2;          // px of glass the drink must stay inside

            public static Cavity Measure(Sprite sprite)
            {
                if (sprite == null || sprite.texture == null) return null;
                Texture2D tex = sprite.texture;
                if (!tex.isReadable) return null;

                Rect r = sprite.textureRect;
                int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
                int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
                if (w <= 4 || h <= 8) return null;

                Color32[] px;
                try { px = tex.GetPixels32(); }
                catch { return null; }              // compressed or otherwise unreadable
                int tw = tex.width;

                var lo = new int[h];
                var hi = new int[h];
                int widest = 0;
                for (int y = 0; y < h; y++)
                {
                    int l = -1, rr = -1;
                    for (int x = 0; x < w; x++)
                    {
                        if (px[(y0 + y) * tw + x0 + x].a < Alpha * 255f) continue;
                        if (l < 0) l = x;
                        rr = x;
                    }
                    lo[y] = l; hi[y] = rr;
                    if (l >= 0 && rr - l + 1 > widest) widest = rr - l + 1;
                }
                if (widest < 6) return null;

                // The SHOULDER is where the silhouette first reaches most of its width,
                // coming down from the cap. A full bottle fills to just inside the neck
                // above it — never to the brim, because a bottle with no air in it reads
                // as a solid block of colour rather than as a bottle with drink in it.
                int shoulder = -1;
                for (int y = h - 1; y >= 0; y--)
                {
                    if (lo[y] < 0) continue;
                    if (hi[y] - lo[y] + 1 >= widest * 0.80f) { shoulder = y; break; }
                }
                if (shoulder < 0) return null;

                int neck = h - 1;
                for (int y = shoulder; y < h; y++) { if (lo[y] < 0) { neck = y - 1; break; } }
                int top = Mathf.Min(neck, shoulder + Mathf.RoundToInt((neck - shoulder) * 0.35f));

                // The base: the first row up from the floor that is as wide as the body, so
                // the drink sits ON the glass bottom instead of inside the thick foot.
                int bottom = 0;
                for (int y = 0; y < shoulder; y++)
                {
                    if (lo[y] >= 0 && hi[y] - lo[y] + 1 >= widest * 0.72f) { bottom = y; break; }
                }
                bottom = Mathf.Min(bottom + Wall, top - 1);
                if (top <= bottom) return null;

                var left = new float[h];
                var right = new float[h];
                for (int y = 0; y < h; y++)
                {
                    if (lo[y] < 0) { left[y] = right[y] = 0.5f; continue; }
                    float l = lo[y] + Wall, rr = hi[y] - Wall;
                    if (rr <= l) { float m = (lo[y] + hi[y]) * 0.5f; l = m; rr = m; }
                    left[y] = l / w;
                    right[y] = (rr + 1f) / w;
                }

                // How much of the body the drink could show through, measured where it
                // would actually be drawn rather than over the whole sprite.
                int clear = 0, body = 0;
                for (int y = bottom; y <= top; y++)
                {
                    if (lo[y] < 0) continue;
                    for (int x = Mathf.RoundToInt(left[y] * w); x < Mathf.RoundToInt(right[y] * w); x++)
                    {
                        byte a = px[(y0 + y) * tw + x0 + x].a;
                        if (a == 0) continue;
                        body++;
                        if (a < 250) clear++;
                    }
                }
                return new Cavity(left, right, bottom, top,
                    body > 0 ? clear / (float)body : 0f);
            }
        }

        private static readonly Dictionary<Sprite, Cavity> CavityCache =
            new Dictionary<Sprite, Cavity>();

        /// <summary>Where each bottle's drink was last drawn, by ingredient id.
        ///
        /// The shelf is not updated, it is DESTROYED AND REBUILT (RefreshMenu drops every
        /// child and lays the wall out again), and a pour refreshes it — so without this the
        /// fluid a player is watching fall is deleted mid-fall and replaced by a fresh one
        /// already at its new level. The drain would be perfectly correct and completely
        /// invisible, which is the whole feature. Remembering the DRAWN level outside the
        /// GameObject lets a rebuilt bottle pick the animation up exactly where the destroyed
        /// one left it.</summary>
        private static readonly Dictionary<string, float> Shown = new Dictionary<string, float>();

        private static readonly HashSet<Sprite> Warned = new HashSet<Sprite>();

        public static void ClearCache()
        {
            CavityCache.Clear();
            Shown.Clear();
            Warned.Clear();
        }

        private static Cavity CavityFor(Sprite sprite)
        {
            if (sprite == null) return null;
            if (CavityCache.TryGetValue(sprite, out var c)) return c;
            c = Cavity.Measure(sprite);
            CavityCache[sprite] = c;            // a null is cached too: do not re-measure a miss
            return c;
        }

        // ── the level ───────────────────────────────────────────────────────────────────
        [SerializeField] private Sprite _bottle;
        private Cavity _cavity;
        private string _key;

        private float _target = 1f;     // what the rules say is left
        private float _shown = -1f;     // what is drawn, chasing it (-1: not yet placed)
        private float _vel;             // level velocity, for the spring and for the slosh
        private float _wave;            // slosh amplitude, in rect units
        private float _phase;

        /// <summary>How fast the drawn level catches the real one. A pour should be VISIBLE
        /// as a fall, not a jump — the whole feel the author asked for lives in this lag.</summary>
        private const float Chase = 6.5f;
        private const float WaveDecay = 2.6f;
        private const float WaveSpeed = 11f;
        private const float WaveMax = 0.055f;

        /// <summary>The art this fluid is measured against, and the bottle it belongs to.
        /// Set both before the level: <paramref name="key"/> is what survives a rebuild.</summary>
        public void Bind(Sprite bottle, string key)
        {
            _bottle = bottle;
            _cavity = CavityFor(bottle);
            _key = key;
            _shown = key != null && Shown.TryGetValue(key, out float s) ? s : -1f;
            enabled = _cavity != null && _cavity.Valid;

            // Say it once per sprite, not once per bottle per rebuild.
            if (_cavity != null && _cavity.SeeThrough < 0.02f && Warned.Add(bottle))
                Debug.LogWarning($"BottleFluid: '{bottle.name}' has an opaque body " +
                    $"({_cavity.SeeThrough:P0} see-through), so the drink drawn behind it " +
                    "cannot be seen. This layer wants the see-through pilot art.");
            SetVerticesDirty();
        }

        /// <summary>The rules' level, 0..1 — where the drink is HEADING. The first call on a
        /// bottle nobody has seen pour places it there outright; after that it is chased, so
        /// the fall is something the player watches rather than something already over.</summary>
        public void SetLevel(float fill)
        {
            _target = Mathf.Clamp01(fill);
            if (_shown < 0f) _shown = _target;      // first sight of this bottle
            SetVerticesDirty();
        }

        public float Level => Mathf.Max(0f, _shown);

        private void Update()
        {
            if (_cavity == null || _shown < 0f) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float before = _shown;
            _shown = Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-Chase * dt));
            _vel = (_shown - before) / Mathf.Max(dt, 1e-4f);

            // The surface only comes alive when the level actually moves: draining pushes
            // energy into it, and it settles on its own. A still bottle draws a still drink.
            float kick = Mathf.Abs(_vel) * 0.30f;
            _wave = Mathf.Max(_wave * Mathf.Exp(-WaveDecay * dt), Mathf.Min(kick, WaveMax));
            _phase += WaveSpeed * dt;
            if (_key != null) Shown[_key] = _shown;   // survive the next RefreshMenu

            if (Mathf.Abs(_shown - before) > 1e-5f || _wave > 1e-4f) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var cav = _cavity;
            if (cav == null || !cav.Valid || _shown <= 0.0005f) return;

            Rect rect = GetPixelAdjustedRect();
            int rows = cav.Left.Length;
            float rowH = rect.height / rows;
            float surface = cav.RowFor(_shown);          // in rows, from the bottom

            // The surface ellipse's depth follows how wide the bottle is where it sits: a
            // level up in the neck draws a narrow oval, one in the belly a wide one.
            int sRow = Mathf.Clamp(Mathf.FloorToInt(surface), 0, rows - 1);
            float halfW = (cav.Right[sRow] - cav.Left[sRow]) * 0.5f * rect.width;
            float ry = Mathf.Max(1f, halfW * 0.34f) / Mathf.Max(rowH, 1e-4f);   // in rows

            Color32 body = color;
            Color32 shade = new Color(color.r * 0.80f, color.g * 0.80f, color.b * 0.80f, color.a);
            Color32 rim = new Color(Mathf.Min(1f, color.r * 1.16f), Mathf.Min(1f, color.g * 1.16f),
                                    Mathf.Min(1f, color.b * 1.16f), color.a);

            int top = Mathf.Min(rows - 1, Mathf.CeilToInt(surface + ry));
            int prev = -1;
            for (int y = cav.Bottom; y <= top; y++)
            {
                float l = rect.x + cav.Left[y] * rect.width;
                float r = rect.x + cav.Right[y] * rect.width;
                if (r <= l) continue;
                float yy = rect.y + y * rowH;

                // How much of THIS row the drink covers, across the row's width. Outside the
                // ellipse the row is empty; inside it, the near half is full.
                if (prev >= 0) AddBand(vh, prev, y, cav, rect, rowH, surface, ry, body, shade, rim);
                prev = y;
            }
        }

        /// <summary>One horizontal band of drink, between two measured rows.</summary>
        private void AddBand(VertexHelper vh, int y0, int y1, Cavity cav, Rect rect,
            float rowH, float surface, float ry, Color32 body, Color32 shade, Color32 rim)
        {
            const int Seg = 6;      // across the bottle: enough to curve the surface, cheap
            float yA = rect.y + y0 * rowH, yB = rect.y + y1 * rowH;
            float lA = rect.x + cav.Left[y0] * rect.width, rA = rect.x + cav.Right[y0] * rect.width;
            float lB = rect.x + cav.Left[y1] * rect.width, rB = rect.x + cav.Right[y1] * rect.width;

            for (int s = 0; s < Seg; s++)
            {
                float t0 = s / (float)Seg, t1 = (s + 1) / (float)Seg;
                float x0A = Mathf.Lerp(lA, rA, t0), x1A = Mathf.Lerp(lA, rA, t1);
                float x0B = Mathf.Lerp(lB, rB, t0), x1B = Mathf.Lerp(lB, rB, t1);

                // the surface height over each column, in rect units
                float s0 = SurfaceAt(t0, surface, ry) * rowH + rect.y;
                float s1 = SurfaceAt(t1, surface, ry) * rowH + rect.y;

                float a0 = Mathf.Min(yA, s0), b0 = Mathf.Min(yB, s0);
                float a1 = Mathf.Min(yA, s1), b1 = Mathf.Min(yB, s1);
                if (b0 <= a0 && b1 <= a1) continue;

                Color32 cL = LerpC(body, shade, Mathf.Pow(Mathf.Abs(t0 * 2f - 1f), 1.6f));
                Color32 cR = LerpC(body, shade, Mathf.Pow(Mathf.Abs(t1 * 2f - 1f), 1.6f));
                bool crest = b0 >= s0 - rowH || b1 >= s1 - rowH;

                int i = vh.currentVertCount;
                vh.AddVert(new Vector3(x0A, a0), cL, Vector2.zero);
                vh.AddVert(new Vector3(x1A, a1), cR, Vector2.zero);
                vh.AddVert(new Vector3(x1B, b1), crest ? rim : cR, Vector2.zero);
                vh.AddVert(new Vector3(x0B, b0), crest ? rim : cL, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i + 2, i + 3, i);
            }
        }

        /// <summary>The waterline over the bottle at 0..1 across it, in rows.</summary>
        private float SurfaceAt(float t, float surface, float ry)
        {
            float nx = t * 2f - 1f;
            float dome = ry * Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx));
            float slosh = _wave * Mathf.Sin(_phase + nx * 2.2f) * Mathf.Max(ry, 1f) * 6f;
            return surface + dome + slosh;
        }

        private static Color32 LerpC(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, t);

        /// <summary>No texture: the drink is flat colour shaded across the bottle.</summary>
        public override Texture mainTexture => s_WhiteTexture;
    }
}
