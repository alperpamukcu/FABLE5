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
        private static Sprite _shadow;

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

        private static Sprite _coaster;

        /// <summary>
        /// THE DRINK'S MAT, drawn — and drawn AGAIN on 2026-09-04 (the author: "sahnedeki
        /// bardak altligi yeniden uretilmeli ve masanin yuzeyine tam otursun").
        ///
        /// A generated one shipped first and did not fit: at its own aspect it stood 38
        /// units deep under a 92-unit glass, which is not a mat but a bowl. What replaced it
        /// was an ellipse of two colours, and that was the fault this take answers — it had
        /// no THICKNESS, so it read as a stain on the bar rather than as an object lying on
        /// it, and it was centred on the counter's foot line with half its body hanging off
        /// the bar's front edge into the shelf bays below.
        ///
        /// So it is built the way every other dish on this counter is drawn: a top face seen
        /// at the bar's own low angle, a dark side under its front arc, and one ink outline
        /// round the whole silhouette. 56x18 art px drawn at 112x36 — one art pixel to two
        /// HUD units, the counter's own grain (the book prop's rule), and shallow enough to
        /// lie WHOLLY inside the counter's drawn band at
        /// <see cref="TycoonHud"/>'s foot line.
        ///
        /// Three rings out from the middle: a worn centre where glasses have stood, cork
        /// around it (lighter along the back, where the room reaches it), and a brass edge
        /// that catches the neon the way every other fitting on this bar does.
        /// </summary>
        public static Sprite Coaster()
        {
            if (_coaster != null) return _coaster;
            const int W = 56, H = 18;
            // How much of the canvas the TOP FACE takes; the rest is the mat's own edge,
            // dropped straight down from the face's front arc.
            const float FaceH = 15f;
            const int Thick = 3;
            var px = new Color32[W * H];
            var ink = new Color32(0x14, 0x0D, 0x10, 0xFF);
            var side = new Color32(0x3A, 0x22, 0x10, 0xFF);
            var sideDark = new Color32(0x25, 0x15, 0x0A, 0xFF);
            var cork = new Color32(0x7A, 0x4C, 0x24, 0xFF);
            var corkLit = new Color32(0x8C, 0x5A, 0x2E, 0xFF);
            var worn = new Color32(0x66, 0x3F, 0x1D, 0xFF);
            var brass = new Color32(0xC9, 0x82, 0x2B, 0xFF);
            var lit = new Color32(0xE8, 0xA3, 0x3D, 0xFF);
            float cx = (W - 1) / 2f, cy = (FaceH - 1) / 2f;
            float rx = (W - 1) / 2f, ry = (FaceH - 1) / 2f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    // ROWS FROM THE TOP, because that is how the drawing was laid out and
                    // Unity's texture rows run the other way. The back of the mat is the
                    // small row and it is the half the room's light reaches.
                    int row = H - 1 - y;
                    float dx = (x - cx) / rx, dy = (row - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = new Color32(0, 0, 0, 0);
                    if (d <= 1f)
                    {
                        if (d > 0.93f) c = ink;                       // the silhouette's own line
                        else if (d > 0.80f) c = row < cy ? lit : brass;
                        else if (d > 0.52f) c = row < cy ? corkLit : cork;
                        else c = worn;                                // where glasses have stood
                    }
                    else if (row > cy)
                    {
                        // The mat has a body: the same ellipse dropped Thick rows is its
                        // front edge, and what shows of it between the two arcs is the side.
                        float dy2 = (row - Thick - cy) / ry;
                        float d2 = Mathf.Sqrt(dx * dx + dy2 * dy2);
                        if (d2 <= 1f) c = d2 > 0.93f ? ink : d2 > 0.80f ? sideDark : side;
                    }
                    px[y * W + x] = c;
                }
            return _coaster = Make(px, W, H);
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
