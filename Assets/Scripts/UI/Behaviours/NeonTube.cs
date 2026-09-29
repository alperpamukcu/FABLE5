using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// ONE TUBE OF THE MENUS' NEON (2026-09-29): a bent line of glass as its four rings - the glass, the rim either side
    /// of it and two flat bands of its light (ChromeArt.NeonRing: NeonPath, NeonBar, MenuArt.FrameTube / NeonLine, the
    /// icons' marks) - stacked as four white masks and run between its states by TINT alone, the way the curtain's
    /// night sign runs (TycoonHud.SignNeon). The numbers are the sign's own; this is the menus' copy of it rather than
    /// the sign promoted, because nothing photographs the curtain to prove a promotion left it pixel-identical (the
    /// critic, 2026-09-29) - dedupe the two with a before/after capture.
    ///   DARK  unlit glass: Night[3] rim, Night[2] glass, no light (a key that cannot be pressed; a tube before it strikes)
    ///   HALF  the hue's [2] rim round its [3] glass, no light (a key at rest)
    ///   LIT   the hue's [3] rim round a Cream[4] glass, its light in two flat bands of its [2] at .30 and .12
    ///   INK   an unlit tube lying on a light plate: the glass in the word's ink, the rim the plate's [1]
    /// </summary>
    public class NeonTube
    {
        public readonly RectTransform Rt;
        private readonly Image _glow2, _glow1, _rim, _core;

        /// <param name="size">The tube's rect in units: its sprite's texels (line + 2 x NeonPad) times two.</param>
        public NeonTube(RectTransform parent, string name, Func<ChromeArt.NeonLayer, Sprite> art, Vector2 size,
            Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Rt = (RectTransform)go.transform;
            Rt.SetParent(parent, false);
            Rt.anchorMin = Rt.anchorMax = anchor;
            Rt.pivot = new Vector2(0.5f, 0.5f);
            Rt.sizeDelta = size;
            Rt.anchoredPosition = pos;
            _glow2 = Layer("Glow2", art(ChromeArt.NeonLayer.Glow2));
            _glow1 = Layer("Glow1", art(ChromeArt.NeonLayer.Glow1));
            _rim = Layer("Rim", art(ChromeArt.NeonLayer.Rim));
            _core = Layer("Core", art(ChromeArt.NeonLayer.Core));
        }

        private Image Layer(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rt, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public bool Visible
        {
            get => Rt.gameObject.activeSelf;
            set { if (Rt.gameObject.activeSelf != value) Rt.gameObject.SetActive(value); }
        }

        /// <summary>
        /// The tube's state by tint. LIT: glow1 hue[2] @ .30 (glow2 hue[2] @ .12 only with <paramref name="outerGlow"/> -
        /// a mark inside a key keeps its light inside the key's rim, the critic's crowding fix), rim hue[3], glass
        /// Cream[4]. HALF: rim hue[2], glass hue[3], no light. DARK: rim Night[3], glass Night[2].
        /// </summary>
        public void Show(NeonIcons.State state, Color[] hue, bool outerGlow)
        {
            bool lit = state == NeonIcons.State.Lit;
            _glow1.enabled = lit;
            _glow2.enabled = lit && outerGlow;
            if (lit)
            {
                _glow2.color = new Color(hue[2].r, hue[2].g, hue[2].b, 0.12f);
                _glow1.color = new Color(hue[2].r, hue[2].g, hue[2].b, 0.30f);
                _rim.color = hue[3];
                _core.color = UITheme.Cream[4];
            }
            else if (state == NeonIcons.State.Half)
            {
                _rim.color = hue[2];
                _core.color = hue[3];
            }
            else
            {
                _rim.color = UITheme.Night[3];
                _core.color = UITheme.Night[2];
            }
        }

        /// <summary>On a light plate (amber enamel): an unlit tube lying on it - the glass in the word's own ink, its
        /// rim a step down the plate's ramp.</summary>
        public void Ink(Color glass, Color rim)
        {
            _glow1.enabled = _glow2.enabled = false;
            _rim.color = rim;
            _core.color = glass;
        }
    }
}
