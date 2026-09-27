using UnityEngine;
using UnityEngine.UI;

namespace LastCall.UI
{
    /// <summary>
    /// A NEON SIGN'S BREATH, FOR A PICTURE (2026-09-27, the front door's redesign): NeonFlicker's
    /// sibling for a Graphic that is not a Text - the title lockup. The sign holds full, and every
    /// few seconds its tube sags a single step and comes back: low contrast (never under
    /// <see cref="Low"/>), slow, on the unscaled clock (the menu holds the game's own), from a fixed
    /// curve with no random numbers. Still at full under REDUCED MOTION and with FLASHES off.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public sealed class NeonPulse : MonoBehaviour
    {
        /// <summary>The dimmest the sign ever reads, as a share of its own alpha.</summary>
        public float Low = 0.84f;

        /// <summary>One breath's length in seconds.</summary>
        public float Period = 4.6f;

        private Graphic _graphic;
        private float _alpha = 1f;

        private void OnEnable()
        {
            _graphic = GetComponent<Graphic>();
            if (_graphic != null) _alpha = _graphic.color.a;
        }

        private void Update()
        {
            if (_graphic == null) return;
            float k = 1f;
            if (!Motion.NoFlashes)
            {
                // Mostly held: the sag is the top of a narrow bump, a third of the period wide.
                float phase = Mathf.Repeat(Time.unscaledTime / Period, 1f);
                float bump = Mathf.Clamp01(1f - Mathf.Abs(phase - 0.8f) / 0.17f);
                k = 1f - (1f - Low) * bump * bump;
            }
            var c = _graphic.color;
            float want = _alpha * k;
            if (!Mathf.Approximately(c.a, want)) { c.a = want; _graphic.color = c; }
        }
    }
}
