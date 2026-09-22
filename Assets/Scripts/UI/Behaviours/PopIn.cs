using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A THING THAT OPENS RATHER THAN APPEARING (2026-09-22, the author: "pembe hoverin acilmasi icin bir
    /// animasyon ekle", "konusma baloncuklarinin olusmasi icin bir animasyon ekle").
    ///
    /// Scales a rect from a fraction of its size up to its own about its OWN PIVOT, with a small overshoot at
    /// the end, and then stops touching it. Nothing else: whoever owns the rect still owns its position, its
    /// alpha and its size, so this can be bolted onto a balloon that is re-laid every frame or a card whose box
    /// is measured from its text without either of them knowing.
    ///
    /// Which axes it opens on is the caller's: a speech balloon grows out of its tail on both, a hover card
    /// opens like a blind on the vertical alone so the bottle lying in its slot is not stretched with it.
    ///
    /// Reduced motion snaps it to full size - what is being said is that the thing is THERE, and an opening is
    /// how it says so, not what it says.
    /// </summary>
    public sealed class PopIn : MonoBehaviour
    {
        /// <summary>How long the opening takes.</summary>
        public float Seconds = 0.2f;
        /// <summary>The fraction of full size it starts at.</summary>
        public float From = 0.55f;
        /// <summary>How far past full size it swings on the way, as a fraction.</summary>
        public float Overshoot = 0.08f;
        public bool Horizontal = true, Vertical = true;
        /// <summary>
        /// A BUBBLE, not a card (2026-09-22, the author's seventh list: "müşterilerin kafasının üstündeki balonlar
        /// baloncuk şeklinde patlayarak açılmalı"): it bursts past its size and wobbles back, and the two axes swing
        /// out of step - wide while it is short, tall while it is narrow - which is what a soap bubble does when it
        /// forms and what a card never does.
        /// </summary>
        public bool Bubble;

        private float _t = -1f;

        /// <summary>Opens it, from the top. Safe to call on something already open.</summary>
        public void Play()
        {
            if (Motion.Reduced) { _t = -1f; Apply(1f); return; }
            _t = 0f;
            Apply(From);
        }

        private void Apply(float s)
        {
            transform.localScale = new Vector3(Horizontal ? s : 1f, Vertical ? s : 1f, 1f);
        }

        private void OnDisable()
        {
            // A rect that goes down mid-open comes back at its own size, not at a fifth of it.
            _t = -1f;
            Apply(1f);
        }

        private void LateUpdate()
        {
            if (_t < 0f) return;
            _t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.0001f, Seconds));
            if (k >= 1f) { _t = -1f; Apply(1f); return; }
            if (Bubble)
            {
                float decay = Mathf.Exp(-6.5f * k) * (1f - From);
                float sy = 1f - decay * Mathf.Cos(k * Mathf.PI * 3.2f);
                float sx = 1f - decay * Mathf.Cos(k * Mathf.PI * 3.2f + 1.1f);
                transform.localScale = new Vector3(Horizontal ? sx : 1f, Vertical ? sy : 1f, 1f);
                return;
            }
            float ease = 1f - Mathf.Pow(1f - k, 3f);                       // out-cubic: quick, then settling
            Apply(Mathf.LerpUnclamped(From, 1f, ease) + Overshoot * Mathf.Sin(k * Mathf.PI) * (1f - k));
        }
    }
}
