using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A REAL seven-segment readout (2026-08-14, the author: "dijital saat olmamış hiç dijital
    /// saate benzememiş").
    ///
    /// The first attempt drew the time in the UI's pixel typeface and put a dim copy behind
    /// it — which is a font trick, not a clock. What makes a display read as a machine is its
    /// GEOMETRY: seven bars per digit, with the unlit ones still faintly there, a gap where
    /// the bars meet, and a colon that keeps time. So this draws the bars.
    ///
    /// Every number here is in whole units and the bars are plain rectangles, because that is
    /// what the rest of this game is made of; a chamfered segment would be smoother than the
    /// art it sits next to. The digits carry a wide, low-alpha copy behind them, which is the
    /// cheapest honest glow: an LED bleeds into the glass in front of it.
    /// </summary>
    public sealed class SegmentClock
    {
        // Segment order: a top, b upper-right, c lower-right, d bottom, e lower-left,
        // f upper-left, g middle. The table is the shape of each numeral.
        private static readonly bool[][] Digits =
        {
            new[] { true,  true,  true,  true,  true,  true,  false }, // 0
            new[] { false, true,  true,  false, false, false, false }, // 1
            new[] { true,  true,  false, true,  true,  false, true  }, // 2
            new[] { true,  true,  true,  true,  false, false, true  }, // 3
            new[] { false, true,  true,  false, false, true,  true  }, // 4
            new[] { true,  false, true,  true,  false, true,  true  }, // 5
            new[] { true,  false, true,  true,  true,  true,  true  }, // 6
            new[] { true,  true,  true,  false, false, false, false }, // 7
            new[] { true,  true,  true,  true,  true,  true,  true  }, // 8
            new[] { true,  true,  true,  true,  false, true,  true  }, // 9
        };

        private readonly Image[][] _bars = new Image[4][];    // four digits, seven bars each
        private readonly Image[][] _glow = new Image[4][];
        private readonly Image[] _colon = new Image[2];
        private Color _lit, _unlit;   // not readonly: the readout re-hues at closing time
        private int _shownHH = -1, _shownMM = -1;
        private bool _colonShown = true;

        /// <summary>
        /// Builds the readout into <paramref name="host"/>, left-aligned inside it.
        /// </summary>
        public SegmentClock(RectTransform host, float digitW, float digitH, float bar, Color lit)
        {
            _lit = lit;
            // The unlit bars have to be READ as absent, and it took three tries. 0.14 made an
            // 8 and a 1 the same block of light. 0.085 still left the dark traces competing
            // with the time, and 0.045 still did (2026-08-14, the author: "saat okunaklı
            // değil, koyu mavi dijital izleri daha silik yap"). At 0.025 the ghost is what a
            // real display's unlit glass is: proof there is a machine behind it, and nothing
            // you have to read past to get the hour.
            _unlit = new Color(lit.r, lit.g, lit.b, 0.025f);

            float x = 0f;
            for (int d = 0; d < 4; d++)
            {
                _bars[d] = new Image[7];
                _glow[d] = new Image[7];
                var cell = NewRect(host, "D" + d, new Vector2(digitW, digitH), new Vector2(x, 0));
                for (int s = 0; s < 7; s++)
                {
                    var (size, pos) = SegmentRect(s, digitW, digitH, bar);
                    // The glow first, so the bar itself draws on top of its own bleed. One unit
                    // of bleed per side and no more: at three it closed the gaps between the
                    // segments and every digit turned back into a lit brick.
                    _glow[d][s] = NewBar(cell, "G" + s, size + new Vector2(2f, 2f), pos,
                        new Color(lit.r, lit.g, lit.b, 0.18f));
                    _bars[d][s] = NewBar(cell, "S" + s, size, pos, _unlit);
                }
                x += digitW + bar;
                if (d == 1)
                {
                    // The colon sits between the pairs, two square lamps on the same grid.
                    var colon = NewRect(host, "Colon", new Vector2(bar * 1.5f, digitH),
                        new Vector2(x, 0));
                    // Whole units. At 0.19 of the digit height the lamps landed on 5.32 and
                    // sat half a pixel off the grid the rest of the readout is drawn on.
                    float colonY = Mathf.Round(digitH * 0.19f);
                    for (int i = 0; i < 2; i++)
                        _colon[i] = NewBar(colon, "C" + i, new Vector2(bar, bar),
                            new Vector2(0, (i == 0 ? -1f : 1f) * colonY), lit);
                    x += bar * 1.5f + bar;
                }
            }
        }

        /// <summary>Lights the segments for a time. The colon blinks on its own second.</summary>
        public void Show(int hours, int minutes, bool colonOn)
        {
            hours = Mathf.Clamp(hours, 0, 99);
            minutes = Mathf.Clamp(minutes, 0, 99);
            if (hours != _shownHH || minutes != _shownMM)
            {
                _shownHH = hours; _shownMM = minutes;
                Paint(0, hours / 10);
                Paint(1, hours % 10);
                Paint(2, minutes / 10);
                Paint(3, minutes % 10);
            }
            if (colonOn == _colonShown) return;
            _colonShown = colonOn;
            foreach (var lamp in _colon)
                if (lamp != null) lamp.color = colonOn ? _lit : _unlit;
        }

        /// <summary>Tints the whole readout — the plaque turns magenta at closing time, and a
        /// clock that stayed cyan through it would be the one thing in the room not saying so.</summary>
        public void SetHue(Color lit)
        {
            // The two colours are the state, not a decoration on top of it: repainting the
            // bars without moving them would hold until the next minute ticked and `Paint`
            // put the old hue back — the clock would go magenta and then quietly go cyan
            // again while the room was still being called.
            _lit = lit;
            _unlit = new Color(lit.r, lit.g, lit.b, 0.025f);   // keep in step with the ctor
            for (int d = 0; d < 4; d++)
                for (int s = 0; s < 7; s++)
                {
                    bool on = _bars[d][s].color.a > 0.5f;
                    _bars[d][s].color = on ? _lit : _unlit;
                    _glow[d][s].color = new Color(lit.r, lit.g, lit.b, on ? 0.18f : 0f);
                }
            foreach (var lamp in _colon)
                if (lamp != null) lamp.color = _colonShown ? _lit : _unlit;
        }

        private void Paint(int digit, int value)
        {
            var on = Digits[Mathf.Clamp(value, 0, 9)];
            for (int s = 0; s < 7; s++)
            {
                _bars[digit][s].color = on[s] ? _lit : _unlit;
                var g = _glow[digit][s].color;
                _glow[digit][s].color = new Color(g.r, g.g, g.b, on[s] ? 0.22f : 0f);
            }
        }

        /// <summary>
        /// Where one segment sits inside a digit cell, and how big it is.
        ///
        /// THE GAP IS THE WHOLE TRICK. The first cut stacked bar/arm/bar/arm/bar to exactly
        /// the cell height, so the seven segments tiled it with no seam and an 8 came out a
        /// solid rectangle — the one numeral that has to look like every segment lit still
        /// has to look like SEGMENTS. A unit of dark between the rows is what a real display
        /// has and what makes the shape legible.
        /// </summary>
        private static (Vector2 size, Vector2 pos) SegmentRect(int s, float w, float h, float t)
        {
            const float gap = 1f;
            float halfH = h * 0.5f;
            float armH = (h - t * 3f - gap * 2f) * 0.5f;   // the two vertical rows
            float armY = halfH - t - gap - armH * 0.5f;    // centre of the upper row
            float run = w - t * 2f;                        // horizontal bar length
            float side = w * 0.5f - t * 0.5f;              // centre of a vertical bar
            switch (s)
            {
                case 0: return (new Vector2(run, t), new Vector2(0, halfH - t * 0.5f));   // a
                case 1: return (new Vector2(t, armH), new Vector2(side, armY));            // b
                case 2: return (new Vector2(t, armH), new Vector2(side, -armY));           // c
                case 3: return (new Vector2(run, t), new Vector2(0, -halfH + t * 0.5f));  // d
                case 4: return (new Vector2(t, armH), new Vector2(-side, -armY));          // e
                case 5: return (new Vector2(t, armH), new Vector2(-side, armY));           // f
                default: return (new Vector2(run, t), Vector2.zero);                       // g
            }
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

        private static Image NewBar(RectTransform cell, string name, Vector2 size, Vector2 pos, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(cell, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var img = go.AddComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }
    }
}
