using System.Collections.Generic;
using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// THE VIEW OUT OF THE WINDOW, DRAWN BY THE HOUR (2026-09-17).
    ///
    /// The author: "güneş batmıyor yok oluyor, hava renk değişimi daha smooth olmalı, güneş
    /// aşağı doğru batmalı, şehir ışıkları ona göre yanmalı. arada gökyüzünde M şeklinde uçan
    /// kuşlar yapılabilir aynı renk paletiyle."
    ///
    /// The view used to be thirty-one whole pictures. It is COMPOSED now, at the source's own
    /// 71×137 and doubled to the glass exactly as the sheet was:
    ///
    ///   THE SKY is bands of the palette, top to horizon, read off <see cref="SkyClock"/> for
    ///   the hour and quantised with a 4×4 ordered dither — so it is pixel art at every
    ///   instant and still moves continuously, because the thing that moves is the colour
    ///   underneath and the pattern only ever answers it.
    ///   THE SUN is a disc with a rim and a halo, and it goes DOWN: a straight line from
    ///   well above the towers at 18:00 to behind them by a quarter past the shift, drawn
    ///   under the city so the skyline takes it the way a skyline does.
    ///   THE CITY is the author's own: every frame's city pixels, cut from the sheet by
    ///   Tools/window_sky.py, and the hour shows the frame it is nearest to — the towers
    ///   walking from purple to navy and the windows coming on the way PixelLab drew them
    ///   (a first cut re-scheduled them pixel by pixel; the author preferred the frames).
    ///   STARS come out where the sky has gone dark enough; CLOUDS are the sheet's own,
    ///   a step darker than the band they stand in, drifting.
    ///   BIRDS are sprites over the plate (<see cref="Flock"/>), an M of four rows in the
    ///   sky's own ink, crossing now and then while there is light to see them by.
    ///
    /// Tools/window_sky.py renders the same model for a contact sheet; the two are kept
    /// in step by sharing sky_cycle.json and the hash below.
    /// </summary>
    public sealed class WindowSky
    {
        public const int SrcW = 71, SrcH = 137, Zoom = 2;
        public const int CellW = 141, CellH = 274;

        /// <summary>The plate the window shows. Its texture is redrawn in place.</summary>
        public Sprite Sprite { get; private set; }
        public readonly SkyClock Clock;

        private readonly Texture2D _tex;
        private readonly Color32[] _cell = new Color32[CellW * CellH];
        private readonly Color32[] _src = new Color32[SrcW * SrcH];
        // THE CITY IS THE FRAMES' OWN (2026-09-17, the author: "şehirdeki değişimi sevmedim,
        // onu önceki gibi kullanabilir miyiz?"): every frame's city pixels, in mask order, and
        // the hour picks the frame. The first cut flipped pixels between three plates on a
        // schedule of its own; the author preferred the thirty-one frames' progression.
        private readonly Color32[][] _cityFrames;
        private readonly int[] _cityAt;       // pixel -> index into a frame's city list, or -1
        private readonly byte[] _mask;        // 0 sky · 1 city
        private readonly bool[] _cloud;
        private readonly int[] _skyline;      // first city row per column
        private readonly Color[] _palette;
        private readonly int[] _starAt;       // star index per pixel, or -1
        private readonly float[] _starBirth, _starPhase, _starPeriod;
        private readonly bool[] _starBright;
        private readonly Color[] _stops;
        private readonly float[] _rowR, _rowG, _rowB;
        private float _lastTau = -1f, _lastClock = -1e9f;
        private SpriteRenderer _moon;

        private static readonly float[] Bayer =
        {
            0f/16, 8f/16, 2f/16, 10f/16,
            12f/16, 4f/16, 14f/16, 6f/16,
            3f/16, 11f/16, 1f/16, 9f/16,
            15f/16, 7f/16, 13f/16, 5f/16,
        };

        /// <summary>Loads Resources/Scene/window_city.bytes. Null when the file is missing or
        /// not the shape this reader expects, so the stage can keep its still plate.</summary>
        public static WindowSky Load(SkyClock clock)
        {
            if (clock == null) return null;
            var asset = Resources.Load<TextAsset>("Scene/window_city");
            if (asset == null) return null;
            var b = asset.bytes;
            if (b.Length < 10 || b[0] != (byte)'L' || b[1] != (byte)'C' || b[2] != (byte)'S' || b[3] != (byte)'2')
            {
                Debug.LogWarning("WindowSky: window_city.bytes is not an LCS2 file.");
                return null;
            }
            int w = b[4] | (b[5] << 8), h = b[6] | (b[7] << 8), count = b[8] | (b[9] << 8);
            int n = w * h;
            if (w != SrcW || h != SrcH || count < 1 || b.Length < 10 + n * 2 + w)
            {
                Debug.LogWarning($"WindowSky: window_city.bytes is {w}×{h}×{count}, expected {SrcW}×{SrcH}.");
                return null;
            }
            return new WindowSky(clock, b, count);
        }

        private WindowSky(SkyClock clock, byte[] b, int count)
        {
            Clock = clock;
            int n = SrcW * SrcH;
            int at = 10;
            _mask = new byte[n];
            System.Array.Copy(b, at, _mask, 0, n); at += n;
            _cloud = new bool[n];
            for (int i = 0; i < n; i++) _cloud[i] = b[at + i] != 0;
            at += n;
            _skyline = new int[SrcW];
            for (int x = 0; x < SrcW; x++) _skyline[x] = b[at + x];
            at += SrcW;
            _cityAt = new int[n];
            int cityCount = 0;
            for (int i = 0; i < n; i++) _cityAt[i] = _mask[i] != 0 ? cityCount++ : -1;
            if (b.Length < at + count * cityCount * 3)
            {
                Debug.LogWarning("WindowSky: window_city.bytes is short of its frames; the city will be its first.");
                count = Mathf.Max(1, (b.Length - at) / Mathf.Max(1, cityCount * 3));
            }
            _cityFrames = new Color32[count][];
            for (int f = 0; f < count; f++) _cityFrames[f] = ReadPlate(b, ref at, cityCount);

            var pal = clock.Spec.skyPalette;
            _palette = new Color[pal.Length];
            for (int i = 0; i < pal.Length; i++) _palette[i] = SkyClock.Token(pal[i]);
            _stops = new Color[clock.Spec.stopsV.Length];
            _rowR = new float[SrcH]; _rowG = new float[SrcH]; _rowB = new float[SrcH];

            // The stars, once: where they are, when they are born, how they twinkle. The
            // same hash the tool uses, so the preview's sky is the game's.
            var st = clock.Spec.stars;
            _starAt = new int[n];
            for (int i = 0; i < n; i++) _starAt[i] = -1;
            _starBirth = new float[st.count]; _starPhase = new float[st.count];
            _starPeriod = new float[st.count]; _starBright = new bool[st.count];
            for (int i = 0; i < st.count; i++)
            {
                int sx = (int)(Hash01(i, 1, 11) * SrcW);
                int sy = (int)(Hash01(i, 2, 11) * st.rowMax);
                _starBirth[i] = st.from + Hash01(i, 3, 11) * (st.to - st.from);
                _starPhase[i] = Hash01(i, 4, 11) * 6.2832f;
                _starPeriod[i] = 2.2f + Hash01(i, 5, 11) * 2.0f;
                _starBright[i] = Hash01(i, 6, 11) > 0.6f;
                if (sx >= 0 && sx < SrcW && sy >= 0 && sy < SrcH) _starAt[sy * SrcW + sx] = i;
            }

            _tex = new Texture2D(CellW, CellH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "WindowSky",
            };
            // PPU 1 and a centred pivot, exactly as the sheet's cells were cut, so the plate
            // hangs where the frames hung and the palms over it need no new numbers.
            Sprite = Sprite.Create(_tex, new Rect(0, 0, CellW, CellH), new Vector2(0.5f, 0.5f), 1f);
            Sprite.name = "WindowSkyPlate";
        }

        private static Color32[] ReadPlate(byte[] b, ref int at, int n)
        {
            var plate = new Color32[n];
            for (int i = 0; i < n; i++)
                plate[i] = new Color32(b[at + i * 3], b[at + i * 3 + 1], b[at + i * 3 + 2], 255);
            at += n * 3;
            return plate;
        }

        /// <summary>A stable number in [0,1) for a pixel and a salt — Tools/window_sky.py's hash01.</summary>
        public static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)salt * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65536f;
            }
        }

        /// <summary>
        /// THE MOON (2026-09-17): the day-pass curtain's own crescent (Scene/curtain_moon,
        /// generated and quantised for the sky over the city on 2026-08-25), hung over the
        /// plate as a sprite the way the birds are, at the glass's 2× so its 24 px read as
        /// twelve of the view's. It comes up in the blue hour and crosses the upper sky by
        /// closing — the one thing that says the night is MOVING once the sun is gone.
        /// </summary>
        public void HangMoon(Transform parent, Material material, string sortingLayer, int order)
        {
            var art = Resources.Load<Sprite>("Scene/curtain_moon");
            if (art == null || Clock.Spec.moon == null) return;
            var go = new GameObject("Moon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.005f);   // over the plate, under the birds
            _moon = go.AddComponent<SpriteRenderer>();
            _moon.sprite = art;
            _moon.sharedMaterial = material;
            _moon.sortingLayerName = sortingLayer;
            _moon.sortingOrder = order;
            _moon.enabled = false;
        }

        private void PlaceMoon(float tau)
        {
            if (_moon == null) return;
            var m = Clock.Spec.moon;
            bool up = tau >= m.from;
            if (_moon.enabled != up) _moon.enabled = up;
            if (!up) return;
            float p = Mathf.Clamp01((tau - m.from) / Mathf.Max(0.0001f, 1f - m.from));
            float x = Mathf.Lerp(m.xStart, m.xEnd, p);
            float y = Mathf.Lerp(m.yStart, m.yEnd, p) - m.arc * Mathf.Sin(p * Mathf.PI);
            _moon.transform.localPosition = new Vector3((x - SrcW * 0.5f) * Zoom, (SrcH * 0.5f - y) * Zoom, -0.005f);
            // In, not on: a moon that pops is a bug in the sky.
            float a = Mathf.Clamp01((tau - m.from) / Mathf.Max(0.0001f, m.fade));
            _moon.color = new Color(1f, 1f, 1f, a);
        }

        /// <summary>How much of the sun's disc the skyline has not yet taken, 0..1 — measured
        /// on the drawing, because only the drawing knows where the towers are.</summary>
        public float SunVisible(float tau)
        {
            var s = Clock.Spec.sun;
            float row = Clock.SunRow(tau), col = s.col, rad = s.radius;
            int total = 0, seen = 0;
            int x0 = Mathf.FloorToInt(col - rad), x1 = Mathf.CeilToInt(col + rad);
            int y0 = Mathf.FloorToInt(row - rad), y1 = Mathf.CeilToInt(row + rad);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - col, dy = y - row;
                    if (dx * dx + dy * dy > rad * rad) continue;
                    total++;
                    int cx = Mathf.Clamp(x, 0, SrcW - 1);
                    if (y < 0 || y < _skyline[cx]) seen++;
                }
            return total == 0 ? 0f : seen / (float)total;
        }

        /// <summary>
        /// Redraws when the hour or the ambient clock has moved enough to show: the sky's
        /// bands crawl a row every second or two, the stars blink at a few hertz. Ten
        /// thousand pixels, eight times a second at most — nothing a weak machine notices.
        /// Returns whether it drew.
        /// </summary>
        public bool Step(float tau, float clock, bool motion)
        {
            bool due = _lastTau < 0f
                       || Mathf.Abs(tau - _lastTau) >= 1f / 1600f
                       || (motion && clock - _lastClock >= 0.11f);
            if (!due) return false;
            _lastTau = tau; _lastClock = clock;
            Render(tau, clock, motion);
            PlaceMoon(tau);
            return true;
        }

        private void Render(float tau, float clock, bool motion)
        {
            var spec = Clock.Spec;
            var sun = spec.sun;
            var city = spec.city;
            var st = spec.stars;
            Clock.Bands(tau, _stops);
            var stopsV = spec.stopsV;

            // The continuous ramp, once per row: v is 0 at the top and 1 at the skyline's base.
            for (int y = 0; y < SrcH; y++)
            {
                float v = Mathf.Clamp01(y / sun.horizonRow);
                int k = 0;
                while (k < stopsV.Length - 2 && v > stopsV[k + 1]) k++;
                float f = Mathf.InverseLerp(stopsV[k], stopsV[k + 1], v);
                var c = Color.Lerp(_stops[k], _stops[k + 1], f);
                _rowR[y] = c.r * 255f; _rowG[y] = c.g * 255f; _rowB[y] = c.b * 255f;
            }

            float sink = SkyClock.SmoothStep(0f, sun.setBy, tau);
            float srow = Clock.SunRow(tau), scol = sun.col, rad = sun.radius;
            Color coreC = Color.Lerp(SkyClock.Token(sun.coreHigh), SkyClock.Token(sun.coreLow), sink);
            Color rimC = Color.Lerp(SkyClock.Token(sun.rimHigh), SkyClock.Token(sun.rimLow), sink);
            Color haloC = string.IsNullOrEmpty(sun.halo) ? coreC : SkyClock.Token(sun.halo);
            float halo = Mathf.Lerp(sun.haloHigh, sun.haloLow, sink);
            float haloR = sun.haloRadius;
            float coreR = coreC.r * 255f, coreG = coreC.g * 255f, coreB = coreC.b * 255f;
            float haloRr = haloC.r * 255f, haloGg = haloC.g * 255f, haloBb = haloC.b * 255f;
            float spread = city.ditherSpread;
            int drift = motion ? (int)((clock * city.cloudDrift) % (2 * SrcW)) : 0;
            // The city's frame: the hour, a little behind the sun, over the frames there are.
            float lag = Mathf.Clamp(city.frameLag, 0f, 0.5f);
            int frame = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0f, tau - lag) / (1f - lag) * (_cityFrames.Length - 1)),
                                    0, _cityFrames.Length - 1);
            var cityNow = _cityFrames[frame];

            for (int y = 0; y < SrcH; y++)
            {
                for (int x = 0; x < SrcW; x++)
                {
                    int i = y * SrcW + x;
                    float thr = Bayer[(y & 3) * 4 + (x & 3)];
                    if (_mask[i] != 0) { _src[i] = cityNow[_cityAt[i]]; continue; }

                    float r = _rowR[y], g = _rowG[y], b = _rowB[y];
                    // The sun's halo warms the sky before the quantiser sees it: that is what
                    // makes it dithered rings rather than a painted glow.
                    float dx = x - scol, dy = y - srow;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float fall = Mathf.Clamp01(1f - d / haloR);
                    fall = fall * fall * halo;
                    if (fall > 0f)
                    {
                        r += (haloRr - r) * fall; g += (haloGg - g) * fall; b += (haloBb - b) * fall;
                    }
                    if (d <= rad)
                    {
                        var sc = d > rad - 1.6f ? rimC : coreC;
                        r = sc.r * 255f; g = sc.g * 255f; b = sc.b * 255f;
                    }
                    // Clouds, a step darker than the band, drifting across a mirrored copy of
                    // themselves so the seam never shows.
                    int cx = (x - drift) % (2 * SrcW);
                    if (cx < 0) cx += 2 * SrcW;
                    if (cx >= SrcW) cx = 2 * SrcW - 1 - cx;
                    bool cloud = _cloud[y * SrcW + cx];
                    if (cloud) { r *= 0.80f; g *= 0.80f; b *= 0.80f; }

                    float luma = (r * 0.299f + g * 0.587f + b * 0.114f) / 255f;
                    float nudge = (thr - 0.5f) * spread;
                    int best = Nearest(r + nudge, g + nudge, b + nudge);
                    Color32 outc = _palette[best];

                    int si = _starAt[i];
                    if (si >= 0 && tau >= _starBirth[si] && luma <= st.lumaMax && !cloud)
                    {
                        float tw = motion ? 0.5f + 0.5f * Mathf.Sin(clock * 6.2832f / _starPeriod[si] + _starPhase[si]) : 1f;
                        bool brightStar = _starBright[si];
                        if (tw > 0.55f) outc = brightStar ? UITheme.Cream[4] : UITheme.Cream[3];
                        else if (tw > 0.22f) outc = brightStar ? UITheme.Cream[3] : UITheme.Cream[2];
                    }
                    _src[i] = outc;
                }
            }

            // Doubled to the glass, bottom-up as Unity keeps its rows.
            for (int y = 0; y < CellH; y++)
            {
                int sy = y / Zoom;
                int rowOut = (CellH - 1 - y) * CellW;
                int rowIn = sy * SrcW;
                for (int x = 0; x < CellW; x++)
                {
                    int sx = x / Zoom; if (sx >= SrcW) sx = SrcW - 1;
                    _cell[rowOut + x] = _src[rowIn + sx];
                }
            }
            _tex.SetPixels32(_cell);
            _tex.Apply(false, false);
        }

        /// <summary>Nearest palette entry to a nudged colour, weighted a little toward
        /// green the way the eye is.</summary>
        private int Nearest(float r, float g, float b)
        {
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < _palette.Length; i++)
            {
                var p = _palette[i];
                float dr = r - p.r * 255f, dg = g - p.g * 255f, db = b - p.b * 255f;
                float d = dr * dr * 0.9f + dg * dg * 1.2f + db * db * 0.8f;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // ── the birds ────────────────────────────────────────────────────────

        /// <summary>
        /// A flock now and then, while there is sky to see it against. Sprites, not pixels
        /// of the plate: they move every frame and the plate does not.
        /// </summary>
        public sealed class Flock
        {
            private readonly SkyClock.BirdSpec _spec;
            private readonly Sprite[] _frames;
            private readonly List<SpriteRenderer> _birds = new List<SpriteRenderer>();
            private readonly List<float> _x = new List<float>(), _y = new List<float>();
            private readonly Transform _root;
            private bool _flying;
            private float _until;          // seconds of ambient clock to the next flock
            private uint _rng = 2463534242u;

            public Flock(SkyClock.BirdSpec spec, Transform parent, Material material,
                         string sortingLayer, int order)
            {
                _spec = spec;
                _root = new GameObject("Birds").transform;
                _root.SetParent(parent, false);
                _root.localPosition = new Vector3(0f, 0f, -0.01f);   // over the plate, under the palms
                _frames = new[] { Frame(FrameUp), Frame(FrameMid), Frame(FrameDown) };
                for (int i = 0; i < spec.flockMax; i++)
                {
                    var go = new GameObject("Bird" + i);
                    go.transform.SetParent(_root, false);
                    go.transform.localScale = new Vector3(Zoom, Zoom, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = _frames[0];
                    sr.sharedMaterial = material;
                    sr.sortingLayerName = sortingLayer;
                    sr.sortingOrder = order;
                    sr.enabled = false;
                    _birds.Add(sr); _x.Add(0f); _y.Add(0f);
                }
                _until = Next(spec.everyMin * 0.4f, spec.everyMin);   // the first flock comes early
            }

            // Four rows of an M, wings up / level / down — the sky's own ink (Night[1]),
            // seven source pixels wide, which is the size a gull is across a window.
            private static readonly string[] FrameUp = { "X.....X", ".X...X.", "..X.X..", "...X..." };
            private static readonly string[] FrameMid = { ".......", ".X...X.", "X.X.X.X", "...X..." };
            private static readonly string[] FrameDown = { ".......", "..X.X..", ".X.X.X.", "X..X..X" };

            private static Sprite Frame(string[] rows)
            {
                int w = rows[0].Length, h = rows.Length;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[w * h];
                Color32 ink = UITheme.Night[1];
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[(h - 1 - y) * w + x] = rows[y][x] == 'X' ? ink : new Color32(0, 0, 0, 0);
                tex.SetPixels32(px); tex.Apply(false, false);
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            }

            private float Next(float lo, float hi)
            {
                _rng ^= _rng << 13; _rng ^= _rng >> 17; _rng ^= _rng << 5;
                return lo + (_rng & 0xFFFF) / 65536f * (hi - lo);
            }

            /// <summary>One frame of the flock. <paramref name="dt"/> is ambient time — it stops
            /// when the room does.</summary>
            public void Step(float tau, float clock, float dt, bool motion)
            {
                if (!motion || tau > _spec.until)
                {
                    if (_flying) Land();
                    return;
                }
                if (!_flying)
                {
                    _until -= dt;
                    if (_until > 0f) return;
                    Launch();
                }
                bool anyLeft = false;
                for (int i = 0; i < _birds.Count; i++)
                {
                    if (!_birds[i].enabled) continue;
                    _x[i] -= _spec.speed * dt;
                    float y = _y[i] + 1.5f * Mathf.Sin(clock * 2.4f + i);
                    var sr = _birds[i];
                    sr.sprite = _frames[((int)(clock * 8f + i)) % 3];
                    sr.transform.localPosition = new Vector3(
                        (_x[i] - SrcW * 0.5f) * Zoom, (SrcH * 0.5f - y) * Zoom, 0f);
                    if (_x[i] > -8f) anyLeft = true;
                }
                if (!anyLeft) Land();
            }

            private void Launch()
            {
                _flying = true;
                int count = Mathf.Clamp((int)Next(_spec.flockMin, _spec.flockMax + 0.999f), 1, _birds.Count);
                float baseY = Next(8f, 30f);
                for (int i = 0; i < _birds.Count; i++)
                {
                    bool on = i < count;
                    _birds[i].enabled = on;
                    if (!on) continue;
                    // A loose echelon: each bird a few pixels behind and a row below the
                    // last, jittered, so the flock is a shape and not a row of stamps.
                    _x[i] = SrcW + 8f + i * 5f + Next(-2f, 2f);
                    _y[i] = baseY + i * 1.6f + Next(-1.5f, 1.5f);
                }
            }

            private void Land()
            {
                _flying = false;
                for (int i = 0; i < _birds.Count; i++) _birds[i].enabled = false;
                _until = Next(_spec.everyMin, _spec.everyMax);
            }
        }
    }
}
