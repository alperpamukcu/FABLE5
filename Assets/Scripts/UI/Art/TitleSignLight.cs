using System;

namespace LastCall.UI
{
    /// <summary>
    /// THE TITLE SIGN'S LIGHT (2026-09-29; engine-free, so it can be played frame by frame outside Unity). A map
    /// texel is (class, group, where-along-its-tube). A group wears one DRESS at a time - dark, half, lit, or the
    /// travelling bead's warm and hot - and a (dress, class) pair is ONE palette token at ONE alpha. The clock is
    /// whole frames of 1/30 s since the sign was lit; every rule below is integer arithmetic on that frame, with no
    /// random number, so a frame is the same on every machine. Only what changed is repainted: a group whose dress
    /// changed, the stretch of tube each bead left and entered, the two outer bands on a breath's edge, the liquid.
    ///
    /// The critic's pass on the maker's port (2026-09-29): the bead was 200 texels of cream on a cream core,
    /// invisible at 1x - so the bead is twice as wide, its hot run turns the whole glass (body and lining) Cream[4]
    /// and flares its two inner bands a step, and a second, cyan bead runs through COCKTAIL BAR SIMULATOR half a
    /// period later. And FLASHES off holds the idle still - the bead and the coupe's glint are pulses too ("OFF:
    /// NOTHING FLICKERS, BLINKS OR PULSES"); only the warm-up's single dark-to-lit steps remain.
    /// </summary>
    public sealed class TitleSignLight
    {
        public enum Ramp : byte { Magenta, Cyan, Cream }

        // ── classes (map R) ─────────────────────────────────────────────────────────────────────────────────
        public const byte Empty = 0, B1 = 1, B2 = 2, B3 = 3, B4 = 4, Body = 5, Lining = 6, Core = 7, Liquid = 8;
        // ── dresses ─────────────────────────────────────────────────────────────────────────────────────────
        private const int Dark = 0, Half = 1, Lit = 2, Warm = 3, Hot = 4, LitNoGlow = 5, Dresses = 6, Classes = 9;
        // ── groups (map G): 1..11 the name, 12 the coupe, 13..32 the genre line ──────────────────────────────
        private const int Groups = 33, Coupe = 12, FirstGenre = 13;

        // the four glow alphas (the store art's own), inner to outer
        private const byte A4 = 150, A3 = 78, A2 = 34, A1 = 12;

        // ── the timing, in frames of 1/30 s since the sign was lit ─────────────────────────────────────────
        public const int Fps = 30;
        private static readonly int[] Strike =
            { 0, 9, 11, 13, 15, 17, 19, 24, 26, 28, 30, 32, 36,
              44, 44, 44, 44, 44, 44, 44, 44,  47, 47, 47,  50, 50, 50, 50, 50, 50, 50, 50, 50 };
        private static readonly int[] SingleStrike = { Half, 2, Dark, 1 };                    // (dress, frames)...
        private static readonly int[] DoubleStrike = { Half, 2, Dark, 1, Half, 1, Dark, 3 };  // M and C catch twice
        private const int GlowLag = 2, LiqF0 = 40, LiqFrames = 11, Idle = 66;

        /// <summary>Each group's tube length in canvas units (measured on the x1 set, sets.json): the name, the coupe
        /// (no bead runs through it), the genre line's twenty letters.</summary>
        private static readonly int[] TubeLen =
            { 0, 598, 65, 75, 55, 71, 124, 255, 76, 122, 72, 159, 102,
              69, 67, 69, 70, 52, 73, 43, 55, 52, 73, 54, 69, 44, 91, 101, 54, 74, 50, 67, 55 };

        // THE BEADS: two runs of light, each through its line in reading order at HlSpeed units a frame, every
        // HlPeriod frames - the magenta name from HlOffset into the idle, the cyan genre line half a period later.
        // Within a letter, texels whose place on the tube is within Hot of the bead are hot, within Warm warm; the
        // bead enters a letter shoulder first, leaves it shoulder last, and crosses HlGap units of dark before the
        // next. The genre line's letters are a fifth the name's size, so its shoulder is half as long.
        private const int HlPeriod = 300, HlOffset = 18, HlSpeed = 12, HlGap = 20, Beads = 2;
        private static readonly int[] BeadFrom = { 1, FirstGenre }, BeadTo = { 11, 32 };
        private static readonly int[] BeadLag = { 0, HlPeriod / 2 };
        private static readonly int[] BeadHot = { 16, 16 }, BeadWarm = { 40, 20 };

        private const int StGroup = 10, StStep = 3, StOffset = 93;
        private const string StTable =
            "LLLLLLLLLLLLLLLLLLLLLLLL" + "HDHL" + "LLLLL" + "H" + "LLLLLLLLLLLLLLLLLL" + "HDDH" + "LLLLLLLLLLLLLLLLLL";
        private const int ShPeriod = 72, ShRun = 27, ShHot = 14, ShWarm = 34, NoGlint = int.MinValue;
        private const int BrPeriod = 192, BrLen = 5, BrOffset = 120;

        // ── the dresses, as (ramp, step, alpha) per class 1..7 ─────────────────────────────────────────────
        private struct Ink { public Ramp R; public byte Step, A; public Ink(Ramp r, int s, byte a) { R = r; Step = (byte)s; A = a; } }
        private static Ink M(int s, byte a) => new Ink(Ramp.Magenta, s, a);
        private static Ink C(int s, byte a) => new Ink(Ramp.Cyan, s, a);
        private static Ink W(byte a = 255) => new Ink(Ramp.Cream, 4, a);
        private static readonly Ink None = new Ink(Ramp.Cream, 0, 0);

        // A HOT texel is the bead itself: the whole glass Cream[4], and its two inner bands a step brighter - a
        // local flare, the light pooling round the bright stretch (bands 1 and 2 are never dressed hot).
        private static Ink[][] MagentaTable() => new[]
        {
            /* Dark      */ new[] { None, None, None, None, M(0, 255), M(0, 255), M(1, 255) },
            /* Half      */ new[] { None, None, None, M(2, A4), M(2, 255), M(3, 255), M(4, 255) },
            /* Lit       */ new[] { M(3, A1), M(3, A2), M(3, A3), M(3, A4), M(3, 255), M(4, 255), W() },
            /* Warm      */ new[] { M(3, A1), M(3, A2), M(3, A3), M(3, A4), M(4, 255), M(4, 255), W() },
            /* Hot       */ new[] { M(3, A1), M(3, A2), M(4, A3), M(4, A4), W(), W(), W() },
            /* LitNoGlow */ new[] { None, None, None, None, M(3, 255), M(4, 255), W() },
        };

        // Cyan is already at the head of its ramp when lit, so its flare is band 3 raised to band 4's light.
        private static Ink[][] CyanTable() => new[]
        {
            /* Dark      */ new[] { None, None, None, None, C(0, 255), C(0, 255), C(1, 255) },
            /* Half      */ new[] { None, None, None, C(2, A4), C(2, 255), C(2, 255), C(4, 255) },
            /* Lit       */ new[] { C(4, A1), C(4, A2), C(4, A3), C(4, A4), C(4, 255), C(4, 255), W() },
            /* Warm      */ new[] { C(4, A1), C(4, A2), C(4, A3), C(4, A4), C(4, 255), C(4, 255), W() },
            /* Hot       */ new[] { C(4, A1), C(4, A2), C(4, A4), C(4, A4), W(), W(), W() },
            /* LitNoGlow */ new[] { None, None, None, None, C(4, 255), C(4, 255), W() },
        };

        // ── the drawing ────────────────────────────────────────────────────────────────────────────────────
        public readonly int Width, Height;
        /// <summary>RGBA32 texels (R | G&lt;&lt;8 | B&lt;&lt;16 | A&lt;&lt;24), in the map's own row order; hand to SetPixelData.</summary>
        public readonly uint[] Pixels;
        private readonly int[] _at;                  // drawn texel -> index into Pixels; sorted by (group, phase)
        private readonly byte[] _cls, _grp, _ph;     // per drawn texel
        private readonly int[] _from = new int[Groups + 1];
        private readonly int[] _outer;               // drawn-texel indices of the two outer glow bands (the breath)
        private readonly int[] _liq;                 // drawn-texel indices of the liquid
        private readonly int[] _liqRow;              // each one's row counted from the bowl's bottom (0 = bottom)
        private readonly int _liqRows;
        private readonly uint[] _lutM = new uint[Dresses * Classes], _lutC = new uint[Dresses * Classes];
        private readonly uint _liqOff, _liqBase, _liqGlint, _liqHot;

        // ── what was painted last ──────────────────────────────────────────────────────────────────────────
        private readonly int[] _dress = new int[Groups], _next = new int[Groups];
        private readonly bool[] _whole = new bool[Groups];
        private bool _painted, _breath;
        private readonly int[] _hlG = { -1, -1 }, _hlP = new int[Beads];
        // scratch for Step, kept so a frame allocates nothing
        private readonly int[] _stepG = new int[Beads], _stepP = new int[Beads], _oldG = new int[Beads], _oldP = new int[Beads];
        private int _filled = -1, _glint = NoGlint;

        /// <param name="cls">map R per texel</param><param name="grp">map G</param><param name="ph">map B</param>
        /// <param name="bottomUp">true when row 0 of the arrays is the picture's BOTTOM row (Unity's GetPixels32)</param>
        /// <param name="token">(ramp, step) -> 0x00BBGGRR, the UITheme colour</param>
        public TitleSignLight(int width, int height, byte[] cls, byte[] grp, byte[] ph, bool bottomUp,
                              Func<Ramp, int, uint> token)
        {
            Width = width; Height = height;
            Pixels = new uint[width * height];
            int n = 0;
            for (int i = 0; i < cls.Length; i++) if (cls[i] != Empty) n++;
            var at = new int[n];
            var key = new int[n];
            n = 0;
            for (int i = 0; i < cls.Length; i++)
                if (cls[i] != Empty) { at[n] = i; key[n] = grp[i] * 256 + ph[i]; n++; }
            Array.Sort(key, at);
            _at = at;
            _cls = new byte[n]; _grp = new byte[n]; _ph = new byte[n];
            for (int k = 0; k < n; k++) { _cls[k] = cls[at[k]]; _grp[k] = grp[at[k]]; _ph[k] = ph[at[k]]; }
            for (int g = 0, k = 0; g <= Groups; g++)
            {
                while (k < n && _grp[k] < g) k++;
                _from[g] = k;
            }

            int outerN = 0;
            for (int k = 0; k < n; k++) if (_cls[k] == B1 || _cls[k] == B2) outerN++;
            _outer = new int[outerN];
            for (int k = 0, j = 0; k < n; k++) if (_cls[k] == B1 || _cls[k] == B2) _outer[j++] = k;

            // the liquid, and the bowl's rows counted from its bottom
            int liqN = 0, yTop = int.MaxValue, yBot = int.MinValue;
            for (int k = 0; k < n; k++)
                if (_cls[k] == Liquid)
                {
                    liqN++;
                    int y = TopDownRow(_at[k], width, height, bottomUp);
                    yTop = Math.Min(yTop, y); yBot = Math.Max(yBot, y);
                }
            _liq = new int[liqN]; _liqRow = new int[liqN];
            for (int k = 0, j = 0; k < n; k++)
                if (_cls[k] == Liquid) { _liq[j] = k; _liqRow[j] = yBot - TopDownRow(_at[k], width, height, bottomUp); j++; }
            _liqRows = liqN > 0 ? yBot - yTop + 1 : 0;

            Fill(_lutM, MagentaTable(), token);
            Fill(_lutC, CyanTable(), token);
            _liqOff = Pack(token(Ramp.Cyan, 0), 150);
            _liqBase = Pack(token(Ramp.Cyan, 2), 190);
            _liqGlint = Pack(token(Ramp.Cyan, 3), 190);
            _liqHot = Pack(token(Ramp.Cyan, 4), 190);
        }

        private static int TopDownRow(int index, int w, int h, bool bottomUp)
        {
            int row = index / w;
            return bottomUp ? h - 1 - row : row;
        }

        private static uint Pack(uint rgb, byte a) => (rgb & 0x00FFFFFFu) | ((uint)a << 24);

        private static void Fill(uint[] lut, Ink[][] table, Func<Ramp, int, uint> token)
        {
            for (int d = 0; d < Dresses; d++)
                for (int c = 1; c <= 7; c++)
                {
                    var ink = table[d][c - 1];
                    lut[d * Classes + c] = ink.A == 0 ? 0u : Pack(token(ink.R, ink.Step), ink.A);
                }
        }

        /// <summary>Start over from the dark sign (the menu was shown again).</summary>
        public void Restart() { _painted = false; }

        /// <summary>Bring the sign to frame n. True when Pixels changed (upload them).</summary>
        public bool Step(int n, bool reduced, bool noFlashes)
        {
            noFlashes |= reduced;
            // the dresses
            for (int g = 1; g < Groups; g++) _next[g] = reduced ? Lit : StrikeDress(g, n, noFlashes);
            int ni = n - Idle - StOffset;
            if (!noFlashes && ni >= 0)
            {
                char c = StTable[(ni / StStep) % StTable.Length];
                if (c != 'L' && _next[StGroup] == Lit) _next[StGroup] = c == 'H' ? Half : Dark;
            }
            // the beads, the breath, the liquid - all of it idle light, and all of it held still with FLASHES off
            int[] hlG = _stepG, hlP = _stepP;
            for (int t = 0; t < Beads; t++)
            {
                hlG[t] = -1;
                hlP[t] = 0;
                int nh = n - Idle - HlOffset - BeadLag[t];
                if (noFlashes || nh < 0) continue;
                int w = BeadWarm[t];
                int p = (nh % HlPeriod) * HlSpeed - w;             // the shoulder reaches the first letter's start
                for (int g = BeadFrom[t]; g <= BeadTo[t]; g++)
                {
                    if (p < -w) break;                              // in the dark between two letters
                    if (p <= TubeLen[g] + w) { hlG[t] = g; hlP[t] = p; break; }
                    p -= TubeLen[g] + 2 * w + HlGap;
                }
            }
            int nb = n - Idle - BrOffset;
            bool breath = !noFlashes && nb >= 0 && nb % BrPeriod < BrLen;
            int filled = reduced ? _liqRows : n < LiqF0 ? 0 : Math.Min(_liqRows, (n - LiqF0) * _liqRows / LiqFrames + 1);
            int glint = NoGlint;
            int ns = n - Idle;
            if (!noFlashes && ns >= 0 && ns % ShPeriod < ShRun) glint = -ShWarm + (255 + 2 * ShWarm) * (ns % ShPeriod) / ShRun;

            bool any = false;
            bool breathFlip = _painted && breath != _breath;
            int[] oldG = _oldG, oldP = _oldP;
            for (int t = 0; t < Beads; t++)
            {
                oldG[t] = _hlG[t]; oldP[t] = _hlP[t];
                _hlG[t] = hlG[t]; _hlP[t] = hlP[t];
            }
            _breath = breath;
            for (int g = 1; g < Groups; g++)
            {
                bool need = !_painted || _next[g] != _dress[g];
                _whole[g] = need;
                _dress[g] = _next[g];
            }
            bool liquidMoved = filled != _filled || glint != _glint;
            _filled = filled; _glint = glint;
            for (int g = 1; g < Groups; g++)
                if (_whole[g]) { Paint(_from[g], _from[g + 1]); any = true; }
            // the breath touches only the two outer bands of the lit groups
            if (breathFlip)
                for (int j = 0; j < _outer.Length; j++)
                {
                    int k = _outer[j];
                    if (!_whole[_grp[k]] && _dress[_grp[k]] == Lit) { Pixels[_at[k]] = Ink_(k); any = true; }
                }
            // each bead's old stretch and new stretch, unless their group was painted whole already
            if (_painted)
                for (int t = 0; t < Beads; t++)
                {
                    if (oldG[t] == hlG[t] && oldP[t] == hlP[t]) continue;
                    if (oldG[t] > 0 && !_whole[oldG[t]]) { PaintStretch(t, oldG[t], oldP[t]); any = true; }
                    if (hlG[t] > 0 && !_whole[hlG[t]]) { PaintStretch(t, hlG[t], hlP[t]); any = true; }
                }
            if (_painted && liquidMoved && !_whole[Coupe])
            {
                for (int j = 0; j < _liq.Length; j++) Pixels[_at[_liq[j]]] = LiquidInk(j);
                any = true;
            }
            _painted = true;
            return any;
        }

        private static int StrikeDress(int g, int n, bool noFlashes)
        {
            int f0 = Strike[g];
            if (n < f0) return Dark;
            int dn = n - f0;
            if (!noFlashes)
            {
                var steps = g == 1 || g == 7 ? DoubleStrike : SingleStrike;
                for (int i = 0; i < steps.Length; i += 2)
                {
                    if (dn < steps[i + 1]) return steps[i];
                    dn -= steps[i + 1];
                }
            }
            return dn < GlowLag ? LitNoGlow : Lit;
        }

        private void Paint(int from, int to)
        {
            for (int k = from; k < to; k++) Pixels[_at[k]] = Ink_(k);
        }

        /// <summary>Repaint the texels of group g whose place on the tube is within bead t's shoulder of p.</summary>
        private void PaintStretch(int t, int g, int p)
        {
            int L = TubeLen[g], w = BeadWarm[t];
            int lo = Lower(g, p - w, L), hi = Lower(g, p + w + 1, L);
            Paint(lo, hi);
        }

        /// <summary>First texel of group g (sorted by phase) whose tube position ph*L/255 is at least `pos`.</summary>
        private int Lower(int g, int pos, int L)
        {
            int a = _from[g], b = _from[g + 1];
            while (a < b)
            {
                int m = (a + b) >> 1;
                if (_ph[m] * L / 255 < pos) a = m + 1; else b = m;
            }
            return a;
        }

        private uint Ink_(int k)
        {
            byte c = _cls[k];
            int g = _grp[k];
            if (c == Liquid)
            {
                int j = Array.BinarySearch(_liq, k);
                return LiquidInk(j);
            }
            int d = _dress[g];
            int t = g >= FirstGenre ? 1 : 0;
            if (d == Lit && g == _hlG[t] && c >= B3 && c <= Core)
            {
                int diff = Math.Abs(_ph[k] * TubeLen[g] / 255 - _hlP[t]);
                if (diff <= BeadHot[t]) d = Hot;
                else if (diff <= BeadWarm[t] && c >= Body) d = Warm;
            }
            uint col = (g >= Coupe ? _lutC : _lutM)[d * Classes + c];
            if (_breath && d == Lit)
            {
                if (c == B2) col = (col & 0x00FFFFFFu) | ((uint)A1 << 24);
                else if (c == B1) col = 0u;
            }
            return col;
        }

        private uint LiquidInk(int j)
        {
            if (_liqRow[j] >= _filled) return _liqOff;
            if (_glint == NoGlint) return _liqBase;
            int dd = Math.Abs(_ph[_liq[j]] - _glint);
            return dd <= ShHot ? _liqHot : dd <= ShWarm ? _liqGlint : _liqBase;
        }
    }
}
