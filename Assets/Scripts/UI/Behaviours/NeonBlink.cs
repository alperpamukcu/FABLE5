using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// Puts the stage's neon signs on their own blink schedules (GDD 24 §8).
    ///
    /// This is what survives of the layered animated backdrop (2026-07-29 → 2026-07-30). That
    /// layer existed to give the room things a painted picture cannot do — a drifting sky, a
    /// breathing street lamp, rain behind a window. The room the game settled on is indoors and
    /// painted whole: it is its own sky and its own skyline, there is no window to see weather
    /// through, and the rain was cut. Every one of those builders ended up with no caller, so
    /// they are gone and the class is named for the one thing it still does.
    ///
    /// Each sign keeps its own rhythm — nothing gives a fake away faster than two signs blinking
    /// in step. Lit for a long while, dark for an instant: a failing tube stutters, it does not
    /// sit there switched off. Cosmetic only; never touches the run, and stops dead under
    /// <see cref="Motion.Reduced"/>.
    /// </summary>
    public sealed class NeonBlink
    {
        private sealed class Sign
        {
            public Graphic Art;
            public bool Lit;
            public float Timer, MinOn, MaxOn, DimAlpha, FullAlpha;
            // The tube's spill into the room (2026-08-10): a real Light2D that rides the
            // SAME schedule as its lettering. A sign and its light on separate rhythms is
            // the fake this class exists to avoid, so the light is a passenger on the
            // sign's own entry, never a registration of its own.
            public UnityEngine.Rendering.Universal.Light2D Spill;
            public float SpillIntensity;
        }

        private readonly List<Sign> _signs = new List<Sign>();

        /// <summary>Puts an already-built graphic — a lettered sign, say — on the blink. Its
        /// current alpha is taken as "lit", so a deliberately faint glow layer stays faint.
        /// A Light2D passed with it stutters in step, scaled by the lettering's own alpha.</summary>
        public void Register(Graphic art, float minOn = 2.5f, float maxOn = 7f, float dim = 0.22f,
            UnityEngine.Rendering.Universal.Light2D spill = null)
        {
            if (art == null) return;
            _signs.Add(new Sign
            {
                Art = art, Lit = true, MinOn = minOn, MaxOn = maxOn,
                FullAlpha = art.color.a, DimAlpha = art.color.a * dim,
                Timer = Random.Range(minOn, maxOn),
                Spill = spill, SpillIntensity = spill != null ? spill.intensity : 0f,
            });
        }

        public void Step(float dt)
        {
            if (Motion.Reduced || dt <= 0f) return;
            for (int i = 0; i < _signs.Count; i++)
            {
                var n = _signs[i];
                if (n.Art == null) continue;
                n.Timer -= dt;
                if (n.Timer <= 0f)
                {
                    n.Lit = !n.Lit;
                    n.Timer = n.Lit ? Random.Range(n.MinOn, n.MaxOn) : Random.Range(0.04f, 0.16f);
                }
                var c = n.Art.color;
                c.a = Mathf.MoveTowards(c.a, n.Lit ? n.FullAlpha : n.DimAlpha,
                                        dt * (n.Lit ? 6f : 18f));
                n.Art.color = c;
                if (n.Spill != null && n.FullAlpha > 0f)
                    n.Spill.intensity = n.SpillIntensity * (c.a / n.FullAlpha);
            }
        }
    }
}
