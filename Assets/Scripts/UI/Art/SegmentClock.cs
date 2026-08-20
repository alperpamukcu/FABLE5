using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The board's digital readout: four digits and a colon, drawn PIXEL BY PIXEL from
    /// hand-authored masks (2026-08-19, the author: "bunu sayılarla değilde gerçekten
    /// kodlar pixel pixel yapsak? çünkü 5-2 gibi sayıların uçları yanmıyor, yandığında
    /// 1 sayısı gibi sayılar kısa kalıyor. Detaylı bir saat çalışması yap").
    ///
    /// It was seven segment RECTANGLES per digit, and that geometry cannot serve two
    /// masters: give the corner squares to the vertical bars and a 5's top stroke stops
    /// a bar short of its corner — "uçları yanmıyor" — and give them to the horizontals
    /// instead and a 1, which lights nothing horizontal, floats at two thirds of the
    /// digit height. A real display's segments are MITRED into the corners; on a pixel
    /// grid the honest version of that is to draw each numeral whole: every corner lit,
    /// every digit the full 14 rows, the seams only where a display actually shows them.
    ///
    /// The masks live in Tools/clock_digits.py first — that file renders the proof
    /// sheets the design was judged on, and its `csharp` mode prints these exact rows.
    /// Change the drawing THERE, look at the proof, then re-print it here.
    ///
    /// The grain: masks are 11×14 in a 13×16 cell (one pixel of margin carries the
    /// halo), drawn at exactly 2× — whole multiples only, the same law as the 8px
    /// fonts. What survives of the display language: the unlit machine is still on the
    /// glass (the ghost 8 under every digit, alpha per the 2026-08-14 tuning), each lit
    /// numeral wears a one-pixel halo, and the colon keeps its own half-second.
    /// </summary>
    public sealed class SegmentClock
    {
        // ── the drawing (11×14, '#' lit) ────────────────────────────────────────
        // Bars and arms are 2px. Outer corners are chamfered by one pixel — the
        // moulded corner of a display — and inner joins are square, so two strokes
        // meeting reads as one lit corner and never as a notch.
        private static readonly string[][] Digits =
        {
            new[] { ".#########.", "###########", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "###########", ".#########." },
            new[] { "........###", "........###", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##" },
            new[] { ".#########.", "###########", ".........##", ".........##", ".........##", ".........##", ".##########", "##########.", "##.........", "##.........", "##.........", "##.........", "###########", ".#########." },
            new[] { ".#########.", "###########", ".........##", ".........##", ".........##", ".........##", "...########", "...#######.", ".........##", ".........##", ".........##", ".........##", "###########", ".#########." },
            new[] { "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "##.......##", "###########", ".##########", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##" },
            new[] { ".#########.", "###########", "##.........", "##.........", "##.........", "##.........", "##########.", ".##########", ".........##", ".........##", ".........##", ".........##", "###########", ".#########." },
            new[] { ".#########.", "###########", "##.........", "##.........", "##.........", "##.........", "##########.", "###########", "##.......##", "##.......##", "##.......##", "##.......##", "###########", ".#########." },
            new[] { ".##########", "###########", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##", ".........##" },
            new[] { ".#########.", "###########", "##.......##", "##.......##", "##.......##", "##.......##", "###########", "###########", "##.......##", "##.......##", "##.......##", "##.......##", "###########", ".#########." },
            new[] { ".#########.", "###########", "##.......##", "##.......##", "##.......##", "##.......##", "###########", ".##########", ".........##", ".........##", ".........##", ".........##", "###########", ".#########." },
        };

        private const int MaskW = 11, MaskH = 14, Pad = 1;          // cell 13×16
        private const int Scale = 2;                                 // drawn at 26×32
        private const float CellPitch = 26f;                         // digit advance
        private const float ColonW = 6f;                             // lamp block width

        // The ghost is the author's own tuning (2026-08-14, four tries down to 0.025:
        // "koyu mavi dijital izleri daha silik yap") — proof there is a machine behind
        // the glass, and nothing you have to read past to get the hour.
        private const float GhostAlpha = 0.025f;
        private const float HaloAlpha = 0.13f;

        // ── sprites, built once from the masks ──────────────────────────────────
        private static Sprite[] _digitSprites, _haloSprites;
        private static Sprite _ghostSprite;

        private readonly Image[] _ghost = new Image[4];
        private readonly Image[] _halo = new Image[4];
        private readonly Image[] _digit = new Image[4];
        private readonly Image[] _colon = new Image[2];
        private readonly int[] _shown = { -1, -1, -1, -1 };
        private Color _lit;
        private bool _colonShown = true;

        /// <summary>Builds the readout into <paramref name="host"/> — sized for the
        /// 110×28 glass the board gives it: four 26-unit cells and the colon block
        /// between the pairs, each cell drawing its 13×16 art at exactly 2×.</summary>
        public SegmentClock(RectTransform host, Color lit)
        {
            _lit = lit;
            EnsureSprites();
            float x = 0f;
            for (int d = 0; d < 4; d++)
            {
                // The cell rect is the ART's 13×16 at 2×, centred on the digit's own
                // 22-wide slot — the pixel of margin puts the halo one unit past the
                // numeral on every side, which is where a glass glow lives.
                var cell = NewRect(host, "D" + d,
                    new Vector2(MaskW * Scale + Pad * 2 * Scale, MaskH * Scale + Pad * 2 * Scale),
                    new Vector2(x - Pad * Scale, 0));
                _ghost[d] = NewImage(cell, "Ghost", _ghostSprite, Faded(GhostAlpha));
                _halo[d] = NewImage(cell, "Halo", null, Faded(0f));
                _digit[d] = NewImage(cell, "Lit", null, Faded(0f));
                x += CellPitch;
                if (d == 1)
                {
                    // The colon sits between the pairs, two square lamps on the grid.
                    // Whole units: at 0.19 of the digit height the lamps once landed on
                    // 5.32 and sat half a pixel off the grid (2026-08-14).
                    var colon = NewRect(host, "Colon", new Vector2(ColonW, MaskH * Scale),
                        new Vector2(x, 0));
                    for (int i = 0; i < 2; i++)
                    {
                        var lamp = NewRect(colon, "C" + i, new Vector2(4, 4),
                            new Vector2(1, (i == 0 ? -1f : 1f) * 5f));
                        var img = lamp.gameObject.AddComponent<Image>();
                        img.color = lit;
                        img.raycastTarget = false;
                        _colon[i] = img;
                    }
                    x += ColonW + 4f;
                }
            }
        }

        /// <summary>Puts a time on the glass. Digits repaint only when their value
        /// changes; the colon repaints only when its half-second flips.</summary>
        public void Show(int hours, int minutes, bool colonOn)
        {
            hours = Mathf.Clamp(hours, 0, 99);
            minutes = Mathf.Clamp(minutes, 0, 99);
            Paint(0, hours / 10); Paint(1, hours % 10);
            Paint(2, minutes / 10); Paint(3, minutes % 10);
            if (colonOn != _colonShown)
            {
                _colonShown = colonOn;
                foreach (var lamp in _colon)
                    if (lamp != null) lamp.color = colonOn ? _lit : Faded(GhostAlpha);
            }
        }

        /// <summary>Tints the whole readout — the plaque turns magenta at closing time,
        /// and a clock that stayed cyan through it would be the one thing in the room
        /// not saying so.</summary>
        public void SetHue(Color lit)
        {
            _lit = lit;
            for (int d = 0; d < 4; d++)
            {
                _digit[d].color = lit;
                _halo[d].color = Faded(HaloAlpha);
                _ghost[d].color = Faded(GhostAlpha);
            }
            foreach (var lamp in _colon)
                if (lamp != null) lamp.color = _colonShown ? _lit : Faded(GhostAlpha);
        }

        private void Paint(int slot, int value)
        {
            if (_shown[slot] == value) return;
            _shown[slot] = value;
            _digit[slot].sprite = _digitSprites[value];
            _digit[slot].color = _lit;
            _halo[slot].sprite = _haloSprites[value];
            _halo[slot].color = Faded(HaloAlpha);
        }

        private Color Faded(float a) => new Color(_lit.r, _lit.g, _lit.b, a);

        // ── sprite construction ─────────────────────────────────────────────────

        /// <summary>The mask sprites, cached for the domain's life. `!= null` and not a
        /// bool: with domain reload off a leftover static can hold destroyed sprites
        /// (the ChromeArt precedent).</summary>
        private static void EnsureSprites()
        {
            if (_digitSprites != null && _digitSprites[0] != null) return;
            _digitSprites = new Sprite[10];
            _haloSprites = new Sprite[10];
            for (int d = 0; d < 10; d++)
            {
                _digitSprites[d] = MaskSprite(Digits[d], halo: false);
                _haloSprites[d] = MaskSprite(Digits[d], halo: true);
            }
            _ghostSprite = MaskSprite(Digits[8], halo: false);   // the whole machine, unlit
        }

        /// <summary>One 13×16 sprite off an 11×14 mask: the numeral itself, or (halo)
        /// the one-pixel ring around it — white, tinted by the Image that wears it.</summary>
        private static Sprite MaskSprite(string[] rows, bool halo)
        {
            int w = MaskW + Pad * 2, h = MaskH + Pad * 2;
            var on = new bool[w, h];
            for (int y = 0; y < MaskH; y++)
                for (int x = 0; x < MaskW; x++)
                    // Mask rows read top-down; textures fill bottom-up.
                    on[x + Pad, (MaskH - 1 - y) + Pad] = rows[y][x] == '#';

            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    bool lit = on[x, y];
                    bool ring = false;
                    if (halo && !lit)
                        for (int dy = -1; dy <= 1 && !ring; dy++)
                            for (int dx = -1; dx <= 1 && !ring; dx++)
                            {
                                int nx = x + dx, ny = y + dy;
                                if (nx >= 0 && nx < w && ny >= 0 && ny < h && on[nx, ny])
                                    ring = true;
                            }
                    bool want = halo ? ring : lit;
                    px[y * w + x] = want ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
        }

        private static RectTransform NewRect(RectTransform parent, string name, Vector2 size, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        private static Image NewImage(RectTransform cell, string name, Sprite sprite, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(cell, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }
    }
}
