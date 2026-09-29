using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A SCREEN'S TITLE, LIT AS A SIGN (2026-09-29, the menus rebuilt in the week's language): the display face drawn
    /// the way the logo draws its script - a Cream[4] glass, a one-face-pixel lining of the ramp's [3] - inside the
    /// CLOSED sign's four flat bands of light (ChromeArt.ClosedNeon: reach 6/4/3/2 of the face's own pixels, ramp
    /// [0]/[0]/[1]/[2] at .30/.45/.55/.65), grown four-way - a diamond, as ClosedNeon grows them - so every band lands
    /// on the letters' own grid. Never a smooth ramp.
    ///
    /// A mesh effect on the Text: each glyph's quad is copied once per offset of each band's ring, widest band first,
    /// then the lining, then the glass itself on top. Every copy is OPAQUE and PRE-BLENDED over the <see cref="Ground"/>
    /// the word really stands on (the critic, 2026-09-29: a band baked over Night[2] drew a box on the credits' Night[1]
    /// recess), which is how ClosedNeon bakes its light - so the word is flat tokens and token blends, and the light
    /// stops wherever the caller's RectMask2D stops it: under a crown it is clipped to the crown's face, below its rim
    /// ("light on a wall stops where the plate does").
    ///
    /// States, as the tubes run them: LIT; HALF (one band of [0] at .45, lining [2], glass [4]) and DARK (lining
    /// Night[3], glass Night[4]) for the strike when a screen opens (NeonStrike).
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class NeonWord : BaseMeshEffect
    {
        /// <summary>How far the widest band reaches, in the face's own pixels.</summary>
        public const int Reach = 6;

        /// <summary>The ramp the word is lit in (the house's Magenta).</summary>
        public Color[] Ramp = UITheme.Magenta;

        /// <summary>The token the word stands on; every band is baked over it.</summary>
        public Color Ground = UITheme.Night[2];

        private NeonIcons.State _state = NeonIcons.State.Lit;

        public NeonIcons.State Shown => _state;

        public void Show(NeonIcons.State state)
        {
            if (state == _state) return;
            _state = state;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        /// <summary>One of the face's pixels, in units: 3 for the display face at 24, 2 at 16 (a 12-px face at 24: 2).</summary>
        public static int Unit(Text text) =>
            text == null ? 1 : Mathf.Max(1, text.fontSize / LanguageFonts.Grid(text.font));

        /// <summary>The light's reach in units, off the glyphs' ink on every side.</summary>
        public static int ReachUnits(Text text) => Reach * Unit(text);

        private static readonly List<UIVertex> In = new List<UIVertex>();
        private static readonly List<UIVertex> Out = new List<UIVertex>();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            int unit = Unit(graphic as Text);
            In.Clear();
            vh.GetUIVertexStream(In);
            Out.Clear();
            Color32 g = Ground;
            Color32 r0 = Ramp[0], r1 = Ramp[1], r2 = Ramp[2], r3 = Ramp[3], r4 = Ramp[4];
            switch (_state)
            {
                case NeonIcons.State.Lit:
                {
                    // the chain ClosedNeon bakes: each band over the band under it, widest and faintest first
                    Color32 b6 = Color32.Lerp(g, r0, 0.30f);
                    Color32 b4 = Color32.Lerp(b6, r0, 0.45f);
                    Color32 b3 = Color32.Lerp(b4, r1, 0.55f);
                    Color32 b2 = Color32.Lerp(b3, r2, 0.65f);
                    Ring(4, 6, b6, unit);
                    Ring(3, 4, b4, unit);
                    Ring(2, 3, b3, unit);
                    Ring(1, 2, b2, unit);
                    Ring(0, 1, r3, unit);
                    Ring(-1, 0, UITheme.Cream[4], unit);
                    break;
                }
                case NeonIcons.State.Half:
                    Ring(1, 2, Color32.Lerp(g, r0, 0.45f), unit);
                    Ring(0, 1, r2, unit);
                    Ring(-1, 0, r4, unit);
                    break;
                default:
                    Ring(0, 1, UITheme.Night[3], unit);
                    Ring(-1, 0, UITheme.Night[4], unit);
                    break;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Out);
        }

        /// <summary>Every glyph copied at each offset whose four-way distance is over <paramref name="inner"/> and up to
        /// <paramref name="outer"/> face pixels, in <paramref name="colour"/>. The rings nest: whatever a narrower band
        /// covers it paints over afterwards, so each texel ends in the colour of the nearest band that reaches it.</summary>
        private static void Ring(int inner, int outer, Color32 colour, int unit)
        {
            colour.a = 255;
            for (int dy = -outer; dy <= outer; dy++)
                for (int dx = -outer; dx <= outer; dx++)
                {
                    int d = Mathf.Abs(dx) + Mathf.Abs(dy);
                    if (d <= inner || d > outer) continue;
                    var shift = new Vector3(dx * unit, dy * unit, 0f);
                    for (int i = 0; i < In.Count; i++)
                    {
                        var v = In[i];
                        v.position += shift;
                        v.color = colour;
                        Out.Add(v);
                    }
                }
        }
    }
}
