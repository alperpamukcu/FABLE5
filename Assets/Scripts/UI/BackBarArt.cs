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
        private static Sprite _boards, _floor, _lip, _shadow, _nicheTop, _ledge;

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
