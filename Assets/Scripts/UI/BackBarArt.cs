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
        private static Sprite _boards, _floor, _lip, _shadow, _nicheTop, _ledge, _luxe, _face;

        // The luxe ramp (the author, 2026-08-01: "daha lüks bir back bar" — less timber,
        // more lounge): deep aubergine-charcoal panels off the stage's Night palette, with
        // brass only where an edge catches the cornice light.
        private static readonly Color32 PanelSeam = new Color32(0x0B, 0x09, 0x11, 0xFF);
        private static readonly Color32 PanelA = new Color32(0x1D, 0x18, 0x27, 0xFF);
        private static readonly Color32 PanelB = new Color32(0x19, 0x14, 0x22, 0xFF);
        private static readonly Color32 PanelEdge = new Color32(0x2E, 0x26, 0x3C, 0xFF);
        private static readonly Color32 Brass = new Color32(0xB8, 0x8A, 0x3C, 0xFF);
        private static readonly Color32 BrassLit = new Color32(0xE6, 0xBE, 0x66, 0xFF);
        private static readonly Color32 FaceWood = new Color32(0x30, 0x1E, 0x12, 0xFF);
        private static readonly Color32 FaceWoodDim = new Color32(0x28, 0x18, 0x0E, 0xFF);

        // The walnut ramp the whole wall is built from.
        private static readonly Color32 Seam = new Color32(0x12, 0x0A, 0x08, 0xFF);
        private static readonly Color32 BoardA = new Color32(0x2A, 0x1A, 0x10, 0xFF);
        private static readonly Color32 BoardB = new Color32(0x24, 0x16, 0x0E, 0xFF);
        private static readonly Color32 FloorFront = new Color32(0x8E, 0x5C, 0x30, 0xFF);
        private static readonly Color32 FloorMid = new Color32(0x6A, 0x42, 0x22, 0xFF);
        private static readonly Color32 FloorBack = new Color32(0x46, 0x2A, 0x16, 0xFF);
        private static readonly Color32 LipFace = new Color32(0x38, 0x20, 0x10, 0xFF);
        private static readonly Color32 LipShine = new Color32(0xB8, 0x84, 0x48, 0xFF);

        /// <summary>The wall: vertical walnut boards, tiling cleanly because the seams are
        /// drawn at the sprite's own edges — no baked frame, which is what broke the kit.</summary>
        public static Sprite Boards()
        {
            if (_boards != null) return _boards;
            const int W = 96, H = 96, board = 24;
            var px = new Color32[W * H];
            uint hash = 977;
            for (int x = 0; x < W; x++)
            {
                int b = x / board;
                bool seam = x % board == 0;
                for (int y = 0; y < H; y++)
                {
                    hash = (hash ^ (uint)(x * 53 + y * 17 + b * 191)) * 16777619;
                    Color32 c = seam ? Seam : (b % 2 == 0 ? BoardA : BoardB);
                    if (!seam && (hash >> 9) % 37 == 0) c = Seam;               // a grain fleck
                    if (!seam && (hash >> 7) % 53 == 0)
                        c = new Color32((byte)(c.r + 8), (byte)(c.g + 5), c.b, 255);
                    // The cornice lamps pool warm light down the boards: brighter toward the
                    // top of each tile, so the tiled wall reads lit rather than flat.
                    if (!seam)
                    {
                        float warm = y / (float)(H - 1);
                        c = new Color32((byte)Mathf.Min(255, c.r + (int)(16 * warm) + 6),
                                        (byte)Mathf.Min(255, c.g + (int)(10 * warm) + 3),
                                        c.b, 255);
                    }
                    px[y * W + x] = c;
                }
            }
            return _boards = Make(px, W, H);
        }

        /// <summary>The shelf floor: a perspective trapezoid, front edge full width and lit,
        /// receding rows narrower and darker — the plane you read as depth.</summary>
        public static Sprite ShelfFloor()
        {
            if (_floor != null) return _floor;
            const int W = 320, H = 30, inset = 22;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)                       // y 0 = bottom = front edge
            {
                float depth = y / (float)(H - 1);             // 0 front → 1 back
                int side = Mathf.RoundToInt(inset * depth);
                Color32 tone = Color32.Lerp(FloorFront, FloorBack, depth * depth);
                if (y < 3) tone = Color32.Lerp(FloorFront, LipShine, 0.35f);
                for (int x = side; x < W - side; x++)
                {
                    bool edge = x == side || x == W - side - 1;
                    px[y * W + x] = edge ? Seam : tone;
                }
            }
            return _floor = Make(px, W, H);
        }

        /// <summary>The lip under the floor's front edge: dark face, one bright brass line.</summary>
        public static Sprite Lip()
        {
            if (_lip != null) return _lip;
            const int W = 8, H = 10;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    px[y * W + x] = y == H - 1 ? LipShine : y == 0 ? Seam : LipFace;
            return _lip = Make(px, W, H);
        }

        /// <summary>The shadow the shelf above throws into the niche: a soft falloff.</summary>
        public static Sprite NicheTop()
        {
            if (_nicheTop != null) return _nicheTop;
            const int W = 8, H = 36;
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
            {
                byte a = (byte)(150 * (y / (float)(H - 1)) * (y / (float)(H - 1)));
                for (int x = 0; x < W; x++) px[y * W + x] = new Color32(0, 0, 0, a);
            }
            return _nicheTop = Make(px, W, H);
        }

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
                    float warm = y / (float)(H - 1);
                    px[y * W + x] = new Color32(
                        (byte)Mathf.Min(255, c.r + (int)(14 * warm) + 4),
                        (byte)Mathf.Min(255, c.g + (int)(8 * warm) + 2),
                        (byte)Mathf.Min(255, c.b + (int)(4 * warm)), 255);
                }
            }
            return _luxe = Make(px, W, H);
        }

        /// <summary>
        /// The shelf's FRONT FACE (the author: shelves thick enough to carry the bottle
        /// names): dark walnut with a brass edge along the top where the light lands, a
        /// near-black shadow along the bottom. The names are lettered over it in engine.
        /// </summary>
        public static Sprite ShelfFace()
        {
            if (_face != null) return _face;
            const int W = 64, H = 36;
            var px = new Color32[W * H];
            uint hash = 733;
            for (int y = 0; y < H; y++)                       // y 0 = bottom
                for (int x = 0; x < W; x++)
                {
                    hash = (hash ^ (uint)(x * 47 + y * 29)) * 16777619;
                    Color32 c;
                    if (y == H - 1) c = BrassLit;
                    else if (y == H - 2) c = Brass;
                    else if (y == H - 3) c = PanelSeam;
                    else if (y <= 1) c = PanelSeam;
                    else
                    {
                        c = (hash >> 6) % 7 == 0 ? FaceWoodDim : FaceWood;
                        if ((hash >> 9) % 43 == 0) c = new Color32(0x3A, 0x26, 0x16, 0xFF);
                    }
                    px[y * W + x] = c;
                }
            return _face = Make(px, W, H);
        }

        private static Sprite _keg;

        /// <summary>
        /// A steel keg seen from slightly above (the author's perspective sketch: a wide
        /// top ellipse, straight sides) — drawn here because the generator's credits ran
        /// out the night it was ordered. Only the crown shows in frame, so the sprite is
        /// the crown done properly: top ellipse with a rolled rim, recessed well, centre
        /// spear valve, and a strip of ribbed side wall with one handling band.
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
