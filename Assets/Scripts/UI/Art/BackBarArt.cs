using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// The back-bar wall, drawn in code (2026-08-01). The generated wall kit came back with
    /// framed tile edges that repeated as a white grid, and the author's ruling followed:
    /// design it here. Everything is a small procedural sprite from a deterministic hash —
    /// the Speckles precedent — and the 3D read comes from geometry, not from a picture:
    /// each shelf is a NICHE — a shadow falling from above, a perspective-trapezoid floor
    /// that is wider and lighter at its front edge, a lit lip below it, and an elliptical
    /// shadow under every bottle standing on it.
    /// </summary>
    public static class BackBarArt
    {
        private static Sprite _shadow, _ledge, _luxe;

        // The luxe ramp (the author, 2026-08-01: "daha lüks bir back bar" — less timber,
        // more lounge): deep aubergine-charcoal panels off the stage's Night palette, with
        // brass only where an edge catches the cornice light.
        // Vice, not velvet (the author, 2026-08-02): teal-navy panels, cool light, and
        // the accents are NEON tubes, not brass edges.
        private static readonly Color32 PanelSeam = new Color32(0x07, 0x0B, 0x12, 0xFF);
        private static readonly Color32 PanelA = new Color32(0x11, 0x1C, 0x26, 0xFF);
        private static readonly Color32 PanelB = new Color32(0x0E, 0x18, 0x21, 0xFF);
        private static readonly Color32 PanelEdge = new Color32(0x1C, 0x33, 0x3E, 0xFF);

        // The walnut ramp the whole wall is built from.
        private static readonly Color32 Seam = new Color32(0x12, 0x0A, 0x08, 0xFF);
        private static readonly Color32 FloorFront = new Color32(0x4A, 0x5E, 0x6E, 0xFF);
        private static readonly Color32 FloorMid = new Color32(0x33, 0x44, 0x52, 0xFF);
        private static readonly Color32 LipShine = new Color32(0x6E, 0xE0, 0xD6, 0xFF);

        /// <summary>The ellipse a bottle stands in — what pins it to the floor plane.</summary>
        public static Sprite BottleShadow()
        {
            if (_shadow != null) return _shadow;
            const int W = 64, H = 16;
            var px = new Color32[W * H];
            float cx = (W - 1) / 2f, cy = (H - 1) / 2f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - cx) / cx, dy = (y - cy) / cy;
                    float d = dx * dx + dy * dy;
                    byte a = d >= 1f ? (byte)0 : (byte)(120 * (1f - d));
                    px[y * W + x] = new Color32(0, 0, 0, a);
                }
            return _shadow = Make(px, W, H);
        }

        /// <summary>The counter ledge at the bottom of the wall — the same floor plane, taller,
        /// where SERVE and the bin stand.</summary>
        public static Sprite Ledge()
        {
            if (_ledge != null) return _ledge;
            const int W = 320, H = 44, inset = 14;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                float depth = y / (float)(H - 1);
                int side = Mathf.RoundToInt(inset * depth);
                Color32 tone = Color32.Lerp(FloorFront, FloorMid, depth);
                if (y < 4) tone = Color32.Lerp(FloorFront, LipShine, 0.3f);
                for (int x = side; x < W - side; x++)
                    px[y * W + x] = (x == side || x == W - side - 1) ? Seam : tone;
            }
            return _ledge = Make(px, W, H);
        }

        /// <summary>
        /// The luxe wall (replaces the walnut boards on the back bar): tall aubergine
        /// panels with a bevelled inner edge and a slim double groove down the middle,
        /// warmed toward the top where the cornice lamps pool. Seams sit at the sprite's
        /// own edges so the tiling is invisible by construction, like <see cref="Boards"/>.
        /// </summary>
        public static Sprite LuxeWall()
        {
            if (_luxe != null) return _luxe;
            const int W = 128, H = 128, panel = 64;
            var px = new Color32[W * H];
            uint hash = 1583;
            for (int x = 0; x < W; x++)
            {
                int p = x / panel, lx = x % panel;
                for (int y = 0; y < H; y++)
                {
                    hash = (hash ^ (uint)(x * 61 + y * 23 + p * 211)) * 16777619;
                    Color32 c = p % 2 == 0 ? PanelA : PanelB;
                    if (lx == 0) c = PanelSeam;                       // panel joint
                    else if (lx == 1) c = PanelEdge;                  // its lit bevel
                    else if (lx == panel - 1) c = PanelSeam;          // and the far shadow
                    else if (lx == 30 || lx == 33) c = PanelSeam;     // the deco double groove
                    else if (lx == 31 || lx == 34) c = PanelEdge;
                    else if ((hash >> 8) % 61 == 0) c = PanelSeam;    // a fleck of wear
                    float cool = y / (float)(H - 1);
                    px[y * W + x] = new Color32(
                        (byte)Mathf.Min(255, c.r + (int)(3 * cool)),
                        (byte)Mathf.Min(255, c.g + (int)(9 * cool) + 2),
                        (byte)Mathf.Min(255, c.b + (int)(13 * cool) + 4), 255);
                }
            }
            return _luxe = Make(px, W, H);
        }

        private static Sprite _plate;

        /// <summary>A small brass-framed walnut plate, 9-sliced so it stretches to any
        /// name's width (the author: each bottle's sign fits its own name).</summary>
        public static Sprite NamePlate()
        {
            if (_plate != null) return _plate;
            const int W = 24, H = 20;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color32 c = new Color32(0x0B, 0x10, 0x18, 0xE8);
                    if (x == 0 || y == 0 || x == W - 1 || y == H - 1) c = PanelSeam;
                    else if (x == 1 || y == 1 || x == W - 2 || y == H - 2)
                        c = new Color32(0x2F, 0xA8, 0xA0, 0xFF);
                    px[y * W + x] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return _plate = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
        }

        private static Sprite _infoPlate, _infoTail;

        /// <summary>
        /// The bubble that answers what is in a bottle, hung UNDER it (the author,
        /// 2026-08-02: the old panel was a bare dark rectangle sitting on top of the
        /// bottle, hiding the very thing it described). Cut corners and a lit top edge,
        /// so it reads as a card raised off the wall rather than a hole in it; 9-sliced,
        /// because a bottle's name decides its width.
        /// </summary>
        public static Sprite InfoPlate()
        {
            if (_infoPlate != null) return _infoPlate;
            const int W = 24, H = 24, Cut = 3;
            var px = new Color32[W * H];
            var fill = new Color32(0x0B, 0x10, 0x18, 0xF4);
            var neon = new Color32(0x2F, 0xA8, 0xA0, 0xFF);
            var neonLit = new Color32(0x6E, 0xE0, 0xD6, 0xFF);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // the four corners are chamfered away, which is what stops a panel this
                    // small from reading as a plain box
                    int cx = System.Math.Min(x, W - 1 - x), cy = System.Math.Min(y, H - 1 - y);
                    if (cx + cy < Cut) { px[y * W + x] = new Color32(0, 0, 0, 0); continue; }
                    Color32 c = fill;
                    if (cx + cy == Cut || x == 0 || y == 0 || x == W - 1 || y == H - 1) c = PanelSeam;
                    else if (cx + cy == Cut + 1 || x == 1 || y == 1 || x == W - 2 || y == H - 2)
                        c = y >= H - 2 ? neonLit : neon;      // the tube catches along the top
                    px[y * W + x] = c;
                }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return _infoPlate = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
        }

        /// <summary>The bubble's spout, pointing up at the bottle it belongs to. Its lowest
        /// rows are plain fill so they cover the plate's own top edge and the two become one
        /// shape.</summary>
        public static Sprite InfoTail()
        {
            if (_infoTail != null) return _infoTail;
            const int W = 13, H = 9;
            var px = new Color32[W * H];
            var fill = new Color32(0x0B, 0x10, 0x18, 0xF4);
            var neonLit = new Color32(0x6E, 0xE0, 0xD6, 0xFF);
            for (int y = 0; y < H; y++)
            {
                // a spout one pixel narrower per row, so its slopes land on whole pixels
                int half = (H - 1 - y);
                int mid = W / 2;
                for (int x = mid - half; x <= mid + half; x++)
                {
                    if (x < 0 || x >= W) continue;
                    bool slope = x == mid - half || x == mid + half;
                    bool skirt = y <= 1;                       // sits over the plate's edge
                    // The slopes carry the same tube as the card's border, so the spout
                    // belongs to the card instead of being a dark wedge stuck to it.
                    px[y * W + x] = skirt ? fill : slope ? neonLit : fill;
                }
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply();
            return _infoTail = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f),
                100f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite _keg;

        /// <summary>
        /// A steel keg seen from slightly above (the author's perspective sketch: a wide
        /// top ellipse, straight sides) — drawn here because the generator's credits ran
        /// out the night it was ordered. Only the crown shows in frame, so the sprite is
        /// the crown done properly: top ellipse with a rolled rim, recessed well, centre
        /// spear valve, and a strip of ribbed side wall with one handling band.
        ///
        /// **NOTHING CALLS THIS SINCE 2026-08-15**, and it is kept rather than deleted: it is
        /// hand-drawn art, not logic. Its call site was the back-bar keg row, which went when
        /// beer left the wall for the font on the counter. The kegs the player still sees —
        /// the one on the line and the spares in the bays — are `ItemArt.Load("keg")` in the
        /// draught station, and this stands in for nothing there.
        /// </summary>
        public static Sprite KegCrown()
        {
            if (_keg != null) return _keg;
            const int W = 200, H = 170;
            var px = new Color32[W * H];   // all clear
            var steelLit = new Color32(0xC2, 0xC6, 0xCE, 0xFF);
            var steel = new Color32(0x93, 0x98, 0xA2, 0xFF);
            var steelDim = new Color32(0x6A, 0x70, 0x7A, 0xFF);
            var steelDark = new Color32(0x44, 0x49, 0x54, 0xFF);
            var outline = new Color32(0x10, 0x0E, 0x14, 0xFF);
            const float cx = 99.5f, topCy = 136f, rx = 92f, ry = 26f;

            // side wall: a cylinder shaded off its curvature, warmed near the top light
            for (int y = 0; y < (int)topCy; y++)
                for (int x = 0; x < W; x++)
                {
                    float t = Mathf.Abs(x - cx) / rx;
                    if (t > 1f) continue;
                    // inside the silhouette only below the top ellipse's front edge
                    float edgeY = topCy - ry * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                    if (y > edgeY) continue;
                    Color32 c = Color32.Lerp(steel, steelDark, t * t);
                    if (t < 0.22f) c = Color32.Lerp(steelLit, steel, t / 0.22f);
                    bool band = y >= 96 && y <= 108;
                    if (band) c = (y == 96 || y == 108) ? steelDark : Color32.Lerp(c, steelLit, 0.35f);
                    if ((x & 15) == 0) c = Color32.Lerp(c, steelDark, 0.4f);   // a rib
                    px[y * W + x] = c;
                }

            // top ellipse: rim, then the recessed well, then the spear valve
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - topCy) / ry;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    Color32 c = d > 0.90f ? steelLit                                  // rolled rim
                        : d > 0.72f ? steel
                        : Color32.Lerp(steelDim, steelDark, 1f - d);                  // the well
                    px[y * W + x] = c;
                }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x - cx) / 16f, dy = (y - topCy) / 5f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    px[y * W + x] = d > 0.55f ? steelLit : steelDark;                 // the valve
                }

            // silhouette outline
            var src = (Color32[])px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (src[y * W + x].a != 0) continue;
                    bool edge = (x > 0 && src[y * W + x - 1].a != 0) ||
                                (x < W - 1 && src[y * W + x + 1].a != 0) ||
                                (y > 0 && src[(y - 1) * W + x].a != 0) ||
                                (y < H - 1 && src[(y + 1) * W + x].a != 0);
                    if (edge) px[y * W + x] = outline;
                }
            return _keg = Make(px, W, H);
        }

        private static Sprite Make(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
        }
    }
}
