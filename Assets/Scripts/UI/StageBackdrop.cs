using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// The club backdrop as a set of MOVING layers rather than one painted picture
    /// (2026-07-29, GDD 24 §8). A single image cannot blink, drift or rain, and those are the
    /// things that make a room look occupied rather than photographed.
    ///
    /// Back to front, everything outdoors is clipped to the window so it can move freely
    /// without escaping the frame:
    ///
    ///   sky      slow horizontal drift, wrapping — drawn twice, the second copy MIRRORED, so
    ///            the join is seamless by construction and needs no seamless-tiling art
    ///   city     the skyline, parked (it is a mile away; it does not move)
    ///   street   the wet road across from the window
    ///   lamp     a street lamp whose glow breathes and occasionally stutters
    ///   rain     streaks angled by the wind and re-thrown when they land
    ///   neon     signs blinking on their own schedules, outside and in
    ///
    /// The clip is a MASK SPRITE, not a rectangle: the window is on a side wall and therefore
    /// drawn in perspective, so a rectangular clip would cut its corners off. The mask is the
    /// glass shape cut out of the room art, which guarantees the two agree exactly.
    ///
    /// The wind is one shared, slowly wandering value that leans the rain and hurries the clouds
    /// together, so the weather reads as one thing happening rather than two effects running.
    /// Everything here is cosmetic, never touches the run, and stops dead under Motion.Reduced.
    /// </summary>
    public sealed class StageBackdrop
    {
        private readonly RectTransform _host;    // stage-sized; the room's own space
        private readonly RectTransform _clip;    // masked to the window's shape
        /// <summary>The glass opening in stage units, measured off the art. Everything outdoors
        /// is placed against THIS, not against the stage: the mask throws away anything outside
        /// it, so a layer centred on the room's middle simply vanishes.</summary>
        private readonly Rect _window;

        private RectTransform _skyA, _skyB;
        private float _skyWidth, _skyOffset;

        private readonly List<Neon> _neons = new List<Neon>();
        private readonly List<Drop> _rain = new List<Drop>();
        private Image _lampGlow;

        private float _wind, _windTarget, _windTimer, _time;

        private const float SkyDriftBase = 1.6f;    // units/sec with no wind at all
        private const float SkyDriftWind = 5.5f;    // added at full wind
        private const float RainFall = 240f;
        private const float RainLean = 130f;
        private const int RainCount = 90;

        private sealed class Neon
        {
            public Graphic Art;
            public bool Lit;
            public float Timer, MinOn, MaxOn, DimAlpha, FullAlpha;
        }

        private sealed class Drop
        {
            public RectTransform Rt;
            public Vector2 Pos;
            public float Speed, Length;
        }

        public StageBackdrop(RectTransform host, Sprite windowMask, Rect window)
        {
            _host = host;
            _window = window;

            _clip = NewRect("Window", host);
            Stretch(_clip);
            // Mask by the glass's own silhouette. showMaskGraphic off, so the white shape only
            // ever cuts — it is never seen.
            var maskImg = _clip.gameObject.AddComponent<Image>();
            maskImg.sprite = windowMask;
            maskImg.raycastTarget = false;
            var mask = _clip.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
        }

        // ── layers ──────────────────────────────────────────────────────────────

        /// <summary>The sky, laid out twice — the second copy mirrored, so the wrap has no seam
        /// whatever the art does at its edges.</summary>
        public void AddSky(Sprite sprite)
        {
            if (sprite == null) return;
            _skyWidth = sprite.rect.width;
            _skyA = SkyPiece(sprite, "SkyA", 0f, false);
            _skyB = SkyPiece(sprite, "SkyB", _skyWidth, true);
        }

        private RectTransform SkyPiece(Sprite sprite, string name, float x, bool mirrored)
        {
            var rt = NewRect(name, _clip);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = sprite.rect.size;
            rt.anchoredPosition = new Vector2(_window.xMin + x, _window.yMax - sprite.rect.height);
            if (mirrored) rt.localScale = new Vector3(-1f, 1f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.raycastTarget = false;
            return rt;
        }

        /// <summary>A still layer behind the glass. <paramref name="anchor"/> is where on the
        /// host it sits (fractions), <paramref name="yFraction"/> how far up from its bottom.</summary>
        public void AddStill(Sprite sprite, string name, float yAboveWindowFloor)
        {
            if (sprite == null) return;
            var rt = NewRect(name, _clip);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = sprite.rect.size;
            // Centred on the window, not on the room, and hung from the window's floor.
            rt.anchoredPosition = new Vector2(_window.center.x - sprite.rect.width * 0.5f,
                                              _window.yMin + yAboveWindowFloor);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.raycastTarget = false;
        }

        /// <summary>The street lamp. Its glow breathes, and now and then it stutters.</summary>
        public void AddLamp(Sprite sprite, float xFraction, float yAboveWindowFloor, float scale)
        {
            if (sprite == null) return;
            var rt = NewRect("StreetLamp", _clip);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = sprite.rect.size * scale;
            rt.anchoredPosition = new Vector2(
                _window.xMin + _window.width * xFraction - rt.sizeDelta.x * 0.5f,
                _window.yMin + yAboveWindowFloor);
            _lampGlow = rt.gameObject.AddComponent<Image>();
            _lampGlow.sprite = sprite; _lampGlow.raycastTarget = false;
        }

        /// <summary>
        /// A neon sign. <paramref name="outside"/> puts it behind the glass with the weather;
        /// otherwise it hangs on the bar's own wall. Each keeps its own rhythm — nothing gives a
        /// fake away faster than two signs blinking in step.
        /// </summary>
        public Graphic AddNeon(Sprite sprite, Vector2 centreInStageUnits, bool outside,
            float scale = 1f, float minOn = 2.5f, float maxOn = 7f, float dim = 0.22f)
        {
            if (sprite == null) return null;
            var rt = NewRect("Neon", outside ? _clip : _host);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.sizeDelta = sprite.rect.size * scale;
            rt.anchoredPosition = centreInStageUnits - rt.sizeDelta * 0.5f;
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.raycastTarget = false;
            RegisterNeon(img, minOn, maxOn, dim);
            return img;
        }

        /// <summary>Puts an already-built graphic — a lettered sign, say — on the blink. Its
        /// current alpha is taken as "lit", so a deliberately faint glow layer stays faint.</summary>
        public void RegisterNeon(Graphic art, float minOn = 2.5f, float maxOn = 7f, float dim = 0.22f)
        {
            if (art == null) return;
            _neons.Add(new Neon
            {
                Art = art, Lit = true, MinOn = minOn, MaxOn = maxOn,
                FullAlpha = art.color.a, DimAlpha = art.color.a * dim,
                Timer = Random.Range(minOn, maxOn),
            });
        }

        /// <summary>Rain, as streaks that fall behind the glass and are re-thrown on landing.</summary>
        public void AddRain(Color colour)
        {
            var size = _window.size;
            for (int i = 0; i < RainCount; i++)
            {
                var rt = NewRect("Drop", _clip);
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                float len = Random.Range(6f, 14f);
                rt.sizeDelta = new Vector2(1f, len);
                var img = rt.gameObject.AddComponent<Image>();
                img.color = new Color(colour.r, colour.g, colour.b, Random.Range(0.16f, 0.40f));
                img.raycastTarget = false;
                _rain.Add(new Drop
                {
                    Rt = rt, Length = len, Speed = Random.Range(0.75f, 1.35f),
                    Pos = new Vector2(Random.Range(-40f, size.x + 40f), Random.Range(0f, -size.y)),
                });
            }
        }

        // ── the frame ───────────────────────────────────────────────────────────

        public void Step(float dt)
        {
            if (Motion.Reduced || dt <= 0f) return;
            _time += dt;

            _windTimer -= dt;
            if (_windTimer <= 0f)
            {
                _windTarget = Random.Range(-1f, 1f);
                _windTimer = Random.Range(4f, 11f);
            }
            _wind = Mathf.Lerp(_wind, _windTarget, 1f - Mathf.Exp(-0.6f * dt));

            DriftSky(dt);
            FallRain(dt);
            BlinkNeons(dt);
            BreatheLamp(dt);
        }

        private void DriftSky(float dt)
        {
            if (_skyA == null) return;
            _skyOffset -= (SkyDriftBase + SkyDriftWind * _wind) * dt;
            // Wrap BOTH ways: the wind reverses, and a one-way wrap tears the seam open the
            // first time the clouds come back.
            if (_skyOffset <= -_skyWidth) _skyOffset += _skyWidth;
            if (_skyOffset > 0f) _skyOffset -= _skyWidth;
            _skyA.anchoredPosition = new Vector2(_skyOffset, 0f);
            _skyB.anchoredPosition = new Vector2(_skyOffset + _skyWidth * 2f, 0f);
        }

        private void FallRain(float dt)
        {
            var size = _window.size;
            float lean = RainLean * _wind;
            float angle = Mathf.Atan2(lean, RainFall) * Mathf.Rad2Deg;
            for (int i = 0; i < _rain.Count; i++)
            {
                var d = _rain[i];
                d.Pos.x += lean * d.Speed * dt;
                d.Pos.y -= RainFall * d.Speed * dt;
                if (d.Pos.y < -size.y - d.Length || d.Pos.x < -60f || d.Pos.x > size.x + 60f)
                    d.Pos = new Vector2(Random.Range(-40f, size.x + 40f), Random.Range(0f, 24f));
                // Drop positions are kept window-relative and offset on the way out, so the
                // wrap logic never has to know where the window sits.
                d.Rt.anchoredPosition = new Vector2(_window.xMin + d.Pos.x, _window.yMax + d.Pos.y);
                d.Rt.localRotation = Quaternion.Euler(0, 0, -angle);
            }
        }

        private void BlinkNeons(float dt)
        {
            for (int i = 0; i < _neons.Count; i++)
            {
                var n = _neons[i];
                if (n.Art == null) continue;
                n.Timer -= dt;
                if (n.Timer <= 0f)
                {
                    n.Lit = !n.Lit;
                    // Lit for a long while, dark for an instant: a failing tube stutters, it does
                    // not sit there switched off.
                    n.Timer = n.Lit ? Random.Range(n.MinOn, n.MaxOn) : Random.Range(0.04f, 0.16f);
                }
                var c = n.Art.color;
                c.a = Mathf.MoveTowards(c.a, n.Lit ? n.FullAlpha : n.DimAlpha,
                                        dt * (n.Lit ? 6f : 18f));
                n.Art.color = c;
            }
        }

        private void BreatheLamp(float dt)
        {
            if (_lampGlow == null) return;
            float breath = 0.94f + 0.06f * Mathf.Sin(_time * 1.7f);
            float tremor = 0.02f * Mathf.Sin(_time * 11.3f) * Mathf.Sin(_time * 4.1f);
            float k = breath + tremor;
            if (Random.value < 0.004f) k *= 0.55f;   // a rare hard flicker
            var c = _lampGlow.color;
            c.a = Mathf.Lerp(c.a, Mathf.Clamp01(k), 1f - Mathf.Exp(-14f * dt));
            _lampGlow.color = c;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }
    }
}
